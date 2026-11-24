"""Sky Bot - Week 12 sound generator.

Every sound made by this script is synthesised from plain math (sine, square,
triangle, noise + envelopes). No samples from the internet are used, so the
files are our own work and are released as CC0 (see README.md -> Sound sources).

Run from the project root:   python Tools/gen_sounds.py
Output:  Assets/Audio/SFX/*.wav   and   Assets/Audio/Music/music_level.wav
"""
import os
import wave

import numpy as np
from scipy.signal import butter, lfilter

SR = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SFX_DIR = os.path.join(ROOT, "Assets", "Audio", "SFX")
MUSIC_DIR = os.path.join(ROOT, "Assets", "Audio", "Music")
rng = np.random.default_rng(12)   # fixed seed -> the same files every run


# ---------------- helpers ----------------

def t_axis(sec):
    return np.arange(int(sec * SR)) / SR


def env_ad(n, attack, decay_pow=1.0):
    """attack seconds linear up, then a curved fall to 0 at the end."""
    e = np.ones(n)
    a = max(1, int(attack * SR))
    e[:a] = np.linspace(0, 1, a)
    rest = n - a
    if rest > 0:
        e[a:] = np.linspace(1, 0, rest) ** decay_pow
    return e


def sweep(f0, f1, sec, shape="sine", curve=1.0):
    """tone whose pitch glides f0 -> f1 (phase is integrated, so no clicks)."""
    n = int(sec * SR)
    k = np.linspace(0, 1, n) ** curve
    f = f0 + (f1 - f0) * k
    ph = 2 * np.pi * np.cumsum(f) / SR
    return osc(ph, shape)


def osc(ph, shape):
    if shape == "sine":
        return np.sin(ph)
    if shape == "square":
        return np.sign(np.sin(ph))
    if shape == "pulse25":
        return np.where((ph / (2 * np.pi)) % 1.0 < 0.25, 1.0, -1.0)
    if shape == "saw":
        return 2 * ((ph / (2 * np.pi)) % 1.0) - 1
    if shape == "tri":
        return 2 * np.abs(2 * ((ph / (2 * np.pi)) % 1.0) - 1) - 1
    raise ValueError(shape)


def noise(sec):
    return rng.uniform(-1, 1, int(sec * SR))


def lowpass(x, hz, order=2):
    b, a = butter(order, hz / (SR / 2), "low")
    return lfilter(b, a, x)


def highpass(x, hz, order=2):
    b, a = butter(order, hz / (SR / 2), "high")
    return lfilter(b, a, x)


def bandpass(x, lo, hi, order=2):
    b, a = butter(order, [lo / (SR / 2), hi / (SR / 2)], "band")
    return lfilter(b, a, x)


def fade_edges(x, ms=3):
    """tiny fade in/out so a clip never starts or ends with a click."""
    n = int(SR * ms / 1000)
    x = x.copy()
    x[:n] *= np.linspace(0, 1, n)
    x[-n:] *= np.linspace(1, 0, n)
    return x


def normalize(x, peak_db=-1.0):
    peak = np.max(np.abs(x)) or 1.0
    return x / peak * (10 ** (peak_db / 20))


def write(path, x, peak_db=-1.0, edges=True):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if edges:
        x = fade_edges(x)
    x = normalize(x, peak_db)
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print(f"{os.path.relpath(path, ROOT):45s} {len(x) / SR:5.2f} s")


def note_hz(n):
    """MIDI note number -> Hz (69 = A4 = 440 Hz)."""
    return 440.0 * 2 ** ((n - 69) / 12)


# ---------------- sound effects ----------------

def ui_click():
    sec = 0.05
    t = t_axis(sec)
    body = np.sin(2 * np.pi * 1900 * t) * np.exp(-t * 90)
    tick = highpass(noise(sec), 3000) * np.exp(-t * 400) * 0.6
    return body + tick


