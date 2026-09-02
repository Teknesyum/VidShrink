# -*- coding: utf-8 -*-
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bitler import paketler
from kuyruk import kotu, t122


def yukle(ad):
    kr = t122(ad)
    e, kt = kotu(kr)
    p = paketler(ad + ".mp4")
    B = dict((i, s) for i, s, k in p if not k)
    return dict(kr), set(kt), B

VA, SA, BA = yukle("auto")
VB, SB, BB = yukle("uzman-hb2")

ORT = set(BA) & set(BB)
KOD = set(i for i in ORT if BA[i] > 3)

def tablo(baslik, evren):
    evren = set(evren)
    oA = sum(BA[i] for i in evren) / float(len(evren))
    oB = sum(BB[i] for i in evren) / float(len(evren))
    print("== %s  evren=%d  auto taban=%.0f B  hb2 taban=%.0f B" % (baslik, len(evren), oA, oB))

    def sat(ad, S):
        ic = sorted(S & evren)
        if not ic:
            return
        n = float(len(ic))
        ba = sum(BA[i] for i in ic) / n
        bb = sum(BB[i] for i in ic) / n
        va = sum(VA[i] for i in ic) / n
        vb = sum(VB[i] for i in ic) / n
        print("   %-20s kare=%4d | auto %6.0f B %.2fx VMAF %7.3f | hb2 %6.0f B %.2fx VMAF %7.3f"
              % (ad, len(ic), ba, ba / oA, va, bb, bb / oB, vb))

    sat("evrenin tamami", evren)
    sat("yalniz auto kotu", SA - SB)
    sat("yalniz hb2 kotu", SB - SA)
    sat("ortak kotu", SA & SB)
    sat("ikisi de iyi", evren - SA - SB)
    print()

tablo("YALNIZ KODLANAN inter kareler (rapordaki tablo)", KOD)
tablo("TUM inter paketler (dayaniklilik)", ORT)
