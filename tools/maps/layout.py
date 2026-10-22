#!/usr/bin/env python3
"""
Ferramenta de layout dos mapas do Véspera: colisão, oclusão e objetos desenhados em cima da arte.

Cada mapa tem um `assets/maps/<pasta>/layout_<id>.json` (fonte, editado à mão/por agentes) que gera
`assets/maps/<pasta>/walk_<id>.png` (máscara de colisão em células de 4 px, branco = andável).

Formato do layout (coordenadas em px do mapa; formas: {"poly": [[x,y],...]}, {"rect": [x,y,w,h]}, {"circle": [cx,cy,r]}):
{
  "cell": 4,
  "base": "mask" | "shapes" | "none",   # ponto de partida: máscara da v2, formas walk/block do maps.json, ou nada
  "walk":  [ {forma, "note": "escada oeste"} ],      # áreas que PASSAM a ser andáveis (aplicado depois da base)
  "block": [ {forma, "note": "casa"} ],              # áreas que PASSAM a ser bloqueadas (aplicado por último)
  "occluders": [ {"poly": [...], "baseY": 512, "sway": false, "note": "lampião"} ],   # recortes da arte desenhados NA
               #   FRENTE do jogador quando o pé dele está acima de baseY (atrás do objeto); ficam translúcidos se cobrirem o
               #   jogador. "sway": true = balança com o vento (árvores, bandeiras, toldos).
  "water":  [ {forma, "note": "canal"} ],          # superfícies de água (brilho animado; balsas navegam aqui)
  "ambient": [ {"kind": "water|fire|forge|crowd|wind|bells|birds", "x": 0, "y": 0, "r": 160} ],  # sons ambiente posicionais
  "props":  [ {"kind": "barrel|crate|crate2|sack|urn", "x": 0, "y": 0} ],     # quebráveis (atlas pr_*)
  "chests": [ {"x": 0, "y": 0, "tier": 0} ],                                    # baús (atlas pr_chest0..4)
  "decor":  [ {"kind": "lamp|flower|boat|fishing|sign|bench|fountain|stall|inspect", "x": 0, "y": 0,
               "r": 24, "label": "Acender lampião", "text": "...", "to": [x,y]} ]   # interações de decoração
               #   "boat" com "to": balsa que leva ao ponto "to" (o check conta como alcançável o cais de destino)
  "objects": [ {"id": "lamp_post_common", "x": 0, "y": 0, "flip": false} ]   # objetos do catálogo (src/Data/objects.json),
               #   pé do objeto em (x,y). Lampião, braseiro, flores, alavanca, porta e quadro de avisos ganham a arte de
               #   estados do designer (DECISIONS C2-D5). O check exige o pé a até 28 px de chão alcançável.
}

Uso:
  python tools/maps/layout.py init   <id|all>          cria o layout se não existir
  python tools/maps/layout.py build  <id|all>          gera walk_<id>.png
  python tools/maps/layout.py check  <id|all>          alcance a partir do spawn (exits, chegadas, interagíveis, objetos)
  python tools/maps/layout.py render <id> [--crop x,y,w,h] [--scale 2] [--out arquivo.png] [--grid 32]
  python tools/maps/layout.py tiles  <id> --out pasta   recortes 2x do mapa inteiro com grade e coordenadas
"""
from __future__ import annotations

import argparse
import json
import math
import sys
from collections import deque
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
MAPS_JSON = ROOT / "src" / "Data" / "maps.json"
CELL = 4
PLAYER_RADIUS = 3.5  # Player.tscn


def res_path(res: str) -> Path:
    return ROOT / res.removeprefix("res://")


def load_maps() -> dict:
    return json.loads(MAPS_JSON.read_text(encoding="utf-8"))


def map_dir(m: dict) -> Path:
    return res_path(m["image"]).parent


def layout_path(m: dict) -> Path:
    return map_dir(m) / f"layout_{m['id']}.json"


def walk_path(m: dict) -> Path:
    return map_dir(m) / f"walk_{m['id']}.png"


def ids_of(arg: str, maps: dict) -> list[str]:
    return list(maps.keys()) if arg == "all" else [arg]


# --- formas ---------------------------------------------------------------------------------------

def draw_shape(draw: ImageDraw.ImageDraw, shape: dict, fill: int, scale: float) -> None:
    if "poly" in shape:
        pts = [(x * scale, y * scale) for x, y in shape["poly"]]
        draw.polygon(pts, fill=fill)
    elif "rect" in shape:
        x, y, w, h = shape["rect"]
        draw.rectangle([x * scale, y * scale, (x + w) * scale - 1, (y + h) * scale - 1], fill=fill)
    elif "circle" in shape:
        cx, cy, r = shape["circle"]
        draw.ellipse([(cx - r) * scale, (cy - r) * scale, (cx + r) * scale, (cy + r) * scale], fill=fill)


