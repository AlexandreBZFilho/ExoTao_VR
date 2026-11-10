import math
import struct
import wave
import os
import random

SAMPLE_RATE = 44100

def write_wav(filename, samples):
    os.makedirs(os.path.dirname(filename), exist_ok=True)
    with wave.open(filename, 'w') as wav_file:
        wav_file.setnchannels(1)  # Mono
        wav_file.setsampwidth(2)  # 16-bit
        wav_file.setframerate(SAMPLE_RATE)
        
        # Normalize
        max_val = max(abs(s) for s in samples) if samples else 1.0
        if max_val == 0:
            max_val = 1.0
        scale = 32767.0 * 0.92 / max_val
        
        packed_frames = bytearray()
        for s in samples:
            val = int(s * scale)
            val = max(-32768, min(32767, val))
            packed_frames.extend(struct.pack('<h', val))
        wav_file.writeframes(packed_frames)
    print(f"Generated {filename} ({len(samples)/SAMPLE_RATE:.2f}s)")

def synthesize_basketball_bounce():
    """
    Hollow rubber basketball bouncing on floor/rim.
    Fundamental thud at 110Hz decaying fast, hollow body resonance at 240Hz, snappy transient.
    """
    duration = 0.35
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    random.seed(101)
    phase_f0 = 0.0
    phase_res = 0.0
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        # Decay envelopes
        env_thud = math.exp(-22.0 * t)
        env_cavity = math.exp(-14.0 * t)
        env_snap = math.exp(-65.0 * t)
        
        # Frequency pitch drop slightly on impact compression
        f0 = 125.0 - 35.0 * (t / duration)
        f_res = 260.0 - 40.0 * (t / duration)
        
        phase_f0 += 2.0 * math.pi * f0 / SAMPLE_RATE
        phase_res += 2.0 * math.pi * f_res / SAMPLE_RATE
        
        snap = (random.random() * 2.0 - 1.0) * env_snap * 0.5
        thud = math.sin(phase_f0) * env_thud * 0.9
        cavity = math.sin(phase_res) * env_cavity * 0.4
        
        output[i] = thud + cavity + snap
        
    return output

def synthesize_hoop_swish():
    """
    Crisp friction of nylon net cords stretching and rubbing as basketball passes cleanly through.
    """
    duration = 0.45
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    random.seed(202)
    b0, b1, b2, b3 = 0.0, 0.0, 0.0, 0.0
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        
        # Double wave envelope: entering net, then popping through bottom
        env1 = math.exp(-((t - 0.10) / 0.06) ** 2)
        env2 = 0.85 * math.exp(-((t - 0.22) / 0.07) ** 2)
        env = env1 + env2
        
        white = random.random() * 2.0 - 1.0
        
        # Bandpass filter around 1800-3200Hz
        b0 = 0.30 * white + 0.70 * b0
        b1 = b0 - b2
        b2 = 0.35 * b0 + 0.65 * b2
        
        # High nylon cord rustle
        rustle = math.sin(t * 2.0 * math.pi * 1450.0 + math.sin(t * 300.0) * 1.5) * 0.2
        
        output[i] = (b1 * 0.85 + rustle) * env
        
    return output

def synthesize_hoop_rim_clang():
    """
    Resonant metallic ring of steel rim struck by basketball.
    Harmonics: 460Hz, 1180Hz, 2150Hz with metallic flutter.
    """
    duration = 0.38
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    random.seed(303)
    p1, p2, p3 = 0.0, 0.0, 0.0
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        
        env_impact = math.exp(-60.0 * t)
        env_ring1 = math.exp(-11.0 * t)
        env_ring2 = math.exp(-15.0 * t)
        env_ring3 = math.exp(-25.0 * t)
        
        p1 += 2.0 * math.pi * 465.0 / SAMPLE_RATE
        p2 += 2.0 * math.pi * 1190.0 / SAMPLE_RATE
        p3 += 2.0 * math.pi * 2180.0 / SAMPLE_RATE
        
        impact = (random.random() * 2.0 - 1.0) * env_impact * 0.6
        m1 = math.sin(p1) * env_ring1 * 0.65
        m2 = math.sin(p2) * env_ring2 * 0.40
        m3 = math.sin(p3) * env_ring3 * 0.25
        
        output[i] = impact + m1 + m2 + m3
        
    return output

def synthesize_arcade_buzzer():
    """
    Classic arcade high-energy score buzzer horn.
    Rich sawtooth-like harmonics at 230Hz and 460Hz with sharp punch.
    """
    duration = 0.55
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    p = 0.0
    f0 = 233.08  # Bb3
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        
        # Envelope: crisp attack (0.01s), steady sustain, then quick fade
        if t < 0.01:
            env = t / 0.01
        elif t > duration - 0.08:
            env = (duration - t) / 0.08
        else:
            env = 1.0
            
        p += f0 / SAMPLE_RATE
        p_fract = p - math.floor(p)
        
        # Sawtooth wave + square subharmonic
        saw = (2.0 * p_fract - 1.0)
        square = 1.0 if (p_fract < 0.5) else -1.0
        
        # Blend
        tone = 0.65 * saw + 0.35 * square
        output[i] = tone * env
        
    return output

def synthesize_arcade_button_press():
    """
    Mechanical microswitch click of an arcade button.
    Sharp plastic tactile click (2400Hz transient) + spring body thump (190Hz).
    """
    duration = 0.16
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    random.seed(505)
    p_body = 0.0
    p_click = 0.0
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        
        env_click = math.exp(-75.0 * t)
        env_body = math.exp(-32.0 * t)
        
        p_click += 2.0 * math.pi * 2300.0 / SAMPLE_RATE
        p_body += 2.0 * math.pi * 185.0 / SAMPLE_RATE
        
        click = math.sin(p_click) * env_click * 0.75 + (random.random() * 2.0 - 1.0) * env_click * 0.4
        body = math.sin(p_body) * env_body * 0.55
        
        output[i] = click + body
        
    return output

if __name__ == "__main__":
    out_dir = os.path.abspath("Assets/Audio/Generated/Task08")
    os.makedirs(out_dir, exist_ok=True)
    
    write_wav(os.path.join(out_dir, "basketball_bounce.wav"), synthesize_basketball_bounce())
    write_wav(os.path.join(out_dir, "hoop_swish.wav"), synthesize_hoop_swish())
    write_wav(os.path.join(out_dir, "hoop_rim_clang.wav"), synthesize_hoop_rim_clang())
    write_wav(os.path.join(out_dir, "arcade_buzzer.wav"), synthesize_arcade_buzzer())
    write_wav(os.path.join(out_dir, "arcade_button_press.wav"), synthesize_arcade_button_press())
    print("ALL TASK-08 SFX GENERATED SUCCESSFULLY!")
