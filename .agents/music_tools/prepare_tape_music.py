"""
TASK-06 (retoque) - prepara as 4 musicas das fitas cassete a partir dos MP3 baixados (incompetech.com, CC-BY 4.0).

Entrada : .agents/music_downloads/*.mp3 (dados nao confiaveis: so lidos como audio, nada e executado de la)
Saida   : Assets/Audio/Music/tape_*.ogg (Ogg Vorbis) + relatorio JSON em .agents/visual_qa/TASK-06_MusicPrep.json

Processamento (igual para as 4, so muda o fade):
  1. decodifica, remove silencio das pontas (< -60 dBFS)
  2. passa-baixa suave de 12 kHz (2a ordem) para tirar qualquer chiado agudo
  3. fade de entrada/saida para a emenda do loop do radio nao estalar
  4. loudness integrado ITU-R BS.1770 (ponderacao K, gating -70/-10) e pico com oversampling 4x
  5. ganho para TODAS ficarem no MESMO loudness (-21 LUFS); se o pico passar de -7 dBFS, um limitador suave
     com look-ahead segura so os ataques (na pratica so o koto de "Ripples", ~3 dB)
  6. grava Ogg Vorbis em blocos e confere o pico de novo depois de decodificar o arquivo gravado

Rodar com um Python que tenha numpy + scipy + soundfile (venv isolado):
  <venv>/Scripts/python -I prepare_tape_music.py
"""
import json
import math
import os
import sys

import numpy as np
import soundfile as sf
from scipy import signal

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SRC = os.path.join(ROOT, ".agents", "music_downloads")
DST = os.path.join(ROOT, "Assets", "Audio", "Music")
REPORT = os.path.join(ROOT, ".agents", "visual_qa", "TASK-06_MusicPrep.json")

TRACKS = [
    # (arquivo baixado, arquivo final, fade in s, fade out s)
    ("Newer Wave.mp3", "tape_cyber_horizon.ogg", 1.0, 2.0),
    ("Study And Relax.mp3", "tape_neon_sunset.ogg", 1.0, 2.0),
    ("Local Forecast.mp3", "tape_luna_cantina.ogg", 0.03, 0.05),   # o autor diz que esta versao "cleanly loops"
    ("Ripples.mp3", "tape_emerald_zen.ogg", 1.5, 2.5),
]
TARGET_LUFS = -21.0
LIMIT_CEILING_DB = -7.0    # teto do limitador (margem de 1 dB para o Vorbis; regra do projeto: pico <= -6 dBFS)


def db(x):
    return 20.0 * math.log10(max(x, 1e-12))


def k_weighting_sos(sr):
    """Filtros de ponderacao K do BS.1770 (shelf de alta + passa-alta RLB) calculados para a taxa dada."""
    # High shelf (pre-filtro): G = +4 dB, fc = 1500 Hz, Q = 1/sqrt(2)
    G, fc, Q = 3.999843853973347, 1681.974450955533, 0.7071752369554196
    K = math.tan(math.pi * fc / sr)
    Vh = 10 ** (G / 20.0)
    Vb = Vh ** 0.4996667741545416
    a0 = 1.0 + K / Q + K * K
    b = [(Vh + Vb * K / Q + K * K) / a0, 2.0 * (K * K - Vh) / a0, (Vh - Vb * K / Q + K * K) / a0]
    a = [1.0, 2.0 * (K * K - 1.0) / a0, (1.0 - K / Q + K * K) / a0]
    # High pass (RLB): fc = 38 Hz, Q = 0.5
    fc2, Q2 = 38.13547087602444, 0.5003270373238773
    K2 = math.tan(math.pi * fc2 / sr)
    a02 = 1.0 + K2 / Q2 + K2 * K2
    b2 = [1.0, -2.0, 1.0]
    a2 = [1.0, 2.0 * (K2 * K2 - 1.0) / a02, (1.0 - K2 / Q2 + K2 * K2) / a02]
    return np.array([b + a, b2 + a2])


def integrated_lufs(x, sr):
    y = signal.sosfilt(k_weighting_sos(sr), x, axis=0)
    block = int(0.4 * sr)
    hop = int(0.1 * sr)
    if len(y) < block:
        return -70.0
    powers = []
    for start in range(0, len(y) - block + 1, hop):
        seg = y[start:start + block]
        powers.append(np.sum(np.mean(seg * seg, axis=0)))   # canais L/R com peso 1
    powers = np.array(powers)
    loud = -0.691 + 10 * np.log10(np.maximum(powers, 1e-12))
    gated = powers[loud > -70.0]
    if gated.size == 0:
        return -70.0
    rel = -0.691 + 10 * np.log10(np.mean(gated)) - 10.0
    gated2 = powers[(loud > -70.0) & (loud > rel)]
    return float(-0.691 + 10 * np.log10(np.mean(gated2)))


