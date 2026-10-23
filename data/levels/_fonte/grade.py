"""Ferramenta de autoria da grade de colisão (células de 48 px) sobre o chão de uma fase.

Uso (da raiz do repositório, Python 3 + Pillow):
  python data/levels/_fonte/grade.py guia    <id> <pasta>        -> <id>_guia.png (chão + grade numerada)
                                                                     e <id>_mascara.png (1 px por célula: preto = bloqueado)
  python data/levels/_fonte/grade.py mascara <id> <imagem.png>   -> lê a máscara e grava "grid" em data/levels/<id>.json
  python data/levels/_fonte/grade.py poligonos <id>              -> gera "grid" a partir de "authoring" no JSON da fase
  python data/levels/_fonte/grade.py previa  <id> <saida.png> [x,y;x,y...]
                                                                  -> chão + objetos (y-sort) + caçadores de teste,
                                                                     células bloqueadas em vermelho, pegadas em ciano

A máscara pode ser a miniatura (cols x rows, 1 px por célula; preto/escuro = bloqueado) ou uma cópia do guia em
tamanho cheio com as células bloqueadas pintadas de vermelho puro (célula com metade ou mais em vermelho = bloqueada).
"authoring" no JSON: {"walkable": [[[x,y],...], ...], "blocked": [[[x,y],...], ...]} em pixels do mundo; uma célula é
andável quando ao menos metade dela cai em "walkable" e fora de "blocked".
Fluxo para um chão novo: trocar ground.texture/scale e cols/rows no JSON, rodar "guia", pintar a máscara, rodar
"mascara", conferir com "previa" e rodar o bot de colisão (godot --headless --fixed-fps 60 -- --smoke-test --bot).
"""
import json
import re
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[3]
SAMPLES = 6
RED = (255, 40, 40, 80)


def level_path(level_id):
    return ROOT / "data" / "levels" / f"{level_id}.json"


def load(level_id):
    return json.loads(level_path(level_id).read_text(encoding="utf-8"))


def save(level_id, level):
    text = json.dumps(level, ensure_ascii=False, indent=2)
    # Listas curtas de números numa linha só ("at": [1, 2]) para o JSON continuar legível.
    text = re.sub(r"\[\s+(-?[\d.]+(?:,\s+-?[\d.]+)*)\s+\]", lambda m: "[" + re.sub(r",\s+", ", ", m.group(1)) + "]", text)
    level_path(level_id).write_text(text + "\n", encoding="utf-8", newline="\n")


def res(path):
    return ROOT / path.replace("res://", "")


def ground_image(level):
    img = Image.open(res(level["ground"]["texture"])).convert("RGBA")
    scale = level["ground"].get("scale", 1.0)
    if scale != 1.0:
        img = img.resize((round(img.width * scale), round(img.height * scale)), Image.LANCZOS)
    canvas = Image.new("RGBA", (level["cols"] * level["cell"], level["rows"] * level["cell"]), (0, 0, 0, 255))
    canvas.alpha_composite(img)
    return canvas


def grid_from_cells(level, blocked_at):
    rows = []
    for r in range(level["rows"]):
        rows.append("".join("#" if blocked_at(c, r) else "." for c in range(level["cols"])))
    return rows


def cmd_guia(level_id, folder):
    level = load(level_id)
    cell = level["cell"]
    img = ground_image(level)
    draw = ImageDraw.Draw(img)
    for c in range(level["cols"] + 1):
        draw.line([(c * cell, 0), (c * cell, img.height)], fill=(0, 220, 255, 255) if c % 5 == 0 else (0, 110, 140, 255))
    for r in range(level["rows"] + 1):
        draw.line([(0, r * cell), (img.width, r * cell)], fill=(0, 220, 255, 255) if r % 5 == 0 else (0, 110, 140, 255))
    for c in range(0, level["cols"], 5):
        for r in range(0, level["rows"], 5):
            draw.text((c * cell + 3, r * cell + 3), f"{c},{r}", fill=(255, 255, 0, 255))
    out = Path(folder)
    out.mkdir(parents=True, exist_ok=True)
    img.save(out / f"{level_id}_guia.png")
    mask = Image.new("L", (level["cols"], level["rows"]), 255)
    for r, line in enumerate(level.get("grid") or []):
        for c, ch in enumerate(line):
            if ch == "#":
                mask.putpixel((c, r), 0)
    mask.save(out / f"{level_id}_mascara.png")
    print(f"guia e máscara em {out}")


