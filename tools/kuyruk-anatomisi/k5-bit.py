# -*- coding: utf-8 -*-
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bitler import paketler
from kuyruk import FPS

SAHNE = [(0, 1700, 13), (1700, 3411, 14), (3411, 3624, 15)]

def profil(ad, g):
    p = paketler(".calisma/t122/ciktilar/%s.mp4" % ad)
    b = [s for _, s, _ in p]
    kf = set(i for i, _, k in p if k)
    n = len(b)
    ort = sum(b) / float(n)
    print("== %s  kare=%d  video=%d bayt  ort=%.0f bayt/kare  anahtar=%d" % (ad, n, sum(b), ort, len(kf)))
    print("  sahne bazinda (anahtar kareler haric):")
    for a, z, ix in SAHNE:
        v = [b[i] for i in range(a, min(z, n)) if i not in kf]
        print("   S%-2d  kare=%4d  ort=%6.0f bayt  goreli=%.3f" % (ix, len(v), sum(v) / len(v), (sum(v) / len(v)) / ort))
    if g:
        print("  GOP fazi (yalniz S14, anahtar kareler haric):")
        d = max(1, g // 12)
        for i in range(0, g, d):
            v = [b[k] for k in range(1700, 3411) if i <= k % g < min(i + d, g) and k not in kf]
            if v:
                print("   %3d-%3d  kare=%3d  ort=%6.0f bayt  goreli=%.3f" % (i, min(i + d, g) - 1, len(v), sum(v) / len(v), (sum(v) / len(v)) / ort))
    print()

profil("auto", 120)
profil("uzman-hb2", 600)
