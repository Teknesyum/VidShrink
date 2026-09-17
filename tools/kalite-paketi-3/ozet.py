import json
import math
import sys
from pathlib import Path

PER_HALVING = 6.0
SCALE_K = 10.0
SCALE_EXP = 1.1
FPS_K = 5.0
LOWFPS_SURCHARGE = 12.0
LOWFPS_THRESHOLD = 20.0


def yukle(yol):
    return json.loads(Path(yol).read_text(encoding="utf-8-sig"))


def f(x, n=2):
    return "—" if x is None else f"{x:.{n}f}".replace(".", ",")


def whatsapp(kok):
    satirlar = yukle(Path(kok) / "whatsapp.json")
    print("| Kesit | kbit | Kol | kbps | VMAF-NEG ort | VMAF-NEG harm | XPSNR | Karanlık PSNR | Ton oranı | Kayma |")
    print("|---|---|---|---|---|---|---|---|---|---|")
    for s in satirlar:
        print(f"| {s['kesit']} | {s['istenen_kbit']} | {s['kol']} | {f(s['kbps'],1)} | {f(s['vmafneg_ort'])} | {f(s['vmafneg_harm'])} | {f(s['xpsnr'])} | {f(s['karanlik_psnr'])} | {f(s['karanlik_ton'])} | {f(s['karanlik_kayma'])} |")
    print()
    print("| Kesit | kbit | Kol | Δ VMAF-NEG ort | Δ XPSNR | Δ karanlık PSNR | Δ kayma |")
    print("|---|---|---|---|---|---|---|")
    for s in satirlar:
        if s["kol"] in ("urun",):
            continue
        u = next(x for x in satirlar if x["kesit"] == s["kesit"] and x["istenen_kbit"] == s["istenen_kbit"] and x["kol"] == "urun")
        print(f"| {s['kesit']} | {s['istenen_kbit']} | {s['kol']} | {f(s['vmafneg_ort']-u['vmafneg_ort'])} | {f(s['xpsnr']-u['xpsnr'])} | {f(s['karanlik_psnr']-u['karanlik_psnr'])} | {f(s['karanlik_kayma']-u['karanlik_kayma'])} |")


def model(olcek, fps, kaynak_fps, kbps_oran):
    ceza_olcek = 0.0 if olcek >= 0.999 else SCALE_K * math.pow(1 / olcek - 1, SCALE_EXP)
    ceza_fps = 0.0
    if fps < kaynak_fps - 0.01:
        ceza_fps = FPS_K * math.log2(kaynak_fps / fps)
        if fps < LOWFPS_THRESHOLD:
            ceza_fps += LOWFPS_SURCHARGE * (LOWFPS_THRESHOLD - fps) / 8
    kazanc = PER_HALVING * math.log2(kbps_oran / (olcek * olcek * fps / kaynak_fps))
    return kazanc - ceza_olcek - ceza_fps, ceza_olcek, ceza_fps


def ceza(kok, kesitler):
    print("| Kesit | kbit | Geometri | kbps | VMAF-NEG ort | XPSNR | Ölçülen Δ | Model Δ | Model ölçek bedeli | Model fps bedeli | Sapma (ölçülen − model) |")
    print("|---|---|---|---|---|---|---|---|---|---|---|")
    ozet = {}
    for k in kesitler:
        satirlar = yukle(Path(kok) / f"sonuc-ceza-{k}" / f"ceza-{k}.json")
        taban = {s["istenen_kbit"]: s for s in satirlar if s["olcek"] == 1.0 and s["fps"] == 24}
        for s in satirlar:
            t = taban[s["istenen_kbit"]]
            olculen = s["vmafneg_ort"] - t["vmafneg_ort"]
            m, co, cf = model(s["olcek"], s["fps"], 24, s["kbps"] / t["kbps"])
            print(f"| {k} | {s['istenen_kbit']} | {s['kol']} | {f(s['kbps'],1)} | {f(s['vmafneg_ort'])} | {f(s['xpsnr'])} | {f(olculen)} | {f(m)} | {f(co)} | {f(cf)} | {f(olculen-m)} |")
            if s is t:
                continue
            anahtar = ("olcek" if s["fps"] == 24 else ("fps" if s["olcek"] == 1.0 else "ikisi"))
            ozet.setdefault((k, anahtar), []).append(olculen - m)
    print()
    print("| Kesit | Değişen | Satır | Ortalama sapma (VMAF-NEG puanı) |")
    print("|---|---|---|---|")
    for (k, a), v in sorted(ozet.items()):
        print(f"| {k} | {a} | {len(v)} | {f(sum(v)/len(v))} |")


