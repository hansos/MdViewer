#!/usr/bin/env python3
"""
Builds every shipped icon asset from the two SVG masters.

Run from the repository root:  python design/icon/build-icons.py

Sizes 32 and below are rendered from the simplified master; 48 and above from
the full one. See the comments in the SVGs for why.

Outputs:
  src/MdViewer.App/Assets/MdViewer.ico          Windows app + window icon
  packaging/linux/hicolor/<size>/apps/*.png     Linux hicolor icon theme
  packaging/linux/hicolor/scalable/apps/*.svg   Linux scalable
  design/icon/preview.png                       Contact sheet for review
"""

import io
import os
import sys

import cairosvg
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ICON_DIR = os.path.join(ROOT, "design", "icon")
FULL = os.path.join(ICON_DIR, "mdviewer.svg")
SMALL = os.path.join(ICON_DIR, "mdviewer-small.svg")

# The size at which the simplified drawing hands over to the full one.
SIMPLIFIED_UP_TO = 32

ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]
PNG_SIZES = [16, 22, 24, 32, 48, 64, 128, 256, 512]


def render(size: int) -> Image.Image:
    source = SMALL if size <= SIMPLIFIED_UP_TO else FULL
    png = cairosvg.svg2png(url=source, output_width=size, output_height=size)
    return Image.open(io.BytesIO(png)).convert("RGBA")


def build_ico(path: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)

    frames = [render(s) for s in ICO_SIZES]

    # Pillow's ICO writer downscales from a single image, which would throw away
    # the hand-tuned small variant. Saving the largest frame with an explicit
    # sizes list and appending the rest keeps each frame as drawn.
    frames[-1].save(
        path,
        format="ICO",
        sizes=[(s, s) for s in ICO_SIZES],
        append_images=frames[:-1],
    )
    print(f"  {os.path.relpath(path, ROOT)}  ({', '.join(str(s) for s in ICO_SIZES)})")


def build_linux(base: str) -> None:
    for size in PNG_SIZES:
        directory = os.path.join(base, f"{size}x{size}", "apps")
        os.makedirs(directory, exist_ok=True)
        target = os.path.join(directory, "mdviewer.png")
        render(size).save(target, format="PNG")

    scalable = os.path.join(base, "scalable", "apps")
    os.makedirs(scalable, exist_ok=True)
    with open(FULL, "rb") as src, open(os.path.join(scalable, "mdviewer.svg"), "wb") as dst:
        dst.write(src.read())

    print(f"  {os.path.relpath(base, ROOT)}  ({', '.join(str(s) for s in PNG_SIZES)}, scalable)")


def build_preview(path: str) -> None:
    """A contact sheet, so the small sizes get judged at their real scale."""
    sizes = [16, 24, 32, 48, 64, 128, 256]
    pad = 24
    width = sum(s + pad for s in sizes) + pad
    height = 256 + pad * 2 + 28

    sheet = Image.new("RGBA", (width, height), (247, 248, 250, 255))
    draw = ImageDraw.Draw(sheet)

    x = pad
    for size in sizes:
        image = render(size)
        y = pad + (256 - size)  # sit them on a common baseline
        sheet.paste(image, (x, y), image)
        draw.text((x, pad + 256 + 8), f"{size}px", fill=(90, 99, 110, 255))
        x += size + pad

    sheet.save(path, format="PNG")
    print(f"  {os.path.relpath(path, ROOT)}")


def main() -> int:
    print("Building icons…")
    build_ico(os.path.join(ROOT, "src", "MdViewer.App", "Assets", "MdViewer.ico"))
    build_linux(os.path.join(ROOT, "packaging", "linux", "hicolor"))
    build_preview(os.path.join(ICON_DIR, "preview.png"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
