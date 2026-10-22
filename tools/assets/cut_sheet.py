#!/usr/bin/env python3
"""
Recorta uma folha de objetos RGBA (fundo transparente) em objetos soltos, de forma determinística.

Etapas:
  1. Máscara: alpha >= --seg (o "véu" semitransparente que as folhas geradas trazem em volta dos objetos fica de fora).
  2. Fusão de partes do mesmo objeto: a máscara é dilatada em --gap px antes de rotular componentes conectados
     (8-vizinhança), então sombra, brilho, chamas e faíscas a até --gap px viram um objeto só. Depois, peças
     pequenas (< --sat-ratio da área do vizinho) a até --sat-gap px de um objeto maior são anexadas a ele.
  3. Classificação: ruído (área < --min-area), TEXTO/rótulo (linha baixa feita de >= 3 glifos, ou faixa muito
     baixa e larga) e PAINEL (retângulo opaco atrás de quadros de animação). Painéis são abertos com --panels split:
     o fundo do painel (cor da borda / alpha mais baixo) é removido e o miolo é recortado de novo.
  4. Limpeza de borda: alpha remapeado de [--alpha-lo, --alpha-hi] para [0, 255] (núcleo 100% opaco, véu zerado) e
     a cor dos pixels semitransparentes da borda é trocada pela média dos pixels opacos vizinhos (raio 2): sem halo
     escuro nem claro do fundo em que a arte foi gerada. Brilhos longe do núcleo mantêm a cor.
  5. Recorte justo + margem transparente de --margin px.

Saídas (em --out): <folha>_NNN.png por objeto, manifest.json (número, bbox na folha, área, classe, arquivo) e
duas folhas de conferência: contact.png (miniaturas numeradas, com as descartadas marcadas) e overview.png
(a folha inteira com as caixas e números por cima).

Uso:
  python -I tools/assets/cut_sheet.py <folha.png> --out <pasta> [--gap 1] [--seg 110] [--min-area 120]
         [--sat-gap 6] [--sat-ratio 0.12] [--panels split|drop|keep] [--text-max-h 20]

Também é uma biblioteca: `cut(image, Params()) -> list[Piece]` (usado por build_objects.py).
Só depende do Pillow. Entrada não confiável: só lê pixels; nunca executa nada da folha.
"""
from __future__ import annotations

import argparse
import json
import re
import sys

from dataclasses import dataclass, field, asdict
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageMath

sys.dont_write_bytecode = True  # roda com -I; não deixa __pycache__ em tools/

ROOT = Path(__file__).resolve().parents[2]
FONT_PATH = ROOT / "assets" / "fonts" / "VT323-Regular.ttf"
_RUN = re.compile(rb"[^\x00]+")


@dataclass
class Params:
    seg: int = 110           # alpha mínimo para entrar na máscara de segmentação
    gap: int = 1             # px de dilatação para fundir partes do mesmo objeto
    min_area: int = 120      # px opacos mínimos; menos que isso é ruído
    sat_gap: int = 6         # px: peças pequenas até essa distância de um objeto maior são anexadas (0 = desliga)
    sat_ratio: float = 0.12  # "pequena" = área < sat_ratio × área do vizinho
    alpha_lo: int = 24       # alpha <= lo vira 0
    alpha_hi: int = 228      # alpha >= hi vira 255
    margin: int = 2          # margem transparente do recorte
    text_max_h: int = 20     # altura máxima (px) de uma linha de texto
    panels: str = "split"    # split = abre o painel e recorta o miolo; drop = descarta; keep = mantém como objeto
    panel_tol: int = 60      # distância de cor (RGB) ao fundo do painel para considerar fundo
    defringe_radius: int = 2


@dataclass
class Piece:
    n: int
    bbox: tuple[int, int, int, int]          # x, y, w, h (justo, na folha, sem margem)
    area: int
    cls: str                                 # object | text | noise | panel
    glyphs: int = 0
    from_panel: bool = False
    mask: Image.Image | None = field(default=None, repr=False)   # 'L' do tamanho do bbox: pixels do objeto
    source: Image.Image | None = field(default=None, repr=False)  # folha de onde recortar (com painéis já abertos)

    @property
    def center(self) -> tuple[float, float]:
        x, y, w, h = self.bbox
        return x + w / 2, y + h / 2

    def contains(self, px: float, py: float) -> bool:
        x, y, w, h = self.bbox
        return x <= px < x + w and y <= py < y + h


