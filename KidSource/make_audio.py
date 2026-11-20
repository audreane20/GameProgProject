# Synthesises the monster audio: chase music loop, "spotted" sting, jumpscare scream, growl loop, heavy footstep.
import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve
from scipy.io import wavfile
import sys, os

SR = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else '/home/claude/kid/out/audio'
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)

def t_(dur): return np.arange(int(dur * SR)) / SR
def lp(x, f, o=4): return sosfilt(butter(o, f, 'low', fs=SR, output='sos'), x)
def hp(x, f, o=4): return sosfilt(butter(o, f, 'high', fs=SR, output='sos'), x)
def bp(x, lo, hi, o=4): return sosfilt(butter(o, [lo, hi], 'band', fs=SR, output='sos'), x)
def saw(f, t): return 2 * ((f * t) % 1.0) - 1
def env(n, a, d):  # attack / exponential decay envelope in seconds
    t = np.arange(n) / SR
    e = np.exp(-np.maximum(t - a, 0) / max(d, 1e-4))
    if a > 0: e *= np.minimum(t / a, 1)
    return e
def reverb(x, secs=2.2, mix=0.3):
    n = int(secs * SR)
    ir = rng.standard_normal(n) * np.exp(-np.arange(n) / SR * 3.2)
    ir = lp(ir, 5000)
    wet = fftconvolve(x, ir)[:len(x)]
    wet /= np.max(np.abs(wet)) + 1e-9
    return (1 - mix) * x / (np.max(np.abs(x)) + 1e-9) + mix * wet
def norm(x, peak=0.9): return x / (np.max(np.abs(x)) + 1e-9) * peak
def save(name, x, peak=0.9):
    x = norm(x, peak)
    wavfile.write(f'{OUT}/{name}.wav', SR, (x * 32767).astype(np.int16))
    print(name, round(len(x) / SR, 2), 's')
def add(buf, x, at):
    i = int(at * SR); j = min(len(buf), i + len(x))
    if i < len(buf): buf[i:j] += x[:j - i]

def kick(dur=0.45, f0=110, f1=38):
    t = t_(dur)
    f = f1 + (f0 - f1) * np.exp(-t * 25)
    ph = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(ph) * env(len(t), 0.002, 0.18) + 0.15 * lp(rng.standard_normal(len(t)), 900) * env(len(t), 0, 0.02)

def metal_hit(dur=1.2):
    t = t_(dur)
    n = rng.standard_normal(len(t))
    x = sum(bp(n, f * 0.97, f * 1.03, 2) for f in (410, 667, 1043, 1580, 2390))
    return x * env(len(t), 0.001, 0.35)

# ---------------- chase music: 150 bpm, 16 bars, seamless loop ----------------
bpm = 150; beat = 60 / bpm; bars = 16
loop = beat * 4 * bars
tail = 2.0
T = t_(loop + tail)
mix = np.zeros_like(T)

# sub drone with slow pulse
drone = (np.sin(2 * np.pi * 36.71 * T) + 0.6 * np.sin(2 * np.pi * 73.42 * T + 0.3) + 0.3 * saw(36.71, T))
drone *= 0.6 + 0.4 * np.sin(2 * np.pi * (bpm / 60 / 2) * T) ** 2
mix += 0.35 * lp(drone, 180)

# dissonant string cluster, tremolo in 16ths
notes = [146.83, 155.56, 220.0, 207.65, 293.66, 311.13]
strings = sum(saw(f * (1 + d), T) for f in notes for d in (-0.004, 0.0, 0.005))
trem = 0.55 + 0.45 * np.abs(np.sin(np.pi * (bpm / 60 * 4) * T))
strings = bp(strings, 180, 2600, 2) * trem
mix += 0.08 * strings / np.max(np.abs(strings))

# drums: pounding kicks, heartbeat doubles, taiko toms
for b in range(int(loop / beat) + 4):
    at = b * beat
    add(mix, 0.9 * kick(), at)
    if b % 2 == 1:
        add(mix, 0.55 * kick(0.35, 160, 70), at + beat * 0.5)
    if b % 4 == 3:
        add(mix, 0.5 * kick(0.3, 90, 45), at + beat * 0.25)
