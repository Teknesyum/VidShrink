import json
import sys
from pathlib import Path

ISLER = ["handbrake", "dusuk", "social", "bantlasma", "turbo", "hdr", "vt", "ekranbant"]
GURULTU_VMAF = 0.3
GURULTU_XPSNR = 0.2
KBPS_TOLERANS = 0.02


def f(x, n=2):
    if x is None or x == "":
        return "—"
    if isinstance(x, bool):
        return "evet" if x else "hayır"
    if isinstance(x, (int, float)):
        return f"{x:.{n}f}".replace(".", ",")
    return str(x).replace("|", "/")


def satirlar(kok):
    hepsi = []
    for yol in sorted(Path(kok).rglob("*.json")):
        ad = yol.name.split("__")[-1]
        if not any(ad == f"{i}.json" or ad.startswith(f"{i}-") for i in ISLER):
            continue
        if ad.endswith(".olcu.json") or ad.startswith("vmaf-"):
            continue
        try:
            veri = json.loads(yol.read_text(encoding="utf-8-sig"))
        except (json.JSONDecodeError, UnicodeDecodeError):
            continue
        if isinstance(veri, dict):
            veri = [veri]
        if not isinstance(veri, list) or not veri or not isinstance(veri[0], dict) or "kol" not in veri[0]:
            continue
        for s in veri:
            s["_dosya"] = str(yol.relative_to(kok))
        hepsi.extend(veri)
    return hepsi


def hukum(u, h):
    if u.get("vmafneg_ort") is None or h.get("vmafneg_ort") is None or u.get("xpsnr") is None or h.get("xpsnr") is None:
        return "ölçülemedi"
    dv = u["vmafneg_ort"] - h["vmafneg_ort"]
    dx = u["xpsnr"] - h["xpsnr"]
    geride = dv < -GURULTU_VMAF or dx < -GURULTU_XPSNR
    onde = dv > GURULTU_VMAF or dx > GURULTU_XPSNR
    if u.get("kbps") and h.get("kbps") and abs(h["kbps"] / u["kbps"] - 1) > KBPS_TOLERANS:
        if u["kbps"] < h["kbps"]:
            return "geride (az bayt)" if geride else "önde (az bayt)" if onde else "bantta (az bayt)"
        return "geride (çok bayt)" if geride else "eş bayt değil"
    if geride:
        return "geride"
    if onde:
        return "önde"
    return "bantta"


def bant_hukum(u, h):
    if u.get("cambi") is None or h.get("cambi") is None:
        return "ölçülemedi"
    return "e0 kötü (eşik aşıldı)" if u["cambi"] - h["cambi"] > 1.0 else "eşik içinde"


def tablo(sat, sutunlar):
    print("| " + " | ".join(b for b, _, _ in sutunlar) + " |")
    print("|" + "---|" * len(sutunlar))
    for s in sat:
        print("| " + " | ".join(f(s.get(k), n) for _, k, n in sutunlar) + " |")
    print()


ORTAK = [
    ("Kesit", "kesit", 0), ("kbit", "istenen_kbit", 0), ("Kol", "kol", 0), ("Kodlayıcı", "kodlayici", 0),
    ("Geometri", "geometri_cikti", 0), ("kbps", "kbps", 1), ("Bayt", "bayt", 0),
    ("VMAF-NEG ort", "vmafneg_ort", 2), ("VMAF-NEG harm", "vmafneg_harm", 2), ("XPSNR", "xpsnr", 2),
    ("SSIM", "ssim", 4), ("CAMBI", "cambi", 3), ("Karanlık PSNR", "karanlik_psnr", 2),
    ("Kodlama sn", "kodlama_sn", 1), ("Hata", "hata", 0),
]