def handbrake(kok, kesitler):
    print("| Kesit | kbit | Kol | Kodlayıcı | Geometri | kbps | MB | VMAF-NEG ort | VMAF-NEG harm | XPSNR | Karanlık PSNR |")
    print("|---|---|---|---|---|---|---|---|---|---|---|")
    farklar = []
    for k in kesitler:
        satirlar = yukle(Path(kok) / k / f"handbrake-{k}.json")
        for s in satirlar:
            print(f"| {k} | {s['istenen_kbit']} | {s['kol']} | {s.get('kodlayici','')} | {s.get('geometri','')} | {f(s['kbps'],1)} | {f(s['mb'],3)} | {f(s['vmafneg_ort'])} | {f(s['vmafneg_harm'])} | {f(s['xpsnr'])} | {f(s['karanlik_psnr'])} |")
        for kbit in sorted({s["istenen_kbit"] for s in satirlar}):
            hb = next(s for s in satirlar if s["istenen_kbit"] == kbit and s["kol"] == "handbrake")
            for kol in ("urun-otomatik", "urun-x265", "urun-svtav1"):
                u = next(s for s in satirlar if s["istenen_kbit"] == kbit and s["kol"] == kol)
                farklar.append((k, kbit, kol, u["kbps"] - hb["kbps"], u["vmafneg_ort"] - hb["vmafneg_ort"], u["xpsnr"] - hb["xpsnr"], u["karanlik_psnr"] - hb["karanlik_psnr"]))
            n = next(s for s in satirlar if s["istenen_kbit"] == kbit and s["kol"].startswith("negatif"))
            farklar.append((k, kbit, "negatif (HB yarım bit − HB)", n["kbps"] - hb["kbps"], n["vmafneg_ort"] - hb["vmafneg_ort"], n["xpsnr"] - hb["xpsnr"], n["karanlik_psnr"] - hb["karanlik_psnr"]))
    print()
    print("| Kesit | kbit | Kol − HandBrake | Δ kbps | Δ VMAF-NEG ort | Δ XPSNR | Δ karanlık PSNR |")
    print("|---|---|---|---|---|---|---|")
    for k, kbit, kol, dk, dv, dx, dp in farklar:
        print(f"| {k} | {kbit} | {kol} | {f(dk,1)} | {f(dv)} | {f(dx)} | {f(dp)} |")


ANAHTAR = ("kesit", "preset", "kontrol", "deger", "filmgrain", "tune", "keyint", "ek", "pixfmt")