def ui_click_2():
    sec = 0.05
    t = t_axis(sec)
    body = np.sin(2 * np.pi * 1600 * t) * np.exp(-t * 80)
    tick = highpass(noise(sec), 2500) * np.exp(-t * 350) * 0.5
    return body + tick


def lose():
    # three falling notes + a slow sliding last note: "game over"
    parts = []
    for midi, sec in [(67, 0.22), (63, 0.22), (60, 0.22)]:
        n = int(sec * SR)
        ph = 2 * np.pi * note_hz(midi) * np.arange(n) / SR
        tone = 0.6 * osc(ph, "square") + 0.4 * osc(ph, "tri")
        parts.append(lowpass(tone, 3000) * env_ad(n, 0.005, 0.6))
    last = sweep(note_hz(59), note_hz(47), 0.9, "square", 0.7) * 0.6 + sweep(note_hz(59), note_hz(47), 0.9, "tri", 0.7) * 0.4
    t = t_axis(0.9)
    vib = 1 + 0.15 * np.sin(2 * np.pi * 6 * t)
    parts.append(lowpass(last, 2200) * env_ad(len(t), 0.01, 1.4) * vib)
    return np.concatenate(parts)


def windup_rifle():
    # Rifleman aims for 0.6 s: a laser-sight "charge" - beeps that speed up and rise
    sec = 0.6
    t = t_axis(sec)
    tone = sweep(700, 1500, sec, "pulse25", 1.0)
    rate = 8 + 22 * (t / sec)                       # beeps per second 8 -> 30
    gate = (np.sin(2 * np.pi * np.cumsum(rate) / SR) > -0.2).astype(float)
    gate = lowpass(gate, 300)                       # soft edges on every beep
    hum = sweep(220, 440, sec, "sine") * 0.25
    x = lowpass(tone, 4500) * gate * 0.7 + hum
    return x * np.linspace(0.45, 1.0, len(t))


def windup_mg():
    # Machine gun spins up for 0.7 s: motor whine + rattle that gets faster
    sec = 0.7
    t = t_axis(sec)
    motor = sweep(60, 240, sec, "saw", 0.8)
    motor = lowpass(motor, 1800) * 0.7
    whine = sweep(500, 1900, sec, "sine", 0.8) * 0.25
    clicks_rate = 12 + 45 * (t / sec) ** 1.2
    ph = np.cumsum(clicks_rate) / SR
    click_env = np.exp(-((ph % 1.0) * 30))          # a short tick every barrel turn
    rattle = bandpass(noise(sec), 1500, 6000) * click_env * 0.8
    return (motor + whine + rattle) * np.linspace(0.4, 1.0, len(t))


def windup_brawler():
    # Brawler pulls back the punch for 0.6 s: low growl + rising whoosh
    sec = 0.6
    t = t_axis(sec)
    growl = sweep(85, 140, sec, "saw", 1.0)
    growl = lowpass(growl, 700) * (1 + 0.5 * np.sin(2 * np.pi * 28 * t)) * 0.7
    air = noise(sec)
    cutoff_env = np.linspace(0, 1, len(t)) ** 2
    whoosh = lowpass(air, 900) * (1 - cutoff_env) + bandpass(air, 900, 4000) * cutoff_env
    whoosh *= np.linspace(0.1, 1.0, len(t)) ** 1.5 * 0.9
    return growl + whoosh


def enemy_hit(variant):
    sec = 0.14
    t = t_axis(sec)
    f = [380, 330][variant]
    thud = sweep(f, f * 0.45, sec, "sine", 0.5) * np.exp(-t * 28)
    ring = np.sin(2 * np.pi * [2350, 2750][variant] * t) * np.exp(-t * 45) * 0.3
    crack = highpass(noise(sec), 2000) * np.exp(-t * 120) * 0.55
    return thud + ring + crack


