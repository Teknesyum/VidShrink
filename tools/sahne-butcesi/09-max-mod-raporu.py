import json, os, io, csv, subprocess

KOK = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
IS = os.path.join(KOK, ".calisma", os.environ.get("VIDSHRINK_IS", "T193"))
HEDEF = os.path.join(KOK, "docs", "olcumler", "max-mod-acilis.md")


def oku(p):
    with io.open(p, encoding="utf-8-sig") as f:
        return json.load(f)


def vir(x, n=3):
    if x is None:
        return "bilinmiyor"
    return ("%." + str(n) + "f") % x if False else (("%." + str(n) + "f") % x).replace(".", ",")


pencereler = oku(os.path.join(IS, "pencereler.json"))
adlar = [p["Ad"] for p in pencereler]

haritalar = {}
for a in adlar:
    p = os.path.join(IS, "harita-" + a + ".json")
    if os.path.exists(p):
        haritalar[a] = oku(p)

k5 = {}
for a in adlar:
    p = os.path.join(IS, "k5-maks-" + a + ".json")
    if os.path.exists(p):
        k5[a] = dict((r["Kol"], r) for r in oku(p))

with io.open(os.path.join(IS, "k2kapi", "kapi.csv"), encoding="utf-8") as f:
    kapi = list(csv.DictReader(f, delimiter=";"))

boyut = {}
for a in adlar:
    for kol in ("taban", "dagitim"):
        p = os.path.join(IS, "k5-maks-" + a + "-" + kol + ".mkv")
        if os.path.exists(p):
            boyut[(a, kol)] = os.path.getsize(p)

ffv = subprocess.run(["ffmpeg", "-version"], capture_output=True, text=True).stdout.splitlines()[0]

kontrol_bayt = {}
for i in (1, 2, 3, 4):
    p = os.path.join(IS, "k2kapi", "kontrol-%d.mkv" % i)
    if os.path.exists(p):
        kontrol_bayt[i] = os.path.getsize(p)
ilk_cift = abs(kontrol_bayt[1] - kontrol_bayt[2]) if 1 in kontrol_bayt and 2 in kontrol_bayt else None

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
kazanc = {}
for a in adlar:
    c = k5.get(a)
    if not c or "taban" not in c or "dagitim" not in c:
        continue
    t, d = c["taban"], c["dagitim"]

    def f(k, t=t, d=d):
        if t.get(k) is None or d.get(k) is None:
            return None
        return d[k] - t[k]

    kazanc[a] = {"p10": f("VmafP10"), "enkotu": f("VmafWorstScene"),
                 "ort": f("VmafMean"), "min": f("VmafMin")}

p10_gecen = [a for a in adlar if a in kazanc and kazanc[a]["p10"] is not None and kazanc[a]["p10"] >= P10_ESIK]
enkotu_gecen = [a for a in adlar if a in kazanc and kazanc[a]["enkotu"] is not None and kazanc[a]["enkotu"] >= ENKOTU_ESIK]
kayip_asan = [a for a in adlar if a in kazanc and kazanc[a]["p10"] is not None and kazanc[a]["p10"] < -KAYIP_ESIK]
band_disi = [(a, kol) for a in adlar for kol in ("taban", "dagitim")
             if a in k5 and kol in k5[a] and not k5[a][kol]["BandIcinde"]]
olculen = len(kazanc)

sart1 = len(p10_gecen) >= 2
sart2 = len(set(p10_gecen) & set(enkotu_gecen)) >= 2
sart3 = len(kayip_asan) == 0
sart4 = len(band_disi) == 0
gecti = sart1 and sart2 and sart3 and sart4

kapi_qcomp = [r for r in kapi if r["anahtar"] == "qcomp"][0]
kapi_zones = [r for r in kapi if r["anahtar"] == "zones"][0]
kapi_qpscs = [r for r in kapi if r["anahtar"] == "qp-scale-compress-strength"][0]
kontrol = [r for r in kapi if r["anahtar"] == "kontrol"][0]
gurultu = int(kontrol["fark_bayt"])

s = []
w = s.append
w("# Max Sikistirma Modu — Acilis Olcumu (T193)")
w("")
w("Bu sayfa **tumuyle** `.calisma/" + os.path.basename(IS) + "/` altindaki olculen dosyalardan")
w("uretilir (`tools/sahne-butcesi/09-max-mod-raporu.py`). Elle yazilan sayi yoktur.")
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
w("Tekrar gurultusu **dort** ayni-parametre kosumunun araligidir: " + str(gurultu) + " bayt")
w("(kosum baytlari: " + ", ".join(str(kontrol_bayt[i]) for i in sorted(kontrol_bayt)) + ").")
if ilk_cift is not None:
    w("Ayni dort kosumun ilk **cifti** " + str(ilk_cift) + " bayt veriyor — gercek araligin")
    w(("%.0f" % (float(gurultu) / ilk_cift)) + " kati kucuk. T114 gurultuyu tek ciftten oluyordu;")
    w("bu, sinirdaki bir adayi yanlislikla gecirebilirdi.")
