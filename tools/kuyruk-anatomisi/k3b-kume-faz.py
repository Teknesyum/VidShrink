# -*- coding: utf-8 -*-
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kuyruk import kareler, kotu, kumele

V = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".calisma", "t122", "vmaf")
ad = sys.argv[1] if len(sys.argv) > 1 else "auto"
g = int(sys.argv[2]) if len(sys.argv) > 2 else 120
kr = kareler(os.path.join(V, ad + "-kilitli.json.gz"))
e, kt = kotu(kr)
ku = [k for k in kumele(kt, 6) if k[1] - k[0] + 1 >= 5]
print("%s g=%d  >=5 karelik kume=%d" % (ad, g, len(ku)))
print("  bas   son | bas%g  son%g | sona kalan")
bs, sn5 = [], 0
for a, b in ku:
    kalan = (-b) % g
    if kalan <= 5:
        sn5 += 1
    bs.append(a % g)
    print("  %4d %4d | %4d %4d | %3d %s" % (a, b, a % g, b % g, kalan, "<<<" if kalan <= 5 else ""))
print("  GOP sonuna <=5 kare kala biten kume: %d / %d" % (sn5, len(ku)))
print("  en kucuk baslangic fazi: %d" % min(bs))
