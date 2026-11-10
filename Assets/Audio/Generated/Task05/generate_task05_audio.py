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
        scale = 32767.0 * 0.92 / max_val
        
        packed_frames = bytearray()
        for s in samples:
            val = int(s * scale)
            val = max(-32768, min(32767, val))
            packed_frames.extend(struct.pack('<h', val))
        wav_file.writeframes(packed_frames)
    print(f"Generated {filename} ({len(samples)/SAMPLE_RATE:.2f}s)")

def synthesize_fanfare():
    # Triumphant brass fanfare:
    # Motif: G4 (0.18s) -> C5 (0.18s) -> E5 (0.18s) -> G5 (sustained chord C5+E5+G5, 1.6s)
    notes = [
        (392.00, 0.00, 0.16, 0.8),  # G4
        (523.25, 0.16, 0.16, 0.85), # C5
        (659.25, 0.32, 0.18, 0.9),  # E5
        (783.99, 0.50, 1.80, 1.0),  # G5
        (523.25, 0.50, 1.80, 0.65), # Chord root C5
        (659.25, 0.50, 1.80, 0.65), # Chord 3rd E5
        (1046.5, 0.50, 1.80, 0.35), # Octave sheen C6
    ]
    
    total_duration = 2.40
    total_samples = int(SAMPLE_RATE * total_duration)
    output = [0.0] * total_samples
    
    # Synthesize trumpet/brass timbre for each note
    for freq, start_t, dur, amp in notes:
        start_idx = int(start_t * SAMPLE_RATE)
        num_note_samples = int(dur * SAMPLE_RATE)
        
        for i in range(num_note_samples):
            idx = start_idx + i
            if idx >= total_samples:
                break
            
            t = i / SAMPLE_RATE
            
            # Envelope: fast attack (15ms), decay to sustain, smooth release
            if t < 0.015:
                env = t / 0.015
            elif t < dur - 0.25:
                env = 1.0 - 0.15 * (t / dur)
            else:
                rel_t = (t - (dur - 0.25)) / 0.25
                env = max(0.0, (1.0 - rel_t) ** 2)
            
            # Vibrato starting after 0.2s of note
            vibrato = 0.0
            if dur > 0.5 and t > 0.2:
                vib_depth = min(1.0, (t - 0.2) / 0.3) * 6.0
                vibrato = math.sin(2 * math.pi * 5.5 * t) * vib_depth
            
            inst_freq = freq + vibrato
            phase = 2 * math.pi * inst_freq * t
            
            # Brass harmonics: rich odd and even harmonics
            sample = (
                1.00 * math.sin(phase) +
                0.60 * math.sin(2 * phase) +
                0.40 * math.sin(3 * phase) +
                0.25 * math.sin(4 * phase) +
                0.15 * math.sin(5 * phase) +
                0.08 * math.sin(6 * phase)
            )
            
            output[idx] += sample * env * amp
            
    # Add gentle shimmering reverb tail
    reverb_delays = [int(SAMPLE_RATE * d) for d in [0.045, 0.078, 0.115, 0.162]]
    reverb_decay = [0.35, 0.25, 0.18, 0.12]
    reverbed = list(output)
    for d, decay in zip(reverb_delays, reverb_decay):
        for i in range(d, total_samples):
            reverbed[i] += output[i - d] * decay
            
    return reverbed

def synthesize_confetti_pop():
    # Double confetti cannon pop:
    # Pop 1 at t=0.0s, Pop 2 at t=0.09s
    # Each pop has:
    # - Low thud / pressure release (exponential pitch drop from 280Hz to 45Hz)
    # - Sudden white noise burst with bandpass filtering
    # - High-frequency fluttering ribbons / paper scatter hiss (0.2s to 1.1s)
    
    total_duration = 1.35
    total_samples = int(SAMPLE_RATE * total_duration)
    output = [0.0] * total_samples
    
    pop_times = [0.00, 0.085]
    random.seed(42)
    
    for pop_idx, start_t in enumerate(pop_times):
        start_sample = int(start_t * SAMPLE_RATE)
        pop_amp = 1.0 if pop_idx == 0 else 0.88
        
        # 1. Low-end pressure transient (40ms)
        thud_samples = int(0.065 * SAMPLE_RATE)
        for i in range(thud_samples):
            idx = start_sample + i
            if idx >= total_samples:
                break
            t = i / SAMPLE_RATE
            # Pitch drop
            freq = 280.0 * math.exp(-t * 60.0) + 40.0
            phase = 2 * math.pi * freq * t
            env = math.exp(-t * 45.0)
            output[idx] += math.sin(phase) * env * 1.4 * pop_amp
            
        # 2. Explosive air release (white noise burst, 80ms)
        noise_samples = int(0.12 * SAMPLE_RATE)
        for i in range(noise_samples):
            idx = start_sample + i
            if idx >= total_samples:
                break
            t = i / SAMPLE_RATE
            noise = (random.random() * 2.0 - 1.0)
            env = math.exp(-t * 35.0)
            output[idx] += noise * env * 0.95 * pop_amp
            
        # 3. Confetti fluttering / hiss dispersion (scatter tail)
        flutter_samples = int(0.95 * SAMPLE_RATE)
        for i in range(flutter_samples):
            idx = start_sample + i
            if idx >= total_samples:
                break
            t = i / SAMPLE_RATE
            noise = (random.random() * 2.0 - 1.0)
            # High-pass / bandpass feel with micro-amplitude modulation (fluttering paper ribbons)
            flutter_mod = 0.5 + 0.5 * math.sin(2 * math.pi * (18.0 + pop_idx * 5.0) * t)
            env = math.exp(-t * 4.8) * (1.0 - math.exp(-t * 40.0))
            output[idx] += noise * env * flutter_mod * 0.35 * pop_amp
            
    return output

if __name__ == "__main__":
    out_dir = os.path.join("Assets", "Audio", "Generated", "Task05")
    os.makedirs(out_dir, exist_ok=True)
    
    fanfare_samples = synthesize_fanfare()
    write_wav(os.path.join(out_dir, "fanfare_triumph.wav"), fanfare_samples)
    
    pop_samples = synthesize_confetti_pop()
    write_wav(os.path.join(out_dir, "confetti_cannon_pop.wav"), pop_samples)
