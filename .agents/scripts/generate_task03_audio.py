"""
Gerador de Áudio VR-Safe — TASK-03: Drone Companheiro Clínico
Áudio de Travamento de Âncora e Distância Relativa: drone_anchor_lock.wav

Especificações Acústicas e Clínicas:
1. Formato: 44.1 kHz, 16-bit Mono PCM.
2. Timbre: Acorde senoidal puro, macio e aveludado (880 Hz -> 1320 Hz com sub-harmônico 440 Hz)
   simulando um bip sci-fi amigável e acolhedor de confirmação de ancoragem.
3. Duração: ~0.32s (14.112 amostras).
4. Teto de pico: máximo de -8.5 dBFS (calibrado a -8.80 dBFS para headroom seguro em fones VR).
5. Filtro passa-baixas: Butterworth 2ª ordem fc = 3400 Hz (<= 3800 Hz) para zero aspereza.
6. Janela de fade cosseno suave: amostras inicial e final rigorosamente em 0.000000.
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

def apply_fade(samples, fade_in_samples=128, fade_out_samples=256):
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
    # Garante 0 exato nas bordas
    out[0] = 0.0
    out[-1] = 0.0
    return out

def normalize_to_peak_db(samples, target_peak_db=-8.8):
    max_amp = max(abs(s) for s in samples) if samples else 0.0
    if max_amp <= 0.0:
        return samples
    target_amp = 10.0 ** (target_peak_db / 20.0)
    gain = target_amp / max_amp
    return [s * gain for s in samples]

def compute_metrics(samples):
    max_amp = max(abs(s) for s in samples) if samples else 0.0
    peak_db = 20.0 * math.log10(max_amp) if max_amp > 1e-9 else -99.0
    sum_sq = sum(s * s for s in samples)
    rms = math.sqrt(sum_sq / len(samples)) if samples else 0.0
    rms_db = 20.0 * math.log10(rms) if rms > 1e-9 else -99.0
    return peak_db, rms_db

def write_wav(path, samples, sample_rate=SAMPLE_RATE):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, 'wb') as wf:
        wf.setnchannels(1)  # Mono
        wf.setsampwidth(2)  # 16-bit PCM
        wf.setframerate(sample_rate)
        packed = bytearray()
        for s in samples:
            clamped = max(-1.0, min(1.0, s))
            val = int(clamped * 32767.0)
            packed.extend(struct.pack('<h', val))
        wf.writeframes(packed)
    print(f"Salvo WAV: {path}")

def generate_drone_anchor_lock(output_path):
    duration = 0.320  # 320ms
    num_samples = int(SAMPLE_RATE * duration)
    raw_samples = []

    phase_sub = 0.0
    phase_main = 0.0
    phase_fifth = 0.0
    phase_octave = 0.0

    f_sub_base = 440.0    # A4 (corpo aveludado)
    f_main_base = 880.0   # A5 (fundamental claro)
    f_fifth_base = 1320.0 # E6 (quinta justa ressonante)
    f_octave_base = 1760.0# A6 (brilho cristalino sutil)

    for i in range(num_samples):
        t = i / SAMPLE_RATE
        progress = t / duration

        # Micro-glide nos primeiros 35ms para um ataque sci-fi orgânico e responsivo
        if progress < 0.12:
            glide_factor = 0.94 + 0.06 * (progress / 0.12)
        else:
            glide_factor = 1.0

        f_sub = f_sub_base * glide_factor
        f_main = f_main_base * glide_factor
        f_fifth = f_fifth_base * glide_factor
        f_octave = f_octave_base * glide_factor

        phase_sub += 2.0 * math.pi * f_sub / SAMPLE_RATE
        phase_main += 2.0 * math.pi * f_main / SAMPLE_RATE
        phase_fifth += 2.0 * math.pi * f_fifth / SAMPLE_RATE
        phase_octave += 2.0 * math.pi * f_octave / SAMPLE_RATE

        # Componentes senoidais puras
        s_sub = math.sin(phase_sub) * 0.22       # Calidez e corpo macio
        s_main = math.sin(phase_main) * 0.65     # Tom principal
        s_fifth = math.sin(phase_fifth) * 0.40   # Harmônico de quinta
        s_octave = math.sin(phase_octave) * 0.06 # Brilho aveludado

        composite = s_sub + s_main + s_fifth + s_octave

        # Envelope ADSR clínico para VR:
        # Ataque rápido e suave (25ms) + Decaimento exponencial macio (anel de ressonância limpo)
        attack_time = 0.025
        if t < attack_time:
            env = 0.5 * (1.0 - math.cos(math.pi * t / attack_time))
        else:
            t_decay = t - attack_time
            env = math.exp(-t_decay * 8.2)

        raw_samples.append(composite * env)

    # 1. Filtro passa-baixas Butterworth (fc = 3400 Hz) para eliminar qualquer corte abrupto
    filtered = apply_butterworth_lowpass(raw_samples, cutoff_freq=3400.0, sample_rate=SAMPLE_RATE)

    # 2. Janela de fade cosseno suave garantindo amostras de borda em 0.000000
    faded = apply_fade(filtered, fade_in_samples=160, fade_out_samples=320)

    # 3. Normalização de Pico para -8.8 dBFS (headroom clínico VR-Safe)
    calibrated = normalize_to_peak_db(faded, target_peak_db=-8.80)

    # Calcula métricas finais
    peak_db, rms_db = compute_metrics(calibrated)
    first_sample = calibrated[0]
    last_sample = calibrated[-1]

    print("=================================================================")
    print("MÉTRICAS DO ÁUDIO SINTETIZADO (TASK-03.B):")
    print(f"Arquivo: {output_path}")
    print(f"Duração: {len(calibrated)/SAMPLE_RATE:.3f} s ({len(calibrated)} amostras)")
    print(f"Taxa de amostragem: {SAMPLE_RATE} Hz, 16-bit Mono PCM")
    print(f"Pico Máximo: {peak_db:.2f} dBFS (Conformidade <= -8.5 dBFS: {'OK' if peak_db <= -8.5 else 'FALHA'})")
    print(f"RMS: {rms_db:.2f} dBFS")
    print(f"Amostra Inicial: {first_sample:.6f} | Amostra Final: {last_sample:.6f}")
    print("=================================================================")

    write_wav(output_path, calibrated, SAMPLE_RATE)
    return calibrated, peak_db, rms_db

if __name__ == "__main__":
    out_dir = os.path.abspath("Assets/Audio/Generated/Task03")
    lock_wav_path = os.path.join(out_dir, "drone_anchor_lock.wav")
    generate_drone_anchor_lock(lock_wav_path)