def kiyas(sat, urun_kol, hb_kol, cambi_hukum=False):
    print("| Kesit | kbit | Δ VMAF-NEG ort | Δ VMAF-NEG harm | Δ XPSNR | Δ SSIM | Δ CAMBI (ürün−HB) | Δ karanlık PSNR | Ürün sn / HB sn | kbps sapma % | Hüküm |")
    print("|---|---|---|---|---|---|---|---|---|---|---|")
    for u in sat:
        if u.get("kol") != urun_kol:
            continue
        h = next((x for x in sat if x.get("kol") == hb_kol and x.get("kesit") == u.get("kesit") and x.get("istenen_kbit") == u.get("istenen_kbit") and x.get("onayar") == u.get("onayar")), None)
        if h is None:
            print(f"| {u.get('kesit')} | {u.get('istenen_kbit')} | — | — | — | — | — | — | — | — | ölçülemedi |")
            continue

        def d(k):
            return None if u.get(k) is None or h.get(k) is None else u[k] - h[k]

        oran = None if not u.get("kodlama_sn") or not h.get("kodlama_sn") else u["kodlama_sn"] / h["kodlama_sn"]
        sapma = None if not u.get("kbps") or not h.get("kbps") else (h["kbps"] / u["kbps"] - 1) * 100
        print(f"| {u.get('kesit')} | {u.get('istenen_kbit') or u.get('onayar')} | {f(d('vmafneg_ort'))} | {f(d('vmafneg_harm'))} | {f(d('xpsnr'))} | {f(d('ssim'), 4)} | {f(d('cambi'), 3)} | {f(d('karanlik_psnr'))} | {f(oran)} | {f(sapma)} | {bant_hukum(u, h) if cambi_hukum else hukum(u, h)} |")
    print()


def main():
    kok = Path(sys.argv[1])
    hepsi = satirlar(kok)
    for is_ in ISLER:
        sat = [s for s in hepsi if s.get("is") == is_]
        if not sat:
            continue
        print(f"## {is_}\n")
        if is_ == "social":
            tablo(sat, [("Kesit", "kesit", 0), ("Ön ayar", "onayar", 0), ("Kol", "kol", 0), ("Hedef MB", "hedef_mb", 3), ("MB", "mb", 3), ("Geometri", "geometri_cikti", 0), ("kbps", "kbps", 1), ("VMAF-NEG ort", "vmafneg_ort", 2), ("VMAF-NEG harm", "vmafneg_harm", 2), ("XPSNR", "xpsnr", 2), ("SSIM", "ssim", 4), ("CAMBI", "cambi", 3), ("Kodlama sn", "kodlama_sn", 1), ("Hata", "hata", 0)])
        elif is_ == "ekranbant":
            tablo(sat, [("Hedef MB", "hedef_mb", 1), ("Teslim MB", "mb", 3), ("Doluluk %", "teslim_doluluk", 2), ("Taşma", "teslim_tasma", 0), ("Bantta", "bantta", 0), ("Deneme", "deneme", 0), ("İlk kbit", "ilk_kbit", 0), ("İlk hedeflenen→çıkan", "dallar", 0), ("İlk verim", "ilk_verim", 3), ("Geometri", "geometri", 0), ("VMAF-NEG ort", "vmafneg_ort", 2), ("VMAF-NEG harm", "vmafneg_harm", 2), ("XPSNR", "xpsnr", 2), ("Kodlama sn", "kodlama_sn", 1), ("Hata", "hata", 0)])
        elif is_ == "hdr":
            tablo(sat, ORTAK + [("Transfer", "color_transfer", 0), ("Primaries", "color_primaries", 0), ("Mastering", "mastering", 0), ("CLL", "content_light", 0), ("Yan veri eş", "yan_veri_es", 0), ("PQ korundu", "pq_korundu", 0)])
        else:
            tablo(sat, ORTAK + [("Toplam sn", "toplam_sn", 1), ("HB deneme", "hb_deneme", 0), ("Ek hata", "ek_hata", 0)])
        if is_ in ("handbrake", "hdr"):
            kiyas(sat, "urun-otomatik", "handbrake")
        if is_ == "dusuk":
            kiyas(sat, "urun-otomatik", "handbrake-1080p")
            kiyas(sat, "urun-dusurme-kapali", "handbrake-1080p")
        if is_ == "social":
            kiyas(sat, "urun-otomatik", "handbrake")
            kiyas(sat, "urun-dusurme-kapali", "handbrake")
        if is_ == "turbo":
            kiyas(sat, "urun-x265", "handbrake-x265")
            kiyas(sat, "urun-x265-turbo", "handbrake-x265-turbo")
            kiyas(sat, "urun-x265-turbo", "urun-x265")
        if is_ == "bantlasma":
            kiyas(sat, "e0", "e1", cambi_hukum=True)
        if is_ == "vt":
            kiyas(sat, "urun-vt", "handbrake-vt")


if __name__ == "__main__":
    main()
