"""VidShrink ikonu - tek kaynak, deterministik uretim betigi.

**Kaynagin tek yonu**: butun geometri (kanca daireleri, dikey govde/spine, govdeden
cikan ok) asagida sabit olarak tanimlanir; bu betik hem rasteri (ICO + PNG) HEM DE
`src/VidShrink.App/Assets/VidShrink.svg`'yi bu sabitlerden uretir. `VidShrink.svg`
elle duzenlenmez - degisiklik burada (bu dosyada) yapilir, `python tools/ikon/uret.py`
her ikisini birden yeniden yazar. Makinede cairosvg/resvg/inkscape yoktu; raster taraf
SVG'yi geri okumaz, ayni sayilari dogrudan Pillow + numpy ile supersample cizer.

Geometri, eski ikonun piksel olcumunden turetildi (bkz. plan): her kol = ust daire
(kanca ucu) + dikey dikdortgen govde (spine) + alt daire (kanca ucu) + govdenin tam
ortasindan cikan, govdeyle AYNI dolgu/kontuk tasiyan tek parca ok (govde+bas). Dort
parca tek maskede birlesir (union), kontur o birlesik siluetin etrafina tek seferde
cizilir - ok artik ayri yuzen bir ucgen degil, kolun kendisi. Kenarlar yumusak (daire +
dikdortgen + ok hep ayni kalinlik ailesinden), koseler yuvarlak, ama siluet halka
degil: ortada bosluk (dikey govde ile ic taraftaki bosluk) eski parantez-ok okunurlugunu
korur.

Renkler yalniz su kaynaktan: C:\\Users\\Administrator\\.claude\\teknesyum-private\\teknesyum-ui\\benim.tokens.json
  brand.renk-1        #6fb7ff  (kollarin BASKIN rengi - govdenin ortasi hep bu, duz)
  brand.renk-2-text   #fa8cff  (pembe, oynat ucgeni ust)
  brand.renk-3-text   #ac7fff  (mor, oynat ucgeni alt)
  brand.black/surface #000000  (tum kontur/cerceve)
Turetilmis (tokende yok, alfa/karisim kesimleri) - yalniz VURGU (gradyanin uc %18'i,
govdenin geri kalani duz renk-1 - kontrast siyah zemine karsi eski ikon kadar okunsun):
  renk-1-acik  = renk-1 %30 beyaza karisik   -> #9acdff
  renk-1-koyu  = renk-1 %20 siyaha karisik   -> #5992cc
  renk-2-parlak= renk-2-text %40 beyaza karisik -> #fcbaff (ucgen parlama yuzeyi)

Calistirma: python tools/ikon/uret.py
Cikti: VidShrink.ico (9 boy), VidShrink.png (1024), VidShrink.svg (ayni geometriden).
Bagimlilik: Pillow, numpy (bu makinede kuruluydu; yoksa `pip install pillow numpy`,
proje disina dokunmaz).
"""

import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

GRID = 256
ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
MASTER_PNG_SIZE = 1024
ASSETS_DIR = "src/VidShrink.App/Assets"

RENK_1 = (0x6F, 0xB7, 0xFF)
RENK_1_ACIK = (0x9A, 0xCD, 0xFF)
RENK_1_KOYU = (0x59, 0x92, 0xCC)
RENK_2_TEXT = (0xFA, 0x8C, 0xFF)
RENK_3_TEXT = (0xAC, 0x7F, 0xFF)
RENK_2_PARLAK = (0xFC, 0xBA, 0xFF)
SIYAH = (0x00, 0x00, 0x00)

# Eski ikonun piksel olcumunden (256 izgarasina cevrilmis, sol kol): tek parca
# siluet - ust kanca (sivri disbukey ucla baslar, disari sisip iceri sivrilir),
# duz dikey govde (spine), govdenin tam ortasindan cikan ok (govdeyle ayni
# kenar), alt kanca (ust kancanin dikey aynasi). Ic/dis kenar profillari
# ayri ayri olculdu; ikisi tek kapali poligona dikilir (daire+dikdortgen
# birlesimi degil - o "kafa-govde" gibi okunuyordu, halka+nokta hissi verdi).
# Ic (sag) kenar - y 49'dan merkez 128'e (ok ucu) kadar, artan y:
ARM_INNER_TOP = [
    (47, 49), (67, 53), (73, 61), (68, 71),
    (38, 82), (34, 92), (34, 102), (57, 112), (75, 128),
]
# Dis (sol) kenar - ayni y araligi, kancanin dis kavisi, sonra duz govde:
ARM_OUTER_TOP = [
    (47, 49), (40, 53), (27, 61), (20, 71),
    (16, 82), (14.7, 92), (14.7, 102), (14.7, 112), (14.7, 128),
]
ARM_CORNER_RADIUS = 4

