"""Mapas de amostra (30x17 tiles = 480x272, uma tela da Vespera) usados pelas previas Python e Godot.

Grava tools/tiles/verify/maps.json. Terrenos pintados por ordem de 'paint_order' (o ultimo vence nos cantos).
Overlay = [x, y, nome_do_tile] posto por cima (paredes, escadas, meio-fio, decalques).
"""
import json
from pathlib import Path

W, H = 30, 17


def grid(default, rects):
    g = [[default] * W for _ in range(H)]
    for ch, x0, y0, x1, y1 in rects:  # retangulo inclusivo
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                g[y][x] = ch
    return g


def blob(g, ch, cx, cy, rx, ry):
    for y in range(H):
        for x in range(W):
            if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0:
                g[y][x] = ch


def rect(g, ch, x0, y0, x1, y1, chamfer=()):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            g[y][x] = ch
    for (x, y) in chamfer:
        g[y][x] = "g"


def praca():
    g = grid("g", [])
    rect(g, "c", 8, 6, 19, 11)                                         # praca
    rect(g, "c", 12, 4, 15, 5)                                         # saliencia norte (>= 2 tiles)
    rect(g, "w", 21, 2, 27, 5)                                         # lago em L
    rect(g, "w", 24, 6, 27, 7)
    rect(g, "d", 0, 14, 29, 15)                                        # estrada
    rect(g, "d", 5, 9, 6, 13)                                          # ramal
    ov = []
    x0, x1 = 2, 9
    side = lambda x, nm: f"{nm}_l" if x == x0 else f"{nm}_r" if x == x1 else f"{nm}_m"
    for x in range(x0, x1 + 1):
        ov.append([x, 0, side(x, "roof_cap")])
        ov.append([x, 1, side(x, "roof_fill")])
        ov.append([x, 2, "face_upper_window" if x in (4, 7) else side(x, "face_upper")])
        ov.append([x, 3, "face_lower_door" if x == 6 else side(x, "face_lower")])
        if x not in (5, 6, 7):
            ov.append([x, 4, "wall_shadow"])
    ov += [[5, 4, "stairs_l"], [6, 4, "stairs_m"], [7, 4, "stairs_r"]]
    # canteiro com meio-fio na praca
    ov += [[12, 8, "curb_nw"], [13, 8, "curb_n"], [14, 8, "curb_n"], [15, 8, "curb_ne"],
           [12, 9, "curb_sw"], [13, 9, "curb_s"], [14, 9, "curb_s"], [15, 9, "curb_se"]]
    return {"rows": ["".join(r) for r in g], "terrain_of": {"g": 0, "c": 1, "d": 2, "w": 3},
            "paint_order": [0, 2, 1, 3], "overlay": ov}


def esgoto():
    g = grid("f", [("w", 1, 7, 28, 10)])
    ov = []
    for x in range(W):
        sd = "l" if x == 0 else "r" if x == W - 1 else "m"
        ov.append([x, 0, f"top_cap_{sd}"])
        ov.append([x, 1, f"top_fill_{sd}"])
        up = "face_upper_slime" if x in (5, 6, 22) else "face_upper_rift" if x == 17 else f"face_upper_{sd}"
        lo = "face_lower_grate" if x in (11, 12, 27) else f"face_lower_{sd}"
        ov += [[x, 2, up], [x, 3, lo]]
    for x in range(13, 16):          # ponte de grade sobre o canal
        for y in range(7, 11):
            ov.append([x, y, "grate_bars"])
    ov += [[3, 5, "debris_pottery_0"], [8, 11, "debris_pottery_1"], [20, 5, "debris_pottery_3"],
           [24, 12, "debris_pottery_2"], [6, 13, "debris_pottery_0"], [18, 14, "debris_pottery_1"],
           [26, 5, "grate_drain"], [2, 12, "grate_drain"]]
    return {"rows": ["".join(r) for r in g], "terrain_of": {"f": 0, "w": 1}, "paint_order": [0, 1], "overlay": ov}


def main():
    out = Path(__file__).resolve().parent / "verify" / "maps.json"
    out.parent.mkdir(exist_ok=True)
    out.write_text(json.dumps({"praca": praca(), "esgoto": esgoto()}, indent=1), encoding="utf-8")


if __name__ == "__main__":
    main()
