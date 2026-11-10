import math
import struct
import wave
import os
import random

def save_wav(filename, samples, sample_rate=44100):
    with wave.open(filename, 'wb') as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(sample_rate)
        # Normalize and convert to 16-bit PCM
        max_val = max(max(abs(s) for s in samples), 0.0001)
        scaling = 30000.0 / max_val if max_val > 0.9 else 30000.0
        packed = bytearray()
        for s in samples:
            val = int(max(-32767, min(32767, s * scaling)))
            packed.extend(struct.pack('<h', val))
        wf.writeframes(packed)
    print(f"Generated: {filename} ({len(samples)/sample_rate:.2f}s)")

SR = 44100

def generate_insert_click():
    duration = 0.38
    num_samples = int(SR * duration)
    samples = [0.0] * num_samples
    
    # 1. Initial mechanical slide friction (t: 0.0 to 0.12)
    for i in range(int(SR * 0.12)):
        t = i / SR
        # friction scratch
        noise = (random.random() * 2 - 1) * math.exp(-t * 25.0)
        slide_tone = math.sin(2 * math.pi * 1200 * t) * math.exp(-t * 30.0)
        samples[i] += 0.25 * noise + 0.15 * slide_tone

    # 2. Hard metallic snap / latch at t = 0.10
    snap_start = int(SR * 0.10)
    for i in range(snap_start, min(num_samples, snap_start + int(SR * 0.18))):
        dt = (i - snap_start) / SR
        # high sharp click + body resonance
        snap = math.sin(2 * math.pi * 3200 * dt) * math.exp(-dt * 90.0)
        body = math.sin(2 * math.pi * 420 * dt) * math.exp(-dt * 45.0)
        sub = math.sin(2 * math.pi * 180 * dt) * math.exp(-dt * 30.0)
        clack = (random.random() * 2 - 1) * math.exp(-dt * 120.0)
        samples[i] += 0.85 * snap + 0.50 * body + 0.40 * sub + 0.30 * clack

    # 3. Motor gear engagement click at t = 0.18
    gear_start = int(SR * 0.18)
    for i in range(gear_start, min(num_samples, gear_start + int(SR * 0.15))):
        dt = (i - gear_start) / SR
        gear = math.sin(2 * math.pi * 2100 * dt) * math.exp(-dt * 75.0)
        whir = math.sin(2 * math.pi * (800 + 400 * dt) * dt) * math.exp(-dt * 20.0)
        samples[i] += 0.35 * gear + 0.20 * whir

    return samples

def generate_eject_snap():
    duration = 0.42
    num_samples = int(SR * duration)
    samples = [0.0] * num_samples

    # 1. Button press click (t = 0.0)
    for i in range(int(SR * 0.08)):
        t = i / SR
        btn = math.sin(2 * math.pi * 1800 * t) * math.exp(-t * 110.0)
        samples[i] += 0.30 * btn

    # 2. Main spring release impulse (t = 0.05)
    spring_start = int(SR * 0.05)
    for i in range(spring_start, min(num_samples, spring_start + int(SR * 0.25))):
        dt = (i - spring_start) / SR
        # Spring twang (rapid frequency decay)
        freq = 380 + 120 * math.exp(-dt * 25.0)
        spring = math.sin(2 * math.pi * freq * dt) * math.exp(-dt * 28.0)
        # Metallic unlatch snap
        snap = math.sin(2 * math.pi * 2800 * dt) * math.exp(-dt * 85.0)
        thud = math.sin(2 * math.pi * 140 * dt) * math.exp(-dt * 35.0)
        samples[i] += 0.65 * spring + 0.70 * snap + 0.45 * thud

    # 3. Cassette landing / catch click (t = 0.18)
    land_start = int(SR * 0.18)
    for i in range(land_start, min(num_samples, land_start + int(SR * 0.15))):
        dt = (i - land_start) / SR
        clack = math.sin(2 * math.pi * 850 * dt) * math.exp(-dt * 60.0)
        noise = (random.random() * 2 - 1) * math.exp(-dt * 90.0)
        samples[i] += 0.30 * clack + 0.15 * noise

    return samples

