import csv, json, sys, os, statistics

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CAL = os.path.join(ROOT, ".calisma", "t133")
ESIK = 0.20
BOYUT_BANDI = 0.5

SINIF = {
    "s1-kesikli": "kesik cok",
    "s2-durgun": "durgun",
    "s3-hareketli": "hareketli",
    "s4-yuksek": "yuksek cozunurluklu",
}


def oku(eti):
    yol = os.path.join(CAL, "izgara-%s.csv" % eti)
    with open(yol, encoding="utf-8-sig", newline="") as f:
        ham = f.read().replace("\r", "")
    satirlar = [r for r in csv.DictReader(ham.splitlines()) if r.get("mean")]
    for r in satirlar:
        for k in ("g_sn", "g_kare", "kmin_kare", "bitrate_k", "boyut_bayt", "ikare", "kare"):
            r[k] = int(float(r[k]))
        for k in ("gerc_aralik_sn", "mean", "p10", "worst"):
            r[k] = float(r[k])
    return satirlar


def boyut_damgasi(hucreler):
    ort = statistics.mean(h["boyut_bayt"] for h in hucreler)
    for h in hucreler:
        h["boyut_sapma_yuzde"] = (h["boyut_bayt"] - ort) / ort * 100.0
        h["es_boyut"] = abs(h["boyut_sapma_yuzde"]) <= BOYUT_BANDI
    return ort


def aralik(h):
    return "%.3f" % h["gerc_aralik_sn"] if h["ikare"] > 1 else "tek I-kare"


def karar(kaynaklar):
    kisa, uzun, karisik = [], [], []
    for s, d in kaynaklar.items():
        e, h10 = d["en_iyi"], d["h10"]
        if e is None or h10 is None:
            continue
        fark_p10 = e["p10"] - h10["p10"]
        fark_ort = e["mean"] - h10["mean"]
        if fark_p10 < ESIK:
            continue
        if fark_ort < 0:
            karisik.append(s)
            continue
        if e["g_sn"] < 10:
            kisa.append(s)
        elif e["g_sn"] > 10:
            uzun.append(s)
    if len(kisa) >= 2 and len(uzun) >= 2:
        return "a+c", kisa, uzun, karisik
    if len(kisa) >= 2:
        return "a", kisa, uzun, karisik
    if len(uzun) >= 2:
        return "c", kisa, uzun, karisik
    return "b", kisa, uzun, karisik


def izgara(eti):
    satirlar = oku(eti)
    kaynaklar = {}
    for s in SINIF:
        hs = [r for r in satirlar if r["kaynak"] == s]
        if not hs:
            continue
        hs.sort(key=lambda r: r["g_sn"])
        boyut_damgasi(hs)
        temiz = [h for h in hs if h["es_boyut"]]
        en_iyi = max(temiz, key=lambda r: r["p10"]) if temiz else None
        h10 = next((h for h in hs if h["g_sn"] == 10), None)
        kaynaklar[s] = dict(hucreler=hs, en_iyi=en_iyi, h10=h10)
    return kaynaklar


def harita_oku():
    with open(os.path.join(CAL, "harita.json"), encoding="utf-8") as f:
        return json.load(f)


def atlama_oku(eti):
    return atlama_dosyadan(os.path.join(CAL, "atlama-%s.txt" % eti))


def atlama_dosyadan(yol):
    d = {}
    if not os.path.exists(yol):
        return d
    for line in open(yol, encoding="utf-8"):
        p = line.split()
        if len(p) < 6 or "=" not in p[1]:
            continue
        d[p[0]] = dict((k, float(v)) for k, v in (x.split("=") for x in p[1:] if "=" in x))
    return d


def yaz_tekrar(eti, out):
    k1 = atlama_dosyadan(os.path.join(CAL, "atlama-%s-kosum1.txt" % eti))
    k2 = atlama_dosyadan(os.path.join(CAL, "atlama-%s-kosum2.txt" % eti))
    if not k1 or not k2:
        return
    out.append("| hucre | yapisal mesafe kosum 1 (sn) | yapisal mesafe kosum 2 (sn) "
               "| net p50 kosum 1 (ms) | net p50 kosum 2 (ms) | net p50 orani |")
    out.append("|---|---|---|---|---|---|")
    for ad in k2:
        if ad not in k1:
            continue
        a, b = k1[ad], k2[ad]
        oran = a["netp50_ms"] / b["netp50_ms"] if b["netp50_ms"] else 0.0
        out.append("| `%s` | %.3f | %.3f | %.1f | %.1f | %.2f |"
                   % (ad, a["yapisal_sn"], b["yapisal_sn"], a["netp50_ms"], b["netp50_ms"], oran))
    out.append("")


