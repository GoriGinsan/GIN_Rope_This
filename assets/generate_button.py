"""Draw the embedded 32 px native-style Rope This option button.

The artwork is entirely original: a grey bevel and two lines of text.  Fourfold
supersampling keeps the small Arial Bold label readable without an illustration.
Run with the Pillow package and the Windows Arial Bold font installed.
"""

from pathlib import Path
import os
from PIL import Image, ImageDraw, ImageFont


HERE = Path(__file__).resolve().parent
SCALE = 4
SIZE = 32
FONT = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts/arialbd.ttf"


def grey_gradient(top, bottom, size):
    image = Image.new("RGB", (size * SCALE, size * SCALE))
    draw = ImageDraw.Draw(image)
    for y in range(size * SCALE):
        t = y / (size * SCALE - 1)
        # Match the familiar light upper half and darker lower half of the
        # Studio option buttons, with a smooth transition through the centre.
        t = max(0.0, min(1.0, (t - 0.36) / 0.28))
        value = round(top + (bottom - top) * t)
        draw.line((0, y, size * SCALE, y), fill=(value,) * 3)
    return image


def rounded_layer(destination, inset, radius, top, bottom, size):
    mask = Image.new("L", destination.size)
    draw = ImageDraw.Draw(mask)
    draw.rounded_rectangle(
        (inset * SCALE, inset * SCALE,
         (size - inset) * SCALE - 1, (size - inset) * SCALE - 1),
        radius=radius * SCALE, fill=255,
    )
    destination.paste(grey_gradient(top, bottom, size), (0, 0), mask)


def render(size):
    ratio = size / 48
    button = Image.new("RGB", (size * SCALE, size * SCALE), (101, 101, 101))
    rounded_layer(button, 0, 3 * ratio, 121, 110, size)
    rounded_layer(button, 1 * ratio, 2 * ratio, 117, 97, size)
    rounded_layer(button, 2 * ratio, 1 * ratio, 103, 70, size)

    draw = ImageDraw.Draw(button)
    font = ImageFont.truetype(str(FONT), round(16 * SCALE * ratio))
    # Anchor each visible glyph box, rather than a font-specific baseline, so
    # both rows remain balanced within the native square button footprint.
    for label, centre_y in (("Rope", 15.25), ("This", 33.25)):
        box = draw.textbbox((0, 0), label, font=font)
        width, height = box[2] - box[0], box[3] - box[1]
        xy = ((size * SCALE - width) / 2 - box[0],
              centre_y * SCALE * ratio - height / 2 - box[1])
        draw.text(xy, label, font=font, fill=(255, 255, 255))
    return button


def main():
    button = render(SIZE)
    button.resize((192, 192), Image.Resampling.LANCZOS).save(HERE / "RopeThis-preview.png")
    button.resize((SIZE, SIZE), Image.Resampling.LANCZOS).save(HERE / "RopeThis.png")
    render(48).resize((48, 48), Image.Resampling.LANCZOS).save(HERE / "RopeThis-48.png")
    print("Created RopeThis.png (32 x 32), RopeThis-48.png and 192 px preview.")


if __name__ == "__main__":
    main()
