# -*- coding: utf-8 -*-
import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bitler import paketler
from kuyruk import p10esik, kotu, t122, BOYUT


print("%-22s %10s %8s %8s %8s | %3s %6s %8s | %s" %
      ("kosum", "boyut B", "ort", "p10", "min", "AK", "AK%", "inter B", "kotu kume"))
for ad in ("auto", "auto-g300", "auto-g600", "auto-g600-boyutesit", "uzman-hb2"):
    mp4 = ".calisma/t122/ciktilar/%s.mp4" % ad
    kr = t122(ad)
    v = [x[1] for x in kr]
    p = paketler(mp4)
    tot = sum(s for _, s, _ in p)
    kb = sum(s for _, s, k in p if k)
    kn = sum(1 for _, _, k in p if k)
    from kuyruk import kumele
    e, kt = kotu(kr)
    ku = kumele(kt, 6)
    print("%-22s %10d %8.3f %8.3f %8.3f | %3d %5.1f%% %8.0f | %d" %
          (ad, BOYUT[ad], sum(v) / len(v), p10esik(v), min(v),
           kn, 100.0 * kb / tot, (tot - kb) / float(len(p) - kn), len(ku)))
