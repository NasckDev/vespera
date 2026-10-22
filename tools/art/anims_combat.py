"""Animações de combate da Lâmina: combo leve (3 golpes x 5q), pesado (carga 2q + golpe giratório 6q),
habilidade 'investida' (6q).

Cada golpe segue antecipação -> impacto (com rastro) -> recuperação. Ângulos de espada: 0 = direita, 4 = baixo,
8 = esquerda, 12 = cima (ver parts_gear.DIRS16). Ângulos de rastro em graus de tela (0 = direita, 90 = baixo).
"""
from __future__ import annotations

from figure import fig
from parts_gear import smear, spark, streak

CENTER = {"down": (24, 32), "up": (24, 26), "side": (25, 28)}


def _f(d, body, wr, sword, *, fx=(), wo=None, el=(0, 0), sword_z=None, arm_z=None, cape=None,
       head=None, legs=None, **kw):
    legs = legs or {}
    p = fig(d, body, wr_s=wr, sword=(sword, (0, 1)) if isinstance(sword, int) else sword, el_s=el,
            wr_o=wo, sword_z=sword_z, arm_s_z=arm_z, cape=cape, head=head, **legs, **kw)
    p.aura = [f[1] for f in fx if isinstance(f, tuple)]
    p.fx = [f for f in fx if not isinstance(f, tuple)]
    return p


def _arc(d, a0, a1, r0=11, r1=16, taper=0.5, style="steel", c=None):
    """Rastro do golpe: vai para baixo do corpo (camada aura), a espada e o corpo ficam por cima."""
    return ("under", smear(c or CENTER[d], r0, r1, a0, a1, taper=taper, style=style))


def _streak(p0, p1, w=3):
    return ("under", streak(p0, p1, w))


# ------------------------------------------------------------------ combo leve
def _stance(d, wide=1):
    """Base afastada para golpes."""
    if d == "side":
        return {"ank_s": (28, 41), "ank_o": (20, 41), "bend_s": (1, 0), "bend_o": (-1, 0)}
    if d == "down":
        return {"ank_s": (21 - wide, 41), "ank_o": (28 + wide, 41)}
    return {"ank_s": (28 + wide, 41), "ank_o": (21 - wide, 41)}


def light1(d):
    """Corte horizontal de direita (forehand)."""
    st = _stance(d)
    if d == "down":
        return [
            _f(d, (-1, 0), (14, 27), 10, el=(-1, 1), wo=(30, 31), legs=st, cape={"sway": 1}),
            _f(d, (0, 1), (17, 35), 6, legs=st, fx=[_arc(d, 200, 120, taper=0.3)], cape={"sway": 1, "flare": 1}),
            _f(d, (1, 1), (26, 37), 3, legs=st, fx=[_arc(d, 185, 45, r1=17)], cape={"sway": -1, "flare": 1}),
            _f(d, (1, 1), (31, 33), 0, legs=st, fx=[_arc(d, 120, 15, taper=0.2)], cape={"sway": -2, "flare": 1}),
            _f(d, (0, 0), (29, 33), 2, legs=st, cape={"sway": -1}),
        ]
    if d == "up":
        return [
            _f(d, (1, 0), (34, 30), 1, el=(1, 1), legs=st, cape={"sway": -1}),
            _f(d, (0, -1), (31, 22), 15, sword_z=1, legs=st, fx=[_arc(d, 20, -60, taper=0.3)], cape={"sway": -1}),
            _f(d, (-1, -1), (24, 19), 12, sword_z=1, legs=st, fx=[_arc(d, 15, -140, r1=17)], cape={"sway": 1}),
            _f(d, (-1, 0), (18, 22), 10, sword_z=1, legs=st, fx=[_arc(d, -60, -165, taper=0.2)], cape={"sway": 2}),
            _f(d, (0, 0), (17, 29), 7, legs=st, cape={"sway": 1}),
        ]
    return [
        _f(d, (-1, 0), (21, 21), 10, el=(-1, 0), legs=st, cape={"flare": 1}),
        _f(d, (0, 0), (26, 21), 14, legs=st, fx=[_arc(d, 230, 310, taper=0.3)], cape={"flare": 2}),
        _f(d, (1, 1), (32, 30), 1, legs=st, fx=[_arc(d, 240, 400, r1=17)], cape={"flare": 3, "lift": 1}),
        _f(d, (1, 1), (31, 34), 3, legs=st, fx=[_arc(d, 330, 430, taper=0.2)], cape={"flare": 3}),
        _f(d, (0, 0), (28, 33), 3, legs=st, cape={"flare": 2}),
    ]


