#!/usr/bin/env python3
"""Recorta a folha do caçador do super kit em quadros alinhados pelo pé e gera o SpriteFrames do jogador.

Entrada: 02_PERSONAGENS_JOGADOR/sprite_sheet_do_caçador_crepuscular.png (1448x1086, fundo transparente).
Saída:   assets/sprites/player/hunter_<anim>_<n>.png (recorte justo de cada quadro, só os pixels daquele quadro)
         assets/sprites/player/hunter_frames.tres (SpriteFrames; cada quadro é um AtlasTexture com margem que o coloca
         numa tela comum, então todos têm o pé no mesmo ponto: metadata/anchor) e metadata/density (px por u a escala 1).

Recorte por transparência: componentes conexos da máscara alpha>100 numa grade 4x4 (o brilho do golpe une quadros
vizinhos na máscara fina), cada pixel com alpha>0 vai para o componente da célula (rótulos dilatados 6 células para
pegar a borda suave). Pé = linha mais baixa com >= 3 pixels alpha>128; x da âncora = centro de massa do tronco
(40%-70% da altura). Densidade = altura do corpo no quadro parado de frente / 32 u (altura opaca do c_sword_front no
atlas), para o mesmo characterScale do rules.json valer para o kit e para o atlas (DECISIONS D41/D43).
Na folha, os quadros de caminhada, golpe, dano e morte foram desenhados ~12% menores que os parados (a cabeça "encolhe"
ao andar); eles são ampliados (Lanczos) para a caminhada ter 97% da altura do parado (a passada abaixa um pouco o corpo).

Uso: python -I tools/sprites/cut_hunter.py <folha.png> [--out assets/sprites/player]
"""
from __future__ import annotations

import argparse
import sys
from collections import deque
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
CELL = 4
SEED_ALPHA = 100
DILATE = 6
ATLAS_BODY_HEIGHT_U = 32.0  # c_sword_front, altura opaca (alpha>128) / k
WALK_TO_IDLE = 0.97  # altura da caminhada sobre a do parado, depois da correção de escala
SMALL_ROW = ("walk_", "attack_", "hurt_", "death_")  # desenhados na escala menor da folha

# quadro -> centro aproximado na folha (px). A folha é fixa; o centro só escolhe o componente.
FRAMES = {
    "idle_front_0": (116, 300),
    "idle_side_0": (326, 300),
    "idle_back_0": (550, 302),
    "walk_side_0": (780, 324),
    "walk_side_1": (978, 322),
    "walk_side_2": (1158, 322),
    "walk_side_3": (1342, 322),
    "attack_0": (132, 654),
    "attack_1": (414, 640),
    "attack_2": (640, 664),
    "hurt_0": (142, 906),
    "death_0": (376, 922),
    "death_1": (724, 952),
}

# animação -> (quadros, fps, loop)
ANIMS = {
    "idle_front": (["idle_front_0"], 1, True),
    "idle_side": (["idle_side_0"], 1, True),
    "idle_back": (["idle_back_0"], 1, True),
    "walk_side": (["walk_side_0", "walk_side_1", "walk_side_2", "walk_side_3"], 7, True),
    "attack": (["attack_0", "attack_1", "attack_2"], 14, False),
    "hurt": (["hurt_0"], 1, False),
    "death": (["death_0", "death_1"], 4, False),
}


def label_components(img: Image.Image):
    w, h = img.size
    gw, gh = w // CELL, h // CELL
    seed = img.getchannel("A").point(lambda v: 255 if v > SEED_ALPHA else 0).resize((gw, gh), Image.BOX)
    sp = seed.load()
    labels = [[0] * gw for _ in range(gh)]
    comps = {}
    next_id = 1
    for y in range(gh):
        for x in range(gw):
            if sp[x, y] > 60 and labels[y][x] == 0:
                q = deque([(x, y)])
                labels[y][x] = next_id
                cells = []
                while q:
                    cx, cy = q.popleft()
                    cells.append((cx, cy))
                    for dx in (-1, 0, 1):
                        for dy in (-1, 0, 1):
                            nx, ny = cx + dx, cy + dy
                            if 0 <= nx < gw and 0 <= ny < gh and labels[ny][nx] == 0 and sp[nx, ny] > 60:
                                labels[ny][nx] = next_id
                                q.append((nx, ny))
                if len(cells) > 30:
                    comps[next_id] = cells
                next_id += 1
    # dilata os rótulos (borda suave e brilho)
    frontier = deque((x, y, 0) for y in range(gh) for x in range(gw) if labels[y][x] in comps)
    while frontier:
        x, y, d = frontier.popleft()
        if d >= DILATE:
            continue
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < gw and 0 <= ny < gh and labels[ny][nx] == 0:
                labels[ny][nx] = labels[y][x]
                frontier.append((nx, ny, d + 1))
    return labels, comps


