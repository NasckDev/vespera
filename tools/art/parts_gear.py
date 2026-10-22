"""Camadas ARMA, LANTERNA e EFEITOS (rastro do golpe).

Espada: 16 ângulos desenhados direto na grade com linhas limpas (passos 1:0, 2:1, 1:1, 1:2) -> nunca gira pixel.
Ângulo 0 = ponta para a direita; cresce no sentido horário (tela com y para baixo): 4 = baixo, 8 = esquerda, 12 = cima.
"""
from __future__ import annotations

import math

import numpy as np

from limbs import line
from pixcore import PAL, blit, canvas, parse

DIRS16 = [(1, 0), (2, 1), (1, 1), (1, 2), (0, 1), (-1, 2), (-1, 1), (-2, 1),
          (-1, 0), (-2, -1), (-1, -1), (-1, -2), (0, -1), (1, -2), (1, -1), (2, -1)]


def _steps_for(v, length):
    """Quantas repetições do vetor-passo dão ~`length` px de comprimento."""
    return max(1, round(length / math.hypot(*v)))


def sword(angle: int, blade: int = 12, ramp_blade: str = "steel") -> tuple[np.ndarray, tuple[int, int]]:
    """Espada curta de aprendiz. Retorna (imagem, pivô = pixel do punho onde a mão segura)."""
    v = DIRS16[angle % 16]
    n = math.hypot(*v)
    d = (v[0] / n, v[1] / n)
    size = 44
    c = size // 2
    img = canvas(size, size)
    st = PAL.ramps[ramp_blade]
    leather = PAL.ramps["leather"]
    brass = PAL.ramps["amber"]

    def at(s):
        return (c + round(d[0] * s), c + round(d[1] * s))

    horiz = abs(v[0]) >= abs(v[1])
    off = (0, 1) if horiz else (1, 0)
    edge = line(at(2), at(2 + blade))
    for i, (x, y) in enumerate(edge):
        img[y, x] = st[5] if i == len(edge) // 3 else st[4]
        if i < len(edge) - 1:
            img[y + off[1], x + off[0]] = st[2] if i < len(edge) - 2 else st[3]
    # guarda de latão perpendicular (5 px)
    p = (-d[1], d[0])
    g = at(1)
    ends = [(g[0] + round(p[0] * j), g[1] + round(p[1] * j)) for j in (-2.2, 2.2)]
    for i, (x, y) in enumerate(line(ends[0], ends[1])):
        img[y, x] = brass[3] if (x, y) == g else (brass[4] if i == 0 else brass[2])
    # cabo de couro e pomo
    for s, col in ((0, leather[3]), (-1, leather[2])):
        x, y = at(s)
        img[y, x] = col
    x, y = at(-2)
    img[y, x] = brass[3]
    return _trim(img, (c, c))


def _sgn(n: int) -> int:
    return (n > 0) - (n < 0)


def _trim(img: np.ndarray, pivot):
    ys, xs = np.nonzero(img >= 0)
    y0, x0 = ys.min(), xs.min()
    return img[y0:ys.max() + 1, x0:xs.max() + 1].copy(), (pivot[0] - x0, pivot[1] - y0)


# ---------------------------------------------------------------- lanterna velha (latão + Fragmento magenta)
LANTERN = {
    "rest": parse("""
        ..c..
        .bdb.
        bdddb
        dz*yb
        cyzxb
        dxyxb
        bcccb
        .aba.
    """),
    "glow": parse("""
        ..c..
        .bdb.
        bdddb
        d**zb
        cz*yb
        dyzyb
        bcccb
        .aba.
    """),
    "dim": parse("""
        ..c..
        .bdb.
        bdddb
        dxyxb
        cwxwb
        dwxwb
        bcccb
        .aba.
    """),
    # caída no chão (morte): deitada de lado
    "fallen": parse("""
        .bdcdb..
        bdz*yxbc
        cdyzyxbb
        .bcbcb..
    """),
    "fallen_glow": parse("""
        .bdcdb..
        bd**zybc
        cd*zyxbb
        .bcbcb..
    """),
    "fallen_out": parse("""
        .bdcdb..
        bdwxwvbc
        cdvwvvbb
        .bcbcb..
    """),
}
LANTERN_PIVOT = {"rest": (2, 0), "glow": (2, 0), "dim": (2, 0),
                 "fallen": (4, 3), "fallen_glow": (4, 3), "fallen_out": (4, 3)}