def yaz_k1(kaynaklar, eti, out):
    out.append("### K1 izgarasi - %s\n" % eti)
    out.append("| kaynak | sinif | `-g` (sn) | `-g` (kare) | teslim (bayt) | boyut sapmasi (%) "
               "| I-kare | gerc. aralik (sn) | VMAF-NEG ort. | VMAF-NEG p10 | en kotu kare | damga |")
    out.append("|---|---|---|---|---|---|---|---|---|---|---|---|")
    for s, d in kaynaklar.items():
        for h in d["hucreler"]:
            damga = "es boyut" if h["es_boyut"] else "**es boyut degil**"
            if d["en_iyi"] is h:
                damga += " - en iyi"
            out.append("| `%s` | %s | %d | %d | %d | %+.3f | %d | %s | %.3f | %.3f | %.3f | %s |"
                       % (s, SINIF[s], h["g_sn"], h["g_kare"], h["boyut_bayt"], h["boyut_sapma_yuzde"],
                          h["ikare"], aralik(h), h["mean"], h["p10"], h["worst"], damga))
    out.append("")


def yaz_k3(kaynaklar, hrt, out, eti=""):
    out.append("### K3 - haritanin secimi izgaranin neresine dusuyor%s\n"
               % ((" (%s)" % eti) if eti else ""))
    out.append("| kaynak | harita medyani (sn) | haritanin sectigi tavan (sn) | izgarada en yakin hucre "
               "| en iyi hucre (sn) | p10 harita-hucresi | p10 en-iyi | fark (p10) | fark (ort.) |")
    out.append("|---|---|---|---|---|---|---|---|---|")
    for s, d in kaynaklar.items():
        h = hrt[s]
        tavan = h["tavan"]
        yakin = min(d["hucreler"], key=lambda r: abs(r["g_sn"] - tavan))
        e = d["en_iyi"]
        med = ("%.3f" % h["medyan"]) if h["medyan"] is not None else "-"
        if e is None:
            out.append("| `%s` | %s | %.3f | %d sn | - | %.3f | - | - | - |"
                       % (s, med, tavan, yakin["g_sn"], yakin["p10"]))
            continue
        out.append("| `%s` | %s | %.3f | %d sn | %d | %.3f | %.3f | %+.3f | %+.3f |"
                   % (s, med, tavan, yakin["g_sn"], e["g_sn"], yakin["p10"], e["p10"],
                      e["p10"] - yakin["p10"], e["mean"] - yakin["mean"]))
    out.append("")


def yaz_k2(atl, kaynaklar, eti, out):
    if not atl:
        return
    out.append("### K2 - atlama maliyeti (%s)\n" % eti)
    out.append("| kaynak | `-g` (sn) | I-kare | yapisal mesafe (sn) | yapisal mesafe (kare) "
               "| net p50 (ms) | net IQR (ms) | ham coz p50 (ms) | ham kopya p50 (ms) |")
    out.append("|---|---|---|---|---|---|---|---|---|")
    for s, d in kaynaklar.items():
        for h in d["hucreler"]:
            a = atl.get("%s-%s-g%d" % (eti, s, h["g_sn"]))
            if not a:
                continue
            out.append("| `%s` | %d | %d | %.3f | %.1f | %.1f | %.1f | %.1f | %.1f |"
                       % (s, h["g_sn"], int(a["ikare"]), a["yapisal_sn"], a["yapisal_kare"],
                          a["netp50_ms"], a.get("netiqr_ms", 0.0), a["coz_p50"], a["kopya_p50"]))
    out.append("")


