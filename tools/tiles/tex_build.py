"""Texturas de construcao: telhado, paredes (frente de 2 tiles + tampo), escada, meio-fio, esgoto."""
import numpy as np

from tiles_lib import (T, XS, YS, blank, solid, put, ramp, rng_of, tnoise, bricks, mix, hex_rgba, PAL)


def _sides(img, side, R, lit=3, dark=0, shade=1):
    """Contorno lateral: L tem borda escura e luz interna; R borda escura e sombra interna."""
    if side == "L":
        img[:, 0] = R[dark]
        img[:, 1] = R[lit]
    elif side == "R":
        img[:, 15] = R[dark]
        img[:, 14] = R[shade]
    return img


# ---------------------------------------------------------------- telhado (tampo de parede) - cidade

def roof_fill(side="M", variant=0):
    R = ramp("roof")
    img = bricks(R, rng_of("roof", variant), bh=4, bw=8, base=2, mort=0, offset=4, speck=0.06)
    return _sides(img, side, R)


def roof_cap(side="M", variant=0):
    """Fileira superior do telhado: cumeeira clara (borda de tras) sobre telhas."""
    R = ramp("roof")
    img = roof_fill(side, variant + 5)
    img[0, :] = R[0]
    img[1, :] = R[4]
    img[2, :] = R[3]
    img[3, :] = R[1]
    if side == "L":
        img[0:4, 0] = R[0]
        img[1, 0] = R[0]
    if side == "R":
        img[0:4, 15] = R[0]
    return img


# ---------------------------------------------------------------- fachada (2 tiles de altura) - cidade

def face_upper(side="M", variant=0, window=False):
    W, Rr, Rw = ramp("wall"), ramp("roof"), ramp("wood")
    rng = rng_of("faceu", variant)
    img = bricks(W, rng, bh=4, bw=8, base=2, mort=1, offset=4, speck=0.06)
    # beiral: contorno escuro, friso roxo, sombra projetada nos tijolos
    img[0, :] = Rr[0]
    img[1, :] = Rr[3]
    img[2, :] = Rr[2]
    img[3, :] = Rr[1]
    shadow = mix(img[4:6], Rr[0], 0.45)
    img[4:6] = shadow
    if window:
        x0, y0 = 4, 6
        for y in range(y0, y0 + 8):
            for x in range(x0, x0 + 8):
                edge = x in (x0, x0 + 7) or y in (y0, y0 + 7)
                img[y, x] = Rr[0] if edge else Rw[3] if y < y0 + 4 else Rw[2]
        img[y0 + 1:y0 + 7, x0 + 3] = Rr[1]       # montante vertical
        img[y0 + 4, x0 + 1:x0 + 7] = Rr[1]       # travessa
        img[y0 + 1, x0 + 1] = Rw[4]
        img[y0 + 2, x0 + 1] = Rw[4]
        img[y0 + 8, x0 - 1:x0 + 9] = W[4]        # peitoril claro
        img[y0 + 9, x0 - 1:x0 + 9] = W[0]
    return _sides(img, side, W, lit=3, dark=0, shade=1)


def face_lower(side="M", variant=0, door=False):
    W, S, Rr, Rw = ramp("wall"), ramp("stone"), ramp("roof"), ramp("wood")
    rng = rng_of("facel", variant)
    img = bricks(W, rng, bh=4, bw=8, base=2, mort=1, offset=4, speck=0.06)
    # soco de pedra (base), com degrau claro no topo e contorno no chao
    found = bricks(S, rng_of("found", variant), bh=5, bw=8, base=2, mort=0, offset=4, speck=0.1)
    img[11:] = found[0:5]
    img[11, :] = S[4]
    img[12, :] = S[3]
    img[15, :] = Rr[0]
    if door:
        for y in range(0, 15):
            for x in range(3, 13):
                edge = x in (3, 12) or (y == 0)
                img[y, x] = Rr[0] if edge else Rw[1] if x % 3 else Rw[0]
        img[7:9, 10] = Rw[4]
        img[15, 3:13] = Rr[0]
    return _sides(img, side, W, lit=3, dark=0, shade=1)


# ---------------------------------------------------------------- escada e meio-fio

