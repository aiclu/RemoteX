# Build-Icons.py — regenerate all RemoteX icon assets from vector geometry.
#
# Renders the RemoteX "X monogram" icon (two terminal chevrons crossing into an
# X, plus two underscore bars, per ADR docs/adr/0008-app-icon-redesign.md) at
# 4096x4096 and downsamples to every required asset:
#
#   Ui/LOGO.ico / Ui/LOGO_D.ico        multi-frame ICO (16..256), debug adds an
#                                      orange "D" badge
#   Ui/Resources/Image/Logo/logo*.png  plated PNGs used by title bar / About page
#   Installer/Images/*.png             the 35 MSIX assets (plated scale-*/targetsize-*,
#                                      brand-glyph unplated, white-glyph lightunplated)
#
# Requires: pip install pillow
# Usage:    python scripts/Build-Icons.py

import os
from PIL import Image, ImageDraw

SS = 4                      # supersample factor (render 4096, downscale)
CANVAS = 1024               # logical design canvas
BRAND = (0, 166, 196, 255)  # #00A6C4
WHITE = (255, 255, 255, 255)
BADGE = (255, 140, 0, 255)  # orange debug badge

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_UI = os.path.join(REPO, "Ui")
OUT_LOGO = os.path.join(OUT_UI, "Resources", "Image", "Logo")
OUT_MSIX = os.path.join(REPO, "Installer", "Images")
OUT_PREVIEW = os.path.join(REPO, "generated-images", "icon-concepts")

# ---------------------------------------------------------------- glyph ----
# Glyph geometry on the 1024 canvas. Two chevrons ("|" facing each other)
# crossing into an X, plus two terminal-cursor underscore bars.
CHEVRON_L = [(300, 320), (520, 512), (300, 704)]   # "|"
CHEVRON_R = [(724, 320), (504, 512), (724, 704)]   # "|"
BAR_TL = [(272, 258), (438, 258)]                  # "_" top-left
BAR_BR = [(586, 766), (752, 766)]                  # "_" bottom-right
STROKE_CHEVRON = 95
STROKE_BAR = 82

# Plated composition
PLATE_MARGIN = 64
PLATE_RADIUS = 215


def _xform(pts, scale, dx, dy):
    return [(x * scale + dx, y * scale + dy) for (x, y) in pts]


def _stroke(draw, pts, width, fill):
    """Polyline with round caps and round joins."""
    draw.line(pts, fill=fill, width=width, joint="curve")
    r = width / 2
    for (x, y) in pts:
        draw.ellipse([x - r, y - r, x + r, y + r], fill=fill)


def draw_glyph(draw, color, scale=1.0, dx=0.0, dy=0.0):
    for pts, w in ((CHEVRON_L, STROKE_CHEVRON), (CHEVRON_R, STROKE_CHEVRON),
                   (BAR_TL, STROKE_BAR), (BAR_BR, STROKE_BAR)):
        _stroke(draw, _xform(pts, scale, dx, dy), max(1, round(w * scale)), color)


def render_master(kind):
    """kind: 'plated' | 'unplated' | 'lightunplated' -> RGBA 4096 master."""
    size = CANVAS * SS
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if kind == "plated":
        m = PLATE_MARGIN * SS
        d.rounded_rectangle([m, m, size - m, size - m],
                            radius=PLATE_RADIUS * SS, fill=BRAND)
        draw_glyph(d, WHITE, SS)
    elif kind == "unplated":
        # glyph only, brand color, fills more of the canvas
        pad = 110
        s = SS * (CANVAS - 2 * pad) / CANVAS
        draw_glyph(d, BRAND, s, pad * SS, pad * SS)
    elif kind == "lightunplated":
        pad = 110
        s = SS * (CANVAS - 2 * pad) / CANVAS
        draw_glyph(d, WHITE, s, pad * SS, pad * SS)
    else:
        raise ValueError(kind)
    return img


def downscale(master, size):
    return master.resize((size, size), Image.LANCZOS)