def cmd_mascara(level_id, mask_path):
    level = load(level_id)
    cols, rows, cell = level["cols"], level["rows"], level["cell"]
    mask = Image.open(mask_path).convert("RGB")
    if mask.size == (cols, rows):
        px = mask.load()
        level["grid"] = grid_from_cells(level, lambda c, r: sum(px[c, r]) < 384)
    elif mask.size == (cols * cell, rows * cell):
        px = mask.load()

        def painted(c, r):
            hits = 0
            for i in range(SAMPLES):
                for j in range(SAMPLES):
                    p = px[c * cell + (i + 0.5) * cell / SAMPLES, r * cell + (j + 0.5) * cell / SAMPLES]
                    hits += p[0] > 200 and p[1] < 70 and p[2] < 70
            return hits * 2 >= SAMPLES * SAMPLES

        level["grid"] = grid_from_cells(level, painted)
    else:
        sys.exit(f"máscara {mask.size} não bate com {cols}x{rows} nem {cols * cell}x{rows * cell}")
    save(level_id, level)
    print("\n".join(level["grid"]))


def cmd_poligonos(level_id):
    level = load(level_id)
    cols, rows, cell = level["cols"], level["rows"], level["cell"]
    authoring = level["authoring"]
    area = Image.new("L", (cols * cell, rows * cell), 0)
    draw = ImageDraw.Draw(area)
    for poly in authoring.get("walkable", []):
        draw.polygon([tuple(p) for p in poly], fill=255)
    for poly in authoring.get("blocked", []):
        draw.polygon([tuple(p) for p in poly], fill=0)
    px = area.load()

    def blocked(c, r):
        inside = sum(px[c * cell + (i + 0.5) * cell / SAMPLES, r * cell + (j + 0.5) * cell / SAMPLES] > 0
                     for i in range(SAMPLES) for j in range(SAMPLES))
        return inside * 2 < SAMPLES * SAMPLES

    level["grid"] = grid_from_cells(level, blocked)
    save(level_id, level)
    print("\n".join(level["grid"]))


def cmd_previa(level_id, out, hunters_arg=""):
    level = load(level_id)
    props = json.loads((ROOT / "data" / "levels" / "props.json").read_text(encoding="utf-8"))
    img = ground_image(level)
    # Contrato de escala (PLANO-V2 §2.1): humano = 0,75 x porta; a tira antiga tem 125 px opacos.
    actor = level["doorHeight"] * 0.75 / 125 if "doorHeight" in level else level.get("actorScale", 1.0)
    hunter = Image.open(ROOT / "assets/v2/personagens/lamina/lamina_e1_andar_baixo.png").convert("RGBA").crop((0, 0, 192, 192))
    hunter = hunter.resize((round(192 * actor), round(192 * actor)), Image.LANCZOS)
    items = [(p["at"][1], p) for p in level["props"]]
    for pair in filter(None, hunters_arg.split(";")):
        x, y = (int(v) for v in pair.split(","))
        items.append((y, {"hunter": (x, y)}))
    for _, p in sorted(items, key=lambda t: t[0]):
        if "hunter" in p:
            x, y = p["hunter"]
            img.alpha_composite(hunter, (round(x - 96 * actor), round(y - 176 * actor)))
            continue
        d = props[p["type"]]
        spr = Image.open(res(d["texture"])).convert("RGBA")
        s = d["scale"] * level.get("propScale", 1.0)
        spr = spr.resize((round(spr.width * s), round(spr.height * s)), Image.LANCZOS)
        if p.get("flip"):
            spr = spr.transpose(Image.FLIP_LEFT_RIGHT)
        x, y = p["at"]
        img.alpha_composite(spr, (round(x - d["anchor"][0] * s), round(y - d["anchor"][1] * s)))
    clean = img.copy()
    over = Image.new("RGBA", img.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(over)
    cell = level["cell"]
    for r, line in enumerate(level["grid"]):
        for c, ch in enumerate(line):
            if ch == "#":
                draw.rectangle([c * cell, r * cell, (c + 1) * cell - 1, (r + 1) * cell - 1], fill=RED)
    for p in level["props"]:
        d = props[p["type"]]
        fx, fy, fw, fh = (v * d["scale"] * level.get("propScale", 1.0) for v in d["footprint"])
        if fw > 0:
            x, y = p["at"]
            draw.rectangle([x + fx, y + fy, x + fx + fw, y + fy + fh], outline=(0, 255, 255, 255), width=3)
    for z in level.get("zones", []):
        x, y = z["at"]
        rad = z["radius"]
        draw.ellipse([x - rad, y - rad, x + rad, y + rad], outline=(255, 255, 0, 255), width=3)
    sx, sy = level["spawn"]
    draw.ellipse([sx - 10, sy - 10, sx + 10, sy + 10], fill=(0, 255, 0, 255))
    img.alpha_composite(over)
    img.save(out)
    clean.save(str(out).replace(".png", "_limpa.png"))
    print(f"prévia em {out}")


def main():
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    cmd, level_id, rest = sys.argv[1], sys.argv[2], sys.argv[3:]
    if cmd == "guia":
        cmd_guia(level_id, rest[0])
    elif cmd == "mascara":
        cmd_mascara(level_id, rest[0])
    elif cmd == "poligonos":
        cmd_poligonos(level_id)
    elif cmd == "previa":
        cmd_previa(level_id, rest[0], rest[1] if len(rest) > 1 else "")
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
