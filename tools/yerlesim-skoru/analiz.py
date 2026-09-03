import sys, math, re
from collections import OrderedDict

def read_layouts(path):
    out = []
    for line in open(path, encoding="utf-8"):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        p = line.split()
        k = int(p[3]) if len(p) > 3 else None
        out.append((int(p[0]), int(p[1]), float(p[2]), k))
    return out

def read_model(path, layouts, default_k):
    tags, cur, prof = OrderedDict(), None, None
    for line in open(path, encoding="utf-8"):
        line = line.rstrip("\n")
        if line.startswith("=== "):
            cur = line[4:].strip().split("-")[0]
            tags[cur] = {"rows": [], "profil": {}, "plan": ""}
            continue
        if line.startswith("#profil"):
            for kv in line.split("\t")[1:]:
                if "=" in kv:
                    a, b = kv.split("=", 1)
                    tags[cur]["profil"][a] = b
            continue
        if line.startswith("#plan"):
            tags[cur]["plan"] = "\t".join(line.split("\t")[1:])
            continue
        if line.startswith("yerlesim") or line.startswith("#videoK") or not line.strip():
            continue
        p = line.split("\t")
        if len(p) < 12:
            continue
        i = len(tags[cur]["rows"])
        w, h, f, k = layouts[i]
        tags[cur]["rows"].append({
            "w": w, "h": h, "fps": f, "k": k or default_k,
            "gerekli": float(p[3]), "saglanan": float(p[4]), "rate": float(p[5]),
            "olcekCezasi": float(p[6]), "fpsCezasi": float(p[7]), "histerez": float(p[8]),
            "skor": float(p[9]), "tabanGecer": p[10] == "True"})
    return tags

def read_tsv(paths):
    out = {}
    for path in paths:
        for line in open(path, encoding="utf-8"):
            p = line.rstrip("\n").split("\t")
            if len(p) < 14:
                continue
            tag = p[0].split("-")[0]
            w, h = p[1].split("x")
            key = (tag, int(w), int(h), float(p[2]), int(p[3]), p[4])
            out[key] = {"mean": float(p[5]), "p10": float(p[6]), "p1": float(p[7]),
                        "min": float(p[8]), "kbps": float(p[10]), "bppf": float(p[11])}
    return out

def ranks(xs):
    order = sorted(range(len(xs)), key=lambda i: xs[i])
    r = [0.0] * len(xs)
    i = 0
    while i < len(order):
        j = i
        while j + 1 < len(order) and xs[order[j + 1]] == xs[order[i]]:
            j += 1
        avg = (i + j) / 2.0 + 1
        for t in range(i, j + 1):
            r[order[t]] = avg
        i = j + 1
    return r

def spearman(a, b):
    ra, rb = ranks(a), ranks(b)
    n = len(a)
    ma, mb = sum(ra) / n, sum(rb) / n
    num = sum((ra[i] - ma) * (rb[i] - mb) for i in range(n))
    da = math.sqrt(sum((x - ma) ** 2 for x in ra))
    db = math.sqrt(sum((x - mb) ** 2 for x in rb))
    return num / (da * db) if da and db else float("nan")

def fit(xs, ys):
    n = len(xs)
    mx, my = sum(xs) / n, sum(ys) / n
    sxy = sum((xs[i] - mx) * (ys[i] - my) for i in range(n))
    sxx = sum((x - mx) ** 2 for x in xs)
    b = sxy / sxx
    a = my - b * mx
    ss = sum((ys[i] - (a + b * xs[i])) ** 2 for i in range(n))
    st = sum((y - my) ** 2 for y in ys)
    return a, b, (1 - ss / st if st else float("nan"))

