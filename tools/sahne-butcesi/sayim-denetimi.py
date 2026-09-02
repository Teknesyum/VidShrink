import glob, io, json, os, re, sys

IS = sys.argv[1]
SAYFA = sys.argv[2]
metin = io.open(SAYFA, encoding="utf-8").read()
satirlar = []
hata = 0

def mae(a, b):
    ta, tb = sum(a), sum(b)
    if ta == 0 or tb == 0:
        return None
    return sum(abs(x / ta - y / tb) for x, y in zip(a, b)) / len(a) * 100

def yaz(ad, benim, iddia):
    global hata
    tuttu = benim == iddia
    if not tuttu:
        hata = 1
    satirlar.append(f"{ad};{benim};{iddia};{'tuttu' if tuttu else 'TUTMADI'}")

k1 = {}
for y in sorted(glob.glob(os.path.join(IS, "k1-*.json"))):
    k = json.load(io.open(y, encoding="utf-8-sig"))
    if k["Bilinmiyor"] or k["ReferansToplamBit"] == 0:
        continue
    k1[os.path.basename(y)[3:-5]] = k

yaz("K1/K2 olculen hucre", len(k1),
    int(re.search(r"Olculen (\d+) hucreden", metin).group(1)))

esit = sum(1 for k in k1.values()
           if mae(k["Verilen"], k["HakEdilen"]) <= mae(k["Harita"], k["HakEdilen"]))
m = re.search(r"MAE\(verilen\) <= MAE\(harita\)`\): (\d+)/(\d+) hucre", metin)
yaz("kodlayici en az esit", esit, int(m.group(1)))

yeterli = [k for k in k1.values() if len(k["HakEdilen"]) >= 4]
m = re.search(r"dort ve ustu olan (\d+) hucreye bakildiginda harita (\d+) tanesinde", metin)
if m:
    yaz("sahne >= 4 hucre", len(yeterli), int(m.group(1)))
    yaz("sahne >= 4, harita onde",
        sum(1 for k in yeterli
            if mae(k["Verilen"], k["HakEdilen"]) > mae(k["Harita"], k["HakEdilen"])),
        int(m.group(2)))

hucre = kazZ = kazQ = tabani = zonesTaban = 0
zonesEnIyiKazanc = None
for y in sorted(glob.glob(os.path.join(IS, "k4b-*.csv"))):
    o = {}
    for l in io.open(y, encoding="utf-8-sig").read().splitlines()[1:]:
        c = l.split(";")
        if len(c) >= 4 and c[2]:
            o[c[0]] = float(c[2])
    if "taban" not in o or len(o) < 2:
        continue
    hucre += 1
    if "zones" in o and o["zones"] < o["taban"]:
        zonesTaban += 1
    en = min((a for a in o if a != "taban"), key=lambda a: o[a])
    if o[en] < o["taban"]:
        tabani += 1
        if en == "zones":
            kazZ += 1
            k = o["taban"] - o["zones"]
            if zonesEnIyiKazanc is None or k > zonesEnIyiKazanc:
                zonesEnIyiKazanc = k
        if en == "qcomp":
            kazQ += 1

m = re.search(r"olculen (\d+) hucrenin (\d+) tanesinde tabani gecti; bunlarin "
              r"(\d+) tanesinde de hucrenin en iyi adayi oldu\. En iyi aday oldugu "
              r"hucredeki kazanc ([\d.]+) pp", metin)
if m:
    yaz("manset olculen hucre", hucre, int(m.group(1)))
    yaz("manset zones tabani gecen", zonesTaban, int(m.group(2)))
    yaz("manset zones en iyi aday", kazZ, int(m.group(3)))
    yaz("manset zones en iyi kazanc",
        "yok" if zonesEnIyiKazanc is None else f"{zonesEnIyiKazanc:.3f}", m.group(4))
else:
    yaz("manset okundu", "evet", "hayir")

