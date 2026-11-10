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
        scale = 32767.0 * 0.94 / max_val
        
        packed_frames = bytearray()
        for s in samples:
            val = int(s * scale)
            val = max(-32768, min(32767, val))
            packed_frames.extend(struct.pack('<h', val))
        wav_file.writeframes(packed_frames)
    print(f"Generated {filename} ({len(samples)/SAMPLE_RATE:.2f}s)")

def synthesize_trophy_unlock_fanfare():
    """
    Glorious achievement fanfare:
    Warm triumphant brass arpeggio (C4 -> E4 -> G4 -> C5 -> E5 -> G5)
    followed by a ringing celestial bell chord and shimmering harp glissando.
    """
    duration = 2.4
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    # Arpeggio note triggers: (time, freq, duration, amp)
    notes = [
        (0.00, 261.63, 0.40, 0.70), # C4
        (0.14, 329.63, 0.40, 0.75), # E4
        (0.28, 392.00, 0.45, 0.80), # G4
        (0.44, 523.25, 0.55, 0.90), # C5
        (0.62, 659.25, 0.60, 0.95), # E5
        (0.82, 783.99, 1.50, 1.10), # G5 hold
        (0.82, 523.25, 1.50, 0.85), # C5 harmonic support
        (0.82, 1046.50, 1.45, 0.65) # C6 bright peak
    ]
    
    for start_t, freq, note_dur, amp in notes:
        start_idx = int(start_t * SAMPLE_RATE)
        end_idx = min(total_samples, start_idx + int(note_dur * SAMPLE_RATE))
        phase = 0.0
        for i in range(start_idx, end_idx):
            t = (i - start_idx) / SAMPLE_RATE
            # Attack-Decay envelope
            env_att = min(1.0, t / 0.025)
            env_dec = math.exp(-2.2 * t / note_dur)
            env = env_att * env_dec * amp
            
            # Brass-like harmonic overtone mix: fundamental + 2nd + 3rd + slight vibrato
            vibrato = 1.0 + 0.006 * math.sin(2.0 * math.pi * 5.5 * t) if t > 0.15 else 1.0
            cur_f = freq * vibrato
            phase += 2.0 * math.pi * cur_f / SAMPLE_RATE
            
            sig = (math.sin(phase) +
                   0.45 * math.sin(phase * 2.0) +
                   0.25 * math.sin(phase * 3.0) +
                   0.12 * math.sin(phase * 4.0))
            output[i] += sig * env
            
    # Add sparkle / celestial chimes at climax (t >= 0.82)
    random.seed(42)
    chime_freqs = [1318.51, 1567.98, 2093.00, 2637.02, 3135.96]
    for k, cf in enumerate(chime_freqs):
        c_start = 0.85 + k * 0.08
        c_idx = int(c_start * SAMPLE_RATE)
        c_dur = 1.2
        c_end = min(total_samples, c_idx + int(c_dur * SAMPLE_RATE))
        c_phase = random.uniform(0, math.pi)
        for i in range(c_idx, c_end):
            ct = (i - c_idx) / SAMPLE_RATE
            c_env = min(1.0, ct / 0.005) * math.exp(-4.5 * ct) * 0.35
            c_phase += 2.0 * math.pi * cf / SAMPLE_RATE
            output[i] += math.sin(c_phase) * c_env
            
    return output

def synthesize_trophy_pickup_shimmer():
    """
    Crisp crystalline harmonic shimmer with resonant bell when the player picks up a trophy.
    Pure bell resonance at 880Hz + ethereal FM shimmer overtone + soft tactile pop.
    """
    duration = 0.95
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    # Tactile pop at start
    for i in range(min(total_samples, int(0.04 * SAMPLE_RATE))):
        t = i / SAMPLE_RATE
        env_pop = math.exp(-85.0 * t)
        output[i] += math.sin(2.0 * math.pi * 140.0 * t) * env_pop * 0.45
        
    # Main crystalline bell frequencies (A5 880Hz, C#6 1108Hz, E6 1318Hz, A6 1760Hz)
    bell_partials = [
        (880.00, 0.85, 3.2),
        (1108.73, 0.65, 3.8),
        (1318.51, 0.50, 4.2),
        (1760.00, 0.40, 5.0),
        (2637.02, 0.25, 6.5)
    ]
    
    for freq, amp, decay_rate in bell_partials:
        phase = 0.0
        for i in range(total_samples):
            t = i / SAMPLE_RATE
            env = min(1.0, t / 0.003) * math.exp(-decay_rate * t) * amp
            phase += 2.0 * math.pi * freq / SAMPLE_RATE
            output[i] += math.sin(phase) * env
            
    # FM Shimmer flutter (carrier 1760Hz modulated by 18Hz)
    fm_phase_c = 0.0
    fm_phase_m = 0.0
    for i in range(total_samples):
        t = i / SAMPLE_RATE
        env_shimmer = min(1.0, t / 0.03) * math.exp(-3.5 * t) * 0.28
        fm_phase_m += 2.0 * math.pi * 18.0 / SAMPLE_RATE
        mod = 120.0 * math.sin(fm_phase_m)
        fm_phase_c += 2.0 * math.pi * (1760.0 + mod) / SAMPLE_RATE
        output[i] += math.sin(fm_phase_c) * env_shimmer
        
    return output

