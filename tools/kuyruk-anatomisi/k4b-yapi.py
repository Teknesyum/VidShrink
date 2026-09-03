# -*- coding: utf-8 -*-
import sys, os, gzip, statistics
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kuyruk import t122, kotu
from bitler import paketler
from hareket import yavg

_KOK = os.path.dirname(os.path.abspath(__file__))
oz = {}
with gzip.open(os.path.join(_KOK, "hareket-t122", "ozdeslik.csv.gz"), "rt", encoding="utf-8") as fh:
    next(fh)
    for l in fh:
        a, b, c = l.strip().split(";")
        oz[int(a)] = (int(b), int(c))

kay = set(i for i, (b, c) in oz.items() if b)
cik = set(i for i, (b, c) in oz.items() if c)
uc = set(i for (i, s, k) in paketler("auto.mp4") if not k and s == 3)
Y = yavg()

print("kaynakta onceki kareyle MD5 ozdes: %d / %d" % (len(kay), len(oz)))
print("auto ciktisinda ozdes: %d / %d  (3 baytlik paketlerden: %d / %d)"
      % (len(cik), len(oz), len(cik & uc), len(uc)))
A = [Y[i] for i in uc if i in Y]
B = [Y[i] for i in Y if i not in uc]
print("kaynak ardisik fark YAVG  3 baytlik konumlar: ort=%.3f medyan=%.3f"
      % (statistics.mean(A), statistics.median(A)))
print("                          digerleri:          ort=%.3f medyan=%.3f"
      % (statistics.mean(B), statistics.median(B)))
print()
print("%-22s %-4s %-6s %-8s %s" % ("kosum", "AK", "inter", "3 baytlik", "kodlanan inter (B/kare)"))
for ad in ("auto", "auto-g300", "auto-g600", "auto-g600-boyutesit", "uzman-hb2"):
    p = paketler(ad + ".mp4")
    inter = [(i, s) for (i, s, k) in p if not k]
    u = [s for _, s in inter if s == 3]
    kod = [s for _, s in inter if s > 3]
    e, idx = kotu(t122(ad))
    print("%-22s %-4d %-6d %-8d %d (%d B)   kotu karenin 3 baytligi: %d/%d"
          % (ad, len(p) - len(inter), len(inter), len(u), len(kod),
             sum(kod) / len(kod), sum(1 for i in idx if i in set(x for x, s in inter if s == 3)), len(idx)))
