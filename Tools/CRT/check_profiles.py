"""Compare actual CRT profile renders; run after the player's -crtValidate run."""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    parser.add_argument("--baseline", type=Path)
    parser.add_argument("--editor", type=Path)
    args = parser.parse_args()
    checks = []

    def check(name, condition, evidence):
        checks.append(dict(name=name, passed=bool(condition), evidence=evidence))

    def load(path):
        return np.asarray(Image.open(path).convert("RGB"), dtype=float)

    images = {p.stem: load(p) for p in args.directory.glob("*.png")}
    regions = {"3D": (340,280,890,520), "Sprite": (1030,285,1560,520), "Canvas": (1010,650,1630,945)}

    def roi(image, bounds):
        x0,y0,x1,y1 = bounds
        return image[y0:y1,x0:x1]

    def difference(a,b,bounds):
        return float(np.abs(roi(images[a],bounds)-roi(images[b],bounds)).mean())

    def chroma(image):
        return float((image.max(axis=2)-image.min(axis=2)).mean())

    for target, bounds in regions.items():
        base = chroma(roi(images["10-profile-color"], bounds))
        half = chroma(roi(images["11-profile-half-mono"], bounds))
        mono = chroma(roi(images["12-profile-mono"], bounds))
        check(target+" full mono removes colored stripes", mono < 1, mono)
        check(target+" half mono retains intermediate color", 0 < half < base*.9, dict(base=base,half=half))
        for key, green in [("13-profile-green",True),("14-profile-amber",False)]:
            r,g,b = roi(images[key],bounds).mean(axis=(0,1))
            passed = g > r*1.3 and g > b*1.2 if green else r > g*1.1 and g > b*1.5
            check(target+" "+key, passed, [r,g,b])
        for a,b in [("15-vignette-off","16-vignette-strong"),("16-vignette-strong","17-vignette-radius"),
                    ("17-vignette-radius","18-vignette-hard"),("18-vignette-hard","19-vignette-soft")]:
            error = difference(a,b,bounds)
            check(target+" "+a+" / "+b, error > 1, error)
        for a,b in [("00-original","30-profile-disabled"),("10-profile-color","31-profile-fallback"),
                    ("01-compare","10-profile-color"),("00-original","34-fullscreen-disabled"),
                    ("29-fullscreen-vignette-soft","35-fullscreen-reenabled")]:
            error = difference(a,b,bounds)
            check(target+" "+a+" matches "+b,error < .15,error)
        if args.baseline:
            error = float(np.abs(roi(load(args.baseline/"01-compare.png"),bounds)-roi(images["01-compare"],bounds)).mean())
            check(target+" legacy appearance preserved",error < .4,error)
        for key in ["22-fullscreen-mono","23-fullscreen-half-mono"]:
            c = chroma(roi(images[key],bounds))
            check(target+" "+key,c < 1 if "half" not in key else c > 1,c)
        for a,b in [("26-fullscreen-vignette-off","27-fullscreen-vignette-strong"),
                    ("27-fullscreen-vignette-strong","28-fullscreen-vignette-radius"),
                    ("28-fullscreen-vignette-radius","29-fullscreen-vignette-soft")]:
            error = difference(a,b,bounds)
            check(target+" "+a+" / "+b,error > 1,error)
        for key, green in [("24-fullscreen-green",True),("25-fullscreen-amber",False)]:
            r,g,b = roi(images[key],bounds).mean(axis=(0,1))
            passed = g > r*1.3 and g > b*1.2 if green else r > g*1.1 and g > b*1.5
            check(target+" "+key,passed,[r,g,b])

    excluded = {"3D":(335,315,380,480),"Sprite":(1025,315,1080,480),"Canvas":(1020,700,1080,930)}
    for target,bounds in excluded.items():
        for key in ["20-profile-partial","21-profile-mask"]:
            error = difference("00-original",key,bounds)
            check(target+" "+key+" preserves excluded pixels",error < .1,error)

    independent = images["33-profile-independent"]
    check("Independent profile: mono 3D",chroma(roi(independent,regions["3D"])) < 1,chroma(roi(independent,regions["3D"])))
    r,g,b = roi(independent,regions["Sprite"]).mean(axis=(0,1))
    check("Independent profile: green Sprite",g > r*1.3 and g > b*1.2,[r,g,b])
    error = difference("33-profile-independent","10-profile-color",regions["Canvas"])
    check("Independent profile: local Canvas",error < .15,error)

    def linear(rgb):
        rgb = rgb/255
        return np.where(rgb <= .04045,rgb/12.92,((rgb+.055)/1.055)**2.4)

    def srgb(rgb):
        return np.where(rgb <= .0031308,rgb*12.92,1.055*rgb**(1/2.4)-.055)*255

    background = images["00-original"][600,50]
    masked = images["32-profile-ui-mask"]
    reference = images["12-profile-mono"][700:745,1090:1140]
    expected = srgb(linear(reference)*.5+linear(background)*.5)
    error = float(np.abs(masked[700:745,1090:1140]-expected).mean())
    check("Mono profile through stencil: alpha applied once",error < 3,error)
    error = float(np.abs(masked[720,1025]-background).max())
    check("Mono profile through stencil: outside clipped",error <= 1,error)

    if args.editor:
        preview = {p.stem:load(p) for p in args.editor.glob("*.png")}
        mono = chroma(preview["02-mono"])
        check("Edit mode preview: monochrome",mono < 1,mono)
        for a,b in [("00-original","01-classic"),("01-classic","02-mono"),("03-green","04-vignette")]:
            error = float(np.abs(preview[a]-preview[b]).mean())
            check("Edit mode preview: "+a+" / "+b,error > 1,error)
        result = json.loads((args.editor/"result.json").read_text())
        check("Edit mode preview: cleanup and scene isolation",result["passed"],result)
    runtime = json.loads((args.directory/"report.json").read_text())
    check("Shared profiles unchanged by runtime controls",runtime["profilesUnchanged"],runtime["profilesUnchanged"])
    check("Profile bindings, live update, fallback and reload",runtime["profileBindingsWorking"],runtime["profileBindingsWorking"])
    check("Runtime errors",not runtime["errors"],runtime["errors"])
    report = dict(passed=all(c["passed"] for c in checks),checks=checks)
    (args.directory/"profile-checks.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
    print(json.dumps(dict(passed=sum(c["passed"] for c in checks),failed=[c for c in checks if not c["passed"]]),indent=2))
    raise SystemExit(0 if report["passed"] else 1)


if __name__ == "__main__":
    main()
