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


if __name__ == "__main__":
    if sys.argv[1] == "handbrake":
        handbrake(sys.argv[2], sys.argv[3].split(","))
    if sys.argv[1] == "whatsapp":
        whatsapp(sys.argv[2])
    elif sys.argv[1] == "ceza":
        ceza(sys.argv[2], sys.argv[3].split(","))