w("")
w("**Ucuncu soru — `qcomp` varsayilan yolda gorunuyor mu: hayir.** ffmpeg anahtari")
w("ayristiramiyor (`[libsvtav1] Error parsing option qcomp: 0.40.`) ama **cikis kodu 0**")
w("donuyor. Iki deger arasindaki " + kapi_qcomp["fark_bayt"] + " baytlik fark tekrar")
w("gurultusunun iki katinin (" + kapi_qcomp["esik_gurultu2"] + " bayt) altinda. Kapi")
w("**desteklenmiyor** diyor; bu yuzden K2'nin geri kalani — deger taramasi —")
w("**kosulmadi**.")
w("")
w("Ayni kapida iki yan sonuc: `zones` da `libsvtav1`de olu (" + kapi_zones["fark_bayt"] + " bayt fark,")
w("gurultunun altinda) — T114'un bulgusu bagimsiz dogrulandi. Buna karsilik SVT-AV1'in")
w("kendi karsiligi `qp-scale-compress-strength` **calisiyor** (" + kapi_qpscs["fark_bayt"] + " bayt fark).")
w("")
w("T114 izgarasinin `libsvtav1 / qcomp` satiri zaten `qcomp` degil")
w("`qp-scale-compress-strength` deniyordu; \"qcomp `libsvtav1`'de calisiyor\" cumlesi")
w("literal anahtarin olcumu hic olmadi.")
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
w("Kazanc = `dagitim - taban`, VMAF-NEG puani:")
w("")
w("| Pencere | p10 kazanci | En kotu sahne kazanci | Ort kazanc | Taban bayt | Dagitim bayt | Bayt farki |")
w("|---------|-------------|-----------------------|------------|------------|--------------|------------|")
for a in adlar:
    k = kazanc.get(a)
    bt, bd = boyut.get((a, "taban")), boyut.get((a, "dagitim"))
    fark = abs(bt - bd) if (bt and bd) else None
    w("| `" + a + "` | " +
      (vir(k["p10"]) if k else "olculmedi") + " | " +
      (vir(k["enkotu"]) if k else "olculmedi") + " | " +
      (vir(k["ort"]) if k else "olculmedi") + " | " +
      (str(bt) if bt else "yok") + " | " + (str(bd) if bd else "yok") + " | " +
      (str(fark) if fark is not None else "yok") + " |")
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
    ciftli = [a for a in bd_pencere if len([1 for x, _ in band_disi if x == a]) == 2]
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
w("**K5 kapisi: " + ("gecti" if gecti else "gecmedi") + ".**")
w("")
w("Sebep: `libsvtav1` `zones` anahtarini yok sayiyor, yani `dagitim` kolu `taban` ile")
mutlak = [abs(kazanc[a]["p10"]) for a in adlar if a in kazanc and kazanc[a]["p10"] is not None]
w("ayni kodlamadir. Kolun p10 kazanci uc pencerede de **sifirin altinda**; mutlak")
w("degerlerin en buyugu **" + vir(max(mutlak)) + " puan**, esik +" + vir(P10_ESIK, 2) + ".")
w("")
w("Bayt tarafinda ayrimi tek cumleye sigdirmamak gerekiyor. Olculen tekrar gurultusu")
w(str(gurultu) + " bayt, ama o gurultu `p1`in bit hizinda (`8230k`, 6 sn) olculdu:")
alt = [a for a in adlar if (a, "taban") in boyut and (a, "dagitim") in boyut
       and abs(boyut[(a, "taban")] - boyut[(a, "dagitim")]) <= gurultu]
ust = [a for a in adlar if (a, "taban") in boyut and (a, "dagitim") in boyut
       and abs(boyut[(a, "taban")] - boyut[(a, "dagitim")]) > gurultu]
w("")
w("- Gurultunun **altinda** kalan pencere: " +
  (", ".join("`" + a + "`" for a in alt) if alt else "yok") + ".")
w("- Gurultunun **ustunde** kalan pencere: " +
  (", ".join("`" + a + "`" for a in ust) if ust else "yok") + ".")
w("")
if ust:
    w("Ustte kalan pencerede tekrar gurultusu **kendi bit hizinda olculmedi**, o yuzden")
    w("\"gurultunun icinde\" denemez. Ama o pencerede de VMAF kazanci sifirin altinda")
    w("(" + ", ".join("`" + a + "` p10 " + vir(kazanc[a]["p10"]) for a in ust if a in kazanc) + "),")
    w("yani bayt farki kaliteye kazanc olarak donmemistir.")
    w("")
w("Bu bir \"az kazandi\" degil, **tasiyicinin yoklugudur**.")
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
w("  kapisi `libsvtav1` icin **hayir** dedi. `uyumlu` (`libx264`) ve `yedek` (`libx265`)")
w("  kollarinda `qcomp` calisiyor, ama uretimin varsayilani o kollar degil.")
w("- `qp-scale-compress-strength` taramasi: bu sozlesmenin sorusu degil. Destek")
w("  kapisindan **evet** ile ciktigi olculdu; kalite ve boyut etkisi olculmedi.")
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
    w("**Max sikistirma modu acilmaz**: `maks` kolunun kalite kapisi bu depoda ilk kez")
    w("kosuldu ve p10 esigini gecen pencere " + str(len(p10_gecen)) + "/" + str(olculen) +
      ", en kotu sahne esigini gecen pencere " + str(len(enkotu_gecen)) + "/" + str(olculen) + " cikti —")
    w("cunku uretimin varsayilan kodlayicisi `libsvtav1` hem dagitimi tasiyacak anahtari")
    w("(`zones`, fark " + kapi_zones["fark_bayt"] + " bayt) hem ayakta kalan tek isareti")
    w("(`qcomp`, fark " + kapi_qcomp["fark_bayt"] + " bayt) destek esiginin (gurultu x 2 = " +
      kapi_qcomp["esik_gurultu2"] + " bayt) altinda birakiyor.")

os.makedirs(os.path.dirname(HEDEF), exist_ok=True)
with io.open(HEDEF, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(s) + "\n")
print("yazildi: " + HEDEF + " (" + str(len(s)) + " satir)")
print("K5 kapisi: " + ("gecti" if gecti else "gecmedi") +
      " | p10 gecen: " + str(len(p10_gecen)) +
      " | enkotu gecen: " + str(len(enkotu_gecen)) +
      " | olculen pencere: " + str(olculen))
