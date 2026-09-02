# -*- coding: utf-8 -*-
import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bitler import paketler
from kuyruk import kotu, p10esik

def duz(yol):
    fr = json.load(open(yol, encoding="utf-8"))["frames"]
    out = []
    for f in fr:
        m = f["metrics"]
        v = m.get("vmaf", m.get("vmaf_neg"))
        if v is not None:
            out.append((f["frameNum"], v))
    out.sort()
    return out

def rapor(ad):
    kr = duz(".calisma/t122/vmaf-t122/%s-kilitli.json" % ad)
    e, kt = kotu(kr)
    ks = set(kt)
    p = paketler(".calisma/t122/ciktilar/%s.mp4" % ad)
    b = {i: s for i, s, _ in p}
    kf = set(i for i, _, k in p if k)
    iyi = [b[n] for n, _ in kr if n not in ks and n not in kf]
    kot = [b[n] for n in kt if n not in kf]
    tum = [b[n] for n, _ in kr if n not in kf]
    o = lambda x: sum(x) / float(len(x))
    print("== %s  p10 esigi=%.3f  kotu=%d (anahtar kare kotu listede: %d)"
          % (ad, e, len(kt), len(ks & kf)))
    print("   inter kare ort bayt : tumu=%.0f  iyi=%.0f  kotu=%.0f  kotu/iyi=%.3f"
          % (o(tum), o(iyi), o(kot), o(kot) / o(iyi)))
    srt = sorted(tum)
    print("   kotu karelerin bayt medyani=%.0f, iyi=%.0f"
          % (sorted(kot)[len(kot) // 2], sorted(iyi)[len(iyi) // 2]))
    dilim = [0, 10, 25, 50, 75, 90, 100]
    print("   bayt yuzdelik dilimlerinde kotu kare orani:")
    for i in range(len(dilim) - 1):
        a = srt[int(len(srt) * dilim[i] / 100.0)]
        z = srt[min(int(len(srt) * dilim[i + 1] / 100.0), len(srt) - 1)]
        gr = [n for n, _ in kr if n not in kf and a <= b[n] <= z]
        kk = sum(1 for n in gr if n in ks)
        print("     %%%3d-%%%3d (%6d-%6d bayt) kare=%4d kotu=%3d (%%%.1f)"
              % (dilim[i], dilim[i + 1], a, z, len(gr), kk, 100.0 * kk / max(len(gr), 1)))
    print()

for ad in sys.argv[1:] or ["auto"]:
    rapor(ad)
