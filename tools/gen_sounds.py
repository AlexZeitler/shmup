"""Generates the sound effects (WAV) and the synthwave music loop (OGG) in Sounds/.

Usage: python3 tools/gen_sounds.py [output dir]
Needs ffmpeg with libvorbis for the music. All files are stereo, and every run
produces identical files.
"""
import math
import struct
import subprocess
import sys
import tempfile
import wave
from pathlib import Path

SR = 44100
TAU = 2 * math.pi
OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent / "Sounds"
OUT.mkdir(parents=True, exist_ok=True)


class Noise:
    """Deterministic white noise in [-1, 1]."""

    def __init__(self, seed):
        self.state = seed

    def __call__(self):
        self.state = (self.state * 1103515245 + 12345) & 0x7FFFFFFF
        return self.state / 0x3FFFFFFF - 1.0


def saw(phase):
    return 2.0 * (phase % 1.0) - 1.0


def tri(phase):
    p = phase % 1.0
    return 4.0 * p - 1.0 if p < 0.5 else 3.0 - 4.0 * p


def midi(note):
    return 440.0 * 2 ** ((note - 69) / 12)


# ---------------------------------------------------------------- DSP blocks

def lowpass(xs, cutoff, res=0.0):
    """Chamberlin state variable filter. cutoff is a number or a list per sample; res 0..0.9."""
    q = 1.0 - res
    low = band = 0.0
    out = [0.0] * len(xs)
    fixed = not isinstance(cutoff, list)
    f = 2 * math.sin(math.pi * min(cutoff, SR / 6) / SR) if fixed else 0.0
    for i, x in enumerate(xs):
        if not fixed:
            f = 2 * math.sin(math.pi * min(cutoff[i], SR / 6) / SR)
        low += f * band
        high = x - low - q * band
        band += f * high
        out[i] = low
    return out


def highpass(xs, cutoff):
    a = math.exp(-TAU * cutoff / SR)
    out = [0.0] * len(xs)
    prev_x = prev_y = 0.0
    for i, x in enumerate(xs):
        prev_y = a * (prev_y + x - prev_x)
        prev_x = x
        out[i] = prev_y
    return out


def _comb(xs, delay, feedback, damp):
    buf = [0.0] * delay
    out = [0.0] * len(xs)
    idx = 0
    store = 0.0
    for i, x in enumerate(xs):
        y = buf[idx]
        store = y * (1 - damp) + store * damp
        buf[idx] = x + store * feedback
        out[i] = y
        idx += 1
        if idx == delay:
            idx = 0
    return out


def _allpass(xs, delay, g=0.5):
    buf = [0.0] * delay
    out = [0.0] * len(xs)
    idx = 0
    for i, x in enumerate(xs):
        b = buf[idx]
        y = -x + b
        buf[idx] = x + b * g
        out[i] = y
        idx += 1
        if idx == delay:
            idx = 0
    return out


COMBS = [1116, 1188, 1277, 1356]
ALLPASSES = [556, 441]
SPREAD = 23


def reverb(xs, size=0.84, damp=0.3):
    """Small Freeverb: returns the wet signal for left and right."""
    chans = []
    for spread in (0, SPREAD):
        acc = [0.0] * len(xs)
        for d in COMBS:
            for i, y in enumerate(_comb(xs, d + spread, size, damp)):
                acc[i] += y
        for d in ALLPASSES:
            acc = _allpass(acc, d + spread)
        chans.append([y * 0.25 for y in acc])
    return chans[0], chans[1]


def echo(xs, delay_l, delay_r, feedback):
    """Ping-pong delay: returns the wet signal for left and right."""
    n = len(xs)
    left = [0.0] * n
    right = [0.0] * n
    for i in range(n):
        l_in = xs[i] + (right[i - delay_l] * feedback if i >= delay_l else 0.0)
        left[i] = l_in
        right[i] = left[i - delay_r] * feedback if i >= delay_r else 0.0
    return [l - x for l, x in zip(left, xs)], right


def pad_tail(xs, seconds):
    return xs + [0.0] * int(seconds * SR)


def space(dry, wet, size=0.84, damp=0.3):
    """Mono effect into stereo with reverb, so it sits in a room instead of the speaker."""
    rl, rr = reverb(dry, size, damp)
    return [d + l * wet for d, l in zip(dry, rl)], [d + r * wet for d, r in zip(dry, rr)]


