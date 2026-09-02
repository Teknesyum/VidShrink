import glob
import json
import os
import subprocess
import sys

IS = sys.argv[1]
CIKTI = os.path.join(IS, "cikti-denetimi.csv")
PENCERELER = ("p1-karisik", "p2-durgun", "p3-hareketli")
ESIK = 0.5


def sure(dosya):
    r = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration",
                        "-of", "default=nw=1:nk=1", dosya], capture_output=True, text=True)
    try:
        return float(r.stdout.strip())
    except ValueError:
        return None


def harita(pencere):
    with open(os.path.join(IS, f"harita-{pencere}.json"), encoding="utf-8") as f:
        return json.load(f)


satirlar = []
for d in sorted(glob.glob(os.path.join(IS, "referans", "*"))):
    ad = os.path.basename(d)
    pencere = ad.split("-", 1)[1]
    for i, sc in enumerate(harita(pencere)["Scenes"]):
        f = os.path.join(d, f"sahne-{i:03d}.mkv")
        bekle = sc["End"] - sc["Start"]
        s = sure(f) if os.path.exists(f) else None
        satirlar.append(("referans", f"{ad}/sahne-{i:03d}", bekle, s))

for f in sorted(glob.glob(os.path.join(IS, "plan-*.mkv")) + glob.glob(os.path.join(IS, "k4b-*.mkv"))
                + glob.glob(os.path.join(IS, "k5-*.mkv")) + glob.glob(os.path.join(IS, "k7-*.mkv"))):
    ad = os.path.basename(f)
    pencere = next((p for p in PENCERELER if p in ad), None)
    if pencere is None:
        continue
    satirlar.append(("cikti", ad, harita(pencere)["Duration"], sure(f)))

sapan = 0
with open(CIKTI, "w", encoding="utf-8", newline="\n") as f:
    f.write("tur;dosya;beklenen_sn;olculen_sn;sonuc\n")
    for tur, ad, bekle, s in satirlar:
        ok = s is not None and abs(s - bekle) <= ESIK
        if not ok:
            sapan += 1
        f.write(f"{tur};{ad};{bekle:.3f};{'' if s is None else f'{s:.3f}'};{'tam' if ok else 'SAPMA'}\n")

yarim = glob.glob(os.path.join(IS, "**", "*.yarim.mkv"), recursive=True)
print(f"denetlenen {len(satirlar)} dosya, sapan {sapan}, ardakalan .yarim.mkv {len(yarim)}")
sys.exit(1 if sapan else 0)
