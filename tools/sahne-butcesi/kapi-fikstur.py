import json, os, sys

senaryo, hedef = sys.argv[1], sys.argv[2]
PENCERELER = ["p1-karisik", "p2-durgun", "p3-hareketli"]
ALT, UST, HEDEF = 57.0, 60.0, 60.0


def kayit(pencere, kol, mb, mean, p10, worst):
    return {
        "Pencere": pencere, "Kol": kol, "GerceklesenMb": mb, "HedefMb": HEDEF,
        "BandAltMb": ALT, "BandUstMb": UST, "BandIcinde": ALT <= mb <= UST,
        "VmafMean": mean, "VmafP10": p10, "VmafMin": p10 - 1.0,
        "VmafWorstScene": worst, "Bilinmiyor": None,
    }


if senaryo == "olcum-yok":
    sys.exit(0)

for i, p in enumerate(PENCERELER):
    taban_mb = 59.0
    dagitim_mb = 59.2
    dagitim_p10 = 90.70
    dagitim_worst = 86.50
    if senaryo == "p10-kaybi" and p == "p3-hareketli":
        dagitim_p10 = 89.50
        dagitim_worst = 86.50
    if senaryo == "band-asan" and p == "p2-durgun":
        dagitim_mb = 61.0
    k5 = [
        kayit(p, "taban", taban_mb, 93.00, 90.00, 85.00),
        kayit(p, "dagitim", dagitim_mb, 93.40, dagitim_p10, dagitim_worst),
    ]
    with open(os.path.join(hedef, f"k5-uyumlu-{p}.json"), "w", encoding="utf-8") as f:
        json.dump(k5, f, indent=2)

    bozuk_p10 = 90.50
    if senaryo == "k7-bedeli" and p == "p1-karisik":
        bozuk_p10 = 89.50
    k7 = [
        kayit(p, "eksik-kesim", 59.1, 93.20, bozuk_p10, 86.00),
        kayit(p, "fazla-kesim", 59.1, 93.20, 90.50, 86.00),
    ]
    with open(os.path.join(hedef, f"k7-uyumlu-{p}.json"), "w", encoding="utf-8") as f:
        json.dump(k7, f, indent=2)
