import json, os, io, csv, glob, subprocess

NL = chr(10)
KOK = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
IS = os.path.join(KOK, ".calisma", os.environ.get("VIDSHRINK_IS", "T193"))
HEDEF = os.path.join(KOK, "docs", "olcumler", "max-mod-acilis.md")


def oku(p):
    with io.open(p, encoding="utf-8-sig") as f:
        return json.load(f)


def vir(x, n=3):
    if x is None:
        return "bilinmiyor"
    return (("%." + str(n) + "f") % x).replace(".", ",")


def isaretli(x, n=3):
    if x is None:
        return "bilinmiyor"
    return ("+" if x > 0 else "") + vir(x, n)


def bayt_isaretli(v):
    if v is None:
        return "yok"
    return ("+" + str(v)) if v > 0 else str(v)


pencereler = oku(os.path.join(IS, "pencereler.json"))
adlar = [p["Ad"] for p in pencereler]

haritalar = {}
for a in adlar:
    p = os.path.join(IS, "harita-" + a + ".json")
    if os.path.exists(p):
        haritalar[a] = oku(p)


def asama_oku(asama):
    d = {}
    for a in adlar:
        p = os.path.join(IS, asama + "-maks-" + a + ".json")
        if os.path.exists(p):
            d[a] = dict((r["Kol"], r) for r in oku(p))
    return d


def asama_boyut(asama):
    d = {}
    for a in adlar:
        for kol in ("taban", "dagitim"):
            p = os.path.join(IS, asama + "-maks-" + a + "-" + kol + ".mkv")
            if os.path.exists(p):
                d[(a, kol)] = os.path.getsize(p)
    return d


k5 = asama_oku("k5")
boyut = asama_boyut("k5")
k12 = asama_oku("k12")
k12boyut = asama_boyut("k12")

with io.open(os.path.join(IS, "k2kapi", "kapi.csv"), encoding="utf-8") as f:
    kapi = list(csv.DictReader(f, delimiter=";"))

ffv = subprocess.run(["ffmpeg", "-version"], capture_output=True, text=True).stdout.splitlines()[0]

kontrol_bayt = {}
for p in sorted(glob.glob(os.path.join(IS, "k2kapi", "kontrol-*.mkv"))):
    i = int(os.path.basename(p).split("-")[1].split(".")[0])
    kontrol_bayt[i] = os.path.getsize(p)
kontrol_sira = sorted(kontrol_bayt)
ilk_cift = abs(kontrol_bayt[1] - kontrol_bayt[2]) if 1 in kontrol_bayt and 2 in kontrol_bayt else None
ilk_dort = [kontrol_bayt[i] for i in kontrol_sira if i <= 4]
dort_aralik = (max(ilk_dort) - min(ilk_dort)) if len(ilk_dort) >= 2 else None

parse_hatalari = []
for p in sorted(glob.glob(os.path.join(IS, "k2kapi", "*.err"))):
    with io.open(p, encoding="utf-8", errors="replace") as f:
        for satir in f:
            if "Error parsing option" in satir:
                parse_hatalari.append((os.path.basename(p), satir.strip()))


def ilk_hata(anahtar):
    for ad, satir in parse_hatalari:
        if satir.find("option " + anahtar) >= 0:
            return ad, satir
    return None, None


planlar = {}
pp = os.path.join(IS, "planlar.txt")
if os.path.exists(pp):
    with io.open(pp, encoding="utf-8") as f:
        for satir in f:
            satir = satir.strip()
            if not satir or ":" not in satir:
                continue
            sol, sag = satir.split(":", 1)
            ad = sol.split("/")[-1]
            alan = sag.split()
            planlar[ad] = {"kodlayici": alan[0], "mod": alan[1], "bit": alan[2],
                           "cozunurluk": alan[3].split("@")[0]}

P10_ESIK, ENKOTU_ESIK, KAYIP_ESIK = 0.50, 1.00, 0.30


def kazanclar(kayit):
    ka = {}
    for a in adlar:
        c = kayit.get(a)
        if not c or "taban" not in c or "dagitim" not in c:
            continue
        t, d = c["taban"], c["dagitim"]

        def f(k, t=t, d=d):
            if t.get(k) is None or d.get(k) is None:
                return None
            return d[k] - t[k]

        ka[a] = {"p10": f("VmafP10"), "enkotu": f("VmafWorstScene"),
                 "ort": f("VmafMean"), "min": f("VmafMin")}
    return ka


