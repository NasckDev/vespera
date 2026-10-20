"""Gera os 18 efeitos sonoros do Véspera em assets/audio/sfx/<id>.wav (16-bit, 44.1 kHz, mono).

Reproduz a síntese WebAudio do protótipo v2 (core.js, seção "áudio"):
  - tone(f, d, type, v, slide, delay): oscilador (sine/square/sawtooth/triangle) em f Hz; se slide,
    rampa exponencial de frequência até max(30, f+slide) em d s; ganho v com rampa exponencial até 0.001
    em d s; o oscilador para em d + 0.05 s.
  - noise(d, v, f, delay): buffer de ruído branco de 0.5 s -> passa-banda (f Hz, Q 0.8) -> ganho v com
    rampa exponencial até 0.001 em d s; para em d + 0.05 s.
  - ganho mestre 0.22.
Padrões do JS: type 'square', v 0.2, noise f 1000, delay 0. slide 0 = sem rampa.

Diferenças conscientes (DECISIONS.md, D6: bug do protótipo vira comportamento pretendido):
  - O buffer de ruído do protótipo tem 0.5 s e não faz loop, então noise(0.6) (portal) era cortado em
    0.5 s. Aqui o buffer faz loop e o ruído dura o pedido.
Osciladores são band-limited por soma de harmônicos até Nyquist, como as PeriodicWave do WebAudio.

Uso: python tools/audio/gen_sfx.py [--out assets/audio/sfx]
"""

import argparse
import math
import os
import random
import sys

sys.dont_write_bytecode = True  # não deixa __pycache__ no repositório

from audiolib import SAMPLE_RATE, TWO_PI, bandpass_coefficients, write_wav  # noqa: E402

MASTER_GAIN = 0.22
NOISE_SECONDS = 0.5
STOP_TAIL = 0.05
ENV_FLOOR = 0.001
SEED = 20261020


def T(f, d, type="square", v=0.2, slide=0, delay=0.0):
    return ("tone", f, d, type, v, slide, delay)


def N(d, v=0.2, f=1000, delay=0.0):
    return ("noise", d, v, f, delay)


# Transcrição 1:1 de SFX em core.js (mesma ordem e mesmos números).
SFX = {
    "swing": [N(0.09, 0.16, 2600)],
    "hit": [N(0.07, 0.28, 900), T(170, 0.08, "square", 0.12, -80)],
    "brk": [T(620, 0.18, "triangle", 0.18, -380), N(0.22, 0.3, 500)],
    "dash": [N(0.14, 0.12, 3200)],
    "pick": [T(880, 0.07, "square", 0.07, 500)],
    "coin": [T(1320, 0.05, "square", 0.06), T(1760, 0.08, "square", 0.06, 0, 0.05)],
    "hurt": [T(150, 0.22, "sawtooth", 0.2, -70), N(0.1, 0.2, 600)],
    "ui": [T(700, 0.04, "square", 0.05)],
    "open": [T(440, 0.1, "triangle", 0.1), T(660, 0.14, "triangle", 0.1, 0, 0.08)],
    "boom": [N(0.4, 0.35, 280), T(90, 0.35, "sine", 0.3, -50)],
    "bow": [T(320, 0.09, "triangle", 0.14, 600), N(0.05, 0.08, 4000)],
    "magic": [T(520, 0.28, "sine", 0.12, 700)],
    "rank": [T(f, 0.3, "triangle", 0.13, 0, i * 0.11) for i, f in enumerate([523, 659, 784, 1046])],
    "die": [T(220, 1.1, "sawtooth", 0.16, -170)],
    "portal": [T(180, 0.6, "sine", 0.18, 260), N(0.6, 0.08, 1500)],
    "growl": [T(90, 0.25, "sawtooth", 0.08, -20)],
    "kill": [T(300, 0.2, "square", 0.1, -200), N(0.15, 0.15, 1500)],
    "eco": [T(f, 0.35, "sine", 0.14, 0, i * 0.09) for i, f in enumerate([392, 523, 784])],
}


