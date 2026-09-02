import json, os, re, subprocess, sys, time

KOK = "C:/Users/Administrator/Desktop/Projeler/Vidshrink"
IS = KOK + "/.claude/worktrees/T134/.calisma/t134"
KAYNAK = IS + "/kaynak"
CIK = IS + "/yokla"

GERCEK = {
    "KA": (1920, 804, 0, 138),
    "KB": (1920, 872, 0, 104),
    "KC": (1920, 1036, 0, 22),
    "KD": (1920, 804, 0, 120),
    "NA": (1920, 1080, 0, 0),
    "NB": (1920, 1080, 0, 0),
    "VD": (1920, 1080, 0, 0),
}

KIRP = re.compile(r"crop=(\d+):(\d+):(\d+):(\d+)")


def kos(kaynak, ss, kare, limit=24):
    yol = KAYNAK + "/" + kaynak + ".mkv"
    a = ["ffmpeg", "-hide_banner", "-nostdin", "-ss", str(ss), "-i", yol,
         "-vf", "cropdetect=limit=%d:round=2:reset=0:skip=0" % limit,
         "-frames:v", str(kare), "-f", "null", "-"]
    t0 = time.perf_counter()
    p = subprocess.run(a, capture_output=True, text=True, errors="replace")
    sure = time.perf_counter() - t0
    bulunan = KIRP.findall(p.stderr)
    if not bulunan:
        return None, sure
    w, h, x, y = bulunan[-1]
    return (int(w), int(h), int(x), int(y)), sure


def birlesim(kutular):
    kutular = [k for k in kutular if k]
    if not kutular:
        return None
    x1 = min(k[2] for k in kutular)
    y1 = min(k[3] for k in kutular)
    x2 = max(k[2] + k[0] for k in kutular)
    y2 = max(k[3] + k[1] for k in kutular)
    return (x2 - x1, y2 - y1, x1, y1)


def main():
    os.makedirs(CIK, exist_ok=True)
    hedef = CIK + "/cropdetect.json"
    sonuc = {}
    if os.path.exists(hedef):
        with open(hedef, encoding="utf-8") as f:
            sonuc = json.load(f)
    istenen = sys.argv[1:] or list(GERCEK)
    for ad in GERCEK:
        if ad not in istenen:
            continue
        kayit = {"gercek": GERCEK[ad], "nokta": {}, "yayilmis": None, "tam": None,
                 "limit": {}}
        for ss in (0, 5, 10, 15):
            kutu, sure = kos(ad, ss, 120)
            kayit["nokta"]["t%02d" % ss] = {"kutu": kutu, "sure": round(sure, 3)}
        t0 = time.perf_counter()
        kutular = []
        for i in range(10):
            k, _ = kos(ad, round(0.5 + i * 19.0 / 9.0, 2), 1)
            kutular.append(k)
        kayit["yayilmis"] = {"kutu": birlesim(kutular), "tekil": kutular,
                             "sure": round(time.perf_counter() - t0, 3)}
        kutu, sure = kos(ad, 0, 1200)
        kayit["tam"] = {"kutu": kutu, "sure": round(sure, 3)}
        for lim in (16, 24, 40, 64):
            k, _ = kos(ad, 10, 120, lim)
            kayit["limit"][str(lim)] = k
        sonuc[ad] = kayit
        print(ad, "bitti", flush=True)
    with open(hedef, "w", encoding="utf-8") as f:
        json.dump(sonuc, f, indent=1)
    print("YOKLA BITTI")


if __name__ == "__main__":
    main()
