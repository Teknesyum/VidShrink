# -*- coding: utf-8 -*-
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kuyruk import kareler, kotu, FPS

V = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".calisma", "t122", "vmaf")

def yukle(ad):
    kr = kareler(os.path.join(V, ad + "-kilitli.json.gz"))
    e, kt = kotu(kr)
    return len(kr), set(kt)

n, A = yukle(sys.argv[1] if len(sys.argv) > 1 else "auto")
_, B = yukle(sys.argv[2] if len(sys.argv) > 2 else "uzman-hb2")

print("kare sayisi=%d  |A|=%d  |B|=%d" % (n, len(A), len(B)))
print(" pencere |  sn  | A kova | B kova | kesisim | sanstan | ustsinir | oran | normal")
for w in (1, 3, 6, 15, 30, 60, 120, 300):
    kv = (n + w - 1) // w
    a = set(f // w for f in A)
    b = set(f // w for f in B)
    ks = a & b
    bek = len(a) * len(b) / float(kv)
    ust = min(len(a), len(b))
    print("  %4d   | %4.2f |  %4d  |  %4d  |   %4d  |  %6.1f |   %4d   | %.2f | %.3f"
          % (w, w / FPS, len(a), len(b), len(ks), bek, ust,
             len(ks) / max(bek, 1e-9), (len(ks) - bek) / max(ust - bek, 1e-9)))
