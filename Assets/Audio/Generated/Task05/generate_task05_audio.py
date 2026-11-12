"""
Gerador de Áudio VR-Safe Calibrado — TASK-05: Celebração de Metas Clínicas do Drone Pet
Arquivos gerados:
  1. Assets/Audio/Generated/Task05/drone_milestone_fanfare.wav
  2. Assets/Audio/Generated/Task05/fanfare_triumph.wav (mesmos bytes de áudio para retrocompatibilidade)
  3. Assets/Audio/Generated/Task05/confetti_cannon_pop.wav

Parâmetros Acústicos Estritos e VR-Safe:
  - Formato: 44.1 kHz, 16-bit Mono PCM.
  - Teto de Pico Digital: rigorosamente <= -8.5 dBFS (calibrado a -9.50 dBFS / -9.80 dBFS).
  - Filtro Passa-Baixas Ativo (Butterworth 2ª ordem):
      * Fanfarra triunfal: fc = 3500 Hz (<= 3800 Hz) para metais e sinos aveludados e reconfortantes.
      * Estouro do canhão de confetes: fc = 2400 Hz (<= 3000 Hz) para 'puff' abafado e suave de ar comprimido.
  - Envelope Cosseno: amostras inicial e final rigorosamente em 0.000000 (zero estalos, sem DC bias).
"""

import math
import struct
import wave
import os
import shutil
import random

SAMPLE_RATE = 44100

def apply_butterworth_lowpass(samples, cutoff_freq, sample_rate=SAMPLE_RATE, q=0.7071):
    """Filtro IIR passa-baixas Butterworth de 2ª ordem para eliminação de estridência em VR."""
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

def remove_dc_offset(samples):
    """Remove componente contínuo (DC bias)."""
    if not samples:
        return samples
    mean = sum(samples) / len(samples)
    return [s - mean for s in samples]

def apply_cosine_fade(samples, fade_in_samples=128, fade_out_samples=512):
    """Aplica envelope cosseno suave garantindo amostras inicial e final em 0.000000."""
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
    out[0] = 0.0
    out[-1] = 0.0
    return out

def normalize_to_peak_db(samples, target_peak_db=-9.5):
    """Normaliza o ganho para atingir estritamente o pico em dBFS especificado."""
    max_amp = max(abs(s) for s in samples) if samples else 0.0
    if max_amp <= 0.0:
        return samples
    target_amp = 10.0 ** (target_peak_db / 20.0)
    gain = target_amp / max_amp
    return [s * gain for s in samples]

def compute_metrics(samples):
    """Calcula pico máximo em dBFS e RMS em dBFS."""
    max_amp = max(abs(s) for s in samples) if samples else 0.0
    peak_db = 20.0 * math.log10(max_amp) if max_amp > 1e-9 else -99.0
    sum_sq = sum(s * s for s in samples)
    rms = math.sqrt(sum_sq / len(samples)) if samples else 0.0
    rms_db = 20.0 * math.log10(rms) if rms > 1e-9 else -99.0
    return peak_db, rms_db

def write_wav(path, samples, sample_rate=SAMPLE_RATE):
    """Grava arquivo WAV PCM 16-bit Mono com clamping de segurança."""
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

# ==============================================================================
# SÍNTESE ACÚSTICA PROCEDURAL DOS SONS DE CELEBRAÇÃO
# ==============================================================================

