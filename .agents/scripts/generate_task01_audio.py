"""
Sintetizador e Calibrador de Áudio VR-Safe — TASK-01 Core Audio
Padrão Clínico para Reabilitação Motora (VR Rehab Gamification)

Critérios de Conformidade:
1. Teto de pico: máximo de -8.0 dBFS (sem estalos nem volumes agressivos).
2. Suavidade: filtro passa-baixas (Butterworth 2ª ordem fc <= 3500 Hz).
3. Transições suaves: fade-in e fade-out suaves sem DC offset ou clicks (start/end = 0.0).
4. Áudio 16-bit 44.1 kHz Mono.
"""

import math
import struct
import wave
import os

SAMPLE_RATE = 44100

def apply_butterworth_lowpass(samples, cutoff_freq, sample_rate=SAMPLE_RATE, q=0.7071):
    w0 = 2.0 * math.pi * cutoff_freq / sample_rate
    cos_w0 = math.cos(w0)
    sin_w0 = math.sin(w0)
    alpha = sin_w0 / (2.0 * q)
    
    b0 = (1.0 - cos_w0) / 2.0
    b1 = 1.0 - cos_w0
    b2 = (1.0 - cos_w0) / 2.0
    a0 = 1.0 + alpha
    a1 = -2.0 * cos_w0
    a2 = 1.0 - alpha
    
    # Normalização
    b0 /= a0
    b1 /= a0
    b2 /= a0
    a1 /= a0
    a2 /= a0
    
    filtered = []
    x1 = x2 = y1 = y2 = 0.0
    for x in samples:
        y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1 = x1, x
        y2, y1 = y1, y
        filtered.append(y)
    return filtered

def normalize_to_peak_db(samples, target_peak_db=-8.5):
    max_amp = max(abs(s) for s in samples) if samples else 0
    if max_amp == 0:
        return samples
    target_amp = 10.0 ** (target_peak_db / 20.0)
    gain = target_amp / max_amp
    return [s * gain for s in samples]

def apply_fade(samples, fade_in_samples, fade_out_samples):
    n = len(samples)
    out = []
    for i in range(n):
        s = samples[i]
        if i < fade_in_samples:
            w = 0.5 * (1.0 - math.cos(math.pi * i / fade_in_samples))
            s *= w
        elif i >= n - fade_out_samples:
            rem = n - 1 - i
            w = 0.5 * (1.0 - math.cos(math.pi * rem / fade_out_samples))
            s *= w
        out.append(s)
    return out

def write_wav_file(path, samples, sample_rate=SAMPLE_RATE):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, 'wb') as wf:
        wf.setnchannels(1)  # Mono
        wf.setsampwidth(2)  # 16-bit
        wf.setframerate(sample_rate)
        
        packed = bytearray()
        for s in samples:
            clamped = max(-1.0, min(1.0, s))
            val = int(clamped * 32767.0)
            packed.extend(struct.pack('<h', val))
        wf.writeframes(packed)
    print(f"Salvo WAV: {path} ({len(samples)} amostras, {len(samples)/sample_rate:.3f}s)")

# --- 1. UI Hover Soft ---
def generate_ui_hover():
    duration = 0.16
    n = int(SAMPLE_RATE * duration)
    samples = []
    phase1 = phase2 = phase3 = 0.0
    for i in range(n):
        t = i / SAMPLE_RATE
        progress = t / duration
        # Envelope em sino suave com decaimento macio
        env = math.sin(math.pi * (progress ** 0.6)) * math.exp(-progress * 5.0)
        # Glide tonal sutil: 520 Hz -> 640 Hz
        f1 = 520.0 + 120.0 * progress
        f2 = f1 * 0.5  # Sub-harmônico quente
        f3 = f1 * 1.5  # Harmônico sutil
        phase1 += 2.0 * math.pi * f1 / SAMPLE_RATE
        phase2 += 2.0 * math.pi * f2 / SAMPLE_RATE
        phase3 += 2.0 * math.pi * f3 / SAMPLE_RATE
        
        s = (0.75 * math.sin(phase1) + 0.35 * math.sin(phase2) + 0.15 * math.sin(phase3)) * env
        samples.append(s)
        
    samples = apply_butterworth_lowpass(samples, 2800)
    samples = apply_fade(samples, int(SAMPLE_RATE * 0.008), int(SAMPLE_RATE * 0.02))
    samples = normalize_to_peak_db(samples, target_peak_db=-9.5)
    return samples

