#!/usr/bin/env python3
"""Fatia uma tira de animação (N quadros igualmente espaçados) e alinha os pés em células 192x192.

Uso:
  python tools/art/slice_strip.py tira.png --frames 6 --anim andar --dir baixo --out saida/ [--class lamina --stage e1]

Para cada quadro: recorta a fatia, apara o transparente, acha a linha opaca mais baixa (pé) e o centroide horizontal
(das linhas opacas do corpo) e cola numa célula CELL x CELL com o pé em (FOOT_X, FOOT_Y) = (96, 176).
Saída: <classe>_<estagio>_<anim>_<dir>.png (a tira de N células, N*192 x 192), ex.: lamina_e1_andar_baixo.png.
A mesma escala é usada em todos os quadros (nada é redimensionado), então o movimento relativo se preserva;
se algum quadro não couber na célula, o script avisa e sai com erro.
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

CELL = 192
FOOT_X = 96
FOOT_Y = 176
ALPHA_MIN = 24
CENTROID_ROWS_FRACTION = 0.35  # centroide medido no terço de baixo do corpo (pernas), estável durante o ataque


def split_strip(strip: Image.Image, frames: int) -> list[Image.Image]:
    width = strip.width / frames
    return [strip.crop((round(i * width), 0, round((i + 1) * width), strip.height)) for i in range(frames)]


def frame_metrics(frame: Image.Image):
    alpha = np.asarray(frame.getchannel("A"))
    mask = alpha >= ALPHA_MIN
    if not mask.any():
        return None
    rows = np.where(mask.any(axis=1))[0]
    top, bottom = int(rows[0]), int(rows[-1])
    body_top = max(top, bottom - max(1, int((bottom - top + 1) * CENTROID_ROWS_FRACTION)))
    cols = np.where(mask[body_top : bottom + 1].any(axis=0))[0]
    weights = mask[body_top : bottom + 1].sum(axis=0)
    centroid = float((cols * weights[cols]).sum() / weights[cols].sum())
    return bottom, centroid


def place_frame(frame: Image.Image) -> Image.Image:
    cell = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    metrics = frame_metrics(frame)
    if metrics is None:
        return cell
    bottom, centroid = metrics
    offset_x = round(FOOT_X - centroid)
    offset_y = FOOT_Y - bottom
    left, top = offset_x, offset_y
    right, lower = left + frame.width, top + frame.height
    bbox = frame.getchannel("A").point(lambda v: 255 if v >= ALPHA_MIN else 0).getbbox()
    if left + bbox[0] < 0 or top + bbox[1] < 0 or left + bbox[2] > CELL or top + bbox[3] > CELL:
        raise ValueError(f"quadro não cabe na célula {CELL}x{CELL} (bbox {bbox}, deslocamento {offset_x},{offset_y})")
    cell.alpha_composite(frame, (max(left, 0), max(top, 0)), (max(-left, 0), max(-top, 0)))
    return cell


def slice_strip(strip: Image.Image, frames: int) -> Image.Image:
    cells = [place_frame(f) for f in split_strip(strip.convert("RGBA"), frames)]
    out = Image.new("RGBA", (CELL * frames, CELL), (0, 0, 0, 0))
    for i, cell in enumerate(cells):
        out.paste(cell, (i * CELL, 0))
    return out


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("tira", type=Path)
    parser.add_argument("--frames", type=int, required=True)
    parser.add_argument("--anim", required=True)
    parser.add_argument("--dir", required=True, dest="direction")
    parser.add_argument("--class", dest="char_class", default="lamina")
    parser.add_argument("--stage", default="e1")
    parser.add_argument("--out", type=Path, default=Path("."))
    args = parser.parse_args()

    try:
        sheet = slice_strip(Image.open(args.tira), args.frames)
    except ValueError as error:
        print(f"ERRO: {error}", file=sys.stderr)
        return 1
    args.out.mkdir(parents=True, exist_ok=True)
    target = args.out / f"{args.char_class}_{args.stage}_{args.anim}_{args.direction}.png"
    sheet.save(target)
    print(f"{target} ({sheet.width}x{sheet.height}, {args.frames} quadros)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