ARM_BORDER = 4


def _mirror_y(pts):
    return [(x, GRID - y) for x, y in pts]


def _dedupe(pts):
    out = [pts[0]]
    for p in pts[1:]:
        if abs(p[0] - out[-1][0]) > 1e-6 or abs(p[1] - out[-1][1]) > 1e-6:
            out.append(p)
    if abs(out[0][0] - out[-1][0]) < 1e-6 and abs(out[0][1] - out[-1][1]) < 1e-6:
        out.pop()
    return out


def build_arm_polygon():
    inner = ARM_INNER_TOP + list(reversed(_mirror_y(ARM_INNER_TOP[:-1])))
    outer = ARM_OUTER_TOP + list(reversed(_mirror_y(ARM_OUTER_TOP[:-1])))
    return _dedupe(inner + list(reversed(outer)))


ARM_POLY = build_arm_polygon()
ARM_RADII = [ARM_CORNER_RADIUS] * len(ARM_POLY)

TRIANGLE_OUTER = [(86, 74), (86, 182), (170, 128)]
TRIANGLE_OUTER_RADII = [14, 14, 9]
TRIANGLE_INNER = [(94, 86), (94, 170), (158, 128)]
TRIANGLE_INNER_RADII = [11, 11, 7]

GRAD_STOPS = [
    (0.0, RENK_1_ACIK),
    (0.18, RENK_1),
    (0.82, RENK_1),
    (1.0, RENK_1_KOYU),
]


def mirror_x(pts):
    return [(GRID - x, y) for x, y in pts]


def mirror_point(p):
    return (GRID - p[0], p[1])


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
        din_u = din / np.hypot(*din)
        a = np.array(v) - din_u * r
        dout = np.array(next_v) - np.array(v)
        dout_u = dout / np.hypot(*dout)
        b = np.array(v) + dout_u * r
        out.append(tuple(a))
        for s in cubic_bezier(tuple(a), v, v, tuple(b), samples)[1:-1]:
            out.append(s)
        out.append(tuple(b))
    return out


def scale_pts(pts, factor):
    return [(x * factor, y * factor) for x, y in pts]


def make_gradient_stops(size, stops, angle_deg=45):
    h = w = size
    yy, xx = np.mgrid[0:h, 0:w]
    theta = np.radians(angle_deg)
    proj = xx * np.cos(theta) + yy * np.sin(theta)
    proj = (proj - proj.min()) / (proj.max() - proj.min())
    grad = np.zeros((h, w, 4), dtype=np.float32)
    for c in range(3):
        xs = [s[0] for s in stops]
        ys = [s[1][c] for s in stops]
        grad[:, :, c] = np.interp(proj, xs, ys)
    grad[:, :, 3] = 255
    return Image.fromarray(grad.astype(np.uint8), "RGBA")


def mask_new(size):
    return Image.new("L", (size, size), 0)


def draw_circle(mask, center, r):
    d = ImageDraw.Draw(mask)
    d.ellipse([center[0] - r, center[1] - r, center[0] + r, center[1] + r], fill=255)


def draw_rect(mask, x0, y0, x1, y1):
    ImageDraw.Draw(mask).rectangle([x0, y0, x1, y1], fill=255)


def draw_polygon(mask, pts):
    ImageDraw.Draw(mask).polygon(pts, fill=255)


def paste_masked(base, source_img, mask):
    base.paste(source_img, (0, 0), mask)


def dilate(mask, border_px):
    r = max(1, int(round(border_px)))
    k = 2 * r + 1
    return mask.filter(ImageFilter.MaxFilter(k))


def arm_union_mask(size, scale, mirror, boost):
    m = mask_new(size)
    pts = rounded_polygon_points(ARM_POLY, ARM_RADII, samples=10)
    if mirror:
        pts = mirror_x(pts)
    draw_polygon(m, scale_pts(pts, scale))
    if boost > 1.0:
        m = dilate(m, (boost - 1.0) * 9 * scale)
    return m