def extract(img: Image.Image, labels, comp_id: int) -> Image.Image:
    w, h = img.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    src = img.load()
    dst = out.load()
    for y in range(h):
        row = labels[min(y // CELL, len(labels) - 1)]
        for x in range(w):
            if row[min(x // CELL, len(row) - 1)] == comp_id:
                p = src[x, y]
                if p[3] > 0:
                    dst[x, y] = p
    return out.crop(out.getbbox())


def anchor_of(frame: Image.Image) -> tuple[int, int]:
    a = frame.getchannel("A").load()
    w, h = frame.size
    foot = h - 1
    for y in range(h - 1, -1, -1):
        if sum(1 for x in range(w) if a[x, y] > 128) >= 3:
            foot = y
            break
    top = int(h * 0.4)
    bottom = max(top + 1, int(h * 0.7))
    total = sx = 0
    for y in range(top, bottom):
        for x in range(w):
            if a[x, y] > 128:
                total += 1
                sx += x
    ax = round(sx / total) if total else w // 2
    return ax, foot + 1


def body_height(frame: Image.Image, anchor_x: int, foot_y: int) -> int:
    """Pé até o topo da cabeça: primeira linha com >= 8 px opacos numa faixa de ±10% da largura em volta do tronco
    (o cabo da espada nas costas fica fora da faixa)."""
    a = frame.getchannel("A").load()
    w, _ = frame.size
    x0, x1 = max(0, int(anchor_x - w * 0.10)), min(w, int(anchor_x + w * 0.10))
    for y in range(foot_y):
        if sum(1 for x in range(x0, x1) if a[x, y] > 128) >= 8:
            return foot_y - y
    return foot_y


def write_tres(path: Path, frames: dict, canvas, anchor, density: float):
    names = list(frames)
    lines = [f'[gd_resource type="SpriteFrames" load_steps={len(names) * 2 + 1} format=3]', ""]
    for i, name in enumerate(names, 1):
        lines.append(f'[ext_resource type="Texture2D" path="res://assets/sprites/player/hunter_{name}.png" id="{i}"]')
    lines.append("")
    cw, ch = canvas
    for i, name in enumerate(names, 1):
        img, (ox, oy) = frames[name]
        w, h = img.size
        lines += [
            f'[sub_resource type="AtlasTexture" id="at_{name}"]',
            f'atlas = ExtResource("{i}")',
            f"region = Rect2(0, 0, {w}, {h})",
            f"margin = Rect2({ox}, {oy}, {cw - w}, {ch - h})",
            "",
        ]
    lines.append("[resource]")
    anims = []
    for anim, (fnames, fps, loop) in ANIMS.items():
        fr = ", ".join('{\n"duration": 1.0,\n"texture": SubResource("at_%s")\n}' % f for f in fnames)
        anims.append('{\n"frames": [%s],\n"loop": %s,\n"name": &"%s",\n"speed": %.1f\n}' % (fr, "true" if loop else "false", anim, fps))
    lines.append("animations = [%s]" % ", ".join(anims))
    lines.append(f"metadata/anchor = Vector2({anchor[0]}, {anchor[1]})")
    lines.append(f"metadata/density = {density:.4f}")
    lines.append('metadata/source = "super kit: 02_PERSONAGENS_JOGADOR/sprite_sheet_do_caçador_crepuscular.png"')
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("sheet")
    ap.add_argument("--out", default=str(ROOT / "assets" / "sprites" / "player"))
    args = ap.parse_args()
    img = Image.open(args.sheet).convert("RGBA")
    labels, comps = label_components(img)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)

    raw = {}
    for name, (cx, cy) in FRAMES.items():
        comp = labels[cy // CELL][cx // CELL]
        if comp not in comps:
            sys.exit(f"{name}: nenhum componente em ({cx},{cy})")
        frame = extract(img, labels, comp)
        ax, ay = anchor_of(frame)
        raw[name] = (frame, ax, ay, body_height(frame, ax, ay))

    idle = raw["idle_side_0"][3]
    walks = [raw[n][3] for n in raw if n.startswith("walk_")]
    k = idle * WALK_TO_IDLE / (sum(walks) / len(walks))
    print(f"quadros menores x{k:.3f} (parado {idle}px, caminhada {sum(walks) / len(walks):.0f}px)")
    cut = {}
    for name, (frame, ax, ay, height) in raw.items():
        if name.startswith(SMALL_ROW):
            frame = frame.resize((round(frame.size[0] * k), round(frame.size[1] * k)), Image.LANCZOS)
            ax, ay = anchor_of(frame)
            height = body_height(frame, ax, ay)
        cut[name] = (frame, ax, ay)
        print(f"{name}: {frame.size[0]}x{frame.size[1]} âncora ({ax},{ay}) corpo {height}px")

    left = max(ax for _, ax, _ in cut.values())
    right = max(f.size[0] - ax for f, ax, _ in cut.values())
    top = max(ay for _, _, ay in cut.values())
    bottom = max(f.size[1] - ay for f, _, ay in cut.values())
    canvas = (left + right, top + bottom)
    anchor = (left, top)
    frames = {}
    for name, (frame, ax, ay) in cut.items():
        frame.save(out / f"hunter_{name}.png")
        frames[name] = (frame, (left - ax, top - ay))

    front, fx, fy = cut["idle_front_0"]
    height_px = body_height(front, fx, fy)
    density = height_px / ATLAS_BODY_HEIGHT_U
    write_tres(out / "hunter_frames.tres", frames, canvas, anchor, density)
    print(f"tela {canvas[0]}x{canvas[1]}, âncora {anchor}, corpo {height_px}px -> densidade {density:.3f} px/u")


if __name__ == "__main__":
    main()
