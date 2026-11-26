"""
TASK-16 (retoque da estante) - sons dos trofeus, suaves e curtos.

  trophy_pickup_shimmer.wav  brilho de sino suave ao pegar (0,45 s)
  trophy_unlock_fanfare.wav  fanfarra curta de conquista (1,8 s)
  crown_equip_royal.wav      coroacao do Drone (1,6 s)
  trophy_place_click.wav     clique suave de metal no pedestal (0,15 s) - NOVO

Regras do projeto: pico <= -6 dBFS (usamos -9 a -12), passa-baixa ~5-6 kHz, sem nada estridente.
Uso: python generate_task16_rework_audio.py
"""
import math
import os
import random
import struct
import wave

SR = 44100
OUT = os.path.dirname(os.path.abspath(__file__))


def lowpass(x, fc, passes=1):
    a = (1.0 / SR) / (1.0 / (2 * math.pi * fc) + 1.0 / SR)
    y = list(x)
    for _ in range(passes):
        acc = 0.0
        for i, v in enumerate(y):
            acc += a * (v - acc)
            y[i] = acc
    return y


def fade(x, fin, fout):
    n = len(x)
    a, b = max(1, int(fin * SR)), max(1, int(fout * SR))
    for i in range(min(a, n)):
        x[i] *= i / a
    for i in range(min(b, n)):
        x[n - 1 - i] *= i / b
    return x


def write(name, x, peak_db):
    pk = max(abs(v) for v in x) or 1.0
    k = 32767.0 * 10 ** (peak_db / 20.0) / pk
    with wave.open(os.path.join(OUT, name), "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", max(-32768, min(32767, int(v * k)))) for v in x))
    print(f"{name}: {len(x) / SR:.2f} s, pico {peak_db:.1f} dBFS")


def tone(buf, start, dur, f, amp, attack=0.008, decay=4.0, harmonics=((1, 1.0),), vibrato=0.0):
    s0 = int(start * SR)
    n = int(dur * SR)
    ph = [0.0] * len(harmonics)
    for i in range(n):
        if s0 + i >= len(buf):
            break
        t = i / SR
        env = min(1.0, t / attack) * math.exp(-t * decay)
        fv = f * (1.0 + vibrato * math.sin(2 * math.pi * 5.5 * t))
        v = 0.0
        for h, (mult, ha) in enumerate(harmonics):
            ph[h] += 2 * math.pi * fv * mult / SR
            v += ha * math.sin(ph[h])
        buf[s0 + i] += v * env * amp


def pickup_shimmer():
    x = [0.0] * int(0.45 * SR)
    for f, a, d in ((660.0, 1.0, 7.0), (990.0, 0.45, 9.0), (1320.0, 0.25, 12.0), (1980.0, 0.10, 16.0)):
        tone(x, 0.0, 0.45, f, a, attack=0.004, decay=d)
    tone(x, 0.06, 0.35, 1320.0 * 1.25, 0.12, attack=0.01, decay=10.0)    # brilho atrasado (terça maior)
    return fade(lowpass(x, 5000.0), 0.002, 0.08)


def unlock_fanfare():
    x = [0.0] * int(1.8 * SR)
    brass = ((1, 1.0), (2, 0.35), (3, 0.15), (4, 0.05))
    for i, f in enumerate((523.25, 659.25, 783.99)):
        tone(x, i * 0.14, 0.5, f, 0.5, attack=0.015, decay=3.5, harmonics=brass)
    for f in (523.25, 659.25, 783.99, 1046.5):                              # acorde final
        tone(x, 0.44, 1.36, f, 0.32, attack=0.03, decay=1.9, harmonics=brass, vibrato=0.004)
    for i, f in enumerate((1567.98, 2093.0)):
        tone(x, 0.46 + i * 0.07, 0.8, f, 0.06, attack=0.003, decay=6.0)    # sininho discreto
    return fade(lowpass(x, 5200.0), 0.003, 0.25)


def crown_royal():
    x = [0.0] * int(1.6 * SR)
    soft = ((1, 1.0), (2, 0.25), (3, 0.08))
    for i, f in enumerate((392.0, 493.88, 587.33, 783.99)):
        tone(x, i * 0.11, 0.45, f, 0.45, attack=0.012, decay=4.0, harmonics=soft)
    for f in (392.0, 493.88, 587.33, 783.99):
        tone(x, 0.44, 1.16, f, 0.3, attack=0.04, decay=2.2, harmonics=soft, vibrato=0.003)
    random.seed(16)
    for k in range(6):                                                       # estrelinhas
        tone(x, 0.5 + k * 0.09, 0.3, random.choice((1174.66, 1318.51, 1567.98)), 0.05, attack=0.003, decay=9.0)
    return fade(lowpass(x, 5000.0), 0.003, 0.25)


def place_click():
    x = [0.0] * int(0.15 * SR)
    tone(x, 0.0, 0.15, 1750.0, 0.35, attack=0.0008, decay=55.0)
    tone(x, 0.0, 0.15, 2650.0, 0.18, attack=0.0008, decay=75.0)
    tone(x, 0.0, 0.15, 310.0, 0.6, attack=0.0015, decay=38.0)              # 'toc' no feltro
    random.seed(161)
    for i in range(int(0.004 * SR)):
        x[i] += (random.random() * 2 - 1) * 0.25 * (1 - i / (0.004 * SR))
    return fade(lowpass(x, 4500.0, 2), 0.0005, 0.03)


if __name__ == "__main__":
    write("trophy_pickup_shimmer.wav", pickup_shimmer(), -12.0)
    write("trophy_unlock_fanfare.wav", unlock_fanfare(), -9.0)
    write("crown_equip_royal.wav", crown_royal(), -10.0)
    write("trophy_place_click.wav", place_click(), -12.0)
