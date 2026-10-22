"""Reações da Lâmina: dano (2q) e morte (7q). E a esquiva/rolamento (5q), que usa os desenhos de bola."""
from __future__ import annotations

from figure import fig
from parts_gear import LANTERN, LANTERN_PIVOT, sword as sword_img
from parts_poses import BALL, FALLEN
from rig import Item

FLIP = {"down": False, "up": True, "side": False}   # para onde o corpo deita/rola na tela


def _p(d, body, **kw):
    fx = kw.pop("fx", [])
    p = fig(d, body, **kw)
    p.fx = fx
    return p


# ------------------------------------------------------------------ dano: tranco para trás + volta
def hurt(d):
    if d == "down":
        return [
            _p(d, (0, -1), head="down_hurt", wr_s=(14, 29), wr_o=(34, 29), el_s=(-1, 0), el_o=(1, 0),
               sword=(9, (0, 1)), cape={"flare": 2, "lift": 2}, lantern_off=(1, -1)),
            _p(d, (0, 0), head="down_blink", wr_s=(16, 32), wr_o=(32, 32), sword=(7, (0, 1)),
               cape={"flare": 1, "lift": 1}, lantern_off=(1, 0)),
        ]
    if d == "up":
        return [
            _p(d, (0, 1), wr_s=(34, 29), wr_o=(14, 29), el_s=(1, 0), el_o=(-1, 0), sword=(15, (0, 1)),
               cape={"flare": 2, "sway": 1}, lantern_off=(-1, 0)),
            _p(d, (0, 1), wr_s=(32, 32), wr_o=(16, 32), sword=(1, (0, 1)), cape={"flare": 1}),
        ]
    return [
        _p(d, (-2, 0), head="side_hurt", head_off=(-1, 0), wr_s=(22, 29), wr_o=(30, 29), sword=(14, (0, 1)),
           ank_s=(27, 41), ank_o=(20, 41), cape={"flare": 0, "sway": 2}, lantern_off=(1, -1)),
        _p(d, (-1, 0), head="side_blink", wr_s=(24, 32), sword=(2, (0, 1)),
           ank_s=(27, 41), ank_o=(21, 41), cape={"flare": 1, "sway": 1}),
    ]


# ------------------------------------------------------------------ morte: cambaleia, ajoelha, cai, lanterna apaga
def _kneel(d, body, head, wr_s, wr_o, sword, slump=False):
    if d == "side":
        legs = dict(ank_s=(30, 44), foot_s="side", bend_s=(2, -3), ank_o=(19, 44), foot_o="side_heel",
                    bend_o=(3, 2))
    elif d == "down":
        legs = dict(ank_s=(21, 44), ank_o=(28, 44), foot_s=None, foot_o=None, bend_s=(0, 1), bend_o=(0, 1))
    else:
        legs = dict(ank_s=(28, 44), ank_o=(21, 44), bend_s=(0, 1), bend_o=(0, 1))
    return _p(d, body, head=head, wr_s=wr_s, wr_o=wr_o, sword=sword, cape={"flare": 1 if not slump else 2},
              **legs)


def _ground_sword(x, y, flip):
    img, piv = sword_img(8 if flip else 0)
    return Item(img, (x - piv[0], y - piv[1]), layer="weapon", z=-1)


def _fallen(d, lantern, lant_xy, extra_y=0):
    flip = FLIP[d] if d != "side" else False
    h, w = FALLEN.shape
    x = 24 - w // 2
    items = [Item(FALLEN, (x, 44 - h + extra_y), layer="auto", z=4, flip=flip)]
    img = LANTERN[lantern]
    piv = LANTERN_PIVOT[lantern]
    lx, ly = lant_xy
    if flip:
        lx = 48 - lx
    items.append(Item(img[:, ::-1] if flip else img, (lx - piv[0], ly - piv[1]), layer="lantern", z=6))
    sx = 10 if not flip else 38
    items.append(_ground_sword(sx, 42, flip))
    return _p(d, (0, 0), legs=False, arms=False, cape=False, lantern=None, head=False, **{}) if False else \
        _fallen_pose(d, items)


def _fallen_pose(d, items):
    p = fig(d, (0, 0), legs=False, arms=False, cape=False, lantern=None)
    p.torso = False
    p.head = False
    p.items = items
    return p