def generate_lofi_sunset_groove():
    # 8-second chill loop (BPM ~ 75, 4 bars = 6.4s or 8.0s at 60 BPM)
    # Let's do 8.0s exactly at 60 BPM (2 beats/sec)
    duration = 8.0
    num_samples = int(SR * duration)
    samples = [0.0] * num_samples
    
    # Chords (Dm7 -> G7 -> Cmaj7 -> Am7)
    # Chord frequencies:
    chords = [
        # Dm7: D3, F3, A3, C4
        [146.83, 174.61, 220.00, 261.63],
        # G7: G2, B3, D4, F4
        [98.00, 246.94, 293.66, 349.23],
        # Cmaj7: C3, E3, G3, B3
        [130.81, 164.81, 196.00, 246.94],
        # Am7: A2, C3, E3, G3
        [110.00, 130.81, 164.81, 196.00],
    ]
    
    # 4 bars, each bar 2.0s
    for bar_idx, chord in enumerate(chords):
        bar_start = int(bar_idx * 2.0 * SR)
        bar_len = int(2.0 * SR)
        for i in range(bar_len):
            idx = bar_start + i
            if idx >= num_samples: break
            t_bar = i / SR
            
            # Rhodes-like warm bell piano for chord
            chord_val = 0.0
            for note_f in chord:
                # fundamental + soft 2nd & 3rd harmonic with decay
                env = math.exp(-t_bar * 1.4) + 0.15 * math.exp(-t_bar * 0.4)
                harmonic1 = math.sin(2 * math.pi * note_f * t_bar)
                harmonic2 = 0.35 * math.sin(2 * math.pi * note_f * 2.0 * t_bar) * math.exp(-t_bar * 3.0)
                harmonic3 = 0.15 * math.sin(2 * math.pi * note_f * 3.0 * t_bar) * math.exp(-t_bar * 5.0)
                chord_val += (harmonic1 + harmonic2 + harmonic3) * env * 0.12
            
            # Warm sub-bass
            bass_f = chord[0] * 0.5 if chord[0] > 120 else chord[0]
            bass_env = math.exp(-t_bar * 1.8) + 0.2
            bass = math.sin(2 * math.pi * bass_f * t_bar) * bass_env * 0.28
            
            samples[idx] += chord_val + bass

    # Drums: Soft vinyl kick & brush snare
    # Beat positions at each quarter note (every 1.0s)
    for beat in range(8):
        t_beat = beat * 1.0
        beat_idx = int(t_beat * SR)
        is_kick = (beat % 2 == 0)
        is_snare = (beat % 2 == 1)
        
        if is_kick:
            # Soft lo-fi kick
            for k in range(int(SR * 0.35)):
                idx = beat_idx + k
                if idx >= num_samples: break
                dt = k / SR
                freq = 110 * math.exp(-dt * 20.0) + 45
                kick = math.sin(2 * math.pi * freq * dt) * math.exp(-dt * 12.0)
                samples[idx] += kick * 0.45
        if is_snare:
            # Lo-fi rim/brush snare
            for s in range(int(SR * 0.25)):
                idx = beat_idx + s
                if idx >= num_samples: break
                dt = s / SR
                noise = (random.random() * 2 - 1) * math.exp(-dt * 25.0)
                snap = math.sin(2 * math.pi * 320 * dt) * math.exp(-dt * 30.0)
                samples[idx] += (noise * 0.22 + snap * 0.25)

    # Subtle vinyl crackle
    for i in range(num_samples):
        if random.random() < 0.0008:
            samples[i] += (random.random() * 2 - 1) * 0.18

    return samples

def generate_ambient_zen_flow():
    # 8-second soothing ambient loop (singing bowls + peaceful pentatonic pad)
    duration = 8.0
    num_samples = int(SR * duration)
    samples = [0.0] * num_samples
    
    # Pentatonic scale in F (F3, G3, A3, C4, D4)
    # Frequencies:
    notes = [174.61, 196.00, 220.00, 261.63, 293.66, 349.23]
    
    # Drone base (F2 + C3 pad)
    for i in range(num_samples):
        t = i / SR
        # Gentle LFO amplitude modulation
        lfo = 0.5 + 0.5 * math.sin(2 * math.pi * 0.125 * t)
        drone1 = math.sin(2 * math.pi * 87.31 * t) * 0.20
        drone2 = math.sin(2 * math.pi * 130.81 * t) * 0.15
        drone3 = math.sin(2 * math.pi * 174.61 * t + 0.5) * 0.12
        samples[i] += (drone1 + drone2 + drone3) * (0.7 + 0.3 * lfo)

    # Bell chimes at intervals
    bell_times = [0.2, 1.8, 3.5, 5.2, 6.8]
    bell_notes = [notes[0], notes[3], notes[2], notes[4], notes[5]]
    
    for t_start, f_note in zip(bell_times, bell_notes):
        start_idx = int(t_start * SR)
        for i in range(int(SR * 2.8)):
            idx = start_idx + i
            if idx >= num_samples:
                # Wrap around for perfect loopability
                idx = idx % num_samples
            dt = i / SR
            # Bell harmonics
            b1 = math.sin(2 * math.pi * f_note * dt) * math.exp(-dt * 1.5)
            b2 = 0.5 * math.sin(2 * math.pi * (f_note * 2.76) * dt) * math.exp(-dt * 2.2)
            b3 = 0.25 * math.sin(2 * math.pi * (f_note * 5.4) * dt) * math.exp(-dt * 3.5)
            samples[idx] += (b1 + b2 + b3) * 0.25

    return samples

