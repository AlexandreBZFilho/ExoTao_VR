"""
TASK-16 (retoque da estante) - texturas simples (PNG RGBA, sem dependencias externas).

  Trophy_LockIcon.png    64x64   cadeado branco (a cor vem do material/MPB) para a placa de bloqueado
  Trophy_HaloRadial.png  64x64   brilho radial suave para o halo atras de cada trofeu
  Trophy_CardBg.png      256x128 retangulo arredondado escuro com borda dourada para o cartao de informacao
Uso: python generate_task16_textures.py
"""
import math
import os
import struct
import zlib

OUT = os.path.dirname(os.path.abspath(__file__))


def write_png(name, w, h, px):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for x in range(w):
            raw.extend(px(x, h - 1 - y))      # y para cima (como UV)

    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)

    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) \
        + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    with open(os.path.join(OUT, name), "wb") as f:
        f.write(data)
    print(name, w, h)


def c8(v):
    return max(0, min(255, int(round(v * 255))))


def smooth_edge(d, aa=1.2):
    """d = distancia com sinal (px), negativa dentro; retorna cobertura 0..1 suavizada."""
    return max(0.0, min(1.0, 0.5 - d / aa))


def lock(x, y):
    # corpo: retangulo arredondado 34x26 centrado em (32, 24); arco: anel raio 11 (externo) / 6 (interno) acima
    cx, cy = 32.0, 24.0
    qx, qy = abs(x + 0.5 - cx) - 17 + 5, abs(y + 0.5 - cy) - 13 + 5
    body = math.hypot(max(qx, 0), max(qy, 0)) + min(max(qx, qy), 0) - 5
    ax, ay = x + 0.5 - 32, y + 0.5 - 38
    r = math.hypot(ax, ay)
    ring = abs(r - 8.5) - 3.0
    if ay < 0:
        ring = max(ring, -ay - 3) if abs(ax) < 5 else ring   # pernas do arco entram no corpo
    shackle = ring if ay >= -4 else 99
    keyhole = math.hypot(x + 0.5 - 32, y + 0.5 - 26) - 3.2
    a = max(smooth_edge(body), smooth_edge(shackle))
    a *= 1.0 - 0.85 * smooth_edge(keyhole)
    return (255, 255, 255, c8(a))


def halo(x, y):
    d = math.hypot((x + 0.5) / 32 - 1, (y + 0.5) / 32 - 1)
    a = max(0.0, 1 - d) ** 2.2
    return (255, 255, 255, c8(a))


def card(x, y):
    w, h, r = 256, 128, 22
    qx, qy = abs(x + 0.5 - w / 2) - w / 2 + r + 2, abs(y + 0.5 - h / 2) - h / 2 + r + 2
    d = math.hypot(max(qx, 0), max(qy, 0)) + min(max(qx, qy), 0) - r
    inside = smooth_edge(d)
    border = smooth_edge(abs(d + 2.5) - 1.6)
    t = (y + 0.5) / h                           # gradiente vertical sutil
    base = (0.06 + 0.04 * t, 0.07 + 0.045 * t, 0.10 + 0.06 * t)
    gold = (0.95, 0.78, 0.38)
    rr = base[0] * (1 - border) + gold[0] * border
    gg = base[1] * (1 - border) + gold[1] * border
    bb = base[2] * (1 - border) + gold[2] * border
    return (c8(rr), c8(gg), c8(bb), c8(inside * (0.88 * (1 - border) + border)))


if __name__ == "__main__":
    write_png("Trophy_LockIcon.png", 64, 64, lock)
    write_png("Trophy_HaloRadial.png", 64, 64, halo)
    write_png("Trophy_CardBg.png", 256, 128, card)
