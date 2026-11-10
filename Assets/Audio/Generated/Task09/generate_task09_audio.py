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

def synthesize_lever_click():
    """
    Heavy mechanical detent notch click:
    Snappy metal strike (850Hz), solid lever chassis thud (180Hz), and mechanical ring.
    """
    duration = 0.28
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    random.seed(301)
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        
        # Envelopes
        env_snap = math.exp(-65.0 * t)
        env_body = math.exp(-28.0 * t)
        env_chime = math.exp(-14.0 * t)
        
        # Components
        noise = (random.random() * 2.0 - 1.0) * env_snap * 0.4
        thud = math.sin(2.0 * math.pi * 175.0 * t) * env_body * 0.8
        click = math.sin(2.0 * math.pi * 920.0 * t + math.sin(2.0 * math.pi * 320.0 * t) * 1.5) * env_snap * 0.9
        ring = math.sin(2.0 * math.pi * 1840.0 * t) * env_chime * 0.25
        
        output[i] = thud + click + noise + ring
        
    return output

def synthesize_atmosphere_whoosh():
    """
    Ethereal wind sweep with subtle celestial resonant harmonic swell.
    """
    duration = 1.35
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    random.seed(402)
    b0, b1, b2 = 0.0, 0.0, 0.0
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        # Smooth bell envelope
        p = t / duration
        env = math.sin(p * math.pi) ** 1.6
        
        white = random.random() * 2.0 - 1.0
        # Swept bandpass filter
        center_f = 350.0 + 800.0 * math.sin(p * math.pi)
        coeff = min(0.85, max(0.15, 2.0 * math.pi * center_f / SAMPLE_RATE))
        b0 = coeff * white + (1.0 - coeff) * b0
        b1 = coeff * b0 + (1.0 - coeff) * b1
        b2 = b0 - b1
        
        # Soft musical chord harmonics (A major 9th ethereal shimmer)
        h1 = math.sin(2.0 * math.pi * 440.0 * t) * 0.12
        h2 = math.sin(2.0 * math.pi * 659.25 * t) * 0.08
        h3 = math.sin(2.0 * math.pi * 880.0 * t) * 0.05
        
        output[i] = (b2 * 1.2 + h1 + h2 + h3) * env
        
    return output

def synthesize_fireworks_whistle():
    """
    Ascending rocket propellant whistle (800Hz -> 2600Hz) with hissing exhaust.
    """
    duration = 0.75
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    random.seed(503)
    phase = 0.0
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        p = t / duration
        
        # Pitch rises quadratically
        f = 850.0 + 1750.0 * (p ** 1.4)
        phase += 2.0 * math.pi * f / SAMPLE_RATE
        
        # Envelope: quick rise, sustaining, trailing into explosion
        env = min(1.0, t / 0.06) * (1.0 - 0.25 * p)
        
        hiss = (random.random() * 2.0 - 1.0) * 0.28 * env
        whistle = math.sin(phase) * 0.75 * env
        second_harm = math.sin(phase * 2.0) * 0.20 * env
        
        output[i] = whistle + second_harm + hiss
        
    return output

def synthesize_fireworks_burst():
    """
    Massive firework explosion: deep boom, shattering transient, and crackling sparkle falloff.
    """
    duration = 1.40
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    random.seed(604)
    phase_sub = 0.0
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        
        # Sub-bass thump (drops from 110Hz to 45Hz)
        f_sub = 110.0 - 65.0 * min(1.0, t / 0.45)
        phase_sub += 2.0 * math.pi * f_sub / SAMPLE_RATE
        env_sub = math.exp(-7.5 * t)
        sub = math.sin(phase_sub) * env_sub * 0.95
        
        # Explosive impact burst (noise)
        env_impact = math.exp(-24.0 * t)
        impact = (random.random() * 2.0 - 1.0) * env_impact * 1.0
        
        # Sparkle crackles after explosion (0.15s to 1.3s)
        sparkles = 0.0
        if 0.12 < t < 1.30:
            env_sparkles = math.exp(-2.2 * (t - 0.12))
            if random.random() < (0.018 * env_sparkles):
                sparkles = (random.random() * 2.0 - 1.0) * 0.85
                
        output[i] = sub + impact + sparkles
        
    return output

def synthesize_bobblehead_boing():
    """
    Classic cartoon spring recoil 'BOING' oscillating at 13.5Hz with metallic harmonics.
    """
    duration = 0.85
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    random.seed(705)
    phase_spring = 0.0
    phase_carrier = 0.0
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        env = math.exp(-4.2 * t)
        
        # Spring wobble modulation (14Hz)
        wobble = math.sin(2.0 * math.pi * 14.0 * t)
        
        # Carrier starts at 360Hz and modulates with wobble
        fc = 340.0 + 90.0 * wobble * env
        phase_carrier += 2.0 * math.pi * fc / SAMPLE_RATE
        
        # Metallic high overtone
        f_overtone = 780.0 + 140.0 * wobble * env
        phase_spring += 2.0 * math.pi * f_overtone / SAMPLE_RATE
        
        carrier = math.sin(phase_carrier) * 0.70
        metal = math.sin(phase_spring) * 0.35
        twang = math.sin(phase_carrier * 0.5) * 0.25
        
        output[i] = (carrier + metal + twang) * env
        
    return output

if __name__ == '__main__':
    out_dir = r"D:\Alunos\Alexandre\Projeto IC\Assets\Audio\Generated\Task09"
    os.makedirs(out_dir, exist_ok=True)
    
    write_wav(os.path.join(out_dir, "lever_click_notch.wav"), synthesize_lever_click())
    write_wav(os.path.join(out_dir, "atmosphere_transition_whoosh.wav"), synthesize_atmosphere_whoosh())
    write_wav(os.path.join(out_dir, "fireworks_launch_whistle.wav"), synthesize_fireworks_whistle())
    write_wav(os.path.join(out_dir, "fireworks_burst_explosion.wav"), synthesize_fireworks_burst())
    write_wav(os.path.join(out_dir, "bobblehead_spring_boing.wav"), synthesize_bobblehead_boing())
    print("All Task09 audio synthesized successfully!")
