"""Camada ROUPA e CAPA do estágio 1 (aprendiz): túnica, colete, cinto, cachecol e capa curta vermelha.

Um 'traje' (Costume) define tronco desenhado por direção, materiais dos membros e a capa.
Trocar de estágio = trocar o traje; as poses (rig) não mudam.
"""
from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np

from pixcore import PAL, canvas, parse

# ---------------------------------------------------------------- troncos (pivô = centro do pescoço)
TORSO_APRENDIZ = {
    # cachecol vermelho, colete escuro com alça de couro na diagonal, cinto com fivela, saia da túnica creme
    "down": parse("""
        ...BDEDC...
        ..BDEEDCB..
        .KACDDCBAK.
        .MNCBMMILK.
        .MNMBMIMLK.
        .MNMMIMMLK.
        .MNMIMMLLK.
        .GHdHGGHGG.
        .QRRQQPQPO.
        .QR.RQ.QP..
    """),
    "up": parse("""
        ...BDEDC...
        ..BDEEDCB..
        .KBCDDCCBK.
        .MNMMMMMLK.
        .MNMMMLMLK.
        .MNMMMMLLK.
        .MNMMMMLLK.
        .GHHGGGHGG.
        .QRRQQPQPO.
        .QR.RQ.QP..
    """),
    "side": parse("""
        ..BDEDC.
        .BDEEDCB
        .KCDDCBA
        .KNMMMIK
        .KNMMILK
        .KNMIMLK
        .KNIMMLK
        .GHHGGdG
        .QRQQPPO
        .QR.QPO.
    """),
}
TORSO_PIVOT = {"down": (5, 0), "up": (5, 0), "side": (4, 0)}


@dataclass
class LimbMaterials:
    """Materiais (rampa, tom base) por trecho do membro."""
    sleeve: tuple[str, int] = ("cream", 2)
    glove: tuple[str, int] = ("leather", 3)
    glove_w: int = 2
    pants: tuple[str, int] = ("cloth", 2)
    boot: tuple[str, int] = ("leather", 2)
    arm_w: int = 3
    leg_w: int = 4


# ---------------------------------------------------------------- capa procedural
@dataclass
class CapeShape:
    """Capa como trapézio com barra esfarrapada.

    top: (x_esq, x_dir, y) do ombro; bottom: (x_esq, x_dir, y) da barra.
    sway: desloca a barra (curva quadrática do ombro para baixo). folds: frações onde caem as dobras.
    """
    top: tuple[int, int, int]
    bottom: tuple[int, int, int]
    sway: int = 0
    folds: tuple[float, ...] = (0.34, 0.68)
    tatter_seed: int = 0
    lit_left: bool = True


TATTER = [0, 1, 0, 2, 1, 0, 0, 1, 2, 0, 1, 0, 0, 2, 1, 0, 1, 0, 2, 0]


def render_cape(shape: CapeShape, size=(48, 48), ramp: str = "red") -> np.ndarray:
    W, H = size
    img = canvas(W, H)
    r = PAL.ramps[ramp]
    xl0, xr0, y0 = shape.top
    xl1, xr1, y1 = shape.bottom
    rows = max(1, y1 - y0)
    for y in range(y0, y1 + 1):
        t = (y - y0) / rows
        bend = round(shape.sway * t * t)
        xl = round(xl0 + (xl1 - xl0) * t) + bend
        xr = round(xr0 + (xr1 - xr0) * t) + bend
        width = max(1, xr - xl + 1)
        for x in range(xl, xr + 1):
            u = (x - xl) / max(1, width - 1)
            col_rel = x - xl
            # barra esfarrapada: corta as últimas fileiras conforme o padrão fixo da própria capa
            cut = TATTER[(col_rel + shape.tatter_seed) % len(TATTER)]
            if y > y1 - cut:
                continue
            shade = 2
            if col_rel == 0:
                shade = 4 if shape.lit_left else 1
            elif col_rel == 1:
                shade = 3 if shape.lit_left else 2
            elif x == xr:
                shade = 1 if shape.lit_left else 4
            elif x == xr - 1:
                shade = 2 if shape.lit_left else 3
            else:
                for f in shape.folds:
                    fx = round(f * (width - 1))
                    if col_rel == fx and t > 0.25:
                        shade = 1
                    elif col_rel == fx - 1 and t > 0.25:
                        shade = 3
            if y == y0:
                shade = min(shade + 1, 4)
            if y >= y1 - cut - 0 and cut == 0 and y == y1:
                shade = max(shade - 1, 0)
            if 0 <= x < W and 0 <= y < H:
                img[y, x] = r[shade]
    return img


@dataclass
class Costume:
    name: str
    torso: dict
    torso_pivot: dict
    limbs: LimbMaterials = field(default_factory=LimbMaterials)
    cape_ramp: str = "red"


APRENDIZ = Costume("aprendiz", TORSO_APRENDIZ, TORSO_PIVOT)
