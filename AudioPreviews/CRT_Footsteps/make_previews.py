"""Reproducible listening previews; writes only beside this script, outside Unity Assets."""
from pathlib import Path
import hashlib
import json
import math
import sys
import wave

import numpy as np

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / '.tools'))
import soundfile as sf

RATE = 44100
SECONDS = 8
COUNT = RATE * SECONDS
STEP_LENGTH = int(.43 * RATE)
TIMES = [.45 + .55 * i for i in range(13)]
VARIANTS = [0, 0, 1, 1, 2, 2, 1, 0, 0, 2, 2, 1, 0]
GAINS = [1, .93, 1.02, .95, .98, .94, 1.03, .93, 1, .95, .98, .93, 1]


def rms(x):
    return float(np.sqrt(np.mean(np.square(x))))


def db(value):
    return float(20 * np.log10(max(float(value), 1e-15)))


def biquad(x, frequency, kind, q=1 / math.sqrt(2)):
    """RBJ two-pole filter, direct-form II; coefficients normalized by a0."""
    omega = 2 * math.pi * frequency / RATE
    cosine = math.cos(omega)
    alpha = math.sin(omega) / (2 * q)
    if kind == 'low':
        b = np.array([(1 - cosine) / 2, 1 - cosine, (1 - cosine) / 2])
    else:
        b = np.array([(1 + cosine) / 2, -(1 + cosine), (1 + cosine) / 2])
    b /= 1 + alpha
    a1, a2 = -2 * cosine / (1 + alpha), (1 - alpha) / (1 + alpha)
    y = np.empty_like(x, dtype=np.float64)
    z1 = z2 = 0.
    for i, sample in enumerate(x):
        out = b[0] * sample + z1
        z1 = b[1] * sample - a1 * out + z2
        z2 = b[2] * sample - a2 * out
        y[i] = out
    return y


def edge_fade(x, attack=.0005, release=.018):
    x = x.copy()
    a, r = int(attack * RATE), int(release * RATE)
    if a > 1:
        x[:a] *= np.sin(np.linspace(0, math.pi / 2, a)) ** 2
    if r > 1:
        x[-r:] *= np.cos(np.linspace(0, math.pi / 2, r)) ** 2
    return x


def prepare_step(x):
    x = biquad(x - x.mean(), 45, 'high')
    x = edge_fade(x)
    padded = np.zeros(STEP_LENGTH)
    padded[:min(len(x), STEP_LENGTH)] = x[:STEP_LENGTH]
    return padded * (.055 / max(rms(padded), 1e-9))


def recorded_steps():
    bank = {}
    for side in ('L', 'R'):
        for variant in range(3):
            path = ROOT / 'Source/Fantozzi-footsteps/flac' / f'Fantozzi-Stone{side}{variant+1}.flac'
            data, rate = sf.read(path, always_2d=True, dtype='float64')
            assert rate == RATE, (path, rate)
            bank[side, variant] = prepare_step(data.mean(axis=1))
    return bank


def synth_step(side, variant):
    """Hard sole: inharmonic heel click, low body, delayed toe, and a short sole scrape."""
    rng = np.random.default_rng(8021 + variant * 37 + (109 if side == 'R' else 0))
    t = np.arange(STEP_LENGTH) / RATE
    result = np.zeros(STEP_LENGTH)
    tune = 1 + rng.uniform(-.065, .065)
    for offset, strength in [(0., 1.), (.095 + rng.uniform(-.012, .015), .55)]:
        u = np.maximum(t - offset, 0)
        gate = (t >= offset).astype(float)
        attack = 1 - np.exp(-u / .0007)
        click = np.zeros_like(t)
        for hz, amplitude, decay in [(720, .27, .025), (1240, .28, .021),
                                      (2180, .20, .017), (3570, .14, .012),
                                      (5340, .08, .008)]:
            click += amplitude * np.sin(2 * np.pi * hz * tune * u) * np.exp(-u / decay)
        body = .48 * np.sin(2 * np.pi * (165 * tune * u + .55 * (1 - np.exp(-u / .013)))) * np.exp(-u / .037)
        impact_noise = biquad(biquad(rng.normal(0, 1, len(t)), 850, 'high'), 9000, 'low')
        impact_noise *= .45 * np.exp(-u / .017)
        result += strength * gate * attack * (click + body + impact_noise)
    scrape = biquad(biquad(rng.normal(0, 1, len(t)), 1200, 'high'), 6500, 'low')
    envelope = (1 - np.exp(-t / .008)) * np.exp(-t / .074)
    result += .13 * scrape * envelope
    return prepare_step(result)


