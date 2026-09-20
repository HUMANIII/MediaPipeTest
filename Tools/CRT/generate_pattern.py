"""Generate original CRT fixtures. Requires Pillow, NumPy, and an FFmpeg executable.

python generate_pattern.py --ffmpeg /path/to/ffmpeg --output Assets/CRT/Media
No downloaded video or runtime Python/FFmpeg dependency is used by Unity.
"""
import argparse
import math
import subprocess
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--ffmpeg", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    w, h, fps, seconds = 1280, 720, 30, 4
    font_path = Path("C:/Windows/Fonts/consola.ttf")
    font = ImageFont.truetype(str(font_path), 28) if font_path.exists() else ImageFont.load_default()
    small = ImageFont.truetype(str(font_path), 20) if font_path.exists() else font
    command = [args.ffmpeg, "-y", "-hide_banner", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24",
               "-s", f"{w}x{h}", "-r", str(fps), "-i", "-", "-an", "-c:v", "libx264", "-crf", "16",
               "-pix_fmt", "yuv420p", "-profile:v", "baseline", "-bf", "0", "-color_primaries", "bt709",
               "-color_trc", "bt709", "-colorspace", "bt709", "-x264-params", "colorprim=bt709:transfer=bt709:colormatrix=bt709",
               "-movflags", "+faststart", str(args.output / "CRTTestPattern.mp4")]
    process = subprocess.Popen(command, stdin=subprocess.PIPE)
    try:
        for frame in range(fps * seconds):
            im = Image.new("RGB", (w, h), (14, 19, 28))
            draw = ImageDraw.Draw(im)
            draw.text((28, 18), "CRT / RGB + SCANLINES", fill="white", font=font)
            draw.text((920, 18), f"TOP / {frame:03d}", fill=(110, 230, 255), font=font)
            bars = [(245,245,245), (245,225,40), (30,220,220), (30,220,80), (230,40,220), (225,45,45), (35,80,230)]
            for i, color in enumerate(bars):
                x0, x1 = 24 + i * 176, 24 + (i + 1) * 176
                draw.rectangle((x0, 78, x1 - 3, 254), fill=color)
            ramp = np.tile(np.linspace(0, 255, 1232, dtype=np.uint8)[None, :, None], (70, 1, 3))
            im.paste(Image.fromarray(ramp), (24, 275))
            for x in range(24, 1257, 32): draw.line((x, 370, x, 650), fill=(65, 82, 101), width=1)
            for y in range(370, 651, 32): draw.line((24, y, 1256, y), fill=(65, 82, 101), width=1)
            phase = frame / (fps * seconds) * 2 * math.pi
            x = 640 + int(475 * math.sin(phase))
            y = 510 + int(75 * math.cos(phase))
            draw.ellipse((x-44, y-44, x+44, y+44), fill=(255,170,35), outline="white", width=3)
            draw.rectangle((600, 438, 680, 582), outline=(70,240,205), width=3)
            draw.text((24, 674), "BOTTOM / 1280 x 720 / 30 fps / 4 second loop", fill=(192, 205, 220), font=small)
            draw.rectangle((0,0,w-1,h-1), outline=(240,240,240), width=2)
            if frame == 0: im.save(args.output / "CRTTestPattern.png")
            process.stdin.write(im.tobytes())
    finally:
        process.stdin.close()
    if process.wait() != 0: raise RuntimeError("FFmpeg failed")
    sprite = Image.new("RGBA", (512, 288), (255,255,255,0))
    ImageDraw.Draw(sprite).rounded_rectangle((4,4,507,283), radius=35, fill="white")
    sprite.save(args.output / "ScreenShape.png")
    mask = Image.new("RGB", (512,288), "black")
    ImageDraw.Draw(mask).rectangle((256,0,511,287), fill="white")
    mask.save(args.output / "RightHalfMask.png")
    print("CRT fixtures generated:", args.output)


if __name__ == "__main__":
    main()
