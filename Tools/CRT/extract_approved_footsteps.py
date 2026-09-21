"""Extract six unprocessed single steps from the approved A preview, without altering it."""
import hashlib
import json
from pathlib import Path
import wave

ROOT = Path(__file__).resolve().parents[2]
source = ROOT / 'AudioPreviews/CRT_Footsteps/A_Recorded_Original.wav'
assert hashlib.sha256(source.read_bytes()).hexdigest() == 'e362e90538676594ce86e5f16a52396773bf4d7454841f982805478184f70600'
destination = ROOT / 'Assets/CRT/Surveillance/Audio/Footsteps'
destination.mkdir(parents=True, exist_ok=True)
with wave.open(str(source), 'rb') as wav:
    assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate()) == (1, 2, 44100)
    pcm = wav.readframes(wav.getnframes())
manifest = []
for index, name in enumerate(('L1', 'R1', 'L2', 'R2', 'L3', 'R3')):
    start, count = int((.45 + .55 * index) * 44100), int(.43 * 44100)
    data = pcm[start * 2:(start + count) * 2]
    path = destination / f'A_Stone_{name}.wav'
    with wave.open(str(path), 'wb') as wav:
        wav.setparams((1, 2, 44100, count, 'NONE', 'not compressed'))
        wav.writeframes(data)
    manifest.append(dict(file=path.name, startFrame=start, frames=count, pcmSHA256=hashlib.sha256(data).hexdigest()))
(destination / 'extraction.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print('PASS: 6 exact PCM slices from approved A; original unchanged.')