def v2_shape(entry) -> dict:
    """Formas do maps.json da v2: [x,y,w,h] ou {"c": [cx,cy,r]}."""
    if isinstance(entry, dict) and "c" in entry:
        return {"circle": entry["c"]}
    return {"rect": entry}


# --- grade ----------------------------------------------------------------------------------------

def build_grid(m: dict, layout: dict) -> Image.Image:
    w, h = m["imageSize"]
    cell = layout.get("cell", CELL)
    gw, gh = math.ceil(w / cell), math.ceil(h / cell)
    grid = Image.new("L", (gw, gh), 0)
    base = layout.get("base", "mask" if m.get("mask") else "shapes")
    if base == "mask" and m.get("mask"):
        mask = Image.open(res_path(m["mask"])).convert("L")
        mcell = m.get("maskCell", 8)
        px = grid.load()
        mp = mask.load()
        for y in range(gh):
            for x in range(gw):
                mx, my = int((x + 0.5) * cell / mcell), int((y + 0.5) * cell / mcell)
                if mx < mask.width and my < mask.height and mp[mx, my] > 127:
                    px[x, y] = 255
    elif base == "shapes":
        d = ImageDraw.Draw(grid)
        for e in m.get("walk", []):
            draw_shape(d, v2_shape(e), 255, 1 / cell)
        for e in m.get("block", []):
            draw_shape(d, v2_shape(e), 0, 1 / cell)
    d = ImageDraw.Draw(grid)
    for s in layout.get("walk", []):
        draw_shape(d, s, 255, 1 / cell)
    for s in layout.get("block", []):
        draw_shape(d, s, 0, 1 / cell)
    return grid


def reachable(grid: Image.Image, start: tuple[float, float], cell: int) -> set[tuple[int, int]]:
    """BFS em células onde o círculo do pé cabe (célula e vizinhas a até 1 célula precisam ser andáveis)."""
    gw, gh = grid.size
    px = grid.load()

    def ok(x: int, y: int) -> bool:
        if not (0 <= x < gw and 0 <= y < gh) or px[x, y] < 128:
            return False
        return True

    sx, sy = int(start[0] / cell), int(start[1] / cell)
    if not ok(sx, sy):
        best = None
        for r in range(1, 12):
            for dy in range(-r, r + 1):
                for dx in range(-r, r + 1):
                    if ok(sx + dx, sy + dy):
                        best = (sx + dx, sy + dy)
                        break
                if best:
                    break
            if best:
                break
        if not best:
            return set()
        sx, sy = best
    seen = {(sx, sy)}
    q = deque([(sx, sy)])
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if n not in seen and ok(*n):
                seen.add(n)
                q.append(n)
    return seen


def reachable_by_boat(grid: Image.Image, m: dict, layout: dict, cell: int) -> set[tuple[int, int]]:
    """Alcance a pé a partir do spawn, somado ao que as balsas (decor 'boat' com 'to') levam de um cais alcançável."""
    seen = reachable(grid, m["spawn"], cell)
    boats = [d for d in layout.get("decor", []) if d.get("kind") == "boat" and d.get("to")]
    changed = True
    while changed:
        changed = False
        for b in boats:
            tx, ty = b["to"]
            if (int(tx / cell), int(ty / cell)) not in seen and near_reachable(seen, b["x"], b["y"], cell):
                more = reachable(grid, (tx, ty), cell)
                if more - seen:
                    seen |= more
                    changed = True
    return seen


def points_of_interest(m: dict, layout: dict) -> list[tuple[str, float, float]]:
    pts = [("spawn", *m["spawn"])]
    for e in m.get("exits", []):
        x, y, w, h = e["r"]
        pts.append((f"saída→{e['to']}", x + w / 2, y + h / 2))
    for k, v in (m.get("arrivals") or {}).items():
        if isinstance(v, list):
            pts.append((f"chegada {k}", *v))
    for i, p in enumerate(m.get("portalSpots") or []):
        pts.append((f"portal{i}", *p))
    for k in ("door", "back"):
        if m.get(k):
            x, y, w, h = m[k]
            pts.append((k, x + w / 2, y + h / 2))
    for it in m.get("interactables", []):
        pts.append((f"interagível {it['id']}", it["x"], it["y"]))
    for p in layout.get("props", []):
        pts.append((f"prop {p['kind']}", p["x"], p["y"]))
    for c in layout.get("chests", []):
        pts.append(("baú", c["x"], c["y"]))
    for d in layout.get("decor", []):
        pts.append((f"decor {d['kind']}", d["x"], d["y"]))
    for o in layout.get("objects", []):
        pts.append((f"objeto {o['id']}", o["x"], o["y"]))
    return pts