def synthesize_milestone_fanfare():
    """
    Fanfarra melódica triunfal suave e reconfortante (2.30s).
    - Progressão arpejada em Dó Maior: G4 -> C5 -> E5 -> acorde sustentado C5+E5+G5+C6.
    - Timbre sci-fi suave: harmônicos aveludados, vibrato orgânico e leve chorus.
    - Sinos celestiais/chimes com decaimento exponencial quente.
    - Reverb difuso multidirecional.
    - Filtro passa-baixas Butterworth em 3500 Hz (<= 3800 Hz).
    - Pico calibrado em -9.50 dBFS (<= -8.5 dBFS).
    """
    duration = 2.30
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    # 1. Trompetes/Metais sci-fi suaves
    notes = [
        (392.00, 0.00, 0.18, 0.65),  # G4
        (523.25, 0.16, 0.18, 0.72),  # C5
        (659.25, 0.32, 0.20, 0.80),  # E5
        (523.25, 0.48, 1.82, 0.55),  # Acorde sustenido: C5
        (659.25, 0.48, 1.82, 0.55),  # E5
        (783.99, 0.48, 1.82, 0.75),  # G5
        (1046.50, 0.48, 1.82, 0.35)  # Oitava superior C6
    ]
    
    for freq, start_t, dur, amp in notes:
        start_idx = int(start_t * SAMPLE_RATE)
        num_note_samples = int(dur * SAMPLE_RATE)
        for i in range(num_note_samples):
            idx = start_idx + i
            if idx >= total_samples:
                break
            t = i / SAMPLE_RATE
            
            # Envelope de nota com ataque macio e decaimento musical
            if t < 0.018:
                env = t / 0.018
            elif t < dur - 0.45:
                env = 1.0 - 0.12 * (t / dur)
            else:
                rel_t = (t - (dur - 0.45)) / 0.45
                env = max(0.0, (1.0 - rel_t) ** 2)
            
            # Vibrato sutil no acorde longo
            vibrato = 0.0
            if dur > 0.5 and t > 0.25:
                vib_depth = min(1.0, (t - 0.25) / 0.35) * 4.0
                vibrato = math.sin(2 * math.pi * 5.2 * t) * vib_depth
            
            inst_freq = freq + vibrato
            phase = 2 * math.pi * inst_freq * t
            phase_detune = 2 * math.pi * (inst_freq + 0.7) * t
            
            tone = (
                1.00 * math.sin(phase) +
                0.48 * math.sin(2 * phase) +
                0.24 * math.sin(3 * phase) +
                0.12 * math.sin(4 * phase) +
                0.05 * math.sin(5 * phase)
            )
            tone_chorus = 0.25 * math.sin(phase_detune)
            output[idx] += (tone + tone_chorus) * env * amp * 0.40

    # 2. Sinos/Chimes Celestiais para reforço de realização positiva
    bells = [
        (783.99, 0.00, 0.35, 0.22),
        (1046.50, 0.16, 0.35, 0.25),
        (1318.51, 0.32, 0.35, 0.28),
        (1046.50, 0.48, 1.40, 0.32),
        (1318.51, 0.48, 1.40, 0.28),
        (1567.98, 0.48, 1.40, 0.30)
    ]
    for bfreq, bstart, bdur, bamp in bells:
        s_idx = int(bstart * SAMPLE_RATE)
        b_samples = int(bdur * SAMPLE_RATE)
        for i in range(b_samples):
            idx = s_idx + i
            if idx >= total_samples:
                break
            t = i / SAMPLE_RATE
            att = min(1.0, t / 0.003)
            decay = math.exp(-t * (4.2 if bdur < 0.5 else 2.8))
            b_sample = math.sin(2 * math.pi * bfreq * t) + 0.30 * math.sin(2 * math.pi * bfreq * 2.01 * t)
            output[idx] += b_sample * att * decay * bamp * 0.35

    # 3. Rede de reverberação difusa
    reverb_delays = [int(SAMPLE_RATE * d) for d in [0.042, 0.075, 0.118, 0.165, 0.215]]
    reverb_decay = [0.26, 0.20, 0.15, 0.10, 0.06]
    reverbed = list(output)
    for d, decay in zip(reverb_delays, reverb_decay):
        for i in range(d, total_samples):
            reverbed[i] += output[i - d] * decay

    # 4. Filtro passa-baixas e conformação dinâmica
    filtered = apply_butterworth_lowpass(reverbed, 3500.0)
    filtered = remove_dc_offset(filtered)
    faded = apply_cosine_fade(filtered, 128, 1024)
    normalized = normalize_to_peak_db(faded, -9.50)
    return normalized

