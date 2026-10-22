"""Gera a Lâmina estágio 1 (aprendiz): folhas, SpriteFrames, anims.json e prévias.

Uso (na raiz do repo):  tools/art/.venv/Scripts/python -I tools/art/build_lamina.py
"""
from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # -I não põe a pasta do script no sys.path

import anims_combat  # noqa: E402
import anims_move  # noqa: E402
import anims_react  # noqa: E402
import export as ex  # noqa: E402
from rig import ANCHOR, CELL, compose  # noqa: E402

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(REPO, "assets", "v2", "characters", "lamina")
RES = "res://assets/v2/characters/lamina"

BUILDERS = {**anims_move.ANIMS, **anims_combat.ANIMS, **anims_react.ANIMS}

# name: fps, loop, durações relativas por quadro (o impacto segura mais), eventos
ATTACK_DUR = [1.3, 0.7, 1.4, 1.0, 1.4]
ANIMS = [
    {"name": "idle", "fps": 6, "loop": True},
    {"name": "walk", "fps": 12, "loop": True, "footsteps": [0, 4]},
    {"name": "dodge", "fps": 15, "loop": False, "iframes": [1, 2, 3], "footsteps": [0, 4],
     "durations": [1.0, 1.0, 1.0, 1.0, 1.6]},
    {"name": "attack1", "fps": 15, "loop": False, "hit": [1, 2], "durations": ATTACK_DUR, "combo_next": "attack2"},
    {"name": "attack2", "fps": 15, "loop": False, "hit": [1, 2], "durations": ATTACK_DUR, "combo_next": "attack3"},
    {"name": "attack3", "fps": 14, "loop": False, "hit": [2, 3], "durations": [1.8, 0.7, 1.6, 1.2, 1.8]},
    {"name": "heavy_charge", "fps": 8, "loop": True},
    {"name": "heavy_strike", "fps": 16, "loop": False, "hit": [1, 2, 3, 4],
     "durations": [1.2, 1.0, 1.0, 1.0, 1.0, 2.0]},
    {"name": "skill", "fps": 14, "loop": False, "hit": [2, 3], "durations": [2.0, 0.8, 1.0, 1.4, 1.2, 1.4]},
    {"name": "hurt", "fps": 10, "loop": False, "durations": [1.4, 1.0]},
    {"name": "death", "fps": 8, "loop": False, "durations": [1.0, 1.0, 1.4, 1.2, 1.0, 1.6, 3.0]},
]


def build() -> None:
    sheets = ex.ensure_dir(os.path.join(OUT, "sheets"))
    prev = ex.ensure_dir(os.path.join(OUT, "preview"))
    specs, contact, meta = [], [], {}
    for a in ANIMS:
        name = a["name"]
        per_dir_img, per_dir_layers = {}, {}
        for d in ex.DIRS:
            frames = [compose(p) for p in BUILDERS[name](d)]
            per_dir_img[d] = [f["image"] for f in frames]
            per_dir_layers[d] = [f["layers"] for f in frames]
        n = len(per_dir_img["down"])
        assert all(len(per_dir_img[d]) == n for d in ex.DIRS), name
        durs = a.get("durations", [1.0] * n)
        assert len(durs) == n, name
        fname = f"lamina_{name}.png"
        png = os.path.join(sheets, fname)
        ex.write_sheet(png, [per_dir_img[d] for d in ex.DIRS])
        ex.write_import(png, f"{RES}/sheets/{fname}")
        specs.append({"name": name, "file": f"sheets/{fname}", "frames": n, "fps": a["fps"], "loop": a["loop"],
                      "durations": durs})
        ms = [round(1000 * dur / a["fps"]) for dur in durs]
        ex.write_gif(os.path.join(prev, f"{name}.gif"), per_dir_img, ms, a["loop"])
        for d in ex.DIRS:
            contact.append((f"{name}\n{d}", per_dir_img[d]))
        meta[name] = _meta(a, n, durs, per_dir_layers)
    ex.write_tres(os.path.join(OUT, "lamina_aprendiz.tres"), RES, specs)
    ex.write_contact(os.path.join(prev, "contact_sheet.png"), contact)
    ex.write_json(os.path.join(OUT, "anims.json"), {
        "character": "lamina", "stage": "aprendiz",
        "cell": [CELL, CELL], "anchor": list(ANCHOR),
        "anchor_note": "pixel (x, y) do chão sob os pés dentro da célula; no Godot: centered=false, "
                       f"offset=({-ANCHOR[0]}, {-ANCHOR[1]}). Esquerda = 'side' com flip_h e offset.x={-(CELL - ANCHOR[0])}"
                       " (espelho em torno do pé), e os retângulos de 'side' com x espelhado: x' = -(x + w).",
        "directions": list(ex.DIRS), "sheet_layout": "linhas = down, up, side; colunas = quadros",
        "animations": meta,
    })
    print(f"ok: {len(ANIMS)} animações x {len(ex.DIRS)} direções -> {OUT}")


def _meta(a: dict, n: int, durs: list, layers: dict) -> dict:
    m = {"file": f"sheets/lamina_{a['name']}.png", "frames": n, "fps": a["fps"], "loop": a["loop"],
         "durations": durs, "events": {}}
    if "footsteps" in a:
        m["events"]["footstep"] = a["footsteps"]
    if "iframes" in a:
        m["events"]["iframes"] = a["iframes"]
    if "combo_next" in a:
        m["combo_next"] = a["combo_next"]
    if "hit" in a:
        boxes = {}
        for d in ex.DIRS:
            boxes[d] = {str(i): ex.hitbox_from_layers(layers[d][i]) for i in a["hit"]}
        m["events"]["hitbox"] = {"frames": a["hit"], "rect_by_dir": boxes,
                                 "rect_note": "px relativos ao pé; y para baixo; tirado da lâmina + rastro do quadro"}
    return m


if __name__ == "__main__":
    build()
