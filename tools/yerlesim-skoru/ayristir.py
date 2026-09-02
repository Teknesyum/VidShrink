import sys, math, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__))))
from analiz import read_layouts, read_model, read_tsv, spearman, fit

SRC = {"A": (1920, 1080, 60.0), "B": (1920, 1080, 60.0), "C": (1920, 1080, 30.0)}

def load(model_path, layout_path, tsvs, k=800):
    layouts = read_layouts(layout_path)
    model = read_model(model_path, layouts, k)
    meas = read_tsv(tsvs)
    out = {}
    for tag, d in model.items():
        rows = []
        for r in d["rows"]:
            m = meas.get((tag, r["w"], r["h"], r["fps"], r["k"], "libsvtav1"))
            if m:
                rows.append((r, m))
        out[tag] = (d, rows)
    return out

def shape_scale(s, e=1.1):
    return 0.0 if s >= 0.999 else math.pow(1.0 / max(s, 0.05) - 1.0, e)

def report(data, k=800):
    fitS, fitF = [], []
    for tag, (d, rows) in data.items():
        sw, sh, sf = SRC[tag]
        base = [(r, m) for r, m in rows if r["w"] == sw and r["fps"] == sf and r["k"] == k]
        if not base:
            continue
        rb, mb = base[0]
        print(f"## {tag}  taban {sw}x{sh}@{sf:g} olculen={mb['mean']:.3f} rate={rb['rate']:.2f}")
        print("  --- olcek ekseni (fps kaynakta sabit)")
        for r, m in rows:
            if r["k"] != k or r["fps"] != sf or r["w"] == sw:
                continue
            sc = r["h"] / sh
            dmeas = m["mean"] - mb["mean"]
            drate = r["rate"] - rb["rate"]
            print(f"    olcek {sc:.4f}  modelin rate kredisi {drate:+6.2f}  modelin cezasi "
                  f"{r['olcekCezasi']:6.3f}  model net {r['skor'] - rb['skor']:+6.2f}  "
                  f"olculen {dmeas:+6.3f}  |  olcek-bagimsiz rate ile gereken ceza {-dmeas:6.3f}")
            fitS.append((tag, shape_scale(sc), -dmeas))
        print("  --- kare hizi ekseni (olcek 1)")
        for r, m in rows:
            if r["k"] != k or r["w"] != sw or r["fps"] == sf:
                continue
            dmeas = m["mean"] - mb["mean"]
            drate = r["rate"] - rb["rate"]
            need = drate - dmeas
            hv = math.log2(sf / r["fps"])
            print(f"    fps {r['fps']:g}  modelin rate kredisi {drate:+5.2f}  modelin cezasi "
                  f"{r['fpsCezasi']:6.3f}  model net {r['skor'] - rb['skor']:+6.2f}  "
                  f"olculen {dmeas:+7.3f}  |  gereken ceza {need:6.3f} ({need/hv:6.3f}/halving)")
            fitF.append((tag, hv, need))
        print()
    return fitS, fitF

def lsq_through_origin(pairs):
    num = sum(x * y for x, y in pairs)
    den = sum(x * x for x, _ in pairs)
    return num / den if den else float("nan")

ab = load(".calisma/T107/gunluk/skor-AB.txt", ".calisma/T107/yerlesimler.txt",
          [".calisma/T107/gunluk/izgara-A.tsv", ".calisma/T107/gunluk/izgara-B.tsv"])
c = load(".calisma/T107/gunluk/skor-C.txt", ".calisma/T107/yerlesimler-C.txt",
         [".calisma/T107/gunluk/izgara-C.tsv"])

print("=== UYDURMA KUMESI (A + B)")
fS, fF = report(ab)
print("=== TUTULAN KUME (C, uydurmaya girmedi)")
cS, cF = report(c)

s = lsq_through_origin([(x, y) for _, x, y in fS])
f = lsq_through_origin([(x, y) for _, x, y in fF])
print(f"A+B en kucuk kareler (orijinden): ScalePenalty carpani = {s:.4f} "
      f"(bugun 10.0*0.70 = 7.00),  FpsPenalty/halving = {f:.4f} (bugun 5.0*0.70 = 3.50)")