def true_peak_db(x):
    up = signal.resample_poly(x, 4, 1, axis=0)
    return db(float(np.max(np.abs(up))))


def spectral_centroid(x, sr):
    mono = x.mean(axis=1)[: sr * 30]
    f, _, Z = signal.stft(mono, sr, nperseg=4096)
    mag = np.abs(Z)
    return float(np.sum(f[:, None] * mag) / max(np.sum(mag), 1e-9))


def trim_silence(x, thr_db=-60.0):
    env = np.max(np.abs(x), axis=1)
    idx = np.where(env > 10 ** (thr_db / 20.0))[0]
    if idx.size == 0:
        return x
    return x[idx[0]: idx[-1] + 1]


def fade(x, sr, fin, fout):
    n = len(x)
    a = max(1, int(fin * sr))
    b = max(1, int(fout * sr))
    x[:a] *= (0.5 - 0.5 * np.cos(np.linspace(0, math.pi, a)))[:, None]
    x[n - b:] *= (0.5 + 0.5 * np.cos(np.linspace(0, math.pi, b)))[:, None]
    return x


def soft_limit(x, sr, ceiling_db):
    """Limitador com look-ahead de 5 ms e soltura suave (~25 ms). Retorna (audio, reducao maxima em dB)."""
    from scipy.ndimage import maximum_filter1d, minimum_filter1d
    c = 10 ** (ceiling_db / 20.0)
    env = maximum_filter1d(np.max(np.abs(x), axis=1), size=int(0.010 * sr) + 1)
    need = np.minimum(1.0, c / np.maximum(env, 1e-9))
    if need.min() >= 1.0:
        return x, 0.0
    g = minimum_filter1d(need, size=int(0.030 * sr) + 1)
    g = signal.sosfiltfilt(signal.butter(1, 12.0, "low", fs=sr, output="sos"), g)
    g = np.minimum(g, need)
    return x * g[:, None], float(-db(g.min()))


def write_ogg(path, y, sr):
    """Grava em blocos (gravar tudo de uma vez estoura a pilha da libsndfile no Windows)."""
    with sf.SoundFile(path, "w", samplerate=sr, channels=y.shape[1], format="OGG", subtype="VORBIS",
                      compression_level=0.45) as f:
        for i in range(0, len(y), 32768):
            f.write(y[i:i + 32768])


def main():
    os.makedirs(DST, exist_ok=True)
    prepared = []
    for src, dst, fin, fout in TRACKS:
        x, sr = sf.read(os.path.join(SRC, src), always_2d=True, dtype="float64")
        if x.shape[1] == 1:
            x = np.repeat(x, 2, axis=1)
        x = trim_silence(x)
        x = signal.sosfiltfilt(signal.butter(2, 12000, "low", fs=sr, output="sos"), x, axis=0)
        x = fade(x, sr, fin, fout)
        lufs = integrated_lufs(x, sr)
        peak = true_peak_db(x)
        prepared.append(dict(src=src, dst=dst, sr=sr, x=x, lufs=lufs, peak=peak))
        print(f"{src}: {len(x) / sr:.1f} s, {lufs:.1f} LUFS, pico {peak:.1f} dBTP (antes do ganho)")

    target = TARGET_LUFS
    report = {"targetLufs": target, "limiterCeilingDb": LIMIT_CEILING_DB, "tracks": []}
    for p in prepared:
        gain_db = target - p["lufs"]
        y = p["x"] * 10 ** (gain_db / 20.0)
        y, reduction = soft_limit(y, p["sr"], LIMIT_CEILING_DB)
        y = y.astype(np.float32)
        out = os.path.join(DST, p["dst"])
        write_ogg(out, y, p["sr"])
        back, _ = sf.read(out, always_2d=True, dtype="float64")
        rec = dict(
            source=p["src"], file="Assets/Audio/Music/" + p["dst"], seconds=round(len(back) / p["sr"], 2),
            sampleRate=p["sr"], gainDb=round(gain_db, 2), limiterMaxReductionDb=round(reduction, 2),
            lufsAfter=round(integrated_lufs(back, p["sr"]), 2),
            samplePeakDbfsAfter=round(db(float(np.max(np.abs(back)))), 2),
            truePeakDbAfter=round(true_peak_db(back), 2),
            spectralCentroidHz=round(spectral_centroid(back, p["sr"]), 1),
            bytes=os.path.getsize(out),
        )
        report["tracks"].append(rec)
        print(json.dumps(rec, ensure_ascii=False))
    with open(REPORT, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
    bad = [t for t in report["tracks"] if t["samplePeakDbfsAfter"] > -6.0 or t["bytes"] > 6_000_000]
    if bad:
        print("PROBLEMA:", bad)
        sys.exit(1)


if __name__ == "__main__":
    main()
