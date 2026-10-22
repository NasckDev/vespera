"""Texturas de chao: grama, terra, calcamento, agua, piso de esgoto e as transicoes."""
import numpy as np

from tiles_lib import (T, XS, YS, blank, solid, put, ramp, rng_of, tnoise, voronoi, nb, dilate,
                       corner_mask, hex_rgba, PAL)

FLOWER = [hex_rgba(c) for c in PAL["ramps"]["flower"]]


def grass(variant, flowers=0):
    R = ramp("grass")
    rng = rng_of("grass", variant)
    img = solid(R[2])
    n = tnoise(rng, 8) * 0.55 + tnoise(rng, 4) * 0.45
    img[n > 0.74] = R[3]
    img[n < 0.27] = R[1]
    for _ in range(4):  # tufos em V: pontas claras, base escura
        x, y = rng.integers(0, T, 2)
        put(img, x, y - 1, R[3])
        put(img, x + 2, y - 1, R[3])
        put(img, x + 1, y, R[1])
    for _ in range(6):
        x, y = rng.integers(0, T, 2)
        put(img, x, y, R[3] if rng.random() < 0.6 else R[1])
    cols = [FLOWER[0], FLOWER[1], FLOWER[3]]
    for k in range(flowers):
        x, y = int(rng.integers(2, 13)), int(rng.integers(3, 13))
        c = cols[(variant + k) % 3]
        for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
            img[y + dy, x + dx] = c
        img[y, x] = FLOWER[2]
        img[y + 2, x] = R[0]  # haste
        img[y + 2, x + 1] = R[1]
    return img


def dirt(variant):
    R = ramp("dirt")
    rng = rng_of("dirt", variant)
    img = solid(R[2])
    n = tnoise(rng, 8)
    img[n > 0.74] = R[3]
    img[n < 0.24] = R[1]
    for _ in range(26):  # granulado cristalino
        x, y = rng.integers(0, T, 2)
        put(img, x, y, R[1] if rng.random() < 0.55 else R[3])
    for _ in range(3):  # pedrinhas 2x1: topo claro, sombra embaixo a direita
        x, y = rng.integers(1, 14, 2)
        put(img, x, y, R[4])
        put(img, x + 1, y, R[3])
        put(img, x + 1, y + 1, R[0])
        put(img, x, y + 1, R[1])
    return img


def stone_cells(Rs, variant, tag, n=3, tint=None, crack=None):
    """Calcamento/lajes irregulares por voronoi tileavel com bisel de luz no canto superior esquerdo."""
    rng = rng_of(tag, variant)
    ids, d1, d2, sdx, sdy = voronoi(rng, n)
    base = rng.choice([2, 2, 3, 2, 3], size=n * n)
    zone = tnoise(rng, 8)
    img = solid(Rs[0])
    gap = d2 - d1
    for y in range(T):
        for x in range(T):
            if gap[y, x] < 0.9:
                continue
            if gap[y, x] < 1.5:
                img[y, x] = Rs[1]
                continue
            s = int(base[ids[y, x]]) + (1 if zone[y, x] > 0.72 else -1 if zone[y, x] < 0.28 else 0)
            lean = -(sdx[y, x] + sdy[y, x])
            if gap[y, x] < 3.4:
                if lean > 2.4:
                    s += 1
                elif lean < -2.4:
                    s -= 1
            s = int(np.clip(s, 1, len(Rs) - 2))
            img[y, x] = Rs[s]

    for _ in range(5):
        x, y = rng.integers(0, T, 2)
        if gap[y, x] >= 1.5:
            img[y, x] = Rs[int(np.clip(base[ids[y, x]] + rng.choice([-1, 1]), 1, 3))]
    return img


def cobble(variant):
    return stone_cells(ramp("stone"), variant, "cobble", 3, tint=ramp("dirt")[3])


def slabs(R, variant, tag):
    """Lajes retangulares (3 fileiras de alturas 6/5/5 em ordem variavel, 2 blocos por fileira)."""
    rng = rng_of(tag, variant)
    img = solid(R[0])
    heights = list(rng.permutation([6, 5, 5]))
    y = 0
    for h in heights:
        split = int(rng.integers(5, 11))
        start = int(rng.integers(0, T))
        for (x0, w) in ((start, split), (start + split, T - split)):
            sh = int(rng.choice([2, 2, 3, 2, 1]))
            for yy in range(h):
                for xx in range(w):
                    if xx == 0 or yy == h - 1:
                        c = R[0]
                    elif yy == 0:
                        c = R[min(sh + 1, 4)]
                    elif xx == 1:
                        c = R[min(sh + 1, 4)]
                    elif xx == w - 1 or yy == h - 2:
                        c = R[max(sh - 1, 1)]
                    else:
                        c = R[sh]
                    put(img, x0 + xx, y + yy, c)
        y += h
    for _ in range(7):
        x, yy = rng.integers(0, T, 2)
        if (img[yy, x] != R[0]).any():
            put(img, x, yy, R[int(rng.choice([1, 3]))])
    return img


def sew_floor(variant):
    return slabs(ramp("sew_floor"), variant, "sewslab")


def sew_floor_rift(variant):
    """Laje com fenda de luz-de-brecha (rara): linha magenta 1px com brilho escuro ao redor."""
    img = sew_floor(variant + 10)
    R = ramp("rift")
    rng = rng_of("rift", variant)
    x, y = int(rng.integers(3, 6)), 2
    path = []
    for _ in range(12):
        path.append((x, y))
        y += 1
        x += int(rng.choice([-1, 0, 1, 1]))
        x = int(np.clip(x, 2, 13))
    for (px, py) in path:
        for dx in (-1, 1):
            if img[py, px + dx][0] < 120:
                img[py, px + dx] = R[0]
    for i, (px, py) in enumerate(path):
        img[py, px] = R[2] if i % 4 else R[3]
    return img


