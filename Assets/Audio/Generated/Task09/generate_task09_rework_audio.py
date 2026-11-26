"""
TASK-09 (rework) - sons dos fogos e do bobblehead.

NAO gera nem altera os sons da alavanca de clima (lever_click_notch.wav, atmosphere_transition_whoosh.wav),
que estao aprovados. Gera somente:
  fireworks_launch_whistle.wav   assobio curto subindo de tom (~0.6 s)
  fireworks_burst_explosion.wav  estouro macio + crepitar curto (~1.2 s), sem ataque assustador
  bobblehead_spring_boing.wav    mola curta (~0.35 s)
  bobblehead_spring_soft.wav     mola leve (~0.2 s), toca quando a cabeca cruza o centro
  bobblehead_limit_tock.wav      batida seca da cabeca no limite (~0.1 s)

Regras do projeto: pico <= -6 dBFS (usamos -9 a -12), passa-baixa ~6 kHz, sem timbre estridente.
Uso: python generate_task09_rework_audio.py
"""
import math
import os
import random
import struct
import wave

SAMPLE_RATE = 44100
OUT_DIR = os.path.dirname(os.path.abspath(__file__))


def lowpass(samples, cutoff_hz, passes=1):
    """Passa-baixa de 1 polo aplicado 'passes' vezes (6 dB/oitava por passada)."""
    dt = 1.0 / SAMPLE_RATE
    rc = 1.0 / (2.0 * math.pi * cutoff_hz)
    a = dt / (rc + dt)
    out = list(samples)
    for _ in range(passes):
        y = 0.0
        for i, x in enumerate(out):
            y += a * (x - y)
            out[i] = y
    return out


def highpass(samples, cutoff_hz):
    """Passa-alta de 1 polo (tira o DC e o grave embolado do ruido)."""
    dt = 1.0 / SAMPLE_RATE
    rc = 1.0 / (2.0 * math.pi * cutoff_hz)
    a = rc / (rc + dt)
    out = [0.0] * len(samples)
    prev_x = 0.0
    y = 0.0
    for i, x in enumerate(samples):
        y = a * (y + x - prev_x)
        prev_x = x
        out[i] = y
    return out


def fade_edges(samples, fade_in_s=0.004, fade_out_s=0.03):
    n = len(samples)
    fi = max(1, int(SAMPLE_RATE * fade_in_s))
    fo = max(1, int(SAMPLE_RATE * fade_out_s))
    for i in range(min(fi, n)):
        samples[i] *= i / fi
    for i in range(min(fo, n)):
        samples[n - 1 - i] *= i / fo
    return samples


def write_wav(name, samples, peak_dbfs):
    path = os.path.join(OUT_DIR, name)
    peak = max(abs(s) for s in samples) or 1.0
    scale = 32767.0 * (10.0 ** (peak_dbfs / 20.0)) / peak
    frames = bytearray()
    for s in samples:
        frames.extend(struct.pack('<h', max(-32768, min(32767, int(s * scale)))))
    with wave.open(path, 'w') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SAMPLE_RATE)
        w.writeframes(frames)
    print(f"{name}: {len(samples) / SAMPLE_RATE:.2f} s, pico {peak_dbfs:.1f} dBFS")


def synth_whistle():
    """Assobio de foguete: tom sobe de 620 Hz a 1350 Hz (nada agudo demais) + sopro de ar."""
    random.seed(901)
    dur = 0.62
    n = int(SAMPLE_RATE * dur)
    tone = [0.0] * n
    air = [0.0] * n
    phase = 0.0
    for i in range(n):
        t = i / SAMPLE_RATE
        p = t / dur
        f = 620.0 + 730.0 * (1.0 - (1.0 - p) ** 2)       # sobe rapido e assenta
        f *= 1.0 + 0.012 * math.sin(2.0 * math.pi * 23.0 * t)  # leve vibrato
        phase += 2.0 * math.pi * f / SAMPLE_RATE
        env = min(1.0, t / 0.06) * (1.0 - p) ** 0.6
        tone[i] = (math.sin(phase) + 0.18 * math.sin(2.0 * phase)) * env * 0.55
        air[i] = (random.random() * 2.0 - 1.0) * env * 0.35
    air = highpass(lowpass(air, 2600.0, 2), 300.0)
    mix = [tone[i] + air[i] for i in range(n)]
    mix = lowpass(mix, 6000.0)
    return fade_edges(mix, 0.01, 0.06)


