"""Read the delivered WAVs independently of their generator and verify actual PCM."""
from pathlib import Path
import json
import wave
import numpy as np

ROOT = Path(__file__).resolve().parent
FILES = ['A_Recorded_Original.wav', 'A_Recorded_CRT.wav',
         'B_Synthesized_Original.wav', 'B_Synthesized_CRT.wav']


def rms(x):
    return float(np.sqrt(np.mean(x * x)))


def db(x):
    return float(20 * np.log10(max(float(x), 1e-15)))


def main():
    checks, metrics, samples = [], {}, {}

    def check(name, condition, evidence):
        checks.append(dict(name=name, passed=bool(condition), evidence=evidence))

    for name in FILES:
        with wave.open(str(ROOT / name), 'rb') as wav:
            rate, channels, width, count = wav.getframerate(), wav.getnchannels(), wav.getsampwidth(), wav.getnframes()
            pcm = np.frombuffer(wav.readframes(count), dtype='<i2')
            x = pcm.astype(np.float64) / 32768
        samples[name] = x
        check(name + ' PCM decodes completely', width == 2 and channels == 1 and len(pcm) == count, len(pcm))
        check(name + ' exactly eight seconds at 44100 Hz', rate == 44100 and count == 352800, count / rate)
        check(name + ' no full-scale clipping', abs(x).max() < .99, float(abs(x).max()))
        spectrum = np.fft.rfft(x)
        spectrum[-1] *= .5
        oversampled = np.fft.irfft(spectrum, n=4*len(x)) * 4
        check(name + ' reconstructed peak has headroom', abs(oversampled).max() < .99, db(abs(oversampled).max()))
        check(name + ' file endpoints fade to silence', pcm[0] == 0 and pcm[-1] == 0, [int(pcm[0]), int(pcm[-1])])
        background = rms(x[round(.08*rate):round(.35*rate)])
        event_rms = [rms(x[round((.45 + .55*i)*rate):round((.45 + .55*i + .04)*rate)]) for i in range(13)]
        check(name + ' all thirteen footsteps begin on the shared schedule', min(event_rms) > max(.002, background * 3), min(event_rms))
        power = abs(np.fft.rfft(x)) ** 2
        frequency = np.fft.rfftfreq(len(x), 1 / rate)
        outside = float(power[(frequency < 150) | (frequency > 6000)].sum() / power.sum())
        metrics[name] = dict(rms_dbfs=db(rms(x)), peak_dbfs=db(abs(x).max()),
                             reconstructed_peak_dbfs=db(abs(oversampled).max()),
                             background_rms_dbfs=db(background), outside_speaker_band_fraction=outside)
    levels = [m['rms_dbfs'] for m in metrics.values()]
    check('four RMS levels matched within 0.05 dB', max(levels)-min(levels) < .05, max(levels)-min(levels))
    for prefix in ['A_Recorded', 'B_Synthesized']:
        raw, crt = metrics[prefix + '_Original.wav'], metrics[prefix + '_CRT.wav']
        ratio = crt['outside_speaker_band_fraction'] / raw['outside_speaker_band_fraction']
        check(prefix + ' CRT reduces energy outside speaker band', ratio < .5, ratio)
        check(prefix + ' CRT has a low background hiss', -65 < crt['background_rms_dbfs'] < -40, crt['background_rms_dbfs'])
        check(prefix + ' original lead-in is silent', raw['background_rms_dbfs'] < -90, raw['background_rms_dbfs'])
    correlation = float(np.corrcoef(samples[FILES[0]], samples[FILES[2]])[0, 1])
    check('A and B are distinct source signals', abs(correlation) < .9, correlation)
    report = dict(passed=all(c['passed'] for c in checks), total=len(checks),
                  passed_count=sum(c['passed'] for c in checks), metrics=metrics, checks=checks,
                  limits='Decoded PCM, numerical signal checks and shared event schedule verified. Subjective listening approval remains with the user.')
    (ROOT / 'validation.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(passed=report['passed'], total=report['total'], passed_count=report['passed_count'],
                         failures=[c for c in checks if not c['passed']], metrics=metrics), indent=2))
    raise SystemExit(0 if report['passed'] else 1)


if __name__ == '__main__':
    main()
