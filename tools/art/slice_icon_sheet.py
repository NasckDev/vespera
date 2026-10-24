"""Recorta folhas de ícones (fundo #00FF00, grade cols x rows) em ícones quadrados RGBA.

Uso: python tools/art/slice_icon_sheet.py <repo> <folha.png> <cols> <rows> <tamanho> <saida1.png> [saida2.png ...]
As saídas seguem a ordem da grade (esquerda->direita, linha de cima primeiro). Saída "-" pula a célula.
Cada componente conexo do alpha vai para a célula do seu centróide (objetos que invadem a célula vizinha
não contaminam o recorte).
"""
import sys
from collections import deque
from pathlib import Path

import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

repo = Path(sys.argv[1])
sys.path.insert(0, str(repo / "tools" / "art"))
from chroma_key import chroma_key  # noqa: E402

ALPHA_MIN = 24  # alpha (0-255) que conta como parte do desenho
MIN_COMPONENT = 6  # px; ruído menor que isso é descartado
MARGIN = 0.06  # folga em volta do desenho no quadrado final


def label(mask: np.ndarray) -> tuple[np.ndarray, int]:
    h, w = mask.shape
    labels = np.zeros((h, w), dtype=np.int32)
    current = 0
    ys, xs = np.nonzero(mask)
    for y0, x0 in zip(ys.tolist(), xs.tolist()):
        if labels[y0, x0]:
            continue
        current += 1
        labels[y0, x0] = current
        queue = deque([(y0, x0)])
        while queue:
            y, x = queue.popleft()
            for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1), (y - 1, x - 1), (y - 1, x + 1), (y + 1, x - 1), (y + 1, x + 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not labels[ny, nx]:
                    labels[ny, nx] = current
                    queue.append((ny, nx))
    return labels, current


def main() -> None:
    sheet, cols, rows, size = Path(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5])
    outs = sys.argv[6:]
    assert len(outs) == cols * rows, f"esperava {cols * rows} saídas, veio {len(outs)}"
    rgba = np.asarray(chroma_key(Image.open(sheet)))
    h, w = rgba.shape[:2]
    mask = rgba[..., 3] >= ALPHA_MIN
    labels, count = label(mask)
    cell_of = np.zeros(count + 1, dtype=np.int32) - 1
    flat = labels.ravel()
    sizes = np.bincount(flat, minlength=count + 1)
    ys, xs = np.indices((h, w))
    sum_y = np.bincount(flat, weights=ys.ravel(), minlength=count + 1)
    sum_x = np.bincount(flat, weights=xs.ravel(), minlength=count + 1)
    for comp in range(1, count + 1):
        if sizes[comp] < MIN_COMPONENT:
            continue
        cy, cx = sum_y[comp] / sizes[comp], sum_x[comp] / sizes[comp]
        col = min(int(cx / (w / cols)), cols - 1)
        row = min(int(cy / (h / rows)), rows - 1)
        cell_of[comp] = row * cols + col
    # pixels de alpha baixo (borda suave, fora da máscara) herdam a célula pela posição
    pos_cell = np.minimum((ys / (h / rows)).astype(int), rows - 1) * cols + np.minimum((xs / (w / cols)).astype(int), cols - 1)
    cell_map = np.where(labels > 0, cell_of[labels], np.where(rgba[..., 3] > 0, pos_cell, -1))
    for index, out in enumerate(outs):
        if out == "-":
            continue
        sel = cell_map == index
        if not sel.any():
            raise SystemExit(f"célula {index} vazia em {sheet.name}")
        yy, xx = np.nonzero(sel & (rgba[..., 3] >= ALPHA_MIN))
        y0, y1, x0, x1 = yy.min(), yy.max() + 1, xx.min(), xx.max() + 1
        crop = rgba[y0:y1, x0:x1].copy()
        crop[~sel[y0:y1, x0:x1]] = 0
        side = int(max(y1 - y0, x1 - x0) * (1 + 2 * MARGIN))
        canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        canvas.paste(Image.fromarray(crop, "RGBA"), ((side - (x1 - x0)) // 2, (side - (y1 - y0)) // 2))
        icon = canvas.resize((size, size), Image.Resampling.LANCZOS)
        Path(out).parent.mkdir(parents=True, exist_ok=True)
        icon.save(out, optimize=True)
        print(f"{sheet.name}[{index}] {x1 - x0}x{y1 - y0} -> {out}")


if __name__ == "__main__":
    main()
