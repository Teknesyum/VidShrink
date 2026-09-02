# -*- coding: utf-8 -*-
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kuyruk import kareler, kotu, kumele, sn, FPS

bosluk = int(sys.argv[1]) if len(sys.argv) > 1 else 6
for ad, yol in (("auto", ".calisma/t122/vmaf/auto-kilitli.json.gz"),
                ("uzman-hb2", ".calisma/t122/vmaf/uzman-hb2-kilitli.json.gz")):
    kr = kareler(yol)
    e, idx = kotu(kr)
    ku = kumele(idx, bosluk)
    buyuk = [k for k in ku if (k[1] - k[0] + 1) >= 5]
    print("== %s  n=%d  p10 esigi=%.3f  kotu kare=%d  kume=%d (>=5 kare: %d)"
          % (ad, len(kr), e, len(idx), len(ku), len(buyuk)))
    for a, b in ku:
        if (b - a + 1) < 5:
            continue
        print("   kare %5d-%5d  %7.3f-%7.3f sn  %4d kare" % (a, b, sn(a), sn(b), b - a + 1))
    tek = sum(1 for a, b in ku if (b - a + 1) < 5)
    print("   ... ayrica %d kucuk kume (<5 kare), toplam %d kare"
          % (tek, sum(b - a + 1 for a, b in ku if (b - a + 1) < 5)))