# --- rotulagem por corridas (run-length + union-find), rápida em Python puro --------------------------------------

def _label(mask: Image.Image) -> list[list[tuple[int, int, int]]]:
    """Componentes 8-conectados de uma máscara 'L' (!= 0). Devolve, por componente, a lista de corridas (y, x0, x1)."""
    w, h = mask.size
    data = mask.tobytes()
    parent: list[int] = []

    def find(a: int) -> int:
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    runs: list[tuple[int, int, int, int]] = []  # y, x0, x1, label
    prev: list[tuple[int, int, int]] = []
    for y in range(h):
        row = data[y * w:(y + 1) * w]
        cur: list[tuple[int, int, int]] = []
        j = 0
        for m in _RUN.finditer(row):
            x0, x1 = m.start(), m.end()
            lab = -1
            while j < len(prev) and prev[j][1] < x0:  # termina antes de x0-1 → não toca (8-viz: x1 >= x0)
                j += 1
            k = j
            while k < len(prev) and prev[k][0] <= x1:
                pl = prev[k][2]
                if lab < 0:
                    lab = find(pl)
                else:
                    ra, rb = find(lab), find(pl)
                    if ra != rb:
                        parent[max(ra, rb)] = min(ra, rb)
                        lab = min(ra, rb)
                k += 1
            if lab < 0:
                lab = len(parent)
                parent.append(lab)
            cur.append((x0, x1, lab))
            runs.append((y, x0, x1, lab))
        prev = cur
    groups: dict[int, list[tuple[int, int, int]]] = {}
    for y, x0, x1, lab in runs:
        groups.setdefault(find(lab), []).append((y, x0, x1))
    return list(groups.values())


def _runs_bbox(runs: list[tuple[int, int, int]]) -> tuple[int, int, int, int]:
    x0 = min(r[1] for r in runs)
    x1 = max(r[2] for r in runs)
    y0 = min(r[0] for r in runs)
    y1 = max(r[0] for r in runs) + 1
    return x0, y0, x1 - x0, y1 - y0


def _runs_mask(runs: list[tuple[int, int, int]], bbox: tuple[int, int, int, int]) -> Image.Image:
    x, y, w, h = bbox
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    for ry, x0, x1 in runs:
        d.line([(x0 - x, ry - y), (x1 - 1 - x, ry - y)], fill=255)
    return m


def _count(mask: Image.Image) -> int:
    return sum(mask.histogram()[1:])


# --- segmentação ---------------------------------------------------------------------------------------------------

@dataclass
class _Comp:
    bbox: tuple[int, int, int, int]   # justo (pixels da máscara base)
    mask: Image.Image                 # 'L' do tamanho de bbox
    area: int


def _components(alpha: Image.Image, origin: tuple[int, int], p: Params) -> list[_Comp]:
    base = alpha.point(lambda v: 255 if v >= p.seg else 0)
    grown = base.filter(ImageFilter.MaxFilter(2 * p.gap + 1)) if p.gap > 0 else base
    comps: list[_Comp] = []
    for runs in _label(grown):
        gb = _runs_bbox(runs)
        region = _runs_mask(runs, gb)
        gx, gy, gw, gh = gb
        own = ImageChops.multiply(base.crop((gx, gy, gx + gw, gy + gh)), region)
        tight = own.getbbox()
        if tight is None:
            continue
        own = own.crop(tight)
        bx, by = gx + tight[0], gy + tight[1]
        comps.append(_Comp((bx + origin[0], by + origin[1], tight[2] - tight[0], tight[3] - tight[1]), own, _count(own)))
    return comps


def _bbox_gap(a: tuple[int, int, int, int], b: tuple[int, int, int, int]) -> float:
    ax, ay, aw, ah = a
    bx, by, bw, bh = b
    dx = max(0, max(ax, bx) - min(ax + aw, bx + bw))
    dy = max(0, max(ay, by) - min(ay + ah, by + bh))
    return max(dx, dy)


def _union(a: _Comp, b: _Comp) -> _Comp:
    x0 = min(a.bbox[0], b.bbox[0])
    y0 = min(a.bbox[1], b.bbox[1])
    x1 = max(a.bbox[0] + a.bbox[2], b.bbox[0] + b.bbox[2])
    y1 = max(a.bbox[1] + a.bbox[3], b.bbox[1] + b.bbox[3])
    m = Image.new("L", (x1 - x0, y1 - y0), 0)
    for c in (a, b):
        m.paste(255, (c.bbox[0] - x0, c.bbox[1] - y0), c.mask)
    return _Comp((x0, y0, x1 - x0, y1 - y0), m, a.area + b.area)


