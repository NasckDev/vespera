"""Previa em Python (sem Godot) a partir dos layouts: resolve terrenos por cantos como o MATCH_CORNERS do Godot.

Uso: python -I tools/tiles/preview_py.py   -> assets/v2/tiles/preview/{praca,esgoto}_py_4x.png (+ colisao)
Serve de conferencia rapida da arte; a verificacao oficial e tools/tiles/verify/verify_tileset.gd.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "assets" / "v2" / "tiles"
T = 16

def load(name):
    lay = json.loads((OUT / f"{name}_layout.json").read_text(encoding="utf-8"))
    tiles, by_corner, by_name = {}, {}, {}
    for src in lay["sources"]:
        png = Image.open(OUT / Path(src["png"]).name).convert("RGBA")
        for t in src["tiles"]:
            t["src"] = src
            t["png_img"] = png
            by_name[t["name"]] = t
            if t["corners"] is not None:
                by_corner.setdefault(tuple(t["corners"]), []).append(t)
    return lay, by_name, by_corner


def tile_img(t, frame=0):
    return t["png_img"].crop(((t["x"] + (frame if t["src"]["animated"] else 0)) * T, t["y"] * T,
                              (t["x"] + 1 + (frame if t["src"]["animated"] else 0)) * T, (t["y"] + 1) * T))


def pick(cands, rng):
    w = np.array([c["probability"] for c in cands], float)
    return cands[rng.choice(len(cands), p=w / w.sum())]


def render(name, ascii_map, tmap, overlay, base_t, extras, frame=0, seed=3):
    lay, by_name, by_corner = load(name)
    rng = np.random.default_rng(seed)
    h, w = len(ascii_map), len(ascii_map[0])
    # semantica do set_cells_terrain_connect (MATCH_CORNERS): um canto so e do terreno T se as 4 celulas ao redor
    # forem T; nas bordas da regiao a transicao fica DENTRO das celulas de borda (a regiao "encolhe" meio tile).
    base = base_t[0]
    vert = np.full((h + 1, w + 1), base, int)
    for vy in range(1, h):
        for vx in range(1, w):
            ids = {tmap[ascii_map[vy + dy][vx + dx]] for dy in (-1, 0) for dx in (-1, 0)}
            if len(ids) == 1:
                vert[vy, vx] = ids.pop()
    img = Image.new("RGBA", (w * T, h * T), (0, 0, 0, 255))
    col = Image.new("RGBA", (w * T, h * T), (0, 0, 0, 0))
    d = ImageDraw.Draw(col)
    for y in range(h):
        for x in range(w):
            key = tuple(int(vert[y + a][x + b]) for a, b in ((0, 0), (0, 1), (1, 1), (1, 0)))
            c = by_corner.get(key)
            if c is None:  # sem tile exato: aceita o mais parecido
                best = max(by_corner, key=lambda k: sum(1 for p, q in zip(k, key) if p == q))
                c = by_corner[best]
            t = pick(c, rng)
            img.alpha_composite(tile_img(t, frame), (x * T, y * T))
            draw_collision(d, t, x, y)
    for (x, y, nm) in overlay:
        t = by_name[nm]
        img.alpha_composite(tile_img(t, frame), (x * T, y * T))
        draw_collision(d, t, x, y)
    return img, col


def draw_collision(d, t, x, y):
    for poly in t["collision"]:
        pts = [((x + 0.5) * T + px, (y + 0.5) * T + py) for px, py in poly]
        d.polygon(pts, fill=(230, 40, 60, 140), outline=(255, 255, 255, 220))


def save(img, col, stem, scale=4):
    big = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
    big.convert("RGB").save(OUT / "preview" / f"{stem}_4x.png")
    over = img.copy()
    over.alpha_composite(col)
    over.resize((over.width * scale, over.height * scale), Image.NEAREST).convert("RGB").save(
        OUT / "preview" / f"{stem}_collision_4x.png")


def main():
    (OUT / "preview").mkdir(parents=True, exist_ok=True)
    maps = json.loads((Path(__file__).resolve().parent / "verify" / "maps.json").read_text(encoding="utf-8"))
    for name in ("praca", "esgoto"):
        m = maps[name]
        img, col = render(name, m["rows"], m["terrain_of"], [tuple(o) for o in m["overlay"]], m["paint_order"], None)
        save(img, col, f"{name}_py")


if __name__ == "__main__":
    main()