def main():
    model_path, layout_path, default_k = sys.argv[1], sys.argv[2], int(sys.argv[3])
    enc = "libsvtav1"
    tsvs = sys.argv[4:]
    layouts = read_layouts(layout_path)
    model = read_model(model_path, layouts, default_k)
    meas = read_tsv(tsvs)

    for tag, d in model.items():
        pr = d["profil"]
        print(f"## {tag}  ReferenceBppf={pr.get('ReferenceBppf')} DetailExponent={pr.get('DetailExponent')} "
              f"MotionExponent={pr.get('MotionExponent')} AtReference={pr.get('AtReference')} "
              f"PerHalving={pr.get('PerHalving')} SlopeMeasured={pr.get('SlopeMeasured')} rejim={pr.get('rejim')}")
        print(f"   plan: {d['plan']}")
        print("yerlesim\tk\tgerekli\tsaglanan\trate\tolcCz\tfpsCz\tskor\tolculenMean\tolculenP10\tfark")
        pairs = []
        for r in d["rows"]:
            key = (tag, r["w"], r["h"], r["fps"], r["k"], enc)
            m = meas.get(key)
            mm = f"{m['mean']:.3f}" if m else "-"
            mp = f"{m['p10']:.3f}" if m else "-"
            fk = f"{r['skor'] - m['mean']:+.2f}" if m else "-"
            print(f"{r['w']}x{r['h']}@{r['fps']:g}\t{r['k']}\t{r['gerekli']:.6f}\t{r['saglanan']:.6f}\t"
                  f"{r['rate']:.2f}\t{r['olcekCezasi']:.3f}\t{r['fpsCezasi']:.3f}\t{r['skor']:.2f}\t{mm}\t{mp}\t{fk}")
            if m and r["k"] == default_k:
                pairs.append((r, m))
        if len(pairs) >= 3:
            sm = [p[0]["skor"] for p in pairs]
            sv = [p[1]["mean"] for p in pairs]
            sp = [p[1]["p10"] for p in pairs]
            print(f"   Spearman(skor, mean) = {spearman(sm, sv):+.3f}   n={len(pairs)}")
            print(f"   Spearman(skor, p10)  = {spearman(sm, sp):+.3f}")
            inv = []
            for i in range(len(pairs)):
                for j in range(i + 1, len(pairs)):
                    ds, dv = sm[i] - sm[j], sv[i] - sv[j]
                    if ds * dv < 0:
                        a, b = pairs[i][0], pairs[j][0]
                        inv.append((abs(ds), f"{a['w']}x{a['h']}@{a['fps']:g} vs {b['w']}x{b['h']}@{b['fps']:g}: "
                                             f"skor {sm[i]:.2f} vs {sm[j]:.2f} ({ds:+.2f}), "
                                             f"olculen {sv[i]:.3f} vs {sv[j]:.3f} ({dv:+.3f})"))
            tot = len(pairs) * (len(pairs) - 1) // 2
            print(f"   ters cift: {len(inv)}/{tot}")
            for _, s in sorted(inv, reverse=True):
                print(f"     - {s}")
        for geo in [(1920, 1080, 60.0), (960, 540, 60.0)]:
            lad = [(r, meas.get((tag, r["w"], r["h"], r["fps"], r["k"], enc)))
                   for r in d["rows"] if (r["w"], r["h"], r["fps"]) == geo]
            lad = [(r, m) for r, m in lad if m]
            if len(lad) >= 3:
                xs = [math.log2(m["bppf"]) for _, m in lad]
                ys = [m["mean"] for _, m in lad]
                a, b, r2 = fit(xs, ys)
                print(f"   merdiven {geo[0]}x{geo[1]}@{geo[2]:g}: olculen PerHalving = {b:.3f} "
                      f"(model {pr.get('PerHalving')}), kesisim {a:.3f}, R2 {r2:.4f}, n={len(lad)}")
                for r, m in sorted(lad, key=lambda t: t[1]["bppf"]):
                    print(f"      {r['k']}k bppf={m['bppf']:.6f} mean={m['mean']:.3f} rate={r['rate']:.2f}")
        print()

if __name__ == "__main__":
    main()