def near_reachable(seen: set, x: float, y: float, cell: int, radius_px: float = 28) -> bool:
    cx, cy = int(x / cell), int(y / cell)
    r = int(radius_px / cell)
    return any((cx + dx, cy + dy) in seen for dy in range(-r, r + 1) for dx in range(-r, r + 1))


# --- comandos -------------------------------------------------------------------------------------

def cmd_init(args, maps):
    for mid in ids_of(args.id, maps):
        m = maps[mid]
        p = layout_path(m)
        if p.exists():
            print(f"{mid}: já existe {p.relative_to(ROOT)}")
            continue
        layout = {"cell": CELL, "base": "mask" if m.get("mask") else "shapes",
                  "walk": [], "block": [], "occluders": [], "water": [], "props": [], "chests": [], "decor": [], "ambient": []}
        p.write_text(json.dumps(layout, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"{mid}: criado {p.relative_to(ROOT)}")


def load_layout(m: dict) -> dict:
    p = layout_path(m)
    if not p.exists():
        sys.exit(f"{m['id']}: rode 'init' antes ({p.relative_to(ROOT)} não existe)")
    return json.loads(p.read_text(encoding="utf-8"))


def cmd_build(args, maps):
    for mid in ids_of(args.id, maps):
        m = maps[mid]
        layout = load_layout(m)
        grid = build_grid(m, layout)
        grid.save(walk_path(m))
        total = grid.size[0] * grid.size[1]
        walk = sum(grid.histogram()[128:])
        print(f"{mid}: {walk_path(m).relative_to(ROOT)} {grid.size[0]}x{grid.size[1]} células, {100 * walk / total:.1f}% andável")


def cmd_check(args, maps):
    bad = 0
    for mid in ids_of(args.id, maps):
        m = maps[mid]
        layout = load_layout(m)
        cell = layout.get("cell", CELL)
        grid = build_grid(m, layout)
        seen = reachable_by_boat(grid, m, layout, cell)
        walk = sum(grid.histogram()[128:])
        islands = walk - len(seen)
        problems = [f"  ✗ {name} em ({x:.0f},{y:.0f}) inalcançável"
                    for name, x, y in points_of_interest(m, layout) if not near_reachable(seen, x, y, cell)]
        status = "OK" if not problems else "FALHA"
        print(f"{mid}: {status} — alcançável {len(seen)}/{walk} células andáveis ({100 * len(seen) / max(1, walk):.0f}%), "
              f"{islands} células em ilhas isoladas")
        for line in problems:
            print(line)
        bad += bool(problems)
    sys.exit(1 if bad else 0)


def font(size: int):
    try:
        return ImageFont.truetype(str(ROOT / "assets" / "fonts" / "VT323-Regular.ttf"), size)
    except OSError:
        return ImageFont.load_default()


def render(m: dict, layout: dict, crop, scale: float, grid_step: int) -> Image.Image:
    img = Image.open(res_path(m["image"])).convert("RGBA")
    cell = layout.get("cell", CELL)
    grid = build_grid(m, layout)
    seen = reachable_by_boat(grid, m, layout, cell)
    over = Image.new("RGBA", img.size, (0, 0, 0, 0))
    po = over.load()
    gp = grid.load()
    for gy in range(grid.size[1]):
        for gx in range(grid.size[0]):
            if gp[gx, gy] < 128:
                color = (255, 0, 60, 105)
            elif (gx, gy) not in seen:
                color = (255, 150, 0, 120)
            else:
                continue
            for yy in range(gy * cell, min(img.size[1], (gy + 1) * cell)):
                for xx in range(gx * cell, min(img.size[0], (gx + 1) * cell)):
                    po[xx, yy] = color
    img = Image.alpha_composite(img, over)
    d = ImageDraw.Draw(img)
    f = font(14)
    for wa in layout.get("water", []):
        tmp = Image.new("L", img.size, 0)
        draw_shape(ImageDraw.Draw(tmp), wa, 255, 1)
        img = Image.composite(Image.new("RGBA", img.size, (0, 140, 255, 255)), img, tmp.point(lambda v: 70 if v else 0))
        d = ImageDraw.Draw(img)
    for am in layout.get("ambient", []):
        x, y, r = am["x"], am["y"], am.get("r", 160)
        d.ellipse([x - r, y - r, x + r, y + r], outline=(180, 120, 255, 200), width=1)
        d.text((x + 4, y + 4), f"♪{am['kind']}", fill=(200, 150, 255, 255), font=f)
    for oc in layout.get("occluders", []):
        if "poly" in oc:
            d.line([tuple(p) for p in oc["poly"]] + [tuple(oc["poly"][0])], fill=(0, 230, 255, 255), width=1)
            xs = [p[0] for p in oc["poly"]]
            d.line([(min(xs), oc["baseY"]), (max(xs), oc["baseY"])], fill=(0, 120, 255, 255), width=1)
    for e in m.get("exits", []):
        x, y, w, h = e["r"]
        d.rectangle([x, y, x + w, y + h], outline=(0, 255, 90, 255), width=2)
        d.text((x, y - 12), f"→{e['to']}", fill=(0, 255, 90, 255), font=f)
    for name, x, y in points_of_interest(m, layout):
        col = (255, 230, 0, 255) if name.startswith(("prop", "baú", "decor")) else (255, 255, 255, 255)
        if name.startswith("objeto"):
            col = (120, 255, 160, 255)
        d.ellipse([x - 3, y - 3, x + 3, y + 3], outline=col, width=2)
        if not name.startswith(("prop", "baú")):
            d.text((x + 5, y - 6), name.replace("interagível ", ""), fill=col, font=f)
    if crop:
        x, y, w, h = crop
        img = img.crop((x, y, x + w, y + h))
        ox, oy = x, y
    else:
        ox, oy = 0, 0
    if scale != 1:
        img = img.resize((int(img.width * scale), int(img.height * scale)), Image.NEAREST)
    d = ImageDraw.Draw(img)
    f2 = font(16)
    if grid_step:
        start_x = (grid_step - ox % grid_step) % grid_step
        start_y = (grid_step - oy % grid_step) % grid_step
        for gx in range(start_x, int(img.width / scale) + 1, grid_step):
            X = gx * scale
            major = (gx + ox) % (grid_step * 2) == 0
            d.line([(X, 0), (X, img.height)], fill=(255, 255, 255, 70 if major else 35), width=1)
            if major:
                d.text((X + 2, 2), str(gx + ox), fill=(255, 255, 255, 255), font=f2, stroke_width=2, stroke_fill=(0, 0, 0, 255))
        for gy in range(start_y, int(img.height / scale) + 1, grid_step):
            Y = gy * scale
            major = (gy + oy) % (grid_step * 2) == 0
            d.line([(0, Y), (img.width, Y)], fill=(255, 255, 255, 70 if major else 35), width=1)
            if major:
                d.text((2, Y + 2), str(gy + oy), fill=(255, 255, 255, 255), font=f2, stroke_width=2, stroke_fill=(0, 0, 0, 255))
    return img


def cmd_render(args, maps):
    m = maps[args.id]
    layout = load_layout(m)
    crop = [int(v) for v in args.crop.split(",")] if args.crop else None
    img = render(m, layout, crop, args.scale, args.grid)
    if not crop and img.width > 1600:
        img = img.resize((1600, int(img.height * 1600 / img.width)), Image.LANCZOS)
    out = Path(args.out or f"{args.id}_layout.png")
    img.convert("RGB").save(out)
    print(out)


def cmd_tiles(args, maps):
    m = maps[args.id]
    layout = load_layout(m)
    w, h = m["imageSize"]
    tw, th, ov = 520, 380, 40
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    for y in range(0, h, th - ov):
        for x in range(0, w, tw - ov):
            cw, ch = min(tw, w - x), min(th, h - y)
            img = render(m, layout, (x, y, cw, ch), args.scale, args.grid)
            p = out / f"{args.id}_{x:04d}_{y:04d}.png"
            img.convert("RGB").save(p)
            print(p)


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")  # acentos no console do Windows
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name in ("init", "build", "check"):
        s = sub.add_parser(name)
        s.add_argument("id")
    s = sub.add_parser("render")
    s.add_argument("id")
    s.add_argument("--crop")
    s.add_argument("--scale", type=float, default=2)
    s.add_argument("--grid", type=int, default=32)
    s.add_argument("--out")
    s = sub.add_parser("tiles")
    s.add_argument("id")
    s.add_argument("--out", required=True)
    s.add_argument("--scale", type=float, default=2)
    s.add_argument("--grid", type=int, default=32)
    args = ap.parse_args()
    maps = load_maps()
    {"init": cmd_init, "build": cmd_build, "check": cmd_check, "render": cmd_render, "tiles": cmd_tiles}[args.cmd](args, maps)


if __name__ == "__main__":
    main()