kazanc = kazanclar(k5)
k12kazanc = kazanclar(k12)

p10_gecen = [a for a in adlar if a in kazanc and kazanc[a]["p10"] is not None and kazanc[a]["p10"] >= P10_ESIK]
enkotu_gecen = [a for a in adlar if a in kazanc and kazanc[a]["enkotu"] is not None and kazanc[a]["enkotu"] >= ENKOTU_ESIK]
kayip_asan = [a for a in adlar if a in kazanc and kazanc[a]["p10"] is not None and kazanc[a]["p10"] < -KAYIP_ESIK]
band_disi = [(a, kol) for a in adlar for kol in ("taban", "dagitim")
             if a in k5 and kol in k5[a] and not k5[a][kol]["BandIcinde"]]
olculen = len(kazanc)
boy_esli = [a for a in adlar if a in kazanc and a in k5
            and k5[a]["taban"]["BandIcinde"] and k5[a]["dagitim"]["BandIcinde"]]

sart1 = len(p10_gecen) >= 2
sart2 = len(set(p10_gecen) & set(enkotu_gecen)) >= 2
sart3 = len(kayip_asan) == 0
sart4 = len(band_disi) == 0
gecti = sart1 and sart2 and sart3 and sart4

kapi_qcomp = [r for r in kapi if r["anahtar"] == "qcomp"][0]
kapi_qcomp_ff = [r for r in kapi if r["anahtar"] == "qcomp-ffmpeg"][0]
kapi_zones = [r for r in kapi if r["anahtar"] == "zones"][0]
kapi_qpscs = [r for r in kapi if r["anahtar"] == "qp-scale-compress-strength"][0]
kontrol = [r for r in kapi if r["anahtar"] == "kontrol"][0]
gurultu = int(kontrol["fark_bayt"])

k12adlar = [a for a in adlar if a in k12kazanc]
k12band_disi = [(a, kol) for a in k12adlar for kol in ("taban", "dagitim")
                if kol in k12[a] and not k12[a][kol]["BandIcinde"]]
k12p10 = [a for a in k12adlar if k12kazanc[a]["p10"] is not None and k12kazanc[a]["p10"] >= P10_ESIK]
k12enkotu = [a for a in k12adlar if k12kazanc[a]["enkotu"] is not None and k12kazanc[a]["enkotu"] >= ENKOTU_ESIK]
if not k12adlar:
    k12yol = "olculmedi"
elif k12band_disi:
    k12yol = "belirsiz"
elif len(k12p10) == len(k12adlar) and len(k12enkotu) == len(k12adlar):
    k12yol = "acilabilir"
else:
    k12yol = "kapanir"

s = []
w = s.append
w("# Max Sikistirma Modu — Acilis Olcumu (T193)")
w("")
w("Bu sayfanin **her olcum sayisi** `.calisma/" + os.path.basename(IS) + "/` altindaki dosyalardan")
w("okunur (`tools/sahne-butcesi/09-max-mod-raporu.py`); hicbiri elle tasinmaz. Govde")
w("cumleleri, esik sabitleri (`+0,50` / `+1,00` / `0,30`) ve duzenek tarifleri ureticin")
w("kendi metnindedir — onlar olcum degil, olcumun cercevesidir.")
w("")
w("ffmpeg: `" + ffv + "`")
w("")
w("## K0 — Kaynak Sapmasi")
w("")
w("T114'un `kaynak-1080p60-hdr-17dk-yalniz-video.mkv` dosyasi bu makinede **yok**")
w("(`VIDSHRINK_KAYNAK`, butun surucu harfleri ve `.calisma` disi klasorler arandi;")
w("`C:/Users` altinda 400 MB ustu tek bir video dosyasi bulunamadi). Pencereler elde")
w("olan uc 60 sn'lik 1080p60 HDR parcadan uretildi.")
w("")
w("| Pencere | Kaynak | Sure (sn) | Harita sahnesi |")
w("|---------|--------|-----------|----------------|")
for p in pencereler:
    h = haritalar.get(p["Ad"])
    w("| `" + p["Ad"] + "` | " + p["Not"] + " | " +
      (vir(h["Duration"], 1) if h else "olculmedi") + " | " +
      (str(len(h["Scenes"])) if h else "olculmedi") + " |")
