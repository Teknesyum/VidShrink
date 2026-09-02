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

hucre = kazZ = kazQ = tabani = 0
for y in sorted(glob.glob(os.path.join(IS, "k4b-*.csv"))):
    o = {}
    for l in io.open(y, encoding="utf-8-sig").read().splitlines()[1:]:
        c = l.split(";")
        if len(c) >= 4 and c[2]:
            o[c[0]] = float(c[2])
    if "taban" not in o or len(o) < 2:
        continue
    hucre += 1
    en = min((a for a in o if a != "taban"), key=lambda a: o[a])
    if o[en] < o["taban"]:
        tabani += 1
        if en == "zones":
            kazZ += 1
        if en == "qcomp":
            kazQ += 1

yaz("K4 eki olculen hucre", hucre,
    int(re.search(r"olculen (\d+) hucrenin tabani gecen", metin).group(1)))
yaz("tabani gecen hucre", tabani,
    int(re.search(r"tabani gecen (\d+) tanesinde", metin).group(1)))
yaz("zones kazandi", kazZ,
    int(re.search(r"`zones` (\d+) kez kazandi", metin).group(1)))
yaz("qcomp kazandi", kazQ,
    int(re.search(r"`qcomp` (\d+) kez", metin).group(1)))

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
