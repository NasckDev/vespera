#!/usr/bin/env python3
"""Importa os objetos do pacote de arte e gera data/art_catalog.json.

Uso:
  python tools/art/build_art_catalog.py --pack <pasta assets/v2/cenarios do pacote> [--v01 <assets/sprites/objects da v0.1>]

- Copia cada PNG para assets/v2/objetos/<lugar>/ (arquivos idênticos entre lugares são copiados uma vez
  e os demais apontam para a cópia: campo "duplicate_of").
- Para cada objeto calcula tamanho, âncora do pé e a pegada (faixa opaca de baixo), e deduz uma dica de interação.
- Sem --pack apenas recalcula o catálogo a partir de assets/v2/objetos.

Âncora = (centro horizontal das linhas opacas mais baixas, linha opaca mais baixa).
Pegada = retângulo [x, y, w, h] da faixa opaca inferior (FOOT_BAND da altura opaca, mínimo MIN_BAND px),
         em pixels do sprite; a colisão do mapa usa este retângulo posicionado pela âncora.
"""
import argparse
import hashlib
import json
import re
import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OBJ_DIR = ROOT / "assets" / "v2" / "objetos"
CATALOG = ROOT / "data" / "art_catalog.json"

FOOT_BAND = 0.18
MIN_BAND = 6
ALPHA_MIN = 24
ANCHOR_ROWS = 4

# (regex no nome, dica). A primeira que casar vence.
HINTS = [
    (r"chest", "chest"),
    (r"door|gate|grate|sewer_outflow", "door"),
    (r"notice_board|banner_stand", "notice_board"),
    (r"well", "well"),
    (r"fountain", "fountain"),
    (r"lamp_post|lantern|brazier|kiln|furnace|forge", "lamp"),
    (r"anvil|weapon_rack|chopping_block", "workstation"),
    (r"stall|cart", "stall"),
    (r"crate|barrel|bucket|urn|jar", "container"),
    (r"statue", "statue"),
    (r"bench", "seat"),
    (r"crack_brecha|crystal", "brecha"),
]


def hint_for(name: str) -> str:
    for pattern, hint in HINTS:
        if re.search(pattern, name):
            return hint
    return "decor"


def measure(path: Path):
    image = Image.open(path).convert("RGBA")
    alpha = image.getchannel("A").point(lambda v: 255 if v >= ALPHA_MIN else 0)
    bbox = alpha.getbbox()
    if bbox is None:
        raise ValueError(f"{path.name}: imagem sem pixels opacos")
    left, top, right, bottom = bbox
    height = bottom - top
    band = max(MIN_BAND, round(height * FOOT_BAND))
    band_top = max(top, bottom - band)
    foot = alpha.crop((left, band_top, right, bottom)).getbbox()
    fx0, _, fx1, _ = foot
    fx0 += left
    fx1 += left
    # âncora: centro horizontal das ANCHOR_ROWS linhas mais baixas
    rows = alpha.crop((left, max(top, bottom - ANCHOR_ROWS), right, bottom))
    xs = [x for x in range(rows.width) for y in range(rows.height) if rows.getpixel((x, y))]
    anchor_x = round(left + sum(xs) / len(xs)) if xs else (left + right) // 2
    return {
        "size": [image.width, image.height],
        "opaque_bbox": [left, top, right - left, bottom - top],
        "anchor": [anchor_x, bottom],
        "footprint": [fx0, band_top, fx1 - fx0, bottom - band_top],
    }


def import_pack(pack: Path):
    seen = {}
    entries = []
    for place_dir in sorted(p for p in pack.iterdir() if p.is_dir()):
        place = place_dir.name
        dest = OBJ_DIR / place
        dest.mkdir(parents=True, exist_ok=True)
        if (place_dir / "MANIFEST.json").exists():
            shutil.copy2(place_dir / "MANIFEST.json", dest / "MANIFEST.json")
        for src in sorted((place_dir / "objetos").glob("*.png")):
            name = src.stem.removeprefix(f"obj_{place}_")
            digest = hashlib.sha1(src.read_bytes()).hexdigest()
            if digest in seen:
                target = seen[digest]
                entries.append((place, name, target, f"{target[0]}.{target[1]}"))
                continue
            shutil.copy2(src, dest / f"{name}.png")
            seen[digest] = (place, name)
            entries.append((place, name, (place, name), None))
    return entries


def import_v01(v01: Path, picks):
    entries = []
    dest = OBJ_DIR / "v01"
    dest.mkdir(parents=True, exist_ok=True)
    for rel in picks:
        src = v01 / (rel + ".png")
        name = rel.replace("/", "_")
        shutil.copy2(src, dest / f"{name}.png")
        entries.append(("v01", name, ("v01", name), None))
    return entries


V01_PICKS = [
    "cathedral/chest_closed", "cathedral/chest_open", "common/chest_iron", "common/chest_gold",
    "cathedral/door_arched", "cathedral/door_barred", "cathedral/door_double_arched",
    "astral/lantern_standing", "market/gate_iron_small",
]


def build_catalog(entries):
    items = []
    for place, name, (real_place, real_name), dup in entries:
        path = OBJ_DIR / real_place / f"{real_name}.png"
        data = measure(path)
        item = {
            "id": f"{place}.{name}",
            "path": f"res://assets/v2/objetos/{real_place}/{real_name}.png",
            "place": place,
            **data,
            "interactive": hint_for(name),
        }
        if dup:
            item["duplicate_of"] = dup
        items.append(item)
    return items


def scan_existing():
    entries = []
    for place_dir in sorted(p for p in OBJ_DIR.iterdir() if p.is_dir()):
        for png in sorted(place_dir.glob("*.png")):
            entries.append((place_dir.name, png.stem, (place_dir.name, png.stem), None))
    return entries


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--pack", type=Path)
    parser.add_argument("--v01", type=Path)
    args = parser.parse_args()

    if args.pack:
        entries = import_pack(args.pack)
        if args.v01:
            entries += import_v01(args.v01, V01_PICKS)
    else:
        entries = scan_existing()

    items = build_catalog(entries)
    CATALOG.parent.mkdir(parents=True, exist_ok=True)
    document = {
        "version": 1,
        "note": "anchor e footprint em pixels do sprite (origem no canto superior esquerdo). Escala 1.0 = 1920x1080.",
        "objects": items,
    }
    CATALOG.write_text(json.dumps(document, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    dups = sum(1 for i in items if "duplicate_of" in i)
    print(f"{len(items)} objetos no catálogo ({dups} duplicados apontando para a cópia original)")


if __name__ == "__main__":
    main()