def sew_mossy(variant):
    """Laje com limo verde crescendo pelas juntas e cantos."""
    img = sew_floor(variant + 20)
    R, G = ramp("sew_floor"), ramp("sewage")
    rng = rng_of("moss", variant)
    n = tnoise(rng, 4)
    joint = (img == R[0]).all(axis=-1)
    img[joint & (n > 0.5)] = G[1]
    for _ in range(5):
        x, y = rng.integers(0, T, 2)
        if joint[y, x]:
            put(img, x, y, G[2])
            put(img, x + 1, y, G[1])
    return img


# ---------------------------------------------------------------- agua

def shifted(ramp_name):
    """Rampa de agua; o esgoto usa a rampa uma casa mais escura (base=1, brilho=3) para ficar turvo."""
    R = ramp(ramp_name)
    if ramp_name == "sewage":
        return [R[0], R[0], R[1], R[2], R[3]]
    return R

def water_tex(frame, ramp_name="water", seed="water", extra=0):
    R = shifted(ramp_name)
    rng = rng_of(seed, extra)
    img = solid(R[2])
    for _ in range(14):  # pontos fundos esparsos (sem blocos grandes, para nao marcar a grade)
        x, y = rng.integers(0, T, 2)
        put(img, x, y, R[1])
    dashes = [(int(rng.integers(0, T)), int(rng.integers(0, T)), int(rng.integers(3, 6)),
               int(rng.choice([4, 8, -4]))) for _ in range(7)]
    for (x, y, ln, sp) in dashes:
        xx = (x + sp * frame) % T
        for i in range(ln):
            put(img, xx + i, y, R[3])
        put(img, xx + ln, y + 1, R[1])
    for k in range(4):  # brilhos que piscam em quadros distintos
        x, y = int(rng.integers(0, T)), int(rng.integers(0, T))
        if frame == k:
            put(img, x, y, R[4])
    if ramp_name == "sewage" and extra == 1 and frame in (1, 2):  # reflexo raro da luz-de-brecha
        put(img, 11, 5, ramp("rift")[1 + (frame == 2)])
    return img


def water_shore(frame, bits_water, rng_name, bank, wtex, ramp_name, bank_ramp, foam=True):
    """Compoe agua (bits=1 -> agua) sobre o tile 'bank' com margem: sombra no N/O, espuma no S/L."""
    rng = rng_of(rng_name, tuple(bits_water))
    m = corner_mask(bits_water, rng)
    W = shifted(ramp_name)
    out = np.where(m[..., None], wtex, bank)
    if m.all() or not m.any():
        return out
    w, e, n, s = nb(~m)  # vizinho nao-agua
    shadow = m & (w | n)
    light = m & (e | s) & ~shadow
    near2 = m & dilate(~m) & ~shadow & ~light
    out[near2] = W[3] if frame % 2 == 0 else W[2]
    out[shadow] = W[0]
    if foam:
        fx = ((XS + frame * 2) % 4) != 0
        out[light & fx] = W[4]
        out[light & ~fx] = W[3]
    else:
        out[light] = W[3]
    B = bank_ramp
    wm, em, nm, sm = nb(m)  # vizinho agua (para pixels de banco)
    edge = ~m & (wm | em | nm | sm)
    out[edge & sm] = B[1]            # agua ao sul: face do barranco, escura
    out[edge & nm & ~sm] = B[3]      # agua ao norte: borda iluminada
    out[edge & (wm | em) & ~sm & ~nm] = B[2]
    second = ~m & ~edge & dilate(edge)
    out[second & sm & (XS % 2 == 0)] = B[2]
    return out


# ---------------------------------------------------------------- transicoes

def edge_grass_cobble(A, B, m, rng):
    out = np.where(m[..., None], B, A)
    Rs, Rg = ramp("stone"), ramp("grass")
    W, E, N, S = nb(m)
    lit = m & (~W | ~N)
    shade = m & (~E | ~S) & ~lit
    out[lit] = Rs[3]
    out[shade] = Rs[1]
    edge = lit | shade
    for y, x in zip(*np.nonzero(edge)):
        if rng.random() < 0.22:
            out[y, x] = Rg[2]
    adj = ~m & (W | E | N | S)
    out[adj] = Rg[1]
    return out


def edge_grass_dirt(A, B, m, rng):
    out = np.where(m[..., None], B, A)
    Rd, Rg = ramp("dirt"), ramp("grass")
    W, E, N, S = nb(m)
    adjb = m & ~(W & E & N & S)
    out[adjb] = Rd[1]
    adj = ~m & (W | E | N | S)
    for y, x in zip(*np.nonzero(adj)):
        out[y, x] = Rd[2] if rng.random() < 0.5 else Rg[1]
    return out


def edge_cobble_dirt(A, B, m, rng):
    """A = calcamento, B = terra."""
    out = np.where(m[..., None], B, A)
    Rd, Rs = ramp("dirt"), ramp("stone")
    W, E, N, S = nb(m)
    adjb = m & ~(W & E & N & S)
    out[adjb] = Rd[1]
    adj = ~m & (W | E | N | S)
    for y, x in zip(*np.nonzero(adj)):
        out[y, x] = Rd[2] if rng.random() < 0.45 else Rs[1]
    return out
