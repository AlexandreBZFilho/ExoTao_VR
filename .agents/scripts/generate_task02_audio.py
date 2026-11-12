"""
Sintetizador e Calibrador de Áudio VR-Safe — TASK-02 Core Audio
Feedback Tátil e Sonoro de Lançamento por Balanço do Laser (Laser Swing Throw)
VR Rehab Gamification — Padrão Clínico para Reabilitação Motora

Critérios de Conformidade VR-Safe:
1. 44.1 kHz, 16-bit Mono PCM.
2. Duração: ~0.30s a 0.38s (ex: 0.34s).
3. Teto estrito de pico: -8.5 dBFS (headroom confortável, sem estalos nem fadiga acústica).
4. Início e fim em amplitude zero absoluta (0.000000) com janelamento Hann/Cosine fade.
5. Filtro passa-baixas (Butterworth 2ª ordem fc <= 3200 Hz) para corte aveludado e suave de vento.
6. Sub-harmônico sutil para feedback de inércia e leveza.
"""

import math
import os
import random
import struct
import wave

SAMPLE_RATE = 44100

def apply_butterworth_lowpass(samples, cutoff_freq, sample_rate=SAMPLE_RATE, q=0.7071):
    """Filtro passa-baixas biquad IIR Butterworth de 2ª ordem."""
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
    
    # Normalização dos coeficientes
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

def apply_cosine_fade(samples, fade_in_samples, fade_out_samples):
    """Aplica rampa cosseno suave para garantir transição perfeita em 0.0."""
    n = len(samples)
    out = []
    for i in range(n):
        s = samples[i]
        if i < fade_in_samples:
            # 0.0 em i=0
            w = 0.5 * (1.0 - math.cos(math.pi * i / fade_in_samples))
            s *= w
        elif i >= n - fade_out_samples:
            # 0.0 em i = n-1
            rem = n - 1 - i
            w = 0.5 * (1.0 - math.cos(math.pi * rem / fade_out_samples))
            s *= w
        out.append(s)
    # Garante estritamente zeros nas extremidades
    if out:
        out[0] = 0.0
        out[-1] = 0.0
    return out

def normalize_to_peak_db(samples, target_peak_db=-8.5):
    """Normaliza o array para o teto estrito especificado em dBFS."""
    max_amp = max(abs(s) for s in samples) if samples else 0.0
    if max_amp <= 1e-9:
        return samples
    target_amp = 10.0 ** (target_peak_db / 20.0)
    gain = target_amp / max_amp
    return [s * gain for s in samples]

def calculate_metrics(samples):
    """Calcula pico (dBFS) e RMS (dBFS) de uma lista de amostras float [-1, 1]."""
    if not samples:
        return -999.0, -999.0
    max_amp = max(abs(s) for s in samples)
    peak_db = 20.0 * math.log10(max_amp) if max_amp > 1e-9 else -999.0
    mean_sq = sum(s * s for s in samples) / len(samples)
    rms = math.sqrt(mean_sq)
    rms_db = 20.0 * math.log10(rms) if rms > 1e-9 else -999.0
    return peak_db, rms, rms_db

