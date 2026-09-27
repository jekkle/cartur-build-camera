"""Draws the 256x256 Thunderstore icon.

Thunderstore rejects anything that is not exactly 256x256 PNG. There is no art asset to
crop from - the upstream mod's icon is Azumatt's own work and the GPL covers the code, not
his artwork - so this is drawn: a viewfinder's four corner brackets for the detached camera,
with a hammer inside them for what the camera is for.

Kept to two shapes and three colours because the thing is read at about 32px in a mod list,
where anything finer turns to mush.

    python tools/make_icon.py
"""
import os

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "package", "icon.png")

SIZE = 256
SCALE = 4  # drawn large and downsampled, so the diagonals are not stair-stepped

BACKGROUND = (38, 34, 30, 255)
BRACKET = (214, 173, 96, 255)      # the warm brass Valheim uses on build UI
HANDLE = (124, 88, 54, 255)
HEAD = (158, 162, 168, 255)
HEAD_EDGE = (44, 46, 50, 255)


def brackets(draw, inset, arm, weight):
    """Four corner brackets, like a camera viewfinder's framing marks."""
    lo, hi = inset, SIZE * SCALE - inset
    for x, y, dx, dy in ((lo, lo, 1, 1), (hi, lo, -1, 1), (lo, hi, 1, -1), (hi, hi, -1, -1)):
        draw.line([(x, y), (x + dx * arm, y)], fill=BRACKET, width=weight)
        draw.line([(x, y), (x, y + dy * arm)], fill=BRACKET, width=weight)


def hammer(draw, cx, cy):
    """A builder's hammer, canted so it reads as a tool and not a cross."""
    # Handle: a rotated rectangle, drawn as a polygon so it can lean.
    half_w, half_l = 11 * SCALE, 68 * SCALE
    lean = 0.34  # x drift per unit of length
    pts = []
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        pts.append((cx + sx * half_w + sy * half_l * lean, cy + sy * half_l))
    draw.polygon(pts, fill=HANDLE)

    # Head: a blocky claw hammer across the top of the handle.
    top_x = cx - half_l * lean
    top_y = cy - half_l
    hw, hh = 46 * SCALE, 21 * SCALE
    draw.polygon(
        [
            (top_x - hw, top_y - hh),
            (top_x + hw, top_y - hh),
            (top_x + hw, top_y + hh),
            (top_x + hw * 0.30, top_y + hh),
            (top_x - hw * 0.34, top_y + hh * 1.5),
            (top_x - hw, top_y + hh * 0.2),
        ],
        fill=HEAD,
        outline=HEAD_EDGE,
        width=3 * SCALE,
    )


image = Image.new("RGBA", (SIZE * SCALE, SIZE * SCALE), BACKGROUND)
draw = ImageDraw.Draw(image)
brackets(draw, inset=26 * SCALE, arm=52 * SCALE, weight=11 * SCALE)
hammer(draw, cx=SIZE * SCALE // 2 + 6 * SCALE, cy=SIZE * SCALE // 2 + 12 * SCALE)

image.resize((SIZE, SIZE), Image.LANCZOS).save(OUT)
print("wrote", OUT)
