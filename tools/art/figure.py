"""Construtor de poses da Lâmina: `fig(...)` monta uma Pose a partir de poucos parâmetros por quadro.

Lados: 's' = lado da espada (perto da câmera no perfil), 'o' = o outro lado.
Quadris e ombros andam com `body`; tornozelos e punhos são absolutos quando informados.
"""
from __future__ import annotations

from rig import Pose, Sword
from skeleton import REST, add, arm, cape_for, leg, z_order

DEFAULT_FOOT = {"down": "down", "up": "up", "side": "side"}


def fig(d: str, body=(0, 0), *,
        ank_s=None, ank_o=None, bend_s=(0, 0), bend_o=(0, 0), foot_s=None, foot_o=None,
        hip_s=(0, 0), hip_o=(0, 0),
        wr_s=None, wr_o=None, el_s=(0, 0), el_o=(0, 0), hand_s="fist", hand_o="front",
        sh_s=(0, 0), sh_o=(0, 0),
        sword=None, sword_z=None, arm_s_z=None, arm_o_z=None,
        cape=None, cape_z=None,
        lantern="rest", lantern_off=(0, 0), lantern_z=None,
        head=None, head_off=(0, 0), torso_off=(0, 0), legs=True, arms=True) -> Pose:
    r = REST[d]
    z = z_order(d)
    p = Pose(dir=d, body=body, head=head, head_off=head_off, torso_off=torso_off)
    p.head_z = z["head"]
    p.torso_z = z["torso"]
    far_o = d == "side"
    if legs:
        hs = add(add(r["hip_s"], body), hip_s)
        ho = add(add(r["hip_o"], body), hip_o)
        p.legs = [
            leg(ho, ank_o or r["ankle_o"], bend_o, foot_o or DEFAULT_FOOT[d], far=far_o,
                z=z.get("leg_far", z["leg"]) if far_o else z["leg"]),
            leg(hs, ank_s or r["ankle_s"], bend_s, foot_s or DEFAULT_FOOT[d], z=z["leg"] + 0.1),
        ]
    if arms:
        ss = add(add(r["sh_s"], body), sh_s)
        so = add(add(r["sh_o"], body), sh_o)
        ws = wr_s or add(r["wr_s"], body)
        wo = wr_o or add(r["wr_o"], body)
        p.arms = [
            arm(so, wo, el_o, hand_o, far=far_o, z=arm_o_z if arm_o_z is not None else
                (z["arm_far"] if far_o else z["arm"]), sep=not far_o),
            arm(ss, ws, el_s, hand_s, z=arm_s_z if arm_s_z is not None else z["arm"]),
        ]
        if sword is not None:
            angle, grip_off = sword if isinstance(sword, tuple) else (sword, (0, 1))
            p.sword = Sword(angle, add(ws, grip_off), sword_z if sword_z is not None else z["sword"])
    if cape is not False:
        c = cape or {}
        p.cape = cape_for(d, body, **c)
        p.cape_z = cape_z if cape_z is not None else z["cape"]
    if lantern:
        p.lantern = (lantern, add(add(r["lantern"], body), lantern_off))
        p.lantern_z = lantern_z if lantern_z is not None else z["lantern"]
    return p
