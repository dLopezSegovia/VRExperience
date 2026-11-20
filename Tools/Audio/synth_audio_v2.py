"""
Same Shore - additional procedural sounds for the narrative redesign.
    python synth_audio_v2.py <output_dir>
"""
import os, sys
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

SR = 44100
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(23)


def write(name, x):
    x = np.asarray(x, dtype=np.float64)
    x = x / (np.max(np.abs(x)) + 1e-9) * 0.89
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (x * 32767).astype(np.int16))
    print("wav", name, f"{x.shape[0] / SR:.1f}s")


def bp(x, lo, hi, o=2): return sosfilt(butter(o, [lo, hi], btype="band", fs=SR, output="sos"), x)
def lp(x, f, o=2): return sosfilt(butter(o, f, btype="low", fs=SR, output="sos"), x)
def hp(x, f, o=2): return sosfilt(butter(o, f, btype="high", fs=SR, output="sos"), x)


def noise(n, slope):
    spec = rng.normal(size=n // 2 + 1) + 1j * rng.normal(size=n // 2 + 1)
    f = np.fft.rfftfreq(n, 1 / SR); f[0] = f[1]
    x = np.fft.irfft(spec * f ** (slope / 2), n)
    return x / np.std(x)


def loop(x, fade=0.5):
    n = int(fade * SR)
    w = np.linspace(0, 1, n)
    return np.concatenate([x[-n:] * (1 - w) + x[:n] * w, x[n:-n]])


t_ = lambda d: np.arange(int(d * SR)) / SR

# hatchling breathing: soft nasal in/out puffs (loop)
d = 6.0
t = t_(d)
breath = np.zeros_like(t)
for k in range(3):
    c = k * 2.0
    inh = np.exp(-((t - c - 0.35) / 0.22) ** 2)
    exh = np.exp(-((t - c - 1.15) / 0.3) ** 2) * 0.8
    breath += inh + exh
write("turtle_breath_loop", loop(bp(noise(len(t), -0.5), 400, 2600) * breath, 0.3))

# scratching / sand shifting under the surface (hatching)
d = 5.0
t = t_(d)
scr = np.zeros_like(t)
for _ in range(70):
    c = rng.uniform(0.1, d - 0.2)
    g = np.exp(-((t - c) / rng.uniform(0.01, 0.04)) ** 2) * rng.uniform(0.3, 1)
    scr += g
write("nest_scratching", bp(noise(len(t), -0.2), 1200, 7000) * scr + 0.3 * lp(noise(len(t), -1.5), 200) * scr)

# swash: water rushing up the sand and hissing back
d = 4.0
t = t_(d)
env = np.clip(t / 0.4, 0, 1) * np.exp(-np.clip(t - 0.4, 0, None) / 1.3)
write("swash_wash", bp(noise(len(t), -0.3), 600, 9000) * env + 0.5 * lp(noise(len(t), -1.2), 500) * env)

# big final wave: build, crash, roar
d = 5.0
t = t_(d)
env = np.where(t < 1.6, (t / 1.6) ** 2.5, np.exp(-(t - 1.6) / 1.4))
write("wave_crash_big", lp(noise(len(t), -1.0), 3000) * env + 0.6 * lp(noise(len(t), -2.0), 160) * env)

# underwater ambience (loop): low rumble + soft bubbles
d = 10.0
t = t_(d)
under = lp(noise(len(t), -2.0), 300) * 0.8
for _ in range(60):
    c = rng.uniform(0, d)
    f0 = rng.uniform(400, 1400)
    tt = t - c
    m = (tt > 0) & (tt < 0.08)
    under[m] += np.sin(2 * np.pi * f0 * (1 + 3 * tt[m]) * tt[m]) * np.exp(-tt[m] * 50) * 0.4
write("underwater_loop", loop(under, 0.5))

# objective complete chime (soft two-note bell)
def bell(f, d, decay):
    tt = t_(d)
    return sum(a * np.sin(2 * np.pi * f * m * tt) for m, a in ((1, 1), (2.01, 0.35), (3.02, 0.12))) * np.exp(-tt * decay) * np.minimum(1, tt / 0.005)
c = np.zeros(int(1.6 * SR))
b1 = bell(784, 1.2, 4); b2 = bell(1175, 1.3, 4)
c[:len(b1)] += b1; c[int(0.12 * SR):int(0.12 * SR) + len(b2)] += b2[:len(c) - int(0.12 * SR)] * 0.8
write("objective_done", c)

# objective "left behind" (muted low note)
write("objective_left", lp(bell(330, 1.0, 6), 1200))

# UI tick
tt = t_(0.05)
write("ui_tick", np.sin(2 * np.pi * 1800 * tt) * np.exp(-tt * 140))

# artificial light hum (loop): eerie beating sines
d = 8.0
t = t_(d)
hum = np.sin(2 * np.pi * 220 * t) + 0.7 * np.sin(2 * np.pi * 223.5 * t) + 0.3 * np.sin(2 * np.pi * 440.7 * t)
hum *= 0.6 + 0.4 * np.sin(2 * np.pi * 0.25 * t)
write("light_hum_loop", loop(hum + 0.05 * bp(noise(len(t), 0), 2000, 6000), 0.5))

# seagull screech (attack warning): harsh descending call, repeated
calls = []
for k in range(3):
    dd = 0.45
    tt = t_(dd)
    f0 = 1700 * (1 - 0.35 * tt / dd) * (1 + 0.04 * np.sin(2 * np.pi * 38 * tt))
    ph = 2 * np.pi * np.cumsum(f0) / SR
    sig = sum((0.7 ** h) * np.sin((h + 1) * ph) for h in range(8))
    sig = bp(sig + 0.3 * noise(len(tt), 0), 900, 6000) * np.sin(np.pi * tt / dd) ** 0.4
    calls += [sig, np.zeros(int(0.08 * SR))]
write("gull_screech", np.concatenate(calls))

# wind whoosh of a diving bird
tt = t_(1.2)
env = np.exp(-((tt - 0.75) / 0.25) ** 2)
write("gull_dive_whoosh", bp(noise(len(tt), -0.6), 300, 3500) * env)

# ending pad: warm chord swelling (Cmaj9)
d = 12.0
t = t_(d)
midi = lambda m: 440 * 2 ** ((m - 69) / 12)
pad = sum(np.sin(2 * np.pi * midi(m) * t + 0.3 * np.sin(2 * np.pi * 0.2 * t)) * a
          for m, a in ((48, 0.6), (55, 0.5), (60, 0.45), (64, 0.4), (67, 0.3), (74, 0.2)))
pad *= np.minimum(1, t / 3.5) * np.minimum(1, (d - t) / 3)
write("ending_pad", lp(pad, 2500))
print("AUDIO V2 DONE")