def _mask_gap_ok(small: _Comp, big: _Comp, gap: int) -> bool:
    """Algum pixel de `big` está a até `gap` px (quadrado) de algum pixel de `small`?"""
    sx, sy, sw, sh = small.bbox
    grown = Image.new("L", (sw + 2 * gap, sh + 2 * gap), 0)
    grown.paste(255, (gap, gap), small.mask)
    grown = grown.filter(ImageFilter.MaxFilter(2 * gap + 1))
    bx, by, bw, bh = big.bbox
    region = Image.new("L", grown.size, 0)
    region.paste(big.mask, (bx - (sx - gap), by - (sy - gap)))
    return ImageChops.multiply(grown, region).getbbox() is not None


def merge_satellites(comps: list[_Comp], p: Params) -> list[_Comp]:
    """
    Anexa peças pequenas (chamas, faíscas, pedrinhas flutuando) ao objeto maior mais próximo, medindo a distância
    entre pixels (não entre caixas). Uma passada só, contra os objetos originais: a fusão não "cresce" em cascata.
    """
    if p.sat_gap <= 0:
        return comps
    order = sorted(range(len(comps)), key=lambda i: comps[i].area)
    merged: dict[int, _Comp] = {i: c for i, c in enumerate(comps)}
    absorbed: set[int] = set()
    for i in order:
        small = comps[i]
        best, best_area = None, -1
        for j, big in enumerate(comps):
            if j == i or j in absorbed or big.area * p.sat_ratio <= small.area:
                continue
            if _bbox_gap(small.bbox, big.bbox) > p.sat_gap:
                continue
            if big.area > best_area and _mask_gap_ok(small, big, p.sat_gap):
                best, best_area = j, big.area
        if best is not None:
            merged[best] = _union(merged[best], merged[i])
            absorbed.add(i)
    return [merged[i] for i in range(len(comps)) if i not in absorbed]


def _glyphs(c: _Comp) -> tuple[int, int]:
    """Quantas peças (sem dilatação) há no componente e a maior altura entre elas."""
    pieces = [r for r in _label(c.mask)]
    tall = 0
    count = 0
    for runs in pieces:
        if len(runs) == 0:
            continue
        _, _, _, h = _runs_bbox(runs)
        count += 1
        tall = max(tall, h)
    return count, tall


def _ring(img: Image.Image, bbox: tuple[int, int, int, int], inset: int = 3) -> list[tuple[int, int, int, int]]:
    x, y, w, h = bbox
    px = img.load()
    ring = []
    for i in range(x + inset, x + w - inset, 2):
        ring.append(px[i, y + inset])
        ring.append(px[i, y + h - 1 - inset])
    for j in range(y + inset, y + h - inset, 2):
        ring.append(px[x + inset, j])
        ring.append(px[x + w - 1 - inset, j])
    return ring


def _is_panel(c: _Comp, img: Image.Image) -> bool:
    """
    Painel = retângulo quase cheio cujos cantos são fundo: sem cor, parecidos entre si (3 de 4 bastam; o objeto pode
    encostar num canto) e translúcidos ou quase pretos/brancos. Tapetes e portas têm cor e são opacos.
    """
    x, y, w, h = c.bbox
    if w < 32 or h < 32 or c.area < 0.8 * w * h:
        return False
    m = c.mask
    if sum(1 for pt in ((2, 2), (w - 3, 2), (2, h - 3), (w - 3, h - 3)) if m.getpixel(pt)) < 3:
        return False
    px = img.load()
    samples = []
    for cx, cy in ((x + 4, y + 4), (x + w - 5, y + 4), (x + 4, y + h - 5), (x + w - 5, y + h - 5)):
        patch = [px[cx + dx, cy + dy] for dy in (-1, 0, 1) for dx in (-1, 0, 1)]
        samples.append(tuple(sum(q[k] for q in patch) / 9 for k in range(4)))
    gray = [s for s in samples if max(s[:3]) - min(s[:3]) <= 36]
    best: list[tuple[float, ...]] = []
    for a in gray:
        group = [b for b in gray if sum((a[k] - b[k]) ** 2 for k in range(3)) < 48 * 48]
        if len(group) > len(best):
            best = group
    if len(best) < 3:
        return False
    lum = sum(sum(s[:3]) / 3 for s in best) / len(best)
    alpha = sum(s[3] for s in best) / len(best)
    return alpha <= 225 or lum < 40 or lum > 200