def cumleler(kaynaklar, hrt, atl, eti, out):
    cevap, kisa, uzun, karisik = karar(kaynaklar)
    out.append("### Uretilen ozet cumleler (%s)\n" % eti)
    out.append("Bu bolumun her cumlesi `tools/anahtar-kare/tablo.py` tarafindan asagidaki")
    out.append("tablolardan uretildi, elle yazilmadi. Her sayinin yaninda hangi tablodan ve")
    out.append("hangi kolondan geldigi yazili.\n")
    out.append("- **Cevap: (%s).** Karar kurali K4'te olcumden once yazildi, esik **%.2f p10**. "
               "Kisa uc kazanan kaynak sayisi **%d/4**, uzun uc kazanan **%d/4**, isaret karisan "
               "**%d/4** (kaynak: K1 izgarasi, `VMAF-NEG p10` kolonu; karsilastirma tabani "
               "`-g` = 10 sn hucresi)."
               % (cevap, ESIK, len(kisa), len(uzun), len(karisik)))
    for s, d in kaynaklar.items():
        e, h10 = d["en_iyi"], d["h10"]
        if e is None or h10 is None:
            out.append("- `%s` (%s): **kazanan secilemedi** - bes hucrenin %s boyut bandinin "
                       "(%%%.1f) disinda, damgali hucre \"en iyi\" olamaz. (kaynak: K1 izgarasi, "
                       "`boyut sapmasi (%%)` kolonu.)"
                       % (s, SINIF[s],
                          "tamami" if not any(h["es_boyut"] for h in d["hucreler"]) else "10 sn'lisi",
                          BOYUT_BANDI))
            continue
        out.append("- `%s` (%s): en iyi hucre **%d sn** (p10 %.3f), 10 sn hucresi p10 %.3f, fark "
                   "**%+.3f p10 / %+.3f ort.** - esigin %s. (kaynak: K1 izgarasi, `VMAF-NEG p10` "
                   "ve `VMAF-NEG ort.` kolonlari.)"
                   % (s, SINIF[s], e["g_sn"], e["p10"], h10["p10"],
                      e["p10"] - h10["p10"], e["mean"] - h10["mean"],
                      "ustunde" if e["p10"] - h10["p10"] >= ESIK else "altinda"))
    for s, d in kaynaklar.items():
        h = hrt[s]
        yakin = min(d["hucreler"], key=lambda r: abs(r["g_sn"] - h["tavan"]))
        e = d["en_iyi"]
        if e is None:
            out.append("- `%s`: bugunku harita **%.3f sn** tavani secerdi; izgarada en yakin hucre "
                       "**%d sn** (p10 %.3f). Es boyut damgasi temiz hucre olmadigi icin \"en iyi\" "
                       "hucre yok, fark hesaplanamaz. (kaynak: K3 tablosu.)"
                       % (s, h["tavan"], yakin["g_sn"], yakin["p10"]))
            continue
        out.append("- `%s`: bugunku harita **%.3f sn** tavani secerdi; izgarada en yakin hucre "
                   "**%d sn**, en iyi hucre **%d sn**, aradaki fark **%+.3f p10**. "
                   "(kaynak: K3 tablosu, `p10 harita-hucresi` ve `p10 en-iyi` kolonlari.)"
                   % (s, h["tavan"], yakin["g_sn"], e["g_sn"], e["p10"] - yakin["p10"]))
    for s, d in kaynaklar.items():
        a2 = atl.get("%s-%s-g2" % (eti, s))
        a20 = atl.get("%s-%s-g20" % (eti, s))
        if not a2 or not a20:
            continue
        kat = a20["yapisal_sn"] / a2["yapisal_sn"] if a2["yapisal_sn"] else 0.0
        out.append("- `%s`: `-g` 2 sn -> 20 sn arasinda yapisal atlama mesafesi **%.3f -> %.3f sn** "
                   "(%.1f kat, deterministik); ayni ucta net p50 %.1f -> %.1f ms, IQR %.1f / %.1f ms "
                   "(paylasilan makine sayisi, karara girmez). (kaynak: K2 tablosu, "
                   "`yapisal mesafe (sn)`, `net p50 (ms)` ve `net IQR (ms)` kolonlari.)"
                   % (s, a2["yapisal_sn"], a20["yapisal_sn"], kat, a2["netp50_ms"], a20["netp50_ms"],
                      a2.get("netiqr_ms", 0.0), a20.get("netiqr_ms", 0.0)))
    out.append("")
    return cevap


if __name__ == "__main__":
    eti = sys.argv[1] if len(sys.argv) > 1 else "x264"
    kaynaklar = izgara(eti)
    hrt = harita_oku()
    atl = atlama_oku(eti)
    out = []
    cumleler(kaynaklar, hrt, atl, eti, out)
    yaz_k3(kaynaklar, hrt, out)
    yaz_k1(kaynaklar, eti, out)
    yaz_k2(atl, kaynaklar, eti, out)
    sys.stdout.reconfigure(encoding="utf-8")
    print("\n".join(out))