# --- 2. Grab Pop ---
def generate_grab_pop():
    duration = 0.12
    n = int(SAMPLE_RATE * duration)
    samples = []
    phase1 = phase2 = 0.0
    for i in range(n):
        t = i / SAMPLE_RATE
        progress = t / duration
        # Pluck tátil elástico
        env = (progress ** 0.4) * math.exp(-progress * 14.0) * 4.2
        # Transição de frequência ágil (200 Hz -> 360 Hz)
        f0 = 200.0 + 160.0 * (progress ** 0.5)
        phase1 += 2.0 * math.pi * f0 / SAMPLE_RATE
        phase2 += 2.0 * math.pi * (f0 * 1.8) / SAMPLE_RATE
        
        # Ressonância de corpo de madeira a 260 Hz
        thump = math.sin(2.0 * math.pi * 260.0 * t) * math.exp(-t * 45.0) * 0.4
        
        s = (math.sin(phase1) + 0.25 * math.sin(phase2)) * env + thump
        samples.append(s)
        
    samples = apply_butterworth_lowpass(samples, 3000)
    samples = apply_fade(samples, int(SAMPLE_RATE * 0.005), int(SAMPLE_RATE * 0.02))
    samples = normalize_to_peak_db(samples, target_peak_db=-8.5)
    return samples

# --- 3. Release Woosh ---
def generate_release_woosh():
    duration = 0.25
    n = int(SAMPLE_RATE * duration)
    samples = []
    phase = 0.0
    for i in range(n):
        t = i / SAMPLE_RATE
        progress = t / duration
        # Swoosh aveludado e suave
        env = math.sin(math.pi * progress) ** 1.5
        # Descida de tom: 380 Hz -> 140 Hz
        f0 = 380.0 - 240.0 * (progress ** 0.8)
        phase += 2.0 * math.pi * f0 / SAMPLE_RATE
        
        # Ar suave filtrado
        air = (math.sin(2.0 * math.pi * 280.0 * t) * 0.3 +
               math.sin(2.0 * math.pi * 420.0 * t) * 0.2 +
               math.sin(2.0 * math.pi * 590.0 * t) * 0.15) * math.sin(2.0 * math.pi * 47.0 * t)
        
        s = (0.7 * math.sin(phase) + 0.3 * air) * env
        samples.append(s)
        
    samples = apply_butterworth_lowpass(samples, 2200)
    samples = apply_fade(samples, int(SAMPLE_RATE * 0.015), int(SAMPLE_RATE * 0.03))
    samples = normalize_to_peak_db(samples, target_peak_db=-9.5)
    return samples

# --- 4. Button Click Mech ---
def generate_button_click():
    duration = 0.08
    n = int(SAMPLE_RATE * duration)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        # Clique mecânico amortecido (microswitch de alta precisão)
        thump = math.sin(2.0 * math.pi * 160.0 * t) * math.exp(-t * 70.0) * 0.6
        click1 = math.sin(2.0 * math.pi * 950.0 * t) * math.exp(-t * 120.0) * 0.5
        click2 = math.sin(2.0 * math.pi * 1500.0 * t) * math.exp(-t * 160.0) * 0.25
        
        s = thump + click1 + click2
        samples.append(s)
        
    samples = apply_butterworth_lowpass(samples, 3200)
    samples = apply_fade(samples, int(SAMPLE_RATE * 0.003), int(SAMPLE_RATE * 0.015))
    samples = normalize_to_peak_db(samples, target_peak_db=-8.5)
    return samples

# --- 5. Socket Snap ---
def generate_socket_snap():
    duration = 0.20
    n = int(SAMPLE_RATE * duration)
    samples = []
    phase = 0.0
    for i in range(n):
        t = i / SAMPLE_RATE
        # Duas etapas: atração magnética rápida (0-30ms) + trava de berço (30-200ms)
        if t < 0.03:
            p_in = t / 0.03
            f_in = 220.0 + 200.0 * p_in
            phase += 2.0 * math.pi * f_in / SAMPLE_RATE
            s = 0.4 * math.sin(phase) * (p_in ** 2.0)
        else:
            t_lock = t - 0.03
            body = (0.65 * math.sin(2.0 * math.pi * 280.0 * t_lock) +
                    0.35 * math.sin(2.0 * math.pi * 560.0 * t_lock) +
                    0.20 * math.sin(2.0 * math.pi * 840.0 * t_lock)) * math.exp(-t_lock * 22.0)
            click = math.sin(2.0 * math.pi * 1200.0 * t_lock) * math.exp(-t_lock * 80.0) * 0.35
            s = body + click
        samples.append(s)
        
    samples = apply_butterworth_lowpass(samples, 2900)
    samples = apply_fade(samples, int(SAMPLE_RATE * 0.004), int(SAMPLE_RATE * 0.025))
    samples = normalize_to_peak_db(samples, target_peak_db=-8.5)
    return samples

