"""Gera os atlases PNG e os layouts JSON dos tilesets 'praca' e 'esgoto'.

Uso (a partir da raiz do repo):  python -I tools/tiles/gen_tiles.py
Saida: assets/v2/tiles/{praca,praca_water,esgoto,esgoto_water}.png e {praca,esgoto}_layout.json.
Depois rode tools/tiles/build_tilesets.ps1 para o Godot montar os .tres a partir do layout.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import tex_build as tb  # noqa: E402
import tex_ground as tg  # noqa: E402
from tiles_lib import T, ramp, rng_of, to_image  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "assets" / "v2" / "tiles"
FRAMES = 4
FRAME_SECONDS = 0.25
FULL = [(-8, -8), (8, -8), (8, 8), (-8, 8)]
QUAD = {  # canto -> retangulo (x0,y0,x1,y1) em coordenadas centradas no tile
    0: (-8, -8, 0, 0), 1: (0, -8, 8, 0), 2: (0, 0, 8, 8), 3: (-8, 0, 0, 8)}


def rect(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def water_collision(bits):
    """Poligonos de colisao dos cantos com agua (tl,tr,br,bl): cobre a agua com folga, nunca o resto."""
    n = sum(bits)
    if n == 4:
        return [FULL]
    if n == 3:
        miss = bits.index(0)
        # L: o quadrado todo menos o canto ausente
        L = {0: [(0, -8), (8, -8), (8, 8), (-8, 8), (-8, 0), (0, 0)],
             1: [(-8, -8), (0, -8), (0, 0), (8, 0), (8, 8), (-8, 8)],
             2: [(-8, -8), (8, -8), (8, 0), (0, 0), (0, 8), (-8, 8)],
             3: [(-8, -8), (8, -8), (8, 8), (0, 8), (0, 0), (-8, 0)]}
        return [L[miss]]
    on = [i for i in range(4) if bits[i]]
    if n == 2 and (on[1] - on[0]) in (1, 3):
        a, b = on
        if (a, b) == (0, 1):
            return [rect(-8, -8, 8, 0)]
        if (a, b) == (1, 2):
            return [rect(0, -8, 8, 8)]
        if (a, b) == (2, 3):
            return [rect(-8, 0, 8, 8)]
        return [rect(-8, -8, 0, 8)]
    return [rect(*QUAD[i]) for i in on]


class Atlas:
    def __init__(self, name, cols, rows):
        self.name, self.cols, self.rows = name, cols, rows
        self.img = Image.new("RGBA", (cols * T, rows * T), (0, 0, 0, 0))
        self.tiles = []

    def blit(self, arr, x, y):
        self.img.paste(Image.fromarray(arr, "RGBA"), (x * T, y * T))

    def add(self, arr, x, y, name, kind, collision=None, terrain=-1, corners=None, prob=1.0, frames=None):
        if frames is None:
            self.blit(arr, x, y)
        else:
            for f, a in enumerate(frames):
                self.blit(a, x + f, y)
        self.tiles.append({"name": name, "x": x, "y": y, "kind": kind, "collision": collision or [],
                           "terrain": terrain, "corners": corners, "probability": prob})

    def save(self, png):
        self.img.save(OUT / png)


def block16(atlas, x0, y0, prefix, kind, A, B, edge_fn, terrain_a, terrain_b, collide=False):
    """Bloco 4x4 MATCH_CORNERS: tile i (1..14) -> bit0=TL, bit1=TR, bit2=BR, bit3=BL (1 = terreno B)."""
    for i in range(1, 15):
        bits = (i & 1, (i >> 1) & 1, (i >> 2) & 1, (i >> 3) & 1)
        arr = edge_fn(A, B, tg.corner_mask(bits, rng_of(prefix, i)), rng_of(prefix, "e", i))
        corners = [terrain_b if b else terrain_a for b in bits]
        main = terrain_b  # centro = terreno pintado por cima, senao cantos convexos nao casam
        atlas.add(arr, x0 + i % 4, y0 + i // 4, f"{prefix}_{i:02d}", kind, None, main, corners)


def water_block(atlas, bank, bank_ramp, ramp_name, seed, terrain_water, terrain_bank, prefix):
    """Fonte animada: tile i em (4*(i%4), i//4); 4 quadros lado a lado. Slot 0 = agua cheia extra."""
    def frames_for(bits, extra=0):
        return [tg.water_shore(f, bits, f"{prefix}shore", bank, tg.water_tex(f, ramp_name, seed, extra),
                               ramp_name, bank_ramp) for f in range(FRAMES)]
    full = (1, 1, 1, 1)
    atlas.add(None, 0, 0, f"{prefix}_full_b", "water", water_collision(full), terrain_water, [terrain_water] * 4,
              0.7, frames_for(full, 1))
    for i in range(1, 16):
        bits = (i & 1, (i >> 1) & 1, (i >> 2) & 1, (i >> 3) & 1)
        corners = [terrain_water if b else terrain_bank for b in bits]
        main = terrain_water
        atlas.add(None, 4 * (i % 4), i // 4, f"{prefix}_{i:02d}", "water", water_collision(bits), main, corners,
                  1.0, frames_for(bits))


def wall_group(atlas, x0, y0, makers, kinds):
    """3 colunas (L,M,R) por linha de makers [(nome, fn(side))]."""
    for r, (nm, fn) in enumerate(makers):
        for c, side in enumerate("LMR"):
            atlas.add(fn(side), x0 + c, y0 + r, f"{nm}_{side.lower()}", "wall", [FULL])


def build_praca():
    a = Atlas("praca", 16, 9)
    for v in range(4):
        a.add(tg.cobble(v), v, 0, f"cobble_{v}", "floor", terrain=1, corners=[1] * 4)
    for v in range(3):
        a.add(tg.grass(v), 4 + v, 0, f"grass_{v}", "floor", terrain=0, corners=[0] * 4)
    for v in range(3):
        a.add(tg.grass(10 + v, flowers=1 + (v == 2)), 7 + v, 0, f"grass_flowers_{v}", "floor",
              terrain=0, corners=[0] * 4, prob=0.12)
    for v in range(3):
        a.add(tg.dirt(v), 10 + v, 0, f"dirt_{v}", "floor", terrain=2, corners=[2] * 4)
    block16(a, 0, 1, "grass_cobble", "floor", tg.grass(0), tg.cobble(0), tg.edge_grass_cobble, 0, 1)
    block16(a, 4, 1, "grass_dirt", "floor", tg.grass(0), tg.dirt(0), tg.edge_grass_dirt, 0, 2)
    block16(a, 8, 5, "cobble_dirt", "floor", tg.cobble(0), tg.dirt(0), tg.edge_cobble_dirt, 1, 2)
    a.add(tb.wall_shadow(), 12, 5, "wall_shadow", "deco")
    wall_group(a, 8, 1, [("roof_cap", lambda s: tb.roof_cap(s)), ("roof_fill", lambda s: tb.roof_fill(s)),
                         ("face_upper", lambda s: tb.face_upper(s)), ("face_lower", lambda s: tb.face_lower(s))], None)
    a.add(tb.roof_cap("M", 1), 11, 1, "roof_cap_m_alt", "wall", [FULL])
    a.add(tb.roof_fill("M", 1), 11, 2, "roof_fill_m_alt", "wall", [FULL])
    a.add(tb.face_upper("M", 0, window=True), 11, 3, "face_upper_window", "wall", [FULL])
    a.add(tb.face_lower("M", 0, door=True), 11, 4, "face_lower_door", "wall", [FULL])
    a.add(tb.face_upper("M", 1), 12, 3, "face_upper_m_alt", "wall", [FULL])
    a.add(tb.face_lower("M", 1), 12, 4, "face_lower_m_alt", "wall", [FULL])
    a.add(tb.face_upper("M", 2, window=True), 12, 2, "face_upper_window_b", "wall", [FULL])
    for c, side in enumerate("LMR"):
        a.add(tb.stairs(side), 13 + c, 1, f"stairs_{side.lower()}", "floor")
    for c, (nm, arr) in enumerate(tb.curb_layers().items()):
        a.add(arr, c, 5, nm, "deco")
    a.save("praca.png")

    w = Atlas("praca_water", 16, 4)
    water_block(w, tg.grass(0), ramp("dirt"), "water", "water", 3, 0, "water")
    w.save("praca_water.png")
    return a, w


def build_esgoto():
    a = Atlas("esgoto", 16, 6)
    for v in range(4):
        a.add(tg.sew_floor(v), v, 0, f"floor_{v}", "floor", terrain=0, corners=[0] * 4)
    a.add(tg.sew_floor_rift(0), 4, 0, "floor_rift_crack", "floor", terrain=0, corners=[0] * 4, prob=0.06)
    for v in range(2):
        a.add(tg.sew_mossy(v), 5 + v, 0, f"floor_moss_{v}", "floor", terrain=0, corners=[0] * 4, prob=0.35)
    a.add(tb.floor_grate(0), 7, 0, "grate_bars", "floor")
    a.add(tb.floor_grate(1), 8, 0, "grate_drain", "floor")
    for v in range(4):
        a.add(tb.debris(v), 9 + v, 0, f"debris_pottery_{v}", "deco")
    wall_group(a, 0, 1, [("top_cap", lambda s: tb.sew_top_cap(s)), ("top_fill", lambda s: tb.sew_top_fill(s)),
                         ("face_upper", lambda s: tb.sew_face_upper(s)),
                         ("face_lower", lambda s: tb.sew_face_lower(s))], None)
    a.add(tb.sew_top_fill("M", 1), 3, 2, "top_fill_m_alt", "wall", [FULL])
    a.add(tb.sew_top_cap("M", 1), 3, 1, "top_cap_m_alt", "wall", [FULL])
    a.add(tb.sew_face_upper("M", 1, slime=True), 3, 3, "face_upper_slime", "wall", [FULL])
    a.add(tb.sew_face_upper("M", 2, rift=True), 4, 3, "face_upper_rift", "wall", [FULL])
    a.add(tb.sew_face_lower("M", 1, grate=True), 3, 4, "face_lower_grate", "wall", [FULL])
    a.add(tb.sew_face_lower("M", 2), 4, 4, "face_lower_m_alt", "wall", [FULL])
    a.add(tb.void_tile(), 5, 1, "void", "wall", [FULL])
    a.save("esgoto.png")

    w = Atlas("esgoto_water", 16, 4)
    water_block(w, tg.sew_floor(0), ramp("sew_floor"), "sewage", "sewage", 1, 0, "sewage")
    w.save("esgoto_water.png")
    return a, w


def write_layout(name, static, water, terrains):
    def entry(atlas, sid, animated):
        d = {"id": sid, "png": f"res://assets/v2/tiles/{atlas.name}.png", "animated": animated,
             "tiles": atlas.tiles}
        if animated:
            d.update({"frames": FRAMES, "frame_seconds": FRAME_SECONDS})
        return d
    layout = {"name": name, "tile_size": T, "terrain_mode": "MATCH_CORNERS", "terrains": terrains,
              "sources": [entry(static, 0, False), entry(water, 1, True)]}
    (OUT / f"{name}_layout.json").write_text(json.dumps(layout, indent=1), encoding="utf-8")


def contact_sheet(static, water, path, scale=3):
    """Folha de contato: atlas estatico + agua (quadro 0) sobre xadrez, em escala inteira."""
    sw = static.img
    wimg = Image.new("RGBA", (16 * T, 4 * T + T), (0, 0, 0, 0))
    wimg.paste(water.img, (0, 0))
    h = sw.height + wimg.height + 8
    sheet = Image.new("RGBA", (sw.width, h), (40, 40, 56, 255))
    for yy in range(0, h, 8):
        for xx in range(0, sw.width, 8):
            if (xx // 8 + yy // 8) % 2:
                sheet.paste((52, 52, 70, 255), (xx, yy, xx + 8, yy + 8))
    sheet.alpha_composite(sw, (0, 0))
    sheet.alpha_composite(wimg, (0, sw.height + 8))
    sheet.resize((sheet.width * scale, sheet.height * scale), Image.NEAREST).save(path)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "preview").mkdir(exist_ok=True)
    praca, praca_w = build_praca()
    write_layout("praca", praca, praca_w, [
        {"name": "grass", "color": "#5C8C4A"}, {"name": "cobble", "color": "#6B6F8A"},
        {"name": "dirt", "color": "#8A5A3C"}, {"name": "water", "color": "#3C7A8A"}])
    esg, esg_w = build_esgoto()
    write_layout("esgoto", esg, esg_w, [
        {"name": "stone", "color": "#44556A"}, {"name": "sewage", "color": "#46684C"}])
    contact_sheet(praca, praca_w, OUT / "preview" / "praca_atlas.png")
    contact_sheet(esg, esg_w, OUT / "preview" / "esgoto_atlas.png")
    print("ok:", len(praca.tiles) + len(praca_w.tiles), "tiles praca,", len(esg.tiles) + len(esg_w.tiles), "esgoto")


if __name__ == "__main__":
    main()
