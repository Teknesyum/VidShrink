# -*- coding: utf-8 -*-
import json, gzip, os, io, sys

base = os.path.dirname(os.path.abspath(__file__))
kok = os.path.abspath(os.path.join(base, "..", ".."))
yeni = os.path.join(kok, ".calisma", "t111")
SURE = 60.442

KOSUMLAR = [
    ("auto",                "gui/parca-2_shrunk.mp4"),
    ("auto-olceksiz",       "gui/parca-2_shrunk.mp4"),
    ("e1-preset4",          "ciktilar/e1-preset4.mp4"),
    ("e2-gop300",           "ciktilar/e2-gop300.mp4"),
    ("e3-olcek810",         "ciktilar/e3-olcek810.mp4"),
    ("uzman-biz3",          "ciktilar/uzman-biz3.mp4"),
    ("uzman-hb",            "ciktilar/uzman-hb.mp4"),
    ("uzman-hb2",           "ciktilar/uzman-hb2.mp4"),
    ("y1-g300-izgara",      "ciktilar/y1-g300-izgara.mp4"),
    ("y2-g300-hizali",      "ciktilar/y2-g300-hizali.mp4"),
    ("y3-hizali-boyutesit", "ciktilar/y3-hizali-boyutesit.mp4"),
    ("uzman-biz4",          "ciktilar/uzman-biz4.mp4"),
    ("uzman-biz5",          "ciktilar/uzman-biz5.mp4"),
    ("uzman-biz6",          "ciktilar/uzman-biz6.mp4"),
    ("uzman-biz7",          "ciktilar/uzman-biz7.mp4"),
    ("uzman-hb3",           "ciktilar/uzman-hb3.mp4"),
]


def istatistik(kareler):
    s = []
    for f in kareler:
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
    return {"ort": sum(s) / len(s), "harm": harm, "p10": p10, "n": len(s),
            "alti": sum(1 for x in s if x < 1.0), "tamsifir": sum(1 for x in s if x == 0.0),
            "min": min(s),
            "altikume": frozenset(i for i, x in enumerate(s) if x < 1.0)}


def arsiv(ad):
    p = os.path.join(base, "vmaf", ad + ".json.gz")
    if not os.path.exists(p):
        return None
    with gzip.open(p, "rt", encoding="utf-8") as fh:
        return istatistik(json.load(fh)["frames"])


def taze(ad, kilit):
    p = os.path.join(yeni, "vmaf", "%s-%s.json" % (ad, kilit))
    if not os.path.exists(p):
        return None
    with io.open(p, encoding="utf-8") as fh:
        return istatistik(json.load(fh)["frames"])


def bayt(yol):
    p = os.path.join(yeni, yol)
    d, ad = os.path.split(p)
    yaziliyor = os.path.join(d, "." + os.path.splitext(ad)[0] + ".yaziliyor.mp4")
    if os.path.exists(yaziliyor):
        raise SystemExit("YAZILIYOR: %s hala uretiliyor, tablo uretilmedi" % ad)
    return os.path.getsize(p) if os.path.exists(p) else None


def tr(x, d=3):
    if x is None:
        return u"ölçülmedi"
    return ("%%.%df" % d % x).replace(".", ",")


def fk(x, d=3):
    if x is None:
        return u"ölçülmedi"
    return ("%%+.%df" % d % x).replace(".", ",")


D = {}
for ad, yol in KOSUMLAR:
    D[ad] = {"yol": yol, "bayt": bayt(yol), "arsiv": arsiv(ad),
             "kilitsiz": taze(ad, "kilitsiz"), "kilitli": taze(ad, "kilitli")}

out = []
w = out.append

