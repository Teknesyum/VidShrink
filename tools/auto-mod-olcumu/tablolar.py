# -*- coding: utf-8 -*-
import json, os, io, sys

base = os.path.dirname(os.path.abspath(__file__))
SURE = 60.442

def vmaf(ad):
    p = os.path.join(base, "vmaf", ad + ".json")
    if not os.path.exists(p):
        return None
    s = []
    for f in json.load(open(p))["frames"]:
        m = f["metrics"]
        v = m.get("vmaf", m.get("vmaf_neg"))
        if v is not None:
            s.append(v)
    if not s:
        return None
    srt = sorted(s)
    r = 0.10 * (len(srt) - 1)
    lo, hi = int(r // 1), int(-(-r // 1))
    p10 = srt[lo] if lo == hi else srt[lo] + (srt[hi] - srt[lo]) * (r - lo)
    harm = len(s) / sum(1.0 / max(x, 1.0) for x in s)
    sifir = sum(1 for x in s if x < 1.0)
    return {"ort": sum(s) / len(s), "harm": harm, "p10": p10, "n": len(s),
            "sifir": sifir, "min": min(s)}

def boyut(yol):
    t = os.path.join(base, yol)
    return os.path.getsize(t) if os.path.exists(t) else None

def sat(yol, ad):
    b = boyut(yol)
    v = vmaf(ad)
    if b is None or v is None:
        return None
    v["bayt"] = b
    v["mib"] = b / 1048576.0
    v["kbps"] = b * 8 / SURE / 1000
    return v

def tr(x, d=3):
    return ("%%.%df" % d % x).replace(".", ",")

def fark(x, d=3):
    s = ("%%+.%df" % d % x).replace(".", ",")
    return s

kaynak = {
    "auto":        ("gui/parca-2_shrunk.mp4", "auto"),
    "e1":          ("ciktilar/e1-preset4.mp4", "e1-preset4"),
    "e2":          ("ciktilar/e2-gop300.mp4", "e2-gop300"),
    "e3":          ("ciktilar/e3-olcek810.mp4", "e3-olcek810"),
    "biz2975":     ("ciktilar/uzman-biz-2975.mp4", "uzman-biz-2975"),
    "biz2775":     ("ciktilar/uzman-biz-2775.mp4", "uzman-biz-2775"),
    "biz3":        ("ciktilar/uzman-biz3.mp4", "uzman-biz3"),
    "hb":          ("ciktilar/uzman-hb.mp4", "uzman-hb"),
    "hb2":         ("ciktilar/uzman-hb2.mp4", "uzman-hb2"),
}
d = {}
for k, (yol, ad) in kaynak.items():
    d[k] = sat(yol, ad)

auto = d["auto"]
if auto is None:
    sys.exit("auto olculmedi")

# boyutu auto'ya en yakin uzman-biz ve uzman-hb varyanti
def enyakin(anahtarlar):
    aday = [(abs(d[k]["bayt"] - auto["bayt"]), k) for k in anahtarlar if d.get(k)]
    return sorted(aday)[0][1] if aday else None

kbiz = enyakin(["biz2975", "biz2775", "biz3"])
khb = enyakin(["hb", "hb2"])

out = []
w = out.append

w(u"| satır | boyut | ortalama | p10 | harmonik | sıfır puanlı kare | en düşük kare |")
w("|---|---|---|---|---|---|---|")
def k3sat(etiket, k):
    if not d.get(k):
        w(u"| %s | ölçülmedi | olculmedi | olculmedi | olculmedi | olculmedi | olculmedi |" % etiket)
        return
    v = d[k]
    w("| %s | %s MiB (%d bayt) | %s | %s | %s | %d | %s |" % (
        etiket, tr(v["mib"], 2), v["bayt"], tr(v["ort"]), tr(v["p10"]), tr(v["harm"]), v["sifir"], tr(v["min"], 2)))
k3sat("auto", "auto")
k3sat("uzman-biz", kbiz)
k3sat("uzman-handbrake", khb)
w("")
if d.get(kbiz):
    v = d[kbiz]
    w(u"**Uzman açığı = uzman-biz - auto:** ortalama %s, harmonik %s, p10 %s" % (
        fark(v["ort"] - auto["ort"]), fark(v["harm"] - auto["harm"]), fark(v["p10"] - auto["p10"])))
    w("")
    w(u"Boyut farkı: uzman-biz %s MiB, auto %s MiB (%s%%)." % (
        tr(v["mib"], 2), tr(auto["mib"], 2), fark((v["mib"] / auto["mib"] - 1) * 100, 1)))
if d.get(khb):
    v = d[khb]
    w("")
    w("**HandBrake - auto:** ortalama %s, harmonik %s, p10 %s; boyut %s MiB (%s%%)." % (
        fark(v["ort"] - auto["ort"]), fark(v["harm"] - auto["harm"]), fark(v["p10"] - auto["p10"]),
        tr(v["mib"], 2), fark((v["mib"] / auto["mib"] - 1) * 100, 1)))

w("")
w("---K4---")
w("")
w(u"| değiştirilen tek ayar | auto değeri | uzman değeri | boyut | Δ ortalama | Δ p10 |")
w("|---|---|---|---|---|---|")
abl = [
    (u"kodlayıcı çabası (preset)", "6", "4", "e1"),
    (u"anahtar kare aralığı (-g)", u"120 (fps × 2)", "300", "e2"),
    (u"çözünürlük", "1920x1080", "1440x810", "e3"),
]
for etiket, a, u, k in abl:
    if not d.get(k):
        w("| %s | %s | %s | ölçülmedi | ölçülmedi | ölçülmedi |" % (etiket, a, u))
        continue
    v = d[k]
    w("| %s | %s | %s | %s MiB (%s%%) | %s | %s |" % (
        etiket, a, u, tr(v["mib"], 2), fark((v["mib"] / auto["mib"] - 1) * 100, 1),
        fark(v["ort"] - auto["ort"]), fark(v["p10"] - auto["p10"])))

io.open(os.path.join(base, "uretilen.md"), "w", encoding="utf-8", newline="\n").write(u"\n".join(out) + u"\n")
print("uretildi: %d satir" % len(out))