def death(d):
    hd = {"down": "down", "up": "up", "side": "side"}[d]
    slump = {"down": "down_slump", "up": "up", "side": "side_slump"}[d]
    hurt_f = hurt(d)[0]
    if d == "side":
        f1 = _p(d, (-1, 3), head="side_hurt", wr_s=(24, 35), wr_o=(28, 35), sword=(3, (0, 1)),
                ank_s=(28, 41), ank_o=(20, 41), bend_s=(2, 0), bend_o=(1, 0), cape={"flare": 1})
        f2 = _kneel(d, (0, 6), slump, (27, 40), (29, 39), (2, (0, 1)))
        f3 = _kneel(d, (2, 8), slump, (31, 43), (30, 43), (0, (0, 1)), slump=True)
    elif d == "down":
        f1 = _p(d, (0, 3), head="down_hurt", wr_s=(17, 36), wr_o=(31, 36), sword=(5, (0, 1)),
                ank_s=(21, 41), ank_o=(28, 41), bend_s=(-1, 0), bend_o=(1, 0), cape={"flare": 1})
        f2 = _kneel(d, (0, 6), slump, (17, 40), (31, 40), (6, (0, 1)))
        f3 = _kneel(d, (0, 8), slump, (18, 44), (30, 44), (8, (0, 1)), slump=True)
    else:
        f1 = _p(d, (0, 3), wr_s=(31, 36), wr_o=(17, 36), sword=(3, (0, 1)),
                ank_s=(28, 41), ank_o=(21, 41), cape={"flare": 1})
        f2 = _kneel(d, (0, 6), hd, (31, 40), (17, 40), (2, (0, 1)))
        f3 = _kneel(d, (0, 8), hd, (30, 44), (18, 44), (0, (0, 1)), slump=True)
    f4 = _fallen(d, "glow", (39, 43), extra_y=-1)
    f5 = _fallen(d, "fallen_glow", (41, 43))
    f6 = _fallen(d, "fallen_out", (41, 43))
    return [hurt_f, f1, f2, f3, f4, f5, f6]


# ------------------------------------------------------------------ esquiva: impulso, bola x3, aterrissa
def _ball(d, name, hop):
    img = BALL[name]
    h, w = img.shape
    flip = FLIP[d]
    p = fig(d, (0, 0), legs=False, arms=False, cape=False, lantern=None)
    p.torso = False
    p.head = False
    p.items = [Item(img, (24 - w // 2, 44 - h + hop), layer="auto", z=5, flip=flip)]
    return p


def dodge(d):
    if d == "side":
        launch = _p(d, (2, 4), ank_s=(30, 41), ank_o=(20, 41), bend_s=(2, -1), bend_o=(0, 1), head="side_blink",
                    wr_s=(30, 34), wr_o=(31, 33), sword=(1, (0, 1)), cape={"flare": 4, "lift": 2})
        land = _p(d, (-1, 3), ank_s=(28, 41), ank_o=(20, 41), bend_s=(2, -1), bend_o=(1, 0),
                  wr_s=(27, 35), sword=(2, (0, 1)), cape={"flare": 3, "lift": 1})
    elif d == "down":
        launch = _p(d, (0, 4), head="down_slump", ank_s=(21, 42), ank_o=(28, 42), wr_s=(20, 38), wr_o=(28, 38),
                    sword=(4, (0, 1)), cape={"flare": 3})
        land = _p(d, (0, 3), ank_s=(21, 41), ank_o=(28, 41), wr_s=(17, 36), sword=(6, (0, 1)), cape={"flare": 2})
    else:
        launch = _p(d, (0, 4), ank_s=(28, 40), ank_o=(21, 40), wr_s=(29, 37), wr_o=(19, 37), sword=(3, (0, 1)),
                    cape={"flare": 3, "lift": 2})
        land = _p(d, (0, 3), ank_s=(28, 41), ank_o=(21, 41), wr_s=(31, 36), sword=(2, (0, 1)), cape={"flare": 2})
    return [launch, _ball(d, "tuck", 0), _ball(d, "cape", -1), _ball(d, "feet", 0), land]


ANIMS = {"dodge": dodge, "hurt": hurt, "death": death}