def stairs(part="M"):
    """4 degraus (sobe para o norte): piso claro com brilho no topo, espelho escuro embaixo."""
    S = ramp("stone")
    img = blank()
    for step in range(4):
        y = step * 4
        img[y, :] = S[4]
        img[y + 1, :] = S[3]
        img[y + 2, :] = S[1]
        img[y + 3, :] = S[0]
    if part in ("L", "R"):
        x0, x1 = (0, 4) if part == "L" else (12, 16)
        img[:, x0:x1] = S[2]
        img[:, x0:x1][::4] = S[4]
        outer, inner = (x0, x1 - 1) if part == "L" else (x1 - 1, x0)
        img[:, outer] = S[0]
        img[:, inner] = S[1]
        img[:, x0 + 1 if part == "L" else x1 - 2] = S[3]
    return img


def curb_layers():
    """Faixas de meio-fio com alfa; luz do canto superior esquerdo. Retorna dict de 8 tiles."""
    S = ramp("stone")

    def band(side):
        a = blank()
        if side == "N":
            a[0, :] = S[0]; a[1:3, :] = S[4]; a[3, :] = S[2]; a[4, :] = S[1]
        elif side == "S":
            a[11, :] = S[0]; a[12:14, :] = S[4]; a[14, :] = S[2]; a[15, :] = S[0]
        elif side == "W":
            a[:, 0] = S[0]; a[:, 1:3] = S[4]; a[:, 3] = S[1]
        elif side == "E":
            a[:, 12] = S[2]; a[:, 13:15] = S[3]; a[:, 15] = S[0]
        return a

    def union(*sides):
        out = blank()
        for sd in sides:
            b = band(sd)
            out = np.where(b[..., 3:4] > 0, b, out)
        return out

    return {"curb_n": union("N"), "curb_s": union("S"), "curb_w": union("W"), "curb_e": union("E"),
            "curb_nw": union("N", "W"), "curb_ne": union("N", "E"),
            "curb_sw": union("S", "W"), "curb_se": union("S", "E")}


# ---------------------------------------------------------------- esgoto

def sew_top_fill(side="M", variant=0):
    R = ramp("sew_wall")
    img = bricks(R, rng_of("sewtop", variant), bh=8, bw=8, base=3, mort=0, offset=4, speck=0.0)
    img[np.all(img == R[4], axis=-1)] = R[3]
    return _sides(img, side, R, lit=4, dark=0, shade=2)


def sew_top_cap(side="M", variant=0):
    R = ramp("sew_wall")
    img = sew_top_fill(side, variant + 3)
    img[0, :] = R[0]
    img[1, :] = R[4]
    img[2, :] = R[3]
    return img


def _slime(img, rng, n=2, maxlen=7):
    G = ramp("sewage")
    for _ in range(n):
        x = int(rng.integers(2, 14))
        ln = int(rng.integers(3, maxlen))
        for i in range(ln):
            img[4 + i, x] = G[2] if i < ln - 1 else G[3]
            if i < ln - 2:
                img[4 + i, x + 1] = G[1]
    return img


def sew_face_upper(side="M", variant=0, slime=False, rift=False):
    R, Rp = ramp("sew_wall"), ramp("rift")
    rng = rng_of("sewfu", variant)
    img = bricks(R, rng, bh=4, bw=8, base=2, mort=0, offset=4, speck=0.08)
    img[0, :] = R[0]
    img[1, :] = R[4]    # borda do tampo, iluminada
    img[2, :] = R[2]
    img[3, :] = R[1]
    img[4:6] = mix(img[4:6], R[0], 0.5)
    if slime:
        _slime(img, rng)
    if rift:  # fenda de luz-de-brecha, pequena e rara
        for i, (x, y) in enumerate([(9, 6), (10, 7), (10, 8), (11, 9), (11, 10)]):
            img[y, x] = Rp[2] if i != 2 else Rp[3]
            img[y, x - 1] = Rp[0]
    return _sides(img, side, R, lit=3, dark=0, shade=1)


def sew_face_lower(side="M", variant=0, grate=False):
    R, G = ramp("sew_wall"), ramp("sewage")
    rng = rng_of("sewfl", variant)
    img = bricks(R, rng, bh=4, bw=8, base=2, mort=0, offset=4, speck=0.08)
    img[12:15] = mix(img[12:15], G[1], 0.35)          # umidade na base
    img[15, :] = R[0]
    for x in range(T):
        if (x * 7 + variant) % 5 < 2:
            img[14, x] = G[1]
    if grate:  # grade de ferro na parede: abertura escura com barras
        S = ramp("stone")
        for y in range(2, 15):
            for x in range(3, 13):
                edge = x in (3, 12) or y == 2
                img[y, x] = S[1] if edge else R[0]
        for x in range(5, 12, 3):
            img[3:15, x] = S[3]
            img[3:15, x + 1] = S[1]
        img[6, 4:12] = S[3]
        img[10, 4:12] = S[2]
        img[13:15, 4:12] = np.where((XS[13:15, 4:12] % 2 == 0)[..., None], G[1], G[0])
    return _sides(img, side, R, lit=3, dark=0, shade=1)


