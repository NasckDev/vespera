"""Exportação: folhas PNG (grade fixa 48x48), SpriteFrames .tres do Godot, anims.json com eventos, prévias GIF 4x
e folha de contato. Só escreve cores da paleta mestre (o fundo das prévias também é da paleta).
"""
from __future__ import annotations

import json
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from pixcore import PAL, canvas, to_rgba
from rig import ANCHOR, CELL

DIRS = ("down", "up", "side")
PREVIEW_BG = "indigo:1"
SHADOW = "indigo:0"


def _bbox(mask: np.ndarray):
    ys, xs = np.nonzero(mask)
    if not len(ys):
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def hitbox_from_layers(layers: dict) -> dict | None:
    """Caixa do golpe = retângulo da lâmina + rastro no quadro, relativo ao pé (ANCHOR), em pixels."""
    mask = np.zeros((CELL, CELL), bool)
    for name in ("weapon", "aura"):
        if name in layers:
            mask |= layers[name] >= 0
    bb = _bbox(mask)
    if bb is None:
        return None
    x0, y0, x1, y1 = bb
    return {"x": x0 - ANCHOR[0], "y": y0 - ANCHOR[1], "w": x1 - x0, "h": y1 - y0}


def write_sheet(path: str, rows: list[list[np.ndarray]]) -> None:
    cols = max(len(r) for r in rows)
    sheet = canvas(cols * CELL, len(rows) * CELL)
    for j, row in enumerate(rows):
        for i, img in enumerate(row):
            sheet[j * CELL:(j + 1) * CELL, i * CELL:(i + 1) * CELL] = img
    Image.fromarray(to_rgba(sheet), "RGBA").save(path, optimize=True)


def write_tres(path: str, res_dir: str, specs: list[dict]) -> None:
    """SpriteFrames com uma animação por (anim, direção): 'idle_down', 'walk_side'... Esquerda = flip_h de 'side'."""
    ext, subs, anims = [], [], []
    for k, spec in enumerate(specs, start=1):
        ext.append(f'[ext_resource type="Texture2D" path="{res_dir}/{spec["file"]}" id="{k}_{spec["name"]}"]')
        for j, d in enumerate(DIRS):
            frames = []
            for i in range(spec["frames"]):
                sid = f"{spec['name']}_{d}_{i}"
                subs.append(f'[sub_resource type="AtlasTexture" id="{sid}"]\natlas = ExtResource("{k}_{spec["name"]}")\n'
                            f"region = Rect2({i * CELL}, {j * CELL}, {CELL}, {CELL})\n")
                dur = spec["durations"][i]
                frames.append(f'{{\n"duration": {dur:.2f},\n"texture": SubResource("{sid}")\n}}')
            anims.append(f'{{\n"frames": [{", ".join(frames)}],\n"loop": {str(spec["loop"]).lower()},\n'
                         f'"name": &"{spec["name"]}_{d}",\n"speed": {float(spec["fps"]):.1f}\n}}')
    steps = len(ext) + len(subs) + 1
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(f'[gd_resource type="SpriteFrames" load_steps={steps} format=3]\n\n')
        fh.write("\n".join(ext) + "\n\n")
        fh.write("\n".join(subs) + "\n")
        fh.write("[resource]\nanimations = [" + ", ".join(anims) + "]\n")


IMPORT_TEMPLATE = """[remap]

importer="texture"
type="CompressedTexture2D"

[deps]

source_file="{src}"

[params]

compress/mode=0
compress/high_quality=false
compress/lossy_quality=0.7
mipmaps/generate=false
mipmaps/limit=-1
process/fix_alpha_border=false
process/premult_alpha=false
process/size_limit=0
detect_3d/compress_to=0
"""


def write_import(png_path: str, res_path: str) -> None:
    """.import mínimo para pixel art (sem mipmaps, sem compressão com perda). O Godot completa o resto."""
    with open(png_path + ".import", "w", encoding="utf-8", newline="\n") as fh:
        fh.write(IMPORT_TEMPLATE.format(src=res_path))


# ------------------------------------------------------------------ prévias
def _shadow() -> np.ndarray:
    img = canvas(CELL, CELL)
    cx, cy = ANCHOR
    for y in range(cy - 2, cy + 2):
        for x in range(cx - 8, cx + 9):
            if ((x - cx + 0.5) / 8.5) ** 2 + ((y - cy + 0.5) / 2.2) ** 2 <= 1:
                img[y, x] = PAL.ref(SHADOW)
    return img


SHADOW_IMG = None


def preview_frame(img: np.ndarray, flip=False) -> np.ndarray:
    global SHADOW_IMG
    if SHADOW_IMG is None:
        SHADOW_IMG = _shadow()
    base = np.full((CELL, CELL), PAL.ref(PREVIEW_BG), np.int16)
    m = SHADOW_IMG >= 0
    base[m] = SHADOW_IMG[m]
    src = img
    if flip:
        from rig import flip_x
        src = flip_x(img)
    m = src >= 0
    base[m] = src[m]
    return base


def write_gif(path: str, per_dir: dict, durations_ms: list[int], loop: bool, scale: int = 4) -> None:
    """Uma linha com as 4 direções (baixo, cima, lado-direita, lado-esquerda espelhado)."""
    order = [("down", False), ("up", False), ("side", False), ("side", True)]
    n = len(durations_ms)
    gap = 2
    frames = []
    for i in range(n):
        strip = np.full((CELL, 4 * CELL + 3 * gap), PAL.ref("indigo:0"), np.int16)
        for k, (d, fl) in enumerate(order):
            strip[:, k * (CELL + gap):k * (CELL + gap) + CELL] = preview_frame(per_dir[d][i], fl)
        rgb = to_rgba(strip)[..., :3]
        im = Image.fromarray(rgb, "RGB").resize((strip.shape[1] * scale, CELL * scale), Image.NEAREST)
        frames.append(im)
    durs = list(durations_ms)
    if not loop:
        durs[-1] = max(durs[-1], 700)   # segura o último quadro para leitura
    frames[0].save(path, save_all=True, append_images=frames[1:], duration=durs, loop=0, disposal=1,
                   optimize=False)


def write_contact(path: str, rows: list[tuple[str, list[np.ndarray]]], scale: int = 3) -> None:
    label_w = 30
    cols = max(len(r[1]) for r in rows)
    H = len(rows) * (CELL + 1)
    W = label_w + cols * (CELL + 1)
    sheet = np.full((H, W), PAL.ref("indigo:0"), np.int16)
    for j, (_, frames) in enumerate(rows):
        for i, img in enumerate(frames):
            y, x = j * (CELL + 1), label_w + i * (CELL + 1)
            sheet[y:y + CELL, x:x + CELL] = preview_frame(img)
    rgb = Image.fromarray(to_rgba(sheet)[..., :3], "RGB").resize((W * scale, H * scale), Image.NEAREST)
    draw = ImageDraw.Draw(rgb)
    draw.fontmode = "1"   # sem antialias: o texto também fica na paleta
    font = ImageFont.load_default()
    cream = PAL.colors[PAL.ref("cream:3")]
    for j, (label, _) in enumerate(rows):
        draw.text((4, j * (CELL + 1) * scale + 6), label, fill=cream, font=font)
    rgb.save(path, optimize=True)


def write_json(path: str, data: dict) -> None:
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(data, fh, ensure_ascii=False, indent=1)
        fh.write("\n")


def ensure_dir(p: str) -> str:
    os.makedirs(p, exist_ok=True)
    return p
