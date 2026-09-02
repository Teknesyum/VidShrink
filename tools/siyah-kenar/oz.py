import json, os, sys

NL = chr(10)

KOK = "C:/Users/Administrator/Desktop/Projeler/Vidshrink"
WT = KOK + "/.claude/worktrees/T134"
IS = WT + "/.calisma/t134"
RAPOR = WT + "/docs/olcumler/siyah-kenar.md"

LETTERBOX = ["KA", "KB", "KC", "KD"]
KENARSIZ = ["NA", "NB"]
ANA_HIZ = 2000
ESIK_DEGER = 1.00
ESIK_TABAN = 0.30
ESIK_SURE = 2.0
ADLAR = {
    "KA": "KA 2,39:1 duz bant", "KB": "KB 2,20:1 duz bant",
    "KC": "KC 1,85:1 duz bant", "KD": "KD 2,39:1 gurultulu asimetrik bant",
    "NA": "NA kenarsiz (parca-1)", "NB": "NB kenarsiz (parca-2)",
    "VD": "VD bant genisligi sahne icinde degisiyor",
    "KE": "KE 2,39:1 agir gurultulu bant (yalniz tespit sinamasi)",
}
ORAN = {"KA": "2,39:1", "KB": "2,20:1", "KC": "1,85:1", "KD": "2,39:1",
        "VD": "2,39:1 -> 1,78:1"}


def s(x, b=2):
    if x is None:
        return "-"
    return ("%%.%df" % b % x).replace(".", ",")


def yuk(p):
    if not os.path.exists(p):
        return None
    with open(p, encoding="utf-8") as f:
        return json.load(f)


def kutu_str(k):
    return "-" if not k else "%d:%d:%d:%d" % tuple(k)


def k1_tablo(kanit):
    r = ["| Kaynak | Cozunurluk | Gercek goruntu alani | Bant yuzdesi | Sure / kare | Kodek |",
         "|---|---|---|---|---|---|"]
    for ad in LETTERBOX + KENARSIZ + ["VD"]:
        d = kanit[ad]
        r.append("| %s | %s | %s | %s%% | %s sn / %d kare | %s %s |" % (
            ADLAR[ad], d["cozunurluk"], d["aktif"], s(d["bant_yuzde"]),
            s(d["sure"], 1), d["kare"], d["kodek"], d["pix_fmt"]))
    r.append("")
    r.append("| Kaynak | Ust bant px | Alt bant px | Bant YAVG (min/ort/maks) | Aktif alan YAVG (min/ort/maks) |")
    r.append("|---|---|---|---|---|")
    for ad in LETTERBOX + ["VD"]:
        d = kanit[ad]
        ub = d.get("ust_bant_stat") or {}
        ak = d.get("aktif_stat") or {}

        def uc(v):
            return "-" if not v else "%s / %s / %s" % (s(v[0]), s(v[1]), s(v[2]))
        r.append("| %s | %d | %d | %s | %s |" % (
            ADLAR[ad], d.get("ust_bant_px", 0), d.get("alt_bant_px", 0),
            uc(ub.get("YAVG")), uc(ak.get("YAVG"))))
    return "\n".join(r)


def kanit_tablo(kanit):
    r = ["| Kaynak | Kaynak dosya sha256 (ilk 16) | Kare | PTS ilk / son | Aktif alan framemd5 ozeti (ilk 16) | Tekrar esit mi |",
         "|---|---|---|---|---|---|"]
    for ad in LETTERBOX + KENARSIZ + ["VD"]:
        d = kanit[ad]
        o = d["kol_ozdesligi"]
        pts = o["pts"] or ("-", "-")
        r.append("| %s | `%s` | %d | %s / %s | `%s` | %s |" % (
            ADLAR[ad], d["sha256"][:16], o["kare"], pts[0], pts[1],
            o["kirpilmis_alan_md5"][:16], "evet" if o["esit"] else "**HAYIR**"))
    r.append("")
    r.append("Her satirda tek bir kaynak dosya var ve iki kol da onu okuyor; "
             "kirpmali kolun gordugu aktif alanin kare kare md5 dizisi iki bagimsiz "
             "cozumde ayni cikti. Kaynak dosyalarin uretimi `tools/siyah-kenar/kaynak.sh`, "
             "kanit `tools/siyah-kenar/kanit.py`.")
    return "\n".join(r)


