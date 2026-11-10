import wave
import math
import struct
import os
import random

SAMPLE_RATE = 44100

def write_wav(filename, samples):
    os.makedirs(os.path.dirname(filename), exist_ok=True)
    with wave.open(filename, 'wb') as wav:
        wav.setnchannels(1)  # Mono
        wav.setsampwidth(2)  # 16-bit
        wav.setframerate(SAMPLE_RATE)
        packed = bytearray()
        for s in samples:
            clamped = max(-1.0, min(1.0, s))
            val = int(clamped * 32767.0)
            packed.extend(struct.pack('<h', val))
        wav.writeframes(packed)
    print(f"Generated audio: {filename} ({len(samples)} samples, {len(samples)/SAMPLE_RATE:.2f}s)")

def generate_coin_pickup_chime(filename, duration=0.85):
    """Chime cristalino harmônico ascendente para coleta de moedas"""
    num_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    # Duas frequências que soam em rápida sucessão:
    # 0.00s: F#5 (739.99 Hz)
    # 0.08s: B5 (987.77 Hz) + harmônicos cintilantes
    f1 = 739.99
    f2 = 987.77
    
    for i in range(num_samples):
        t = i / SAMPLE_RATE
        val = 0.0
        
        # Primeira nota suave
        if t >= 0.0:
            env1 = math.exp(-12.0 * t)
            val += 0.35 * math.sin(2.0 * math.pi * f1 * t) * env1
            val += 0.15 * math.sin(2.0 * math.pi * (f1 * 2.0) * t) * (env1 ** 1.5)
            
        # Segunda nota principal com ressonância cristalina rica
        if t >= 0.07:
            t2 = t - 0.07
            env2 = math.exp(-4.2 * t2)
            
            # Fundamental
            val += 0.55 * math.sin(2.0 * math.pi * f2 * t2) * env2
            # 2º harmônico (B6 1975.5 Hz)
            val += 0.28 * math.sin(2.0 * math.pi * (f2 * 2.0) * t2) * math.exp(-6.0 * t2)
            # 3º harmônico (F#7 2963.3 Hz)
            val += 0.15 * math.sin(2.0 * math.pi * (f2 * 3.0) * t2) * math.exp(-8.5 * t2)
            # Brilho de campainha metálica/cristalina
            val += 0.08 * math.sin(2.0 * math.pi * (f2 * 4.25) * t2) * math.exp(-14.0 * t2)
            val += 0.04 * math.sin(2.0 * math.pi * (f2 * 6.1) * t2) * math.exp(-20.0 * t2)
            
        # Fade in ultra-rápido para eliminar pop inicial
        if i < 80:
            val *= (i / 80.0)
            
        samples.append(val * 0.85)
        
    write_wav(filename, samples)

def generate_coin_magnet_whoosh(filename, duration=0.60):
    """Woosh suave de atração magnética aérea com sweep e modulação em anel"""
    num_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    # Sweep suave de frequência de 160Hz a 480Hz
    # com modulação de amplitude pulsante rápida (32Hz)
    random.seed(42)
    noise_buffer = [random.uniform(-1.0, 1.0) for _ in range(num_samples)]
    
    for i in range(num_samples):
        t = i / SAMPLE_RATE
        norm_t = t / duration
        
        # Envelope em arco suave
        env = math.sin(math.pi * norm_t) ** 1.4
        
        # Sweep exponencial
        f = 160.0 * ((480.0 / 160.0) ** (norm_t ** 1.2))
        phase = 2.0 * math.pi * f * t
        
        # Modulação magnética
        tremolo = 0.70 + 0.30 * math.sin(2.0 * math.pi * 32.0 * t)
        
        # Tom base
        tone = 0.50 * math.sin(phase) + 0.25 * math.sin(phase * 1.5) + 0.15 * math.sin(phase * 2.0)
        
        # Componente de ar/vento suave
        noise_val = noise_buffer[i] * 0.18 * (norm_t ** 0.5)
        
        val = (tone * tremolo + noise_val) * env
        samples.append(val * 0.75)
        
    write_wav(filename, samples)

def generate_secret_coin_fanfare(filename, duration=1.25):
    """Mini fanfarra triunfante cintilante para moedas bônus (laser)"""
    num_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    # 4 notas em arpeggio alegre: E5, G#5, B5, E6
    notes = [
        (0.00, 659.25, 0.22), # E5
        (0.11, 830.61, 0.22), # G#5
        (0.22, 987.77, 0.26), # B5
        (0.33, 1318.51, 0.90) # E6 (Sustentada)
    ]
    
    for i in range(num_samples):
        t = i / SAMPLE_RATE
        val = 0.0
        
        for (start_t, freq, note_dur) in notes:
            if t >= start_t:
                local_t = t - start_t
                if local_t < note_dur:
                    # Envelope de cada nota
                    attack = min(1.0, local_t / 0.008)
                    decay = math.exp(-3.5 * (local_t / note_dur))
                    env = attack * decay
                    
                    # Fundamental + harmônicos cintilantes
                    s = 0.50 * math.sin(2.0 * math.pi * freq * local_t)
                    s += 0.25 * math.sin(2.0 * math.pi * (freq * 2.0) * local_t) * 0.6
                    s += 0.15 * math.sin(2.0 * math.pi * (freq * 3.0) * local_t) * 0.35
                    s += 0.08 * math.sin(2.0 * math.pi * (freq * 4.0) * local_t) * 0.2
                    
                    val += s * env
                    
        # Pequeno brilho estelar cintilante na nota final
        if t >= 0.45:
            sparkle_t = t - 0.45
            sparkle_env = math.exp(-3.0 * sparkle_t)
            val += 0.06 * math.sin(2.0 * math.pi * 2637.0 * sparkle_t) * sparkle_env
            val += 0.04 * math.sin(2.0 * math.pi * 3136.0 * sparkle_t) * sparkle_env
            
        samples.append(val * 0.80)
        
    write_wav(filename, samples)

if __name__ == "__main__":
    out_dir = os.path.abspath("Assets/Audio/Generated/Task14")
    generate_coin_pickup_chime(os.path.join(out_dir, "coin_pickup_chime_combo.wav"))
    generate_coin_magnet_whoosh(os.path.join(out_dir, "coin_magnet_whoosh.wav"))
    generate_secret_coin_fanfare(os.path.join(out_dir, "secret_coin_fanfare.wav"))
    print("ALL TASK-14 AUDIO FILES GENERATED SUCCESSFULLY!")
