"""VidShrink ikonu - deterministik uretim betigi.

Kaynak: src/VidShrink.App/Assets/VidShrink.svg ile ayni geometriyi tasir (bu betik
SVG'yi rasterlemez; makinede cairosvg/resvg/inkscape yoktu, bu yuzden ayni Bezier
noktalari burada dogrudan PIL + numpy ile supersample cizilir). Iki dosyayi birlikte
degistir: SVG elle, bu betik elle - sayilar es.

Renkler yalniz su kaynaktan: C:\\Users\\Administrator\\.claude\\teknesyum-private\\teknesyum-ui\\benim.tokens.json
  brand.renk-1        #6fb7ff  (birincil mavi, kollar)
  brand.renk-2-text   #fa8cff  (pembe, oynat ucgeni ust)
  brand.renk-3-text   #ac7fff  (mor, oynat ucgeni alt)
  brand.black/surface #000000  (ucgen cercevesi)
Turetilmis (bu dosyada uretilen, tokenlarda olmayan) tonlar - alfa/karisim kesimleri:
  renk-1-acik  = renk-1 %30 beyaza karisik   -> #9acdff (kol gradyani ust ucu)
  renk-1-koyu  = renk-1 %20 siyaha karisik   -> #5992cc (kol gradyani alt ucu)
  renk-2-parlak= renk-2-text %40 beyaza karisik -> #fcbaff (ucgen parlama yuzeyi)

Calistirma: python tools/ikon/uret.py
Cikti: src/VidShrink.App/Assets/VidShrink.ico (9 boy), src/VidShrink.App/Assets/VidShrink.png (1024)
Bagimlilik: Pillow, numpy (ikisi de bu makinede kuruluydu; yoksa `pip install pillow numpy`).
"""

import math
import numpy as np
from PIL import Image, ImageDraw

GRID = 256
ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
MASTER_PNG_SIZE = 1024

RENK_1 = (0x6F, 0xB7, 0xFF)
RENK_1_ACIK = (0x9A, 0xCD, 0xFF)
RENK_1_KOYU = (0x59, 0x92, 0xCC)
RENK_2_TEXT = (0xFA, 0x8C, 0xFF)
RENK_3_TEXT = (0xAC, 0x7F, 0xFF)
RENK_2_PARLAK = (0xFC, 0xBA, 0xFF)
SIYAH = (0x00, 0x00, 0x00)

LEFT_ARM = dict(p0=(100, 46), c1=(24, 72), c2=(24, 184), p3=(100, 210))
RIGHT_ARM = dict(p0=(156, 46), c1=(232, 72), c2=(232, 184), p3=(156, 210))
ARM_STROKE_W = 30

LEFT_ARROW = [(52, 110), (52, 146), (92, 128)]
RIGHT_ARROW = [(204, 110), (204, 146), (164, 128)]

TRIANGLE_OUTER = [(96, 74), (96, 182), (180, 128)]
TRIANGLE_OUTER_RADII = [14, 14, 9]
TRIANGLE_INNER = [(104, 86), (104, 170), (168, 128)]
TRIANGLE_INNER_RADII = [11, 11, 7]


def cubic_bezier(p0, c1, c2, p3, n):
    pts = []
    for i in range(n + 1):
        t = i / n
        mt = 1 - t
        x = (mt ** 3) * p0[0] + 3 * (mt ** 2) * t * c1[0] + 3 * mt * (t ** 2) * c2[0] + (t ** 3) * p3[0]
        y = (mt ** 3) * p0[1] + 3 * (mt ** 2) * t * c1[1] + 3 * mt * (t ** 2) * c2[1] + (t ** 3) * p3[1]
        pts.append((x, y))
    return pts


def rounded_polygon_points(vertices, radii, samples=24):
    n = len(vertices)
    out = []
    for i in range(n):
        prev_v = vertices[(i - 1) % n]
        v = vertices[i]
        next_v = vertices[(i + 1) % n]
        r = radii[i]

        din = np.array(v) - np.array(prev_v)
        din_len = np.hypot(*din)
        din_u = din / din_len
        a = np.array(v) - din_u * r

        dout = np.array(next_v) - np.array(v)
        dout_len = np.hypot(*dout)
        dout_u = dout / dout_len
        b = np.array(v) + dout_u * r

        out.append(tuple(a))
        for s in cubic_bezier(tuple(a), v, v, tuple(b), samples)[1:-1]:
            out.append(s)
        out.append(tuple(b))
    return out


def scale_pts(pts, factor):
    return [(x * factor, y * factor) for x, y in pts]


def make_gradient(size, color_a, color_b, angle_deg=45):
    h = w = size
    yy, xx = np.mgrid[0:h, 0:w]
    theta = math.radians(angle_deg)
    proj = xx * math.cos(theta) + yy * math.sin(theta)
    proj = (proj - proj.min()) / (proj.max() - proj.min())
    grad = np.zeros((h, w, 4), dtype=np.float32)
    for c in range(3):
        grad[:, :, c] = color_a[c] + (color_b[c] - color_a[c]) * proj
    grad[:, :, 3] = 255
    return Image.fromarray(grad.astype(np.uint8), "RGBA")


