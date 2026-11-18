"""Pio suave do pássaro guia (hotfix do playtest: o pio antigo era estridente, 2.8-4.3 kHz a -0.6 dB).
Gera bird_chirp_cheerful.wav: 0.45 s, duas notas melodiosas entre 1.2 e 2.2 kHz, pico -12 dB, low-pass 3.5 kHz.
Atenua bird_wing_flutter.wav para pico -12 dB. Uso: python generate_bird_soft_audio.py (só biblioteca padrão)."""
import math, os, struct, wave

SR = 44100
HERE = os.path.dirname(os.path.abspath(__file__))
PEAK = 10 ** (-12.0 / 20.0)

def write_wav(path, samples):
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32767)) for s in samples))

def read_wav(path):
    with wave.open(path, "rb") as w:
        n, ch = w.getnframes(), w.getnchannels()
        data = struct.unpack("<%dh" % (n * ch), w.readframes(n))
    return [x / 32768.0 for x in data[::ch]]

def lowpass(samples, cutoff):
    # dois polos em cascata (12 dB/oitava)
    a = 1.0 - math.exp(-2.0 * math.pi * cutoff / SR)
    y1 = y2 = 0.0
    out = []
    for s in samples:
        y1 += a * (s - y1)
        y2 += a * (y1 - y2)
        out.append(y2)
    return out

def normalize(samples, peak):
    m = max(abs(s) for s in samples) or 1.0
    return [s * peak / m for s in samples]

def note(duration, f0, f1, vibrato=0.0):
    n = int(duration * SR)
    out, phase = [], 0.0
    for i in range(n):
        p = i / max(1, n - 1)
        freq = f0 + (f1 - f0) * (0.5 - 0.5 * math.cos(math.pi * p))          # glissando suave
        freq += vibrato * math.sin(2.0 * math.pi * 28.0 * i / SR)
        phase += 2.0 * math.pi * freq / SR
        env = math.sin(math.pi * p) ** 1.6                                     # ataque e final macios
        out.append((math.sin(phase) + 0.08 * math.sin(2.0 * phase)) * env)
    return out

def chirp():
    silence = lambda d: [0.0] * int(d * SR)
    s = silence(0.01) + note(0.17, 1500.0, 2100.0, 25.0) + silence(0.05) + note(0.20, 1900.0, 1300.0, 18.0) + silence(0.02)
    return normalize(lowpass(s, 3500.0), PEAK)

if __name__ == "__main__":
    write_wav(os.path.join(HERE, "bird_chirp_cheerful.wav"), chirp())
    flutter = os.path.join(HERE, "bird_wing_flutter.wav")
    write_wav(flutter, normalize(lowpass(read_wav(flutter), 3500.0), PEAK))
    print("ok")
