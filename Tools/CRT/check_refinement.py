"""Compare removal of the global blend against same-API Strength=1 captures."""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    parser.add_argument("--baseline", required=True, type=Path)
    args = parser.parse_args()
    checks = []
    regions = {"3D": (340, 280, 890, 520), "Sprite": (1030, 285, 1560, 520),
               "Canvas": (1010, 650, 1630, 945)}
    for current in sorted(args.directory.glob("*.png")):
        if int(current.name[:2]) > 33:
            continue
        before = np.array(Image.open(args.baseline / current.name).convert("RGB"), dtype=float)
        after = np.array(Image.open(current).convert("RGB"), dtype=float)
        if before.shape != after.shape:
            raise ValueError("Capture dimensions changed: " + current.name)
        height, width = after.shape[:2]
        for target, bounds in regions.items():
            x0, y0, x1, y1 = [round(v * (width / 1920 if i % 2 == 0 else height / 1080))
                              for i, v in enumerate(bounds)]
            delta = np.abs(before[y0:y1, x0:x1] - after[y0:y1, x0:x1])
            error = float(delta.mean())
            checks.append(dict(name=current.stem + " " + target, passed=error < .4,
                               meanAbsoluteError=error, maxAbsoluteError=float(delta.max())))
    if len(checks) != 102:
        raise ValueError("Expected 34 matching captures x 3 display regions")
    report = dict(passed=all(c["passed"] for c in checks), baseline=str(args.baseline),
                  comparison="RGB 0-255, same API, display regions exclude changed controls", checks=checks)
    (args.directory / "refinement-checks.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(dict(passed=sum(c["passed"] for c in checks),
                         maxMeanError=max(c["meanAbsoluteError"] for c in checks),
                         failed=[c for c in checks if not c["passed"]]), indent=2))
    raise SystemExit(0 if report["passed"] else 1)


if __name__ == "__main__":
    main()
