# -*- coding: utf-8 -*-
import json, gzip, io, sys, os

def stat(p):
    op = gzip.open if p.endswith(".gz") else io.open
    with op(p, "rt", encoding="utf-8") as fh:
        fr = json.load(fh)["frames"]
    s = []
    for f in fr:
        m = f["metrics"]
        v = m.get("vmaf", m.get("vmaf_neg"))
        if v is not None:
            s.append(v)
    srt = sorted(s)
    r = 0.10 * (len(srt) - 1)
    lo, hi = int(r // 1), int(-(-r // 1))
    p10 = srt[lo] if lo == hi else srt[lo] + (srt[hi] - srt[lo]) * (r - lo)
    harm = len(s) / sum(1.0 / max(x, 1.0) for x in s)
    return len(s), sum(s) / len(s), p10, harm, min(s), sum(1 for x in s if x < 1.0)

for p in sys.argv[1:]:
    if not os.path.exists(p):
        sys.stdout.write("%-28s olculmedi\n" % os.path.basename(p)); continue
    n, o, q, h, mn, a = stat(p)
    sys.stdout.write("%-28s n=%-5d ort=%8.3f p10=%8.3f harm=%8.3f min=%8.3f alti1=%d\n"
                     % (os.path.basename(p), n, o, q, h, mn, a))