w(u"### A. Koşum başına üç ölçüm")
w(u"")
w(u"| koşum | ölçüm | kare | ortalama | p10 | harmonik | `<1` kare | tam 0 | en düşük |")
w(u"|---|---|---|---|---|---|---|---|---|")
for ad, _ in KOSUMLAR:
    d = D[ad]
    for etiket, anahtar in ((u"T102 arşivi (kilitsiz)", "arsiv"),
                            (u"T111 yeni (kilitsiz)", "kilitsiz"),
                            (u"T111 yeni (**kilitli**)", "kilitli")):
        v = d[anahtar]
        if v is None:
            w(u"| %s | %s | ölçülmedi | | | | | | |" % (ad, etiket))
            continue
        w(u"| %s | %s | %d | %s | %s | %s | %d | %d | %s |" % (
            ad, etiket, v["n"], tr(v["ort"]), tr(v["p10"]), tr(v["harm"]),
            v["alti"], v["tamsifir"], tr(v["min"], 3)))
w(u"")

w(u"### B. Kilidin tek başına etkisi (aynı dosya, iki ölçüm)")
w(u"")
w(u"| koşum | Δ ortalama | Δ p10 | Δ harmonik | `<1` kare kilitsiz → kilitli | eşlenen kare kilitsiz → kilitli | min kilitsiz → kilitli |")
w(u"|---|---|---|---|---|---|---|")
for ad, _ in KOSUMLAR:
    a, b = D[ad]["kilitsiz"], D[ad]["kilitli"]
    if not a or not b:
        w(u"| %s | ölçülmedi | | | | | |" % ad)
        continue
    w(u"| %s | %s | %s | %s | %d → %d | %d → %d | %s → %s |" % (
        ad, fk(b["ort"] - a["ort"]), fk(b["p10"] - a["p10"]), fk(b["harm"] - a["harm"]),
        a["alti"], b["alti"], a["n"], b["n"], tr(a["min"], 3), tr(b["min"], 3)))
w(u"")

w(u"### C. Yeniden kodlamanın payı (T102 arşivi ↔ T111 kilitsiz, ikisi de kilitsiz)")
w(u"")
w(u"| koşum | Δ ortalama | Δ p10 | Δ harmonik | `<1` kare | tam 0 |")
w(u"|---|---|---|---|---|---|")
for ad, _ in KOSUMLAR:
    a, b = D[ad]["arsiv"], D[ad]["kilitsiz"]
    if not a or not b:
        w(u"| %s | ölçülmedi | | | | |" % ad)
        continue
    w(u"| %s | %s | %s | %s | %d → %d | %d → %d |" % (
        ad, fk(b["ort"] - a["ort"]), fk(b["p10"] - a["p10"]), fk(b["harm"] - a["harm"]),
        a["alti"], b["alti"], a["tamsifir"], b["tamsifir"]))
w(u"")

w(u"### D. Boyut: T102 ↔ T111")
w(u"")
T102BAYT = {"auto": 15766933, "uzman-biz3": 15752039, "uzman-hb2": 15754005,
            "e2-gop300": 11903000, "y1-g300-izgara": 11809579,
            "y2-g300-hizali": 11160196, "y3-hizali-boyutesit": 11973383}
w(u"| koşum | T102 bayt | T111 bayt | fark |")
w(u"|---|---|---|---|")
for ad, _ in KOSUMLAR:
    b = D[ad]["bayt"]
    o = T102BAYT.get(ad)
    if b is None:
        w(u"| %s | %s | ölçülmedi | |" % (ad, o if o else u"—"))
        continue
    if o:
        w(u"| %s | %d | %d | %s%% |" % (ad, o, b, fk((b / float(o) - 1) * 100, 2)))
    else:
        w(u"| %s | belgede yok | %d | — |" % (ad, b))
w(u"")

av1 = [a for a, _ in KOSUMLAR if not a.startswith("uzman-hb")]
hb = [a for a, _ in KOSUMLAR if a.startswith("uzman-hb")]

