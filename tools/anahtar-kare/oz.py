"""libvmaf gunlugunden ortalama, p10 ve en kotu kareyi cikarir."""
import json, sys

with open(sys.argv[1], encoding="utf-8") as f:
    frames = json.load(f)["frames"]

scores = []
for fr in frames:
    m = fr["metrics"]
    scores.append(m.get("vmaf", m.get("vmaf_neg")))

if not scores:
    raise SystemExit("gunlukte kare puani yok: " + sys.argv[1])

scores.sort()
n = len(scores)
# Uretimdeki Percentile ile ayni: dogrusal aradegerleme.
pos = 0.10 * (n - 1)
lo = int(pos)
hi = min(lo + 1, n - 1)
p10 = scores[lo] + (pos - lo) * (scores[hi] - scores[lo])

print("%.4f %.4f %.4f" % (sum(scores) / n, p10, scores[0]))
