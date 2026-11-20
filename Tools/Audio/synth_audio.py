"""
Same Shore - procedural sound design (numpy + scipy).

    python synth_audio.py <output_dir>

Every clip is synthesised from noise / oscillators, so the project has no third-party
audio licensing constraints. Loops are made seamless by computing their envelopes on
a periodic timeline and cross-fading the tail into the head.
"""
import os, sys
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt, sosfiltfilt

SR = 44100
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(11)


def write(name, x, stereo=False):
    x = np.asarray(x, dtype=np.float64)
    peak = np.max(np.abs(x)) + 1e-9
    x = x / peak * 0.89
    data = (x * 32767).astype(np.int16)
    if stereo and data.ndim == 1:
        data = np.stack([data, data], axis=1)
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, data)
    print("wav", name, f"{len(x) / SR:.1f}s")


def bp(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], btype="band", fs=SR, output="sos"), x)


def lp(x, f, order=2):
    return sosfilt(butter(order, f, btype="low", fs=SR, output="sos"), x)


def hp(x, f, order=2):
    return sosfilt(butter(order, f, btype="high", fs=SR, output="sos"), x)


def colored_noise(n, slope):
    """slope 0 = white, -1 = pink, -2 = brown (power exponent)."""
    spec = rng.normal(size=n // 2 + 1) + 1j * rng.normal(size=n // 2 + 1)
    f = np.fft.rfftfreq(n, 1 / SR)
    f[0] = f[1]
    spec *= f ** (slope / 2)
    x = np.fft.irfft(spec, n)
    return x / np.std(x)


def make_loop(x, fade_s=1.5):
    """Cross-fade the last fade_s seconds into the start so the clip loops seamlessly."""
    n = int(fade_s * SR)
    head, body, tail = x[:n], x[n:-n] if x.ndim == 1 else x[n:-n], x[-n:]
    w = np.linspace(0, 1, n)
    if x.ndim > 1:
        w = w[:, None]
    mixed = tail * (1 - w) + head * w
    return np.concatenate([mixed, x[n:-n]])


def smooth_env(points, n, period=None):
    """Piecewise-linear envelope from (time_s, value) points, optionally periodic."""
    t = np.arange(n) / SR
    pts = np.array(points)
    if period:
        pts = np.concatenate([pts - [period, 0], pts, pts + [period, 0]])
    return np.interp(t, pts[:, 0], pts[:, 1])


# ------------------------------------------------------------------ ocean
def ocean(duration, intensity, rumble, seed):
    r = np.random.default_rng(seed)
    n = int(duration * SR)
    t = np.arange(n) / SR
    chans = []
    events = []
    tt = 0.5
    while tt < duration:
        events.append((tt, r.uniform(0.6, 1.0)))
        tt += r.uniform(5.5, 8.5)
    for ch in range(2):
        hiss = hp(colored_noise(n, -0.6), 900)
        body = lp(colored_noise(n, -1.0), 1800)
        low = lp(colored_noise(n, -2.0), 160)
        env_crash = np.zeros(n)
        env_wash = np.zeros(n)
        for (et, a) in events:
            for k in (-1, 0, 1):                     # periodic -> seamless loop
                e = et + k * duration + ch * 0.07
                d = t - e
                build = np.where((d > -1.8) & (d < 0), (1 + d / 1.8) ** 3, 0)
                crash = np.where(d >= 0, np.exp(-d / 0.55), 0)
                wash = np.where(d >= 0, np.exp(-d / 2.8) * (1 - np.exp(-d / 0.3)), 0)
                env_crash += a * (0.35 * build + crash)
                env_wash += a * wash
        bed = 0.25 + 0.05 * np.sin(2 * np.pi * t / duration * 3 + ch)
        x = (body * (bed + 0.8 * env_crash) * intensity
             + hiss * (0.12 + 0.7 * env_wash) * intensity
             + low * (0.4 + 0.9 * env_crash) * rumble)
        chans.append(x)
    return np.stack(chans, axis=1)


write("ocean_waves_loop", make_loop(ocean(48, 1.0, 0.6, 1)))
write("ocean_waves_close_loop", make_loop(ocean(48, 1.25, 2.2, 2)))

# ------------------------------------------------------------------ wind
n = 40 * SR
t = np.arange(n) / SR
ch = []
for c in range(2):
    gust = smooth_env([(0, .3), (6, .6), (11, .35), (17, .8), (23, .4), (29, .65), (35, .3), (40, .3)], n, 40)
    w = bp(colored_noise(n, -1.0), 180, 1400) * gust + 0.25 * bp(colored_noise(n, 0), 2500, 6000) * gust ** 2
    ch.append(w)
write("wind_loop", make_loop(np.stack(ch, 1)))


# ------------------------------------------------------------------ distant hotel music
def tone(freq, dur, harmonics=(1, .5, .25, .12), decay=1.5):
    tt = np.arange(int(dur * SR)) / SR
    s = sum(a * np.sin(2 * np.pi * freq * (i + 1) * tt) for i, a in enumerate(harmonics))
    return s * np.exp(-tt * decay) * np.minimum(1, tt / 0.01)


bpm = 104
beat = 60 / bpm
bars = 8
n = int(bars * 4 * beat * SR)
music = np.zeros(n + SR)
chords = [(57, 60, 64, 67), (50, 57, 60, 65), (55, 59, 62, 65), (48, 55, 59, 64)]  # Am7 Dm7 G7 Cmaj7
midi = lambda m: 440 * 2 ** ((m - 69) / 12)
for b in range(bars):
    notes = chords[b % 4]
    start = int(b * 4 * beat * SR)
    for k, off in enumerate((0, 1.5, 2.5)):            # syncopated keys
        for m in notes:
            s = tone(midi(m), beat * 1.4, decay=2.5) * 0.18
            i = start + int(off * beat * SR)
            music[i:i + len(s)] += s
    bass = tone(midi(notes[0] - 12), beat * 3.5, (1, .3), decay=0.8) * 0.6
    music[start:start + len(bass)] += bass
    for q in range(4):                                  # soft kick + shaker
        i = start + int(q * beat * SR)
        kt = np.arange(int(0.25 * SR)) / SR
        kick = np.sin(2 * np.pi * (50 + 60 * np.exp(-kt * 30)) * kt) * np.exp(-kt * 12) * 0.7
        music[i:i + len(kick)] += kick
        sh = hp(rng.normal(size=int(0.06 * SR)), 5000) * np.exp(-np.arange(int(0.06 * SR)) / SR * 60) * 0.05
        j = i + int(beat * 0.5 * SR)
        music[j:j + len(sh)] += sh
music = music[:n]
music = lp(music, 1100, 4)          # heard from far away
write("hotel_music_loop", make_loop(music, 0.5))

# ------------------------------------------------------------------ muffled voices (babble)
n = 30 * SR
t = np.arange(n) / SR
babble = np.zeros(n)
for speaker in range(5):
    src = colored_noise(n, -0.5)
    syll = (np.sin(2 * np.pi * rng.uniform(3, 5) * t + rng.uniform(0, 6)) > rng.uniform(-0.2, 0.3)).astype(float)
    phrase = smooth_env([(0, 1), (2.5, 1), (3, 0), (4, 0), (4.5, 1), (8, 1), (8.4, 0), (10, 0), (10.5, 1), (30, 1)],
                        n, 30)
    env = lp(syll * np.roll(phrase, rng.integers(0, n)), 18)
    f1, f2 = rng.uniform(450, 750), rng.uniform(1000, 1600)
    voiced = bp(src, f1 * 0.8, f1 * 1.2) + 0.5 * bp(src, f2 * 0.85, f2 * 1.15)
    babble += voiced * env * rng.uniform(0.6, 1)
babble = lp(babble, 700, 4)         # muffled through sand / distance
write("voices_muffled_loop", make_loop(babble, 1.0))


# ------------------------------------------------------------------ one-shots
def burst(dur, lo, hi, attack, decay, slope=-1.0):
    n = int(dur * SR)
    tt = np.arange(n) / SR
    x = bp(colored_noise(n, slope), lo, hi)
    return x * np.minimum(1, tt / attack) * np.exp(-tt / decay)


for i in range(4):
    step = burst(0.35, 150, 2400, 0.012, 0.07) + 0.4 * burst(0.35, 2000, 7000, 0.02, 0.05, 0)
    grains = (rng.random(len(step)) > 0.995) * rng.normal(size=len(step)) * np.exp(-np.arange(len(step)) / SR / 0.08)
    write(f"footstep_sand_{i + 1}", step + 0.6 * hp(grains, 1500))

for i in range(3):
    n = int(0.55 * SR)
    tt = np.arange(n) / SR
    env = np.sin(np.pi * np.clip(tt / 0.45, 0, 1)) ** 1.5
    swish = bp(colored_noise(n, -0.3), 900 + 300 * i, 6000) * env
    write(f"flipper_stroke_{i + 1}", swish)

n = 12 * SR
t = np.arange(n) / SR
drag = bp(colored_noise(n, -0.8), 300, 4000) * (0.6 + 0.4 * np.sin(2 * np.pi * 0.7 * t) ** 2)
write("sand_drag_loop", make_loop(drag, 0.6))

n = 6 * SR
pour = bp(colored_noise(n, -0.2), 1500, 9000) * (0.8 + 0.2 * lp(rng.normal(size=n), 8) * 8)
write("sand_pour_loop", make_loop(pour, 0.4))

for i in range(3):
    calls = []
    for k in range(rng.integers(2, 5)):
        dur = rng.uniform(0.18, 0.35)
        tt = np.arange(int(dur * SR)) / SR
        f0 = rng.uniform(950, 1300) * (1 + 0.35 * np.sin(np.pi * tt / dur)) * (1 - 0.25 * tt / dur)
        ph = 2 * np.pi * np.cumsum(f0) / SR
        sig = sum((0.6 ** h) * np.sin((h + 1) * ph) for h in range(6))
        sig = bp(sig, 900, 4500) * np.sin(np.pi * tt / dur) ** 0.6
        calls.append(sig)
        calls.append(np.zeros(int(rng.uniform(0.05, 0.12) * SR)))
    write(f"gull_call_{i + 1}", np.concatenate(calls))

n = int(1.2 * SR)
flap = np.zeros(n)
for k in range(5):
    s = burst(0.15, 80, 900, 0.01, 0.05, -1.5)
    i = int(k * 0.21 * SR)
    flap[i:i + len(s)] += s * (1 - k * 0.12)
write("gull_flap", flap)

n = int(0.08 * SR)
tt = np.arange(n) / SR
click = (np.sin(2 * np.pi * 2400 * tt) * np.exp(-tt * 120) + hp(rng.normal(size=n), 3000) * np.exp(-tt * 300) * 0.5)
write("flashlight_click", click)

write("thud_sand", burst(0.4, 60, 1200, 0.004, 0.06, -1.5))
write("pickup_rustle", burst(0.3, 1500, 8000, 0.03, 0.1, -0.2))

n = 2 * SR
tt = np.arange(n) / SR
hb = np.zeros(n)
for k, off in enumerate((0.0, 0.28, 1.0, 1.28)):
    i = int(off * SR)
    m = int(0.18 * SR)
    st = np.arange(m) / SR
    hb[i:i + m] += np.sin(2 * np.pi * 55 * st) * np.exp(-st * 25) * (1 if k % 2 == 0 else 0.7)
write("heartbeat_loop", hb)

n = int(3.5 * SR)
tt = np.arange(n) / SR
chime = np.zeros(n)
for k, m in enumerate((72, 76, 79, 84)):
    s = tone(midi(m), 3.5 - k * 0.25, (1, .3, .1), decay=1.3)
    i = int(k * 0.18 * SR)
    chime[i:i + len(s)] += s[:n - i]
write("success_chime", chime)

n = int(4.0 * SR)
tt = np.arange(n) / SR
swell = lp(colored_noise(n, -1.0), 600) * np.sin(np.pi * tt / 4.0) ** 2
write("transition_swell", swell)
print("AUDIO DONE")
