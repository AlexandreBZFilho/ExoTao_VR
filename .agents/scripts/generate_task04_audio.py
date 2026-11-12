"""
Gerador de Áudio VR-Safe Calibrado — TASK-04: Reatividade Afetiva do Drone Pet
Sons:
  1. drone_purr_happy.wav (e drone_purr.wav) - Ronronar sci-fi aveludado e relaxante de carinho
  2. drone_poke_chirp.wav (e drone_boop.wav) - Bip amigável e fofo de cutucada/boop
  3. drone_giggle_soft.wav (e drone_giggle.wav) - Risinho robótico alegre e macio
  4. drone_dance_groove.wav - Pulso rítmico musical alegre

Parâmetros Acústicos Estritos:
  - Formato: 44.1 kHz, 16-bit Mono PCM.
  - Teto de Pico Digital: <= -9.0 dBFS (calibrado a -9.50 dBFS).
  - Filtro Passa-Baixas Ativo (Butterworth 2ª ordem):
      * purr: fc <= 2000 Hz (1800 Hz)
      * poke: fc <= 3200 Hz (2800 Hz)
      * giggle: fc <= 3500 Hz (3200 Hz)
      * dance: fc <= 3200 Hz (2800 Hz)
  - Envelope de Fade Cosseno: amostras inicial e final rigorosamente em 0.000000 (zero estalos, sem DC offset).
"""

import math
import struct
import wave
import os
import shutil

SAMPLE_RATE = 44100

def apply_butterworth_lowpass(samples, cutoff_freq, sample_rate=SAMPLE_RATE, q=0.7071):
    """Filtro IIR passa-baixas Butterworth de 2ª ordem para eliminação de sibilâncias agudas."""
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

def remove_dc_offset(samples):
    """Remove qualquer componente de corrente contínua (DC bias)."""
    if not samples:
        return samples
    mean = sum(samples) / len(samples)
    return [s - mean for s in samples]

def apply_cosine_fade(samples, fade_in_samples=128, fade_out_samples=256):
    """Aplica janelamento cosseno suave nas extremidades, garantindo 0.0 absoluto no início e fim."""
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
    """Grava arquivo WAV PCM 16-bit Mono."""
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
# SÍNTESE ACÚSTICA PROCEDURAL DOS 4 EFEITOS
# ==============================================================================

def generate_drone_purr_happy():
    """
    1. drone_purr_happy.wav:
    Ronronar sci-fi aveludado e relaxante de carinho.
    Duração ~0.60s (26460 amostras), fundamental ~120 Hz modulada suavemente,
    modulação AM de purr ~16 Hz, corpo em sub-bass ~60 Hz, saturação quente suave.
    Filtro passa-baixas <= 2000 Hz (1800 Hz).
    """
    duration = 0.600
    num_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    phase_f0 = 0.0
    phase_sub = 0.0
    phase_h2 = 0.0
    
    for i in range(num_samples):
        t = i / SAMPLE_RATE
        norm_t = t / duration
        
        # Envelope de respiração e volume
        env = math.sin(norm_t * math.pi) ** 0.85
        
        # Modulação de amplitude de tremolo / ronrom (16 Hz)
        purr_tremolo = 0.58 + 0.42 * math.sin(2.0 * math.pi * 16.0 * t)
        
        # Frequência fundamental ligeiramente ondulante para sensação biônica orgânica
        f0 = 120.0 + 3.5 * math.sin(2.0 * math.pi * 4.0 * t)
        f_sub = f0 * 0.5  # 60 Hz (calor no peito)
        f_h2 = f0 * 2.0   # 240 Hz
        
        phase_f0 += 2.0 * math.pi * f0 / SAMPLE_RATE
        phase_sub += 2.0 * math.pi * f_sub / SAMPLE_RATE
        phase_h2 += 2.0 * math.pi * f_h2 / SAMPLE_RATE
        
        s_main = math.sin(phase_f0) * 0.60
        s_sub = math.sin(phase_sub) * 0.35
        s_h2 = math.sin(phase_h2) * 0.15
        
        composite = (s_main + s_sub + s_h2) * purr_tremolo * env
        
        # Saturação suave tipo fita magnética/válvula
        warm = math.tanh(composite * 1.25)
        samples.append(warm)
        
    # Pós-processamento DSP
    samples = remove_dc_offset(samples)
    samples = apply_butterworth_lowpass(samples, cutoff_freq=1800)
    samples = apply_cosine_fade(samples, fade_in_samples=512, fade_out_samples=512)
    samples = normalize_to_peak_db(samples, target_peak_db=-9.50)
    return samples

