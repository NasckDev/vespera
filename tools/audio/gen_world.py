"""Gera os sons do motor do mundo (frente B4a): passo do jogador e os 7 loops de ambiente posicionais.

Saída (WAV 16-bit, 44.1 kHz, mono):
  assets/audio/sfx/step.wav              passo (baque curto e abafado de bota na pedra)
  assets/audio/ambient/<kind>.wav        loops sem emenda: water, fire, forge, crowd, wind, bells, birds

Os loops são montados num buffer circular: cada evento (estalo, martelada, pio, sino) que passa do fim volta pelo
começo, e os filtros são aquecidos com uma volta inteira (audiolib.biquad circular), então o loop não clica.
Picos baixos (0.03–0.09): ambiente fica sob a música e os efeitos; o volume final vem da distância no jogo
(AudioManager.SetAmbientEmitters, DECISIONS B4a-D47).

Uso: python tools/audio/gen_world.py [--out-sfx assets/audio/sfx] [--out-ambient assets/audio/ambient] [--only water,fire]
"""

import argparse
import math
import os
import random
import sys

sys.dont_write_bytecode = True  # não deixa __pycache__ no repositório

from audiolib import (  # noqa: E402
    SAMPLE_RATE,
    TWO_PI,
    bandpass_coefficients,
    biquad,
    lowpass_coefficients,
    normalize,
    remove_dc,
    write_wav,
)

SEED = 20261021


def noise(n, rng):
    return [rng.random() * 2.0 - 1.0 for _ in range(n)]


def add_wrapped(buf, start, samples, gain=1.0):
    n = len(buf)
    for i, s in enumerate(samples):
        buf[(start + i) % n] += s * gain


def decay_env(n, seconds, attack=0.002):
    a = max(1, int(attack * SAMPLE_RATE))
    k = 1.0 / max(1e-4, seconds * SAMPLE_RATE)
    return [min(1.0, i / a) * math.exp(-i * k) for i in range(n)]


def partials(freqs, amps, seconds, decay, phase_rng=None):
    n = int(seconds * SAMPLE_RATE)
    env = decay_env(n, decay)
    out = [0.0] * n
    for f, a in zip(freqs, amps):
        ph = phase_rng.random() * TWO_PI if phase_rng else 0.0
        w = TWO_PI * f / SAMPLE_RATE
        for i in range(n):
            out[i] += math.sin(ph + w * i) * a * env[i]
    return out


def slow_lfo(n, cycles, phase=0.0):
    """Senoide com número inteiro de ciclos no loop (emenda perfeita)."""
    return [math.sin(TWO_PI * cycles * i / n + phase) for i in range(n)]


# --- passo ---------------------------------------------------------------------------------------

def step(rng):
    n = int(0.11 * SAMPLE_RATE)
    thud = partials([92.0, 141.0], [0.6, 0.25], 0.11, 0.025)
    grit = biquad(noise(n, rng), lowpass_coefficients(1800.0, 0.8))
    env = decay_env(n, 0.018)
    scrape = biquad(noise(n, rng), bandpass_coefficients(3400.0, 1.2))
    env2 = decay_env(n, 0.03, attack=0.008)
    out = [thud[i] + grit[i] * env[i] * 0.55 + scrape[i] * env2[i] * 0.08 for i in range(n)]
    return normalize(remove_dc(out), 0.05)


# --- loops ---------------------------------------------------------------------------------------

def water(rng):
    n = int(6.0 * SAMPLE_RATE)
    base = biquad(noise(n, rng), lowpass_coefficients(520.0, 0.6), circular=True)
    lap = [0.55 + 0.25 * a + 0.2 * b for a, b in zip(slow_lfo(n, 3, 0.4), slow_lfo(n, 5, 2.1))]
    out = [base[i] * lap[i] for i in range(n)]
    # plips: gotas e marolas batendo no cais
    for _ in range(14):
        f = rng.uniform(600.0, 1400.0)
        d = rng.uniform(0.04, 0.09)
        m = int(d * SAMPLE_RATE)
        env = decay_env(m, d * 0.4)
        plip = [math.sin(TWO_PI * (f + 900.0 * i / m) * i / SAMPLE_RATE) * env[i] for i in range(m)]
        add_wrapped(out, rng.randrange(n), plip, rng.uniform(0.08, 0.2))
    return normalize(remove_dc(out), 0.07)


def fire(rng):
    n = int(4.0 * SAMPLE_RATE)
    roar = biquad(noise(n, rng), lowpass_coefficients(260.0, 0.7), circular=True)
    flick = [0.7 + 0.3 * a for a in slow_lfo(n, 7, 1.0)]
    out = [roar[i] * flick[i] * 0.9 for i in range(n)]
    hiss = biquad(noise(n, rng), bandpass_coefficients(2600.0, 0.7), circular=True)
    for i in range(n):
        out[i] += hiss[i] * 0.05
    for _ in range(70):  # estalos
        m = int(rng.uniform(0.004, 0.012) * SAMPLE_RATE)
        env = decay_env(m, m / SAMPLE_RATE / 3.0, attack=0.0005)
        pop = biquad([rng.random() * 2.0 - 1.0 for _ in range(m)], bandpass_coefficients(rng.uniform(1500.0, 4500.0), 1.0))
        add_wrapped(out, rng.randrange(n), [pop[i] * env[i] for i in range(m)], rng.uniform(0.6, 1.8))
    return normalize(remove_dc(out), 0.07)


