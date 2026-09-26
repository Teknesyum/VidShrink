"""VidShrink ikonu - rasterleme betigi.

**Tek kaynak**: `src/VidShrink.App/Assets/VidShrink.svg`. O dosya elle yazilmis
kubik Bezier path'ler tasir (parantez kollari: yuvarlatilmis-uc/yuvarlatilmis-
kose stroke; ok govdesi+basi ayni gruptaki ayri, tanidik bir sekil; oynat
ucgeni: kose yuvarlatilmis path). Bu betik SVG'yi DEGISTIRMEZ, yalnizca onu
gercek bir SVG motoruyla (headless Microsoft Edge, `--headless=new
--screenshot`) her ICO boyunda (16, 20, 24, 32, 40, 48, 64, 128, 256) ve
logo boyunda (1024) rasterler, sonuclari tek `.ico`da toplar.

Kucuk boylar icin ayri sadelestirilmis dosya YOK: gercek vektor motoru 16px'te
bile anti-alias ile temiz sonuc veriyor (bkz. rapor), stroke kalinligi SVG'de
sabit - ihtiyac olursa `VidShrink-kucuk.svg`burada eklenir ve KUCUK_SINIR
altindaki boylar ondan uretilir.

Calistirma: python tools/ikon/uret.py
Bagimlilik: Microsoft Edge (headless render), Pillow (ICO paketleme).
"""

import os
import subprocess
import time
from PIL import Image

ASSETS_DIR = "src/VidShrink.App/Assets"
SVG_PATH = os.path.join(ASSETS_DIR, "VidShrink.svg")
ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
MASTER_PNG_SIZE = 1024
KUCUK_SVG_PATH = os.path.join(ASSETS_DIR, "VidShrink-kucuk.svg")
KUCUK_SINIR = 24  # bu boy ve altinda VidShrink-kucuk.svg kullanilir (kalin kontur).

EDGE_CANDIDATES = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
]

HTML_TEMPLATE = """<!DOCTYPE html>
<html><head><style>
html,body{{margin:0;padding:0;background:transparent;}}
svg{{display:block;}}
</style></head>
<body>
{svg}
</body></html>
"""


def find_edge():
    for c in EDGE_CANDIDATES:
        if os.path.isfile(c):
            return c
    raise RuntimeError("msedge.exe bulunamadi - " + ", ".join(EDGE_CANDIDATES))


def render_svg_to_png(edge, svg_path, size, out_png, work_dir):
    with open(svg_path, "r", encoding="utf-8") as f:
        svg = f.read()
    svg = svg.replace(
        'viewBox="0 0 256 256"',
        'viewBox="0 0 256 256" width="%d" height="%d"' % (size, size),
        1,
    )
    html_path = os.path.join(work_dir, "_render_%d.html" % size)
    with open(html_path, "w", encoding="utf-8") as f:
        f.write(HTML_TEMPLATE.format(svg=svg))

    url = "file:///" + os.path.abspath(html_path).replace("\\", "/")
    subprocess.run(
        [
            edge,
            "--headless=new",
            "--disable-gpu",
            "--screenshot=" + os.path.abspath(out_png),
            "--window-size=%d,%d" % (size, size),
            "--default-background-color=00000000",
            url,
        ],
        check=True,
        capture_output=True,
    )
    for _ in range(50):
        if os.path.isfile(out_png) and os.path.getsize(out_png) > 0:
            break
        time.sleep(0.1)
    im = Image.open(out_png).convert("RGBA")
    if im.size != (size, size):
        im = im.resize((size, size), Image.LANCZOS)
    return im


def build_all(out_dir=ASSETS_DIR, work_dir=".calisma/ikon-render"):
    os.makedirs(work_dir, exist_ok=True)
    edge = find_edge()

    frames = {}
    for size in ICO_SIZES:
        src = KUCUK_SVG_PATH if (KUCUK_SINIR and size <= KUCUK_SINIR and os.path.isfile(KUCUK_SVG_PATH)) else SVG_PATH
        out_png = os.path.join(work_dir, "boy_%d.png" % size)
        frames[size] = render_svg_to_png(edge, src, size, out_png, work_dir)

    frames[ICO_SIZES[-1]].save(
        os.path.join(out_dir, "VidShrink.ico"),
        sizes=[(s, s) for s in ICO_SIZES],
        append_images=[frames[s] for s in ICO_SIZES if s != ICO_SIZES[-1]],
    )

    master_png = os.path.join(work_dir, "master.png")
    master = render_svg_to_png(edge, SVG_PATH, MASTER_PNG_SIZE, master_png, work_dir)
    master.save(os.path.join(out_dir, "VidShrink.png"))

    return frames, master


if __name__ == "__main__":
    build_all()
    print("Yazildi: VidShrink.ico (9 boy, headless Edge ile rasterlendi), VidShrink.png (1024)")