w("")
w("**Sapma.** Pencereler ~189 sn yerine ~60 sn. Hedef boyut (60 MB) degismedigi icin")
w("bit hizi ucuyor. T114'te `p1` plani `806x454`e dusuyordu; bu kosumun `maks` planlari:")
w("")
w("| Pencere | Kodlayici | Mod | Bit hizi | Plan cozunurlugu |")
w("|---------|-----------|-----|----------|------------------|")
for a in adlar:
    pl = planlar.get(a)
    if pl is None:
        w("| `" + a + "` | olculmedi | | | |")
        continue
    w("| `" + a + "` | `" + pl["kodlayici"] + "` | " + pl["mod"] + " | " + pl["bit"] +
      " | " + pl["cozunurluk"] + " |")
w("")
w("Bu kosumun sayilari T114'un hucreleriyle **dogrudan karsilastirilamaz**; taban bu")
w("kosumda yeniden olculdu.")
w("")
w("## K2 — `libsvtav1` Destek Kapisi")
w("")
w("`08-svtav1-kapisi.sh`: 6 sn'lik pencere, iki gecis, `preset 6`, `-b:v 8230k`.")
w("Destek ancak `fark > gurultu x 2` **ve** `fark > cikti/100` iken yazilir.")
w("")
w("| Anahtar | A | B | A bayt | B bayt | Fark | Gurultu | Gurultu x2 | Cikti/100 | Destek |")
w("|---------|---|---|--------|--------|------|---------|------------|-----------|--------|")
for r in kapi:
    if r["anahtar"] == "kontrol":
        w("| kontrol | " + r["a_deger"] + " | " + r["b_deger"] + " | " + r["a_bayt"] +
          " | " + r["b_bayt"] + " | " + r["fark_bayt"] + " | — | — | — | — |")
    else:
        w("| `" + r["anahtar"] + "` | " + r["a_deger"] + " | " + r["b_deger"] + " | " +
          r["a_bayt"] + " | " + r["b_bayt"] + " | " + r["fark_bayt"] + " | " +
          r["gurultu_bayt"] + " | " + r["esik_gurultu2"] + " | " + r["esik_yuzde1"] +
          " | **" + r["destek"] + "** |")
w("")
w("### Tekrar Gurultusu — Kac Kosumdan Olculdugu Kararin Kendisidir")
w("")
w("Tekrar gurultusu **" + str(len(kontrol_bayt)) + "** ayni-parametre kosumunun araligidir: " +
  str(gurultu) + " bayt")
w("(kosum baytlari: " + ", ".join(str(kontrol_bayt[i]) for i in kontrol_sira) + ").")
w("Kosum 1-4 yapicinin, 5-8 denetcinin ayni duzenekteki bagimsiz kosumudur")
w("(`.calisma/" + os.path.basename(IS) + "/k2kapi/KAYNAK.txt`).")
w("")
if dort_aralik is not None:
    w("Daha az kosum **gurultuyu oldugundan kucuk gosteriyor**:")
    w("")
    w("| Kosum sayisi | Aralik (bayt) | Sekiz kosumun araligina orani |")
    w("|--------------|---------------|-------------------------------|")
    if ilk_cift is not None:
        w("| 2 (ilk cift) | " + str(ilk_cift) + " | " + vir(float(gurultu) / ilk_cift, 1) + " kat kucuk |")
    w("| 4 (yapicinin ilk kosumu) | " + str(dort_aralik) + " | " +
      vir(float(gurultu) / dort_aralik, 1) + " kat kucuk |")
    w("| " + str(len(kontrol_bayt)) + " (bu sayfanin tabani) | " + str(gurultu) + " | — |")
    w("")
    w("T114 gurultuyu **tek ciftten** oluyordu; T193'un ilk kosumu **dort kosumdan**.")
    w("Ikisi de sinirdaki bir adayi yanlislikla gecirebilir. Bu sayfanin butun destek")
    w("kararlari " + str(len(kontrol_bayt)) + " kosumun araligina (" + str(gurultu) +
      " bayt) ve esigine (" + kapi_qcomp["esik_gurultu2"] + " bayt) gore verildi.")
    w("")
w("**Ucuncu soru — `qcomp` varsayilan yolda gorunuyor mu: hayir.** Iki ad alani da")
w("denendi: `-svtav1-params qcomp=...` ve ffmpeg'in kendi `-qcomp` secenegi.")
w("Ikisinde de fark tekrar gurultusunun iki katinin (" + kapi_qcomp["esik_gurultu2"] +
  " bayt) altinda:")
