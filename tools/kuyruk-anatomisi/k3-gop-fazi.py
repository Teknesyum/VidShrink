# -*- coding: utf-8 -*-
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kuyruk import kareler, kotu

def faz(yol, g, ad):
    kr = kareler(yol)
    e, idx = kotu(kr)
    kotus = set(idx)
    puan = dict(kr)
    kova = [[] for _ in range(g)]
    kotukova = [0] * g
    for n, v in kr:
        kova[n % g].append(v)
        if n in kotus:
            kotukova[n % g] += 1
    dilim = max(1, g // 12)
    print("== %s  (g=%d varsayimi, %d dilim)" % (ad, g, g // dilim))
    print("   faz araligi | ort VMAF | kotu kare | beklenen")
    tk = len(idx); tn = len(kr)
    for i in range(0, g, dilim):
        vs = [x for j in range(i, min(i + dilim, g)) for x in kova[j]]
        kk = sum(kotukova[i:i + dilim])
        bek = tk * len(vs) / float(tn)
        print("   %3d-%3d      | %8.3f | %6d    | %6.1f  %s"
              % (i, min(i + dilim, g) - 1, sum(vs) / len(vs), kk, bek,
                 "<<<" if kk > 2 * bek else ("---" if kk < 0.5 * bek else "")))

faz(".calisma/t122/vmaf/auto-kilitli.json.gz", 120, "auto  -g 120")
print()
faz(".calisma/t122/vmaf/uzman-biz3-kilitli.json.gz", 300, "uzman-biz3  -g 300")
