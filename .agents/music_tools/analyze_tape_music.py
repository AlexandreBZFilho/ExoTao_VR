"""
TASK-06 (retoque) - prova numerica de que as 4 musicas das fitas soam diferentes entre si e da musica padrao do radio.

Para cada par (4 fitas + Music_SciFi_Menu): correlacao normalizada do ENVELOPE DE ENERGIA dos primeiros 30 s
(janelas de 50 ms, deslocamento livre de ate +-2 s -> pega a melhor correlacao possivel), centroide espectral,
andamento estimado por autocorrelacao do envelope de ataques (50-140 BPM) e duracao.
Saida: .agents/visual_qa/TASK-06_MusicAnalysis.json
Rodar: <venv>/Scripts/python -I analyze_tape_music.py
"""
import json
import os

import numpy as np
import soundfile as sf
from scipy import signal

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FILES = {
    "Cyber Horizon": "Assets/Audio/Music/tape_cyber_horizon.ogg",
    "Neon Sunset": "Assets/Audio/Music/tape_neon_sunset.ogg",
    "Luna Cantina": "Assets/Audio/Music/tape_luna_cantina.ogg",
    "Emerald Zen": "Assets/Audio/Music/tape_emerald_zen.ogg",
    "Radio padrao (Music_SciFi_Menu)": "Assets/Resources/Audio/Music_SciFi_Menu.mp3",
}
HOP_S = 0.05


def load(path):
    x, sr = sf.read(os.path.join(ROOT, path), always_2d=True, dtype="float64")
    return x.mean(axis=1), sr, len(x) / sr


def envelope(m, sr, seconds=30.0, skip=3.0, detrend_s=2.0):
    """Envelope em dB de [skip, skip+seconds] s, sem a tendencia lenta (media movel de 2 s).
    Pular o fade de entrada e tirar o 'crescendo' evita que duas introducoes que so 'sobem de volume'
    parecam iguais; o que sobra e o desenho ritmico/textura (duas faixas identicas continuam dando ~1)."""
    m = m[int(skip * sr): int((skip + seconds) * sr)]
    hop = int(HOP_S * sr)
    n = len(m) // hop
    e = np.sqrt(np.mean(m[: n * hop].reshape(n, hop) ** 2, axis=1))
    e = 20 * np.log10(np.maximum(e, 1e-6))
    k = max(1, int(detrend_s / HOP_S))
    trend = np.convolve(e, np.ones(k) / k, mode="same")
    return e - trend


def best_corr(a, b, max_lag=int(2.0 / HOP_S)):
    n = min(len(a), len(b))
    a = a[:n] - a[:n].mean()
    b = b[:n] - b[:n].mean()
    best = -1.0
    for lag in range(-max_lag, max_lag + 1):
        if lag >= 0:
            x, y = a[lag:], b[: n - lag]
        else:
            x, y = a[: n + lag], b[-lag:]
        d = np.linalg.norm(x) * np.linalg.norm(y)
        if d > 0:
            best = max(best, float(np.dot(x, y) / d))
    return best


def centroid(m, sr):
    f, _, Z = signal.stft(m[: sr * 30], sr, nperseg=4096)
    mag = np.abs(Z)
    return float(np.sum(f[:, None] * mag) / max(np.sum(mag), 1e-9))


def tempo(m, sr):
    seg = m[: sr * 60]
    hop = 512
    f, t, Z = signal.stft(seg, sr, nperseg=2048, noverlap=2048 - hop)
    mag = np.log1p(np.abs(Z))
    flux = np.maximum(0, np.diff(mag, axis=1)).sum(axis=0)
    flux -= flux.mean()
    ac = np.correlate(flux, flux, mode="full")[len(flux) - 1:]
    fps = sr / hop
    lags = np.arange(len(ac))
    bpm = 60.0 * fps / np.maximum(lags, 1)
    ok = (bpm >= 50) & (bpm <= 140)
    i = lags[ok][np.argmax(ac[ok])]
    strength = float(ac[i] / ac[0]) if ac[0] > 0 else 0.0
    return float(60.0 * fps / i), strength


def main():
    data = {}
    for name, path in FILES.items():
        m, sr, dur = load(path)
        bpm, strength = tempo(m, sr)
        data[name] = dict(path=path, seconds=round(dur, 1), env=envelope(m, sr), centroidHz=round(centroid(m, sr), 1),
                          estimatedBpm=round(bpm, 1), beatStrength=round(strength, 3))
    names = list(data)
    pairs = []
    for i in range(len(names)):
        for j in range(i + 1, len(names)):
            c = best_corr(data[names[i]]["env"], data[names[j]]["env"])
            pairs.append(dict(a=names[i], b=names[j], envelopeCorrelation=round(c, 3)))
    # Controle: a mesma faixa comparada com ela mesma 1 s adiante tem de dar correlacao alta (o metodo detecta igualdade)
    m0, sr0, _ = load(FILES["Cyber Horizon"])
    control = best_corr(envelope(m0, sr0), envelope(m0[sr0:], sr0))
    out = dict(
        controlSameTrackShifted1s=round(control, 3),
        method="correlacao normalizada do envelope de energia (dB, janelas de 50 ms) de 3 s a 33 s, sem a tendencia lenta (media movel 2 s), melhor deslocamento em +-2 s; controle: a mesma faixa contra ela mesma deslocada 1 s",
        tracks={k: {kk: vv for kk, vv in v.items() if kk != "env"} for k, v in data.items()},
        pairs=pairs,
        maxCorrelation=max(p["envelopeCorrelation"] for p in pairs),
    )
    path = os.path.join(ROOT, ".agents", "visual_qa", "TASK-06_MusicAnalysis.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=2)
    print(json.dumps(out, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