def enemy_death():
    sec = 0.8
    t = t_axis(sec)
    boom = sweep(150, 38, sec, "sine", 0.6) * np.exp(-t * 4.5)
    crunch = lowpass(noise(sec), 2500) * np.exp(-t * 7) * 0.7
    zap = sweep(1300, 160, 0.35, "square", 0.6)
    zap = np.concatenate([lowpass(zap, 3000) * env_ad(len(zap), 0.005, 1.5) * 0.35, np.zeros(len(t) - len(zap))])
    return boom + crunch + zap


def jump_2():
    # a second jump clip so repeated jumps alternate between two sounds
    sec = 0.16
    t = t_axis(sec)
    tone = sweep(330, 760, sec, "pulse25", 0.7)
    return lowpass(tone, 3500) * env_ad(len(t), 0.004, 1.2) * 0.8 + sweep(160, 380, sec, "sine") * env_ad(len(t), 0.004, 1.2) * 0.3


def pickup_2():
    # second coin clip: two quick rising notes
    out = []
    for midi in (84, 91):
        n = int(0.07 * SR)
        ph = 2 * np.pi * note_hz(midi) * np.arange(n) / SR
        out.append((osc(ph, "square") * 0.5 + osc(ph, "sine") * 0.5) * env_ad(n, 0.002, 1.5))
    return lowpass(np.concatenate(out), 6000)


# ---------------- level music ----------------

