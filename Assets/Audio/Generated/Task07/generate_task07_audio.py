import math
import struct
import wave
import os
import random

SAMPLE_RATE = 44100

def write_wav(filename, samples):
    with wave.open(filename, 'w') as wav_file:
        wav_file.setnchannels(1)  # Mono
        wav_file.setsampwidth(2)  # 16-bit
        wav_file.setframerate(SAMPLE_RATE)
        
        # Normalize
        max_val = max(abs(s) for s in samples) if samples else 1.0
        if max_val == 0:
            max_val = 1.0
        scale = 32767.0 * 0.94 / max_val
        
        packed_frames = bytearray()
        for s in samples:
            val = int(s * scale)
            val = max(-32768, min(32767, val))
            packed_frames.extend(struct.pack('<h', val))
        wav_file.writeframes(packed_frames)
    print(f"Generated {filename} ({len(samples)/SAMPLE_RATE:.2f}s)")

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
        
        sample = (hiss * 0.40 + stretch_tone * 0.35 + squeak * 0.45) * env
        output[i] = sample
        
    return output

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

def synthesize_pop_loud():
    """
    Synthesize an energetic, realistic, satisfying balloon pop!
    Instant supersonic pressure impulse (<2ms), followed by high-frequency rubber shred crack,
    and resonant room air expansion thud.
    """
    duration = 0.50
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    random.seed(777)
    
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        
        # 1. Initial Supersonic Shock Impulse (0.0 to 0.003s)
        shock = 0.0
        if t < 0.003:
            # Rapid positive spike then sharp negative recoil
            phase_shock = t / 0.003
            shock = math.sin(phase_shock * math.pi) * 1.5
            if phase_shock > 0.5:
                shock = -1.2 * math.sin((phase_shock - 0.5) * 2.0 * math.pi)
        
        # 2. High-Frequency Rubber Snapping Crack (burst of noise & high harmonics)
        crack_env = math.exp(-t * 45.0)
        crack_noise = (random.random() * 2.0 - 1.0) * crack_env * 0.95
        crack_tone = math.sin(2.0 * math.pi * 2200.0 * t) * crack_env * 0.4
        
        # 3. Low-Frequency Pressure Expansion Thud (~110 Hz decaying)
        thud_env = math.exp(-t * 14.0)
        thud_freq = 135.0 * math.exp(-t * 8.0) + 65.0
        thud = math.sin(2.0 * math.pi * thud_freq * t) * thud_env * 0.8
        
        # 4. Room Reverb Tail (diffuse decaying noise)
        tail_env = math.exp(-t * 7.0) * 0.15
        tail_noise = (random.random() * 2.0 - 1.0) * tail_env
        
        sample = shock * 0.7 + crack_noise * 0.45 + crack_tone * 0.25 + thud * 0.45 + tail_noise * 0.2
        output[i] = sample
        
    return output

def main():
    target_dir = os.path.dirname(os.path.abspath(__file__))
    
    f1 = os.path.join(target_dir, "balloon_inflate_stretch.wav")
    f2 = os.path.join(target_dir, "balloon_release_whoosh.wav")
    f3 = os.path.join(target_dir, "balloon_pop_loud.wav")
    
    write_wav(f1, synthesize_inflate_stretch())
    write_wav(f2, synthesize_release_whoosh())
    write_wav(f3, synthesize_pop_loud())
    
    print("All Task07 procedural audio files successfully generated!")

if __name__ == "__main__":
    main()
