# -*- coding: utf-8 -*-
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kuyruk import arsiv, kotu, kumele, sn

ka, kb = arsiv("auto"), arsiv("uzman-hb2")
ea, ia = kotu(ka)
eb, ib = kotu(kb)
sa, sb = set(ia), set(ib)
ks = sa & sb
print("auto kotu=%d  hb2 kotu=%d  kesisim=%d" % (len(sa), len(sb), len(ks)))
print("Jaccard=%.4f  auto'nun kesisim orani=%.4f  hb2'nin=%.4f"
      % (len(ks) / float(len(sa | sb)), len(ks) / float(len(sa)), len(ks) / float(len(sb))))
print("rastgele beklenen kesisim (bagimsizlik): %.1f kare (%.4f oran)"
      % (len(sa) * len(sb) / 3624.0, len(sa) * len(sb) / 3624.0 / len(sa)))
for ad, kume in (("YALNIZ auto", sorted(sa - sb)), ("YALNIZ hb2", sorted(sb - sa)),
                 ("ORTAK", sorted(ks))):
    ku = kumele(kume, 6)
    buyuk = [k for k in ku if k[1] - k[0] + 1 >= 5]
    print("\n== %s: %d kare, %d kume (>=5 kare: %d)" % (ad, len(kume), len(ku), len(buyuk)))
    for a, b in buyuk:
        print("   %5d-%5d  %7.3f-%7.3f sn  %4d kare" % (a, b, sn(a), sn(b), b - a + 1))
