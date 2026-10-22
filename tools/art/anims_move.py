"""Animações de locomoção da Lâmina: idle (4q), andar (8q), esquiva/rolamento (5q)."""
from __future__ import annotations

from figure import fig

DIRS = ("down", "up", "side")


# ------------------------------------------------------------------ idle: respiração + capa e lanterna atrasadas
def idle(d: str):
    bob = [0, 0, 1, 1]
    sway = [0, 1, 1, 0]      # capa atrasa a respiração em 1 quadro
    lant = [(0, 0), (0, 0), (0, 1), (0, 1)]
    out = []
    for i in range(4):
        b = (0, bob[i])
        kw = dict(cape={"sway": sway[i] if d != "side" else -sway[i]}, lantern_off=lant[i])
        if d == "down":
            out.append(fig(d, b, sword=(6, (0, 1)), wr_s=(17, 33 + bob[i]), **kw))
        elif d == "up":
            out.append(fig(d, b, sword=(2, (0, 1)), wr_s=(31, 33 + bob[i]), **kw))
        else:
            out.append(fig(d, b, sword=(3, (0, 1)), wr_s=(26, 33 + bob[i]), el_s=(0, 0), **kw))
    return out


# ------------------------------------------------------------------ andar: contato, baixo, passagem, alto (x2)
WALK_BOB = [1, 1, 0, 0, 1, 1, 0, 0]


def _cape_walk(d, i):
    prev = (i - 1) % 8           # capa segue o corpo com 1 quadro de atraso
    if d == "side":
        flare = [2, 3, 3, 2, 2, 3, 3, 2][prev]
        return {"flare": flare, "sway": -[0, 1, 1, 0, 0, 1, 1, 0][prev], "lift": [0, 1, 1, 0, 0, 1, 1, 0][prev]}
    sway = [-1, -1, 0, 1, 1, 1, 0, -1][prev]
    return {"sway": sway, "flare": 1}


def walk(d: str):
    out = []
    if d == "side":
        # perna A (perto) à frente no quadro 0; B (longe) atrás. Pés plantados recuam 2-3 px por quadro.
        a_ank = [(29, 41), (27, 41), (25, 41), (22, 41), (19, 40), (20, 38), (24, 38), (28, 40)]
        a_ft = ["side", "side", "side", "side_toe", "side_toe", "side_toe", "side", "side"]
        a_bend = [(0, 0), (1, 0), (0, 0), (0, 0), (0, 0), (1, 0), (2, -1), (1, 0)]
        wr_near = [22, 23, 25, 27, 28, 27, 25, 23]   # braço da espada: contra a perna de perto
        wr_far = [29, 28, 26, 24, 23, 24, 26, 28]
        for i in range(8):
            j = (i + 4) % 8
            b = (0, WALK_BOB[i])
            out.append(fig(
                d, b,
                ank_s=a_ank[i], foot_s=a_ft[i], bend_s=a_bend[i],
                ank_o=a_ank[j], foot_o=a_ft[j], bend_o=a_bend[j],
                wr_s=(wr_near[i], 33 + b[1]), wr_o=(wr_far[i], 32 + b[1]),
                sword=(2, (0, 1)),
                cape=_cape_walk(d, i), lantern_off=([0, 0, -1, -1, 0, 0, 1, 1][(i - 1) % 8], 0),
            ))
        return out
    # frente/costas: perna da frente desce 1 px, a de trás sobe e encurta; braços contra as pernas
    s_y = [42, 41, 41, 40, 40, 39, 39, 41]
    o_y = [40, 39, 39, 41, 42, 41, 41, 40]
    s_bend = [(0, 0), (0, 0), (0, 0), (0, -1), (0, -1), (0, -2), (0, -2), (0, 0)]
    o_bend = [(0, -1), (0, -2), (0, -2), (0, 0), (0, 0), (0, 0), (0, 0), (0, -1)]
    sw_y = [32, 32, 33, 34, 35, 35, 34, 33]       # mão da espada vai à frente quando a perna 'o' avança
    ot_y = [35, 35, 34, 33, 32, 32, 33, 34]
    for i in range(8):
        b = (0, WALK_BOB[i])
        if d == "down":
            sx, ox = 22, 27
            wsx, wox = 17, 31
            sword = (6, (0, 1))
        else:
            sx, ox = 27, 22
            wsx, wox = 31, 17
            sword = (2, (0, 1))
        out.append(fig(
            d, b,
            ank_s=(sx, s_y[i]), bend_s=s_bend[i], ank_o=(ox, o_y[i]), bend_o=o_bend[i],
            wr_s=(wsx, sw_y[i] + b[1] - 1 + 1), wr_o=(wox, ot_y[i] + b[1] - 1 + 1),
            sword=sword, cape=_cape_walk(d, i),
            lantern_off=(0, [0, 0, 1, 1, 0, 0, 1, 1][(i - 1) % 8]),
        ))
    return out


ANIMS = {"idle": idle, "walk": walk}
