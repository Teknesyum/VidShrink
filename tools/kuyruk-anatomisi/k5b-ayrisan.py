# -*- coding: utf-8 -*-
import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bitler import paketler
from kuyruk import kotu

def duz(y):
    fr = json.load(open(y, encoding="utf-8"))["frames"]
    o = []
    for f in fr:
        m = f["metrics"]
        v = m.get("vmaf", m.get("vmaf_neg"))
        if v is not None:
            o.append((f["frameNum"], v))
    o.sort()
    return o

def yukle(ad):
    kr = duz(".calisma/t122/vmaf-t122/%s-kilitli.json" % ad)
    e, kt = kotu(kr)
    p = paketler(".calisma/t122/ciktilar/%s.mp4" % ad)
    B = {i: s for i, s, _ in p}
    kf = set(i for i, _, k in p if k)
    inter = [B[i] for i in B if i not in kf]
    return dict(kr), set(kt), B, kf, sum(inter) / float(len(inter))

VA, SA, BA, KA, oA = yukle("auto")
VB, SB, BB, KB, oB = yukle("uzman-hb2")
N = len(VA)
print("auto  kotu=%d  inter ort=%.0f B   hb2 kotu=%d  inter ort=%.0f B  (hb2/auto=%.2f)"
      % (len(SA), oA, len(SB), oB, oB / oA))
print("kesisim=%d  sanstan=%.1f" % (len(SA & SB), len(SA) * len(SB) / float(N)))
print()

def kume(ad, S):
    ic = [n for n in S if n not in KA and n not in KB]
    if not ic:
        return
    ba = sum(BA[n] for n in ic) / float(len(ic))
    bb = sum(BB[n] for n in ic) / float(len(ic))
    va = sum(VA[n] for n in ic) / float(len(ic))
    vb = sum(VB[n] for n in ic) / float(len(ic))
    print("%-22s kare=%4d | auto %6.0f B (%.2f×) VMAF %.3f | hb2 %6.0f B (%.2f×) VMAF %.3f | hb2/auto bit=%.2f"
          % (ad, len(ic), ba, ba / oA, va, bb, bb / oB, vb, (bb / oB) / (ba / oA)))

tum = set(range(N))
kume("TUM inter kareler", tum)
kume("YALNIZ auto kotu", SA - SB)
kume("YALNIZ hb2 kotu", SB - SA)
kume("ORTAK kotu", SA & SB)
kume("IKISI DE IYI", tum - SA - SB)
