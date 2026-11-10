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
        
        # Normalize to -0.8 dB
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

def synthesize_tree_leaves_rustle():
    """
    Organic voluminous rustle of tree canopy leaves being vigorously shaken.
    Layered filtered pink/brown noise with low-frequency swooshes and micro crackles.
    """
    duration = 1.5
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    random.seed(1101)
    b0, b1, b2 = 0.0, 0.0, 0.0
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        # Envelope: quick attack, sustained flutter, smooth decay
        if t < 0.15:
            env = t / 0.15
        elif t < 0.9:
            env = 1.0 - 0.25 * ((t - 0.15) / 0.75) + 0.15 * math.sin(2.0 * math.pi * 7.5 * t)
        else:
            env = 0.75 * math.exp(-6.0 * (t - 0.9))
            
        # Leaf flutter modulation
        flutter = 0.7 + 0.3 * math.sin(2.0 * math.pi * 12.0 * t + 0.4 * math.sin(2.0 * math.pi * 3.0 * t))
        
        # White noise source
        white = random.random() * 2.0 - 1.0
        
        # Bandpass filter around foliage frequencies (350 - 1200 Hz)
        center_f = 450.0 + 350.0 * math.sin(2.0 * math.pi * 2.5 * t)
        coeff = min(0.65, max(0.08, 2.0 * math.pi * center_f / SAMPLE_RATE))
        b0 = coeff * white + (1.0 - coeff) * b0
        b1 = coeff * b0 + (1.0 - coeff) * b1
        b2 = coeff * b1 + (1.0 - coeff) * b2
        
        # Branch creak / wood sway undertone (subtle 140 Hz)
        wood = 0.12 * math.sin(2.0 * math.pi * 135.0 * t) * math.exp(-2.5 * t)
        
        # Occasional leaf snap / twig crackle
        crackle = 0.0
        if random.random() < 0.008 and t < 1.0:
            crackle = (random.random() * 2.0 - 1.0) * 0.4
            
        output[i] = (b2 * 1.8 + crackle + wood) * env * flutter
        
    return output

def synthesize_apple_drop_thud():
    """
    Soft organic impact of a crisp, heavy fresh fruit landing on grass / loam.
    Damped sine transient (125 Hz down to 60 Hz) + organic thud resonance.
    """
    duration = 0.45
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    random.seed(1102)
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        
        env_impact = math.exp(-42.0 * t)
        env_body = math.exp(-18.0 * t)
        
        # Pitch drops quickly on contact
        freq = 65.0 + 75.0 * math.exp(-35.0 * t)
        thud = math.sin(2.0 * math.pi * freq * t) * env_impact * 1.1
        body = math.sin(2.0 * math.pi * 85.0 * t) * env_body * 0.6
        
        # Low frequency grass rustle on contact
        grass_noise = (random.random() * 2.0 - 1.0) * math.exp(-30.0 * t) * 0.35
        
        output[i] = thud + body + grass_noise
        
    return output

def synthesize_basket_score_bell():
    """
    Bright celebratory chime / score bell when apple lands into the wicker basket.
    Major chord arpeggio / bell harmonic (C6 1046.5Hz, E6 1318.5Hz, G6 1568Hz, C7 2093Hz)
    with shimmering decay.
    """
    duration = 1.6
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    harmonics = [
        (1046.50, 0.90, 3.5),
        (1318.51, 0.75, 4.0),
        (1567.98, 0.85, 4.2),
        (2093.00, 0.95, 5.0),
        (2637.02, 0.40, 6.0),
        (3135.96, 0.30, 7.0),
    ]
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        sample = 0.0
        
        # Initial wooden basket swish / thud contact
        basket_wood = math.sin(2.0 * math.pi * 220.0 * t) * math.exp(-55.0 * t) * 0.4
        
        for freq, amp, decay in harmonics:
            env = math.exp(-decay * t)
            # Subtle vibrato / bell beating
            mod = 1.0 + 0.003 * math.sin(2.0 * math.pi * 6.5 * t)
            sample += math.sin(2.0 * math.pi * freq * mod * t) * amp * env
            
        output[i] = sample + basket_wood
        
    return output

def synthesize_water_splash():
    """
    Realistic water splash as apple hits the river.
    High transient entry pop, burst of bubbling noise, and resonant liquid slosh.
    """
    duration = 1.2
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    random.seed(1104)
    lp0, lp1 = 0.0, 0.0
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        
        # Quick surface rupture pop (0 to 0.04s)
        pop_env = math.exp(-75.0 * t)
        pop = math.sin(2.0 * math.pi * (280.0 - 150.0 * t) * t) * pop_env * 0.8
        
        # Bubble / droplet sprays
        spray_env = (t / 0.08) * math.exp(-8.0 * t) if t < 0.08 else math.exp(-8.0 * (t - 0.08))
        white = random.random() * 2.0 - 1.0
        
        # Resonant water bubble frequencies
        bubble1 = math.sin(2.0 * math.pi * (420.0 + 200.0 * math.sin(35.0 * t)) * t) * math.exp(-12.0 * t) * 0.4
        bubble2 = math.sin(2.0 * math.pi * (680.0 + 150.0 * math.cos(42.0 * t)) * t) * math.exp(-15.0 * t) * 0.35
        
        # Lowpass slosh
        lp_coeff = 0.12
        lp0 = lp_coeff * white + (1.0 - lp_coeff) * lp0
        lp1 = lp_coeff * lp0 + (1.0 - lp_coeff) * lp1
        slosh = lp1 * spray_env * 1.2
        
        output[i] = pop + bubble1 + bubble2 + slosh
        
    return output

def synthesize_golden_apple_sparkle():
    """
    Mythic shimmering chime for the Golden Bonus Apple.
    Ascending celestial crystalline glissando with stereo tremolo and sparkle.
    """
    duration = 1.8
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    notes = [1174.66, 1479.98, 1760.00, 2217.46, 2637.02, 3135.96]
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        env_overall = math.sin(min(1.0, t / 0.3) * math.pi * 0.5) * math.exp(-1.8 * t)
        
        sample = 0.0
        for idx, base_f in enumerate(notes):
            # Staggered chime entry
            stagger_t = t - idx * 0.12
            if stagger_t > 0:
                note_env = math.exp(-5.0 * stagger_t)
                shimmer = math.sin(2.0 * math.pi * (base_f + 12.0 * math.sin(18.0 * t)) * stagger_t)
                sample += shimmer * note_env * 0.35
                
        # Subtle harmonic golden halo
        halo = math.sin(2.0 * math.pi * 880.0 * t) * math.exp(-1.5 * t) * 0.2
        output[i] = (sample + halo) * env_overall
        
    return output

if __name__ == '__main__':
    out_dir = r"Assets/Audio/Generated/Task11"
    os.makedirs(out_dir, exist_ok=True)
    
    write_wav(os.path.join(out_dir, "tree_leaves_rustle.wav"), synthesize_tree_leaves_rustle())
    write_wav(os.path.join(out_dir, "apple_drop_thud.wav"), synthesize_apple_drop_thud())
    write_wav(os.path.join(out_dir, "basket_score_bell.wav"), synthesize_basket_score_bell())
    write_wav(os.path.join(out_dir, "water_splash.wav"), synthesize_water_splash())
    write_wav(os.path.join(out_dir, "golden_apple_sparkle.wav"), synthesize_golden_apple_sparkle())
    print("All Task 11 procedural audio generated successfully.")
