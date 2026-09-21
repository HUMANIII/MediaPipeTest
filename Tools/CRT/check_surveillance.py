"""Check actual surveillance renders and the generated PCM independently of runtime assertions."""
import argparse
import json
import wave
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    parser.add_argument("--audio", type=Path, required=True)
    args = parser.parse_args()
    checks = []

    def check(name, passed, evidence):
        checks.append(dict(name=name, passed=bool(passed), evidence=evidence))

    def read(name):
        return np.asarray(Image.open(args.directory / f"{name}.png").convert("RGB"), dtype=float)

    original = read("same-frame-original")
    raw_glitch = read("same-frame-original-with-transition")
    crt = read("same-frame-crt")
    glitch = read("same-frame-glitch")
    mask = Image.open(args.directory / "screen-mask.png").convert("L")
    inside = np.asarray(mask) > 240
    outside = np.asarray(mask.filter(ImageFilter.MaxFilter(7))) < 5
    check("display mask contains a visible screen", inside.sum() > 10000, int(inside.sum()))
    check("raw mode ignores maximum transition strength", np.max(np.abs(original - raw_glitch)) <= 1,
          float(np.abs(original - raw_glitch).mean()))
    for name, image in [("CRT", crt), ("glitch", glitch)]:
        delta = np.abs(original - image)
        check(f"{name} changes screen pixels", delta[inside].mean() > 3, float(delta[inside].mean()))
        check(f"{name} leaves pixels outside the screen unchanged", delta[outside].max() <= 1,
              dict(mean=float(delta[outside].mean()), maximum=float(delta[outside].max())))
    check("transition visibly differs from normal CRT", np.abs(glitch - crt)[inside].mean() > 15,
          float(np.abs(glitch - crt)[inside].mean()))
    check("normal CRT is monochrome", (crt.max(axis=2) - crt.min(axis=2))[inside].mean() < 2,
          float((crt.max(axis=2) - crt.min(axis=2))[inside].mean()))
    channels = [read(f"cam-{i}") for i in range(1, 5)]
    for i, camera in enumerate(channels, 1):
        check(f"CAM {i} is 1024x768", camera.shape[:2] == (768, 1024), list(camera.shape[:2]))
        check(f"CAM {i} renders scene detail", camera.std() > 10 and (camera.max(axis=2) > 30).mean() > .2,
              dict(std=float(camera.std()), lit_fraction=float((camera.max(axis=2) > 30).mean())))
        magenta = (camera[:, :, 0] > 220) & (camera[:, :, 1] < 35) & (camera[:, :, 2] > 220)
        check(f"CAM {i} has no error-shader magenta", magenta.mean() < .001, float(magenta.mean()))
    for i in range(4):
        for j in range(i + 1, 4):
            error = float(np.abs(channels[i] - channels[j]).mean())
            check(f"CAM {i+1} and CAM {j+1} have distinct views", error > 5, error)
    with wave.open(str(args.audio), "rb") as wav:
        duration = wav.getnframes() / wav.getframerate()
        samples = np.frombuffer(wav.readframes(wav.getnframes()), dtype="<i2").astype(float) / 32768
        check("mono PCM lasts 0.20 seconds", wav.getnchannels() == 1 and abs(duration - .2) < .001, duration)
    rms = lambda values: float(np.sqrt(np.mean(values ** 2)))
    check("audio fades at both ends", rms(samples[:150]) < rms(samples[1200:4000]) * .5
          and rms(samples[-150:]) < rms(samples[1200:4000]) * .2,
          dict(start=rms(samples[:150]), middle=rms(samples[1200:4000]), end=rms(samples[-150:])))
    report = dict(passed=all(c["passed"] for c in checks), checks=checks)
    (args.directory / "visual-audio-results.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(dict(passed=sum(c["passed"] for c in checks), total=len(checks),
                         failures=[c for c in checks if not c["passed"]]), indent=2))
    raise SystemExit(0 if report["passed"] else 1)


if __name__ == "__main__":
    main()
