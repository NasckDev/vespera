"""Primitivas de pixel art para os tilesets da Vespera v2 (16x16, ruido semeado e tileavel)."""
import json
import zlib
from pathlib import Path

import numpy as np

T = 16
HERE = Path(__file__).resolve().parent
PAL = json.loads((HERE / "palette_tiles.json").read_text(encoding="utf-8"))
YS, XS = np.mgrid[0:T, 0:T]


def hex_rgba(h, a=255):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)] + [a], np.uint8)


def ramp(name):
    return [hex_rgba(c) for c in PAL["ramps"][name]]


def rng_of(*parts):
    return np.random.default_rng(zlib.crc32("|".join(map(str, parts)).encode()))


def blank():
    return np.zeros((T, T, 4), np.uint8)


def solid(c):
    a = blank()
    a[:, :] = c
    return a


def put(img, x, y, c, wrap=True):
    if wrap:
        img[y % T, x % T] = c
    elif 0 <= x < T and 0 <= y < T:
        img[y, x] = c


def tnoise(rng, cell):
    """Ruido de valor tileavel (periodo 16) em [0,1]."""
    n = T // cell
    g = rng.random((n, n))
    fx, fy = (XS + 0.5) / cell - 0.5, (YS + 0.5) / cell - 0.5
    x0, y0 = np.floor(fx).astype(int), np.floor(fy).astype(int)
    tx, ty = fx - x0, fy - y0
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    a, b = g[y0 % n, x0 % n], g[y0 % n, (x0 + 1) % n]
    c, d = g[(y0 + 1) % n, x0 % n], g[(y0 + 1) % n, (x0 + 1) % n]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty


def voronoi(rng, n=3, jitter=0.8):
    """Celulas tileaveis: id, d1, d2 e vetor (dx,dy) do pixel ao centro mais proximo."""
    cell = T / n
    pts = np.array([[(i + 0.5 + (rng.random() - 0.5) * jitter) * cell,
                     (j + 0.5 + (rng.random() - 0.5) * jitter) * cell] for j in range(n) for i in range(n)])
    px, py = XS + 0.5, YS + 0.5
    dxs, dys = [], []
    for p in pts:
        dx, dy = px - p[0], py - p[1]
        dx, dy = (dx + T / 2) % T - T / 2, (dy + T / 2) % T - T / 2
        dxs.append(dx)
        dys.append(dy)
    dxs, dys = np.array(dxs), np.array(dys)
    d = np.hypot(dxs, dys)
    order = np.argsort(d, axis=0)
    ids = order[0]
    d1 = np.take_along_axis(d, order[:1], 0)[0]
    d2 = np.take_along_axis(d, order[1:2], 0)[0]
    sdx = np.take_along_axis(dxs, order[:1], 0)[0]
    sdy = np.take_along_axis(dys, order[:1], 0)[0]
    return ids, d1, d2, sdx, sdy


def nb(mask):
    """Vizinhos (W, E, N, S) de uma mascara, com borda replicada."""
    p = np.pad(mask, 1, mode="edge")
    return p[1:-1, :-2], p[1:-1, 2:], p[:-2, 1:-1], p[2:, 1:-1]


def dilate(mask):
    w, e, n, s = nb(mask)
    return mask | w | e | n | s


def mix(img, tint, t):
    """Mistura RGB de img com a cor tint (sombra com matiz deslocado)."""
    out = img.copy()
    out[..., :3] = (img[..., :3] * (1 - t) + np.array(tint[:3]) * t).astype(np.uint8)
    return out


def corner_mask(bits, rng, amp=0.45):
    """Regiao B (True) de um tile de transicao; cantos tl,tr,br,bl. Cruza as bordas no meio do lado."""
    tl, tr, br, bl = bits
    u, v = (XS + 0.5) / T, (YS + 0.5) / T
    f = tl * (1 - u) * (1 - v) + tr * u * (1 - v) + br * u * v + bl * (1 - u) * v
    w = np.sin(np.pi * u) * np.sin(np.pi * v)
    return (f + (tnoise(rng, 4) - 0.5) * amp * w) > 0.5


def bricks(R, rng, bh=4, bw=8, base=2, mort=0, offset=4, speck=0.08, y_shift=0):
    """Tijolos/blocos tileaveis em x: mortalha escura, topo claro, base sombreada."""
    img = solid(R[base])
    rows = T // bh
    for r in range(rows):
        off = (r % 2) * offset
        for k in range(T // bw):
            x0 = (k * bw + off) % T
            sh = int(np.clip(base + rng.choice([-1, 0, 0, 0, 1]), 1, len(R) - 2))
            for yy in range(bh):
                for xx in range(bw):
                    col = R[sh]
                    if yy == 0:
                        col = R[min(sh + 1, len(R) - 1)]
                    elif yy == bh - 1:
                        col = R[mort]
                    elif xx == 0:
                        col = R[mort]
                    elif xx == bw - 1:
                        col = R[max(sh - 1, 1)]
                    put(img, x0 + xx, (r * bh + yy + y_shift), col)
    if speck:
        for _ in range(int(speck * T * T)):
            x, y = rng.integers(0, T, 2)
            if not (img[y, x] == R[mort]).all():
                img[y, x] = R[int(np.clip(base + rng.choice([-1, 1]), 1, len(R) - 1))]
    return img


def to_image(arr, scale=1):
    from PIL import Image
    im = Image.fromarray(arr, "RGBA")
    return im if scale == 1 else im.resize((im.width * scale, im.height * scale), Image.NEAREST)
