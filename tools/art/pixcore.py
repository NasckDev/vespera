"""Núcleo do pipeline de pixel art da v2: paleta, mapas de texto, camadas e contorno.

Toda imagem intermediária é uma matriz numpy int16 de índices da paleta mestre (-1 = transparente).
Nada aqui escala, gira ou filtra pixels: só copia índices em posições inteiras.
"""
from __future__ import annotations

import json
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
EMPTY = -1


class Palette:
    """Paleta mestre carregada de palette.json, com rampas ordenadas do escuro ao claro."""

    def __init__(self, path: str = os.path.join(HERE, "palette.json")):
        with open(path, encoding="utf-8") as fh:
            data = json.load(fh)
        self.colors: list[tuple[int, int, int]] = []
        self.ramp_of: list[str] = []
        self.shade_of: list[int] = []
        self.ramps: dict[str, list[int]] = {}
        for name, ramp in data["ramps"].items():
            idxs = []
            for hx in ramp["colors"]:
                rgb = tuple(int(hx[i:i + 2], 16) for i in (1, 3, 5))
                if rgb in self.colors:
                    raise ValueError(f"cor repetida na paleta: {hx}")
                self.colors.append(rgb)
                self.ramp_of.append(name)
                self.shade_of.append(len(idxs))
                idxs.append(len(self.colors) - 1)
            self.ramps[name] = idxs
        self.legend: dict[str, int] = {}
        for ch, ref in data["legend"].items():
            self.legend[ch] = EMPTY if ref is None else self.ref(ref)

    def ref(self, ref: str) -> int:
        """'rampa:tom' -> índice global."""
        name, shade = ref.split(":")
        return self.ramps[name][int(shade)]

    def shift(self, idx: int, steps: int) -> int:
        """Anda `steps` tons na própria rampa (negativo escurece), preso às pontas."""
        if idx < 0:
            return idx
        ramp = self.ramps[self.ramp_of[idx]]
        pos = max(0, min(len(ramp) - 1, self.shade_of[idx] + steps))
        return ramp[pos]

    def darkest(self, idx: int) -> int:
        return self.ramps[self.ramp_of[idx]][0]

    def rgba_lut(self) -> np.ndarray:
        lut = np.zeros((len(self.colors) + 1, 4), dtype=np.uint8)
        for i, rgb in enumerate(self.colors):
            lut[i] = (*rgb, 255)
        return lut  # a última linha (índice -1) é transparente


PAL = Palette()


def canvas(w: int, h: int) -> np.ndarray:
    return np.full((h, w), EMPTY, dtype=np.int16)


def parse(text: str, legend: dict[str, str] | None = None) -> np.ndarray:
    """Mapa de texto (uma linha por fileira, um caractere por pixel) -> matriz de índices.

    Linhas em branco nas pontas são ignoradas; a indentação comum é removida.
    `legend` sobrepõe caracteres da legenda global com 'rampa:tom'.
    """
    rows = [r for r in text.split("\n")]
    while rows and not rows[0].strip():
        rows.pop(0)
    while rows and not rows[-1].strip():
        rows.pop()
    indent = min(len(r) - len(r.lstrip(" ")) for r in rows if r.strip())
    rows = [r[indent:].rstrip() for r in rows]
    width = max(len(r) for r in rows)
    out = canvas(width, len(rows))
    local = {ch: PAL.ref(ref) for ch, ref in (legend or {}).items()}
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch == " " or ch == ".":
                continue
            if ch in local:
                out[y, x] = local[ch]
            elif ch in PAL.legend:
                out[y, x] = PAL.legend[ch]
            else:
                raise KeyError(f"caractere sem legenda: {ch!r} na linha {y}: {row!r}")
    return out


