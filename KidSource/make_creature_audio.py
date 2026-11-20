# Organic creature sounds built from a vocal model: irregular glottal pulses (vocal fry / death rattle)
# pushed through vocal-tract formant filters, plus breath noise, bone cracks and dragging feet.
import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve, lfilter
from scipy.io import wavfile
import sys, os

SR = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else '/home/claude/kid/out/audio2'
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(11)

def lp(x, f, o=4): return sosfilt(butter(o, f, 'low', fs=SR, output='sos'), x)
def hp(x, f, o=4): return sosfilt(butter(o, f, 'high', fs=SR, output='sos'), x)
def bp(x, lo, hi, o=2): return sosfilt(butter(o, [lo, hi], 'band', fs=SR, output='sos'), x)
def norm(x, peak=0.9): return x / (np.max(np.abs(x)) + 1e-9) * peak
def save(name, x, peak=0.9):
    x = norm(x, peak)
    wavfile.write(f'{OUT}/{name}.wav', SR, (x * 32767).astype(np.int16))
    print(name, round(len(x) / SR, 2), 's')

def resonator(x, f, bw):
    """Two-pole resonant filter = one formant."""
    r = np.exp(-np.pi * bw / SR)
    a1, a2 = -2 * r * np.cos(2 * np.pi * f / SR), r * r
    return lfilter([1 - r], [1, a1, a2], x)

def formants(x, F, gains=(1.0, 0.7, 0.4, 0.25)):
    out = np.zeros_like(x)
    for (f, bw), g in zip(F, gains):
        out += g * resonator(x, f, bw)
    return out

def formants_moving(x, F0, F1, gains=(1.0, 0.7, 0.4, 0.25), blocks=64):
    """Formants that glide from vowel F0 to vowel F1 over the sound (processed in blocks)."""
    n = len(x); out = np.zeros(n); step = n // blocks + 1
    for b in range(blocks):
        i, j = b * step, min(n, (b + 1) * step + 2048)
        if i >= n: break
        t = b / (blocks - 1)
        F = [((1 - t) * a[0] + t * c[0], (1 - t) * a[1] + t * c[1]) for a, c in zip(F0, F1)]
        y = formants(x[max(0, i - 2048):j], F, gains)
        seg = y[(i - max(0, i - 2048)):(i - max(0, i - 2048)) + min(step, n - i)]
        w = np.ones(len(seg))
        out[i:i + len(seg)] += seg * w
    return out

def glottal(dur, f0, jitter=0.25, shimmer=0.5, fry=0.0):
    """Irregular pulse train: f0 can be an array (Hz per sample). fry adds random long gaps (vocal fry)."""
    n = int(dur * SR)
    f0 = np.broadcast_to(np.asarray(f0, float), (n,))
    x = np.zeros(n)
    t = 0.0
    pulse = np.exp(-np.arange(int(0.006 * SR)) / (0.0012 * SR)) * np.sin(np.linspace(0, np.pi, int(0.006 * SR)))
    while True:
        i = int(t)
        if i >= n: break
        amp = 1 + shimmer * rng.standard_normal() * 0.5
        seg = pulse * amp
        x[i:i + len(seg)] += seg[:max(0, min(len(seg), n - i))]
        period = SR / max(f0[i], 5) * (1 + jitter * rng.standard_normal())
        if fry and rng.random() < fry: period *= rng.uniform(1.5, 3.0)
        t += max(period, SR / 2000)
    return x

def breath(dur, lo=300, hi=4000):
    return bp(rng.standard_normal(int(dur * SR)), lo, hi)

def env_ad(n, a, d, shape=1.0):
    t = np.arange(n) / SR
    up = np.minimum(t / max(a, 1e-4), 1)
    down = np.clip(1 - (t - (n / SR - d)) / max(d, 1e-4), 0, 1)
    return (up * down) ** shape

def room(x, secs=1.4, mix=0.22):
    n = int(secs * SR)
    ir = rng.standard_normal(n) * np.exp(-np.arange(n) / SR * 4.0)
    ir = lp(ir, 4500)
    wet = fftconvolve(x, ir)[:len(x)]
    return (1 - mix) * norm(x, 1) + mix * norm(wet, 1)

AH = [(520, 90), (880, 110), (2300, 160), (3300, 200)]     # big throat "ahh"
OO = [(350, 70), (650, 90), (2200, 150), (3100, 200)]
EE = [(420, 90), (2100, 140), (2900, 180), (3800, 220)]
KK = [(300, 60), (1200, 120), (2500, 180), (3500, 220)]    # tight, rattly

def croak(dur, f0=32, vowel=KK, fry=0.25, breathy=0.25):
    g = glottal(dur, f0, jitter=0.3, shimmer=0.8, fry=fry)
    src = g + breathy * breath(dur) * 0.05
    v = formants(src, vowel)
    return v * env_ad(len(v), 0.08, 0.3)

# ---------- breathing/croak loop that sits on the creature ----------
L = 8.0
loop = np.zeros(int((L + 2) * SR))
def put(buf, x, at):
    i = int(at * SR); j = min(len(buf), i + len(x)); buf[i:j] += x[:j - i]