for etiket, anahtar in ((u"T102 arşivi (kilitsiz)", "arsiv"),
                        (u"T111 yeni (kilitsiz)", "kilitsiz"),
                        (u"T111 yeni (kilitli)", "kilitli")):
    w(u"### E. `<1` kare kümesi — %s" % etiket)
    w(u"")
    kumeler = {a: D[a][anahtar]["altikume"] for a in av1 if D[a][anahtar]}
    if not kumeler:
        w(u"ölçülmedi")
        w(u"")
        continue
    ilk = list(kumeler.values())[0]
    ayni = all(k == ilk for k in kumeler.values())
    w(u"AV1 koşumu sayısı **%d**; kümeler birebir aynı mı → **%s**; ortak küme boyu **%d**." % (
        len(kumeler), u"evet" if ayni else u"hayır", len(ilk)))
    if not ayni:
        birlesim = set()
        kesisim = set(ilk)
        for k in kumeler.values():
            birlesim |= set(k)
            kesisim &= set(k)
        w(u"Birleşim %d, kesişim %d kare." % (len(birlesim), len(kesisim)))
    say25 = sum(1 for a in av1 if D[a][anahtar] and D[a][anahtar]["tamsifir"] == 25)
    say24 = sum(1 for a in av1 if D[a][anahtar] and D[a][anahtar]["tamsifir"] == 24)
    w(u"Tam sıfır sayısı: **%d** koşumda 25, **%d** koşumda 24." % (say25, say24))
    for a in av1:
        v = D[a][anahtar]
        if v:
            w(u"- `%s`: `<1` %d, tam 0 %d" % (a, v["alti"], v["tamsifir"]))
    for a in hb:
        v = D[a][anahtar]
        if v:
            w(u"- `%s` (x265): `<1` %d, tam 0 %d, min %s" % (a, v["alti"], v["tamsifir"], tr(v["min"], 3)))
    w(u"")


CIFTLER = [(u"uzman açığı", "uzman-biz3", "auto"),
           (u"HandBrake açığı (boyut eşli)", "uzman-hb2", "auto"),
           (u"HandBrake açığı (boyut eşsiz)", "uzman-hb", "auto"),
           (u"HandBrake açığı (T111 boyut eşli)", "uzman-hb3", "auto"),
           (u"uzman açığı (T111 boyut eşli)", "uzman-biz5", "auto"),
           (u"uzman açığı (T111 boyut eşli, biz6)", "uzman-biz6", "auto"),
           (u"uzman açığı (T111 boyut eşli, biz7)", "uzman-biz7", "auto")]

w(u"### F. Açıklar — eski fark / yeni fark")
w(u"")
w(u"| açık | ölçüm | Δ ortalama | Δ p10 | Δ harmonik |")
w(u"|---|---|---|---|---|")
for etiket, ust, alt in CIFTLER:
    for ad, anahtar in ((u"T102 arşivi (kilitsiz)", "arsiv"),
                        (u"T111 yeni (kilitsiz)", "kilitsiz"),
                        (u"T111 yeni (**kilitli**)", "kilitli")):
        u_, a_ = D[ust][anahtar], D[alt][anahtar]
        if not u_ or not a_:
            w(u"| %s | %s | ölçülmedi | | |" % (etiket, ad))
            continue
        w(u"| %s | %s | %s | %s | %s |" % (etiket, ad, fk(u_["ort"] - a_["ort"]),
                                           fk(u_["p10"] - a_["p10"]), fk(u_["harm"] - a_["harm"])))
w(u"")
w(u"Boyut farkları (T111 yeniden kodlaması, üst / alt):")
for etiket, ust, alt in CIFTLER:
    bu, ba = D[ust]["bayt"], D[alt]["bayt"]
    if bu and ba:
        w(u"- %s: %d / %d bayt — **%s%%**" % (etiket, bu, ba, fk((bu / float(ba) - 1) * 100, 2)))
    else:
        w(u"- %s: ölçülmedi" % etiket)
w(u"")

io.open(os.path.join(yeni, "uretilen.md"), "w", encoding="utf-8", newline="\n").write(u"\n".join(out) + u"\n")
sys.stdout.write("uretildi: %d satir -> %s\n" % (len(out), os.path.join(yeni, "uretilen.md")))