def synth_burst():
    """Estouro macio: 'pum' grave com ataque de 10 ms (nao assusta) + nuvem de ruido curta + crepitar."""
    random.seed(902)
    dur = 1.25
    n = int(SAMPLE_RATE * dur)
    out = [0.0] * n

    # 1. 'Pum' grave: seno de 95 Hz caindo para 55 Hz
    phase = 0.0
    for i in range(n):
        t = i / SAMPLE_RATE
        f = 55.0 + 40.0 * math.exp(-t * 9.0)
        phase += 2.0 * math.pi * f / SAMPLE_RATE
        env = min(1.0, t / 0.010) * math.exp(-t * 7.5)
        out[i] += math.sin(phase) * env * 0.9

    # 2. Nuvem de ruido abafada (como um 'fuump' distante)
    cloud = [0.0] * n
    for i in range(n):
        t = i / SAMPLE_RATE
        env = min(1.0, t / 0.012) * math.exp(-t * 6.0)
        cloud[i] = (random.random() * 2.0 - 1.0) * env
    cloud = lowpass(cloud, 1200.0, 2)
    for i in range(n):
        out[i] += cloud[i] * 1.6

    # 3. Crepitar: estalinhos curtos e esparsos, cada vez mais fracos
    crackle = [0.0] * n
    t_click = 0.22
    while t_click < 1.12:
        start = int(t_click * SAMPLE_RATE)
        amp = 0.55 * math.exp(-(t_click - 0.22) * 2.2) * (0.6 + 0.4 * random.random())
        length = int(SAMPLE_RATE * (0.002 + 0.003 * random.random()))
        for k in range(length):
            if start + k >= n:
                break
            crackle[start + k] += (random.random() * 2.0 - 1.0) * amp * math.exp(-k / (length * 0.35))
        t_click += 0.012 + 0.05 * random.random()
    crackle = highpass(lowpass(crackle, 4800.0, 2), 700.0)
    for i in range(n):
        out[i] += crackle[i] * 1.4

    out = lowpass(out, 6000.0)
    return fade_edges(out, 0.006, 0.12)


def boing(dur, base_hz, wobble, decay, seed):
    """Mola: tom com vibrato largo que decai (o 'boinnng' classico), sem harmonicos agudos."""
    random.seed(seed)
    n = int(SAMPLE_RATE * dur)
    out = [0.0] * n
    phase = 0.0
    for i in range(n):
        t = i / SAMPLE_RATE
        f = base_hz * (1.0 + wobble * math.sin(2.0 * math.pi * 17.0 * t) * math.exp(-t * 5.0))
        f *= 1.0 + 0.10 * math.exp(-t * 25.0)   # pequeno 'tuim' no ataque
        phase += 2.0 * math.pi * f / SAMPLE_RATE
        env = min(1.0, t / 0.004) * math.exp(-t * decay)
        out[i] = (math.sin(phase) + 0.25 * math.sin(2.0 * phase) + 0.08 * math.sin(3.0 * phase)) * env
    out = lowpass(out, 5500.0)
    return fade_edges(out, 0.003, 0.04)


def synth_tock():
    """Batida seca de plastico/madeira: dois modos amortecidos (720 e 1180 Hz) + clique abafado."""
    random.seed(905)
    dur = 0.11
    n = int(SAMPLE_RATE * dur)
    out = [0.0] * n
    for i in range(n):
        t = i / SAMPLE_RATE
        env = min(1.0, t / 0.0015)
        out[i] = env * (math.sin(2.0 * math.pi * 720.0 * t) * math.exp(-t * 55.0)
                        + 0.45 * math.sin(2.0 * math.pi * 1180.0 * t) * math.exp(-t * 80.0)
                        + 0.6 * math.sin(2.0 * math.pi * 240.0 * t) * math.exp(-t * 40.0))
    click = [(random.random() * 2.0 - 1.0) * math.exp(-i / (SAMPLE_RATE * 0.004)) for i in range(n)]
    click = lowpass(click, 3000.0, 2)
    for i in range(n):
        out[i] += click[i] * 0.5
    out = lowpass(out, 6000.0)
    return fade_edges(out, 0.001, 0.02)


if __name__ == '__main__':
    write_wav("fireworks_launch_whistle.wav", synth_whistle(), -11.0)
    write_wav("fireworks_burst_explosion.wav", synth_burst(), -9.0)
    write_wav("bobblehead_spring_boing.wav", boing(0.36, 240.0, 0.30, 9.0, 903), -9.0)
    write_wav("bobblehead_spring_soft.wav", boing(0.20, 330.0, 0.18, 16.0, 904), -13.0)
    write_wav("bobblehead_limit_tock.wav", synth_tock(), -10.0)
