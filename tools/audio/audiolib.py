"""Utilidades de síntese compartilhadas por gen_sfx.py e gen_music.py.

Só biblioteca padrão do Python 3 (sem numpy). Tudo é determinístico: quem precisa de ruído
recebe um random.Random com seed fixa.
"""

import math
import struct
import wave

SAMPLE_RATE = 44100
TWO_PI = 2.0 * math.pi


def write_wav(path, samples, sample_rate=SAMPLE_RATE):
    """Grava WAV PCM 16-bit mono. Amostras em [-1, 1]; o que passar disso é cortado."""
    frames = bytearray()
    pack = struct.Struct("<h").pack
    for s in samples:
        if s > 1.0:
            s = 1.0
        elif s < -1.0:
            s = -1.0
        frames += pack(int(round(s * 32767.0)))
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sample_rate)
        w.writeframes(bytes(frames))


def bandpass_coefficients(freq, q, sample_rate=SAMPLE_RATE):
    """Biquad passa-banda do WebAudio (Audio EQ Cookbook, pico de 0 dB), já normalizado por a0."""
    w0 = TWO_PI * freq / sample_rate
    alpha = math.sin(w0) / (2.0 * q)
    a0 = 1.0 + alpha
    return (alpha / a0, 0.0, -alpha / a0, (-2.0 * math.cos(w0)) / a0, (1.0 - alpha) / a0)


def lowpass_coefficients(freq, q=0.7071, sample_rate=SAMPLE_RATE):
    """Biquad passa-baixa (Audio EQ Cookbook), normalizado por a0."""
    w0 = TWO_PI * freq / sample_rate
    alpha = math.sin(w0) / (2.0 * q)
    cw = math.cos(w0)
    a0 = 1.0 + alpha
    return ((1.0 - cw) / 2.0 / a0, (1.0 - cw) / a0, (1.0 - cw) / 2.0 / a0, (-2.0 * cw) / a0, (1.0 - alpha) / a0)


def biquad(samples, coefficients, circular=False):
    """Filtra e devolve uma lista nova. circular=True aquece o estado com uma volta inteira,
    para que o início do loop continue exatamente o fim (sem clique na emenda)."""
    b0, b1, b2, a1, a2 = coefficients
    x1 = x2 = y1 = y2 = 0.0
    passes = 2 if circular else 1
    out = samples
    for p in range(passes):
        out = [0.0] * len(samples)
        for i, x in enumerate(samples):
            y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
            x2, x1 = x1, x
            y2, y1 = y1, y
            out[i] = y
    return out


def reverb(samples, wet=0.3, room=0.84, damp=0.25, circular=True, sample_rate=SAMPLE_RATE):
    """Reverb simples estilo Schroeder/Freeverb: 4 combs com amortecimento em paralelo + 2 allpass em série.
    circular=True processa duas voltas e devolve a segunda, então a cauda do fim entra no começo do loop."""
    scale = sample_rate / 44100.0
    comb_lengths = [int(n * scale) for n in (1116, 1188, 1277, 1356)]
    allpass_lengths = [int(n * scale) for n in (556, 441)]
    n = len(samples)
    passes = 2 if circular else 1

    combs = [[0.0] * length for length in comb_lengths]
    comb_idx = [0] * len(combs)
    comb_store = [0.0] * len(combs)
    aps = [[0.0] * length for length in allpass_lengths]
    ap_idx = [0] * len(aps)

    out = [0.0] * n
    gain_in = 0.015 * 4  # Freeverb usa ganho fixo de entrada; aqui compensado para 4 combs
    for p in range(passes):
        for i in range(n):
            x = samples[i] * gain_in
            acc = 0.0
            for c in range(4):
                buf = combs[c]
                j = comb_idx[c]
                y = buf[j]
                comb_store[c] = y * (1.0 - damp) + comb_store[c] * damp
                buf[j] = x + comb_store[c] * room
                comb_idx[c] = j + 1 if j + 1 < len(buf) else 0
                acc += y
            for a in range(2):
                buf = aps[a]
                j = ap_idx[a]
                bufout = buf[j]
                y = -acc + bufout
                buf[j] = acc + bufout * 0.5
                ap_idx[a] = j + 1 if j + 1 < len(buf) else 0
                acc = y
            if p == passes - 1:
                out[i] = samples[i] * (1.0 - wet * 0.5) + acc * wet
    return out


def remove_dc(samples):
    """Tira a média (offset DC). Num loop a média é constante, então a emenda continua perfeita."""
    mean = sum(samples) / len(samples)
    return [s - mean for s in samples]


def normalize(samples, peak):
    biggest = max(abs(s) for s in samples) or 1.0
    k = peak / biggest
    return [s * k for s in samples]


def midi_to_hz(note):
    return 440.0 * 2.0 ** ((note - 69) / 12.0)