def sequence(bank):
    x = np.zeros(COUNT)
    for i, (time, variant, gain) in enumerate(zip(TIMES, VARIANTS, GAINS)):
        sample = bank['L' if i % 2 == 0 else 'R', variant] * gain
        offset = round(time * RATE)
        x[offset:offset+len(sample)] += sample
    return x


def crt_process(x):
    wet = biquad(biquad(x, 250, 'high'), 3500, 'low')
    # Low wet saturation mix keeps transients recognizable, without digital clipping.
    wet = .8 * wet + .2 * .24 * np.tanh(wet / .24)
    wet = biquad(wet, 4200, 'low')
    rng = np.random.default_rng(91921)
    hiss = biquad(biquad(rng.normal(0, 1, COUNT), 500, 'high'), 4200, 'low')
    hiss *= .00125 / rms(hiss)
    t = np.arange(COUNT) / RATE
    hum = .00020 * np.sin(2 * np.pi * 100 * t) + .00010 * np.sin(2 * np.pi * 200 * t)
    return edge_fade(wet + hiss + hum, .025, .04)


def write_wav(path, data):
    assert np.isfinite(data).all() and abs(data).max() < 1
    samples = np.round(data * 32767).astype('<i2')
    with wave.open(str(path), 'wb') as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(samples.tobytes())


def main():
    recorded = sequence(recorded_steps())
    synthesized = sequence({(s, v): synth_step(s, v) for s in ('L', 'R') for v in range(3)})
    raw = [recorded, synthesized]
    # Establish the same level before either source reaches the shared speaker treatment.
    raw = [x * (.05 / rms(x)) for x in raw]
    samples = {'A_Recorded_Original.wav': raw[0], 'A_Recorded_CRT.wav': crt_process(raw[0]),
               'B_Synthesized_Original.wav': raw[1], 'B_Synthesized_CRT.wav': crt_process(raw[1])}
    unit = {name: x / rms(x) for name, x in samples.items()}
    # Equal output RMS, with common peak headroom. Never use per-file hard clipping.
    target = min(10 ** (-24 / 20), .80 / max(abs(x).max() for x in unit.values()))
    manifest = {'sample_rate': RATE, 'seconds': SECONDS, 'channels': 1, 'format': 'PCM signed 16-bit WAV',
                'step_times_seconds': TIMES, 'step_count': len(TIMES), 'interval_seconds': .55,
                'target_rms_dbfs': db(target), 'synthesis_seed': 8021,
                'crt': {'high_pass_hz': 250, 'low_pass_hz': 3500, 'final_low_pass_hz': 4200,
                        'saturation_wet_mix': .2, 'saturation_threshold': .24,
                        'hiss_rms_before_output_matching': .00125}, 'outputs': {}}
    for name, x in unit.items():
        data = x * target
        path = ROOT / name
        write_wav(path, data)
        manifest['outputs'][name] = {'rms_dbfs': db(rms(data)), 'peak_dbfs': db(abs(data).max()),
                                    'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
    manifest['sources'] = {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                           for p in sorted((ROOT / 'Source/Fantozzi-footsteps/flac').glob('*.flac'))}
    (ROOT / 'generation.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(manifest['outputs'], indent=2))


if __name__ == '__main__':
    main()