def synthesize_crown_equip_royal():
    """
    Majestic royal fanfare and coronation jingle:
    Imperial trumpets fanfare (D5 -> F#5 -> A5 -> D6) with warm vibrato,
    followed by ascending coronation harp glissando and joyful sparkle chime.
    """
    duration = 2.8
    total_samples = int(SAMPLE_RATE * duration)
    output = [0.0] * total_samples
    
    # Regal trumpet fanfare motives:
    # 0.00s: D5 (587.33Hz) short
    # 0.15s: D5 (587.33Hz) short
    # 0.30s: D5 (587.33Hz) short
    # 0.45s: G5 (783.99Hz) held
    # 0.80s: F#5 (739.99Hz)
    # 0.98s: A5 (880.00Hz)
    # 1.20s: D6 (1174.66Hz) glorious royal resolution chord
    motifs = [
        (0.00, 587.33, 0.12, 0.70),
        (0.14, 587.33, 0.12, 0.75),
        (0.28, 587.33, 0.14, 0.80),
        (0.44, 783.99, 0.34, 0.95),
        (0.78, 739.99, 0.20, 0.85),
        (0.98, 880.00, 0.24, 0.90),
        (1.22, 1174.66, 1.50, 1.15), # D6 apex
        (1.22, 880.00, 1.40, 0.80),  # A5 fifth
        (1.22, 587.33, 1.40, 0.75),  # D5 octave base
        (1.22, 739.99, 1.35, 0.65)   # F#5 major third
    ]
    
    for start_t, freq, note_dur, amp in motifs:
        start_idx = int(start_t * SAMPLE_RATE)
        end_idx = min(total_samples, start_idx + int(note_dur * SAMPLE_RATE))
        phase = 0.0
        for i in range(start_idx, end_idx):
            t = (i - start_idx) / SAMPLE_RATE
            env_att = min(1.0, t / 0.02)
            env_dec = math.exp(-2.0 * t / note_dur)
            vibrato = 1.0 + 0.007 * math.sin(2.0 * math.pi * 5.8 * t) if t > 0.20 else 1.0
            phase += 2.0 * math.pi * (freq * vibrato) / SAMPLE_RATE
            
            # Rich trumpet harmonics
            sig = (math.sin(phase) +
                   0.50 * math.sin(phase * 2.0) +
                   0.28 * math.sin(phase * 3.0) +
                   0.15 * math.sin(phase * 4.0) +
                   0.08 * math.sin(phase * 5.0))
            output[i] += sig * env_att * env_dec * amp
            
    # Ascending harp glissando starting at 1.15s (D4, F#4, A4, D5, F#5, A5, D6, F#6)
    harp_scale = [293.66, 369.99, 440.00, 587.33, 739.99, 880.00, 1174.66, 1479.98, 1760.00, 2349.32]
    for h_idx, h_freq in enumerate(harp_scale):
        h_start = 1.10 + h_idx * 0.045
        h_start_idx = int(h_start * SAMPLE_RATE)
        h_dur = 1.1
        h_end_idx = min(total_samples, h_start_idx + int(h_dur * SAMPLE_RATE))
        h_phase = 0.0
        for i in range(h_start_idx, h_end_idx):
            ht = (i - h_start_idx) / SAMPLE_RATE
            h_env = min(1.0, ht / 0.004) * math.exp(-4.2 * ht) * 0.38
            h_phase += 2.0 * math.pi * h_freq / SAMPLE_RATE
            output[i] += math.sin(h_phase) * h_env
            
    # Royal golden star shimmer (high cluster from 1.30s to 2.5s)
    random.seed(99)
    shimmer_freqs = [1975.53, 2349.32, 2793.83, 3135.96, 3520.00, 4186.01]
    for sf in shimmer_freqs:
        s_phase = random.uniform(0, math.pi * 2)
        s_start = 1.25 + random.uniform(0.0, 0.25)
        s_start_idx = int(s_start * SAMPLE_RATE)
        s_dur = 1.2
        s_end_idx = min(total_samples, s_start_idx + int(s_dur * SAMPLE_RATE))
        for i in range(s_start_idx, s_end_idx):
            st = (i - s_start_idx) / SAMPLE_RATE
            s_env = min(1.0, st / 0.01) * math.exp(-3.5 * st) * 0.20
            s_phase += 2.0 * math.pi * sf / SAMPLE_RATE
            output[i] += math.sin(s_phase) * s_env
            
    return output

if __name__ == "__main__":
    out_dir = os.path.dirname(os.path.abspath(__file__))
    print(f"Generating Task 16 procedural audio in: {out_dir}")
    
    write_wav(os.path.join(out_dir, "trophy_unlock_fanfare.wav"), synthesize_trophy_unlock_fanfare())
    write_wav(os.path.join(out_dir, "trophy_pickup_shimmer.wav"), synthesize_trophy_pickup_shimmer())
    write_wav(os.path.join(out_dir, "crown_equip_royal.wav"), synthesize_crown_equip_royal())
    
    print("All Task 16 audio files generated successfully!")
