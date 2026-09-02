# -*- coding: utf-8 -*-
import sys, os, statistics
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kuyruk import arsiv, kotu
from hareket import yavg

Y = yavg()
SAHNE = [(0, 1700, "S13"), (1700, 3411, "S14"), (3411, 3624, "S15")]

print("== sahne basina ardisik kare farki (YAVG, 10 bit)")
for a, b, ad in SAHNE:
    v = [Y[i] for i in range(a, b) if i in Y]
    print("   %s [%4d,%4d)  ort=%.3f  medyan=%.3f  en buyuk=%.1f"
          % (ad, a, b, statistics.mean(v), statistics.median(v), max(v)))

def dilim(ad, alt, ust):
    kr = arsiv(ad)
    e, idx = kotu(kr)
    ks = set(idx)
    fr = sorted((Y[i], i) for i in range(alt, ust) if i in Y)
    kt = sum(1 for _, i in fr if i in ks)
    n = len(fr)
    print("\n== %s  kare=[%d,%d) n=%d kotu=%d" % (ad, alt, ust, n, kt))
    d = n // 10
    for j in range(10):
        p = fr[j * d:(j + 1) * d if j < 9 else n]
        kk = sum(1 for _, i in p if i in ks)
        bek = kt * len(p) / float(n)
        print("   dilim %2d  YAVG %.3f-%7.3f  n=%3d  kotu=%3d  beklenen=%5.1f  oran=%.2f"
              % (j + 1, p[0][0], p[-1][0], len(p), kk, bek, kk / max(bek, 1e-9)))

alt = int(sys.argv[1]) if len(sys.argv) > 1 else 1700
ust = int(sys.argv[2]) if len(sys.argv) > 2 else 3411
for ad in ("auto", "uzman-hb2", "uzman-biz3"):
    dilim(ad, alt, ust)
