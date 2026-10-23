"""Mede a altura opaca em pé de cada folha de assets/v2/sprites e grava "standing_height" no JSON (PLANO-V2 §2.1).

Altura = mediana, nos quadros da animação parada de frente (parado_baixo; nos monstros, parado), da distância do
pixel opaco mais alto (alfa >= 128) até a linha do pé (anchor y). É contra ela que o ScaleRegistry calcula a escala:
escala = altura-alvo no mapa / standing_height.

Uso (da raiz do repositório, Python 3 + Pillow):  python tools/art/medir_altura_em_pe.py [--verificar]
--verificar só compara com o que está gravado e sai com erro se mudou (para o CI).
"""
import json
import statistics
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SPRITES = ROOT / "assets" / "v2" / "sprites"
ALPHA = 128


def idle_name(meta):
    names = meta["animations"].keys()
    for wanted in ("parado_baixo", "parado"):
        if wanted in names:
            return wanted
    raise SystemExit(f"{meta.get('character')}: sem animação parada")


def frame_height(atlas, cell_w, cell_h, row, col, foot_y):
    cell = atlas.crop((col * cell_w, row * cell_h, (col + 1) * cell_w, (row + 1) * cell_h))
    alpha = cell.getchannel("A").point(lambda a: 255 if a >= ALPHA else 0)
    box = alpha.getbbox()
    return 0 if box is None else foot_y - box[1]


def measure(json_path):
    meta = json.loads(json_path.read_text(encoding="utf-8"))
    atlas = Image.open(json_path.parent / meta["atlas"]).convert("RGBA")
    cell_w, cell_h = meta["cell"]
    anim = meta["animations"][idle_name(meta)]
    heights = [frame_height(atlas, cell_w, cell_h, anim["row"], c, meta["anchor"][1]) for c in range(anim["frames"])]
    return meta, int(round(statistics.median(heights)))


def main():
    check = "--verificar" in sys.argv
    changed = []
    for path in sorted(SPRITES.rglob("*.json")):
        meta, height = measure(path)
        old = meta.get("standing_height")
        print(f"{path.relative_to(ROOT)}: standing_height {height} (antes {old})")
        if old != height:
            changed.append(path)
            if not check:
                meta["standing_height"] = height
                path.write_text(json.dumps(meta, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    if check and changed:
        raise SystemExit(f"standing_height desatualizado em {len(changed)} arquivo(s)")


if __name__ == "__main__":
    main()
