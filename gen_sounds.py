#!/usr/bin/env python3
"""Generate simple, pleasant UI sound effects as mono 16-bit PCM WAV files."""
import wave, struct, math, os

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "Assets")
os.makedirs(OUT, exist_ok=True)

def env(i, n, attack=0.02, release=0.4):
    """Attack/decay envelope 0..1."""
    a = int(n * attack); r = int(n * release)
    if i < a: return i / max(1, a)
    if i > n - r: return max(0.0, (n - i) / max(1, r))
    return 1.0

def tone(freq, dur, vol=0.5, decay=6.0):
    n = int(SR * dur)
    out = bytearray()
    for i in range(n):
        t = i / SR
        e = math.exp(-decay * t)
        s = math.sin(2 * math.pi * freq * t) * vol * e
        out += struct.pack('<h', int(max(-1, min(1, s)) * 32767))
    return out, n

def sweep(f0, f1, dur, vol=0.45):
    n = int(SR * dur)
    out = bytearray()
    phase = 0.0
    for i in range(n):
        t = i / n
        freq = f0 + (f1 - f0) * t
        phase += 2 * math.pi * freq / SR
        e = math.sin(math.pi * t)  # bell envelope
        s = math.sin(phase) * vol * e
        out += struct.pack('<h', int(max(-1, min(1, s)) * 32767))
    return out, n

def write(name, data, n):
    path = os.path.join(OUT, name)
    with wave.open(path, 'w') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(bytes(data))

# confirm.wav — gentle two-note ascending chime
d1, n1 = tone(660, 0.18, 0.45, 7)
d2, n2 = tone(990, 0.40, 0.5, 4)
write("confirm.wav", d1 + d2, n1 + n2)

# connect.wav — rising sweep (warp feel)
d, n = sweep(220, 1320, 0.9, 0.4)
write("connect.wav", d, n)

# disconnect.wav — descending tone
d, n = sweep(880, 220, 0.6, 0.4)
write("disconnect.wav", d, n)

# error.wav — low buzz
d, n = tone(140, 0.35, 0.5, 3)
write("error.wav", d, n)

# listen.wav — soft high tick (voice listening started)
d1, n1 = tone(1200, 0.06, 0.25, 18)
d2, n2 = tone(1600, 0.10, 0.2, 12)
write("listen.wav", d1 + d2, n1 + n2)

# click.wav — short UI click
d, n = tone(880, 0.05, 0.3, 30)
write("click.wav", d, n)

for f in sorted(os.listdir(OUT)):
    if f.endswith('.wav'):
        print(f, os.path.getsize(os.path.join(OUT, f)), "bytes")
