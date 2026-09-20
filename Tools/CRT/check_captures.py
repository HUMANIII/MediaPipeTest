"""Check rendered player captures, independently of Unity's C# status assertions."""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    args = parser.parse_args()
    root = args.directory
    checks = []

    def check(name, passed, evidence):
        checks.append(dict(name=name, passed=bool(passed), evidence=evidence))

    def read(name):
        return np.asarray(Image.open(root / (name + ".png")).convert("RGB"), dtype=np.int16)

    images = {path.stem: read(path.stem) for path in sorted(root.glob("*.png"))}
    for name, pixels in images.items():
        visible = float(np.mean(np.max(pixels, axis=2) > 40))
        check(name + " contains rendered content", visible > .025, visible)
    original = images["00-original"]

    def diff(a, b, roi):
        x0, y0, x1, y1 = roi
        return float(np.abs(a[y0:y1, x0:x1] - b[y0:y1, x0:x1]).mean())

    regions = {"3D": (335,315,380,480), "Sprite": (1025,315,1080,480), "Canvas": (1020,700,1080,930)}
    for target, roi in regions.items():
        for name in ["05-partial", "06-mask"]:
            error = diff(original, images[name], roi)
            check(f"{target}: {name} preserves excluded pixels", error < .1, {"mean_error_8bit": error, "roi": roi})
    ui = (1010,650,1630,945)
    error = diff(original, images["02-fullscreen-ui-excluded"], ui)
    check("Fullscreen excludes display UI", error < .1, error)
    error = diff(original, images["03-fullscreen-ui-included"], ui)
    check("Fullscreen includes display UI", error > 2, error)
    for target, roi in {"3D": (340,280,890,520), "Sprite": (1030,285,1560,520), "Canvas": ui}.items():
        error = diff(original, images["01-compare"], roi)
        check(target + " CRT changes the image", error > 2, error)
        pixel_error = diff(images["01-compare"], images["04-pixelation"], roi)
        check(target + " pixelation changes the image", pixel_error > .5, pixel_error)
    controls = (350,90,460,119)
    error = diff(images["02-fullscreen-ui-excluded"], images["03-fullscreen-ui-included"], controls)
    check("Controls remain outside CRT", error < .1, error)
    corner = original[235,1010]
    background = original[600,50]
    check("Sprite transparent corner preserves background", np.max(np.abs(corner-background)) <= 1, {"corner": corner.tolist(), "background": background.tolist()})
    check("4:3 output size", images["07-aspect-4x3"].shape[:2] == (768,1024), list(images["07-aspect-4x3"].shape[:2]))
    masked = images["09-ui-mask-alpha"]
    check("UI stencil clips outside its bounds", np.max(np.abs(masked[720,1025] - background)) <= 1, masked[720,1025].tolist())
    alpha_original = images["04-pixelation"][700:745,1090:1140].mean()
    alpha_masked = masked[700:745,1090:1140].mean()
    check("CanvasGroup alpha affects CRT", .3 < alpha_masked / alpha_original < .85, float(alpha_masked / alpha_original))
    def linear(rgb):
        rgb = rgb.astype(np.float64) / 255
        return np.where(rgb <= .04045, rgb / 12.92, ((rgb + .055) / 1.055) ** 2.4)

    def srgb(rgb):
        return np.where(rgb <= .0031308, rgb * 12.92, 1.055 * rgb ** (1 / 2.4) - .055) * 255

    unmasked_pixels = images["04-pixelation"][700:745,1090:1140]
    expected_half_alpha = srgb(linear(unmasked_pixels) * .5 + linear(background) * .5)
    alpha_error = float(np.abs(masked[700:745,1090:1140] - expected_half_alpha).mean())
    check("CanvasGroup alpha is applied once", alpha_error < 3, {"mean_error_8bit": alpha_error})
    report = dict(passed=all(c["passed"] for c in checks), checks=checks)
    (root / "capture-checks.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"passed": sum(c["passed"] for c in checks), "failed": [c for c in checks if not c["passed"]]}, indent=2))
    raise SystemExit(0 if report["passed"] else 1)


if __name__ == "__main__":
    main()