def light2(d):
    """Corte de volta (backhand), no sentido oposto."""
    st = _stance(d)
    if d == "down":
        return [
            _f(d, (1, 0), (30, 29), 14, el=(1, -1), legs=st, cape={"sway": -1}),
            _f(d, (1, 1), (29, 35), 2, legs=st, fx=[_arc(d, -20, 60, taper=0.3)], cape={"sway": -1, "flare": 1}),
            _f(d, (-1, 1), (19, 37), 5, legs=st, fx=[_arc(d, -10, 150, r1=17)], cape={"sway": 1, "flare": 1}),
            _f(d, (-1, 1), (14, 33), 8, legs=st, fx=[_arc(d, 70, 175, taper=0.2)], cape={"sway": 2, "flare": 1}),
            _f(d, (0, 0), (16, 33), 6, legs=st, cape={"sway": 1}),
        ]
    if d == "up":
        return [
            _f(d, (-1, 0), (19, 28), 6, el=(-1, 0), legs=st, cape={"sway": 1}),
            _f(d, (-1, -1), (18, 22), 10, sword_z=1, legs=st, fx=[_arc(d, 190, 250, taper=0.3)], cape={"sway": 1}),
            _f(d, (1, -1), (26, 19), 13, sword_z=1, legs=st, fx=[_arc(d, 185, 330, r1=17)], cape={"sway": -1}),
            _f(d, (1, 0), (32, 23), 15, sword_z=1, legs=st, fx=[_arc(d, 260, 360, taper=0.2)], cape={"sway": -2}),
            _f(d, (0, 0), (31, 30), 2, legs=st, cape={"sway": -1}),
        ]
    return [
        _f(d, (-1, 1), (21, 36), 6, legs=st, cape={"flare": 1}),
        _f(d, (0, 1), (28, 35), 2, legs=st, fx=[_arc(d, 140, 60, taper=0.3)], cape={"flare": 2}),
        _f(d, (1, 0), (32, 26), 15, legs=st, fx=[_arc(d, 130, -40, r1=17)], cape={"flare": 3, "lift": 1}),
        _f(d, (1, -1), (29, 21), 13, legs=st, fx=[_arc(d, 30, -80, taper=0.2)], cape={"flare": 3}),
        _f(d, (0, 0), (28, 31), 2, legs=st, cape={"flare": 2}),
    ]


