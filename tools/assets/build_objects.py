#!/usr/bin/env python3
"""
Gera a biblioteca de objetos do mundo a partir das folhas curadas.

Lê `tools/assets/objects_curation.json` (quais peças de cada folha viram objeto, nome, tema, kind e calibração),
recorta as folhas de `docs/brand/source/objects/NN.png` com `cut_sheet.py` e grava:

  assets/sprites/objects/<tema>/<nome>.png   recorte limpo (margem 2 px, sem halo), reduzido se passar de maxDensity px/u
  src/Data/objects.json                       catálogo lido pelo Core (`ObjectCatalog`)
  docs/design/catalogo/<tema>.jpg             imagem-resumo do tema: cada objeto no tamanho calibrado ao lado do jogador e
                                              de uma porta (com --docs)

Calibração: a altura (ou a largura, nos pisos) de cada objeto vem do kind (`kinds.<kind>.h` × altura do jogador) ou da
opção `h`/`w` do objeto. Âncora = centro da base do recorte (pé, como DECISIONS D10). Footprint = retângulo em u
relativo à âncora (largura e profundidade em fração da largura do objeto). Luz: cor/raio do kind; posição = centroide dos
pixels mais claros do recorte (a chama/o cristal).

Uso:
  python -I tools/assets/build_objects.py [--src pasta] [--docs] [--only tema] [--check]
  --check: não grava nada; só confere se as peças existem e se o objects.json do repositório está atualizado.
"""
from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))

from PIL import Image, ImageDraw  # noqa: E402

import cut_sheet  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
CURATION = ROOT / "tools" / "assets" / "objects_curation.json"
SRC_DEFAULT = ROOT / "docs" / "brand" / "source" / "objects"
OUT_SPRITES = ROOT / "assets" / "sprites" / "objects"
OUT_JSON = ROOT / "src" / "Data" / "objects.json"
OUT_DOCS = ROOT / "docs" / "design" / "catalogo"
ATLAS_PNG = ROOT / "assets" / "sprites" / "atlas" / "atlas.png"
ATLAS_JSON = ROOT / "assets" / "sprites" / "atlas" / "atlas.json"

FIELDS = ("h", "w", "foot", "layer", "light", "sway", "interaction", "ambient", "material", "occluder", "note")
LAYERS = ("entity", "floor", "wall", "overhead", "fx")


def r1(v: float) -> float:
    """Uma casa decimal, estável entre plataformas (floor(x*10+0.5)/10)."""
    return math.floor(v * 10 + 0.5) / 10


def used_box(im: Image.Image) -> tuple[int, int, int, int]:
    box = im.getchannel("A").getbbox()
    return box if box else (0, 0, im.width, im.height)


def resize_premultiplied(im: Image.Image, size: tuple[int, int]) -> Image.Image:
    """Redução sem halo: reamostra em alpha pré-multiplicado."""
    return im.convert("RGBa").resize(size, Image.LANCZOS).convert("RGBA")


