"""
Same Shore - sounds for the storyboard v2 (day at the beach -> hatchling).
    python synth_audio_v3.py <output_dir>
All sounds are procedural (own work, no third-party samples).
"""
import os, sys
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

SR = 44100
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(31)


def write(name, x, peak=0.89):
    x = np.asarray(x, dtype=np.float64)
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (x * 32767).astype(np.int16))
    print("wav", name, f"{x.shape[0] / SR:.1f}s")


def bp(x, lo, hi, o=2): return sosfilt(butter(o, [lo, hi], btype="band", fs=SR, output="sos"), x)
def lp(x, f, o=2): return sosfilt(butter(o, f, btype="low", fs=SR, output="sos"), x)
def hp(x, f, o=2): return sosfilt(butter(o, f, btype="high", fs=SR, output="sos"), x)
t_ = lambda d: np.arange(int(d * SR)) / SR


def noise(n, slope=0.0):
    spec = rng.normal(size=n // 2 + 1) + 1j * rng.normal(size=n // 2 + 1)
    f = np.fft.rfftfreq(n, 1 / SR); f[0] = f[1]
    x = np.fft.irfft(spec * f ** (slope / 2), n)
    return x / np.std(x)


def env(t, a, d):
    return np.minimum(1, t / max(a, 1e-4)) * np.exp(-np.maximum(0, t - a) / d)


def place(buf, x, at):
    i = int(at * SR)
    n = min(len(x), len(buf) - i)
    if n > 0: buf[i:i + n] += x[:n]


def pluck(freq, dur, bright=0.5):
    """Karplus-Strong plucked string (ukulele / guitar)."""
    n = int(dur * SR)
    p = max(2, int(SR / freq))
    buf = rng.uniform(-1, 1, p)
    buf = lp(buf, 1500 + 5000 * bright, 1)
    out = np.zeros(n)
    for i in range(n):
        out[i] = buf[i % p]
        buf[i % p] = 0.5 * (buf[i % p] + buf[(i + 1) % p]) * 0.996
    return out * np.exp(-t_(dur) * 1.2)


# ---------------------------------------------------------------- beach music (player's speaker), 4 bars loop
bpm = 96
beat = 60 / bpm
bars = 8
dur = bars * 4 * beat
music = np.zeros(int(dur * SR) + SR)
# I - vi - IV - V in C (ukulele voicings)
chords = [[261.6, 329.6, 392.0, 523.3], [220.0, 261.6, 329.6, 440.0], [174.6, 220.0, 261.6, 349.2], [196.0, 246.9, 293.7, 392.0]]
bass = [65.4, 55.0, 87.3, 98.0]
cache = {}
def pl(f, d, b=0.5):
    k = (round(f, 1), d, b)
    if k not in cache: cache[k] = pluck(f, d, b)
    return cache[k]
strum = [0, 1.5, 2, 3, 3.5]
for bar in range(bars):
    ch = chords[bar % 4]
    for s in strum:
        at = (bar * 4 + s) * beat
        for j, f in enumerate(ch):
            place(music, pl(f, 1.2, 0.55) * (0.22 if s % 1 == 0 else 0.15), at + j * 0.012)
    # bass on 1 and 3
    for s in (0, 2):
        tb = t_(beat * 1.6)
        b = np.sin(2 * np.pi * bass[bar % 4] * tb) * env(tb, 0.01, 0.5) + 0.3 * np.sin(4 * np.pi * bass[bar % 4] * tb) * env(tb, 0.01, 0.2)
        place(music, b * 0.5, (bar * 4 + s) * beat)
    # shaker on eighths
    for e in range(8):
        n = hp(noise(int(0.09 * SR)), 5000) * env(t_(0.09), 0.005, 0.03)
        place(music, n * (0.05 if e % 2 else 0.08), (bar * 4 + e * 0.5) * beat)
# whistled melody (recognisable hook) in bars 5-8
mel = [(659.3, 0, 1), (784.0, 1, 0.5), (659.3, 1.5, 0.5), (587.3, 2, 1), (523.3, 3, 1),
       (523.3, 4, 1), (587.3, 5, 0.5), (659.3, 5.5, 0.5), (587.3, 6, 2),
       (659.3, 8, 1), (784.0, 9, 0.5), (880.0, 9.5, 0.5), (784.0, 10, 1), (659.3, 11, 1),
       (587.3, 12, 1), (659.3, 13, 0.5), (587.3, 13.5, 0.5), (523.3, 14, 2)]
for f, s, d in mel:
    tm = t_(d * beat)
    vib = np.sin(2 * np.pi * 5.2 * tm) * 0.004
    tone = np.sin(2 * np.pi * f * tm * (1 + vib)) + 0.08 * np.sin(4 * np.pi * f * tm)
    a = np.minimum(1, tm / 0.04) * np.minimum(1, (d * beat - tm) / 0.08)
    place(music, tone * a * 0.16, (16 + s) * beat)
music = music[:int(dur * SR)]
# seamless loop: the tail rings into the start
tail = int(0.6 * SR)
music[:tail] += np.zeros(tail)
write("beach_music_loop", lp(music, 9000), 0.8)

# ---------------------------------------------------------------- fireworks
d = 1.6; t = t_(d)
whistle_f = 900 + 1400 * (t / d)
launch = np.sin(2 * np.pi * np.cumsum(whistle_f) / SR) * np.exp(-((t - 0.9) / 0.6) ** 2) * 0.35
thump = lp(noise(len(t)), 300) * env(t, 0.003, 0.05)
hiss = bp(noise(len(t)), 2000, 7000) * np.exp(-((t - 0.6) / 0.5) ** 2) * 0.25
write("firework_launch", thump * 1.4 + launch + hiss)

d = 3.2; t = t_(d)
boom = lp(noise(len(t), -1.0), 220) * env(t, 0.004, 0.45) * 2.5
crack = bp(noise(len(t)), 600, 4000) * env(t, 0.002, 0.08)
crackle = np.zeros(len(t))
for k in range(70):
    at = 0.25 + rng.uniform(0, 1.8)
    pop = hp(noise(int(0.02 * SR)), 2500) * env(t_(0.02), 0.001, 0.004)
    place(crackle, pop * rng.uniform(0.2, 0.6) * np.exp(-at / 1.4), at)
echo = np.zeros(len(t))
place(echo, lp(boom, 150) * 0.35, 0.42)
write("firework_burst", boom + crack * 0.6 + crackle + echo)

# distant, muffled (for the hatchling it is felt more than heard)
write("firework_rumble", lp(boom + echo, 120, 3) + lp(crackle, 800) * 0.2)

# ---------------------------------------------------------------- cans
d = 0.7; t = t_(d)
partials = [(1180, 1), (2630, 0.6), (3910, 0.4), (5200, 0.25)]
clank = sum(a * np.sin(2 * np.pi * f * t) * np.exp(-t * (8 + f / 600)) for f, a in partials)
write("can_clank", clank + hp(noise(len(t)), 3000) * env(t, 0.001, 0.01) * 0.5)
d = 0.5; t = t_(d)
write("can_bin", clank[:len(t)] * 0.5 + lp(noise(len(t)), 900) * env(t, 0.002, 0.06) +
      sum(0.4 * np.sin(2 * np.pi * f * t) * np.exp(-t * 10) for f in (420, 690)))
d = 0.35; t = t_(d)
write("can_open", hp(noise(len(t)), 2500) * env(t, 0.002, 0.07) + bp(noise(len(t)), 300, 900) * env(t, 0.001, 0.02))

# ---------------------------------------------------------------- drink / eat
d = 0.9; t = t_(d)
glug = np.zeros(len(t))
for k in range(4):
    tg = t_(0.12)
    f = 260 + 90 * k + 400 * tg / 0.12
    glug_k = np.sin(2 * np.pi * np.cumsum(f) / SR) * env(tg, 0.01, 0.04)
    place(glug, glug_k, 0.1 + k * 0.17)
write("sip", glug + bp(noise(len(t)), 800, 3000) * 0.05 * np.exp(-t * 3))
d = 0.45; t = t_(d)
crunch = np.zeros(len(t))
for k in range(9):
    place(crunch, bp(noise(int(0.03 * SR)), 1200, 6000) * env(t_(0.03), 0.001, 0.008) * rng.uniform(0.4, 1), rng.uniform(0, 0.25))
write("bite", crunch + lp(noise(len(t)), 400) * env(t, 0.005, 0.06) * 0.4)
d = 1.0; t = t_(d)
write("wrapper_rustle", bp(noise(len(t)), 1500, 9000) * (0.5 + 0.5 * np.abs(np.sin(2 * np.pi * 7 * t))) * env(t, 0.05, 0.4))

# ---------------------------------------------------------------- wind gust
d = 4.0; t = t_(d)
g = np.exp(-((t - 1.5) / 0.9) ** 2)
wind = bp(noise(len(t), -1.0), 150, 1600) * g + bp(noise(len(t)), 1500, 4000) * g * 0.25
write("wind_gust", wind)

# ---------------------------------------------------------------- sand tools
d = 0.6; t = t_(d)
write("shovel_dig", bp(noise(len(t), -0.5), 300, 3500) * env(t, 0.05, 0.18) + lp(noise(len(t)), 250) * env(t, 0.01, 0.06) * 0.6)
d = 0.8; t = t_(d)
write("sand_dump", bp(noise(len(t), -0.5), 200, 2500) * env(t, 0.03, 0.3))
d = 0.5; t = t_(d)
write("bucket_thump", lp(noise(len(t)), 220) * env(t, 0.003, 0.08) * 2 + np.sin(2 * np.pi * 95 * t) * env(t, 0.003, 0.1))
d = 1.2; t = t_(d)
write("castle_done", sum(0.3 * np.sin(2 * np.pi * f * t) * np.exp(-t * 2.5) * (t > dl) for f, dl in ((523.3, 0), (659.3, 0.12), (784, 0.24), (1046.5, 0.36))))

# ---------------------------------------------------------------- lights / phone
d = 0.25; t = t_(d)
write("hook_clip", hp(noise(len(t)), 2000) * env(t, 0.001, 0.015) + np.sin(2 * np.pi * 1900 * t) * env(t, 0.001, 0.03) * 0.5)
d = 1.4; t = t_(d)
write("lights_on", sum(0.25 * np.sin(2 * np.pi * f * t) * env(t - dl, 0.01, 0.35) * (t > dl) for f, dl in ((880, 0), (1108.7, 0.1), (1318.5, 0.2), (1760, 0.3))))
d = 0.35; t = t_(d)
shutter = hp(noise(len(t)), 1500) * (env(t, 0.001, 0.012) + 0.7 * env(np.maximum(0, t - 0.09), 0.001, 0.015) * (t > 0.09))
write("camera_shutter", shutter)
d = 0.4; t = t_(d)
write("flash_whine", np.sin(2 * np.pi * np.cumsum(3000 + 5000 * t / d) / SR) * 0.08 * np.minimum(1, t / 0.2))
d = 0.2; t = t_(d)
write("button_click", hp(noise(len(t)), 3000) * env(t, 0.001, 0.006) + np.sin(2 * np.pi * 2200 * t) * env(t, 0.001, 0.01) * 0.3)

# ---------------------------------------------------------------- hatchling: plastic, discomfort, final gull attack
d = 1.6; t = t_(d)
gag = np.zeros(len(t))
for k in range(3):
    tg = t_(0.22)
    x = bp(noise(len(tg), -0.5), 250, 1400) * env(tg, 0.02, 0.07)
    place(gag, x * (1 - k * 0.2), 0.1 + k * 0.45)
write("turtle_gag", gag)
d = 1.2; t = t_(d)
write("plastic_crinkle", bp(noise(len(t)), 2500, 10000) * np.abs(np.sin(2 * np.pi * 11 * t)) ** 3 * env(t, 0.02, 0.5))
d = 2.2; t = t_(d)
wings = np.zeros(len(t))
for k in range(16):
    tw = t_(0.1)
    place(wings, lp(noise(len(tw), -1), 600) * env(tw, 0.01, 0.035) * (0.35 + 0.65 * k / 16), k * 0.12)
write("wings_fast", wings)
d = 0.8; t = t_(d)
write("gull_strike", lp(noise(len(t)), 350) * env(t, 0.002, 0.05) * 2 + bp(noise(len(t)), 900, 3000) * env(t, 0.002, 0.03))
