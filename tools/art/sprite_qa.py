#!/usr/bin/env python3
"""QA visual dos sprites importados por import_strips.py: GIFs 1x sobre índigo, folha de contato por personagem
e folhas de comparação de escala (parado/andar/dano/ataque1 lado a lado, na mesma escala, com guias de pés e altura).

Uso (da raiz do repositório):
  python tools/art/sprite_qa.py
Saída: docs/v2/qa/sprites/*.gif, contato_<personagem>.png, comparacao_<personagem>.png, comparacao_todos.png
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
SPRITES = ROOT / "assets" / "v2" / "sprites"
QA = ROOT / "docs" / "v2" / "qa" / "sprites"
INDIGO = (0x1B, 0x1F, 0x3B, 255)
GUIDE = (242, 165, 65, 160)  # âmbar
CONTACT_SCALE = 0.5
COMPARE_ANIMS = ("parado", "andar", "dano", "ataque1", "tiro", "esquiva", "pesado", "morte")


def characters() -> list[tuple[str, Path]]:
    found = []
    for sidecar in sorted(SPRITES.rglob("*.json")):
        found.append((sidecar.stem, sidecar))
    return found


def load(sidecar: Path) -> tuple[dict, Image.Image, int]:
    meta = json.loads(sidecar.read_text(encoding="utf-8"))
    atlas = Image.open(sidecar.parent / meta["atlas"]).convert("RGBA")
    return meta, atlas, meta["cell"][0]


def frames_of(meta: dict, atlas: Image.Image, cell: int, name: str) -> list[Image.Image]:
    info = meta["animations"][name]
    return [atlas.crop((i * cell, info["row"] * cell, (i + 1) * cell, (info["row"] + 1) * cell)) for i in range(info["frames"])]


def over_indigo(image: Image.Image) -> Image.Image:
    background = Image.new("RGBA", image.size, INDIGO)
    background.alpha_composite(image)
    return background


def write_gifs(key: str, meta: dict, atlas: Image.Image, cell: int) -> int:
    count = 0
    for name, info in meta["animations"].items():
        frames = [over_indigo(f).convert("RGB") for f in frames_of(meta, atlas, cell, name)]
        duration = max(20, round(1000 / info["fps"] / 10) * 10)
        frames[0].save(QA / f"{key}_{name}.gif", save_all=True, append_images=frames[1:], duration=duration, loop=0, disposal=2)
        count += 1
    return count


def contact_sheet(key: str, meta: dict, atlas: Image.Image, cell: int) -> None:
    size = int(cell * CONTACT_SCALE)
    label_w = 150
    columns = max(a["frames"] for a in meta["animations"].values())
    names = list(meta["animations"])
    sheet = Image.new("RGBA", (label_w + columns * size, len(names) * size), INDIGO)
    draw = ImageDraw.Draw(sheet)
    anchor = meta["anchor"]
    for row, name in enumerate(names):
        y = row * size
        draw.text((4, y + size // 2 - 5), name, fill=(244, 231, 198, 255))
        feet_y = y + int(anchor[1] * CONTACT_SCALE)
        draw.line([(label_w, feet_y), (sheet.width, feet_y)], fill=(242, 165, 65, 70))
        for column, frame in enumerate(frames_of(meta, atlas, cell, name)):
            small = frame.resize((size, size), Image.LANCZOS)
            sheet.alpha_composite(small, (label_w + column * size, y))
            draw.line([(label_w + column * size + int(anchor[0] * CONTACT_SCALE),) * 1 + (y,), (label_w + column * size + int(anchor[0] * CONTACT_SCALE), y + size)], fill=(230, 74, 142, 40))
    sheet.convert("RGB").save(QA / f"contato_{key}.png")


def compare_sheet(key: str, meta: dict, atlas: Image.Image, cell: int, directions: list[str]) -> Image.Image:
    anims = [a for a in COMPARE_ANIMS if any(n.startswith(a + "_") or n == a for n in meta["animations"])]
    sheet = Image.new("RGBA", (cell * len(anims), cell * len(directions)), INDIGO)
    draw = ImageDraw.Draw(sheet)
    anchor, stand = meta["anchor"], meta["stand_height_px"]
    for row, direction in enumerate(directions):
        y0 = row * cell
        draw.line([(0, y0 + anchor[1]), (sheet.width, y0 + anchor[1])], fill=GUIDE)
        draw.line([(0, y0 + anchor[1] - stand), (sheet.width, y0 + anchor[1] - stand)], fill=GUIDE)
        for column, anim in enumerate(anims):
            name = f"{anim}_{direction}" if f"{anim}_{direction}" in meta["animations"] else anim
            if name not in meta["animations"]:
                continue
            frame = frames_of(meta, atlas, cell, name)[0]
            sheet.alpha_composite(frame, (column * cell, y0))
            draw.text((column * cell + 4, y0 + 4), name, fill=(244, 231, 198, 255))
    return sheet.convert("RGB")


def main() -> int:
    QA.mkdir(parents=True, exist_ok=True)
    hunters = {}
    for key, sidecar in characters():
        meta, atlas, cell = load(sidecar)
        gifs = write_gifs(key, meta, atlas, cell)
        contact_sheet(key, meta, atlas, cell)
        directions = ["baixo", "cima", "lado"] if key.endswith("_e1") else [""]
        if directions == [""]:
            continue
        cmp = compare_sheet(key, meta, atlas, cell, directions)
        cmp.save(QA / f"comparacao_{key}.png")
        hunters[key] = (meta, atlas, cell)
        print(f"{key}: {gifs} gifs, contato, comparação")
    if hunters:
        rows = [compare_sheet(k, m, a, c, ["lado"]) for k, (m, a, c) in hunters.items()]
        width = max(r.width for r in rows)
        total = Image.new("RGB", (width, sum(r.height for r in rows)), INDIGO[:3])
        offset = 0
        for row in rows:
            total.paste(row, (0, offset))
            offset += row.height
        total.save(QA / "comparacao_todos.png")
    return 0


if __name__ == "__main__":
    sys.exit(main())
