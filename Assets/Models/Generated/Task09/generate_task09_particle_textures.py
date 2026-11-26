"""
TASK-09 (rework) - texturas de particula dos fogos (PNG RGBA, sem dependencias externas).

  Firework_Spark.png  64x64   ponto suave: nucleo branco brilhante + halo largo (faiscas, estrelas, flash)
  Firework_Trail.png  128x64  risco alongado simetrico (Stretched Billboard da trilha do foguete)

RGB = branco (a cor vem do gradiente do ParticleSystem); alpha = intensidade.
Uso: python generate_task09_particle_textures.py
"""
import math
import os
import struct
import zlib

OUT_DIR = os.path.dirname(os.path.abspath(__file__))


def write_png(path, width, height, pixels):
    """pixels: lista de linhas, cada linha lista de (r, g, b, a) 0..255."""
    raw = bytearray()
    for row in pixels:
        raw.append(0)  # filtro 'None'
        for r, g, b, a in row:
            raw.extend((r, g, b, a))

    def chunk(tag, data):
        c = struct.pack('>I', len(data)) + tag + data
        return c + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = b'\x89PNG\r\n\x1a\n'
    png += chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(bytes(raw), 9))
    png += chunk(b'IEND', b'')
    with open(path, 'wb') as f:
        f.write(png)
    print(f"{os.path.basename(path)} {width}x{height}")


def to8(v):
    return max(0, min(255, int(round(v * 255.0))))


def spark(size=64):
    rows = []
    for y in range(size):
        row = []
        for x in range(size):
            dx = (x + 0.5) / size * 2.0 - 1.0
            dy = (y + 0.5) / size * 2.0 - 1.0
            r = math.sqrt(dx * dx + dy * dy)
            core = math.exp(-(r / 0.16) ** 2)
            glow = 0.55 * math.exp(-(r / 0.45) ** 2)
            edge = max(0.0, 1.0 - r) ** 0.5          # zera na borda (sem quadrado visivel)
            a = min(1.0, (core + glow) * edge)
            white = min(1.0, 0.75 + 0.25 * core)
            row.append((to8(white), to8(white), to8(white), to8(a)))
        rows.append(row)
    return rows


def trail(w=128, h=64):
    rows = []
    for y in range(h):
        row = []
        for x in range(w):
            u = (x + 0.5) / w * 2.0 - 1.0    # comprimento
            v = (y + 0.5) / h * 2.0 - 1.0    # largura
            along = max(0.0, 1.0 - abs(u)) ** 0.7
            across = math.exp(-(v / 0.30) ** 2)
            core = math.exp(-(v / 0.10) ** 2) * along
            a = min(1.0, (0.7 * across + 0.6 * core) * along)
            row.append((255, 255, 255, to8(a)))
        rows.append(row)
    return rows


if __name__ == '__main__':
    write_png(os.path.join(OUT_DIR, "Firework_Spark.png"), 64, 64, spark())
    write_png(os.path.join(OUT_DIR, "Firework_Trail.png"), 128, 64, trail())
