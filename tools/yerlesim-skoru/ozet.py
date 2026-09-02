import json, sys, statistics

log, tag, w, h, fps, kbit, enc, kbps, bppf, size, frames = sys.argv[1:12]
d = json.load(open(log))
vals = [f["metrics"]["vmaf"] for f in d["frames"]]
vals_sorted = sorted(vals)
n = len(vals)
def pct(p):
    if n == 0: return float("nan")
    i = min(n - 1, max(0, int(round(p / 100.0 * (n - 1)))))
    return vals_sorted[i]
mean = statistics.fmean(vals)
print("\t".join([tag, f"{w}x{h}", fps, kbit, enc,
                 f"{mean:.3f}", f"{pct(10):.3f}", f"{pct(1):.3f}",
                 f"{min(vals):.3f}", str(n), kbps, bppf, size, frames]))