# ----------------------------------------------------------------- badge ----
def add_debug_badge(img):
    """Orange circle + white 'D' at bottom-right (drawn from primitives,
    so no font dependency)."""
    size = img.width
    badge = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(badge)
    r = int(size * 0.155)
    cx, cy = int(size * 0.78), int(size * 0.78)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=BADGE)
    # "D": vertical bar + right arc
    w = max(1, int(r * 0.30))
    x0, y0 = cx - r * 0.34, cy - r * 0.52
    d.line([(x0, y0), (x0, y0 + r * 1.04)], fill=WHITE, width=w)
    d.ellipse([cx - r * 0.05, cy - r * 0.05, cx + r * 0.05, cy + r * 0.05],
              fill=WHITE)
    arc_box = [x0 - w / 2, y0, x0 + r * 0.72, y0 + r * 1.04]
    d.arc(arc_box, start=-90, end=90, fill=WHITE, width=w)
    return Image.alpha_composite(img, badge)


# -------------------------------------------------------------------- io ----
def save_ico(master, path, sizes=(16, 24, 32, 48, 64, 128, 256)):
    # Pillow's ICO writer resizes the master to every requested frame with
    # LANCZOS, so passing the 4096 supersampled master directly gives the
    # sharpest result at each size.
    master.save(path, format="ICO", sizes=[(s, s) for s in sizes])
    print("ico:", os.path.relpath(path, REPO), sizes)


def save_png(master, path, size):
    downscale(master, size).save(path)
    print("png:", os.path.relpath(path, REPO), (size, size))


def main():
    os.makedirs(OUT_PREVIEW, exist_ok=True)
    plated = render_master("plated")
    plated_dbg = add_debug_badge(render_master("plated"))
    unplated = render_master("unplated")
    lightunplated = render_master("lightunplated")

    # --- Ui app icons ---
    save_ico(plated, os.path.join(OUT_UI, "LOGO.ico"))
    save_ico(plated_dbg, os.path.join(OUT_UI, "LOGO_D.ico"))
    for s in (32, 64, 256):
        save_png(plated, os.path.join(OUT_LOGO, f"logo{s}.png"), s)

    # --- MSIX assets ---
    def m(name):
        return os.path.join(OUT_MSIX, name)

    # explicit pixel sizes per MSIX convention (round-half-up of base*scale)
    scale_sizes = {
        "PackageLogo": {"scale-100": 50, "scale-125": 63, "scale-150": 75,
                        "scale-200": 100, "scale-400": 200},
        "SmallTile": {"scale-100": 71, "scale-125": 89, "scale-150": 107,
                      "scale-200": 142, "scale-400": 284},
        "Square150x150Logo": {"scale-100": 150, "scale-125": 188, "scale-150": 225,
                              "scale-200": 300, "scale-400": 600},
        "Square44x44Logo": {"scale-100": 44, "scale-125": 55, "scale-150": 66,
                            "scale-200": 88, "scale-400": 176},
    }
    for family, sizes in scale_sizes.items():
        for scale, size in sizes.items():
            save_png(plated, m(f"{family}.{scale}.png"), size)

    for s in (16, 24, 32, 48, 256):
        save_png(plated, m(f"Square44x44Logo.targetsize-{s}.png"), s)
        save_png(unplated, m(f"Square44x44Logo.altform-unplated_targetsize-{s}.png"), s)
        save_png(lightunplated, m(f"Square44x44Logo.altform-lightunplated_targetsize-{s}.png"), s)

    # --- preview contact sheet ---
    sheet = Image.new("RGBA", (4 * 260 + 40, 300), (40, 40, 40, 255))
    for i, (img, label) in enumerate([(plated, "plated"), (plated_dbg, "debug"),
                                      (unplated, "unplated"), (lightunplated, "lightunplated")]):
        tile = downscale(img, 256)
        sheet.alpha_composite(tile, (20 + i * 260, 20))
    sheet.save(os.path.join(OUT_PREVIEW, "icon-contact-sheet.png"))
    print("preview:", os.path.join(OUT_PREVIEW, "icon-contact-sheet.png"))


if __name__ == "__main__":
    main()
