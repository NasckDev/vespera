#!/usr/bin/env python3
"""Importa os NPCs aprovados (Odete, Ravi) e os retratos de diálogo para assets/v2/sprites/npcs/.

Uso (da raiz do repositório):  python tools/art/import_npcs.py

Entrada: assets/v2/gerado/npcs/ (fundo verde #00FF00).
  * npc_<id>_{parado,andar}_{baixo,cima,lado}.png  (células 256, parado = 4 quadros, andar = 6)
  * retrato_<id>_{1,2}.png (1024x1024; 1 = neutro, 2 = expressão)
Saída: assets/v2/sprites/npcs/
  * npc_<id>_{parado,andar}_{baixo,cima,lado}.png  RGBA, células 192x192, pés em (96,176), como o caçador
    (HunterSprites.CellSize/FootY), de modo que o MESMO actorScale da fase vale para NPC e caçador.
  * retrato_<id>_{1,2}.png  RGBA 512x512 (chroma-key).

Escala: a altura do corpo na caminhada de frente vira NPC_HEIGHT[id] px na célula 192 (o caçador tem ~125 px: Odete é
um pouco mais baixa, Ravi tem a mesma altura). Uma escala por NPC, igual para todas as tiras dele (não normaliza por
quadro, senão a animação pulsa). Só o corpo fica: sombra de chão e restos do quadro vizinho são apagados.
"""
from __future__ import annotations

import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, str(Path(__file__).resolve().parent))
import import_strips as strips  # noqa: E402
from chroma_key import chroma_key  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "assets" / "v2" / "gerado" / "npcs"
OUT = ROOT / "assets" / "v2" / "sprites" / "npcs"

CELL = 192
FEET = (96, 176)
NPC_HEIGHT = {"odete": 118, "ravi": 124}
FRAMES = {"parado": 4, "andar": 6}
DIRECTIONS = ("baixo", "cima", "lado")
PORTRAITS = ("odete", "ravi", "nilza", "lis")
PORTRAIT_SIZE = 512
JOIN = 3


def keep_body_only(image: Image.Image) -> Image.Image:
    """Apaga tudo que não encosta no maior componente (sombra de chão, pedaços do quadro vizinho)."""
    data = np.array(image)
    mask = data[..., 3] >= strips.ALPHA_MIN
    joined = ndimage.binary_dilation(mask, structure=np.ones((2 * JOIN + 1,) * 2))
    labels, total = ndimage.label(joined)
    if total == 0:
        return image
    sizes = np.bincount(labels[mask], minlength=total + 1)
    sizes[0] = 0
    main = int(sizes.argmax())
    data[labels != main] = 0
    return Image.fromarray(data, "RGBA")


def load(npc: str, anim: str, direction: str) -> strips.Strip:
    path = SRC / f"npc_{npc}_{anim}_{direction}.png"
    strip = strips.load_strip(path, FRAMES[anim], anim, direction)
    for frame in strip.frames:
        frame.image = keep_body_only(frame.image)
        data = np.array(frame.image)
        rows = np.flatnonzero((data[..., 3] >= strips.ALPHA_MIN).any(axis=1))
        frame.top, frame.bottom = int(rows[0]), int(rows[-1])
    return strip


def build_npc(npc: str) -> None:
    loaded = {(a, d): load(npc, a, d) for a in FRAMES for d in DIRECTIONS}
    reference = float(np.median([f.height for f in loaded[("andar", "baixo")].frames]))
    scale = NPC_HEIGHT[npc] / reference
    print(f"{npc}: altura de referência {reference:.0f}px na fonte -> {NPC_HEIGHT[npc]}px (escala {scale:.3f})")
    for (anim, direction), strip in loaded.items():
        strip.scale = scale
        strip.fixed_anchor = (
            float(np.median([f.feet_x for f in strip.frames])),
            float(np.median([f.bottom for f in strip.frames])),
        )
        cells, clipped = strips.render_strip(strip, CELL, FEET)
        sheet = Image.new("RGBA", (CELL * len(cells), CELL), (0, 0, 0, 0))
        for i, cell in enumerate(cells):
            sheet.paste(cell, (i * CELL, 0))
        name = f"npc_{npc}_{anim}_{direction}.png"
        sheet.save(OUT / name, optimize=True)
        print(f"  {name}: {len(cells)} quadros" + (f", {clipped} cortado(s) pela célula" if clipped else ""))


def build_portraits() -> None:
    for npc in PORTRAITS:
        for n in (1, 2):
            image = Image.open(SRC / f"retrato_{npc}_{n}.png").convert("RGB")
            keyed = chroma_key(image).resize((PORTRAIT_SIZE, PORTRAIT_SIZE), Image.LANCZOS)
            keyed.save(OUT / f"retrato_{npc}_{n}.png", optimize=True)
            print(f"  retrato_{npc}_{n}.png")


def main() -> int:
    OUT.mkdir(parents=True, exist_ok=True)
    for npc in NPC_HEIGHT:
        build_npc(npc)
    build_portraits()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