def light_spot(im: Image.Image) -> tuple[float, float]:
    """Centroide (px) dos pixels mais claros e opacos: onde fica a chama, o cristal ou a água brilhante."""
    px = im.load()
    lums = []
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a > 200:
                lums.append((max(r, g, b) + (r + g + b) / 3, x, y))
    if not lums:
        return im.width / 2, im.height / 2
    lums.sort(reverse=True)
    top = lums[: max(4, len(lums) // 40)]
    return sum(t[1] for t in top) / len(top), sum(t[2] for t in top) / len(top)


def resolve(kind: str, opts: dict, kinds: dict) -> dict:
    if kind not in kinds:
        raise SystemExit(f"kind desconhecido: {kind}")
    spec = {k: v for k, v in kinds[kind].items()}
    for k, v in opts.items():
        if k not in FIELDS:
            raise SystemExit(f"opção desconhecida '{k}' (kind {kind})")
        spec[k] = v
    if "w" in opts and "h" not in opts:
        spec.pop("h", None)
    if "h" in opts and "w" not in opts:
        spec.pop("w", None)
    spec.setdefault("layer", "entity")
    if spec["layer"] not in LAYERS:
        raise SystemExit(f"layer inválida: {spec['layer']}")
    return spec


def build_object(name: str, theme: str, kind: str, pieces: list, sheet_id: str, spec: dict, ref: dict,
                 colors: dict) -> tuple[dict, Image.Image]:
    params = cut_sheet.Params(**CUR["params"])
    im = cut_sheet.extract(SHEET_IMG[sheet_id], pieces, params)
    ux0, uy0, ux1, uy1 = used_box(im)
    pw, ph = ux1 - ux0, uy1 - uy0
    player = ref["playerHeight"]
    if spec.get("w") is not None:
        width_u = spec["w"] * player
        height_u = width_u * ph / pw
    else:
        height_u = spec["h"] * player
        width_u = height_u * pw / ph
    density = ph / height_u
    max_d = ref["maxDensity"]
    if density > max_d:
        k = max_d / density
        im = resize_premultiplied(im, (max(1, round(im.width * k)), max(1, round(im.height * k))))
        ux0, uy0, ux1, uy1 = used_box(im)
        pw, ph = ux1 - ux0, uy1 - uy0
        density = ph / height_u
    ax, ay = ux0 + pw / 2, uy1
    entry: dict = {
        "id": name,
        "file": f"res://assets/sprites/objects/{theme}/{name}.png",
        "theme": theme,
        "kind": kind,
        "layer": spec["layer"],
        "source": f"{sheet_id}#{'+'.join(str(p.n) for p in pieces)}",
        "px": [im.width, im.height],
        "worldSize": [r1(width_u), r1(height_u)],
        "density": round(density, 4),
        "anchor": [r1(ax), r1(ay)],
    }
    foot = spec.get("foot")
    if foot and spec["layer"] == "entity":
        half = r1(foot[0] * width_u / 2)
        fd = r1(min(foot[1] * width_u, height_u))
        entry["footprint"] = [-half, -fd, r1(2 * half), fd]
    else:
        entry["footprint"] = None
    occ = spec.get("occluder")
    if occ is None:
        occ = spec["layer"] in ("entity", "overhead") and height_u > 1.1 * player
    entry["occluder"] = bool(occ)
    entry["sway"] = bool(spec.get("sway", False))
    light = spec.get("light")
    if light:
        color, radius = light
        lx, ly = light_spot(im)
        entry["light"] = {
            "color": colors[color],
            "radius": radius,
            "offset": [r1((lx - ax) / density), r1((ly - ay) / density)],
        }
    else:
        entry["light"] = None
    entry["interaction"] = spec.get("interaction")
    entry["sound"] = {"ambient": spec.get("ambient"), "material": spec.get("material")}
    if spec.get("note"):
        entry["note"] = spec["note"]
    return entry, im


# --- imagem-resumo por tema ----------------------------------------------------------------------------------------

def _atlas_frame(key: str) -> tuple[Image.Image, float, float, float]:
    frames = json.loads(ATLAS_JSON.read_text(encoding="utf-8"))
    x, y, w, h, ax, ay, k = frames[key]
    return Image.open(ATLAS_PNG).convert("RGBA").crop((x, y, x + w, y + h)), ax, ay, k


def theme_sheet(theme: str, title: str, items: list[tuple[dict, Image.Image]], ref: dict, scale: float = 3.0) -> Image.Image:
    """Objetos do tema em fila, no tamanho calibrado (scale px/u), cada linha começando pelo jogador e por uma porta."""
    font = cut_sheet._font(20)
    small = cut_sheet._font(16)
    player, pax, pay, pk = _atlas_frame("c_sword_front")
    pl = player.resize((max(1, round(player.width * scale / pk)), max(1, round(player.height * scale / pk))), Image.LANCZOS)
    pl_ax = pax * scale / pk
    door_w, door_h = ref["doorWidth"] * scale, ref["doorHeight"] * scale
    max_w = 1800
    pad = 14
    rows: list[list[tuple[dict, Image.Image]]] = [[]]
    x = pad + pl.width + door_w + 3 * pad
    for e, im in items:
        k = scale / e["density"]
        w = max(round(im.width * k), int(small.getlength(e["id"])) + 2)
        if x + w + pad > max_w and rows[-1]:
            rows.append([])
            x = pad + pl.width + door_w + 3 * pad
        rows[-1].append((e, im))
        x += w + pad
    row_hs = []
    for row in rows:
        hmax = max([pl.height, door_h] + [im.height * scale / e["density"] for e, im in row])
        row_hs.append(int(hmax) + 40)
    H = 56 + sum(row_hs) + pad
    sheet = Image.new("RGBA", (max_w, H), (30, 28, 42, 255))
    d = ImageDraw.Draw(sheet)
    d.text((pad, 12), f"{title} ({theme}) — {len(items)} objetos · escala {scale:g} px/u · jogador {ref['playerHeight']} u · porta {ref['doorWidth']}×{ref['doorHeight']} u",
           fill=(244, 231, 198, 255), font=font)
    y = 56
    for row, rh in zip(rows, row_hs):
        base = y + rh - 30
        d.line([(pad, base), (max_w - pad, base)], fill=(80, 76, 100, 255))
        sheet.alpha_composite(pl, (int(pad + 0), int(base - pl.height)))
        dx = pad + pl.width + pad
        d.rectangle([dx, base - door_h, dx + door_w, base], outline=(242, 165, 65, 255), width=2)
        d.text((dx + 2, base + 2), "porta", fill=(242, 165, 65, 255), font=small)
        x = dx + door_w + 2 * pad
        for e, im in row:
            k = scale / e["density"]
            t = resize_premultiplied(im, (max(1, round(im.width * k)), max(1, round(im.height * k))))
            ay = e["anchor"][1] * k
            sheet.alpha_composite(t, (int(x), int(base - ay)))
            fp = e.get("footprint")
            if fp:
                cx = x + e["anchor"][0] * k
                d.rectangle([cx + fp[0] * scale, base + fp[1] * scale, cx + (fp[0] + fp[2]) * scale, base], outline=(82, 224, 196, 160))
            d.text((x, base + 2), e["id"], fill=(200, 196, 220, 255), font=small)
            x += max(t.width, int(small.getlength(e["id"])) + 2) + pad
        y += rh
    return sheet.convert("RGB")


# --- principal -----------------------------------------------------------------------------------------------------

CUR: dict = {}
SHEET_IMG: dict[str, Image.Image] = {}


def main() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--src", default=str(SRC_DEFAULT))
    ap.add_argument("--docs", action="store_true", help="gera também docs/design/catalogo/<tema>.jpg")
    ap.add_argument("--only", help="só um tema (para conferir); não grava objects.json")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    CUR.update(json.loads(CURATION.read_text(encoding="utf-8")))
    ref = CUR["reference"]
    kinds = CUR["kinds"]
    colors = CUR["lights"]
    params = cut_sheet.Params(**CUR["params"])
    src = Path(args.src)
    cuts: dict[str, dict[int, cut_sheet.Piece]] = {}
    objects: list[dict] = []
    images: dict[str, list[tuple[dict, Image.Image]]] = {}
    seen: set[str] = set()
    for group in CUR["sheets"]:
        sid, theme = group["sheet"], group["theme"]
        if theme not in CUR["themes"]:
            raise SystemExit(f"tema sem nome: {theme}")
        if args.only and theme != args.only:
            continue
        if sid not in cuts:
            img = Image.open(src / f"{sid}.png").convert("RGBA")
            SHEET_IMG[sid] = img
            cuts[sid] = {p.n: p for p in cut_sheet.cut(img, params)}
        for row in group["objects"]:
            name, kind, nums = row[0], row[1], row[2]
            opts = row[3] if len(row) > 3 else {}
            if name in seen:
                raise SystemExit(f"id repetido: {name}")
            seen.add(name)
            nums = nums if isinstance(nums, list) else [nums]
            pieces = []
            for n in nums:
                pc = cuts[sid].get(n)
                if pc is None or pc.cls != "object":
                    raise SystemExit(f"{sid}#{n} ({name}) não é um objeto recortado ({pc.cls if pc else 'inexistente'})")
                pieces.append(pc)
            entry, im = build_object(name, theme, kind, pieces, sid, resolve(kind, opts, kinds), ref, colors)
            objects.append(entry)
            images.setdefault(theme, []).append((entry, im))
    order = list(CUR["themes"].keys())
    objects.sort(key=lambda e: (order.index(e["theme"]), e["id"]))
    doc = {
        "_doc": "Gerado por tools/assets/build_objects.py a partir de tools/assets/objects_curation.json. Não edite à mão. Unidades em u (1 u = 1 px do mapa); anchor e px em pixels da textura; footprint [x, y, w, h] em u relativo à âncora (pé).",
        "version": 1,
        "reference": {"playerHeight": ref["playerHeight"], "doorHeight": ref["doorHeight"], "doorWidth": ref["doorWidth"]},
        "themes": CUR["themes"],
        "objects": objects,
    }
    text = json.dumps(doc, ensure_ascii=False, indent=1) + "\n"
    if args.check:
        current = OUT_JSON.read_text(encoding="utf-8") if OUT_JSON.exists() else ""
        if args.only:
            print(f"ok: {len(objects)} objetos em {args.only}")
            return
        if current != text:
            raise SystemExit("src/Data/objects.json desatualizado: rode build_objects.py")
        print(f"ok: {len(objects)} objetos, objects.json atualizado")
        return
    total = 0
    for theme, items in images.items():
        folder = OUT_SPRITES / theme
        folder.mkdir(parents=True, exist_ok=True)
        keep = {f"{e['id']}.png" for e, _ in items}
        for old in folder.glob("*.png"):
            if old.name not in keep and not args.only:
                old.unlink()
                imp = old.with_name(old.name + ".import")
                if imp.exists():
                    imp.unlink()
        for e, im in items:
            im.save(folder / f"{e['id']}.png", optimize=True)
            total += (folder / f"{e['id']}.png").stat().st_size
        if args.docs:
            OUT_DOCS.mkdir(parents=True, exist_ok=True)
            theme_sheet(theme, CUR["themes"][theme], items, ref).save(OUT_DOCS / f"{theme}.jpg", quality=86)
    if not args.only:
        OUT_JSON.write_bytes(text.encode("utf-8"))
    print(f"{len(objects)} objetos em {len(images)} temas, {total / 1024:.0f} KiB de PNG")


if __name__ == "__main__":
    main()
