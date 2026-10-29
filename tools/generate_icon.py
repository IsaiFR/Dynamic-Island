"""Generate the Windows icon from the pill-and-violet-halo design."""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "src" / "DynamicIsland.App" / "Assets"
SIZE = 1024
BOUNDS = (128, 356, 896, 668)
RADIUS = 156


def tint_mask(mask: Image.Image, color: tuple[int, int, int], opacity: float) -> Image.Image:
    layer = Image.new("RGBA", mask.size, (*color, 0))
    layer.putalpha(mask.point(lambda value: round(value * opacity)))
    return layer


def main() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))

    outline = Image.new("L", (SIZE, SIZE), 0)
    draw = ImageDraw.Draw(outline)
    draw.rounded_rectangle(BOUNDS, radius=RADIUS, outline=255, width=8)

    for blur, opacity in ((68, 0.15), (30, 0.25), (12, 0.35)):
        glow = outline.filter(ImageFilter.GaussianBlur(blur))
        image.alpha_composite(tint_mask(glow, (155, 99, 217), opacity))

    pill = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pill_draw = ImageDraw.Draw(pill)
    pill_draw.rounded_rectangle(
        BOUNDS,
        radius=RADIUS,
        fill=(23, 15, 32, 255),
        outline=(195, 149, 232, 190),
        width=5,
    )
    image.alpha_composite(pill)

    # A soft light on the upper rim keeps the silhouette legible at 16 px.
    upper_rim = Image.new("L", (SIZE, SIZE), 0)
    rim_draw = ImageDraw.Draw(upper_rim)
    rim_draw.rounded_rectangle(BOUNDS, radius=RADIUS, outline=150, width=4)
    upper_rim = upper_rim.filter(ImageFilter.GaussianBlur(5))
    image.alpha_composite(tint_mask(upper_rim, (216, 177, 246), 0.20))

    icon = image.convert("RGBa").resize((256, 256), Image.Resampling.LANCZOS).convert("RGBA")
    icon.save(ASSETS / "DynamicIsland.png", optimize=True)
    icon.save(
        ASSETS / "DynamicIsland.ico",
        format="ICO",
        sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
    )


if __name__ == "__main__":
    main()