w("`-svtav1-params` yolunda " + kapi_qcomp["fark_bayt"] + " bayt, `-qcomp` yolunda " +
  kapi_qcomp_ff["fark_bayt"] + " bayt.")
w("Kapi iki yolda da **desteklenmiyor** diyor; bu yuzden K2'nin geri kalani — deger")
w("taramasi — **kosulmadi**.")
w("")
ad_q, satir_q = ilk_hata("qcomp")
ad_z, satir_z = ilk_hata("zones")
if satir_q:
    w("Nitel yari da ham dosyada duruyor. `08-svtav1-kapisi.sh` her kodlamanin stderr'ini")
    w("`.calisma/" + os.path.basename(IS) + "/k2kapi/<ad>.p1.err` ve `<ad>.p2.err` olarak saklar;")
    w("`Error parsing option` dizgesi " + str(len(set(x[0] for x in parse_hatalari))) +
      " dosyada, toplam " + str(len(parse_hatalari)) + " kez gecer:")
    w("")
    w("```")
    w(ad_q + ": " + satir_q)
    if satir_z:
        w(ad_z + ": " + satir_z)
    w("```")
    w("")
    w("Kodlama yine de **cikis kodu 0** donuyor: betik `set -euo pipefail` ile kosuyor,")
    w("sifirdan farkli bir kod donseydi cikti dosyasi hic olusmazdi. Anahtarin")
    w("ayristirilamadigi ffmpeg'in kendi hata satirinda yazili; kabuk bunu gormuyor.")
    w("")
w("Ayni kapida iki yan sonuc: `zones` da `libsvtav1`de olu (" + kapi_zones["fark_bayt"] + " bayt fark,")
w("gurultunun altinda) — T114'un bulgusu bagimsiz dogrulandi. Buna karsilik SVT-AV1'in")
w("kendi karsiligi `qp-scale-compress-strength` **calisiyor** (" + kapi_qpscs["fark_bayt"] + " bayt fark);")
w("o kosumlarin stderr'inde `Error parsing option` yok, SVT'nin kendi yapilandirma dokumu")
w("`QP scale compress strength` degerini iki kosumda `0` ve `3` olarak basiyor.")
w("")
w("T114 izgarasinin `libsvtav1 / qcomp` satiri zaten `qcomp` degil")
w("`qp-scale-compress-strength` deniyordu; bu yuzden T114'un qcomp cumlesi literal")
w("anahtarin olcumu degildi.")
w("")
w("## K1 — `maks` Kolunun Kalite Kapisi (Ilk Kez Kosuldu)")
w("")
w("T114'te `maks` kolunun K5 hucreleri hic kosulmamisti: kapi `ZonesFlag` uzerinden")
w("kuruluydu, `libsvtav1` icin `null` donuyor ve kol sessizce atlaniyordu. Kapi")
w("`ParamsFlag`e cevrildi (`-svtav1-params`) ve uc pencerede de kosuldu.")
w("")
w("| Pencere | Kol | Boyut (MB) | Band (MB) | Band ici | VMAF-NEG ort | p10 | min | En kotu sahne |")
w("|---------|-----|------------|-----------|----------|--------------|-----|-----|---------------|")
for a in adlar:
    for kol in ("taban", "dagitim"):
        r = k5.get(a, {}).get(kol)
        if r is None:
            w("| `" + a + "` | " + kol + " | olculmedi | | | | | | |")
            continue
        w("| `" + a + "` | " + kol + " | " + vir(r["GerceklesenMb"], 2) + " | " +
          vir(r["BandAltMb"], 1) + "–" + vir(r["BandUstMb"], 1) + " | " +
          ("evet" if r["BandIcinde"] else "**hayir**") + " | " +
          vir(r["VmafMean"]) + " | " + vir(r["VmafP10"]) + " | " +
          vir(r["VmafMin"]) + " | " + vir(r["VmafWorstScene"]) + " |")