def write_wav(name, stereo, peak, folder=OUT):
    left, right = stereo
    top = max(max(abs(s) for s in left), max(abs(s) for s in right)) or 1.0
    scale = peak / top
    frames = bytearray()
    for l, r in zip(left, right):
        frames += struct.pack("<hh", int(max(-1, min(1, l * scale)) * 32767),
                              int(max(-1, min(1, r * scale)) * 32767))
    with wave.open(str(folder / name), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(bytes(frames))


def trim_silence(stereo, threshold=0.0005):
    """Cut the reverb tail once it has died away."""
    left, right = stereo
    top = max(max(abs(s) for s in left), max(abs(s) for s in right))
    end = len(left)
    while end > 0 and abs(left[end - 1]) < top * threshold and abs(right[end - 1]) < top * threshold:
        end -= 1
    fade = min(int(0.05 * SR), end)
    for i in range(fade):
        g = i / fade
        left[end - 1 - i] *= g
        right[end - 1 - i] *= g
    return left[:end], right[:end]


def env_ad(n, attack, decay_rate):
    a = max(1, int(attack * SR))
    return [min(1.0, i / a) * math.exp(-max(0, i - a) / SR * decay_rate) for i in range(n)]


# ------------------------------------------------------------------ Effects

def shot():
    """Soft laser: sine and triangle sweeping down, gently filtered."""
    n = int(0.12 * SR)
    phase = 0.0
    xs = []
    for i in range(n):
        t = i / SR
        phase += (1700 * math.exp(-t * 18) + 420) / SR
        xs.append((math.sin(TAU * phase) * 0.6 + tri(phase) * 0.4) * (1 - t / 0.12) ** 2)
    xs = lowpass(xs, 3500)
    return trim_silence(space(pad_tail(xs, 0.5), 0.18, size=0.7))


def hit():
    """Metallic tick: inharmonic sines, filtered so it rings instead of clicks."""
    n = int(0.09 * SR)
    xs = []
    for i in range(n):
        t = i / SR
        xs.append((math.sin(TAU * 900 * t) * math.sin(TAU * 1370 * t) * 0.7
                   + math.sin(TAU * 2410 * t) * 0.3) * math.exp(-t * 45))
    xs = lowpass(xs, 4000)
    return trim_silence(space(pad_tail(xs, 0.4), 0.15, size=0.6))


def beam(level):
    """Detuned saws through a closing resonant filter, with a sub drop underneath."""
    dur = 0.35 + 0.15 * level
    n = int(dur * SR)
    noise = Noise(100 + level)
    ph1 = ph2 = sub = 0.0
    xs, cut = [], []
    top = 700 - 120 * level
    for i in range(n):
        t = i / SR
        f = top * math.exp(-t * 5) + 70
        ph1 += f / SR
        ph2 += f * 1.012 / SR
        sub += (f * 0.5) / SR
        env = min(1.0, t / 0.004) * (1 - t / dur) ** 1.4
        xs.append((saw(ph1) * 0.4 + saw(ph2) * 0.4 + math.sin(TAU * sub) * 0.6
                   + noise() * (0.08 + 0.06 * level)) * env)
        cut.append(5000 * math.exp(-t * 6) + 300)
    xs = lowpass(xs, cut, res=0.5)
    return trim_silence(space(pad_tail(xs, 1.2), 0.3 + 0.05 * level))


def charge_loop():
    """Seamless 0.5 s hum. All frequencies are even, so every one fits whole periods.

    It is filtered twice in a row and only the second pass is kept, so the filter state
    at the end matches the start and the loop has no click.
    """
    n = int(0.5 * SR)
    xs = []
    for i in range(n):
        t = i / SR
        tone = math.sin(TAU * 220 * t) * 0.5 + saw(t * 222) * 0.3 + saw(t * 218) * 0.3
        xs.append(tone * (0.75 + 0.25 * math.sin(TAU * 8 * t)))
    xs = lowpass(xs + xs, 1400, res=0.3)[n:]
    return xs, xs[:]


def explosion(dur, boom_hz, cutoff, wet, seed):
    """Low-passed noise with a falling cutoff and a sub boom, then a large room."""
    n = int(dur * SR)
    noise = Noise(seed)
    xs, cut = [], []
    sub = 0.0
    for i in range(n):
        t = i / SR
        sub += (boom_hz * math.exp(-t * 3) + 30) / SR
        decay = math.exp(-t * 6 / dur)
        crackle = noise() if noise() > 0.97 - 0.1 * (1 - t / dur) else 0.0
        xs.append(noise() * decay * 0.8 + crackle * decay * 0.5
                  + math.sin(TAU * sub) * math.exp(-t * 7 / dur) * 0.9)
        cut.append(cutoff * math.exp(-t * 3 / dur) + 120)
    xs = lowpass(xs, cut, res=0.2)
    return trim_silence(space(pad_tail(xs, 2.0), wet, size=0.88, damp=0.45))


def supersaw(f, n, voices=5, detune=0.012, seed=1):
    """Detuned saws spread across the stereo field."""
    noise = Noise(seed)
    phases = [(noise() + 1) / 2 for _ in range(voices)]
    ratios = [1 + detune * (v - (voices - 1) / 2) / ((voices - 1) / 2) for v in range(voices)]
    pans = [v / (voices - 1) for v in range(voices)]
    left, right = [0.0] * n, [0.0] * n
    for v in range(voices):
        step = f * ratios[v] / SR
        ph = phases[v]
        gl, gr = math.cos(pans[v] * math.pi / 2), math.sin(pans[v] * math.pi / 2)
        for i in range(n):
            s = saw(ph)
            left[i] += s * gl
            right[i] += s * gr
            ph += step
    return left, right


def game_over():
    """Falling lead line with echo, landing on a wide A minor chord. The leading pause
    lets the explosion of the ship ring out first."""
    lead = 0.6
    step = 0.22
    notes = [76, 72, 69, 64]
    total = int((lead + step * len(notes) + 3.5) * SR)
    mono = [0.0] * total
    for k, note in enumerate(notes):
        f = midi(note)
        o = int((lead + k * step) * SR)
        n = int(step * SR)
        for i in range(n):
            t = i / SR
            env = min(1.0, t / 0.01) * math.exp(-t * 6)
            mono[o + i] += (tri(f * t) * 0.6 + math.sin(TAU * f * t) * 0.4) * env
    el, er = echo(mono, int(0.33 * SR), int(0.33 * SR), 0.45)
    left = [m + e * 0.5 for m, e in zip(mono, el)]
    right = [m + e * 0.5 for m, e in zip(mono, er)]

    o = int((lead + step * len(notes)) * SR)
    n = total - o
    for note in (45, 57, 60, 64):
        pl, pr = supersaw(midi(note), n, seed=note)
        env = [min(1.0, i / (0.25 * SR)) * math.exp(-i / SR * 1.1) for i in range(n)]
        pl = lowpass([s * e for s, e in zip(pl, env)], 1600)
        pr = lowpass([s * e for s, e in zip(pr, env)], 1600)
        for i in range(n):
            left[o + i] += pl[i] * 0.12
            right[o + i] += pr[i] * 0.12

    rl, _ = reverb(left, 0.88)
    _, rr = reverb(right, 0.88)
    return trim_silence(([d + w * 0.6 for d, w in zip(left, rl)],
                         [d + w * 0.6 for d, w in zip(right, rr)]))


# -------------------------------------------------------------------- Music

BPM = 126
STEP = int(SR * 60 / BPM / 4)  # one sixteenth: 5250 samples
BAR = 16

A1, D2, E1, F1, G1, C2 = 33, 38, 28, 29, 31, 36
CHORDS = [  # bass root, pad notes
    (A1, [57, 60, 64]), (F1, [57, 60, 65]), (C2, [55, 60, 64]), (G1, [55, 59, 62]),
    (A1, [57, 60, 64]), (F1, [57, 60, 65]), (G1, [55, 59, 62]), (E1, [56, 59, 64]),
    (F1, [57, 60, 65]), (G1, [55, 59, 62]), (E1, [55, 59, 64]), (A1, [57, 60, 64]),
    (D2, [57, 62, 65]), (F1, [57, 60, 65]), (G1, [55, 59, 62]), (E1, [56, 59, 64]),
]

B4, C5, D5, E5, F5, G5, A5, B5 = 71, 72, 74, 76, 77, 79, 81, 83
A4, Gs4, Gs5 = 69, 68, 80
R = None
MELODY = [  # (note, length in sixteenths); the first four bars are the intro without lead
    [(R, 16)], [(R, 16)], [(R, 16)], [(R, 16)],
    [(E5, 6), (D5, 2), (C5, 4), (B4, 4)],
    [(C5, 8), (A4, 4), (C5, 4)],
    [(D5, 6), (E5, 2), (D5, 4), (B4, 4)],
    [(B4, 12), (R, 4)],
    [(A5, 6), (G5, 2), (F5, 4), (E5, 4)],
    [(D5, 8), (B4, 4), (D5, 4)],
    [(E5, 6), (G5, 2), (B5, 4), (G5, 4)],
    [(A5, 12), (R, 4)],
    [(F5, 6), (E5, 2), (D5, 4), (A4, 4)],
    [(C5, 6), (D5, 2), (F5, 4), (A5, 4)],
    [(G5, 6), (F5, 2), (D5, 4), (B4, 4)],
    [(E5, 4), (Gs4, 4), (B4, 8)],
]


def music():
    length = len(CHORDS) * BAR * STEP

    def buf():
        return [0.0] * length

    def add(target, offset, samples, gain=1.0):
        # Wraps around, so tails at the end of the loop continue at its start.
        for i, s in enumerate(samples):
            target[(offset + i) % length] += s * gain

    def looped(fn, xs):
        """Runs a stateful effect over two loops and keeps the second, so the loop seam
        sees the same history as every other point."""
        out = fn(xs + xs)
        if isinstance(out, tuple):
            return tuple(o[length:] for o in out)
        return out[length:]

    noise = Noise(1985)

    # --- Drums
    def kick():
        n = int(0.35 * SR)
        ph = 0.0
        out = []
        for i in range(n):
            t = i / SR
            ph += (120 * math.exp(-t * 25) + 45) / SR
            out.append(math.sin(TAU * ph) * math.exp(-t * 9) + (noise() * math.exp(-t * 300) * 0.3))
        return out

    def snare():
        n = int(0.25 * SR)
        body = [math.sin(TAU * 190 * i / SR) * math.exp(-i / SR * 25) * 0.5 for i in range(n)]
        hiss = lowpass([noise() * math.exp(-i / SR * 14) for i in range(n)], 6000)
        return [a + b for a, b in zip(body, hiss)]

    def hat():
        n = int(0.05 * SR)
        return highpass([noise() * math.exp(-i / SR * 70) for i in range(n)], 7000)

    kick_s, snare_s, hat_s = kick(), snare(), hat()
    drums = buf()
    snare_bus = buf()
    kicks = []
    for bar in range(len(CHORDS)):
        base = bar * BAR * STEP
        for s in (0, 8):
            add(drums, base + s * STEP, kick_s, 0.9)
            kicks.append(base + s * STEP)
        if bar % 4 == 3:  # small fill into the next phrase
            add(drums, base + 14 * STEP, kick_s, 0.6)
            kicks.append(base + 14 * STEP)
        for s in (4, 12):
            add(snare_bus, base + s * STEP, snare_s, 0.55)
        for s in range(2, BAR, 4):
            add(drums, base + s * STEP, hat_s, 0.25)
        for s in range(BAR):
            if s % 4 != 2:
                add(drums, base + s * STEP, hat_s, 0.08)

    # Sidechain: the pad and bass duck after every kick, which gives the pumping feel.
    duck = [1.0] * length
    for k in kicks:
        for i in range(int(0.25 * SR)):
            j = (k + i) % length
            duck[j] = min(duck[j], 1 - 0.6 * math.exp(-i / SR * 14))

    # --- Bass: sixteenths of two detuned saws, filter opens over each four-bar phrase.
    bass = buf()
    for bar, (root, _) in enumerate(CHORDS):
        base = bar * BAR * STEP
        for s in range(BAR):
            f = midi(root + (12 if s % 4 == 2 else 0))
            n = STEP
            p1 = p2 = 0.0
            out = []
            for i in range(n):
                p1 += f / SR
                p2 += f * 1.008 / SR
                env = min(1.0, i / 60) * math.exp(-i / SR * 9) * min(1.0, (n - i) / 200)
                out.append((saw(p1) + saw(p2)) * 0.5 * env)
            add(bass, base + s * STEP, out)
    phrase = 4 * BAR * STEP
    bass_cut = [200 + 900 * (0.5 - 0.5 * math.cos(TAU * (i % phrase) / phrase)) for i in range(length)]
    bass = looped(lambda xs: lowpass(xs, bass_cut + bass_cut, res=0.55), bass)
    sub = buf()
    for bar, (root, _) in enumerate(CHORDS):
        f = midi(root)
        base = bar * BAR * STEP
        n = BAR * STEP
        add(sub, base, [math.sin(TAU * f * i / SR) * min(1.0, i / 441, (n - i) / 441)
                        for i in range(n)], 0.35)

    # --- Pad: supersaw chords, slow attack, filtered.
    pad_l, pad_r = buf(), buf()
    bar_len = BAR * STEP
    for bar, (_, notes) in enumerate(CHORDS):
        base = bar * bar_len
        n = bar_len + int(0.4 * SR)
        env = [min(1.0, i / (0.12 * SR)) * min(1.0, (n - i) / (0.4 * SR)) for i in range(n)]
        for note in notes:
            l, r = supersaw(midi(note), n, seed=note + bar)
            add(pad_l, base, [s * e for s, e in zip(l, env)], 0.06)
            add(pad_r, base, [s * e for s, e in zip(r, env)], 0.06)
    pad_l = looped(lambda xs: lowpass(xs, 1800), pad_l)
    pad_r = looped(lambda xs: lowpass(xs, 1800), pad_r)

    # --- Lead: soft triangle and saw, vibrato, ping-pong echo of a dotted eighth.
    lead = buf()
    for bar, line in enumerate(MELODY):
        pos = 0
        for note, steps in line:
            if note is not None:
                f = midi(note)
                n = steps * STEP
                ph = 0.0
                out = []
                for i in range(n):
                    t = i / SR
                    vib = 1 + 0.006 * math.sin(TAU * 5 * t) * min(1.0, t / 0.3)
                    ph += f * vib / SR
                    env = min(1.0, t / 0.02) * (0.7 + 0.3 * math.exp(-t * 3)) * min(1.0, (n - i) / (0.05 * SR))
                    out.append((tri(ph) * 0.6 + saw(ph) * 0.4) * env)
                add(lead, bar * BAR * STEP + pos * STEP, out, 0.22)
            pos += steps
        assert pos == BAR, f"bar {bar + 1} has {pos} sixteenths"
    lead = looped(lambda xs: lowpass(xs, 2600), lead)
    echo_l, echo_r = looped(lambda xs: echo(xs, 3 * STEP, 3 * STEP, 0.5), lead)

    # --- Reverb send: pad, lead with its echo, snare.
    send = [p * 0.5 + q * 0.5 + l * 0.6 + el * 0.3 + er * 0.3 + s * 0.8
            for p, q, l, el, er, s in zip(pad_l, pad_r, lead, echo_l, echo_r, snare_bus)]
    rev_l, rev_r = looped(lambda xs: reverb(xs, 0.88, 0.35), send)

    left, right = [], []
    for i in range(length):
        d = duck[i]
        core = drums[i] + snare_bus[i] + (bass[i] * 0.45 + sub[i]) * d + lead[i]
        left.append(core + pad_l[i] * d + echo_l[i] * 0.45 + rev_l[i] * 0.55)
        right.append(core + pad_r[i] * d + echo_r[i] * 0.45 + rev_r[i] * 0.55)
    return left, right


write_wav("shot.wav", shot(), 0.32)
write_wav("hit.wav", hit(), 0.38)
for lvl in (1, 2, 3):
    write_wav(f"beam{lvl}.wav", beam(lvl), 0.5 + 0.1 * lvl)
write_wav("charge.wav", charge_loop(), 0.22)
write_wav("explosion_small.wav", explosion(0.5, 90, 2500, 0.3, 11), 0.6)
write_wav("explosion_large.wav", explosion(1.4, 70, 1800, 0.45, 12), 0.9)
write_wav("game_over.wav", game_over(), 0.5)
with tempfile.TemporaryDirectory() as tmp:
    write_wav("music.wav", music(), 0.5, Path(tmp))
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", str(Path(tmp) / "music.wav"),
                    "-c:a", "libvorbis", "-q:a", "5", str(OUT / "music.ogg")], check=True)
