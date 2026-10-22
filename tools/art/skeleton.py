"""Esqueleto base da Lâmina por direção: juntas em repouso e construtores de membros.

Todas as coordenadas são pixels absolutos da célula 48x48 (pé em ANCHOR = (24, 44)).
"""
from __future__ import annotations

from parts_clothes import CapeShape
from rig import Arm, Leg, Pose, Sword


def add(p, q):
    return (p[0] + q[0], p[1] + q[1])


def knee_between(hip, ankle, bend=(0, 0)):
    return ((hip[0] + ankle[0]) // 2 + bend[0], (hip[1] + ankle[1]) // 2 + bend[1])


def leg(hip, ankle, bend=(0, 0), foot="down", far=False, z=3):
    return Leg(hip, knee_between(hip, ankle, bend), ankle, foot, far, z)


def arm(shoulder, wrist, bend=(0, 0), hand="front", far=False, z=7, sep=True):
    elbow = ((shoulder[0] + wrist[0]) // 2 + bend[0], (shoulder[1] + wrist[1]) // 2 + bend[1])
    return Arm(shoulder, elbow, wrist, hand, far, z, sep)


# ------------------------------------------------------------------ repouso por direção
# down: mão da espada = mão direita do personagem = lado ESQUERDO da tela.
REST = {
    "down": {
        "hip_s": (22, 33), "hip_o": (27, 33),           # s = lado da espada, o = outro lado
        "ankle_s": (22, 41), "ankle_o": (27, 41),
        "sh_s": (18, 26), "sh_o": (30, 26),
        "wr_s": (17, 33), "wr_o": (31, 33),
        "lantern": (28, 32),
    },
    "up": {
        "hip_s": (27, 33), "hip_o": (22, 33),
        "ankle_s": (27, 41), "ankle_o": (22, 41),
        "sh_s": (30, 26), "sh_o": (18, 26),
        "wr_s": (31, 33), "wr_o": (17, 33),
        "lantern": (20, 32),
    },
    "side": {
        "hip_s": (25, 33), "hip_o": (23, 33),           # s = perna/braço de perto
        "ankle_s": (26, 41), "ankle_o": (22, 41),
        "sh_s": (24, 26), "sh_o": (27, 26),
        "wr_s": (25, 33), "wr_o": (28, 33),
        "lantern": (21, 32),
    },
}


def cape_for(d: str, body=(0, 0), sway=0, flare=0, lift=0, seed=0) -> CapeShape:
    """Capa curta vermelha. sway: barra para os lados; flare: abre/sopra; lift: encurta (vento)."""
    bx, by = body
    if d == "down":
        return CapeShape((18 + bx, 30 + bx, 25 + by), (16 + bx - flare, 32 + bx + flare, 37 + by - lift),
                         sway=sway, tatter_seed=seed)
    if d == "up":
        return CapeShape((19 + bx, 29 + bx, 26 + by), (17 + bx - flare, 31 + bx + flare, 37 + by - lift),
                         sway=sway, tatter_seed=seed, folds=(0.36, 0.7))
    # side: cai atrás das costas (esquerda da tela), o vento joga a barra para trás
    return CapeShape((20 + bx, 25 + bx, 25 + by), (17 + bx - flare, 24 + bx - flare // 2, 37 + by - lift),
                     sway=sway, tatter_seed=seed, folds=(0.45,))


def base_pose(d: str, body=(0, 0)) -> Pose:
    return Pose(dir=d, body=body)


def z_order(d: str) -> dict:
    """Ordem de desenho padrão por direção."""
    if d == "down":
        return {"cape": 0, "arm_far": 2, "leg": 3, "torso": 5, "lantern": 6, "arm": 7, "head": 8, "sword": 9}
    if d == "up":
        return {"sword": 1, "lantern": 2, "leg": 3, "arm": 4, "torso": 5, "cape": 6, "head": 8, "arm_far": 4}
    return {"arm_far": 1, "cape": 2, "leg_far": 2.5, "leg": 3, "torso": 5, "lantern": 6, "head": 8,
            "arm": 8.5, "sword": 9}