# ---------------------------------------------------------------- rastro do golpe (smear)
def smear(center, r_in: float, r_out: float, a_from: float, a_to: float, size=(48, 48),
          taper: float = 0.6, style: str = "steel") -> np.ndarray:
    """Arco de golpe entre os ângulos (graus, 0 = direita, horário) a_from -> a_to.

    A ponta (a_to) é a mais grossa e clara; o rabo (a_from) afina. Bandas: borda externa clara, miolo magenta.
    """
    W, H = size
    img = canvas(W, H)
    cx, cy = center
    span = a_to - a_from
    bands = {
        "steel": [PAL.ref("steel:5"), PAL.ref("breach:5"), PAL.ref("breach:4"), PAL.ref("breach:3")],
        "heavy": [PAL.ref("amber:5"), PAL.ref("amber:4"), PAL.ref("breach:4"), PAL.ref("breach:3")],
    }[style]
    for y in range(H):
        for x in range(W):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            rr = math.hypot(dx, dy)
            if rr < r_in - 0.01 or rr > r_out + 0.5:
                continue
            ang = math.degrees(math.atan2(dy, dx))
            rel = (ang - a_from) / span if span else 0
            # normaliza para o intervalo do arco nas duas direções de giro
            for k in (-360, 0, 360):
                rel_k = (ang + k - a_from) / span if span else 0
                if 0 <= rel_k <= 1:
                    rel = rel_k
                    break
            else:
                continue
            # espessura cresce do rabo (0) para a ponta (1)
            thick = (r_out - r_in) * (taper + (1 - taper) * rel) if rel < 1 else (r_out - r_in)
            inner = r_out - thick
            if rr < inner:
                continue
            depth = (r_out - rr) / max(0.5, thick)
            if rel < 0.25 and depth > 0.5:
                continue
            band = 0 if depth < 0.3 else (1 if depth < 0.55 else (2 if depth < 0.8 else 3))
            if rel < 0.5:
                band = min(3, band + 1)
            img[y, x] = bands[band]
    return img


def streak(p0, p1, width: int = 3, size=(48, 48), style: str = "steel") -> np.ndarray:
    """Rastro reto de investida/estocada: grosso e claro em p1 (frente), afinando até p0."""
    W, H = size
    img = canvas(W, H)
    cols = {"steel": ["steel:5", "breach:5", "breach:4"], "heavy": ["amber:5", "amber:4", "breach:4"]}[style]
    cols = [PAL.ref(c) for c in cols]
    pts = line(p0, p1)
    n = len(pts)
    dx, dy = p1[0] - p0[0], p1[1] - p0[1]
    perp = (0, 1) if abs(dx) >= abs(dy) else (1, 0)
    for i, (x, y) in enumerate(pts):
        t = i / max(1, n - 1)
        half = int(round((width - 1) / 2 * t))
        for k in range(-half, half + 1):
            px, py = x + perp[0] * k, y + perp[1] * k
            if 0 <= px < W and 0 <= py < H:
                band = 0 if k == 0 else (1 if abs(k) < half or half == 1 else 2)
                if t < 0.35:
                    band = min(2, band + 1)
                img[py, px] = cols[band]
    return img


SPARK = {
    "big": parse("""
        ...*...
        ...z...
        *z+*+z*
        ...z...
        ...*...
    """, {"+": "steel:5"}),
    "small": parse("""
        .*.
        *+*
        .*.
    """, {"+": "steel:5"}),
    "glint": parse("""
        .z.
        z+z
        .z.
    """, {"+": "amber:5"}),
}


def spark(name: str, center, size=(48, 48)) -> np.ndarray:
    img = canvas(*size)
    s = SPARK[name]
    h, w = s.shape
    blit(img, s, center[0] - w // 2, center[1] - h // 2)
    return img