def generate_drone_poke_chirp():
    """
    2. drone_poke_chirp.wav:
    Bip amigável e fofo de cutucada/boop.
    Duração ~0.25s (11025 amostras), sweep rápido (blip inicial 420->680 Hz,
    seguido de sweep amortecido até 240 Hz), decaimento exponencial elástico.
    Filtro passa-baixas <= 3200 Hz (2800 Hz).
    """
    duration = 0.250
    num_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    phase = 0.0
    for i in range(num_samples):
        t = i / SAMPLE_RATE
        norm_t = t / duration
        
        # Envelope elástico: ataque rápido (8ms) e decaimento exponencial
        if norm_t < 0.04:
            env = norm_t / 0.04
        else:
            decay_t = norm_t - 0.04
            env = math.exp(-decay_t * 8.5)
            
        # Curva de frequência: sweep de boop elástico descendente
        if norm_t < 0.05:
            freq = 420.0 + (680.0 - 420.0) * (norm_t / 0.05)
        else:
            sweep_progress = (norm_t - 0.05) / 0.95
            freq = 240.0 + (680.0 - 240.0) * math.exp(-12.0 * sweep_progress)
            
        phase += 2.0 * math.pi * freq / SAMPLE_RATE
        
        # Onda senoidal com segundo harmônico macio para corpo emborrachado
        tone = math.sin(phase) + 0.25 * math.sin(phase * 2.0) + 0.05 * math.sin(phase * 3.0)
        samples.append(tone * env)
        
    samples = remove_dc_offset(samples)
    samples = apply_butterworth_lowpass(samples, cutoff_freq=2800)
    samples = apply_cosine_fade(samples, fade_in_samples=128, fade_out_samples=256)
    samples = normalize_to_peak_db(samples, target_peak_db=-9.50)
    return samples

def generate_drone_giggle_soft():
    """
    3. drone_giggle_soft.wav:
    Risinho robótico alegre e macio.
    Duração ~0.40s (17640 amostras), arpeggio saltitante em tríade amigável
    (D5: 587.3 Hz, F#5: 739.99 Hz, A5: 880.0 Hz) com vibrato sutil e envelope staccato elástico.
    Filtro passa-baixas <= 3500 Hz (3200 Hz).
    """
    duration = 0.400
    num_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    notes = [587.33, 739.99, 880.00]  # D5, F#5, A5
    note_dur = duration / len(notes)
    
    phase = 0.0
    for i in range(num_samples):
        t = i / SAMPLE_RATE
        note_idx = min(len(notes) - 1, int(t / note_dur))
        local_t = t - (note_idx * note_dur)
        norm_local = local_t / note_dur
        
        # Envelope de cada bolinha de riso (staccato arredondado)
        env = math.sin(norm_local * math.pi) ** 1.35
        
        # Vibrato brincalhão suave
        vibrato = math.sin(2.0 * math.pi * 13.0 * t) * 12.0
        f = notes[note_idx] + vibrato
        
        phase += 2.0 * math.pi * f / SAMPLE_RATE
        
        tone = math.sin(phase) + 0.22 * math.sin(phase * 2.0)
        samples.append(tone * env)
        
    samples = remove_dc_offset(samples)
    samples = apply_butterworth_lowpass(samples, cutoff_freq=3200)
    samples = apply_cosine_fade(samples, fade_in_samples=128, fade_out_samples=384)
    samples = normalize_to_peak_db(samples, target_peak_db=-9.50)
    return samples