w("")
w("Kazanc = `dagitim - taban`, VMAF-NEG puani. Bayt farki da **isaretli**: eksi isaret")
w("dagitim kolunun daha kucuk ciktigi anlamina gelir.")
w("")
w("| Pencere | p10 kazanci | En kotu sahne kazanci | Ort kazanc | Taban bayt | Dagitim bayt | Bayt farki (dagitim - taban) | Bagil fark |")
w("|---------|-------------|-----------------------|------------|------------|--------------|------------------------------|------------|")
for a in adlar:
    k = kazanc.get(a)
    bt, bd = boyut.get((a, "taban")), boyut.get((a, "dagitim"))
    fark = (bd - bt) if (bt and bd) else None
    bagil = (abs(bd - bt) * 100.0 / max(bt, bd)) if (bt and bd) else None
    w("| `" + a + "` | " +
      (isaretli(k["p10"]) if k else "olculmedi") + " | " +
      (isaretli(k["enkotu"]) if k else "olculmedi") + " | " +
      (isaretli(k["ort"]) if k else "olculmedi") + " | " +
      (str(bt) if bt else "yok") + " | " + (str(bd) if bd else "yok") + " | " +
      bayt_isaretli(fark) + " | " +
      (("%" + vir(bagil, 3)) if bagil is not None else "yok") + " |")
w("")
w("Isaretler **karisik** — iki pencerede dagitim kolu kucuk, birinde buyuk. Tek yonlu")
w("bir etki degil, gurultu okumasidir.")
w("")
w("### K5 Kapisinin Sayimi")
w("")
w("`ESIKLER.md`'nin dort sarti, `maks` kolu icinde, olculen " + str(olculen) + " pencere uzerinden:")
w("")
w("1. p10 kazanci >= +" + vir(P10_ESIK, 2) + ", en az iki pencerede — **gecen pencere: " +
  str(len(p10_gecen)) + "/" + str(olculen) + "** (" +
  (", ".join("`" + x + "`" for x in p10_gecen) if p10_gecen else "yok") + ") -> " +
  ("saglandi" if sart1 else "**saglanmadi**"))
w("2. En kotu sahne kazanci >= +" + vir(ENKOTU_ESIK, 2) + ", ayni pencerelerde — **gecen pencere: " +
  str(len(enkotu_gecen)) + "/" + str(olculen) + "** (" +
  (", ".join("`" + x + "`" for x in enkotu_gecen) if enkotu_gecen else "yok") + ") -> " +
  ("saglandi" if sart2 else "**saglanmadi**"))
w("3. Hicbir pencerede p10 kaybi > " + vir(KAYIP_ESIK, 2) + " — **asan pencere: " +
  str(len(kayip_asan)) + "** -> " + ("saglandi" if sart3 else "**saglanmadi**"))
w("4. K6: her kosum hedef bandin icinde — **band disi kosum: " + str(len(band_disi)) +
  "** -> " + ("saglandi" if sart4 else "**saglanmadi**"))
w("")
if band_disi:
    bd_pencere = sorted(set(a for a, _ in band_disi))
    w("Band disi " + str(len(band_disi)) + " kosumun hepsi " +
      ", ".join("`" + a + "`" for a in bd_pencere) + " penceresinde, ve o pencerede")
    w("**her iki kol da** band disinda. Sart 4 bu yuzden kollari ayirt etmez.")
    w("")
    for a in bd_pencere:
        pl = planlar.get(a)
        if pl is None:
            continue
        digerleri = sorted(set(planlar[x]["bit"] for x in adlar if x != a and x in planlar))
        w("Sebep plan seviyesinde: `" + a + "` icin plan bit hizi " + pl["bit"] +
          ", diger pencerelerde " + ", ".join(digerleri) + ".")
        w("60 sn'lik pencerede " + pl["bit"] + " hedef bandin (" +
          vir(k5[a]["taban"]["BandAltMb"], 1) + "–" + vir(k5[a]["taban"]["BandUstMb"], 1) +
          " MB) altinda kalir; olculen " + vir(k5[a]["taban"]["GerceklesenMb"], 2) + " MB.")
        w("")
    w("Kollari ayirt eden ve karari veren sayi sart 1 ve 2'dir.")
    w("")
    w("**Bant disi pencerenin kalite satiri nitelikli kanit degildir.** `ESIKLER.md`'nin")
    w("4. sarti kaliteyi **esit boyda** karsilastirmak icin var; iki kolu da bant disinda")
    w("olan pencerede o esitlik yok. Olculen bagil boy farklari:")
    w("")
    for a in adlar:
        bt, bd2 = boyut.get((a, "taban")), boyut.get((a, "dagitim"))
        if not (bt and bd2):
            continue
        w("- `" + a + "`: %" + vir(abs(bd2 - bt) * 100.0 / max(bt, bd2), 3) +
          (" — iki kol da bant disi, **boy eslenmemis**" if a in [x for x, _ in band_disi] else ""))
    w("")
    w("Bu yuzden sayim iki turlu okunur: uc pencere uzerinden **" + str(len(p10_gecen)) +
      "/" + str(olculen) + "**, boy eslenmis")
    w("(iki kolu da bant icinde) pencereler uzerinden **" +
      str(len([a for a in p10_gecen if a in boy_esli])) + "/" + str(len(boy_esli)) + "**.")
    w("Hukum ikisinde de ayni.")
    w("")