def av1(kok, kesitler):
    tum = []
    for k in kesitler:
        tum += yukle(Path(kok) / k / f"av1-{k}.json")
    for s in tum:
        s.setdefault("ek", "")
        s.setdefault("pixfmt", "yuv420p10le")
        s.setdefault("sha256", None)
    ekler = []
    for s in tum:
        if s["ek"] not in ekler:
            ekler.append(s["ek"])
    sirali = sorted(tum, key=lambda s: (kesitler.index(s["kesit"]), s["preset"], s["kontrol"], s["deger"], s["filmgrain"], s["tune"], s["keyint"], ekler.index(s["ek"]), s["pixfmt"]))
    if len(ekler) > 1:
        print("| Ek | svtav1-params eki |")
        print("|---|---|")
        for i, e in enumerate(ekler):
            print(f"| e{i} | `{e or '(yok)'}` |")
        print()
    print("| Kesit | Preset | Kontrol | Değer | Film-grain | Tune | Keyint | Ek | Piksel | kbps | VMAF-NEG ort | VMAF-NEG harm | VMAF-NEG p10 | XPSNR | Karanlık PSNR | Kodlama sn |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for s in sirali:
        print(f"| {s['kesit']} | {s['preset']} | {s['kontrol']} | {s['deger']} | {s['filmgrain']} | {s['tune']} | {s['keyint']} | e{ekler.index(s['ek'])} | {s['pixfmt']} | {f(s['kbps'],1)} | {f(s['vmafneg_ort'])} | {f(s['vmafneg_harm'])} | {f(s['vmafneg_p10'])} | {f(s['xpsnr'])} | {f(s['karanlik_psnr'])} | {f(s['kodlama_sn'],1)} |")

    def eslesik(alan, a, b):
        sonuc = {}
        for s in tum:
            if s[alan] != b:
                continue
            anahtar = {x: s[x] for x in ANAHTAR}
            anahtar[alan] = a
            t = next((x for x in tum if all(x[y] == v for y, v in anahtar.items())), None)
            if t is None:
                continue
            ayni = s["sha256"] is not None and s["sha256"] == t["sha256"]
            sonuc.setdefault(s["kesit"], []).append((s["kbps"] / t["kbps"] - 1, s["vmafneg_ort"] - t["vmafneg_ort"], s["xpsnr"] - t["xpsnr"], s["karanlik_psnr"] - t["karanlik_psnr"], s["kodlama_sn"] / t["kodlama_sn"] - 1, ayni))
        return sonuc

    def aralik(v, n=2, yuzde=False):
        c = 100 if yuzde else 1
        return f"{f(sum(v)/len(v)*c, n)} ({f(min(v)*c, n)} … {f(max(v)*c, n)})"

    degisimler = [("tune 0 → 1", "tune", 0, 1), ("film-grain 0 → 8", "filmgrain", 0, 8), ("yuv420p → yuv420p10le", "pixfmt", "yuv420p", "yuv420p10le")]
    degisimler += [(f"e0 → e{i}", "ek", ekler[0], e) for i, e in enumerate(ekler) if i > 0]
    print()
    print("| Kesit | Değişim | Çift | Aynı çıktı (sha256) | Δ kbps % | Δ VMAF-NEG ort | Δ XPSNR | Δ karanlık PSNR | Δ kodlama sn % |")
    print("|---|---|---|---|---|---|---|---|---|")
    for etiket, alan, a, b in degisimler:
        for k, v in eslesik(alan, a, b).items():
            sha = sum(1 for x in v if x[5])
            print(f"| {k} | {etiket} | {len(v)} | {sha} | {aralik([x[0] for x in v], 1, True)} | {aralik([x[1] for x in v])} | {aralik([x[2] for x in v])} | {aralik([x[3] for x in v])} | {aralik([x[4] for x in v], 1, True)} |")

    egriler = {}
    for s in tum:
        if s["kontrol"] != "crf":
            continue
        egriler.setdefault((s["kesit"], s["preset"], s["filmgrain"], s["tune"], s["keyint"], s["ek"], s["pixfmt"]), []).append(s)
    if not egriler:
        return
    print()
    print("| Kesit | Ortak kbps | Preset | Film-grain | Tune | VMAF-NEG ort | XPSNR | Karanlık PSNR | Ort. kodlama sn |")
    print("|---|---|---|---|---|---|---|---|---|")
    for k in kesitler:
        benim = {a: sorted(v, key=lambda s: s["kbps"]) for a, v in egriler.items() if a[0] == k and len(v) >= 2}
        if not benim:
            continue
        alt = max(v[0]["kbps"] for v in benim.values())
        ust = min(v[-1]["kbps"] for v in benim.values())
        if alt >= ust:
            continue
        hedef = math.sqrt(alt * ust)
        for a, v in sorted(benim.items(), key=lambda x: (x[0][1], x[0][2], x[0][3])):
            def ara(alan):
                for s0, s1 in zip(v, v[1:]):
                    if s0["kbps"] <= hedef <= s1["kbps"]:
                        w = (math.log(hedef) - math.log(s0["kbps"])) / (math.log(s1["kbps"]) - math.log(s0["kbps"]))
                        return s0[alan] + w * (s1[alan] - s0[alan])
                return None
            sure = sum(s["kodlama_sn"] for s in v) / len(v)
            print(f"| {k} | {f(hedef,1)} | {a[1]} | {a[2]} | {a[3]} | {f(ara('vmafneg_ort'))} | {f(ara('xpsnr'))} | {f(ara('karanlik_psnr'))} | {f(sure,1)} |")

def bul(kok, ad):
    return yukle(next(Path(kok).rglob(ad)))


def hedefbant(kok, kesitler):
    print("| Kesit | Kol | svtav1-params | Kaynak | Hedef MB | Bant alt MB | 1. deneme kbit | 1. hedeflenen MB | 1. çıkan MB | 1. doluluk % | 1. bantta | 1. dal | Deneme | Teslim MB | Teslim taşma | Kodlama sn |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for k in kesitler:
        for s in bul(kok, f"hedefbant-{k}.json"):
            kaynak = f"{s['kaynak_bas']}+{f(s['kaynak_sure'],1)} sn{' döngü' if s['kaynak_dongu'] else ''}"
            print(f"| {k} | {s['kol']} | `{s['svtav1_params']}` | {kaynak} | {f(s['hedef_mb'],0)} | {f(s['bant_alt_mb'],3)} | {s['ilk_kbit']} | {f(s['ilk_hedeflenen_mb'],3)} | {f(s['ilk_cikan_mb'],3)} | {f(s['ilk_doluluk'])} | {'evet' if s['ilk_bantta'] else 'hayır'} | {s['ilk_dal']} | {s['deneme']} | {f(s['teslim_mb'],3)} | {'VAR' if s['teslim_tasma'] else 'yok'} | {f(s['kodlama_sn'],1)} |")
    print()
    print("| Kesit | Kol | Hedef MB | Denemeler (no:dal:kbit:çıkan MB) |")
    print("|---|---|---|---|")
    for k in kesitler:
        for s in bul(kok, f"hedefbant-{k}.json"):
            print(f"| {k} | {s['kol']} | {f(s['hedef_mb'],0)} | {s['dallar']} |")


def egri(satirlar, alan):
    v = sorted(satirlar, key=lambda s: s["kbps"])
    noktalar = []
    for s in v:
        if s[alan] is None:
            continue
        if noktalar and s[alan] <= noktalar[-1][0]:
            continue
        noktalar.append((s[alan], math.log(s["kbps"])))
    return noktalar


def logkbps(noktalar, q):
    for (q0, l0), (q1, l1) in zip(noktalar, noktalar[1:]):
        if q0 <= q <= q1:
            return l0 + (q - q0) / (q1 - q0) * (l1 - l0)
    return None


def esoran(taban, test, adim=40):
    if len(taban) < 2 or len(test) < 2:
        return None, None, None
    alt = max(taban[0][0], test[0][0])
    ust = min(taban[-1][0], test[-1][0])
    if alt >= ust:
        return None, alt, ust
    farklar = []
    for i in range(adim + 1):
        q = alt + (ust - alt) * i / adim
        a, b = logkbps(taban, q), logkbps(test, q)
        if a is not None and b is not None:
            farklar.append(b - a)
    return math.exp(sum(farklar) / len(farklar)), alt, ust


def oran(kok, kesitler):
    tum = []
    for k in kesitler:
        tum += bul(kok, f"oran-{k}.json")
    print("| Kesit | Kol | İstenen kbit | kbps | VMAF-NEG ort | XPSNR | Karanlık PSNR | Kodlama sn |")
    print("|---|---|---|---|---|---|---|---|")
    for s in tum:
        print(f"| {s['kesit']} | {s['kol']} | {s['istenen_kbit']} | {f(s['kbps'],1)} | {f(s['vmafneg_ort'])} | {f(s['xpsnr'])} | {f(s['karanlik_psnr'])} | {f(s['kodlama_sn'],1)} |")
    kollar = []
    for s in tum:
        if s["kol"] not in kollar:
            kollar.append(s["kol"])
    print()
    print("| Kesit | Ölçü | Kol | Eş kalitede kbps oranı (kol / libx264) | Ortak aralık | Kol / libx265 |")
    print("|---|---|---|---|---|---|")
    for k in kesitler:
        for alan, ad in (("vmafneg_ort", "VMAF-NEG"), ("xpsnr", "XPSNR")):
            taban = egri([s for s in tum if s["kesit"] == k and s["kol"] == "libx264"], alan)
            x265 = egri([s for s in tum if s["kesit"] == k and s["kol"] == "libx265"], alan)
            for kol in kollar:
                if kol == "libx264":
                    continue
                test = egri([s for s in tum if s["kesit"] == k and s["kol"] == kol], alan)
                r, alt, ust = esoran(taban, test)
                r2, _, _ = esoran(x265, test) if kol != "libx265" else (None, None, None)
                aralik = "—" if alt is None else f"{f(alt)} … {f(ust)}"
                print(f"| {k} | {ad} | {kol} | {f(r,3)} | {aralik} | {f(r2,3)} |")


if __name__ == "__main__":
    if sys.argv[1] == "hedefbant":
        hedefbant(sys.argv[2], sys.argv[3].split(","))
    elif sys.argv[1] == "oran":
        oran(sys.argv[2], sys.argv[3].split(","))
    elif sys.argv[1] == "av1":
        av1(sys.argv[2], sys.argv[3].split(","))
    elif sys.argv[1] == "handbrake":
        handbrake(sys.argv[2], sys.argv[3].split(","))
    elif sys.argv[1] == "whatsapp":
        whatsapp(sys.argv[2])
    elif sys.argv[1] == "ceza":
        ceza(sys.argv[2], sys.argv[3].split(","))
