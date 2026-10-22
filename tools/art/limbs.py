"""Membros desenhados por código: segmentos grossos entre juntas inteiras, sombreados com luz de cima-esquerda.

Não há rotação de pixel art: cada membro é rasterizado direto na grade a partir das juntas da pose
(Bresenham + pincel fixo), e o sombreado é decidido pixel a pixel pela borda da silhueta do próprio membro.
"""
from __future__ import annotations

import numpy as np

from pixcore import EMPTY, PAL, blit, canvas


def line(p0: tuple[int, int], p1: tuple[int, int]) -> list[tuple[int, int]]:
    (x0, y0), (x1, y1) = p0, p1
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    pts = []
    while True:
        pts.append((x0, y0))
        if x0 == x1 and y0 == y1:
            return pts
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy


def brush(w: int) -> list[tuple[int, int]]:
    lo = -(w // 2)
    hi = lo + w - 1
    pts = []
    for dy in range(lo, hi + 1):
        for dx in range(lo, hi + 1):
            corner = dx in (lo, hi) and dy in (lo, hi)
            if corner and w >= 3:
                continue
            pts.append((dx, dy))
    return pts


class Segment:
    """Trecho de membro: de p0 a p1, largura w, material 'rampa' com tom base."""

    def __init__(self, p0, p1, w: int, ramp: str, base: int):
        self.p0, self.p1, self.w, self.ramp, self.base = p0, p1, w, ramp, base

    def pixels(self) -> set[tuple[int, int]]:
        out = set()
        for (x, y) in line(self.p0, self.p1):
            for dx, dy in brush(self.w):
                out.add((x + dx, y + dy))
        return out


def render_limb(segments: list[Segment], size: tuple[int, int]) -> np.ndarray:
    """Rasteriza os segmentos (o último cobre os anteriores) e sombreia pela silhueta do conjunto."""
    W, H = size
    mat: dict[tuple[int, int], Segment] = {}
    for seg in segments:
        for p in seg.pixels():
            if 0 <= p[0] < W and 0 <= p[1] < H:
                mat[p] = seg
    img = canvas(W, H)
    for (x, y), seg in mat.items():
        ramp = PAL.ramps[seg.ramp]
        shade = seg.base
        left_open = (x - 1, y) not in mat
        top_open = (x, y - 1) not in mat
        right_open = (x + 1, y) not in mat
        if right_open:
            shade -= 1
        elif left_open or (top_open and (x - 1, y - 1) not in mat):
            shade += 1
        img[y, x] = ramp[max(0, min(len(ramp) - 1, shade))]
    return img


def stamp(img: np.ndarray, part: np.ndarray, at: tuple[int, int], pivot: tuple[int, int]) -> None:
    """Cola uma peça desenhada com o seu pixel `pivot` caindo em `at`."""
    blit(img, part, at[0] - pivot[0], at[1] - pivot[1])


def empty_like(img: np.ndarray) -> np.ndarray:
    return np.full_like(img, EMPTY)