put(loop, 0.9 * croak(2.6, f0=np.linspace(38, 26, int(2.6 * SR))), 0.3)             # long rattling exhale
put(loop, 0.12 * breath(1.3, 900, 5000) * env_ad(int(1.3 * SR), 0.5, 0.4, 1.5), 3.3)  # wet inhale
put(loop, 0.7 * croak(1.1, f0=45, vowel=AH, fry=0.4), 5.0)                          # short clicks
put(loop, 0.5 * croak(1.4, f0=np.linspace(28, 40, int(1.4 * SR)), vowel=OO), 6.5)
loop = room(loop, 1.2, 0.18)
n = int(L * SR); fade = int(1.5 * SR); w = np.linspace(0, 1, fade)
out = loop[:n].copy(); out[:fade] = out[:fade] * w + loop[n:n + fade] * (1 - w)
save('monster_growl', out, 0.85)

# ---------- shriek when it catches you ----------
d = 2.4; nS = int(d * SR); t = np.arange(nS) / SR
f0 = np.interp(t, [0, 0.12, 0.5, 1.3, 2.4], [90, 620, 760, 540, 220])
f0 = f0 * (1 + 0.06 * np.sin(2 * np.pi * 17 * t))
v1 = glottal(d, f0, jitter=0.12, shimmer=0.6)
v2 = glottal(d, f0 * 0.5, jitter=0.2, shimmer=0.8, fry=0.15)      # sub-harmonic growl under it
src = v1 + 0.8 * v2 + 0.15 * breath(d, 1500, 9000)
scream = hp(formants_moving(src, AH, EE, gains=(1.0, 0.9, 0.7, 0.5)), 280)
scream = np.tanh(4 * norm(scream, 1))                               # torn, overdriven throat
scream = scream * env_ad(nS, 0.02, 0.9, 0.8)
lead = 0.8 * croak(0.35, f0=60, vowel=AH, fry=0.0)                 # sharp intake before the scream
full = np.zeros(nS + len(lead)); full[:len(lead)] += lead; full[len(lead) // 2:len(lead) // 2 + nS] += scream
save('monster_scream', room(full, 1.4, 0.2), 0.99)

# ---------- "it sees you": low boom + short hiss-shriek + rattle ----------
d = 3.2; nS = int(d * SR); t = np.arange(nS) / SR
fb = 28 + 60 * np.exp(-t * 12)
boom = np.sin(2 * np.pi * np.cumsum(fb) / SR) * np.exp(-t * 1.2)
hiss = formants(breath(1.0, 2000, 9000), EE) * env_ad(int(1.0 * SR), 0.01, 0.8, 2)
sh_f0 = np.interp(np.arange(int(0.9 * SR)) / SR, [0, 0.1, 0.9], [300, 1100, 700])
shriek = formants(glottal(0.9, sh_f0, 0.15, 0.6), EE, (1, .9, .7, .5)) * env_ad(int(0.9 * SR), 0.01, 0.6)
sting = np.zeros(nS)
sting += 1.0 * boom
sting[:len(hiss)] += 0.6 * norm(hiss, 1)
sting[int(0.05 * SR):int(0.05 * SR) + len(shriek)] += 0.7 * norm(np.tanh(3 * norm(shriek, 1)), 1)
cr = croak(1.8, f0=30)
sting[int(1.2 * SR):int(1.2 * SR) + len(cr)] += 0.6 * norm(cr, 1)
save('monster_spotted', room(sting, 2.0, 0.3), 0.95)

# ---------- footsteps: heavy bony step, and a dragging foot ----------
d = 0.7; t = np.arange(int(d * SR)) / SR
thud = np.sin(2 * np.pi * np.cumsum(35 + 70 * np.exp(-t * 30)) / SR) * np.exp(-t * 9)
knock = bp(rng.standard_normal(len(t)), 600, 2500) * np.exp(-t * 60)
save('monster_step', room(thud + 0.5 * knock, 0.8, 0.15), 0.9)
d = 0.9; nS = int(d * SR)
scrape = bp(rng.standard_normal(nS), 900, 6000) * (0.6 + 0.4 * np.abs(np.sin(np.arange(nS) / SR * 2 * np.pi * 23)))
scrape *= env_ad(nS, 0.15, 0.4, 1.3)
save('monster_drag', room(scrape, 0.6, 0.12), 0.55)

# ---------- bone cracks for twitches ----------
d = 0.6; nS = int(d * SR); x = np.zeros(nS)
for k in range(rng.integers(4, 7)):
    at = int(rng.uniform(0, 0.35) * SR); ln = int(0.03 * SR)
    burst = rng.standard_normal(ln) * np.exp(-np.arange(ln) / (0.004 * SR))
    burst = resonator(burst, rng.uniform(1500, 3500), 300) + 0.5 * resonator(burst, rng.uniform(400, 900), 150)
    x[at:at + ln] += burst * rng.uniform(0.4, 1.0)
save('monster_crack', room(x, 0.5, 0.15), 0.85)