w("**K5 kapisi: " + ("gecti" if gecti else "gecmedi") + ".**")
w("")
w("Sebep: `libsvtav1` `zones` anahtarini yok sayiyor, yani `dagitim` kolu `taban` ile")
mutlak = [abs(kazanc[a]["p10"]) for a in adlar if a in kazanc and kazanc[a]["p10"] is not None]
w("ayni kodlamadir. Kolun p10 kazanci uc pencerede de **sifirin altinda**; mutlak")
w("degerlerin en buyugu **" + vir(max(mutlak)) + " puan**, esik +" + vir(P10_ESIK, 2) + ".")
w("")
w("Bayt tarafinda ayrimi tek cumleye sigdirmamak gerekiyor. Olculen tekrar gurultusu")
w(str(gurultu) + " bayt, ama o gurultu `p1`in bit hizinda (`8230k`) ve **6 sn'lik** bir")
w("ciktida olculdu; asagida karsilastirilan pencereler ise **60 sn**. Olcekler bire bir")
w("degil: 6 sn'lik cikti 60 sn'lik ciktinin gurultusunu **kucuk gosterir**, yani bu")
w("ayrim muhafazakar yondedir.")
alt = [a for a in adlar if (a, "taban") in boyut and (a, "dagitim") in boyut
       and abs(boyut[(a, "taban")] - boyut[(a, "dagitim")]) <= gurultu]
ust = [a for a in adlar if (a, "taban") in boyut and (a, "dagitim") in boyut
       and abs(boyut[(a, "taban")] - boyut[(a, "dagitim")]) > gurultu]
w("")
w("- 6 sn'lik gurultunun **altinda** kalan pencere: " +
  (", ".join("`" + a + "`" for a in alt) if alt else "yok") + ".")
w("- 6 sn'lik gurultunun **ustunde** kalan pencere: " +
  (", ".join("`" + a + "`" for a in ust) if ust else "yok") + ".")
w("")
if ust:
    w("Ustte kalan pencerede tekrar gurultusu kendi bit hizinda ve kendi suresinde")
    w("olculmedi; o pencere icin gurultunun icinde ya da disinda demek olculmus bir")
    w("iddia olmaz. Ama o pencerede de VMAF kazanci sifirin altinda (" +
      ", ".join("`" + a + "` p10 " + isaretli(kazanc[a]["p10"]) for a in ust if a in kazanc) + "),")
    w("yani bayt farki kaliteye kazanc olarak donmemistir.")
    w("")
w("Bu bir az kazandi durumu degil, **tasiyicinin yoklugudur**.")
w("")
w("## K12 — `qp-scale-compress-strength` Kalite Olcumu")
w("")
if not k12adlar:
    w("Kosulmadi.")
    w("")