def forge(rng):
    n = int(5.6 * SAMPLE_RATE)
    out = [0.0] * n
    bellows = biquad(noise(n, rng), lowpass_coefficients(380.0, 0.7), circular=True)
    breath = slow_lfo(n, 2, -1.2)
    for i in range(n):
        out[i] += bellows[i] * max(0.0, breath[i]) * 0.5
    for k in range(4):  # marteladas: duas fortes e duas leves no compasso
        t = int((k * 1.4 + rng.uniform(-0.02, 0.02)) * SAMPLE_RATE)
        strong = k % 2 == 0
        clank = partials([523.0, 1187.0, 1973.0, 2861.0], [0.5, 0.35, 0.22, 0.12], 0.9, 0.16 if strong else 0.1, rng)
        add_wrapped(out, t, clank, 1.0 if strong else 0.55)
        m = int(0.03 * SAMPLE_RATE)
        env = decay_env(m, 0.006, attack=0.0005)
        add_wrapped(out, t, [(rng.random() * 2 - 1) * env[i] for i in range(m)], 0.6)
    return normalize(remove_dc(out), 0.08)


def crowd(rng):
    n = int(6.0 * SAMPLE_RATE)
    out = [0.0] * n
    for v in range(6):  # vozes: ruído com formante e sílabas
        band = biquad(noise(n, rng), bandpass_coefficients(rng.uniform(380.0, 1100.0), 1.6), circular=True)
        rate = rng.choice([9, 11, 13, 15, 17])
        syll = [max(0.0, s) ** 1.5 for s in slow_lfo(n, rate * 6, rng.random() * TWO_PI)]
        phrase = [0.5 + 0.5 * s for s in slow_lfo(n, rng.choice([2, 3, 4]), rng.random() * TWO_PI)]
        for i in range(n):
            out[i] += band[i] * syll[i] * phrase[i] * 0.4
    bed = biquad(noise(n, rng), lowpass_coefficients(700.0, 0.7), circular=True)
    for i in range(n):
        out[i] += bed[i] * 0.25
    return normalize(remove_dc(out), 0.06)


def wind(rng):
    n = int(8.0 * SAMPLE_RATE)
    low = biquad(noise(n, rng), lowpass_coefficients(420.0, 0.7), circular=True)
    whistle = biquad(noise(n, rng), bandpass_coefficients(900.0, 4.0), circular=True)
    gust = [0.55 + 0.3 * a + 0.15 * b for a, b in zip(slow_lfo(n, 2, 0.3), slow_lfo(n, 3, 1.9))]
    out = [low[i] * gust[i] + whistle[i] * max(0.0, gust[i] - 0.6) * 0.8 for i in range(n)]
    return normalize(remove_dc(out), 0.06)


def bells(rng):
    n = int(10.0 * SAMPLE_RATE)
    out = [0.0] * n
    # sino distante da torre: parciais de sino (hum, prime, terça menor, quinta, oitava) com decaimento longo
    for t, f0, g in [(0.3, 294.0, 1.0), (2.6, 220.0, 0.7), (6.1, 294.0, 0.8)]:
        ratios = [0.5, 1.0, 1.183, 1.506, 2.0, 2.514]
        amps = [0.5, 0.6, 0.45, 0.25, 0.3, 0.12]
        b = partials([f0 * r for r in ratios], amps, 4.5, 1.6, rng)
        add_wrapped(out, int(t * SAMPLE_RATE), b, g)
    out = biquad(out, lowpass_coefficients(2400.0, 0.7), circular=True)
    return normalize(remove_dc(out), 0.06)


def birds(rng):
    n = int(6.0 * SAMPLE_RATE)
    out = [0.0] * n
    leaves = biquad(noise(n, rng), bandpass_coefficients(5200.0, 0.8), circular=True)
    for i in range(n):
        out[i] += leaves[i] * 0.04
    for _ in range(9):  # pios: subida rápida de frequência, às vezes em dupla
        start = rng.randrange(n)
        for rep in range(rng.choice([1, 2, 3])):
            d = rng.uniform(0.06, 0.13)
            m = int(d * SAMPLE_RATE)
            f0, f1 = rng.uniform(2400.0, 3600.0), rng.uniform(3800.0, 5600.0)
            env = [math.sin(math.pi * i / m) ** 2 for i in range(m)]
            ph = 0.0
            chirp = []
            for i in range(m):
                ph += TWO_PI * (f0 + (f1 - f0) * (i / m) ** 0.6) / SAMPLE_RATE
                chirp.append(math.sin(ph) * env[i])
            add_wrapped(out, start + rep * int(0.16 * SAMPLE_RATE), chirp, rng.uniform(0.25, 0.5))
    return normalize(remove_dc(out), 0.05)


AMBIENT = {"water": water, "fire": fire, "forge": forge, "crowd": crowd, "wind": wind, "bells": bells, "birds": birds}


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.normpath(os.path.join(here, "..", ".."))
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out-sfx", default=os.path.join(root, "assets", "audio", "sfx"))
    parser.add_argument("--out-ambient", default=os.path.join(root, "assets", "audio", "ambient"))
    parser.add_argument("--only", default="")
    args = parser.parse_args()
    only = {x for x in args.only.split(",") if x}
    os.makedirs(args.out_sfx, exist_ok=True)
    os.makedirs(args.out_ambient, exist_ok=True)

    jobs = [("step", os.path.join(args.out_sfx, "step.wav"), step)]
    jobs += [(k, os.path.join(args.out_ambient, k + ".wav"), f) for k, f in AMBIENT.items()]
    for name, path, fn in jobs:
        if only and name not in only:
            continue
        rng = random.Random(f"{SEED}:{name}")
        samples = fn(rng)
        write_wav(path, samples)
        peak = max(abs(s) for s in samples)
        print(f"{name:6s} {len(samples) / SAMPLE_RATE:5.2f}s  pico {peak:.3f}  -> {path}")


if __name__ == "__main__":
    main()
