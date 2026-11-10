import wave
import math
import struct
import os

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

def generate_tractor_beam_hum(filename, duration=3.0):
    # Loopable magnetic hum with warm fundamental, second harmonic and gentle low frequency oscillation (LFO)
    num_samples = int(SAMPLE_RATE * duration)
    samples = []
    
    # Fundamental frequency ~92 Hz (F#2), 2nd harmonic ~184 Hz, 3rd ~276 Hz
    f0 = 92.0
    f1 = 184.0
    f2 = 276.0
    lfo_rate = 2.0  # 2 Hz gentle pulsation

    for i in range(num_samples):
        t = i / SAMPLE_RATE
        # LFO modulation
        lfo = 0.85 + 0.15 * math.sin(2.0 * math.pi * lfo_rate * t)
        
        # Sub harmonic
        s_sub = 0.35 * math.sin(2.0 * math.pi * (f0 * 0.5) * t)
        # Main harmonic
        s_main = 0.50 * math.sin(2.0 * math.pi * f0 * t)
        # 2nd harmonic with slight phase shift
        s_harm1 = 0.25 * math.sin(2.0 * math.pi * f1 * t + 0.5)
        # 3rd harmonic
        s_harm2 = 0.12 * math.sin(2.0 * math.pi * f2 * t + 1.2)
        # subtle high-frequency magnetic resonance
        s_res = 0.04 * math.sin(2.0 * math.pi * (f0 * 8.0) * t + math.sin(2.0 * math.pi * 6.0 * t))

        total = (s_sub + s_main + s_harm1 + s_harm2 + s_res) * lfo * 0.70
        
        # Smooth window at start/end for seamless looping
        fade_len = int(SAMPLE_RATE * 0.05)
        if i < fade_len:
            total *= (i / fade_len)
        elif i > num_samples - fade_len:
            total *= ((num_samples - i) / fade_len)

        samples.append(total)

    write_wav(filename, samples)

def generate_drone_fetch_alert(filename, duration=0.65):
    # Cheerful ascending cybernetic chirp: 880 Hz -> 1320 Hz -> 1760 Hz with bell envelope
    num_samples = int(SAMPLE_RATE * duration)
    samples = []

    # Two-stage melodic chirp: first chirp (0 to 0.22s), second chirp (0.24 to 0.55s)
    phase = 0.0
    for i in range(num_samples):
        t = i / SAMPLE_RATE
        
        if t < 0.20:
            # First chirp (F5 ~ 698Hz -> A5 ~ 880Hz)
            progress = t / 0.20
            freq = 698.0 + 182.0 * progress
            env = math.sin(math.pi * progress) ** 0.8
            phase += 2.0 * math.pi * freq / SAMPLE_RATE
            val = math.sin(phase) + 0.3 * math.sin(phase * 2.0)
            sample = val * env * 0.65
        elif t < 0.24:
            # Short gap
            sample = 0.0
            phase = 0.0
        elif t < 0.58:
            # Second higher chirp (A5 ~ 880Hz -> E6 ~ 1320Hz)
            t_sub = t - 0.24
            progress = t_sub / 0.34
            freq = 880.0 + 440.0 * (progress ** 0.7)
            env = (math.sin(math.pi * progress) ** 0.7) * math.exp(-progress * 1.5)
            phase += 2.0 * math.pi * freq / SAMPLE_RATE
            val = math.sin(phase) + 0.35 * math.sin(phase * 2.0) + 0.15 * math.sin(phase * 3.0)
            sample = val * env * 0.75
        else:
            sample = 0.0

        samples.append(sample)

    write_wav(filename, samples)

if __name__ == "__main__":
    out_dir = os.path.abspath("Assets/Audio/Generated/Task03")
    generate_tractor_beam_hum(os.path.join(out_dir, "tractor_beam_hum.wav"))
    generate_drone_fetch_alert(os.path.join(out_dir, "drone_fetch_alert.wav"))
    print("ALL TASK-03 AUDIO GENERATED SUCCESSFULLY!")
