import math
import struct
import wave
import os

def create_wave_file(filename, samples, sample_rate=44100):
    os.makedirs(os.path.dirname(filename), exist_ok=True)
    with wave.open(filename, 'wb') as wf:
        wf.setnchannels(1)  # Mono
        wf.setsampwidth(2)  # 16-bit
        wf.setframerate(sample_rate)
        
        packed_samples = bytearray()
        for s in samples:
            # Clamp to -1.0 .. 1.0
            clamped = max(-1.0, min(1.0, s))
            int_val = int(clamped * 32767.0)
            packed_samples.extend(struct.pack('<h', int_val))
            
        wf.writeframes(packed_samples)
    print(f"Generated: {filename} ({len(samples)} samples, {len(samples)/sample_rate:.2f}s)")

def generate_drone_purr(sample_rate=44100, duration=1.8):
    # Ronronar mecânico e orgânico suave: motor ressonante a 55Hz + modulação AM a 14Hz + harmônicos quentes
    num_samples = int(sample_rate * duration)
    samples = []
    
    for i in range(num_samples):
        t = i / sample_rate
        # Envelope de fade in e fade out para loop suave
        fade_in = min(1.0, t / 0.15)
        fade_out = min(1.0, (duration - t) / 0.15)
        env = fade_in * fade_out
        
        # Modulação de amplitude (tremolo de purr)
        purr_mod = 0.5 + 0.5 * math.sin(2.0 * math.pi * 14.0 * t)
        
        # Frequência fundamental (ronronar robótico suave)
        f0 = 58.0 + 3.0 * math.sin(2.0 * math.pi * 3.5 * t)
        
        carrier1 = math.sin(2.0 * math.pi * f0 * t)
        carrier2 = 0.45 * math.sin(2.0 * math.pi * (f0 * 2.0) * t + 0.3)
        carrier3 = 0.20 * math.sin(2.0 * math.pi * (f0 * 3.0) * t + 0.7)
        sub_bass = 0.35 * math.sin(2.0 * math.pi * (f0 * 0.5) * t)
        
        # Leve ruído colorido filtrado para textura de ronrom
        noise = 0.08 * (math.sin(2.0 * math.pi * 123.0 * t) * math.sin(2.0 * math.pi * 377.0 * t))
        
        signal = (carrier1 + carrier2 + carrier3 + sub_bass + noise) * purr_mod * env * 0.75
        samples.append(signal)
        
    return samples

def generate_drone_boop(sample_rate=44100, duration=0.28):
    # Boop tátil elástico: sweep rápido de 750Hz para 240Hz com decaimento exponencial
    num_samples = int(sample_rate * duration)
    samples = []
    phase = 0.0
    
    for i in range(num_samples):
        t = i / sample_rate
        norm_t = t / duration
        
        # Envelope de amplitude com ataque rápido e amortecimento elástico
        env = math.exp(-6.5 * norm_t) * math.sin(min(1.0, norm_t * 8.0) * math.pi * 0.5)
        
        # Curva de frequência descendente exponencial (boop elástico)
        freq = 240.0 + (750.0 - 240.0) * math.exp(-12.0 * norm_t)
        
        phase += 2.0 * math.pi * freq / sample_rate
        
        # Onda senoidal com segundo harmônico suave
        s = math.sin(phase) + 0.3 * math.sin(phase * 2.0) + 0.1 * math.sin(phase * 3.0)
        samples.append(s * env * 0.85)
        
    return samples

def generate_drone_giggle(sample_rate=44100, duration=0.65):
    # Risadinha cibernética fofa: arpeggio ascendente de 4 tons saltitantes com vibrato
    num_samples = int(sample_rate * duration)
    samples = []
    
    notes = [523.25, 659.25, 783.99, 1046.50] # C5, E5, G5, C6
    note_duration = duration / len(notes)
    
    for i in range(num_samples):
        t = i / sample_rate
        note_idx = min(len(notes) - 1, int(t / note_duration))
        local_t = t - (note_idx * note_duration)
        norm_local = local_t / note_duration
        
        # Envelope por nota (staccato saltitante)
        env = math.sin(norm_local * math.pi) ** 1.3
        
        # Vibrato brincalhão
        vibrato = math.sin(2.0 * math.pi * 12.0 * t) * 18.0
        f = notes[note_idx] + vibrato
        
        tone = math.sin(2.0 * math.pi * f * t) + 0.25 * math.sin(2.0 * math.pi * f * 2.0 * t)
        samples.append(tone * env * 0.70)
        
    return samples

def generate_drone_dance_groove(sample_rate=44100, duration=0.80):
    # Beep rítmico alegre: 4 pulsos sincronizados estilo 120 BPM
    num_samples = int(sample_rate * duration)
    samples = []
    
    beats = [
        (0.00, 440.0, 0.14),
        (0.20, 554.37, 0.14),
        (0.40, 659.25, 0.14),
        (0.60, 880.0, 0.18)
    ]
    
    for i in range(num_samples):
        t = i / sample_rate
        val = 0.0
        for start_t, freq, dur in beats:
            if start_t <= t < (start_t + dur):
                dt = (t - start_t) / dur
                env = math.sin(dt * math.pi) ** 1.5
                vibe = math.sin(2.0 * math.pi * freq * (t - start_t))
                harm = 0.3 * math.sin(2.0 * math.pi * freq * 2.0 * (t - start_t))
                val += (vibe + harm) * env * 0.75
        samples.append(val)
        
    return samples

if __name__ == '__main__':
    out_dir = r"Assets/Audio/Generated/Task04"
    create_wave_file(os.path.join(out_dir, "drone_purr.wav"), generate_drone_purr())
    create_wave_file(os.path.join(out_dir, "drone_boop.wav"), generate_drone_boop())
    create_wave_file(os.path.join(out_dir, "drone_giggle.wav"), generate_drone_giggle())
    create_wave_file(os.path.join(out_dir, "drone_dance_groove.wav"), generate_drone_dance_groove())
    print("All Task04 audio assets successfully generated!")