izgara = os.path.join(IS, "k4-izgara.csv")
if os.path.exists(izgara):
    yalnizYuzde = 0
    for l in io.open(izgara, encoding="utf-8-sig").read().splitlines()[1:]:
        c = l.split(";")
        if len(c) < 8 or c[2] != "hayir" or not (c[3] and c[5] and c[6]):
            continue
        if int(c[5]) > int(c[6]) * 2 and not int(c[5]) > int(c[3]) // 100:
            yalnizYuzde += 1
    yaz("K4 destek gerekcesi yalniz %1 kolu", yalnizYuzde,
        len(re.findall(r"iki katini asiyor ama ciktinin %1'ini asmiyor", metin)))

yaz("K4 eki olculen hucre", hucre,
    int(re.search(r"olculen (\d+) hucrenin tabani gecen", metin).group(1)))
yaz("tabani gecen hucre", tabani,
    int(re.search(r"tabani gecen (\d+) tanesinde", metin).group(1)))
yaz("zones kazandi", kazZ,
    int(re.search(r"`zones` (\d+) kez kazandi", metin).group(1)))
yaz("qcomp kazandi", kazQ,
    int(re.search(r"`qcomp` (\d+) kez", metin).group(1)))

def yukle(ad):
    y = os.path.join(IS, ad)
    if not os.path.exists(y):
        return []
    return json.load(io.open(y, encoding="utf-8-sig"))

k7kars = k7alt = 0
k7ust = 0.0
k7enKayip = None
for y in sorted(glob.glob(os.path.join(IS, "k7-*.json"))):
    hucre = os.path.basename(y)[3:-5]
    dogru = [x for x in yukle("k5-" + hucre + ".json")
             if x["Kol"] == "dagitim" and x["VmafP10"] is not None]
    if not dogru:
        continue
    for b in json.load(io.open(y, encoding="utf-8-sig")):
        if b["VmafP10"] is None:
            continue
        k7kars += 1
        kayip = dogru[0]["VmafP10"] - b["VmafP10"]
        if k7enKayip is None or kayip > k7enKayip:
            k7enKayip = kayip
        if kayip <= 0:
            k7alt += 1
            k7ust = max(k7ust, -kayip)

m = re.search(r"en buyuk p10 kaybi (-?[\d.]+) puan", metin)
if m:
    yaz("K7 en buyuk p10 kaybi",
        "yok" if k7enKayip is None else f"{k7enKayip:+.3f}".replace("+0.000", "0.000"),
        m.group(1))

m = re.search(r"\(K7\) karsilastirilan (\d+) kosumun\s+(\d+) tanesi de dogru haritanin altina dusmedi; "
              r"en iyi bozuk\s+kol dogru haritayi ([\d.]+) puan gecti", metin)
if m:
    yaz("K7 karsilastirilan kosum", k7kars, int(m.group(1)))
    yaz("K7 geri dusmeyen kosum", k7alt, int(m.group(2)))
    yaz("K7 en iyi bozuk ustunluk", f"{k7ust:.3f}", m.group(3))

bolum = metin.split("## Olculemeyenler")[1].split("\n\n")
tablo = [l for b in bolum for l in b.splitlines()
         if l.startswith("| ") and not l.startswith("|--") and "Bolum" not in l]
yaz("olculemeyen satir", len(tablo),
    int(re.search(r"Toplam (\d+) satir", metin).group(1)))

cikti = os.path.join(IS, "sayim-denetimi.csv")
io.open(cikti, "w", encoding="utf-8", newline="\n").write(
    "olcu;bagimsiz sayim;sayfadaki iddia;sonuc\n" + "\n".join(satirlar) + "\n")
print("\n".join(satirlar))
print(f"denetlenen {len(satirlar)} iddia, tutmayan {sum(1 for s in satirlar if 'TUTMADI' in s)}")
sys.exit(hata)