for name, pts, fitted, today in (("olcek", fS + cS, s, 7.00), ("fps", fF + cF, f, 3.50)):
    print(f"  {name} artiklari:")
    for tag, x, y in pts:
        print(f"    {tag}  sekil {x:.4f}  gereken {y:7.3f}  uydurulan {fitted*x:7.3f}  "
              f"artik {y - fitted*x:+7.3f}")

def yeni_skor_tablosu(data, sMul, fMul, k=800):
    print()
    print(f"=== YENI MODEL (olcek-bagimsiz rate, ScalePenalty carpani {sMul:.4f}, Fps/halving {fMul:.4f})")
    for tag, (d, rows) in data.items():
        sw, sh, sf = SRC[tag]
        rate1 = {}
        for r, m in rows:
            if r["w"] == sw and r["k"] == k:
                rate1[r["fps"]] = r["rate"]
        pairs = []
        print(f"## {tag}")
        print("yerlesim\teski skor\tyeni skor\tolculen")
        for r, m in rows:
            if r["k"] != k or r["fps"] not in rate1:
                continue
            sc = r["h"] / sh
            new = rate1[r["fps"]] - sMul * shape_scale(sc) - fMul * math.log2(sf / r["fps"])
            pairs.append((r, m, new))
            print(f"{r['w']}x{r['h']}@{r['fps']:g}\t{r['skor']:.2f}\t{new:.2f}\t{m['mean']:.3f}")
        old = [p[0]["skor"] for p in pairs]
        new = [p[2] for p in pairs]
        mea = [p[1]["mean"] for p in pairs]
        def inv(model):
            c = 0
            bad = []
            for i in range(len(pairs)):
                for j in range(i + 1, len(pairs)):
                    if (model[i] - model[j]) * (mea[i] - mea[j]) < 0:
                        c += 1
                        a, b = pairs[i][0], pairs[j][0]
                        bad.append(f"{a['w']}x{a['h']}@{a['fps']:g} vs {b['w']}x{b['h']}@{b['fps']:g} "
                                   f"(model {model[i] - model[j]:+.2f}, olculen {mea[i] - mea[j]:+.3f})")
            return c, bad
        oc, _ = inv(old)
        nc, nb = inv(new)
        tot = len(pairs) * (len(pairs) - 1) // 2
        print(f"   Spearman eski {spearman(old, mea):+.3f} -> yeni {spearman(new, mea):+.3f}")
        print(f"   ters cift eski {oc}/{tot} -> yeni {nc}/{tot}")
        for b in nb:
            print(f"     - {b}")
        print()

yeni_skor_tablosu(ab, s, f)
yeni_skor_tablosu(c, s, f)

def sadece_ceza_uydur(datasets, k=800):
    ptsS, ptsF = [], []
    for data in datasets:
        for tag, (d, rows) in data.items():
            if tag == "C":
                continue
            sw, sh, sf = SRC[tag]
            base = [(r, m) for r, m in rows if r["w"] == sw and r["fps"] == sf and r["k"] == k]
            if not base:
                continue
            rb, mb = base[0]
            for r, m in rows:
                if r["k"] != k:
                    continue
                if r["fps"] == sf and r["w"] != sw:
                    ptsS.append((shape_scale(r["h"] / sh), (r["rate"] - rb["rate"]) - (m["mean"] - mb["mean"])))
                if r["w"] == sw and r["fps"] != sf:
                    ptsF.append((math.log2(sf / r["fps"]), (r["rate"] - rb["rate"]) - (m["mean"] - mb["mean"])))
    return lsq_through_origin(ptsS), lsq_through_origin(ptsF)

def eski_rate_yeni_ceza(data, sMul, fMul, k=800):
    for tag, (d, rows) in data.items():
        sw, sh, sf = SRC[tag]
        base = [(r, m) for r, m in rows if r["w"] == sw and r["fps"] == sf and r["k"] == k]
        if not base:
            continue
        pairs = []
        for r, m in rows:
            if r["k"] != k:
                continue
            sc = r["h"] / sh
            new = r["rate"] - sMul * shape_scale(sc) - fMul * math.log2(sf / r["fps"])
            pairs.append((r, m, new))
        mo = [p[2] for p in pairs]
        me = [p[1]["mean"] for p in pairs]
        c = sum(1 for i in range(len(pairs)) for j in range(i + 1, len(pairs))
                if (mo[i] - mo[j]) * (me[i] - me[j]) < 0)
        tot = len(pairs) * (len(pairs) - 1) // 2
        print(f"  {tag}: Spearman {spearman(mo, me):+.3f}, ters cift {c}/{tot}")

