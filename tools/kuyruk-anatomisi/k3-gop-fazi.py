# -*- coding: utf-8 -*-
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kuyruk import arsiv, kotu

ALT = int(sys.argv[1]) if len(sys.argv) > 1 else 1700
UST = int(sys.argv[2]) if len(sys.argv) > 2 else 3411

def faz(kaynak, g, ad):
    tum = arsiv(kaynak)
    e, tumidx = kotu(tum)
    kr = [(n, v) for n, v in tum if ALT <= n < UST]
    idx = [n for n in tumidx if ALT <= n < UST]
    kotus = set(idx)
    puan = dict(kr)
    kova = [[] for _ in range(g)]
    kotukova = [0] * g
    for n, v in kr:
        kova[n % g].append(v)
        if n in kotus:
            kotukova[n % g] += 1
    dilim = max(1, g // 12)
    print("== %s  (g=%d varsayimi, %d dilim, kare [%d,%d), esik=%.3f)" % (ad, g, g // dilim, ALT, UST, e))
    print("   faz araligi | ort VMAF | kotu kare | beklenen")
    tk = len(idx); tn = len(kr)
    for i in range(0, g, dilim):
        vs = [x for j in range(i, min(i + dilim, g)) for x in kova[j]]
        kk = sum(kotukova[i:i + dilim])
        bek = tk * len(vs) / float(tn)
        print("   %3d-%3d      | %8.3f | %6d    | %6.1f  %s"
              % (i, min(i + dilim, g) - 1, sum(vs) / len(vs), kk, bek,
                 "<<<" if kk > 2 * bek else ("---" if kk < 0.5 * bek else "")))

faz("auto", 120, "auto  -g 120")
print()
faz("uzman-biz3", 300, "uzman-biz3  -g 300")
