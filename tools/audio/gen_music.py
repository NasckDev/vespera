"""Gera as 3 músicas ambiente do Véspera em assets/audio/music/<id>.wav (16-bit, 44.1 kHz, mono).

  city_day  Lumeira de dia: calma, lá menor/dó maior, 76 BPM, 16 compassos (~50 s), mar ao fundo.
  vespera   a véspera: tensão âmbar em ré menor, 90 BPM, 16 compassos (~43 s), ostinato e pulso grave.
  brecha    a música da cidade tocada AO CONTRÁRIO, abafada (passa-baixa) e com reverb (GDD §6).

Cada faixa é um loop perfeito: as notas são somadas num buffer circular (a cauda do fim entra no
começo), tons contínuos têm um número inteiro de ciclos no loop e filtros/reverb aquecem o estado com
uma volta inteira. O import do Godot liga o loop (edit/loop_mode=2, Forward) em <id>.wav.import.

Só biblioteca padrão, determinístico (seed fixa). Leva alguns minutos em Python puro.
Uso: python tools/audio/gen_music.py [--out assets/audio/music] [--only city_day,vespera,brecha]
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
    biquad,
    lowpass_coefficients,
    midi_to_hz,
    normalize,
    remove_dc,
    reverb,
    write_wav,
)

SEED = 20261020
SR = SAMPLE_RATE
# Pico final das faixas. Os SFX do protótipo (ganho mestre 0.22) têm pico entre 0.013 e 0.069;
# a música fica por baixo deles para não cobrir o combate.
MUSIC_PEAK = 0.06


class Loop:
    """Buffer circular de amostras com utilidades de sequenciamento em compassos/tempos."""

    def __init__(self, bpm, bars, beats_per_bar=4):
        self.beat = 60.0 / bpm
        self.bar = self.beat * beats_per_bar
        self.length = int(round(self.bar * bars * SR))
        self.buf = [0.0] * self.length

    def seconds(self):
        return self.length / SR

    def at(self, bar, beat=0.0):
        return (bar * self.bar + beat * self.beat)

    def add(self, start_s, samples):
        n = self.length
        i0 = int(round(start_s * SR)) % n
        buf = self.buf
        for k, s in enumerate(samples):
            buf[(i0 + k) % n] += s

    def whole_cycles(self, freq):
        """Frequência mais próxima com número inteiro de ciclos no loop (tom contínuo sem clique)."""
        secs = self.seconds()
        return max(1, round(freq * secs)) / secs


# ---------- instrumentos (cada um devolve a lista de amostras da nota, já com o release)

def adsr(i, n_on, attack, decay, sustain, release):
    t = i / SR
    on = n_on / SR
    if t < attack:
        level = t / attack
    elif t < attack + decay:
        level = 1.0 - (1.0 - sustain) * (t - attack) / decay
    else:
        level = sustain
    if t >= on:
        # release a partir do nível em que a nota estava
        if on < attack:
            start = on / attack
        elif on < attack + decay:
            start = 1.0 - (1.0 - sustain) * (on - attack) / decay
        else:
            start = sustain
        r = (t - on) / release
        level = start * max(0.0, 1.0 - r) ** 2
    return level


def tri(freq, dur, vol, attack=0.012, decay=0.35, sustain=0.55, release=0.35, vibrato=0.004):
    n_on = int(dur * SR)
    n = n_on + int(release * SR)
    out = [0.0] * n
    phase = 0.0
    for i in range(n):
        t = i / SR
        vib = 1.0 + vibrato * math.sin(TWO_PI * 5.0 * t) * min(1.0, max(0.0, (t - 0.25) * 3.0))
        phase += freq * vib / SR
        phase -= int(phase)
        w = 4.0 * abs(phase - 0.5) - 1.0
        out[i] = w * vol * adsr(i, n_on, attack, decay, sustain, release)
    return out


def poly_blep(t, dt):
    if t < dt:
        t /= dt
        return t + t - t * t - 1.0
    if t > 1.0 - dt:
        t = (t - 1.0) / dt
        return t * t + t + t + 1.0
    return 0.0


def pulse(freq, dur, vol, duty=0.25, tau=0.22, cutoff=2800.0):
    """Pulso 25% com polyBLEP (sem aliasing áspero), decaimento exponencial e passa-baixa de um polo."""
    n = int((dur + tau * 3.0) * SR)
    out = [0.0] * n
    dt = freq / SR
    phase = 0.0
    lp = 0.0
    k = 1.0 - math.exp(-TWO_PI * cutoff / SR)
    for i in range(n):
        t = i / SR
        w = 1.0 if phase < duty else -1.0
        w += poly_blep(phase, dt)
        p2 = phase - duty
        if p2 < 0.0:
            p2 += 1.0
        w -= poly_blep(p2, dt)
        w -= 2.0 * duty - 1.0  # tira o DC do pulso (média de um pulso 25% é -0.5)
        phase += dt
        phase -= int(phase)
        env = min(1.0, t / 0.004) * math.exp(-t / tau)
        lp += k * (w * env - lp)
        out[i] = lp * vol
    return out


def pad(freq, dur, vol, attack=0.7, release=1.2):
    n_on = int(dur * SR)
    n = n_on + int(release * SR)
    out = [0.0] * n
    p1 = p2 = p3 = 0.0
    f1, f2 = freq * 2 ** (4 / 1200), freq * 2 ** (-4 / 1200)
    for i in range(n):
        p1 += f1 / SR
        p1 -= int(p1)
        p2 += f2 / SR
        p2 -= int(p2)
        p3 += freq / SR
        p3 -= int(p3)
        w = (4.0 * abs(p1 - 0.5) - 1.0) * 0.4 + (4.0 * abs(p2 - 0.5) - 1.0) * 0.4 + math.sin(TWO_PI * p3) * 0.5
        out[i] = w * vol * adsr(i, n_on, attack, 0.5, 0.85, release)
    return out


def bass(freq, dur, vol, release=0.3):
    n_on = int(dur * SR)
    n = n_on + int(release * SR)
    out = [0.0] * n
    phase = 0.0
    for i in range(n):
        phase += freq / SR
        phase -= int(phase)
        w = math.sin(TWO_PI * phase) + 0.22 * math.sin(2.0 * TWO_PI * phase)
        out[i] = w * vol * adsr(i, n_on, 0.025, 0.4, 0.7, release)
    return out


def thump(vol, f0=88.0, f1=44.0, tau=0.2):
    n = int(tau * 5.0 * SR)
    out = [0.0] * n
    phase = 0.0
    for i in range(n):
        t = i / SR
        f = f1 + (f0 - f1) * math.exp(-t / 0.05)
        phase += f / SR
        phase -= int(phase)
        out[i] = math.sin(TWO_PI * phase) * vol * min(1.0, t / 0.003) * math.exp(-t / tau)
    return out


def drone(loop, freq, vol, swell_cycles=4, swell_depth=0.35):
    """Tom contínuo com respiração lenta; frequência e respiração fecham ciclos inteiros no loop."""
    f = loop.whole_cycles(freq)
    n = loop.length
    out = [0.0] * n
    for i in range(n):
        t = i / SR
        swell = 1.0 - swell_depth * 0.5 * (1.0 + math.cos(TWO_PI * swell_cycles * i / n))
        ph = f * t
        out[i] = (math.sin(TWO_PI * ph) + 0.3 * math.sin(TWO_PI * 2.0 * ph) + 0.12 * math.sin(TWO_PI * 3.0 * ph)) * vol * swell
    return out


def sea(loop, rng, vol, swells=6):
    """Mar ao fundo (som diegético da cidade, GDD §6): ruído passa-baixa com ondas lentas."""
    n = loop.length
    raw = [rng.random() * 2.0 - 1.0 for _ in range(n)]
    low = biquad(biquad(raw, lowpass_coefficients(420.0), circular=True), lowpass_coefficients(420.0), circular=True)
    out = [0.0] * n
    for i in range(n):
        wave = 0.5 * (1.0 - math.cos(TWO_PI * swells * i / n))
        out[i] = low[i] * vol * (0.25 + 0.75 * wave * wave)
    return out


def mix_into(loop, samples, gain=1.0):
    buf = loop.buf
    for i, s in enumerate(samples):
        buf[i] += s * gain


# ---------- city_day

CITY_CHORDS = [  # 16 compassos: (baixo, tríade do pad)
    (45, (57, 60, 64)),  # Am
    (41, (53, 57, 60)),  # F
    (48, (55, 60, 64)),  # C
    (43, (55, 59, 62)),  # G
    (45, (57, 60, 64)),  # Am
    (41, (53, 57, 60)),  # F
    (43, (55, 59, 62)),  # G
    (43, (55, 59, 62)),  # G
    (41, (53, 57, 60)),  # F
    (43, (55, 59, 62)),  # G
    (40, (52, 55, 59)),  # Em
    (45, (57, 60, 64)),  # Am
    (41, (53, 57, 60)),  # F
    (43, (55, 59, 62)),  # G
    (48, (55, 60, 64)),  # C
    (40, (52, 56, 59)),  # E (dominante: puxa de volta para o lá menor do início)
]

CITY_MELODY = [  # por compasso: (tempo, duração em tempos, nota MIDI)
    [(0, 2, 76), (2, 1, 72), (3, 1, 74)],
    [(0, 1.5, 72), (1.5, 0.5, 69), (2, 2, 72)],
    [(0, 1, 67), (1, 1, 72), (2, 1, 76), (3, 1, 79)],
    [(0, 3, 74), (3, 1, 71)],
    [(0, 2, 76), (2, 1, 81), (3, 1, 79)],
    [(0, 1.5, 77), (1.5, 0.5, 76), (2, 2, 72)],
    [(0, 1, 74), (1, 1, 76), (2, 1, 74), (3, 1, 71)],
    [(0, 4, 74)],
    [(0, 2, 81), (2, 1, 79), (3, 1, 77)],
    [(0, 2, 79), (2, 2, 74)],
    [(0, 1.5, 76), (1.5, 0.5, 74), (2, 2, 71)],
    [(0, 1, 72), (1, 1, 76), (2, 2, 81)],
    [(0, 1.5, 81), (1.5, 0.5, 79), (2, 1, 77), (3, 1, 76)],
    [(0, 2, 74), (2, 2, 79)],
    [(0, 1, 76), (1, 1, 74), (2, 1, 72), (3, 1, 76)],
    [(0, 2, 71), (2, 1, 68), (3, 1, 71)],
]


def render_city_parts(rng):
    """Devolve (loop só com a música, loop do mar). A Brecha reaproveita a música sem o mar."""
    music = Loop(bpm=76, bars=16)
    for bar, (root, triad) in enumerate(CITY_CHORDS):
        start = music.at(bar)
        for note in triad:
            music.add(start, pad(midi_to_hz(note), music.bar, 0.05))
        music.add(start, bass(midi_to_hz(root), music.beat * 1.9, 0.16))
        music.add(music.at(bar, 2), bass(midi_to_hz(root + 7 if root < 44 else root), music.beat * 1.9, 0.12))
        arp = [triad[0], triad[1], triad[2], triad[0] + 12, triad[2], triad[1], triad[0], triad[1]]
        for k, note in enumerate(arp):
            music.add(music.at(bar, k * 0.5), pulse(midi_to_hz(note + 12), music.beat * 0.5, 0.035))
        for beat, dur, note in CITY_MELODY[bar]:
            music.add(music.at(bar, beat), tri(midi_to_hz(note), music.beat * dur * 0.95, 0.2))
    waves = sea(music, rng, 0.5)
    return music, waves


def build_city_day(rng):
    music, waves = render_city_parts(rng)
    final = Loop(bpm=76, bars=16)
    mix_into(final, music.buf)
    mix_into(final, waves)
    wet = reverb(final.buf, wet=0.22, room=0.8, damp=0.35)
    return normalize(remove_dc(wet), MUSIC_PEAK), music


# ---------- vespera

VESPERA_CHORDS = [  # (fundamental do ostinato, tríade do pad)
    (50, (50, 53, 57)),  # Dm
    (50, (50, 53, 57)),  # Dm
    (46, (46, 50, 53)),  # Bb
    (45, (45, 49, 52)),  # A
    (50, (50, 53, 57)),  # Dm
    (50, (50, 53, 57)),  # Dm
    (43, (43, 46, 50)),  # Gm
    (45, (45, 49, 52)),  # A
    (46, (46, 50, 53)),  # Bb
    (48, (48, 52, 55)),  # C
    (50, (50, 53, 57)),  # Dm
    (45, (45, 49, 52)),  # A
    (43, (43, 46, 50)),  # Gm
    (46, (46, 50, 53)),  # Bb
    (45, (45, 49, 52)),  # A
    (45, (45, 49, 52)),  # A
]

# O motivo de abertura da cidade (mi-dó-ré) reaparece aqui em ré menor (lá-fá-sol).
VESPERA_MELODY = [
    [],
    [(0, 2, 69), (2, 1, 65), (3, 1, 67)],
    [(0, 3, 65), (3, 1, 62)],
    [(0, 2, 64), (2, 2, 61)],
    [],
    [(0, 2, 69), (2, 1, 65), (3, 1, 67)],
    [(0, 2, 70), (2, 1, 69), (3, 1, 67)],
    [(0, 4, 69)],
    [(0, 2, 74), (2, 1, 72), (3, 1, 70)],
    [(0, 2, 72), (2, 2, 67)],
    [(0, 2, 69), (2, 1, 65), (3, 1, 69)],
    [(0, 3, 76), (3, 1, 73)],
    [(0, 2, 74), (2, 2, 70)],
    [(0, 2, 77), (2, 2, 74)],
    [(0, 2, 76), (2, 2, 73)],
    [(0, 4, 69)],
]


def build_vespera(rng):
    loop = Loop(bpm=90, bars=16)
    mix_into(loop, drone(loop, midi_to_hz(38), 0.07, swell_cycles=4))
    for bar, (root, triad) in enumerate(VESPERA_CHORDS):
        start = loop.at(bar)
        for note in triad:
            loop.add(start, pad(midi_to_hz(note + 12), loop.bar, 0.04, attack=1.0, release=1.4))
        # ostinato de relógio: a tarde acabando
        pattern = [root, root + 12, root + 7, root + 12]
        for k in range(8):
            loop.add(loop.at(bar, k * 0.5), pulse(midi_to_hz(pattern[k % 4]), loop.beat * 0.4, 0.05 if k % 2 == 0 else 0.035, tau=0.12, cutoff=1800.0))
        # pulso grave (coração) no 1 e no "e" do 2
        loop.add(start, thump(0.2))
        loop.add(loop.at(bar, 1.5), thump(0.13))
        for beat, dur, note in VESPERA_MELODY[bar]:
            loop.add(loop.at(bar, beat), tri(midi_to_hz(note + 12), loop.beat * dur * 0.95, 0.17, sustain=0.65, release=0.5))
    wet = reverb(loop.buf, wet=0.3, room=0.84, damp=0.3)
    return normalize(remove_dc(wet), MUSIC_PEAK)


# ---------- brecha

def build_brecha(city_music):
    """A música da cidade (sem o mar) invertida no tempo, abafada e com reverb longo."""
    reversed_city = list(reversed(city_music.buf))
    muffled = biquad(reversed_city, lowpass_coefficients(720.0), circular=True)
    muffled = biquad(muffled, lowpass_coefficients(720.0), circular=True)
    wet = reverb(muffled, wet=0.6, room=0.9, damp=0.45)
    return normalize(remove_dc(wet), MUSIC_PEAK)


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    default_out = os.path.normpath(os.path.join(here, "..", "..", "assets", "audio", "music"))
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", default=default_out)
    parser.add_argument("--only", default="city_day,vespera,brecha")
    args = parser.parse_args()
    wanted = set(args.only.split(","))
    os.makedirs(args.out, exist_ok=True)

    rng = random.Random(SEED)
    city, city_music = build_city_day(rng)
    tracks = {"city_day": lambda: city, "vespera": lambda: build_vespera(rng), "brecha": lambda: build_brecha(city_music)}
    for track_id in ("city_day", "vespera", "brecha"):
        if track_id not in wanted:
            continue
        samples = tracks[track_id]()
        path = os.path.join(args.out, track_id + ".wav")
        write_wav(path, samples)
        seam = abs(samples[0] - samples[-1])
        print(f"{track_id:9s} {len(samples) / SR:5.1f}s  emenda {seam:.5f}  -> {path}")


if __name__ == "__main__":
    main()
