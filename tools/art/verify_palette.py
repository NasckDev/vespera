"""Confere que todo pixel visível das imagens geradas é uma cor da paleta mestre.

Uso: tools/art/.venv/Scripts/python -I tools/art/verify_palette.py [pasta ...]
Padrão: assets/v2. Sai com código 1 se achar cor fora da paleta ou alfa parcial.
"""
from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

from pixcore import PAL  # noqa: E402

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def check(path: str, allowed: set) -> list[str]:
    errs = []
    im = Image.open(path)
    frames = range(getattr(im, "n_frames", 1))
    for f in frames:
        im.seek(f)
        rgba = np.asarray(im.convert("RGBA"))
        a = rgba[..., 3]
        if ((a > 0) & (a < 255)).any():
            errs.append(f"{path} [quadro {f}]: alfa parcial")
        vis = rgba[a == 255][:, :3]
        cols = {tuple(int(v) for v in c) for c in np.unique(vis, axis=0)} if len(vis) else set()
        bad = cols - allowed
        if bad:
            errs.append(f"{path} [quadro {f}]: {len(bad)} cor(es) fora da paleta, ex.: {sorted(bad)[:3]}")
    return errs


def main(argv: list[str]) -> int:
    roots = argv or [os.path.join(REPO, "assets", "v2")]
    allowed = set(PAL.colors)
    files, errs = 0, []
    for root in roots:
        for dirpath, _, names in os.walk(root):
            for n in names:
                if n.lower().endswith((".png", ".gif")):
                    files += 1
                    errs += check(os.path.join(dirpath, n), allowed)
    for e in errs:
        print("ERRO", e)
    print(f"{files} imagens conferidas, {len(errs)} problema(s); paleta com {len(allowed)} cores")
    return 1 if errs or files == 0 else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