def music_level():
    """16 bars, 132 BPM, A minor, chiptune: bass + arpeggio + lead + drums. Loops seamlessly."""
    bpm = 132
    beat = 60.0 / bpm
    step = beat / 4                         # 16th note
    bars = 16
    steps_total = bars * 16
    n_total = int(round(steps_total * step * SR))
    mix = np.zeros(n_total + SR * 2)        # +2 s tail, wrapped to the start at the end

    def put(sig, start_step, gain):
        i = int(round(start_step * step * SR))
        mix[i:i + len(sig)] += sig * gain

    def tone(midi, length_steps, shape, decay=1.0, attack=0.004):
        n = int(length_steps * step * SR)
        ph = 2 * np.pi * note_hz(midi) * np.arange(n) / SR
        return osc(ph, shape) * env_ad(n, attack, decay)

    # chord roots per bar (A minor - F - C - G, second half Am - F - G - E)
    prog = [57, 53, 48, 55, 57, 53, 48, 55, 57, 53, 55, 52, 57, 53, 55, 52]
    quality = {57: "m", 53: "M", 48: "M", 55: "M", 52: "M"}

    for bar, root in enumerate(prog):
        s0 = bar * 16
        third = 3 if quality[root] == "m" else 4
        chord = [root, root + third, root + 7, root + 12]

        # bass: driving 8th notes, octave jump on the off-beat
        for k in range(8):
            midi = root - 12 + (12 if k % 2 else 0)
            put(lowpass(tone(midi, 2, "tri", 0.6), 2000), s0 + k * 2, 0.55)

        # arpeggio: 16th notes up and down the chord
        pattern = [0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 2, 1, 0, 1, 2, 3]
        for k, idx in enumerate(pattern):
            put(lowpass(tone(chord[idx] + 12, 1, "pulse25", 1.8), 5000), s0 + k, 0.12)

        # drums: kick on 1 and 3 (+ an extra), snare on 2 and 4, hats on every 8th
        for k in (0, 8, 10 if bar % 2 else 0):
            kn = int(0.18 * SR)
            kick = sweep(150, 45, 0.18, "sine", 0.4) * np.exp(-np.arange(kn) / SR * 18)
            put(kick, s0 + k, 0.9)
        for k in (4, 12):
            sn = int(0.16 * SR)
            snare = bandpass(rng.uniform(-1, 1, sn), 900, 7000) * np.exp(-np.arange(sn) / SR * 22)
            snare += np.sin(2 * np.pi * 190 * np.arange(sn) / SR) * np.exp(-np.arange(sn) / SR * 30) * 0.5
            put(snare, s0 + k, 0.35)
        for k in range(0, 16, 2):
            hn = int(0.04 * SR)
            hat = highpass(rng.uniform(-1, 1, hn), 7000) * np.exp(-np.arange(hn) / SR * 90)
            put(hat, s0 + k, 0.18 if k % 4 else 0.25)

    # lead melody (bars 5-8 and 13-16), [midi, start 16th inside the 4-bar phrase, length]
    phrase_a = [(76, 0, 4), (74, 4, 2), (72, 6, 2), (74, 8, 6), (69, 14, 2),
                (72, 16, 4), (71, 20, 2), (69, 22, 2), (67, 24, 6), (64, 30, 2),
                (72, 32, 4), (74, 36, 2), (76, 38, 2), (79, 40, 6), (76, 46, 2),
                (74, 48, 4), (72, 52, 2), (71, 54, 2), (69, 56, 8)]
    phrase_b = [(81, 0, 4), (79, 4, 2), (76, 6, 2), (77, 8, 6), (76, 14, 2),
                (74, 16, 4), (72, 20, 2), (74, 22, 2), (79, 24, 6), (76, 30, 2),
                (74, 32, 4), (76, 36, 2), (77, 38, 2), (79, 40, 6), (77, 46, 2),
                (76, 48, 6), (71, 54, 2), (68, 56, 8)]
    for start_bar, phrase in ((4, phrase_a), (12, phrase_b)):
        for midi, st, ln in phrase:
            n = int(ln * step * SR)
            t = np.arange(n) / SR
            vib = 1 + 0.004 * np.sin(2 * np.pi * 5.5 * t) * np.clip(t * 4, 0, 1)
            ph = 2 * np.pi * np.cumsum(note_hz(midi) * vib) / SR
            sig = (osc(ph, "square") * 0.6 + osc(ph, "tri") * 0.4) * env_ad(n, 0.01, 0.5)
            put(lowpass(sig, 3500), start_bar * 16 + st, 0.2)

    # pad in bars 1-4 and 9-12 so the track does not feel empty there
    for bar in list(range(0, 4)) + list(range(8, 12)):
        root = prog[bar]
        third = 3 if quality[root] == "m" else 4
        for iv in (0, third, 7):
            put(lowpass(tone(root + iv + 12, 16, "saw", 0.3, 0.15), 1200), bar * 16, 0.06)

    # seamless loop: whatever rings past the end is added to the beginning
    tail = mix[n_total:].copy()
    loop = mix[:n_total]
    loop[:len(tail)] += tail
    return loop


def main():
    write(os.path.join(SFX_DIR, "ui_click.wav"), ui_click(), -6)
    write(os.path.join(SFX_DIR, "ui_click_2.wav"), ui_click_2(), -6)
    write(os.path.join(SFX_DIR, "lose.wav"), lose(), -2)
    write(os.path.join(SFX_DIR, "windup_rifle.wav"), windup_rifle(), -2)
    write(os.path.join(SFX_DIR, "windup_mg.wav"), windup_mg(), -2)
    write(os.path.join(SFX_DIR, "windup_brawler.wav"), windup_brawler(), -2)
    write(os.path.join(SFX_DIR, "enemy_hit_1.wav"), enemy_hit(0), -3)
    write(os.path.join(SFX_DIR, "enemy_hit_2.wav"), enemy_hit(1), -3)
    write(os.path.join(SFX_DIR, "enemy_death.wav"), enemy_death(), -2)
    write(os.path.join(SFX_DIR, "jump_2.wav"), jump_2(), -3)
    write(os.path.join(SFX_DIR, "pickup_2.wav"), pickup_2(), -4)
    # music: no edge fades, the loop must join without a gap
    write(os.path.join(MUSIC_DIR, "music_level.wav"), music_level(), -3, edges=False)


if __name__ == "__main__":
    main()
