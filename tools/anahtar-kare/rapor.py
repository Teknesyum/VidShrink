"""Raporun sayi tasiyan her bolumunu izgara dosyalarindan uretir.

Govde `rapor-govde.md`dedir ve yalniz yer tutucu tasir. Ozet cumleleri de
buradan cikar; elle yazilan bir sayi rapora giremez.
"""
import os, sys, io
import tablo

ROOT = tablo.ROOT
CAL = tablo.CAL
BURA = os.path.dirname(os.path.abspath(__file__))


def bolum(eti, baslik):
    kaynaklar = tablo.izgara(eti)
    hrt = tablo.harita_oku()
    atl = tablo.atlama_oku(eti)
    out = []
    cevap = tablo.cumleler(kaynaklar, hrt, atl, eti, out)
    ozet = "\n".join(out)
    t3, t1, t2 = [], [], []
    tablo.yaz_k3(kaynaklar, hrt, t3, eti)
    tablo.yaz_k1(kaynaklar, eti, t1)
    tablo.yaz_k2(atl, kaynaklar, eti, t2)
    return dict(cevap=cevap, ozet=ozet, k3="\n".join(t3), k1="\n".join(t1), k2="\n".join(t2),
                kaynaklar=kaynaklar, baslik=baslik)


def kisa_tablo(eti, baslik):
    """Sadece izgara, karar kurali uygulanmadan (donanim / ikinci kodlayici)."""
    satirlar = tablo.oku(eti)
    out = ["### %s\n" % baslik]
    out.append("| kaynak | `-g` (sn) | `-g` (kare) | teslim (bayt) | boyut sapmasi (%) | I-kare "
               "| gerc. aralik (sn) | VMAF-NEG ort. | VMAF-NEG p10 | en kotu kare | damga |")
    out.append("|---|---|---|---|---|---|---|---|---|---|---|")
    for s in dict.fromkeys(r["kaynak"] for r in satirlar):
        hs = sorted([r for r in satirlar if r["kaynak"] == s], key=lambda r: r["g_sn"])
        tablo.boyut_damgasi(hs)
        temiz = [h for h in hs if h["es_boyut"]]
        en_iyi = max(temiz, key=lambda r: r["p10"]) if temiz else None
        for h in hs:
            damga = "es boyut" if h["es_boyut"] else "**es boyut degil**"
            if h is en_iyi:
                damga += " - en iyi"
            out.append("| `%s` | %d | %d | %d | %+.3f | %d | %s | %.3f | %.3f | %.3f | %s |"
                       % (s, h["g_sn"], h["g_kare"], h["boyut_bayt"], h["boyut_sapma_yuzde"],
                          h["ikare"], tablo.aralik(h), h["mean"], h["p10"], h["worst"], damga))
    out.append("")
    return "\n".join(out)


def kisa_cumleler(eti, etiket):
    satirlar = tablo.oku(eti)
    out = []
    for s in dict.fromkeys(r["kaynak"] for r in satirlar):
        hs = sorted([r for r in satirlar if r["kaynak"] == s], key=lambda r: r["g_sn"])
        tablo.boyut_damgasi(hs)
        temiz = [h for h in hs if h["es_boyut"]]
        if not temiz:
            continue
        e = max(temiz, key=lambda r: r["p10"])
        h5 = next((h for h in hs if h["g_sn"] == 5), None)
        h10 = next((h for h in hs if h["g_sn"] == 10), None)
        out.append("- %s `%s`: en iyi es-boyut hucresi **%d sn** (p10 %.3f). 5 sn hucresi p10 %.3f, "
                   "10 sn hucresi p10 %.3f; 5 sn'nin 10 sn'ye gore farki **%+.3f p10**. "
                   "(kaynak: %s tablosu, `VMAF-NEG p10` kolonu.)"
                   % (etiket, s, e["g_sn"], e["p10"], h5["p10"], h10["p10"],
                      h5["p10"] - h10["p10"], etiket))
    return "\n".join(out)


def tekrar(eti):
    out = []
    tablo.yaz_tekrar(eti, out)
    return "\n".join(out)


def hizalama_tablosu():
    out = ["| kaynak | kaynak karesi | kayipsiz PSNR | kayipsiz VMAF-NEG ort. | p10 | en kotu kare |",
           "|---|---|---|---|---|---|"]
    for line in open(os.path.join(CAL, "hizalama.txt"), encoding="utf-8"):
        p = line.split()
        if len(p) < 6:
            continue
        out.append("| `%s` | %s | %s | %s | %s | %s |"
                   % (p[0], p[1].split("=")[1], p[2].split("=")[1], p[-3], p[-2], p[-1]))
    return "\n".join(out)


def harita_tablosu():
    hrt = tablo.harita_oku()
    out = ["| kaynak | sahne kesme adayi | esikten gecen kesme | sahne | sahne suresi medyani (sn) "
           "| haritanin sectigi tavan (sn) |", "|---|---|---|---|---|---|"]
    for s, h in hrt.items():
        out.append("| `%s` | %d | %d | %d | %s | %.3f |"
                   % (s, h["aday"], h["kesim"], h["sahne"],
                      ("%.3f" % h["medyan"]) if h["medyan"] is not None else "-", h["tavan"]))
    return "\n".join(out)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    zor = bolum("zorluk", "zorluk esitlenmis")
    bpp = bolum("bpp", "es bit/piksel")
    govde = open(os.path.join(BURA, "rapor-govde.md"), encoding="utf-8").read()
    yer = {
        "CEVAP_ZORLUK": zor["cevap"],
        "CEVAP_BPP": bpp["cevap"],
        "OZET_ZORLUK": zor["ozet"],
        "OZET_BPP": bpp["ozet"],
        "K3_ZORLUK": zor["k3"],
        "K3_BPP": bpp["k3"],
        "K1_ZORLUK": zor["k1"],
        "K1_BPP": bpp["k1"],
        "K2_ZORLUK": zor["k2"],
        "K2_BPP": bpp["k2"],
        "TEKRAR_BPP": tekrar("bpp"),
        "TEKRAR_ZORLUK": tekrar("zorluk"),
        "RECETE": open(os.path.join(BURA, "rapor-recete.md"), encoding="utf-8").read().rstrip(),
        "K4": open(os.path.join(BURA, "rapor-k4.md"), encoding="utf-8").read().rstrip(),
        "HIZALAMA": hizalama_tablosu(),
        "HARITA": harita_tablosu(),
        "NVENC": kisa_tablo("nvenc", "Donanim kolu - `av1_nvenc` preset p5, es bit/piksel"),
        "NVENC_CUMLE": kisa_cumleler("nvenc", "av1_nvenc"),
        "SVTAV1": kisa_tablo("svtav1", "Ikinci kodlayici - `libsvtav1` preset 4, es bit/piksel"),
        "SVTAV1_CUMLE": kisa_cumleler("svtav1", "libsvtav1"),
    }
    for k, v in yer.items():
        govde = govde.replace("{{%s}}" % k, v)
    kalan = [x.split("}}")[0] for x in govde.split("{{")[1:]]
    hedef = os.path.join(ROOT, "docs", "olcumler", "anahtar-kare-tavani.md")
    with open(hedef, "w", encoding="utf-8", newline="\n") as f:
        f.write(govde)
    print("yazildi: " + hedef)
    if kalan:
        print("DOLDURULMAMIS YER TUTUCU: " + ", ".join(kalan), file=sys.stderr)
        sys.exit(1)