sS, sF = sadece_ceza_uydur([ab, c])
print()
print(f"=== (a) ESKI rate + yalniz cezalar A+B'ye uydurulmus "
      f"(ScalePenalty carpani {sS:.4f}, Fps/halving {sF:.4f})")
eski_rate_yeni_ceza(ab, sS, sF)
eski_rate_yeni_ceza(c, sS, sF)

def yeni_rate_eski_ceza(data, k=800):
    for tag, (d, rows) in data.items():
        sw, sh, sf = SRC[tag]
        rate1 = {r["fps"]: r["rate"] for r, m in rows if r["w"] == sw and r["k"] == k}
        pairs = []
        for r, m in rows:
            if r["k"] != k or r["fps"] not in rate1:
                continue
            new = rate1[r["fps"]] - 7.0 * shape_scale(r["h"] / sh) - 3.5 * math.log2(sf / r["fps"])
            pairs.append((r, m, new))
        mo = [p[2] for p in pairs]
        me = [p[1]["mean"] for p in pairs]
        cnt = sum(1 for i in range(len(pairs)) for j in range(i + 1, len(pairs))
                  if (mo[i] - mo[j]) * (me[i] - me[j]) < 0)
        tot = len(pairs) * (len(pairs) - 1) // 2
        print(f"  {tag}: Spearman {spearman(mo, me):+.3f}, ters cift {cnt}/{tot}")

print()
print("=== (b) YENI rate (olcek-bagimsiz) + ESKI cezalar (7.00 / 3.50, dokunulmadi)")
yeni_rate_eski_ceza(ab)
yeni_rate_eski_ceza(c)

def varyant(data, sMul, fMul, k=800, goster=False):
    res = {}
    for tag, (d, rows) in data.items():
        sw, sh, sf = SRC[tag]
        rate1 = {r["fps"]: r["rate"] for r, m in rows if r["w"] == sw and r["k"] == k}
        pairs = []
        for r, m in rows:
            if r["k"] != k or r["fps"] not in rate1:
                continue
            new = rate1[r["fps"]] - sMul * shape_scale(r["h"] / sh) - fMul * math.log2(sf / r["fps"])
            pairs.append((r, m, new))
        mo = [p[2] for p in pairs]
        me = [p[1]["mean"] for p in pairs]
        cnt = sum(1 for i in range(len(pairs)) for j in range(i + 1, len(pairs))
                  if (mo[i] - mo[j]) * (me[i] - me[j]) < 0)
        tot = len(pairs) * (len(pairs) - 1) // 2
        res[tag] = (spearman(mo, me), cnt, tot)
        if goster:
            for r, m, n in sorted(pairs, key=lambda p: -p[2])[:4]:
                print(f"      {tag} en yuksek: {r['w']}x{r['h']}@{r['fps']:g} yeni skor {n:.2f} olculen {m['mean']:.3f}")
    return res

print()
print("=== varyant taramasi (yeni olcek-bagimsiz rate)")
for name, sM, fM in (("cezalar dokunulmadi", 7.00, 3.50),
                     ("yalniz fps A+B'ye uyduruldu", 7.00, 5.6809),
                     ("yalniz olcek A+B'ye uyduruldu", 8.2477, 3.50),
                     ("ikisi de A+B'ye uyduruldu", 8.2477, 5.6809)):
    r = {**varyant(ab, sM, fM), **varyant(c, sM, fM)}
    line = "  ".join(f"{t}: rho {r[t][0]:+.3f} ters {r[t][1]}/{r[t][2]}" for t in ("A", "B", "C"))
    print(f"  {name:32s} olcek {sM:.4f} fps {fM:.4f}  |  {line}")

print()
print("=== K5: yeni modelin A kaynaginda en yuksek dort yerlesimi (cezalar dokunulmadi)")
varyant(ab, 7.00, 3.50, goster=True)