def void_tile():
    return solid(ramp("sew_wall")[0])


def floor_grate(kind=0):
    """Grade de piso (anda por cima): moldura, barras e escuro com brilho esverdeado embaixo."""
    S, G = ramp("sew_floor"), ramp("sewage")
    img = solid(S[0])
    if kind == 0:   # barras verticais
        for x in range(2, 14, 3):
            img[2:14, x:x + 2] = ramp("stone")[3]
            img[2:14, x + 1] = ramp("stone")[2]
            img[13, x:x + 2] = ramp("stone")[1]
    else:           # ralo quadrado
        for y in range(3, 13):
            for x in range(3, 13):
                if (x + y) % 2 == 0:
                    img[y, x] = ramp("stone")[3]
        img[2, 2:14] = ramp("stone")[4]
        img[13, 2:14] = ramp("stone")[1]
        img[2:14, 2] = ramp("stone")[3]
        img[2:14, 13] = ramp("stone")[1]
    img[0, :] = ramp("stone")[3]
    img[15, :] = ramp("stone")[0]
    img[:, 0] = ramp("stone")[3]
    img[:, 15] = ramp("stone")[0]
    return img


def debris(variant):
    """Decal de cacos de ceramica quebrada sobre fundo transparente."""
    R, cream = ramp("clay"), hex_rgba(PAL["brand"]["creme"])
    rng = rng_of("debris", variant)
    img = blank()

    def shard(cx, cy, r, ang):
        pts = []
        for k in range(3):
            a = ang + k * 2.1 + rng.random() * 0.5
            rr = r * 1.7 * (0.75 + rng.random() * 0.4)
            pts.append((cx + np.cos(a) * rr, cy + np.sin(a) * rr))
        (x1, y1), (x2, y2), (x3, y3) = pts
        den = (y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3)
        if abs(den) < 1e-6:
            return
        inside = np.zeros((T, T), bool)
        for y in range(T):
            for x in range(T):
                px, py = x + 0.5, y + 0.5
                a = ((y2 - y3) * (px - x3) + (x3 - x2) * (py - y3)) / den
                b = ((y3 - y1) * (px - x3) + (x1 - x3) * (py - y3)) / den
                inside[y, x] = a >= 0 and b >= 0 and (1 - a - b) >= 0
        for y in range(T):
            for x in range(T):
                if inside[y, x]:
                    up = inside[y - 1, x] if y > 0 else False
                    lf = inside[y, x - 1] if x > 0 else False
                    dn = inside[y + 1, x] if y < T - 1 else False
                    rt = inside[y, x + 1] if x < T - 1 else False
                    if not (dn and rt):
                        col = R[0]
                    elif not (up and lf):
                        col = R[4]
                    else:
                        col = R[2] if (x + y) % 3 else cream
                    img[y, x] = col
        # contorno de sombra projetada para baixo e direita
        for y in range(T - 1):
            for x in range(T - 1):
                if not inside[y, x] and (inside[y - 1, x] if y else False) and img[y, x, 3] == 0:
                    img[y, x] = (*R[0][:3], 150)

    layouts = {
        0: [(4, 5, 1.9), (11, 4, 1.6), (8, 11, 1.9), (3, 12, 1.3)],
        1: [(8, 8, 2.8), (3, 3, 1.3), (13, 12, 1.4)],
        2: [(5, 6, 1.8), (11, 10, 2.0)],
        3: [(4, 11, 1.5), (10, 5, 1.5), (12, 12, 1.2), (6, 3, 1.2)],
    }
    for (cx, cy, r) in layouts[variant % 4]:
        shard(cx, cy, r, rng.random() * 6.28)
    if variant == 2:  # base circular do pote: anel com miolo escuro
        for y in range(T):
            for x in range(T):
                d = np.hypot(x - 8.5, y - 9.5)
                if 2.2 < d < 3.6 and img[y, x, 3] == 0:
                    img[y, x] = R[3] if (x + y) % 2 else R[2]
    return img


def wall_shadow():
    """Sombra projetada no chao na base de uma fachada (faixa com alfa decrescente)."""
    img = blank()
    ind = hex_rgba(PAL["brand"]["indigo"])
    for y, al in enumerate([150, 110, 70, 35]):
        img[y, :] = (*ind[:3], al)
    return img