def render(size, stroke_boost=1.0, supersample=8):
    ss = size * supersample
    scale = ss / GRID
    canvas = Image.new("RGBA", (ss, ss), (0, 0, 0, 0))

    left_union = arm_union_mask(ss, scale, False, stroke_boost)
    right_union = arm_union_mask(ss, scale, True, stroke_boost)

    border_px = ARM_BORDER * scale * stroke_boost
    for union in (left_union, right_union):
        outline = dilate(union, border_px)
        black = Image.new("RGBA", (ss, ss), SIYAH + (255,))
        paste_masked(canvas, black, outline)
    for union in (left_union, right_union):
        grad = make_gradient_stops(ss, GRAD_STOPS, angle_deg=45)
        paste_masked(canvas, grad, union)

    outer_pts = scale_pts(rounded_polygon_points(TRIANGLE_OUTER, TRIANGLE_OUTER_RADII), scale)
    outer_mask = mask_new(ss)
    draw_polygon(outer_mask, outer_pts)
    black = Image.new("RGBA", (ss, ss), SIYAH + (255,))
    paste_masked(canvas, black, outer_mask)

    inner_pts = scale_pts(rounded_polygon_points(TRIANGLE_INNER, TRIANGLE_INNER_RADII), scale)
    inner_mask = mask_new(ss)
    draw_polygon(inner_mask, inner_pts)
    grad = make_gradient_stops(ss, [(0.0, RENK_2_TEXT), (1.0, RENK_3_TEXT)], angle_deg=45)
    paste_masked(canvas, grad, inner_mask)

    cx = sum(p[0] for p in TRIANGLE_INNER) / 3
    cy = sum(p[1] for p in TRIANGLE_INNER) / 3
    hi_pts = [
        TRIANGLE_INNER[0],
        (cx, cy),
        ((TRIANGLE_INNER[0][0] + TRIANGLE_INNER[1][0]) / 2 - 6, (TRIANGLE_INNER[0][1] + TRIANGLE_INNER[1][1]) / 2),
        TRIANGLE_INNER[1],
    ]
    hi_mask_poly = mask_new(ss)
    draw_polygon(hi_mask_poly, scale_pts(hi_pts, scale))
    hi_mask_np = np.minimum(np.array(hi_mask_poly), np.array(inner_mask))
    hi_mask = Image.fromarray((hi_mask_np.astype(np.float32) * (110 / 255)).astype(np.uint8))
    flat_hi = Image.new("RGBA", (ss, ss), RENK_2_PARLAK + (255,))
    canvas.paste(flat_hi, (0, 0), hi_mask)

    return canvas.resize((size, size), Image.LANCZOS)


def build_all(out_dir=ASSETS_DIR):
    os.makedirs(out_dir, exist_ok=True)

    icons = {}
    for size in ICO_SIZES:
        boost = 1.3 if size <= 20 else (1.15 if size <= 32 else 1.0)
        supersample = 16 if size <= 32 else 8
        icons[size] = render(size, stroke_boost=boost, supersample=supersample)

    icons[ICO_SIZES[-1]].save(
        os.path.join(out_dir, "VidShrink.ico"),
        sizes=[(s, s) for s in ICO_SIZES],
        append_images=[icons[s] for s in ICO_SIZES if s != ICO_SIZES[-1]],
    )

    master = render(MASTER_PNG_SIZE, stroke_boost=1.0, supersample=2)
    master.save(os.path.join(out_dir, "VidShrink.png"))

    emit_svg(os.path.join(out_dir, "VidShrink.svg"))

    return icons, master


def _hex(c):
    return "#%02x%02x%02x" % c


def _inflate_rect(x0, y0, x1, y1, b):
    return x0 - b, y0 - b, x1 + b, y1 + b


def _inflate_polygon(pts, b):
    cx = sum(p[0] for p in pts) / len(pts)
    cy = sum(p[1] for p in pts) / len(pts)
    out = []
    for x, y in pts:
        dx, dy = x - cx, y - cy
        d = (dx ** 2 + dy ** 2) ** 0.5 or 1.0
        out.append((x + dx / d * b, y + dy / d * b))
    return out


