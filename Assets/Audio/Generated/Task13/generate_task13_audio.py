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
        
        # Normalize to -0.6 dB
        max_val = max(abs(s) for s in samples) if samples else 1.0
        if max_val == 0:
            max_val = 1.0
        scale = 32767.0 * 0.93 / max_val
        
        packed_frames = bytearray()
        for s in samples:
            val = int(s * scale)
            val = max(-32768, min(32767, val))
            packed_frames.extend(struct.pack('<h', val))
        wav_file.writeframes(packed_frames)
    print(f"Generated {filename} ({len(samples)/SAMPLE_RATE:.2f}s)")

def synthesize_marimba_bar(freq, duration=0.85):
    """
    Synthesizes a realistic warm wooden marimba tone.
    Features:
    - Initial mallet transient strike (soft wooden clack)
    - Resonant fundamental with exponential decay
    - Harmonics characteristic of marimba tuning (fundamental, 4th harmonic / double octave overtone)
    """
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    # Random mallet impulse seed
    random.seed(int(freq * 100))
    mallet_samples = int(SAMPLE_RATE * 0.015)
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        
        # Mallet click / tap transient
        mallet = 0.0
        if i < mallet_samples:
            m_env = (1.0 - i / mallet_samples) ** 2
            mallet = (random.random() * 2.0 - 1.0) * 0.4 * m_env
            mallet += math.sin(2.0 * math.pi * freq * 3.98 * t) * 0.5 * m_env
            
        # Fundamental tone
        env_fund = math.exp(-3.8 * t)
        tone_fund = math.sin(2.0 * math.pi * freq * t) * env_fund
        
        # Second harmonic (octave overtone, subtle)
        env_h2 = math.exp(-7.0 * t)
        tone_h2 = math.sin(2.0 * math.pi * freq * 2.0 * t) * 0.30 * env_h2
        
        # Third harmonic
        env_h3 = math.exp(-12.0 * t)
        tone_h3 = math.sin(2.0 * math.pi * freq * 3.0 * t) * 0.12 * env_h3
        
        # Wooden resonator body mode (~3.98x fundamental, rapid decay)
        env_res = math.exp(-16.0 * t)
        tone_res = math.sin(2.0 * math.pi * freq * 3.98 * t) * 0.25 * env_res
        
        output[i] = mallet + tone_fund + tone_h2 + tone_h3 + tone_res
        
    return output

def synthesize_lantern_ignite(duration=0.65):
    """
    A mystical warm flame ignition whoosh with resonant thermal expansion and crackle.
    """
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    random.seed(1301)
    
    b0, b1 = 0.0, 0.0
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        # Envelope: soft attack, peak at 0.18s, gentle dissipation
        if t < 0.18:
            env = (t / 0.18) ** 1.8
        else:
            env = math.exp(-4.5 * (t - 0.18))
            
        # Frequency sweep of expanding heat wave
        freq = 160.0 + 380.0 * math.sin(math.pi * min(1.0, t / 0.55))
        core_tone = math.sin(2.0 * math.pi * freq * t + 0.3 * math.sin(2.0 * math.pi * 32.0 * t)) * 0.5
        
        # Lowpass filtered noise whoosh
        white = random.random() * 2.0 - 1.0
        b0 = 0.85 * b0 + 0.15 * white
        b1 = 0.85 * b1 + 0.15 * b0
        whoosh = b1 * 1.2
        
        # Tiny sparkling ember crackles
        crackle = 0.0
        if random.random() < 0.04 and t > 0.10:
            crackle = (random.random() * 2.0 - 1.0) * (1.0 - t / duration) * 0.8
            
        output[i] = (core_tone * 0.6 + whoosh * 0.8 + crackle * 0.3) * env
        
    return output

def synthesize_bird_chirp(duration=0.48):
    """
    Cheerful melodic double bird chirp (rouxinol / bluebird).
    Chirp 1: quick cheerful ascending flick
    Chirp 2: longer melodic flourish with vibrato
    """
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    phase = 0.0
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        val = 0.0
        
        # Chirp 1: 0.0s to 0.13s
        if t < 0.13:
            p = t / 0.13
            env = math.sin(math.pi * p) ** 1.5
            freq = 2800.0 + 1500.0 * (p ** 1.2)
            phase += 2.0 * math.pi * freq / SAMPLE_RATE
            val = math.sin(phase) * env
            val += math.sin(phase * 2.0) * 0.18 * env  # Air harmonic
            
        # Pause: 0.13s to 0.18s
        elif t < 0.18:
            phase = 0.0
            val = 0.0
            
        # Chirp 2: 0.18s to 0.44s
        elif t < 0.44:
            p = (t - 0.18) / 0.26
            env = (math.sin(math.pi * p) ** 1.2) * math.exp(-1.5 * p)
            # Melodic contour: rises, dips, soft vibrato
            vibrato = math.sin(2.0 * math.pi * 26.0 * (t - 0.18)) * 120.0
            contour = 3200.0 + 1800.0 * math.sin(math.pi * p) + vibrato
            phase += 2.0 * math.pi * contour / SAMPLE_RATE
            val = math.sin(phase) * env
            val += math.sin(phase * 2.0) * 0.22 * env
            
        output[i] = val * 0.95
        
    return output

def synthesize_wing_flutter(duration=0.52):
    """
    Soft flutter of low-poly bird wings taking flight.
    A rhythmic sequence of 5 gentle air whooshes.
    """
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    random.seed(1302)
    
    flutter_count = 5
    b0, b1 = 0.0, 0.0
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        
        # Wing beat pulsation at ~14 Hz
        wing_phase = (t * 14.0) % 1.0
        pulse_env = math.sin(math.pi * wing_phase) ** 3.0
        overall_env = math.sin(math.pi * (t / duration)) ** 1.2
        
        # Soft low aerodynamic woosh (220 Hz - 400 Hz)
        woosh_f = 240.0 + 160.0 * math.sin(math.pi * wing_phase)
        sine_air = math.sin(2.0 * math.pi * woosh_f * t) * 0.4
        
        # Filtered pink feather noise
        white = random.random() * 2.0 - 1.0
        b0 = 0.82 * b0 + 0.18 * white
        b1 = 0.82 * b1 + 0.18 * b0
        
        output[i] = (sine_air * 0.5 + b1 * 0.8) * pulse_env * overall_env
        
    return output

if __name__ == '__main__':
    out_dir = r"D:\Alunos\Alexandre\Projeto IC\Assets\Audio\Generated\Task13"
    os.makedirs(out_dir, exist_ok=True)
    
    # 5 pentatonic notes
    pentatonic_notes = [
        ("marimba_C4.wav", 261.63),
        ("marimba_D4.wav", 293.66),
        ("marimba_E4.wav", 329.63),
        ("marimba_G4.wav", 392.00),
        ("marimba_A4.wav", 440.00),
    ]
    
    for filename, freq in pentatonic_notes:
        samples = synthesize_marimba_bar(freq)
        write_wav(os.path.join(out_dir, filename), samples)
        
    write_wav(os.path.join(out_dir, "lantern_ignite_whoosh.wav"), synthesize_lantern_ignite())
    write_wav(os.path.join(out_dir, "bird_chirp_cheerful.wav"), synthesize_bird_chirp())
    write_wav(os.path.join(out_dir, "bird_wing_flutter.wav"), synthesize_wing_flutter())
    
    print("All Task 13 audio assets synthesized successfully.")