def patch(img: np.ndarray, edits: str) -> np.ndarray:
    """Cópia de `img` com pixels trocados: "x,y=c; x,y=c" (c = caractere da legenda, '.' apaga)."""
    out = img.copy()
    for item in edits.split(";"):
        item = item.strip()
        if not item:
            continue
        pos, ch = item.split("=")
        x, y = (int(v) for v in pos.split(","))
        out[y, x] = EMPTY if ch == "." else PAL.legend[ch]
    return out


def mirror(img: np.ndarray) -> np.ndarray:
    return img[:, ::-1].copy()


def blit(dst: np.ndarray, src: np.ndarray, x: int, y: int) -> None:
    """Copia pixels não transparentes de `src` com o canto superior esquerdo em (x, y)."""
    h, w = src.shape
    H, W = dst.shape
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(W, x + w), min(H, y + h)
    if x0 >= x1 or y0 >= y1:
        return
    s = src[y0 - y:y1 - y, x0 - x:x1 - x]
    d = dst[y0:y1, x0:x1]
    mask = s >= 0
    d[mask] = s[mask]


def over(base: np.ndarray, top: np.ndarray) -> np.ndarray:
    out = base.copy()
    m = top >= 0
    out[m] = top[m]
    return out


def neighbours(mask: np.ndarray, dx: int, dy: int) -> np.ndarray:
    """Máscara deslocada: out[y, x] = mask[y - dy, x - dx] (fora = False)."""
    out = np.zeros_like(mask)
    H, W = mask.shape
    ys = slice(max(0, dy), H + min(0, dy))
    xs = slice(max(0, dx), W + min(0, dx))
    ys_src = slice(max(0, -dy), H + min(0, -dy))
    xs_src = slice(max(0, -dx), W + min(0, -dx))
    out[ys, xs] = mask[ys_src, xs_src]
    return out


def outline(img: np.ndarray) -> np.ndarray:
    """Contorno escuro seletivo (sel-out) em volta da silhueta.

    Cada pixel vazio vizinho (4-conexo) da silhueta recebe o tom mais escuro da rampa do pixel que ele toca.
    Do lado da luz (cima/esquerda da figura) usa um tom acima do mais escuro, para o contorno 'acender' de leve.
    Prioridade do vizinho: de baixo, da direita, da esquerda, de cima (sombras pesam mais).
    """
    out = img.copy()
    filled = img >= 0
    H, W = img.shape
    # (dx, dy) = de onde vem a cor: pixel vizinho em (x+dx, y+dy)
    order = [((0, -1), 0), ((-1, 0), 0), ((1, 0), 1), ((0, 1), 1)]
    for (dx, dy), lift in order:
        src = np.full_like(img, EMPTY)
        ys = slice(max(0, -dy), H - max(0, dy))
        xs = slice(max(0, -dx), W - max(0, dx))
        src[ys, xs] = img[max(0, dy):H + min(0, dy), max(0, dx):W + min(0, dx)]
        target = (~filled) & (src >= 0) & (out < 0)
        for idx in np.unique(src[target]):
            sel = target & (src == idx)
            col = PAL.darkest(int(idx))
            if lift:
                col = PAL.shift(col, 1) if PAL.shade_of[int(idx)] >= 3 else col
            out[sel] = col
    return out


def separate(below: np.ndarray, part: np.ndarray, steps: int = 2) -> np.ndarray:
    """Escurece o que está por baixo nas bordas de `part` (sombra de separação entre peças sobrepostas)."""
    pm = part >= 0
    ring = np.zeros_like(pm)
    for dx, dy in ((1, 0), (0, 1), (-1, 0), (0, -1)):
        ring |= neighbours(pm, dx, dy)
    ring &= ~pm
    ring &= below >= 0
    out = below.copy()
    for idx in np.unique(below[ring]):
        sel = ring & (below == idx)
        out[sel] = PAL.shift(int(idx), -steps)
    return out


def to_rgba(img: np.ndarray) -> np.ndarray:
    lut = PAL.rgba_lut()
    return lut[img]  # índice -1 cai na última linha (transparente)
