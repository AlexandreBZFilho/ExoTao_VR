import math
import struct
import wave
import os
import random

SAMPLE_RATE = 44100

def lowpass(samples, cutoff_hz):
    """Filtro passa-baixa de 1 polo (tira o agudo estridente)."""
    if not samples:
        return samples
    dt = 1.0 / SAMPLE_RATE
    rc = 1.0 / (2.0 * math.pi * cutoff_hz)
    a = dt / (rc + dt)
    out = [0.0] * len(samples)
    y = 0.0
    for i, x in enumerate(samples):
        y += a * (x - y)
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

def write_wav(filename, samples, peak_dbfs=-8.0):
    """Grava WAV 16-bit mono normalizado para um PICO ALVO em dBFS (regra do projeto: pico <= -6 dBFS)."""
    with wave.open(filename, 'w') as wav_file:
        wav_file.setnchannels(1)  # Mono
        wav_file.setsampwidth(2)  # 16-bit
        wav_file.setframerate(SAMPLE_RATE)
        max_val = max(abs(s) for s in samples) if samples else 1.0
        if max_val == 0:
            max_val = 1.0
        target = 10.0 ** (peak_dbfs / 20.0)
        scale = 32767.0 * target / max_val
        packed_frames = bytearray()
        for s in samples:
            val = int(s * scale)
            val = max(-32768, min(32767, val))
            packed_frames.extend(struct.pack('<h', val))
        wav_file.writeframes(packed_frames)
    print(f"Generated {filename} ({len(samples)/SAMPLE_RATE:.2f}s, pico {peak_dbfs:.1f} dBFS)")

def synthesize_inflate_stretch():
    """
    Synthesize an elastic rubber friction stretching sound combined with pressurized air hiss.
    Tension climbs as balloon expands over 1.6s.
    """
    duration = 1.60
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    random.seed(42)
    phase_stretch = 0.0
    phase_squeak = 0.0
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        progress = t / duration  # 0.0 -> 1.0
        
        # Envelope: smooth fade in (0.05s) and gentle fade out at end
        env = min(1.0, t / 0.05) * max(0.0, (1.0 - (t / duration) ** 3))
        
        # 1. Pressurized Air Hiss (Bandpass-filtered white noise)
        white_noise = (random.random() * 2.0 - 1.0)
        # Lowpass smoothing for softer hiss
        hiss = white_noise * (0.35 + 0.15 * math.sin(t * 30.0))
        
        # 2. Rubber friction tension creak / squeaks
        # Frequency glides up from 420 Hz to 1100 Hz as tension increases
        freq_stretch = 380.0 + 720.0 * (progress ** 1.8)
        # Add micro-stutter / stick-slip friction
        friction_mod = math.sin(t * 120.0) * 0.25 + math.sin(t * 280.0) * 0.15
        phase_stretch += 2.0 * math.pi * (freq_stretch * (1.0 + friction_mod * 0.1)) / SAMPLE_RATE
        stretch_tone = math.sin(phase_stretch) + 0.4 * math.sin(phase_stretch * 2.0) + 0.15 * math.sin(phase_stretch * 3.0)
        
        # 3. Occasional rubber squeak bursts (elastic tension slips)
        squeak = 0.0
        for burst_center in [0.25, 0.55, 0.85, 1.15, 1.35]:
            diff = abs(t - burst_center)
            if diff < 0.06:
                burst_env = math.exp(- (diff / 0.02) ** 2)
                freq_burst = 950.0 + 600.0 * progress
                phase_squeak += 2.0 * math.pi * freq_burst / SAMPLE_RATE
                squeak += math.sin(phase_squeak) * burst_env * 0.6
        
        sample = (hiss * 0.28 + stretch_tone * 0.38 + squeak * 0.22) * env
        output[i] = sample
        
    return lowpass(output, 3500.0)

def synthesize_release_whoosh():
    """
    Synthesize pneumatic release whoosh with air puff and rubber snap.
    """
    duration = 0.45
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    random.seed(1337)
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        
        # Attack / Decay
        if t < 0.025:
            env = (t / 0.025)
        else:
            env = math.exp(-(t - 0.025) * 8.5)
            
        # 1. Pneumatic Air Release (shaped noise)
        noise = (random.random() * 2.0 - 1.0)
        # Frequency sweep downward (air expansion)
        cutoff_mod = math.exp(-t * 12.0)
        air_puff = noise * (0.6 + 0.4 * cutoff_mod)
        
        # 2. Nozzle elastic slip 'twang' / thud
        snap_freq = 280.0 * math.exp(-t * 15.0) + 90.0
        snap = math.sin(2.0 * math.pi * snap_freq * t) * math.exp(-t * 22.0) * 0.7
        
        # 3. Gentle resonant airy whoosh
        whoosh_freq = 550.0 * math.exp(-t * 5.0)
        whoosh = math.sin(2.0 * math.pi * whoosh_freq * t) * math.exp(-t * 6.0) * 0.35
        
        output[i] = (air_puff * 0.55 + snap * 0.45 + whoosh * 0.3) * env

    return output

def synthesize_pop_soft():
    """
    Estouro de balão CONFORTÁVEL para reabilitação (TASK-07.B, 2026-11-25):
    sem o pico supersônico da versão antiga, estalo de borracha filtrado (low-pass 3,2 kHz),
    "puf" grave curto e cauda mínima. 0,28 s, pico -8 dBFS.
    """
    duration = 0.28
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    random.seed(777)
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        attack = min(1.0, t / 0.0015)
        crack_env = math.exp(-t * 60.0)
        crack = (random.random() * 2.0 - 1.0) * crack_env * 0.55
        thud_env = math.exp(-t * 18.0)
        thud_freq = 120.0 * math.exp(-t * 9.0) + 70.0
        thud = math.sin(2.0 * math.pi * thud_freq * t) * thud_env * 0.75
        tail = (random.random() * 2.0 - 1.0) * math.exp(-t * 14.0) * 0.06
        output[i] = (crack + thud + tail) * attack
    return lowpass(output, 3200.0)

def main():
    target_dir = os.path.dirname(os.path.abspath(__file__))
    f1 = os.path.join(target_dir, "balloon_inflate_stretch.wav")
    f2 = os.path.join(target_dir, "balloon_release_whoosh.wav")
    f3 = os.path.join(target_dir, "balloon_pop_soft.wav")
    write_wav(f1, fade_edges(synthesize_inflate_stretch(), 0.02, 0.05), peak_dbfs=-10.0)
    write_wav(f2, fade_edges(lowpass(synthesize_release_whoosh(), 3500.0)), peak_dbfs=-9.0)
    write_wav(f3, fade_edges(synthesize_pop_soft(), 0.0015, 0.04), peak_dbfs=-8.0)
    print("All Task07 procedural audio files successfully generated!")

if __name__ == "__main__":
    main()