for b in range(0, int(loop / beat) + 4, 8):
    add(mix, 0.35 * metal_hit(), b * beat)

# shrieking glissandi every 4 bars
for k in range(0, bars + 1, 4):
    at = k * 4 * beat + 2 * beat
    t = t_(3.5)
    f = 900 + 500 * np.sin(np.pi * t / 3.5) + 30 * np.sin(2 * np.pi * 7 * t)
    s = np.sin(2 * np.pi * np.cumsum(f) / SR) + 0.4 * np.sin(4 * np.pi * np.cumsum(f) / SR)
    add(mix, 0.12 * s * np.sin(np.pi * t / 3.5) ** 2, at)

mix = reverb(mix, 2.0, 0.25)
n = int(loop * SR); fade = int(tail * SR)
music = mix[:n].copy()
w = np.linspace(0, 1, fade)
music[:fade] = music[:fade] * w + mix[n:n + fade] * (1 - w)   # tail wraps into the start: seamless loop
music = np.tanh(1.4 * music / np.max(np.abs(music)))
save('monster_chase_music', music, 0.85)

# ---------------- "it sees you" sting ----------------
t = t_(3.0)
boom = kick(3.0, 70, 28) * 1.2
stab = sum(saw(f, t) for f in (98, 103.8, 146.8, 155.6, 415.3, 440)) * env(len(t), 0.005, 0.5)
stab = bp(stab, 120, 3500, 2)
f = 1200 + 900 * t
shriek = np.sin(2 * np.pi * np.cumsum(f) / SR) * env(len(t), 0.02, 0.7) * 0.5
sting = reverb(boom + 0.25 * stab / np.max(np.abs(stab)) + 0.2 * shriek + 0.4 * metal_hit(3.0), 2.5, 0.35)
save('monster_spotted', sting, 0.95)

# ---------------- jumpscare scream ----------------
t = t_(2.2)
glide = np.interp(t, [0, 0.08, 0.5, 1.4, 2.2], [260, 820, 700, 560, 300])
vib = 1 + 0.03 * np.sin(2 * np.pi * 23 * t)
ph = 2 * np.pi * np.cumsum(glide * vib) / SR
voice = sum(np.sin(ph * h + h) / h ** 0.6 for h in range(1, 9))
voice += 0.6 * saw(1, t) * 0 + 0.5 * np.sign(np.sin(ph * 0.5))           # rough sub-octave
ring = np.sin(2 * np.pi * 63 * t)
noise = bp(rng.standard_normal(len(t)), 1500, 7000, 2)
scream = (voice * (0.7 + 0.3 * ring) + 0.6 * noise) * env(len(t), 0.01, 0.9)
scream = np.tanh(3.0 * scream / np.max(np.abs(scream)))
scream += 1.2 * kick(2.2, 80, 30)
scream = reverb(scream, 1.6, 0.2)
save('monster_scream', scream, 0.99)

# ---------------- growl loop (quiet, 3D, attached to the monster) ----------------
loop_g = 6.0
t = t_(loop_g + 1)
rasp = lp(rng.standard_normal(len(t)), 700) * (0.5 + 0.5 * np.abs(np.sin(2 * np.pi * 11 * t)))
low = np.sin(2 * np.pi * 55 * t + 2 * np.sin(2 * np.pi * 3 * t)) * 0.6
breath = 0.5 + 0.5 * np.sin(2 * np.pi * t / 3.0 - np.pi / 2)      # inhale/exhale every 3 s
g = (rasp + low) * breath
n = int(loop_g * SR); fade = int(1 * SR); w = np.linspace(0, 1, fade)
growl = g[:n].copy(); growl[:fade] = growl[:fade] * w + g[n:n + fade] * (1 - w)
save('monster_growl', growl, 0.8)

# ---------------- heavy footstep ----------------
t = t_(0.6)
step = kick(0.6, 90, 32) + 0.4 * lp(rng.standard_normal(len(t)), 2500) * env(len(t), 0.001, 0.03)
save('monster_step', reverb(step, 0.8, 0.15), 0.9)
