# -*- coding: utf-8 -*-
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kuyruk import arsiv, kotu, FPS

KESIM = [1700, 3411]
SAHNE = [(0, 1700, 0.07119, 13), (1700, 3411, 0.12892, 14), (3411, 3626, 0.08546, 15)]

def rapor(ad):
    kr = arsiv(ad)
    n = len(kr)
    e, kt = kotu(kr)
    ks = set(kt)
    print("== %s  n=%d  esik=%.3f  kotu=%d" % (ad, n, e, len(kt)))

    print("  sahne bazinda:")
    for a, b, kx, ix in SAHNE:
        b = min(b, n)
        top = sum(1 for f in range(a, b) if f in ks)
        pay = 100.0 * top / max(b - a, 1)
        bek = (b - a) * len(kt) / n
        print("   S%-2d [%4d-%4d) %5.1f-%5.1f sn karm=%.5f  kare=%4d  kotu=%3d (%%%.1f)  sanstan=%.1f  oran=%.2f"
              % (ix, a, b, a / FPS, b / FPS, kx, b - a, top, pay, bek, top / max(bek, 1e-9)))

    print("  kesim sonrasi pencereler (kesim=%s):" % KESIM)
    for w in (15, 30, 60, 120):
        top = 0
        kap = 0
        for c in KESIM:
            for f in range(c, min(c + w, n)):
                kap += 1
                if f in ks:
                    top += 1
        bek = kap * len(kt) / n
        print("   +%3d kare (%.2f sn): kapsam=%3d  kotu=%3d  sanstan=%5.1f  oran=%.2f"
              % (w, w / FPS, kap, top, bek, top / max(bek, 1e-9)))

    print("  kesim oncesi pencereler:")
    for w in (30, 60):
        top = 0
        kap = 0
        for c in KESIM:
            for f in range(max(c - w, 0), c):
                kap += 1
                if f in ks:
                    top += 1
        bek = kap * len(kt) / n
        print("   -%3d kare: kapsam=%3d  kotu=%3d  sanstan=%5.1f  oran=%.2f"
              % (w, kap, top, bek, top / max(bek, 1e-9)))
    print()

for ad in sys.argv[1:] or ["auto", "uzman-hb2", "uzman-biz3"]:
    rapor(ad)