# --- 6. Success Chime ---
def generate_success_chime():
    duration = 0.85
    n = int(SAMPLE_RATE * duration)
    samples = []
    
    # Acorde celestial caloroso e edificante (Fá Maior estendido)
    notes = [
        {"freq": 349.23, "delay": 0.00, "weight": 0.35, "decay": 4.0},
        {"freq": 698.46, "delay": 0.00, "weight": 0.60, "decay": 4.5},
        {"freq": 880.00, "delay": 0.03, "weight": 0.55, "decay": 4.5},
        {"freq": 1046.50, "delay": 0.06, "weight": 0.50, "decay": 5.0},
        {"freq": 1396.91, "delay": 0.09, "weight": 0.20, "decay": 6.0},
    ]
    
    for i in range(n):
        t = i / SAMPLE_RATE
        s = 0.0
        for note in notes:
            if t >= note["delay"]:
                t_rel = t - note["delay"]
                env = (min(1.0, t_rel / 0.006) ** 0.8) * math.exp(-t_rel * note["decay"])
                f = note["freq"]
                val = math.sin(2.0 * math.pi * f * t_rel) + 0.25 * math.sin(2.0 * math.pi * (f * 2.0) * t_rel)
                s += val * env * note["weight"]
        samples.append(s)
        
    samples = apply_butterworth_lowpass(samples, 3500)
    samples = apply_fade(samples, int(SAMPLE_RATE * 0.005), int(SAMPLE_RATE * 0.04))
    samples = normalize_to_peak_db(samples, target_peak_db=-8.5)
    return samples

def audit_clip(samples, name):
    peak = max(abs(s) for s in samples) if samples else 0
    peak_db = 20 * math.log10(peak) if peak > 0 else -999.0
    rms = math.sqrt(sum(s**2 for s in samples) / len(samples))
    rms_db = 20 * math.log10(rms) if rms > 0 else -999.0
    
    start_val = abs(samples[0])
    end_val = abs(samples[-1])
    diffs = [abs(samples[i] - samples[i-1]) for i in range(1, len(samples))]
    max_step = max(diffs) if diffs else 0
    duration = len(samples) / SAMPLE_RATE

    print(f"\nAUDITORIA: {name}")
    print(f"  Duração: {duration:.3f}s ({len(samples)} frames)")
    print(f"  Pico: {peak:.4f} ({peak_db:.2f} dBFS) -> {'PASS' if peak_db <= -8.0 else 'FAIL'}")
    print(f"  RMS:  {rms:.4f} ({rms_db:.2f} dBFS)")
    print(f"  Zero-Offset: Início={start_val:.6f}, Fim={end_val:.6f} -> {'PASS' if start_val < 0.001 and end_val < 0.001 else 'FAIL'}")
    print(f"  Passo Máximo Adjacente: {max_step:.4f}")
    
    assert peak_db <= -8.0, f"Pico {peak_db} excede teto de -8.0 dBFS!"
    assert start_val < 0.001 and end_val < 0.001, "Presença de click nos extremos!"

def build_all(output_dir="Assets/Audio/Generated/Core"):
    generators = {
        "ui_hover_soft.wav": generate_ui_hover,
        "grab_pop.wav": generate_grab_pop,
        "release_woosh.wav": generate_release_woosh,
        "button_click_mech.wav": generate_button_click,
        "socket_snap.wav": generate_socket_snap,
        "success_chime.wav": generate_success_chime,
    }
    
    print(f"Gerando áudios para: {output_dir}")
    for fname, gen_fn in generators.items():
        samples = gen_fn()
        audit_clip(samples, fname)
        out_path = os.path.join(output_dir, fname)
        write_wav_file(out_path, samples)
    print("\nTODOS OS 6 ÁUDIOS CORE SINTETIZADOS, CALIBRADOS E APROVADOS (VR-SAFE)!")

if __name__ == "__main__":
    build_all()
