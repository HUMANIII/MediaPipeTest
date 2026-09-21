"""Independently inspect diagnostic WAVs from Tools > CRT > Validate Audio Preview.

Requires Python and NumPy. The optional historical UI export check predates
the settings-asset-only UI. It checks the old export made at 49.4% mix:
    python Tools/CRT/check_audio_preview.py --check-ui-export
"""
import argparse
import hashlib
import json
from pathlib import Path
import wave

import numpy as np


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--check-ui-export', action='store_true')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    results = root / 'Logs/CRTAudioPreview'
    checks, metrics, signals = [], {}, {}

    def check(name, passed):
        checks.append({'name': name, 'passed': bool(passed)})

    def read(path):
        with wave.open(str(path), 'rb') as wav:
            assert wav.getsampwidth() == 2 and wav.getcomptype() == 'NONE'
            channels, rate, frames = wav.getnchannels(), wav.getframerate(), wav.getnframes()
            pcm = wav.readframes(frames)
            assert len(pcm) == frames * channels * 2
            data = np.frombuffer(pcm, dtype='<i2').reshape(-1, channels).astype(float) / 32768
        return data, rate

    for name in ['preview-original', 'preview-crt', 'preview-stereo']:
        data, rate = read(results / (name + '.wav'))
        signals[name] = data
        spectrum = abs(np.fft.rfft(data[:, 0])) ** 2
        frequencies = np.fft.rfftfreq(len(data), 1 / rate)
        outside = (frequencies < 150) | (frequencies > 6000)
        metrics[name] = {
            'duration': len(data) / rate, 'channels': data.shape[1],
            'peak': float(np.max(abs(data))), 'rms': float(np.sqrt(np.mean(data ** 2))),
            'outside_band_ratio': float(spectrum[outside].sum() / spectrum.sum()),
        }
        check(name + ' valid PCM and no clipping',
              rate == 44100 and np.isfinite(data).all() and 0 < np.max(abs(data)) < .999)

    check('stereo channel isolation', signals['preview-stereo'].shape[1] == 2
          and np.count_nonzero(signals['preview-stereo'][:, 1]) == 0)
    check('original and CRT duration retained', all(metrics[n]['duration'] == 8
          for n in ['preview-original', 'preview-crt']))
    check('CRT narrows actual exported spectrum', metrics['preview-crt']['outside_band_ratio']
          < metrics['preview-original']['outside_band_ratio'] * .2)
    source = root / 'AudioPreviews/CRT_Footsteps/A_Recorded_Original.wav'
    example = root / 'Assets/CRT/AudioPreview/Examples/A_Recorded_Original.wav'
    expected_hash = 'e362e90538676594ce86e5f16a52396773bf4d7454841f982805478184f70600'
    check('approved original and imported example are unchanged',
          hashlib.sha256(source.read_bytes()).hexdigest() == expected_hash
          and source.read_bytes() == example.read_bytes())

    if args.check_ui_export:
        exported, rate = read(root / 'AudioPreviews/CRT_Footsteps/Exports/A_Recorded_Original_CRT.wav')
        dry, wet = signals['preview-original'], signals['preview-crt']
        delta = wet - dry
        strength = float(np.sum((exported - dry) * delta) / np.sum(delta ** 2))
        residual = float(np.max(abs(exported - (dry + strength * delta))))
        metrics['ui_export'] = {'estimated_strength': strength, 'maximum_residual': residual}
        check('UI WAV export matches chosen 49.4 percent effect mix',
              rate == 44100 and exported.shape == dry.shape and abs(strength - .494) < .001
              and residual < 2.1 / 32768)

    report = {'passed': all(c['passed'] for c in checks), 'checks': checks, 'metrics': metrics}
    (results / 'independent-pcm-checks.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))
    raise SystemExit(0 if report['passed'] else 1)


if __name__ == '__main__':
    main()