def generate_laser_throw_woosh(duration=0.34, target_peak_db=-8.5, cutoff_freq=2800):
    """
    Gera o som 'laser_throw_woosh.wav':
    - Ruído filtrado rosa/azul moldado para simular corte de ar e vento aerodinâmico.
    - Componente tonal sutil de laser (glissando descendente de 540 Hz para 160 Hz) que dá corpo cinético.
    - Envelope suave com ataque orgânico rápido (~0.08s) e cauda aveludada (~0.26s).
    - Duplo estágio de filtro passa-baixas Butterworth (fc <= 2800 Hz) para textura sedosa sem chiado.
    """
    random.seed(42)  # Determismo para reprodutibilidade clínica
    num_samples = int(SAMPLE_RATE * duration)
    raw_samples = []
    
    # 1. Ruído rosa com filtro IIR de 3 polos (Paul Kellet)
    b0 = b1 = b2 = b3 = b4 = b5 = b6 = 0.0
    
    phase_sweep = 0.0
    phase_sub = 0.0
    
    for i in range(num_samples):
        t = i / SAMPLE_RATE
        progress = t / duration
        
        # Envelope assimétrico: ascensão rápida e cauda com decaimento suave
        if progress < 0.25:
            # Fade-in suave em forma de meia onda seno
            env = math.sin((progress / 0.25) * (math.pi / 2.0))
        else:
            # Decaimento suave exponencial atenuado
            decay_prog = (progress - 0.25) / 0.75
            env = math.cos(decay_prog * (math.pi / 2.0)) ** 1.8
            
        # Geração de ruído branco [-1, 1]
        white = random.uniform(-1.0, 1.0)
        # Filtro de ruído rosa
        b0 = 0.99886 * b0 + white * 0.0555179
        b1 = 0.99332 * b1 + white * 0.0750759
        b2 = 0.96900 * b2 + white * 0.1538520
        b3 = 0.86650 * b3 + white * 0.3104856
        b4 = 0.55000 * b4 + white * 0.5329522
        b5 = -0.7616 * b5 - white * 0.0168980
        pink = b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362
        b6 = white * 0.115926
        pink *= 0.12  # Normaliza escala aproximada
        
        # Componente harmônico suave de laser (curva descendente que confere movimento direcional)
        # 520 Hz no pico -> 150 Hz no fim
        f_laser = 520.0 * (1.0 - 0.72 * (progress ** 0.8))
        f_sub = f_laser * 0.5  # Sub-harmônico para peso sutil
        
        phase_sweep += 2.0 * math.pi * f_laser / SAMPLE_RATE
        phase_sub += 2.0 * math.pi * f_sub / SAMPLE_RATE
        
        laser_tone = 0.32 * math.sin(phase_sweep) + 0.18 * math.sin(phase_sub)
        
        # Modulação de amplitude do ar (leque de vento)
        wind_mod = 0.85 + 0.15 * math.sin(2.0 * math.pi * 12.0 * t)
        
        sample = (pink * 0.65 * wind_mod + laser_tone * 0.35) * env
        raw_samples.append(sample)
        
    # Filtragem passa-baixas para eliminar qualquer frequência aguda acima de 2800 Hz
    filtered = apply_butterworth_lowpass(raw_samples, cutoff_freq, SAMPLE_RATE)
    # Segundo passe mais suave a 3000 Hz para garantir atenuação ultra-limpa de altas frequências
    filtered = apply_butterworth_lowpass(filtered, 3000, SAMPLE_RATE)
    
    # Aplicação de fade-in e fade-out com estrito zero nas pontas
    fade_in_len = int(SAMPLE_RATE * 0.015)   # 15 ms
    fade_out_len = int(SAMPLE_RATE * 0.040)  # 40 ms
    windowed = apply_cosine_fade(filtered, fade_in_len, fade_out_len)
    
    # Normalização precisa ao teto estrito de -8.5 dBFS
    final_samples = normalize_to_peak_db(windowed, target_peak_db=target_peak_db)
    return final_samples

def write_wav(file_path, samples):
    """Grava o arquivo WAV em formato 16-bit PCM Mono a 44.1 kHz."""
    os.makedirs(os.path.dirname(file_path), exist_ok=True)
    with wave.open(file_path, 'wb') as wf:
        wf.setnchannels(1)      # Mono
        wf.setsampwidth(2)      # 16-bit (2 bytes)
        wf.setframerate(SAMPLE_RATE)
        
        packed_data = bytearray()
        for s in samples:
            clamped = max(-1.0, min(1.0, s))
            int_val = int(clamped * 32767.0)
            packed_data.extend(struct.pack('<h', int_val))
        wf.writeframes(packed_data)

def main():
    output_dir = r"Assets/Audio/Generated/Task02"
    wav_path = os.path.join(output_dir, "laser_throw_woosh.wav")
    
    duration = 0.34  # ~0.34s (dentro de 0.30s a 0.38s)
    target_peak = -8.5  # dBFS
    cutoff = 2800  # Hz (estritamente <= 3200 Hz)
    
    print("=== Gerando Áudio VR-Safe: Laser Throw Woosh ===")
    samples = generate_laser_throw_woosh(duration=duration, target_peak_db=target_peak, cutoff_freq=cutoff)
    write_wav(wav_path, samples)
    
    peak_db, rms, rms_db = calculate_metrics(samples)
    actual_dur = len(samples) / SAMPLE_RATE
    
    print(f"Arquivo gerado: {wav_path}")
    print(f"  Duração: {actual_dur:.4f}s ({len(samples)} amostras)")
    print(f"  Pico Máximo: {peak_db:.2f} dBFS (Teto solicitado: {target_peak:.1f} dBFS)")
    print(f"  RMS: {rms:.4f} ({rms_db:.2f} dBFS)")
    print(f"  Início: {samples[0]:.6f}, Fim: {samples[-1]:.6f}")
    print(f"  Filtro Passa-Baixas: {cutoff} Hz (Butterworth 2ª ordem)")
    print("Áudio VR-Safe gerado com sucesso!")

if __name__ == "__main__":
    main()