def generate_drone_dance_groove():
    """
    4. drone_dance_groove.wav:
    Pulso rítmico musical alegre.
    Duração ~0.50s (22050 amostras), 2 pulsos rítmicos sincronizados estilo 120 BPM
    (Beat 1: kick/blip aveludado 95 Hz + harmônico 280 Hz; Beat 2: blip melódico 520 Hz -> 660 Hz).
    Filtro passa-baixas <= 3200 Hz (2800 Hz).
    """
    duration = 0.500
    num_samples = int(SAMPLE_RATE * duration)
    samples = [0.0] * num_samples
    
    # Pulso 1: Beat suave de base (0.00s a 0.22s)
    beat1_dur = 0.22
    beat1_samples = int(SAMPLE_RATE * beat1_dur)
    phase1 = 0.0
    for i in range(beat1_samples):
        t = i / SAMPLE_RATE
        norm_t = t / beat1_dur
        env = math.sin(norm_t * math.pi) ** 1.4
        f = 95.0 + 80.0 * math.exp(-18.0 * norm_t)
        phase1 += 2.0 * math.pi * f / SAMPLE_RATE
        val = (math.sin(phase1) + 0.28 * math.sin(phase1 * 2.5)) * env * 0.7
        samples[i] += val
        
    # Pulso 2: Melodia saltitante sincopada (0.24s a 0.48s)
    beat2_start = int(SAMPLE_RATE * 0.24)
    beat2_dur = 0.24
    beat2_samples = int(SAMPLE_RATE * beat2_dur)
    phase2 = 0.0
    for i in range(beat2_samples):
        idx = beat2_start + i
        if idx >= num_samples:
            break
        t = i / SAMPLE_RATE
        norm_t = t / beat2_dur
        env = math.sin(norm_t * math.pi) ** 1.3
        f = 523.25 + (659.25 - 523.25) * norm_t
        phase2 += 2.0 * math.pi * f / SAMPLE_RATE
        val = (math.sin(phase2) + 0.20 * math.sin(phase2 * 2.0)) * env * 0.75
        samples[idx] += val
        
    samples = remove_dc_offset(samples)
    samples = apply_butterworth_lowpass(samples, cutoff_freq=2800)
    samples = apply_cosine_fade(samples, fade_in_samples=128, fade_out_samples=384)
    samples = normalize_to_peak_db(samples, target_peak_db=-9.50)
    return samples

# ==============================================================================
# EXECUÇÃO E AUDITORIA
# ==============================================================================

def ensure_meta_file(wav_path):
    """Garante arquivo .meta com normalize: 0 para que a Unity não normalize a 0 dBFS."""
    import uuid
    meta_path = wav_path + '.meta'
    if os.path.exists(meta_path):
        with open(meta_path, 'r', encoding='utf-8') as mf:
            content = mf.read()
        if 'normalize: 1' in content:
            content = content.replace('normalize: 1', 'normalize: 0')
            with open(meta_path, 'w', encoding='utf-8') as mf:
                mf.write(content)
    else:
        new_guid = uuid.uuid4().hex
        meta_content = f"""fileFormatVersion: 2
guid: {new_guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 1
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
        with open(meta_path, 'w', encoding='utf-8') as mf:
            mf.write(meta_content)

def main():
    out_dir = r"Assets/Audio/Generated/Task04"
    os.makedirs(out_dir, exist_ok=True)
    
    items = [
        ("drone_purr_happy.wav", generate_drone_purr_happy(), "drone_purr.wav"),
        ("drone_poke_chirp.wav", generate_drone_poke_chirp(), "drone_boop.wav"),
        ("drone_giggle_soft.wav", generate_drone_giggle_soft(), "drone_giggle.wav"),
        ("drone_dance_groove.wav", generate_drone_dance_groove(), None)
    ]
    
    print("=" * 80)
    print("AUDITORIA ACÚSTICA VR-SAFE — TASK-04 AUDIO GENERATOR")
    print("=" * 80)
    print(f"{'Arquivo':<26} | {'Duração':<8} | {'Pico (dBFS)':<12} | {'RMS (dBFS)':<12} | {'Zero Edge':<10}")
    print("-" * 80)
    
    for filename, samples, alias_legacy in items:
        path = os.path.join(out_dir, filename)
        write_wav(path, samples)
        ensure_meta_file(path)
        
        # Também grava no nome legado para compatibilidade 100% retroativa
        if alias_legacy:
            legacy_path = os.path.join(out_dir, alias_legacy)
            write_wav(legacy_path, samples)
            ensure_meta_file(legacy_path)
            
        peak_db, rms_db = compute_metrics(samples)
        dur = len(samples) / SAMPLE_RATE
        zero_edge = (samples[0] == 0.0 and samples[-1] == 0.0)
        
        print(f"{filename:<26} | {dur:6.2f}s  | {peak_db:9.2f} dB  | {rms_db:9.2f} dB  | {str(zero_edge):<10}")
        
    print("=" * 80)
    print("Todos os 4 áudios e seus equivalentes legados foram sintetizados e validados!")

if __name__ == "__main__":
    main()