def _fmt(pts):
    return " ".join(f"{x:.1f},{y:.1f}" for x, y in pts)


def rounded_tri_path(vertices, radii):
    n = len(vertices)
    corners = []
    for i in range(n):
        prev_v = vertices[(i - 1) % n]
        v = vertices[i]
        next_v = vertices[(i + 1) % n]
        r = radii[i]
        din = np.array(v) - np.array(prev_v)
        din_u = din / np.hypot(*din)
        a = np.array(v) - din_u * r
        dout = np.array(next_v) - np.array(v)
        dout_u = dout / np.hypot(*dout)
        b = np.array(v) + dout_u * r
        corners.append((tuple(np.round(a, 2)), v, tuple(np.round(b, 2))))
    d = ""
    for i, (a, v, b) in enumerate(corners):
        d += (f"M{a[0]},{a[1]} " if i == 0 else f"L{a[0]},{a[1]} ") + f"Q{v[0]},{v[1]} {b[0]},{b[1]} "
    return d + "Z"


def emit_svg(path):
    """Rasterle ayni sabitlerden SVG uretir (bu betik tek kaynak; VidShrink.svg
    elle degistirilmez)."""
    outer_b = ARM_BORDER * 2

    svg = []
    svg.append('<svg viewBox="0 0 256 256" xmlns="http://www.w3.org/2000/svg">')
    svg.append("  <!-- Bu dosya tools/ikon/uret.py::emit_svg tarafindan uretilir - elle duzenlenmez. -->")
    svg.append("  <defs>")
    svg.append(f'    <linearGradient id="kol" x1="0" y1="0" x2="{GRID}" y2="{GRID}" gradientUnits="userSpaceOnUse">')
    for pos, col in GRAD_STOPS:
        svg.append(f'      <stop offset="{pos}" stop-color="{_hex(col)}"/>')
    svg.append("    </linearGradient>")
    svg.append(f'    <linearGradient id="ucgen" x1="{TRIANGLE_INNER[0][0]}" y1="{TRIANGLE_INNER[0][1]}" x2="{TRIANGLE_INNER[2][0]}" y2="{TRIANGLE_INNER[2][1]}" gradientUnits="userSpaceOnUse">')
    svg.append(f'      <stop offset="0" stop-color="{_hex(RENK_2_TEXT)}"/>')
    svg.append(f'      <stop offset="1" stop-color="{_hex(RENK_3_TEXT)}"/>')
    svg.append("    </linearGradient>")
    svg.append("  </defs>")

    arm_smooth = rounded_polygon_points(ARM_POLY, ARM_RADII, samples=10)
    for mirror, name in ((False, "Sol"), (True, "Sag")):
        pts = mirror_x(arm_smooth) if mirror else arm_smooth
        svg.append(f'  <g id="kol{name}">')
        svg.append(f'    <polygon points="{_fmt(_inflate_polygon(pts, outer_b))}" fill="#000000"/>')
        svg.append(f'    <polygon points="{_fmt(pts)}" fill="url(#kol)"/>')
        svg.append("  </g>")

    svg.append(f'  <path d="{rounded_tri_path(TRIANGLE_OUTER, TRIANGLE_OUTER_RADII)}" fill="#000000"/>')
    svg.append(f'  <path d="{rounded_tri_path(TRIANGLE_INNER, TRIANGLE_INNER_RADII)}" fill="url(#ucgen)"/>')
    cx = sum(p[0] for p in TRIANGLE_INNER) / 3
    cy = sum(p[1] for p in TRIANGLE_INNER) / 3
    hi = [TRIANGLE_INNER[0], (cx, cy), ((TRIANGLE_INNER[0][0] + TRIANGLE_INNER[1][0]) / 2 - 6, (TRIANGLE_INNER[0][1] + TRIANGLE_INNER[1][1]) / 2), TRIANGLE_INNER[1]]
    svg.append(f'  <polygon points="{_fmt(hi)}" fill="{_hex(RENK_2_PARLAK)}" opacity="0.43"/>')
    svg.append("</svg>")
    svg.append("")

    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(svg))


if __name__ == "__main__":
    build_all()
    print("Yazildi: VidShrink.ico (9 boy), VidShrink.png (1024), VidShrink.svg")
