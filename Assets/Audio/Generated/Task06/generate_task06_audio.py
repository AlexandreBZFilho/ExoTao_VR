import math
import struct
import wave
import os
import random

SR = 44100

def biquad_lowpass(samples, cutoff=3600.0, sample_rate=44100, Q=0.7071):
    """Filtro IIR passa-baixas Butterworth de 2ª ordem para suavizar agudos e evitar estalos em VR."""
    w0 = 2.0 * math.pi * cutoff / sample_rate
    alpha = math.sin(w0) / (2.0 * Q)
    cos_w0 = math.cos(w0)

    b0 = (1.0 - cos_w0) * 0.5
    b1 = 1.0 - cos_w0
    b2 = (1.0 - cos_w0) * 0.5
    a0 = 1.0 + alpha
    a1 = -2.0 * cos_w0
    a2 = 1.0 - alpha

    y = [0.0] * len(samples)
    x1, x2 = 0.0, 0.0
    y1, y2 = 0.0, 0.0

    for i, x0 in enumerate(samples):
        y0 = (b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0
        y[i] = y0
        x2, x1 = x1, x0
        y2, y1 = y1, y0
    return y

def save_calibrated_wav(filename, samples, target_dbfs=-9.5, cutoff=3600.0, sample_rate=44100):
    """
    Salva áudio 16-bit PCM mono com filtro passa-baixas e normalização milimétrica de pico em dBFS.
    Garante pico <= target_dbfs estrito (<= -8.5 dBFS para conforto acústico total em VR).
    """
    if cutoff and cutoff < sample_rate * 0.48:
        samples = biquad_lowpass(samples, cutoff=cutoff, sample_rate=sample_rate)

    # Remover eventual offset DC
    mean_val = sum(samples) / max(len(samples), 1)
    samples = [s - mean_val for s in samples]

    max_val = max(max(abs(s) for s in samples), 0.00001)
    target_amplitude = 32767.0 * (10.0 ** (target_dbfs / 20.0))
    scale = target_amplitude / max_val

    packed = bytearray()
    for s in samples:
        v = int(max(-32767, min(32767, round(s * scale))))
        packed.extend(struct.pack('<h', v))

    with wave.open(filename, 'wb') as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(sample_rate)
        wf.writeframes(packed)

    actual_peak = max(abs(s * scale) for s in samples)
    actual_dbfs = 20.0 * math.log10(actual_peak / 32767.0) if actual_peak > 0 else -100.0
    print(f"Salvo: {os.path.basename(filename)} | {len(samples)/sample_rate:.3f}s | Pico: {actual_peak:.0f} ({actual_dbfs:.2f} dBFS)")

def generate_insert_click():
    """Som mecânico suave de inserção da fita no deck (guia deslizante + trava magnética suave)."""
    duration = 0.35
    n_samples = int(SR * duration)
    samples = [0.0] * n_samples
    rng = random.Random(42)

    # 1. Deslizamento no trilho plástico (t = 0.0 a 0.09s)
    for i in range(int(SR * 0.09)):
        t = i / SR
        friction = (rng.random() * 2.0 - 1.0) * math.exp(-t * 35.0)
        rail_whine = math.sin(2.0 * math.pi * 950.0 * t) * math.exp(-t * 40.0)
        samples[i] += friction * 0.18 + rail_whine * 0.12

    # 2. Clique de assentamento e travamento magnético (t = 0.08 a 0.22s)
    snap_start = int(SR * 0.08)
    for i in range(snap_start, min(n_samples, snap_start + int(SR * 0.16))):
        dt = (i - snap_start) / SR
        # Tom suave de travamento plástico-metálico (920 Hz e 360 Hz)
        latch = math.sin(2.0 * math.pi * 920.0 * dt) * math.exp(-dt * 65.0)
        body = math.sin(2.0 * math.pi * 360.0 * dt) * math.exp(-dt * 45.0)
        sub = math.sin(2.0 * math.pi * 160.0 * dt) * math.exp(-dt * 30.0)
        clack = (rng.random() * 2.0 - 1.0) * math.exp(-dt * 90.0)
        samples[i] += latch * 0.60 + body * 0.45 + sub * 0.35 + clack * 0.20

    # 3. Engajamento dos pinos de carretel (t = 0.16 a 0.32s)
    pin_start = int(SR * 0.16)
    for i in range(pin_start, min(n_samples, pin_start + int(SR * 0.14))):
        dt = (i - pin_start) / SR
        gear = math.sin(2.0 * math.pi * 640.0 * dt) * math.exp(-dt * 50.0)
        thud = math.sin(2.0 * math.pi * 120.0 * dt) * math.exp(-dt * 35.0)
        samples[i] += gear * 0.25 + thud * 0.30

    return samples

def generate_eject_snap():
    """Som suave de destravamento com mola e liberação mecânica do compartimento."""
    duration = 0.38
    n_samples = int(SR * duration)
    samples = [0.0] * n_samples
    rng = random.Random(1337)

    # 1. Clique tátil do botão físico afundando (t = 0.0 a 0.05s)
    for i in range(int(SR * 0.05)):
        t = i / SR
        btn = math.sin(2.0 * math.pi * 1150.0 * t) * math.exp(-t * 90.0)
        samples[i] += btn * 0.25

    # 2. Destravamento e recuo de mola amortecida (t = 0.04 a 0.24s)
    spring_start = int(SR * 0.04)
    for i in range(spring_start, min(n_samples, spring_start + int(SR * 0.20))):
        dt = (i - spring_start) / SR
        freq = 460.0 * math.exp(-dt * 20.0) + 210.0
        spring = math.sin(2.0 * math.pi * freq * dt) * math.exp(-dt * 30.0)
        unlatch = math.sin(2.0 * math.pi * 1250.0 * dt) * math.exp(-dt * 70.0)
        body = math.sin(2.0 * math.pi * 140.0 * dt) * math.exp(-dt * 28.0)
        samples[i] += spring * 0.55 + unlatch * 0.40 + body * 0.35

    # 3. Batente amortecido do compartimento aberto (t = 0.16 a 0.35s)
    stop_start = int(SR * 0.16)
    for i in range(stop_start, min(n_samples, stop_start + int(SR * 0.16))):
        dt = (i - stop_start) / SR
        catch = math.sin(2.0 * math.pi * 320.0 * dt) * math.exp(-dt * 45.0)
        noise = (rng.random() * 2.0 - 1.0) * math.exp(-dt * 60.0)
        samples[i] += catch * 0.30 + noise * 0.12

    return samples

def generate_lofi_sunset_groove():
    """Trilha Lo-Fi relaxante (8.0s em loop contínuo e acolhedor)."""
    duration = 8.0
    n_samples = int(SR * duration)
    samples = [0.0] * n_samples
    rng = random.Random(777)

    # Progressão: Dm9 -> G13 -> Cmaj9 -> Am9 (2 segundos por acorde)
    chords = [
        [146.83, 174.61, 220.00, 261.63, 293.66], # Dm9
        [98.00, 196.00, 246.94, 293.66, 329.63],  # G13
        [130.81, 164.81, 196.00, 246.94, 293.66], # Cmaj9
        [110.00, 164.81, 220.00, 261.63, 329.63], # Am9
    ]

    for b_idx, chord in enumerate(chords):
        b_start = int(b_idx * 2.0 * SR)
        b_len = int(2.0 * SR)
        for i in range(b_len):
            idx = b_start + i
            if idx >= n_samples: break
            t_bar = i / SR
            
            # E.Piano Rhodes suave
            chord_sum = 0.0
            for note in chord:
                env = math.exp(-t_bar * 1.3) + 0.15 * math.exp(-t_bar * 0.35)
                h1 = math.sin(2.0 * math.pi * note * t_bar)
                h2 = 0.30 * math.sin(2.0 * math.pi * note * 2.0 * t_bar) * math.exp(-t_bar * 2.5)
                chord_sum += (h1 + h2) * env * 0.09
            
            # Baixo elétrico aveludado
            bass_f = chord[0] * 0.5 if chord[0] > 115.0 else chord[0]
            bass_env = math.exp(-t_bar * 1.6) + 0.22
            bass = math.sin(2.0 * math.pi * bass_f * t_bar) * bass_env * 0.24
            samples[idx] += chord_sum + bass

    # Batida Lo-Fi acolhedora (Kick suave em 1 e 3, Snare escovado em 2 e 4)
    for beat in range(8):
        t_beat = beat * 1.0
        b_idx = int(t_beat * SR)
        if beat % 2 == 0:
            # Kick macio
            for k in range(int(SR * 0.30)):
                idx = b_idx + k
                if idx >= n_samples: break
                dt = k / SR
                freq = 95.0 * math.exp(-dt * 22.0) + 42.0
                kick = math.sin(2.0 * math.pi * freq * dt) * math.exp(-dt * 12.0)
                samples[idx] += kick * 0.36
        else:
            # Snare escovado
            for s in range(int(SR * 0.22)):
                idx = b_idx + s
                if idx >= n_samples: break
                dt = s / SR
                noise = (rng.random() * 2.0 - 1.0) * math.exp(-dt * 22.0)
                snap = math.sin(2.0 * math.pi * 280.0 * dt) * math.exp(-dt * 28.0)
                samples[idx] += noise * 0.18 + snap * 0.20

    return samples

def generate_ambient_zen_flow():
    """Trilha sonora zen e imersiva para relaxamento profundo (8.0s em loop com sinos suaves)."""
    duration = 8.0
    n_samples = int(SR * duration)
    samples = [0.0] * n_samples

    # Drone tonal suave em Fá lídio (F2=87.31, C3=130.81, A3=220.00, E4=329.63)
    for i in range(n_samples):
        t = i / SR
        lfo = 0.6 + 0.4 * math.sin(2.0 * math.pi * 0.125 * t)
        d1 = math.sin(2.0 * math.pi * 87.31 * t) * 0.18
        d2 = math.sin(2.0 * math.pi * 130.81 * t) * 0.14
        d3 = math.sin(2.0 * math.pi * 220.00 * t + 0.4) * 0.10
        d4 = math.sin(2.0 * math.pi * 329.63 * t + 0.8) * 0.06
        samples[i] += (d1 + d2 + d3 + d4) * lfo

    # Sinos tibetanos com harmônicos ricos e decaimento calmo
    bell_times = [0.2, 2.0, 3.8, 5.6]
    bell_freqs = [261.63, 329.63, 392.00, 523.25]
    for b_time, b_freq in zip(bell_times, bell_freqs):
        s_idx = int(b_time * SR)
        for i in range(int(SR * 2.6)):
            idx = (s_idx + i) % n_samples
            dt = i / SR
            b1 = math.sin(2.0 * math.pi * b_freq * dt) * math.exp(-dt * 1.6)
            b2 = 0.40 * math.sin(2.0 * math.pi * (b_freq * 2.75) * dt) * math.exp(-dt * 2.2)
            b3 = 0.15 * math.sin(2.0 * math.pi * (b_freq * 5.40) * dt) * math.exp(-dt * 3.4)
            samples[idx] += (b1 + b2 + b3) * 0.20

    return samples

def generate_synthwave_pulse():
    """Trilha Synthwave retrô com pulso cadenciado (8.0s em loop, 120 BPM)."""
    duration = 8.0
    n_samples = int(SR * duration)
    samples = [0.0] * n_samples
    rng = random.Random(999)

    # 120 BPM: cada compasso tem 2.0s
    # Linha de baixo pulsante em semicolcheias (0.125s)
    for step in range(64):
        t_step = step * 0.125
        s_idx = int(t_step * SR)
        f_bass = 55.0 if (step % 2 == 0) else 110.0
        for k in range(int(SR * 0.115)):
            idx = s_idx + k
            if idx >= n_samples: break
            dt = k / SR
            saw = (2.0 * ((f_bass * dt) % 1.0) - 1.0) * math.exp(-dt * 16.0)
            sub = math.sin(2.0 * math.pi * (f_bass * 0.5) * dt) * math.exp(-dt * 12.0)
            samples[idx] += saw * 0.18 + sub * 0.22

    # Bateria vintage (Kick em todas as semínimas, Caixa nos tempos 2 e 4)
    for beat in range(16):
        t_beat = beat * 0.50
        b_idx = int(t_beat * SR)
        for k in range(int(SR * 0.24)):
            idx = b_idx + k
            if idx >= n_samples: break
            dt = k / SR
            freq = 135.0 * math.exp(-dt * 30.0) + 45.0
            kick = math.sin(2.0 * math.pi * freq * dt) * math.exp(-dt * 14.0)
            samples[idx] += kick * 0.34

        if beat % 2 == 1:
            for s in range(int(SR * 0.26)):
                idx = b_idx + s
                if idx >= n_samples: break
                dt = s / SR
                noise = (rng.random() * 2.0 - 1.0) * (1.0 if dt < 0.14 else 0.0) * math.exp(-dt * 9.0)
                body = math.sin(2.0 * math.pi * 210.0 * dt) * math.exp(-dt * 22.0)
                samples[idx] += noise * 0.28 + body * 0.24

    # Arpeggio brilhante
    arp_scale = [220.0, 261.63, 329.63, 440.0]
    for step in range(64):
        f_note = arp_scale[step % 4]
        t_arp = step * 0.125
        a_idx = int(t_arp * SR)
        for i in range(int(SR * 0.10)):
            idx = a_idx + i
            if idx >= n_samples: break
            dt = i / SR
            sqr = (1.0 if ((f_note * dt) % 1.0) < 0.5 else -1.0) * math.exp(-dt * 18.0)
            samples[idx] += sqr * 0.09

    return samples

def generate_luna_cantina():
    """Trilha acústica alegre e acolhedora em estilo Cantina Latina Retro (8.0s em loop)."""
    duration = 8.0
    n_samples = int(SR * duration)
    samples = [0.0] * n_samples
    rng = random.Random(2026)

    # Acordes: G -> D7 -> Em -> C (2.0s por acorde)
    chords = [
        [196.00, 246.94, 293.66, 392.00], # G
        [146.83, 220.00, 293.66, 369.99], # D7
        [164.81, 196.00, 246.94, 329.63], # Em
        [130.81, 164.81, 196.00, 261.63], # C
    ]

    for b_idx, chord in enumerate(chords):
        b_start = int(b_idx * 2.0 * SR)
        for strum in range(4): # 4 batidas por compasso (cada 0.5s)
            s_time = b_start + int(strum * 0.5 * SR)
            for i in range(int(SR * 0.45)):
                idx = s_time + i
                if idx >= n_samples: break
                dt = i / SR
                guitar = 0.0
                for n_idx, note in enumerate(chord):
                    strum_delay = n_idx * 0.015
                    if dt > strum_delay:
                        t_strum = dt - strum_delay
                        # Violão de nylon suave
                        string = math.sin(2.0 * math.pi * note * t_strum) * math.exp(-t_strum * 7.0)
                        guitar += string * 0.10
                samples[idx] += guitar

    # Baixo acústico suave
    for b_idx, chord in enumerate(chords):
        b_start = int(b_idx * 2.0 * SR)
        root = chord[0] * 0.5
        for i in range(int(SR * 1.8)):
            idx = b_start + i
            if idx >= n_samples: break
            dt = i / SR
            bass = math.sin(2.0 * math.pi * root * dt) * math.exp(-dt * 2.0)
            samples[idx] += bass * 0.26

    # Percussão suave de congas
    for beat in range(16):
        t_b = beat * 0.50
        b_idx = int(t_b * SR)
        for k in range(int(SR * 0.18)):
            idx = b_idx + k
            if idx >= n_samples: break
            dt = k / SR
            conga = math.sin(2.0 * math.pi * (220.0 if beat % 2 == 0 else 330.0) * dt) * math.exp(-dt * 25.0)
            shake = (rng.random() * 2.0 - 1.0) * math.exp(-dt * 45.0)
            samples[idx] += conga * 0.16 + shake * 0.08

    return samples

if __name__ == '__main__':
    out_dir = r"D:\Alunos\Alexandre\Projeto IC\Assets\Audio\Generated\Task06"
    os.makedirs(out_dir, exist_ok=True)

    print("--- GERANDO E CALIBRANDO EFEITOS SONOROS (SFX) ---")
    sfx_insert = generate_insert_click()
    save_calibrated_wav(os.path.join(out_dir, "cassette_insert_click.wav"), sfx_insert, target_dbfs=-9.5, cutoff=3600.0)

    sfx_eject = generate_eject_snap()
    save_calibrated_wav(os.path.join(out_dir, "cassette_eject_snap.wav"), sfx_eject, target_dbfs=-9.5, cutoff=3600.0)

    print("\n--- GERANDO E CALIBRANDO FAIXAS MUSICAIS (LOOPS SUAVES) ---")
    lofi = generate_lofi_sunset_groove()
    save_calibrated_wav(os.path.join(out_dir, "lofi_sunset_groove.wav"), lofi, target_dbfs=-9.0, cutoff=3800.0)
    save_calibrated_wav(os.path.join(out_dir, "track_neon_sunset.wav"), lofi, target_dbfs=-9.0, cutoff=3800.0)

    zen = generate_ambient_zen_flow()
    save_calibrated_wav(os.path.join(out_dir, "ambient_zen_flow.wav"), zen, target_dbfs=-9.5, cutoff=3600.0)
    save_calibrated_wav(os.path.join(out_dir, "track_emerald_zen.wav"), zen, target_dbfs=-9.5, cutoff=3600.0)

    synth = generate_synthwave_pulse()
    save_calibrated_wav(os.path.join(out_dir, "synthwave_pulse.wav"), synth, target_dbfs=-9.0, cutoff=3800.0)
    save_calibrated_wav(os.path.join(out_dir, "track_cyber_horizon.wav"), synth, target_dbfs=-9.0, cutoff=3800.0)

    cantina = generate_luna_cantina()
    save_calibrated_wav(os.path.join(out_dir, "track_luna_cantina.wav"), cantina, target_dbfs=-9.0, cutoff=3800.0)

    print("\nTodos os áudios gerados e calibrados com sucesso com picos rigorosamente <= -8.5 dBFS!")
