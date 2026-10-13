#!/usr/bin/env python3
"""Synthesises AVABurgerT's sound effects and tunes into ../src/AVABurgerT/Sounds as 16-bit mono WAV.

Nothing is sampled from any arcade machine: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVABurgerT", "Sounds")
RNG = random.Random(1984)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def main():
    os.makedirs(OUT, exist_ok=True)
    write("Step.wav", tone(0.03, 700, 500, "pulse", release=2), 0.22)
    # A layer sagging underfoot: a soft squeak.
    write("Squeak.wav", tone(0.06, 900, 1300, "tri", release=1.2), 0.3)
    write("Drop.wav", tone(0.35, 800, 200, "square", release=0.7), 0.4)
    write("Thud.wav", cat(tone(0.04, 140, 90, "square"), tone(0.12, 200, 60, "noise", release=1.2)), 0.5)
    write("Plate.wav", cat(tone(0.03, 2400, 2200, "tri"), tone(0.25, 2200, 1800, "tri", release=0.6)), 0.45)
    write("BurgerDone.wav", cat(*[tone(0.07, hz(n), kind="pulse", release=0.5) for n in ["C6", "E6", "G6", "C7", "G6", "C7"]]), 0.45)
    write("Squash.wav", cat(tone(0.05, 600, 200, "square"), tone(0.25, 300, 50, "noise", release=0.9)), 0.6)
    write("Pepper.wav", tone(0.25, 6000, 3000, "noise", attack=0.01, release=0.8), 0.35)
    write("Stunned.wav", cat(*[tone(0.05, f, kind="tri", release=0.5) for f in [1800, 1500, 1800, 1500]]), 0.35)
    write("NoPepper.wav", tone(0.15, 200, 160, "square", release=0.6), 0.35)
    write("Treat.wav", cat(*[tone(0.06, hz(n), kind="square", release=0.4) for n in ["G5", "C6", "E6", "G6"]]), 0.45)
    write("TreatShows.wav", cat(tone(0.1, hz("E6"), kind="tri"), tone(0.1, hz("G6"), kind="tri"), tone(0.2, hz("C7"), kind="tri")), 0.45)
    write("ExtraLife.wav", cat(*[tone(0.07, hz(n), kind="square", release=0.4) for n in ["F5", "A5", "C6", "F6"] * 3]), 0.45)

    write("Death.wav", tune(150,
        [("C6", 0.5), ("B5", 0.5), ("Bb5", 0.5), ("A5", 0.5), ("Ab5", 0.5), ("G5", 1.5)],
        [("C3", 1), ("G2", 1), ("C2", 2)], kind="square"), 0.6)

    # The kitchen music: a busy, bustling tune in F (new), 8 bars, loops.
    lead = [("F5", 0.5), ("A5", 0.5), ("C6", 0.5), ("A5", 0.5), ("Bb5", 0.5), ("G5", 0.5), ("E5", 0.5), ("C5", 0.5),
            ("D5", 0.5), ("F5", 0.5), ("A5", 0.5), ("F5", 0.5), ("G5", 1), ("C5", 1),
            ("F5", 0.5), ("A5", 0.5), ("C6", 0.5), ("D6", 0.5), ("C6", 0.5), ("Bb5", 0.5), ("A5", 0.5), ("G5", 0.5),
            ("A5", 0.5), ("G5", 0.5), ("E5", 0.5), ("G5", 0.5), ("F5", 2)]
    bass = [(n, 0.5) for bar in ["F2", "C3", "Bb2", "C3", "F2", "Bb2", "C3", "F2"] for n in [bar, bar[:-1] + str(int(bar[-1]) + 1)] * 2]
    write("Music.wav", tune(156, lead, bass), 0.5)

    write("TitleTune.wav", tune(144,
        [("C5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 0.5), ("A5", 1), ("F5", 1), ("G5", 0.5), ("E5", 0.5), ("C5", 0.5), ("E5", 0.5), ("D5", 2),
         ("C5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 0.5), ("D6", 1), ("B5", 1), ("C6", 0.5), ("G5", 0.5), ("E5", 0.5), ("D5", 0.5), ("C5", 2)],
        [(n, 1) for n in ["C3", "G2", "F2", "F2", "C3", "G2", "G2", "G2", "C3", "G2", "G2", "G2", "C3", "G2", "C3", "C3"]]), 0.6)
    write("StartTune.wav", tune(170,
        [("G5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 0.5), ("E6", 0.5), ("D6", 0.5), ("C6", 1.5)],
        [("C3", 1), ("G2", 1), ("C3", 2)]), 0.6)
    write("LevelClear.wav", tune(160,
        [("C6", 0.5), ("G5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 0.5), ("E6", 0.5), ("G6", 0.5), ("E6", 0.5), ("C7", 2)],
        [("C3", 1), ("E3", 1), ("G3", 1), ("C4", 1), ("C3", 2)]), 0.6)
    write("GameOver.wav", tune(92,
        [("G5", 1), ("E5", 1), ("C5", 1), ("D5", 1), ("C5", 2), ("-", 1)],
        [("C3", 2), ("A2", 2), ("G2", 2), ("C2", 1)], kind="tri"), 0.65)
    print("sounds written to", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
