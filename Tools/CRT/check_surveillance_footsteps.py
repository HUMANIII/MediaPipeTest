"""Independently inspect imported A slices and PCM captured from the running player."""
import argparse
import hashlib
import json
from pathlib import Path
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('directory', type=Path)
args = parser.parse_args()
checks = []
def check(name, ok, evidence):
    checks.append(dict(name=name, passed=bool(ok), evidence=evidence))
def read(path):
    with wave.open(str(path), 'rb') as f:
        assert f.getsampwidth() == 2 and f.getnchannels() == 1
        return f.getframerate(), f.readframes(f.getnframes())
def signal(path):
    rate, data = read(path)
    return rate, np.frombuffer(data, dtype='<i2').astype(float) / 32768

source = ROOT / 'AudioPreviews/CRT_Footsteps/A_Recorded_Original.wav'
check('approved A original unchanged', hashlib.sha256(source.read_bytes()).hexdigest() ==
      'e362e90538676594ce86e5f16a52396773bf4d7454841f982805478184f70600', str(source))
_, original = read(source)
clips = ROOT / 'Assets/CRT/Surveillance/Audio/Footsteps'
manifest = json.loads((clips / 'extraction.json').read_text())
for entry in manifest:
    rate, data = read(clips / entry['file'])
    expected = original[entry['startFrame'] * 2:(entry['startFrame'] + entry['frames']) * 2]
    check(entry['file'] + ' exact approved PCM slice', data == expected and rate == 44100 and len(data) == 37926,
          hashlib.sha256(data).hexdigest())
rate, raw = signal(args.directory / 'footstep-approved-original.wav')
crt_rate, crt = signal(args.directory / 'footstep-runtime-crt.wav')
_, imported = signal(clips / 'A_Stone_L1.wav')
check('player reads the imported approved step', len(raw) == len(imported) and np.max(np.abs(raw - imported)) <= 1 / 32768,
      float(np.max(np.abs(raw - imported))))
check('runtime CRT retains rate and duration', rate == crt_rate == 44100 and raw.shape == crt.shape,
      dict(rate=rate, seconds=len(crt) / crt_rate))
check('runtime CRT output has no clipping and fades both edges', np.max(np.abs(crt)) < .99 and crt[0] == crt[-1] == 0,
      dict(peak=float(np.max(np.abs(crt))), first=float(crt[0]), last=float(crt[-1])))
frequencies = np.fft.rfftfreq(len(raw), 1 / rate)
outside = (frequencies < 250) | (frequencies > 3500)
def out_of_band(x):
    power = abs(np.fft.rfft(x)) ** 2
    return float(power[outside].sum() / power.sum())
check('runtime default CRT narrows the recorded spectrum', out_of_band(crt) < out_of_band(raw) * .3,
      dict(original=out_of_band(raw), crt=out_of_band(crt)))
check('runtime CRT is audibly nonzero PCM', np.sqrt(np.mean(crt * crt)) > .005,
      float(np.sqrt(np.mean(crt * crt))))
result = dict(passed=all(c['passed'] for c in checks), checks=checks)
(args.directory / 'footstep-pcm-results.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result, indent=2))
raise SystemExit(0 if result['passed'] else 1)