def harmonic_amp(kind, n):
    """Coeficientes de seno da série de Fourier usados pelo WebAudio para cada forma de onda."""
    if n == 0:
        return 0.0
    if kind == "sine":
        return 1.0 if n == 1 else 0.0
    if kind == "square":
        return 4.0 / (n * math.pi) if n % 2 == 1 else 0.0
    if kind == "sawtooth":
        return 2.0 * (1 if n % 2 == 1 else -1) / (n * math.pi)
    if kind == "triangle":
        if n % 2 == 0:
            return 0.0
        return 8.0 * (1 if n % 4 == 1 else -1) / (math.pi * n) ** 2
    raise ValueError(kind)


def envelope(t, v, d):
    """setValueAtTime(v, 0) + exponentialRampToValueAtTime(0.001, d); depois segura 0.001."""
    if t >= d:
        return ENV_FLOOR
    return v * (ENV_FLOOR / v) ** (t / d)


def render_tone(f, d, kind, v, slide, sr):
    length = int(round((d + STOP_TAIL) * sr))
    f_end = max(30.0, f + slide) if slide else f
    max_harm = 64 if kind != "sine" else 1
    amps = [harmonic_amp(kind, n) for n in range(max_harm * 8 + 1)]
    out = [0.0] * length
    phase = 0.0
    nyquist = sr / 2.0
    for i in range(length):
        t = i / sr
        freq = f * (f_end / f) ** (t / d) if (slide and t < d) else f_end
        # soma de harmônicos até Nyquist (recorrência de Chebyshev para sin(n*theta))
        theta = TWO_PI * phase
        s1 = math.sin(theta)
        acc = amps[1] * s1
        harmonics = min(int(nyquist / freq), len(amps) - 1) if kind != "sine" else 1
        if harmonics >= 2:
            c2 = 2.0 * math.cos(theta)
            s_prev, s_cur = 0.0, s1
            for n in range(2, harmonics + 1):
                s_prev, s_cur = s_cur, c2 * s_cur - s_prev
                a = amps[n]
                if a:
                    acc += a * s_cur
        out[i] = acc * envelope(t, v, d)
        phase += freq / sr
        if phase >= 1.0:
            phase -= 1.0
    return out


def render_noise(d, v, f, noise_buf, sr):
    length = int(round((d + STOP_TAIL) * sr))
    b0, b1, b2, a1, a2 = bandpass_coefficients(f, 0.8, sr)
    x1 = x2 = y1 = y2 = 0.0
    out = [0.0] * length
    n_buf = len(noise_buf)
    for i in range(length):
        x = noise_buf[i % n_buf]  # D6: o protótipo cortava em 0.5 s; aqui o buffer faz loop
        y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1 = x1, x
        y2, y1 = y1, y
        out[i] = y * envelope(i / sr, v, d)
    return out


def render_sfx(parts, noise_buf, sr=SAMPLE_RATE):
    rendered = []
    for part in parts:
        if part[0] == "tone":
            _, f, d, kind, v, slide, delay = part
            rendered.append((delay, render_tone(f, d, kind, v, slide, sr)))
        else:
            _, d, v, f, delay = part
            rendered.append((delay, render_noise(d, v, f, noise_buf, sr)))
    total = max(int(round(delay * sr)) + len(buf) for delay, buf in rendered)
    mix = [0.0] * total
    for delay, buf in rendered:
        offset = int(round(delay * sr))
        for i, s in enumerate(buf):
            mix[offset + i] += s
    return [s * MASTER_GAIN for s in mix]


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    default_out = os.path.normpath(os.path.join(here, "..", "..", "assets", "audio", "sfx"))
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", default=default_out)
    args = parser.parse_args()
    os.makedirs(args.out, exist_ok=True)

    rng = random.Random(SEED)
    noise_buf = [rng.random() * 2.0 - 1.0 for _ in range(int(SAMPLE_RATE * NOISE_SECONDS))]

    for sfx_id, parts in SFX.items():
        samples = render_sfx(parts, noise_buf)
        path = os.path.join(args.out, sfx_id + ".wav")
        write_wav(path, samples)
        peak = max(abs(s) for s in samples)
        print(f"{sfx_id:7s} {len(samples) / SAMPLE_RATE:5.2f}s  pico {peak:.3f}  -> {path}")


if __name__ == "__main__":
    main()