def k2_tablo(yokla):
    r = ["| Kaynak | Gercek sinir | cropdetect t=10sn | Fark (px) | Sure (sn) |",
         "|---|---|---|---|---|"]
    for ad in LETTERBOX + KENARSIZ + ["VD"]:
        d = yokla[ad]
        g = tuple(d["gercek"])
        n = d["nokta"]["t10"]["kutu"]
        fark = "-" if not n else "%+d yatay / %+d dikey" % (n[0] - g[0], n[1] - g[1])
        r.append("| %s | %s | %s | %s | %s |" % (
            ADLAR[ad], kutu_str(g), kutu_str(n), fark, s(d["nokta"]["t10"]["sure"], 2)))
    r.append("")
    r.append("| Kaynak | t=0 | t=5 | t=10 | t=15 | 10 kare yayilmis (birlesim) | tam klip |")
    r.append("|---|---|---|---|---|---|---|")
    for ad in LETTERBOX + KENARSIZ + ["VD"]:
        d = yokla[ad]
        r.append("| %s | %s | %s | %s | %s | %s | %s |" % (
            ADLAR[ad],
            kutu_str(d["nokta"]["t00"]["kutu"]), kutu_str(d["nokta"]["t05"]["kutu"]),
            kutu_str(d["nokta"]["t10"]["kutu"]), kutu_str(d["nokta"]["t15"]["kutu"]),
            kutu_str(d["yayilmis"]["kutu"]), kutu_str(d["tam"]["kutu"])))
    r.append("")
    r.append("| Kaynak | 2 sn pencere | 10 kare yayilmis | tam klip (20 sn) |")
    r.append("|---|---|---|---|")
    for ad in LETTERBOX + KENARSIZ + ["VD"]:
        d = yokla[ad]
        r.append("| %s | %s sn | %s sn | %s sn |" % (
            ADLAR[ad], s(d["nokta"]["t10"]["sure"], 2),
            s(d["yayilmis"]["sure"], 2), s(d["tam"]["sure"], 2)))
    return "\n".join(r)