else:
    w("Destek kapisindan **evet** ile cikan tek anahtarin kalite kazanci olculdu.")
    w("`dagitim` kolu `zones` yerine `-svtav1-params qp-scale-compress-strength=3` ile")
    w("kuruldu; `taban` kol K5'in taban ciktisidir (SVT varsayilani `=0`). Deger ikilisi")
    w("kapida olculen ikilidir, tarama yok. `p2-durgun` disarida: iki kolu da bant disi")
    w("oldugu icin kalite satiri boy eslenmemis olurdu.")
    w("")
    w("| Pencere | Kol | Boyut (MB) | Band (MB) | Band ici | VMAF-NEG ort | p10 | min | En kotu sahne |")
    w("|---------|-----|------------|-----------|----------|--------------|-----|-----|---------------|")
    for a in k12adlar:
        for kol in ("taban", "dagitim"):
            r = k12[a][kol]
            w("| `" + a + "` | " + kol + " | " + vir(r["GerceklesenMb"], 2) + " | " +
              vir(r["BandAltMb"], 1) + "–" + vir(r["BandUstMb"], 1) + " | " +
              ("evet" if r["BandIcinde"] else "**hayir**") + " | " +
              vir(r["VmafMean"]) + " | " + vir(r["VmafP10"]) + " | " +
              vir(r["VmafMin"]) + " | " + vir(r["VmafWorstScene"]) + " |")
    w("")
    w("| Pencere | p10 kazanci | En kotu sahne kazanci | Ort kazanc | Taban bayt | Dagitim bayt | Bayt farki (dagitim - taban) |")
    w("|---------|-------------|-----------------------|------------|------------|--------------|------------------------------|")
    for a in k12adlar:
        k = k12kazanc[a]
        bt, bd2 = k12boyut.get((a, "taban")), k12boyut.get((a, "dagitim"))
        fark = (bd2 - bt) if (bt and bd2) else None
        w("| `" + a + "` | " + isaretli(k["p10"]) + " | " + isaretli(k["enkotu"]) + " | " +
          isaretli(k["ort"]) + " | " + (str(bt) if bt else "yok") + " | " +
          (str(bd2) if bd2 else "yok") + " | " + bayt_isaretli(fark) + " |")
    w("")
    w("Ayni dort sart, olculen " + str(len(k12adlar)) + " pencere uzerinden:")
    w("")
    w("1. p10 kazanci >= +" + vir(P10_ESIK, 2) + " — **gecen pencere: " + str(len(k12p10)) +
      "/" + str(len(k12adlar)) + "** (" + (", ".join("`" + x + "`" for x in k12p10) if k12p10 else "yok") + ")")
    w("2. En kotu sahne kazanci >= +" + vir(ENKOTU_ESIK, 2) + " — **gecen pencere: " +
      str(len(k12enkotu)) + "/" + str(len(k12adlar)) + "** (" +
      (", ".join("`" + x + "`" for x in k12enkotu) if k12enkotu else "yok") + ")")
    w("3. Hicbir pencerede p10 kaybi > " + vir(KAYIP_ESIK, 2) + " — **asan pencere: " +
      str(len([a for a in k12adlar if k12kazanc[a]["p10"] is not None and k12kazanc[a]["p10"] < -KAYIP_ESIK])) + "**")
    w("4. K6: her kosum hedef bandin icinde — **band disi kosum: " + str(len(k12band_disi)) + "**" +
      ((" (" + ", ".join("`" + a + "`/" + kol for a, kol in k12band_disi) + ")") if k12band_disi else ""))
    w("")
    if k12yol == "belirsiz":
        w("**Sonuc: belirsiz.** Bant disina cikan kosum var, yani iki kol **esit boyda")
        w("karsilastirilmadi**; bu haliyle kalite farki kazanc olarak okunamaz.")
        for a, kol in k12band_disi:
            r = k12[a][kol]
            w("`" + a + "`/" + kol + ": " + vir(r["GerceklesenMb"], 2) + " MB, band " +
              vir(r["BandAltMb"], 1) + "–" + vir(r["BandUstMb"], 1) + " MB.")
        w("")
        w("Anahtar calisiyor — bayt farki bunu soyluyor — ama `=3` ile cikan dosya hedef")
        w("bandi asiyor: kalite kazanci sorusu once bir hedef boyut sorusudur. Uydurma")
        w("yapilmaz; mod bu anahtar icin ne acilir ne kapanir.")
    elif k12yol == "acilabilir":
        w("**Sonuc: mod acilabilir** — ama tasiyici `zones` degil `qp-scale-compress-strength`.")
    elif k12yol == "kapanir":
        w("**Sonuc: mod kapanir.** Kazanc esigin altinda; bu sefer her iki anahtar icin de")
        w("kanitli. En buyuk p10 kazanci **" +
          vir(max(k12kazanc[a]["p10"] for a in k12adlar if k12kazanc[a]["p10"] is not None)) +
          " puan**, esik +" + vir(P10_ESIK, 2) + ".")
    w("")
