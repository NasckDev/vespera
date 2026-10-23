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

Benício (folha única, só 'parado'): ver build_benicio. npcs.json guarda a altura opaca em pé de cada NPC.
Escala: a altura do corpo na caminhada de frente vira NPC_HEIGHT[id] px na célula 192 (o caçador tem ~125 px: Odete é
um pouco mais baixa, Ravi tem a mesma altura). Uma escala por NPC, igual para todas as tiras dele (não normaliza por
quadro, senão a animação pulsa). Só o corpo fica: sombra de chão e restos do quadro vizinho são apagados.
"""
from __future__ import annotations

import json
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
NPC_HEIGHT = {"odete": 118, "ravi": 124, "lis": 118, "nilza": 118, "celeste": 118}
# NPCs cujas tiras vieram em tamanhos diferentes entre si (Celeste: parado_lado maior): escala por tira, não por animação.
PER_STRIP_SCALE = {"celeste"}
FRAMES = {"parado": 4, "andar": 6}
DIRECTIONS = ("baixo", "cima", "lado")
PORTRAITS = ("odete", "ravi", "nilza", "lis", "celeste")
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
    # uma escala por animação (parado e andar foram desenhados em tamanhos diferentes), igual nas 3 direções
    scales = {}
    for anim in FRAMES:
        reference = float(np.median([f.height for f in loaded[(anim, "baixo")].frames]))
        scales[anim] = NPC_HEIGHT[npc] / reference
        print(f"{npc} {anim}: altura de referência {reference:.0f}px na fonte -> {NPC_HEIGHT[npc]}px (escala {scales[anim]:.3f})")
    for (anim, direction), strip in loaded.items():
        strip.scale = scales[anim]
        if npc in PER_STRIP_SCALE:
            strip.scale = NPC_HEIGHT[npc] / float(np.median([f.height for f in strip.frames]))
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


BENICIO_SRC = SRC / "benicio"
BENICIO_HEIGHT = 119
BENICIO_BAND = 260  # a linha 1 da folha (parado frente 1-4, lado 5-6, costas 7-8, caminhada lado 9-15) fica acima disto
BENICIO_FACES = {"baixo": (0, 4), "lado": (4, 6), "cima": (6, 8)}


def column_runs(mask: np.ndarray) -> list[tuple[int, int]]:
    cols = mask.any(axis=0)
    runs, start = [], None
    for x, on in enumerate(cols):
        if on and start is None:
            start = x
        elif not on and start is not None:
            runs.append((start, x))
            start = None
    if start is not None:
        runs.append((start, len(cols)))
    return runs


def build_benicio() -> None:
    """Benício só tem parado (frente 4q, lado 2q, costas 2q) na folha: fica estacionário (sem tira de andar).
    A linha 1 é fatiada por colunas vazias (quadros separados por verde); os 8 primeiros quadros são limpos."""
    sheet = chroma_key(Image.open(BENICIO_SRC / "benicio_folha_animacoes_verde.png").convert("RGB"))
    band = np.array(sheet)[:BENICIO_BAND]
    runs = column_runs(band[..., 3] >= strips.ALPHA_MIN)[:8]
    frames = []
    for x0, x1 in runs:
        piece = Image.fromarray(band[:, x0:x1].copy(), "RGBA")
        rows = np.flatnonzero((np.array(piece)[..., 3] >= strips.ALPHA_MIN).any(axis=1))
        frames.append((piece, int(rows[0]), int(rows[-1])))
    front_heights = [bottom - top + 1 for _, top, bottom in frames[0:4]]
    scale = BENICIO_HEIGHT / float(np.median(front_heights))
    print(f"benicio: altura de referência {np.median(front_heights):.0f}px na fonte -> {BENICIO_HEIGHT}px (escala {scale:.3f})")
    for direction, (first, last) in BENICIO_FACES.items():
        cells = []
        for piece, _top, bottom in frames[first:last]:
            size = (max(1, round(piece.width * scale)), max(1, round(piece.height * scale)))
            scaled = strips.resize_rgba(piece, size, False)
            cell = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
            cell.alpha_composite(scaled, (FEET[0] - size[0] // 2, FEET[1] - round(bottom * scale)))
            cells.append(cell)
        sheet_out = Image.new("RGBA", (CELL * len(cells), CELL), (0, 0, 0, 0))
        for i, cell in enumerate(cells):
            sheet_out.paste(cell, (i * CELL, 0))
        sheet_out.save(OUT / f"npc_benicio_parado_{direction}.png", optimize=True)
        print(f"  npc_benicio_parado_{direction}.png: {len(cells)} quadros")


def write_registry() -> None:
    """npcs.json: altura opaca em pé (px da célula 192, mediana dos quadros 'parado' de frente) de cada NPC.
    O jogo calcula a escala do sprite = altura-alvo (ScaleRegistry) ÷ este valor."""
    registry = {}
    for npc in (*NPC_HEIGHT, "benicio"):
        data = np.array(Image.open(OUT / f"npc_{npc}_parado_baixo.png"))
        heights = []
        for i in range(data.shape[1] // CELL):
            rows = np.flatnonzero((data[:, i * CELL:(i + 1) * CELL, 3] >= strips.ALPHA_MIN).any(axis=1))
            heights.append(int(rows[-1] - rows[0] + 1))
        registry[npc] = {"standing_height": int(np.median(heights))}
    (OUT / "npcs.json").write_text(json.dumps(registry, indent=2) + chr(10), encoding="utf-8")
    print("npcs.json:", registry)


def build_portraits(only: set[str] | None = None) -> None:
    for npc in PORTRAITS:
        if only and npc not in only:
            continue
        for n in (1, 2):
            image = Image.open(SRC / f"retrato_{npc}_{n}.png").convert("RGB")
            keyed = chroma_key(image).resize((PORTRAIT_SIZE, PORTRAIT_SIZE), Image.LANCZOS)
            keyed.save(OUT / f"retrato_{npc}_{n}.png", optimize=True)
            print(f"  retrato_{npc}_{n}.png")
    if only:
        return
    for n in (1, 2):
        image = Image.open(BENICIO_SRC / f"retrato_benicio_{n}.png").convert("RGB")
        chroma_key(image).resize((PORTRAIT_SIZE, PORTRAIT_SIZE), Image.LANCZOS).save(OUT / f"retrato_benicio_{n}.png", optimize=True)
        print(f"  retrato_benicio_{n}.png")


def main() -> int:
    """Sem argumentos refaz todos; com ids (ex.: celeste) refaz só esses (o npcs.json é sempre regravado inteiro)."""
    OUT.mkdir(parents=True, exist_ok=True)
    only = set(sys.argv[1:]) or None
    for npc in NPC_HEIGHT:
        if not only or npc in only:
            build_npc(npc)
    if not only:
        build_benicio()
    write_registry()
    build_portraits(only)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
