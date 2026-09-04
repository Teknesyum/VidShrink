import json, sys, gzip, os
def stats(path):
    op = gzip.open if path.endswith(".gz") else open
    with op(path, "rt", encoding="utf-8") as f:
        d = json.load(f)
    xs = sorted(fr["metrics"]["vmaf"] for fr in d["frames"])
    n = len(xs)
    mean = sum(xs) / n
    k = (n - 1) * 0.10
    lo, hi = int(k), min(int(k) + 1, n - 1)
    p10 = xs[lo] + (k - lo) * (xs[hi] - xs[lo])
    return n, mean, p10, xs[0]
if __name__ == "__main__":
    n, mean, p10, worst = stats(sys.argv[1])
    print(f"{n} {mean:.3f} {p10:.3f} {worst:.3f}")