def generate_synthwave_pulse():
    # 8-second 80s synthwave loop (120 BPM, 4 bars = 8.0s)
    duration = 8.0
    num_samples = int(SR * duration)
    samples = [0.0] * num_samples
    
    # 120 BPM = 2.0 beats/sec => 16th note = 0.125s
    # Bassline: 16th note pumping octaves on A (A1=55, A2=110)
    for step in range(64):
        t_step = step * 0.125
        step_idx = int(t_step * SR)
        f_bass = 55.0 if (step % 2 == 0) else 110.0
        
        for k in range(int(SR * 0.120)):
            idx = step_idx + k
            if idx >= num_samples: break
            dt = k / SR
            # Sawtooth-like bass
            saw = (2 * ((f_bass * dt) % 1.0) - 1) * math.exp(-dt * 14.0)
            sub = math.sin(2 * math.pi * (f_bass * 0.5) * dt) * math.exp(-dt * 10.0)
            samples[idx] += (saw * 0.22 + sub * 0.25)
            
    # Drum machine: Four-on-the-floor Kick + 80s Snare on 2 and 4
    for beat in range(16):
        t_beat = beat * 0.50
        beat_idx = int(t_beat * SR)
        
        # Punchy 80s Kick every beat
        for k in range(int(SR * 0.25)):
            idx = beat_idx + k
            if idx >= num_samples: break
            dt = k / SR
            freq = 150 * math.exp(-dt * 32.0) + 48
            kick = math.sin(2 * math.pi * freq * dt) * math.exp(-dt * 15.0)
            samples[idx] += kick * 0.40
            
        # Gated Snare on beats 1, 3, 5, ...
        if beat % 2 == 1:
            for s in range(int(SR * 0.28)):
                idx = beat_idx + s
                if idx >= num_samples: break
                dt = s / SR
                noise = (random.random() * 2 - 1) * (1.0 if dt < 0.16 else 0.0) * math.exp(-dt * 8.0)
                body = math.sin(2 * math.pi * 220 * dt) * math.exp(-dt * 20.0)
                samples[idx] += noise * 0.35 + body * 0.30

    # Arpeggio melody (Am -> F -> C -> G)
    arp_notes = [
        [220, 261.63, 329.63, 440], # Am
        [174.61, 220, 261.63, 349.23], # F
        [130.81, 164.81, 196, 261.63], # C
        [196, 246.94, 293.66, 392] # G
    ]
    for bar in range(4):
        notes_bar = arp_notes[bar]
        for note_step in range(16):
            f_note = notes_bar[note_step % len(notes_bar)] * 1.5
            t_arp = (bar * 2.0) + (note_step * 0.125)
            start_idx = int(t_arp * SR)
            for i in range(int(SR * 0.11)):
                idx = start_idx + i
                if idx >= num_samples: break
                dt = i / SR
                square = (1.0 if ((f_note * dt) % 1.0) < 0.5 else -1.0) * math.exp(-dt * 18.0)
                samples[idx] += square * 0.12

    return samples

if __name__ == '__main__':
    out_dir = r"D:\Alunos\Alexandre\Projeto IC\Assets\Audio\Generated\Task06"
    os.makedirs(out_dir, exist_ok=True)
    
    save_wav(os.path.join(out_dir, "cassette_insert_click.wav"), generate_insert_click())
    save_wav(os.path.join(out_dir, "cassette_eject_snap.wav"), generate_eject_snap())
    save_wav(os.path.join(out_dir, "lofi_sunset_groove.wav"), generate_lofi_sunset_groove())
    save_wav(os.path.join(out_dir, "ambient_zen_flow.wav"), generate_ambient_zen_flow())
    save_wav(os.path.join(out_dir, "synthwave_pulse.wav"), generate_synthwave_pulse())
    print("All Task06 audio files generated successfully!")
