#!/usr/bin/env python3
"""Remove fundo verde chapado (#00FF00) de imagens do Gemini com borda suave e sem halo verde.

Uso:
  python tools/art/chroma_key.py entrada.png saida.png [--lo 100] [--hi 190] [--despill 1.0]
  python tools/art/chroma_key.py pasta_in pasta_out        (processa todos os PNG/JPG da pasta)

Método:
  1. "Verdez" d = G - max(R, B). O fundo puro tem d = 255; pixels do desenho quase nunca passam de ~100.
  2. alpha = 1 - smoothstep(lo, hi, d): opaco abaixo de lo, transparente acima de hi, rampa suave no meio (borda anti-aliased).
  3. Nas bordas a cor foi misturada com o verde: desfaz a mistura (F = (C - (1-a)*K) / a), com K = (0,255,0).
  4. Despill: o canal G é limitado a max(R, B) nos pixels de borda (até EDGE_REACH px da transparência), tirando a franja verde.
  5. Apara ilhas de alpha quase zero (ruído de JPEG) e mantém RGB limpo onde alpha = 0.
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

KEY = np.array([0.0, 255.0, 0.0])
DEFAULT_LO = 100.0
DEFAULT_HI = 190.0
EDGE_ALPHA = 0.98  # abaixo disso o pixel é de borda e recebe despill completo
NOISE_ALPHA = 0.02
EDGE_REACH = 2  # px


def smoothstep(edge0: float, edge1: float, x: np.ndarray) -> np.ndarray:
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def near_transparent(mask: np.ndarray) -> np.ndarray:
    """Dilata a máscara de borda em EDGE_REACH px: o halo verde também cai em pixels ainda opacos junto da borda."""
    image = Image.fromarray((mask * 255).astype(np.uint8))
    return np.asarray(image.filter(ImageFilter.MaxFilter(2 * EDGE_REACH + 1))) > 0


def chroma_key(image: Image.Image, lo: float = DEFAULT_LO, hi: float = DEFAULT_HI, despill: float = 1.0) -> Image.Image:
    rgb = np.asarray(image.convert("RGB"), dtype=np.float64)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]

    greenness = g - np.maximum(r, b)
    alpha = 1.0 - smoothstep(lo, hi, greenness)

    # desfaz a mistura com o verde nas bordas
    safe = np.maximum(alpha, 1e-3)[..., None]
    fg = (rgb - (1.0 - alpha)[..., None] * KEY) / safe
    fg = np.clip(fg, 0.0, 255.0)

    # despill: G não pode passar do maior entre R e B nos pixels de borda
    limit = np.maximum(fg[..., 0], fg[..., 2])
    spilled = np.minimum(fg[..., 1], limit)
    edge = near_transparent(alpha < EDGE_ALPHA)[..., None]
    mix = despill * edge[..., 0]
    fg[..., 1] = fg[..., 1] * (1.0 - mix) + spilled * mix

    # leve despill também no miolo, só quando o verde passa muito do vermelho/azul (reflexo do fundo)
    core_excess = np.clip(fg[..., 1] - limit - 40.0, 0.0, None)
    fg[..., 1] -= core_excess * despill * 0.5 * (~edge[..., 0])

    alpha = np.where(alpha < NOISE_ALPHA, 0.0, alpha)
    fg[alpha == 0.0] = 0.0
    out = np.dstack([np.clip(fg, 0, 255), alpha * 255.0]).round().astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def process(src: Path, dst: Path, lo: float, hi: float, despill: float) -> None:
    dst.parent.mkdir(parents=True, exist_ok=True)
    chroma_key(Image.open(src), lo, hi, despill).save(dst)
    print(f"{src.name} -> {dst}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("entrada", type=Path)
    parser.add_argument("saida", type=Path)
    parser.add_argument("--lo", type=float, default=DEFAULT_LO)
    parser.add_argument("--hi", type=float, default=DEFAULT_HI)
    parser.add_argument("--despill", type=float, default=1.0)
    args = parser.parse_args()

    if args.entrada.is_dir():
        for src in sorted(p for p in args.entrada.iterdir() if p.suffix.lower() in (".png", ".jpg", ".jpeg", ".webp")):
            process(src, args.saida / (src.stem + ".png"), args.lo, args.hi, args.despill)
    else:
        process(args.entrada, args.saida, args.lo, args.hi, args.despill)


if __name__ == "__main__":
    main()