def light3(d):
    """Finalizador: golpe de cima para baixo com as duas mãos, rastro largo e faísca no impacto."""
    st = _stance(d, wide=2)
    if d == "down":
        return [
            _f(d, (0, -1), (21, 17), 12, wo=(26, 18), sword_z=1, arm_z=7, legs=st, cape={"flare": 1}),
            _f(d, (0, 0), (22, 21), 12, wo=(26, 22), sword_z=1, legs=st,
               fx=[_arc(d, 290, 210, r0=8, r1=16, taper=0.3, style="heavy")], cape={"flare": 1}),
            _f(d, (0, 2), (23, 37), 4, wo=(26, 37), legs=st, head=None,
               fx=[_arc(d, 280, 100, r0=8, r1=17, style="heavy"), spark("big", (24, 45))], cape={"flare": 2, "lift": 1}),
            _f(d, (0, 2), (23, 37), 4, wo=(26, 37), legs=st, fx=[_arc(d, 200, 105, r0=10, r1=17, taper=0.2,
                                                                       style="heavy")], cape={"flare": 2}),
            _f(d, (0, 1), (19, 35), 5, legs=st, cape={"flare": 1}),
        ]
    if d == "up":
        return [
            _f(d, (0, 1), (30, 24), 4, wo=(24, 24), sword_z=9, legs=st, cape={"flare": 1}),
            _f(d, (0, 0), (27, 18), 12, wo=(23, 19), sword_z=1, legs=st,
               fx=[_arc(d, 100, 20, r0=8, r1=16, taper=0.3, style="heavy")], cape={"flare": 1}),
            _f(d, (0, -1), (25, 15), 12, wo=(23, 16), sword_z=1, legs=st,
               fx=[_arc(d, 100, -95, r0=8, r1=17, style="heavy"), spark("big", (25, 6))], cape={"flare": 2}),
            _f(d, (0, -1), (25, 15), 12, wo=(23, 16), sword_z=1, legs=st,
               fx=[_arc(d, 20, -90, r0=10, r1=17, taper=0.2, style="heavy")], cape={"flare": 2}),
            _f(d, (0, 0), (30, 26), 15, sword_z=1, legs=st, cape={"flare": 1}),
        ]
    return [
        _f(d, (-1, -1), (22, 17), 10, wo=(24, 18), el=(-1, 0), legs=st, cape={"flare": 1}),
        _f(d, (0, -1), (27, 17), 13, wo=(28, 18), legs=st,
           fx=[_arc(d, 210, 290, r0=8, r1=16, taper=0.3, style="heavy")], cape={"flare": 2}),
        _f(d, (2, 2), (33, 32), 2, wo=(32, 31), legs=st,
           fx=[_arc(d, 220, 415, r0=8, r1=18, style="heavy"), spark("big", (42, 39))], cape={"flare": 4, "lift": 2}),
        _f(d, (2, 2), (33, 33), 3, wo=(32, 32), legs=st,
           fx=[_arc(d, 320, 420, r0=10, r1=18, taper=0.2, style="heavy")], cape={"flare": 3, "lift": 1}),
        _f(d, (1, 1), (30, 33), 3, legs=st, cape={"flare": 2}),
    ]


def _u(img):
    return ("under", img)


# ------------------------------------------------------------------ pesado: carga (loop) + corte giratório
def heavy_charge(d):
    """Agachado, espada para trás, brilho pulsando na lâmina (2 quadros em loop)."""
    out = []
    for i in range(2):
        glow = spark("glint" if i == 0 else "small", _charge_tip(d, i))
        if d == "down":
            p = _f(d, (1, 2), (30, 35), 1, wo=(26, 35), legs=_stance(d, 2), cape={"flare": 1 + i}, fx=[glow])
        elif d == "up":
            p = _f(d, (-1, 2), (18, 35), 7, wo=(22, 35), legs=_stance(d, 2), cape={"flare": 1 + i}, fx=[glow])
        else:
            p = _f(d, (-2, 2), (19, 34), 7, wo=(22, 34), legs=_stance(d), cape={"flare": 2 + i}, fx=[glow])
        out.append(p)
    return out


def _charge_tip(d, i):
    return {"down": (39, 39 - i), "up": (9, 39 - i), "side": (8, 39 - i)}[d]


# vistas do giro: (direção do corpo, espelhar, punho, ângulo da espada, z da espada)
SPIN_VIEW = {
    90: ("down", False, (24, 37), 4, 9),
    0: ("side", False, (33, 30), 0, 9),
    270: ("up", False, (24, 18), 12, 1),
    180: ("side", True, (33, 30), 0, 9),
}
SPIN_START = {"down": 90, "side": 0, "up": 270}


def heavy_strike(d):
    """Corte giratório: o corpo passa pelas 4 vistas enquanto a espada varre 360° (anti-horário na tela)."""
    a0 = SPIN_START[d]
    seq = [a0, (a0 - 90) % 360, (a0 - 180) % 360, (a0 - 270) % 360, a0, a0]
    out = []
    for i, ang in enumerate(seq):
        view, flip, wr, sw, sz = SPIN_VIEW[ang]
        fx = []
        if 1 <= i <= 4:
            fx.append(_u(smear((24, 30), 10, 19, ang + 150, ang, taper=0.35, style="heavy")))
        elif i == 0:
            fx.append(_u(smear((24, 30), 11, 18, ang + 60, ang, taper=0.3, style="heavy")))
        else:
            fx.append(_u(smear((24, 30), 13, 19, ang + 70, ang + 10, taper=0.15, style="heavy")))
        body = (0, 1) if i < 5 else (0, 0)
        p = _f(view, body, wr if i < 5 else None, sw if i < 5 else ({"down": 6, "up": 2, "side": 3}[view]),
               sword_z=sz, legs=_stance(view, 2), cape={"flare": 3 if 1 <= i <= 4 else 1}, fx=fx)
        p.flip = flip
        out.append(p)
    return out