def medyan_kutu(tekil):
    k = [t for t in tekil if t]
    if not k:
        return None
    return [sorted(x[i] for x in k)[len(k) // 2] for i in range(4)]


def k2b_tablo(yokla):
    r = ["| Kaynak | Gercek sinir | 10 tekil karenin birlesimi | 10 tekil karenin medyani | Tam kare donen kare sayisi |",
         "|---|---|---|---|---|"]
    for ad in LETTERBOX + KENARSIZ + ["VD"]:
        d = yokla[ad]
        tekil = d["yayilmis"].get("tekil") or []
        bozuk = sum(1 for t in tekil if t and tuple(t) == (1920, 1080, 0, 0))
        r.append("| %s | %s | %s | %s | %d/%d |" % (
            ADLAR[ad], kutu_str(tuple(d["gercek"])), kutu_str(d["yayilmis"]["kutu"]),
            kutu_str(medyan_kutu(tekil)), bozuk, len(tekil)))
    return "\n".join(r)


def limit_tablo(lim):
    if not lim:
        return "`limit.json` yok — esik taramasi kosulmadi."
    limitler = sorted({int(k) for d in lim.values() for k in d["limit"]})
    r = ["| Kaynak | Gercek sinir | " + " | ".join("limit=%d" % x for x in limitler) + " |",
         "|---" * (2 + len(limitler)) + "|"]
    for ad, d in lim.items():
        r.append("| %s | %s | %s |" % (
            ADLAR.get(ad, ad), kutu_str(tuple(d["gercek"])),
            " | ".join(kutu_str(d["limit"].get(str(x))) for x in limitler)))
    r.append("")
    r.append("| Kaynak | Ust bant YMIN / YAVG / YMAX (8 bit olcek, 60 kare ortalamasi) |")
    r.append("|---|---|")
    for ad, d in lim.items():
        b = d.get("ust_bant_luma")
        if b:
            r.append("| %s | %s / %s / %s |" % (ADLAR.get(ad, ad),
                                                s(b["YMIN"]), s(b["YAVG"]), s(b["YMAX"])))
    return "\n".join(r)


def sure_tablo(sr):
    if not sr:
        return "`sure.json` yok — sure yeniden olculmedi."
    r = ["Makine bosken tekrarlanan olcum. Tekrar sayisi %d, medyan verildi. "
         "Olcum sirasinda es zamanli ffmpeg sayisi: basta %s, sonda %s."
         % (sr["tekrar"], sr["ffmpeg_sayisi_basta"], sr["ffmpeg_sayisi_sonda"]),
         "",
         "| Kaynak | 2 sn pencere (120 kare) | 10 tekil kare yayilmis | Tam klip (1200 kare) |",
         "|---|---|---|---|"]
    for ad, d in sr["kaynak"].items():
        r.append("| %s | %s sn | %s sn | %s sn |" % (
            ADLAR.get(ad, ad), s(d["pencere2sn"]["medyan"], 2),
            s(d["yayilmis10kare"]["medyan"], 2), s(d["tamklip"]["medyan"], 2)))
    return NL.join(r)


def kazanc(vmaf, ad, hiz, yontem="A"):
    a = vmaf.get("%s|duz|%d" % (ad, hiz))
    b = vmaf.get("%s|kirp|%d" % (ad, hiz))
    if not a or not b or yontem not in a or yontem not in b:
        return None
    return b[yontem]["p10"] - a[yontem]["p10"]


def es_boyut(vmaf, ad, hiz):
    a = vmaf.get("%s|duz|%d" % (ad, hiz))
    b = vmaf.get("%s|kirp|%d" % (ad, hiz))
    if not a or not b:
        return None
    return abs(b["boyut"] - a["boyut"]) * 100.0 / a["boyut"]


def k3_tablo(vmaf, kanit):
    r = ["| Kaynak | Bitrate | Kol | Boyut (bayt) | Boyut farki | VMAF-NEG ort | p10 | En kotu kare |",
         "|---|---|---|---|---|---|---|---|"]
    for ad in LETTERBOX:
        for hiz in (1000, 2000, 4000):
            f = es_boyut(vmaf, ad, hiz)
            damga = "" if f is None or f <= 1.0 else " **es boyut degil**"
            for kol in ("duz", "kirp"):
                d = vmaf.get("%s|%s|%d" % (ad, kol, hiz))
                if not d:
                    r.append("| %s | %dk | %s | - | - | - | - | - |" % (ADLAR[ad], hiz, kol))
                    continue
                m = d.get("A")
                r.append("| %s | %dk | %s | %d | %s%%%s | %s | %s | %s |" % (
                    ADLAR[ad], hiz, "kirpmasiz" if kol == "duz" else "kirpmali",
                    d["boyut"], s(f) if f is not None else "-",
                    damga if kol == "kirp" else "",
                    s(m["ort"], 3) if m else "-", s(m["p10"], 3) if m else "-",
                    s(m["en_kotu"], 3) if m else "-"))
    r.append("")
    r.append("**Kazanc (kirpmali p10 - kirpmasiz p10), A yontemi, aktif alanda:**")
    r.append("")
    r.append("| Kaynak | Bant yuzdesi | 1000k | 2000k (karar) | 4000k |")
    r.append("|---|---|---|---|---|")
    for ad in LETTERBOX:
        r.append("| %s | %s%% | %s | %s | %s |" % (
            ADLAR[ad], s(kanit[ad]["bant_yuzde"]),
            s(kazanc(vmaf, ad, 1000), 3), s(kazanc(vmaf, ad, 2000), 3),
            s(kazanc(vmaf, ad, 4000), 3)))
    r.append("")
    r.append("**B yontemi (kirpmali cikti geri doldurulup tam karede puanlandi), 2000k:**")
    r.append("")
    r.append("| Kaynak | Kirpmasiz p10 | Kirpmali+dolgu p10 | Kazanc |")
    r.append("|---|---|---|---|")
    for ad in LETTERBOX:
        a = vmaf.get("%s|duz|2000" % ad, {}).get("B")
        b = vmaf.get("%s|kirp|2000" % ad, {}).get("B")
        r.append("| %s | %s | %s | %s |" % (
            ADLAR[ad], s(a["p10"], 3) if a else "-", s(b["p10"], 3) if b else "-",
            s(kazanc(vmaf, ad, 2000, "B"), 3)))
    return "\n".join(r)


def k5_tablo(vmaf, yokla):
    r = ["**(a) Kenarsiz kaynakta yanlis kirpma:**", "",
         "| Kaynak | Gercek sinir | t=0 | t=5 | t=10 | t=15 | 10 kare yayilmis | tam klip | Yanlis kirpma |",
         "|---|---|---|---|---|---|---|---|---|"]
    for ad in KENARSIZ:
        d = yokla[ad]
        kutular = [d["nokta"]["t00"]["kutu"], d["nokta"]["t05"]["kutu"],
                   d["nokta"]["t10"]["kutu"], d["nokta"]["t15"]["kutu"],
                   d["yayilmis"]["kutu"], d["tam"]["kutu"]]
        yanlis = [k for k in kutular if k and tuple(k) != (1920, 1080, 0, 0)]
        r.append("| %s | 1920:1080:0:0 | %s | evet (%d/6 nokta) |" % (
            ADLAR[ad], " | ".join(kutu_str(k) for k in kutular), len(yanlis))
            if yanlis else
            "| %s | 1920:1080:0:0 | %s | hayir |" % (
                ADLAR[ad], " | ".join(kutu_str(k) for k in kutular)))
    r.append("")
    r.append("**(b) Bant genisligi sahne icinde degisen kaynak (VD):**")
    r.append("")
    d = yokla["VD"]
    r.append("| Olcu | Deger |")
    r.append("|---|---|")
    r.append("| Ilk 10 sn | 2,39:1 letterbox (aktif 1920x804) |")
    r.append("| Son 10 sn | tam kare (aktif 1920x1080) |")
    r.append("| cropdetect t=0 | %s |" % kutu_str(d["nokta"]["t00"]["kutu"]))
    r.append("| cropdetect t=5 | %s |" % kutu_str(d["nokta"]["t05"]["kutu"]))
    r.append("| cropdetect t=15 | %s |" % kutu_str(d["nokta"]["t15"]["kutu"]))
    r.append("| cropdetect 10 kare yayilmis | %s |" % kutu_str(d["yayilmis"]["kutu"]))
    r.append("| cropdetect tam klip | %s |" % kutu_str(d["tam"]["kutu"]))
    a = vmaf.get("VD|duz|2000", {}).get("B")
    b = vmaf.get("VD|kirp|2000", {}).get("B")
    r.append("| B yontemi p10, kirpmasiz | %s |" % (s(a["p10"], 3) if a else "-"))
    r.append("| B yontemi p10, 804'e kirpilip geri doldurulmus | %s |" % (s(b["p10"], 3) if b else "-"))
    r.append("| B yontemi en kotu kare, kirpmasiz | %s |" % (s(a["en_kotu"], 3) if a else "-"))
    r.append("| B yontemi en kotu kare, kirpilmis | %s |" % (s(b["en_kotu"], 3) if b else "-"))
    r.append("")
    r.append("**(c) Yanlis kirpmanin bedeli.** `limit=64`'te cropdetect'in kenarsiz "
             "kaynaklarda onerdigi hatali kirpma uygulandi, cikti geri doldurulup "
             "tam karede puanlandi (B yontemi, 2000k):")
    r.append("")
    r.append("| Kaynak | Hatali kirpma | Kesilen piksel | Kirpmasiz p10 | Hatali kirpilmis p10 | Fark |")
    r.append("|---|---|---|---|---|---|")
    for ad, kes in (("NA", "1920:1042:0:4"), ("NB", "1920:1072:0:6")):
        a = vmaf.get("%s|duz|2000" % ad, {}).get("B")
        b2 = vmaf.get("%s|yanlis|2000" % ad, {}).get("B")
        d = None if not (a and b2) else b2["p10"] - a["p10"]
        r.append("| %s | %s | %d satir | %s | %s | %s |" % (
            ADLAR[ad], kes, 1080 - int(kes.split(":")[1]),
            s(a["p10"], 3) if a else "-", s(b2["p10"], 3) if b2 else "-", s(d, 3)))
    return "\n".join(r)


def hukum(vmaf, kanit, yokla):
    k = {ad: kazanc(vmaf, ad, ANA_HIZ) for ad in LETTERBOX}
    var = [v for v in k.values() if v is not None]
    if len(var) < len(LETTERBOX):
        return "Hukum uretilemedi: %d/%d kaynakta %dk olcumu eksik." % (
            len(var), len(LETTERBOX), ANA_HIZ)
    ort = sum(var) / len(var)
    poz = sum(1 for v in var if v > 0)

    vetolar = []
    yanlis = []
    for ad in KENARSIZ:
        d = yokla[ad]
        kutular = [d["nokta"]["t00"]["kutu"], d["nokta"]["t05"]["kutu"],
                   d["nokta"]["t10"]["kutu"], d["nokta"]["t15"]["kutu"],
                   d["yayilmis"]["kutu"], d["tam"]["kutu"]]
        if any(kk and tuple(kk) != (1920, 1080, 0, 0) for kk in kutular):
            yanlis.append(ad)
    if yanlis:
        vetolar.append("yanlis kirpma vetosu tetiklendi (%s)" % ", ".join(yanlis))
    en_uzun = max(yokla[ad]["yayilmis"]["sure"] for ad in LETTERBOX + KENARSIZ)
    if en_uzun > ESIK_SURE:
        vetolar.append("yoklama maliyeti vetosu tetiklendi (10 kare yayilmis yoklama en kotu %s sn, esik %s sn)"
                       % (s(en_uzun, 2), s(ESIK_SURE, 1)))

    pay = ("n=%d letterbox'li kaynak, kaynak basina %d kare, VMAF-NEG p10 aktif goruntu alaninda, %dk 2 gecis, teslim boyutu esitlenmis"
           % (len(var), kanit[LETTERBOX[0]]["kare"], ANA_HIZ))

    if ort >= ESIK_DEGER and poz >= 3:
        bas = "**Kirpma degerdir.**"
        govde = ("Dort letterbox'li kaynakta 2000k'da p10 kazanci ortalama %s puan (%s); %d/%d kaynakta kazanc pozitif; K4'un +%s esigi asildi."
                 % (s(ort, 3), pay, poz, len(var), s(ESIK_DEGER)))
    elif ort >= ESIK_TABAN:
        gecen = [(kanit[ad]["bant_yuzde"], ad) for ad in LETTERBOX
                 if k[ad] is not None and k[ad] >= ESIK_DEGER]
        if gecen:
            kesim = min(g[0] for g in gecen)
            sinif = "bant yuzdesi %%%s ve uzeri olan kaynak sinifinda" % s(kesim)
        else:
            sinif = "hicbir kaynak tek basina +%s'i gecmedigi icin sinif siniri cizilemedi" % s(ESIK_DEGER)
        bas = "**Kirpma %s degerdir.**" % sinif
        govde = ("Ortalama p10 kazanci %s puan (%s), K4'un +%s tabani ile +%s esigi arasinda; %d/%d kaynakta kazanc pozitif."
                 % (s(ort, 3), pay, s(ESIK_TABAN), s(ESIK_DEGER), poz, len(var)))
    else:
        bas = "**Kirpma degmez.**"
        govde = ("Ortalama p10 kazanci %s puan (%s), K4'un +%s tabaninin altinda; %d/%d kaynakta kazanc pozitif."
                 % (s(ort, 3), pay, s(ESIK_TABAN), poz, len(var)))

    tek = " ".join("%s %s" % (ad, s(k[ad], 3)) for ad in LETTERBOX)
    satir = [bas + " " + govde, "", "Kaynak basina kazanc (2000k, p10): " + tek + "."]
    if vetolar:
        satir += ["", "**Veto:** " + "; ".join(vetolar) +
                  ". Hukum ne olursa olsun otomatik kirpma varsayilan acik gelemez."]
    else:
        satir += ["", "K4'un iki vetosu da tetiklenmedi."]
    return "\n".join(satir)


def yaz(metin, ad):
    with open(RAPOR, encoding="utf-8") as f:
        r = f.read()
    b = "<!-- BETIK-%s-BASLANGIC -->" % ad
    e = "<!-- BETIK-%s-BITIS -->" % ad
    if b not in r or e not in r:
        print("ISARET YOK:", ad)
        return
    i = r.index(b) + len(b)
    j = r.index(e)
    r = r[:i] + "\n" + metin + "\n" + r[j:]
    with open(RAPOR, "w", encoding="utf-8") as f:
        f.write(r)


def main():
    vmaf = yuk(IS + "/olcu/vmaf.json") or {}
    kanit = yuk(IS + "/olcu/kaynak-kanit.json") or {}
    yokla = yuk(IS + "/yokla/cropdetect.json") or {}
    if not (vmaf and kanit and yokla):
        print("EKSIK VERI: vmaf=%s kanit=%s yokla=%s" % (bool(vmaf), bool(kanit), bool(yokla)))
        return 1
    h = hukum(vmaf, kanit, yokla)
    yaz(h, "HUKUM")
    yaz(kanit_tablo(kanit), "KANIT")
    yaz(k1_tablo(kanit), "K1")
    yaz(k2_tablo(yokla), "K2")
    yaz(k2b_tablo(yokla), "K2B")
    yaz(limit_tablo(yuk(IS + "/yokla/limit.json")), "LIMIT")
    yaz(sure_tablo(yuk(IS + "/yokla/sure.json")), "SURE")
    yaz(k3_tablo(vmaf, kanit), "K3")
    yaz(k5_tablo(vmaf, yokla), "K5")
    print(h)
    return 0


if __name__ == "__main__":
    sys.exit(main())