def synthesize_confetti_pop():
    """
    Puff suave e abafado de ar comprimido liberando confetes (0.18s).
    - Corpo pneumático com sweep de pitch suave (160 Hz -> 45 Hz).
    - Dispersão de ruído branco abafado (ar comprimido filtrado).
    - Micro-sopro secundário a 0.032s mimetizando canhões duplos.
    - Filtro passa-baixas Butterworth em 2400 Hz (<= 3000 Hz) eliminando cliques agudos.
    - Pico calibrado em -9.80 dBFS (<= -9.5 dBFS).
    """
    duration = 0.180
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    random.seed(1337)
    
    # 1. Corpo pneumático inicial
    thud_samples = int(0.080 * SAMPLE_RATE)
    for i in range(thud_samples):
        t = i / SAMPLE_RATE
        freq = 160.0 * math.exp(-t * 45.0) + 40.0
        phase = 2 * math.pi * freq * t
        att = min(1.0, t / 0.006)
        env = att * math.exp(-t * 35.0)
        output[i] += math.sin(phase) * env * 1.20
        
    # 2. Ruído de sopro de ar comprimido
    noise_samples = int(0.120 * SAMPLE_RATE)
    for i in range(noise_samples):
        t = i / SAMPLE_RATE
        noise = random.random() * 2.0 - 1.0
        att = min(1.0, t / 0.005)
        env = att * math.exp(-t * 38.0)
        output[i] += noise * env * 0.75
        
    # 3. Dispersão secundária (canhão duplo ligeiramente defasado)
    p2_start = int(0.032 * SAMPLE_RATE)
    p2_samples = int(0.080 * SAMPLE_RATE)
    for i in range(p2_samples):
        idx = p2_start + i
        if idx >= total_samples:
            break
        t = i / SAMPLE_RATE
        freq = 130.0 * math.exp(-t * 40.0) + 38.0
        phase = 2 * math.pi * freq * t
        att = min(1.0, t / 0.005)
        env = att * math.exp(-t * 40.0)
        noise = random.random() * 2.0 - 1.0
        output[idx] += (math.sin(phase) * 0.70 + noise * 0.40) * env * 0.55

    filtered = apply_butterworth_lowpass(output, 2400.0)
    filtered = remove_dc_offset(filtered)
    faded = apply_cosine_fade(filtered, 64, 256)
    normalized = normalize_to_peak_db(faded, -9.80)
    return normalized

def generate_all():
    target_dir = os.path.join("Assets", "Audio", "Generated", "Task05")
    os.makedirs(target_dir, exist_ok=True)
    
    print("=== Síntese de Áudio VR-Safe da TASK-05 ===")
    
    # Fanfarra triunfal suave
    fanfare_samples = synthesize_milestone_fanfare()
    f_peak, f_rms = compute_metrics(fanfare_samples)
    fanfare_path = os.path.join(target_dir, "drone_milestone_fanfare.wav")
    write_wav(fanfare_path, fanfare_samples)
    print(f"[OK] {fanfare_path}")
    print(f"     Duração: {len(fanfare_samples)/SAMPLE_RATE:.3f}s | Pico: {f_peak:.2f} dBFS | RMS: {f_rms:.2f} dBFS")
    
    # Cópia para fanfare_triumph.wav
    triumph_path = os.path.join(target_dir, "fanfare_triumph.wav")
    shutil.copyfile(fanfare_path, triumph_path)
    print(f"[OK] {triumph_path} (cópia idêntica de retrocompatibilidade)")
    
    # Estouro do canhão de confetes
    pop_samples = synthesize_confetti_pop()
    p_peak, p_rms = compute_metrics(pop_samples)
    pop_path = os.path.join(target_dir, "confetti_cannon_pop.wav")
    write_wav(pop_path, pop_samples)
    print(f"[OK] {pop_path}")
    print(f"     Duração: {len(pop_samples)/SAMPLE_RATE:.3f}s | Pico: {p_peak:.2f} dBFS | RMS: {p_rms:.2f} dBFS")
    
    return {
        "fanfare": {
            "duration": len(fanfare_samples)/SAMPLE_RATE,
            "peak_dbfs": f_peak,
            "rms_dbfs": f_rms,
            "first_sample": fanfare_samples[0],
            "last_sample": fanfare_samples[-1]
        },
        "confetti_pop": {
            "duration": len(pop_samples)/SAMPLE_RATE,
            "peak_dbfs": p_peak,
            "rms_dbfs": p_rms,
            "first_sample": pop_samples[0],
            "last_sample": pop_samples[-1]
        }
    }

if __name__ == "__main__":
    generate_all()