# ------------------------------------------------------------------ habilidade: investida (estocada em avanço)
G1, G2 = "purple:2", "purple:1"   # imagens-fantasma do avanço (mais perto = mais clara)


def skill(d):
    """Investida: agacha e recolhe a espada, dispara (fantasmas + rastro), crava a estocada, segura, volta."""
    st = _stance(d)
    if d == "side":
        frames = [
            _f(d, (-2, 2), (20, 31), 0, wo=(23, 32), legs=st, cape={"flare": 1}, fx=[spark("glint", (31, 31))]),
            _f(d, (1, 1), (29, 31), 0, legs=st, cape={"flare": 4, "lift": 2},
               fx=[_streak((8, 33), (22, 33), 3)]),
            _f(d, (3, 1), (34, 30), 0, legs=st, cape={"flare": 5, "lift": 3},
               fx=[_streak((2, 33), (20, 33), 5), _streak((6, 27), (18, 27), 3)]),
            _f(d, (3, 1), (34, 30), 0, legs=st, cape={"flare": 4, "lift": 2},
               fx=[_streak((12, 33), (20, 33), 3), spark("big", (46, 31))]),
            _f(d, (2, 0), (31, 32), 1, legs=st, cape={"flare": 2}),
            _f(d, (0, 0), (27, 33), 3, cape={"flare": 1}),
        ]
        ghosts = [[], [(-5, 0, G1)], [(-6, 0, G1), (-12, 0, G2)], [(-5, 0, G2)], [], []]
    elif d == "down":
        frames = [
            _f(d, (0, -1), (19, 29), 12, wo=(29, 32), sword_z=1, legs=st, cape={"flare": 1},
               fx=[spark("glint", (19, 17))]),
            _f(d, (0, 1), (22, 34), 4, legs=st, cape={"flare": 3}, fx=[_streak((24, 6), (24, 16), 3)]),
            _f(d, (0, 2), (23, 37), 4, legs=st, cape={"flare": 4},
               fx=[_streak((19, 4), (19, 17), 5), _streak((29, 7), (29, 16), 3)]),
            _f(d, (0, 2), (23, 37), 4, legs=st, cape={"flare": 3}, fx=[spark("big", (23, 49))]),
            _f(d, (0, 1), (20, 35), 5, legs=st, cape={"flare": 2}),
            _f(d, (0, 0), (17, 33), 6, cape={"flare": 1}),
        ]
        ghosts = [[], [(0, -5, G1)], [(0, -5, G1), (0, -10, G2)], [(0, -4, G2)], [], []]
    else:
        frames = [
            _f(d, (0, 2), (31, 35), 4, sword_z=9, legs=st, cape={"flare": 1}, fx=[spark("glint", (31, 46))]),
            _f(d, (0, 0), (27, 24), 12, sword_z=1, legs=st, cape={"flare": 3, "lift": 1},
               fx=[_streak((24, 50), (24, 40), 3)]),
            _f(d, (0, -1), (25, 20), 12, sword_z=1, legs=st, cape={"flare": 4, "lift": 2},
               fx=[_streak((19, 50), (19, 37), 5), _streak((29, 50), (29, 39), 3)]),
            _f(d, (0, -1), (25, 20), 12, sword_z=1, legs=st, cape={"flare": 3, "lift": 1}, fx=[spark("big", (25, 6))]),
            _f(d, (0, 0), (29, 26), 14, sword_z=1, legs=st, cape={"flare": 2}),
            _f(d, (0, 0), (31, 31), 2, cape={"flare": 1}),
        ]
        ghosts = [[], [(0, 5, G1)], [(0, 5, G1), (0, 10, G2)], [(0, 4, G2)], [], []]
    for p, g in zip(frames, ghosts):
        p.ghosts = g
    return frames


ANIMS = {"attack1": light1, "attack2": light2, "attack3": light3,
         "heavy_charge": heavy_charge, "heavy_strike": heavy_strike, "skill": skill}