def mask_from_polygon(size, pts, blur_free=True):
    m = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(m)
    d.polygon(pts, fill=255)
    return m


def mask_from_strokeline(size, curve_pts, width):
    """Kalin, yumusak, kesisme hatasiz bir cizgi: egri uzerindeki her ornek
    noktasina bir disk cizip birlestirerek (dilation) uretilir - eksenlerin
    tersine donmesi (mirror) durumunda ofset ciftlerinin sarma yonu karisip
    hilal seklinde kesim yapmasi riskini tasimaz."""
    r = width / 2
    m = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(m)
    for x, y in curve_pts:
        d.ellipse([x - r, y - r, x + r, y + r], fill=255)
    return m


def paste_masked(base, gradient_img, mask):
    base.paste(gradient_img, (0, 0), mask)


def render(size, stroke_boost=1.0, supersample=8):
    ss = size * supersample
    scale = ss / GRID
    canvas = Image.new("RGBA", (ss, ss), (0, 0, 0, 0))

    for arm, mirror_grad in ((LEFT_ARM, (RENK_1_ACIK, RENK_1_KOYU)), (RIGHT_ARM, (RENK_1_KOYU, RENK_1_ACIK))):
        curve = cubic_bezier(arm["p0"], arm["c1"], arm["c2"], arm["p3"], 160)
        curve_s = scale_pts(curve, scale)
        stroke_w = ARM_STROKE_W * scale * stroke_boost
        mask = mask_from_strokeline(ss, curve_s, stroke_w)
        grad = make_gradient(ss, mirror_grad[0], mirror_grad[1], angle_deg=45)
        paste_masked(canvas, grad, mask)

    for arrow in (LEFT_ARROW, RIGHT_ARROW):
        pts = scale_pts(arrow, scale)
        mask = mask_from_polygon(ss, pts)
        flat = Image.new("RGBA", (ss, ss), RENK_1 + (255,))
        paste_masked(canvas, flat, mask)

    outer_pts = rounded_polygon_points(TRIANGLE_OUTER, TRIANGLE_OUTER_RADII)
    outer_pts_s = scale_pts(outer_pts, scale)
    outer_mask = mask_from_polygon(ss, outer_pts_s)
    black = Image.new("RGBA", (ss, ss), SIYAH + (255,))
    paste_masked(canvas, black, outer_mask)

    inner_pts = rounded_polygon_points(TRIANGLE_INNER, TRIANGLE_INNER_RADII)
    inner_pts_s = scale_pts(inner_pts, scale)
    inner_mask = mask_from_polygon(ss, inner_pts_s)
    grad = make_gradient(ss, RENK_2_TEXT, RENK_3_TEXT, angle_deg=45)
    paste_masked(canvas, grad, inner_mask)

    cx = sum(p[0] for p in TRIANGLE_INNER) / 3
    cy = sum(p[1] for p in TRIANGLE_INNER) / 3
    hi_pts = [
        (TRIANGLE_INNER[0][0], TRIANGLE_INNER[0][1]),
        (cx, cy),
        ((TRIANGLE_INNER[0][0] + TRIANGLE_INNER[1][0]) / 2 - 6, (TRIANGLE_INNER[0][1] + TRIANGLE_INNER[1][1]) / 2),
        (TRIANGLE_INNER[1][0], TRIANGLE_INNER[1][1]),
    ]
    hi_pts_s = scale_pts(hi_pts, scale)
    hi_mask_poly = mask_from_polygon(ss, hi_pts_s)
    hi_mask_np = np.minimum(np.array(hi_mask_poly), np.array(inner_mask))
    hi_mask = Image.fromarray((hi_mask_np.astype(np.float32) * (110 / 255)).astype(np.uint8))
    flat_hi = Image.new("RGBA", (ss, ss), RENK_2_PARLAK + (255,))
    canvas.paste(flat_hi, (0, 0), hi_mask)

    return canvas.resize((size, size), Image.LANCZOS)


def build_all(out_dir):
    import os
    os.makedirs(out_dir, exist_ok=True)

    icons = {}
    for size in ICO_SIZES:
        boost = 1.35 if size <= 20 else (1.18 if size <= 32 else 1.0)
        supersample = 16 if size <= 32 else 8
        icons[size] = render(size, stroke_boost=boost, supersample=supersample)

    icons[ICO_SIZES[-1]].save(
        os.path.join(out_dir, "VidShrink.ico"),
        sizes=[(s, s) for s in ICO_SIZES],
        append_images=[icons[s] for s in ICO_SIZES if s != ICO_SIZES[-1]],
    )

    master = render(MASTER_PNG_SIZE, stroke_boost=1.0, supersample=2)
    master.save(os.path.join(out_dir, "VidShrink.png"))

    return icons, master


if __name__ == "__main__":
    build_all("src/VidShrink.App/Assets")
    print("Yazildi: VidShrink.ico (9 boy), VidShrink.png (1024)")
