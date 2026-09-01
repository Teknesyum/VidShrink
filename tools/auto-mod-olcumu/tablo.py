import json, os

base = os.path.dirname(os.path.abspath(__file__))
dosyalar = {
    "auto": "gui/parca-2_shrunk.mp4",
    "e1-preset4": "ciktilar/e1-preset4.mp4",
    "e2-gop300": "ciktilar/e2-gop300.mp4",
    "e3-olcek810": "ciktilar/e3-olcek810.mp4",
    "uzman-biz-2975": "ciktilar/uzman-biz-2975.mp4",
    "uzman-biz-2775": "ciktilar/uzman-biz-2775.mp4",
    "uzman-biz3": "ciktilar/uzman-biz3.mp4",
    "uzman-hb": "ciktilar/uzman-hb.mp4",
    "uzman-hb2": "ciktilar/uzman-hb2.mp4",
}

def olc(ad):
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
    harm2 = 0.0
    kalan = [x for x in s if x >= 1.0]
    if kalan:
        harm2 = len(kalan) / sum(1.0 / x for x in kalan)
    return sum(s) / len(s), harm, p10, len(s), sifir, harm2

print("%-13s | %9s | %6s | %7s | %6s | %6s | %6s | %5s | %6s | kare" % ("ad", "bayt", "MiB", "kbit/s", "ort", "harm", "p10", "sifir", "harm*"))
for ad, yol in dosyalar.items():
    tam = os.path.join(base, yol)
    if not os.path.exists(tam):
        print("%-13s | olculmedi" % ad)
        continue
    b = os.path.getsize(tam)
    r = olc(ad)
    if r is None:
        print("%-13s | %9d | %6.2f | %7s | olculmedi" % (ad, b, b / 1048576, "-"))
        continue
    ort, harm, p10, n, sifir, harm2 = r
    kbps = b * 8 / 60.442 / 1000
    print("%-13s | %9d | %6.2f | %7.1f | %6.3f | %6.3f | %6.3f | %5d | %6.3f | %d" % (ad, b, b / 1048576, kbps, ort, harm, p10, sifir, harm2, n))