def _classify(c: _Comp, img: Image.Image, p: Params) -> tuple[str, int]:
    x, y, w, h = c.bbox
    if c.area < p.min_area:
        return "noise", 0
    if _is_panel(c, img):
        return "panel", 0
    if h <= p.text_max_h * 1.6:
        count, tall = _glyphs(c)
        if count >= 3 and tall <= p.text_max_h and w >= 2 * h:
            return "text", count
        if h <= p.text_max_h * 0.6 and w >= 6 * h and c.area < 0.5 * w * h:
            return "text", count
    return "object", 0


def _open_panel(img: Image.Image, c: _Comp, p: Params) -> Image.Image:
    """Remove o fundo do painel (cor da borda interna e alpha do painel) e devolve o recorte RGBA do miolo."""
    x, y, w, h = c.bbox
    crop = img.crop((x, y, x + w, y + h))
    px = crop.load()
    ring = _ring(img, c.bbox)

    def median(vals: list[int]) -> int:
        s = sorted(vals)
        return s[len(s) // 2]

    bg = tuple(median([r[k] for r in ring]) for k in range(4))
    alpha_cut = bg[3] + 25 if bg[3] < 230 else -1
    bg_lum = sum(bg[:3]) / 3
    tol2 = p.panel_tol * p.panel_tol
    like = Image.new("L", (w, h), 0)
    pl = like.load()
    for j in range(h):
        for i in range(w):
            r, g, b, a = px[i, j]
            d2 = (r - bg[0]) ** 2 + (g - bg[1]) ** 2 + (b - bg[2]) ** 2
            lum, sat = (r + g + b) / 3, max(r, g, b) - min(r, g, b)
            # painel claro: sombras cinza-claras do fundo também contam; painel escuro: o quase preto
            grayish = (bg_lum > 150 and sat <= 28 and lum >= 120) or (bg_lum < 40 and lum < 45)
            if d2 < tol2 or grayish or a <= alpha_cut or i < 2 or j < 2 or i >= w - 2 or j >= h - 2:
                pl[i, j] = 255
    # só sai o "fundo" ligado à borda do painel; brilhos e páginas claras DENTRO do objeto ficam
    out = crop.copy()
    remove = Image.new("L", (w, h), 0)
    rd = ImageDraw.Draw(remove)
    for runs in _label(like):
        if any(ry in (0, h - 1) or x0 == 0 or x1 == w for ry, x0, x1 in runs):
            for ry, x0, x1 in runs:
                rd.line([(x0, ry), (x1 - 1, ry)], fill=255)
    out.putalpha(ImageChops.multiply(out.getchannel("A"), ImageChops.invert(remove)))
    # a orla de 1 px dos objetos ainda é mistura com o fundo do painel: some
    a2 = out.getchannel("A").filter(ImageFilter.MinFilter(3))
    out.putalpha(a2)
    return out


def _sort_key(c: _Comp) -> tuple[int, int]:
    x, y, w, h = c.bbox
    return int((y + h / 2) // 48), x


def cut(img: Image.Image, p: Params | None = None) -> list[Piece]:
    """Recorta a folha inteira em peças classificadas (numeradas de 1, em ordem de linhas)."""
    p = p or Params()
    img = img.convert("RGBA")
    work = img.copy()  # recebe o miolo dos painéis abertos (sem fundo), para o recorte final
    alpha = img.getchannel("A")
    comps = merge_satellites(_components(alpha, (0, 0), p), p)
    out: list[tuple[_Comp, str, int, bool]] = []
    for c in comps:
        x, y, w, h = c.bbox
        cls, glyphs = _classify(c, img, p)
        if cls == "panel" and p.panels == "split":
            inner = _open_panel(img, c, p)
            work.paste(inner, (x, y))
            sub = merge_satellites(_components(inner.getchannel("A"), (x, y), p), p)
            for s in sub:
                scls, sg = _classify(s, img, p)
                out.append((s, "object" if scls == "panel" else scls, sg, True))
            continue
        if cls == "panel" and p.panels == "keep":
            cls = "object"
        out.append((c, cls, glyphs, False))
    out.sort(key=lambda t: _sort_key(t[0]))
    return [Piece(i + 1, t[0].bbox, t[0].area, t[1], t[2], t[3], t[0].mask, work) for i, t in enumerate(out)]


def find_piece(pieces: list[Piece], x: float, y: float) -> Piece | None:
    """Peça (não descartada) cujo bbox contém o ponto; havendo várias, a de pixel no ponto e menor bbox."""
    hits = [pc for pc in pieces if pc.cls == "object" and pc.contains(x, y)]
    if not hits:
        return None

    def on_pixel(pc: Piece) -> bool:
        bx, by, _, _ = pc.bbox
        return pc.mask is not None and pc.mask.getpixel((int(x) - bx, int(y) - by)) > 0

    hits.sort(key=lambda pc: (not on_pixel(pc), pc.bbox[2] * pc.bbox[3]))
    return hits[0]


# --- limpeza e recorte ---------------------------------------------------------------------------------------------

def _remap_alpha(a: Image.Image, lo: int, hi: int) -> Image.Image:
    span = max(1, hi - lo)
    lut = [0 if v <= lo else 255 if v >= hi else int((v - lo) * 255 / span + 0.5) for v in range(256)]
    return a.point(lut)


def defringe(rgba: Image.Image, p: Params) -> Image.Image:
    """Alpha remapeado e cor da borda semitransparente trocada pela média dos opacos vizinhos (sem halo)."""
    r, g, b, a = rgba.split()
    a2 = _remap_alpha(a, p.alpha_lo, p.alpha_hi)
    core = a2.point(lambda v: 255 if v == 255 else 0)
    # o anel mais externo do núcleo ainda carrega a cor do fundo: usa o núcleo erodido de 1 px como fonte de cor
    src = core.filter(ImageFilter.MinFilter(3))
    if _count(src) == 0:
        src = core
    rad = p.defringe_radius
    blur = ImageFilter.BoxBlur(rad)
    cb = src.filter(blur)
    fixed = []
    for ch in (r, g, b):
        prem = ImageChops.multiply(ch, src).filter(blur)
        avg = ImageMath.unsafe_eval("convert(min(c * 255 / max(w, 1), 255), 'L')", c=prem, w=cb)
        fixed.append(avg)
    # borda = semitransparente ou a orla de 1 px do núcleo, com algum opaco por perto
    edge = ImageChops.subtract(a2.point(lambda v: 255 if v > 0 else 0), src)
    edge = ImageChops.multiply(edge, cb.point(lambda v: 255 if v > 0 else 0))
    out = Image.merge("RGBA", (
        Image.composite(fixed[0], r, edge),
        Image.composite(fixed[1], g, edge),
        Image.composite(fixed[2], b, edge),
        a2,
    ))
    # pixels totalmente transparentes ficam pretos (PNG menor e sem cor "fantasma" no filtro bilinear)
    zero = a2.point(lambda v: 255 if v == 0 else 0)
    out.paste((0, 0, 0, 0), (0, 0), zero)
    return out


def extract(img: Image.Image, pieces: list[Piece], p: Params) -> Image.Image:
    """Recorte limpo (RGBA, margem de p.margin) de uma ou mais peças fundidas."""
    x0 = min(pc.bbox[0] for pc in pieces)
    y0 = min(pc.bbox[1] for pc in pieces)
    x1 = max(pc.bbox[0] + pc.bbox[2] for pc in pieces)
    y1 = max(pc.bbox[1] + pc.bbox[3] for pc in pieces)
    pad = p.gap + p.defringe_radius + 1
    src = pieces[0].source or img
    ox, oy = max(0, x0 - pad), max(0, y0 - pad)
    ex, ey = min(src.width, x1 + pad), min(src.height, y1 + pad)
    crop = src.convert("RGBA").crop((ox, oy, ex, ey))
    # só os pixels das peças (dilatadas de leve para trazer a borda suave) entram; vizinhos ficam de fora
    own = Image.new("L", crop.size, 0)
    for pc in pieces:
        own.paste(255, (pc.bbox[0] - ox, pc.bbox[1] - oy), pc.mask)
    own = own.filter(ImageFilter.MaxFilter(2 * p.gap + 1))
    a = ImageChops.multiply(crop.getchannel("A"), own)
    crop.putalpha(a)
    clean = defringe(crop, p)
    box = clean.getchannel("A").getbbox()
    if box is None:
        return Image.new("RGBA", (1, 1), (0, 0, 0, 0))
    clean = clean.crop(box)
    m = p.margin
    out = Image.new("RGBA", (clean.width + 2 * m, clean.height + 2 * m), (0, 0, 0, 0))
    out.paste(clean, (m, m))
    return out


# --- folhas de conferência ----------------------------------------------------------------------------------------

def _font(size: int):
    try:
        return ImageFont.truetype(str(FONT_PATH), size)
    except OSError:
        return ImageFont.load_default()


def checker(size: tuple[int, int], cell: int = 8) -> Image.Image:
    bg = Image.new("RGBA", size, (92, 92, 104, 255))
    d = ImageDraw.Draw(bg)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2:
                d.rectangle([x, y, x + cell - 1, y + cell - 1], fill=(120, 120, 134, 255))
    return bg


def contact_sheet(items: list[tuple[str, Image.Image, str]], cols: int = 10, thumb: int = 128) -> Image.Image:
    """Miniaturas numeradas: (rótulo, imagem RGBA, classe). Descartadas ganham borda vermelha."""
    label_h = 22
    rows = (len(items) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * (thumb + 8) + 8, max(1, rows) * (thumb + label_h + 8) + 8), (24, 22, 34, 255))
    f = _font(20)
    d = ImageDraw.Draw(sheet)
    for i, (label, im, cls) in enumerate(items):
        cx = 8 + (i % cols) * (thumb + 8)
        cy = 8 + (i // cols) * (thumb + label_h + 8)
        cell = checker((thumb, thumb))
        k = min(thumb / max(1, im.width), thumb / max(1, im.height), 2.0)
        t = im.resize((max(1, int(im.width * k)), max(1, int(im.height * k))), Image.LANCZOS if k < 1 else Image.NEAREST)
        cell.alpha_composite(t, ((thumb - t.width) // 2, (thumb - t.height) // 2))
        sheet.paste(cell, (cx, cy))
        color = (255, 220, 120, 255) if cls == "object" else (255, 70, 90, 255)
        if cls != "object":
            d.rectangle([cx, cy, cx + thumb - 1, cy + thumb - 1], outline=color, width=3)
        d.text((cx + 2, cy + thumb + 1), f"{label} {im.width}x{im.height}" if cls == "object" else f"{label} {cls}", fill=color, font=f)
    return sheet


def overview(img: Image.Image, pieces: list[Piece]) -> Image.Image:
    base = Image.new("RGBA", img.size, (128, 128, 128, 255))
    base.alpha_composite(img.convert("RGBA"))
    d = ImageDraw.Draw(base)
    f = _font(18)
    for pc in pieces:
        if pc.cls == "noise":
            continue
        x, y, w, h = pc.bbox
        color = (0, 255, 120, 255) if pc.cls == "object" else (255, 40, 60, 255)
        d.rectangle([x, y, x + w - 1, y + h - 1], outline=color, width=1)
        d.text((x + 1, y), str(pc.n), fill=(255, 255, 0, 255), font=f, stroke_width=2, stroke_fill=(0, 0, 0, 255))
    return base


def main() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("sheet")
    ap.add_argument("--out", required=True)
    d = Params()
    for name, val in asdict(d).items():
        ap.add_argument(f"--{name.replace('_', '-')}", type=type(val), default=val)
    ap.add_argument("--no-files", action="store_true", help="só manifest e folhas de conferência")
    args = ap.parse_args()
    p = Params(**{k: getattr(args, k) for k in asdict(d)})
    src = Path(args.sheet)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    img = Image.open(src).convert("RGBA")
    pieces = cut(img, p)
    stem = src.stem
    items = []
    manifest = {"sheet": src.name, "size": list(img.size), "params": asdict(p), "pieces": []}
    for pc in pieces:
        entry = {"n": pc.n, "bbox": list(pc.bbox), "center": [round(pc.center[0]), round(pc.center[1])], "area": pc.area, "class": pc.cls}
        if pc.from_panel:
            entry["fromPanel"] = True
        if pc.cls != "noise":
            im = extract(img, [pc], p)
            if pc.cls == "object" and not args.no_files:
                name = f"{stem}_{pc.n:03d}.png"
                im.save(out / name, optimize=True)
                entry["file"] = name
                entry["size"] = list(im.size)
            items.append((str(pc.n), im, pc.cls))
        manifest["pieces"].append(entry)
    (out / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    contact_sheet(items).convert("RGB").save(out / "contact.png")
    overview(img, pieces).convert("RGB").save(out / "overview.png")
    counts: dict[str, int] = {}
    for pc in pieces:
        counts[pc.cls] = counts.get(pc.cls, 0) + 1
    print(f"{src.name}: {len(pieces)} peças " + ", ".join(f"{k}={v}" for k, v in sorted(counts.items())) + f" → {out}")


if __name__ == "__main__":
    main()
