#!/usr/bin/env python3
"""
Suaviza a borda reta dos sprites de monstro que vieram recortados da v2 (arte encostada em cima ou nos lados da imagem).

Os PNG de `assets/sprites/enemies/` saíram da v2 já cortados: a foice do Ceifador, a asa do corvo… terminam numa reta no
limite da imagem, que aparece no jogo como um corte duro. Não há arte para recuperar; o script faz a arte sumir em rampa
(alpha × t, t de 0 na borda a 1 a `--ramp` px dela) só nos lados em que há pixels opacos encostados (mesmo critério do
cenário "combate qa: testa sprites de monstros sem recorte reto na borda da imagem": > 12 em cima, > 24 de cada lado).
A base fica intacta (sombra e pés podem encostar embaixo). Determinístico e idempotente.

Uso:
  python -I tools/sprites/feather_cut_edges.py [--ramp 14] [--check]
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
DIR = ROOT / "assets" / "sprites" / "enemies"
OPAQUE = 204  # 0,8 de alpha, como o cenário de QA


def opaque_counts(im: Image.Image) -> tuple[int, int, int]:
    a = im.getchannel("A").load()
    w, h = im.size
    top = sum(1 for x in range(w) if a[x, 0] > OPAQUE)
    left = sum(1 for y in range(h) if a[0, y] > OPAQUE)
    right = sum(1 for y in range(h) if a[w - 1, y] > OPAQUE)
    return top, left, right


def feather(im: Image.Image, sides: set[str], ramp: int) -> Image.Image:
    im = im.copy()
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            k = 1.0
            if "top" in sides and y < ramp:
                k = min(k, y / ramp)
            if "left" in sides and x < ramp:
                k = min(k, x / ramp)
            if "right" in sides and x >= w - ramp:
                k = min(k, (w - 1 - x) / ramp)
            if k < 1.0:
                r, g, b, al = px[x, y]
                px[x, y] = (r, g, b, int(al * max(0.0, k)))
    return im


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ramp", type=int, default=14)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    changed = []
    for path in sorted(DIR.glob("*.png")):
        im = Image.open(path).convert("RGBA")
        top, left, right = opaque_counts(im)
        sides = {s for s, n, lim in (("top", top, 12), ("left", left, 24), ("right", right, 24)) if n > lim}
        if not sides:
            continue
        changed.append(f"{path.name}: {', '.join(sorted(sides))}")
        if not args.check:
            feather(im, sides, args.ramp).save(path)
    print(f"{len(changed)} sprite(s) {'a suavizar' if args.check else 'suavizados'}")
    for c in changed:
        print("  " + c)
    return 1 if args.check and changed else 0


if __name__ == "__main__":
    sys.exit(main())