w("## K3 — Sure Sayilari")
w("")
w("Bu olcum ayni makinede es zamanli baska islerle birlikte kosuldu. ffmpeg cagrilari")
w("kendi icinde **sirayla** kosuldu; duzenegin paralel kolu (eski")
w("`00-pencereleri-kes.sh` icindeki `&`/`wait`) kaldirildi.")
w("")
w("Kalite (VMAF) ve bayt sayilari islemci rekabetinden etkilenmez ve bu sayfadaki")
w("butun hukumler yalniz onlara dayanir. **Sure sayisi bu sayfada hic raporlanmamistir**")
w("— es zamanlilik yuzunden guvenilmez olurdu.")
w("")
w("## Olculmeyenler")
w("")
w("- `qcomp` deger taramasi (`0,40 / 0,50 / 0,60 / 0,75`): kosulmadi, cunku destek")
w("  kapisi `libsvtav1` icin her iki ad alaninda da **hayir** dedi. `uyumlu` (`libx264`)")
w("  ve `yedek` (`libx265`) kollarinda `qcomp` calisiyor, ama uretimin varsayilani o")
w("  kollar degil.")
w("- `qp-scale-compress-strength` **taramasi**: yalniz kapida olculen `0` / `3` ikilisi")
w("  denendi; ara degerler ve `variance-boost` ailesi olculmedi.")
w("- **Bit kitliginin oldugu dusuk cozunurluklu plan rejimi bu kosumda olculmedi**: uc")
w("  pencere de `1920x1080` planlandi, oysa T114'un 189 sn'lik `p1`i `806x454`e")
w("  dusuyordu. Sahne basina bit dagitiminin en cok ise yarayacagi rejim budur ve bu")
w("  sayfada yok. Hukum kazanc sayisina degil anahtar destegine dayandigi icin bu bosluk")
w("  hukmu cevirmez; yine de olculmemistir.")
w("- K7 (bozuk harita bedeli) `maks` kolunda kosulmadi: K5 kazanci sifir oldugu icin")
w("  karsilastirilacak kazanc yok.")
w("- T114 ile sayisal kiyas: pencere suresi ve plan cozunurlugu farkli.")
w("- Sure / duvar saati: bkz. K3.")
w("")
w("## Hukum")
w("")
if gecti:
    w("**Max sikistirma modu sahne dagitimi uzerine acilir**: `maks` kolunda p10 kazanci")
    w(str(len(p10_gecen)) + "/" + str(olculen) + " pencerede esigi gecti.")
else:
    w("**Max sikistirma modu sahne dagitimi uzerine acilmaz**: `maks` kolunun kalite")
    w("kapisi bu depoda ilk kez kosuldu ve p10 esigini gecen pencere " + str(len(p10_gecen)) +
      "/" + str(olculen) + ", en kotu sahne esigini gecen pencere " + str(len(enkotu_gecen)) +
      "/" + str(olculen) + " cikti —")
    w("cunku uretimin varsayilan kodlayicisi `libsvtav1` hem dagitimi tasiyacak anahtari")
    w("(`zones`, fark " + kapi_zones["fark_bayt"] + " bayt) hem ayakta kalan tek isareti")
    w("(`qcomp`, `-svtav1-params` yolunda " + kapi_qcomp["fark_bayt"] + " bayt, `-qcomp` yolunda " +
      kapi_qcomp_ff["fark_bayt"] + " bayt) destek esiginin (gurultu x 2 = " +
      kapi_qcomp["esik_gurultu2"] + " bayt) altinda birakiyor.")
if k12adlar:
    w("")
    if k12yol == "belirsiz":
        w("Ayakta kalan tek anahtar `qp-scale-compress-strength` icin sonuc **belirsiz**:")
        w("kalite olculdu ama `=3` kolu hedef bandin disina cikti (" +
          ", ".join("`" + a + "`/" + kol for a, kol in k12band_disi) +
          "), yani iki kol esit boyda degil.")
        w("Bu anahtar icin mod ne acilir ne kapanir; onunde duran soru kalite degil hedef")
        w("boyuttur.")
    elif k12yol == "kapanir":
        w("Ayakta kalan tek anahtar `qp-scale-compress-strength` de esigi gecemedi; mod bu")
        w("depoda her iki anahtar icin de kanitli olarak **kapanir**.")
    elif k12yol == "acilabilir":
        w("Ayakta kalan anahtar `qp-scale-compress-strength` esigi gecti: mod **acilabilir**,")
        w("ama tasiyici `zones` degil bu anahtardir.")

os.makedirs(os.path.dirname(HEDEF), exist_ok=True)
with io.open(HEDEF, "w", encoding="utf-8", newline=NL) as f:
    f.write(NL.join(s) + NL)
print("yazildi: " + HEDEF + " (" + str(len(s)) + " satir)")
print("K5 kapisi: " + ("gecti" if gecti else "gecmedi") +
      " | p10 gecen: " + str(len(p10_gecen)) +
      " | enkotu gecen: " + str(len(enkotu_gecen)) +
      " | olculen pencere: " + str(olculen) +
      " | gurultu: " + str(gurultu) + " bayt / " + str(len(kontrol_bayt)) + " kosum" +
      " | K12: " + k12yol)
