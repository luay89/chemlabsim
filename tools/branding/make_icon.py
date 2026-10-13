#!/usr/bin/env python3
"""Draws the ChemLabSim app icon (1024x1024 PNG): a conical flask with glowing liquid.
Run from the repository root:  python3 tools/branding/make_icon.py
"""
import math
import os
import random
from PIL import Image, ImageDraw, ImageFilter

S = 4096                      # supersampled canvas, downsampled at the end
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets/_ProjectV3/Branding/AppIcon.png")


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def vertical_gradient(size, top, bottom):
    g = Image.new("RGBA", (1, 256))
    for y in range(256):
        g.putpixel((0, y), lerp(top, bottom, y / 255))
    return g.resize(size)


def flask_polygon(cx, top, bottom, neck_w, body_w, neck_h):
    """Erlenmeyer outline: straight neck, sloped shoulders, flat base with rounded corners."""
    pts = [(cx - neck_w / 2, top), (cx + neck_w / 2, top),
           (cx + neck_w / 2, top + neck_h)]
    r = body_w * 0.08
    pts += [(cx + body_w / 2 - r * 0.3, bottom - r), (cx + body_w / 2 - r, bottom),
            (cx - body_w / 2 + r, bottom), (cx - body_w / 2 + r * 0.3, bottom - r),
            (cx - neck_w / 2, top + neck_h)]
    return pts


def main():
    random.seed(7)
    img = vertical_gradient((S, S), (14, 34, 66, 255), (6, 12, 26, 255))

    # soft radial light behind the flask
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse((S * 0.18, S * 0.28, S * 0.82, S * 0.92), fill=(40, 140, 255, 110))
    img = Image.alpha_composite(img, glow.filter(ImageFilter.GaussianBlur(S * 0.08)))

    cx, top, bottom = S / 2, S * 0.17, S * 0.83
    neck_w, body_w, neck_h = S * 0.17, S * 0.62, S * 0.27
    outline = flask_polygon(cx, top, bottom, neck_w, body_w, neck_h)

    # liquid: clip a gradient to the lower part of the flask
    level = S * 0.55
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).polygon(outline, fill=255)
    cut = Image.new("L", (S, S), 0)
    ImageDraw.Draw(cut).rectangle((0, level, S, S), fill=255)
    liquid_mask = Image.composite(mask, Image.new("L", (S, S), 0), cut).filter(ImageFilter.GaussianBlur(4))
    liquid = vertical_gradient((S, S), (40, 230, 200, 255), (20, 110, 255, 255))
    img.paste(liquid, (0, 0), liquid_mask)

    # bubbles rising in the liquid and out of the neck
    bub = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    bd = ImageDraw.Draw(bub)
    for _ in range(26):
        x = cx + random.uniform(-0.26, 0.26) * S * (0.5 + random.random() * 0.5)
        y = random.uniform(level + S * 0.03, bottom - S * 0.06)
        r = random.uniform(0.010, 0.026) * S
        if mask.getpixel((int(x), int(y))) > 0:
            bd.ellipse((x - r, y - r, x + r, y + r), outline=(255, 255, 255, 170), width=int(S * 0.006))
    for i, (dx, dy, r) in enumerate([(0.00, 0.12, 0.030), (0.035, 0.07, 0.022), (-0.03, 0.035, 0.018)]):
        x, y, rr = cx + dx * S, top - dy * S + S * 0.1, r * S
        bd.ellipse((x - rr, y - rr, x + rr, y + rr), outline=(160, 240, 255, 200), width=int(S * 0.007))
    img = Image.alpha_composite(img, bub)

    # glass: bright outline, rim and a highlight streak
    glass = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    gl = ImageDraw.Draw(glass)
    gl.line(outline + [outline[0]], fill=(235, 245, 255, 255), width=int(S * 0.022), joint="curve")
    gl.rounded_rectangle((cx - neck_w * 0.72, top - S * 0.02, cx + neck_w * 0.72, top + S * 0.02),
                         radius=S * 0.02, fill=(235, 245, 255, 255))
    gl.line([(cx - body_w * 0.30, bottom - S * 0.08), (cx - neck_w * 0.25, top + neck_h + S * 0.04)],
            fill=(255, 255, 255, 120), width=int(S * 0.02))
    img = Image.alpha_composite(img, glass)

    # rounded-square app shape
    shape = Image.new("L", (S, S), 0)
    ImageDraw.Draw(shape).rounded_rectangle((0, 0, S - 1, S - 1), radius=int(S * 0.22), fill=255)
    out = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    out.paste(img, (0, 0), shape)
    out = out.resize((1024, 1024), Image.LANCZOS)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    out.save(OUT)
    print("wrote", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
