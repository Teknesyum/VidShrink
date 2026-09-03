import json, os, re, subprocess, time

KOK = "C:/Users/Administrator/Desktop/Projeler/Vidshrink"
IS = KOK + "/.claude/worktrees/T134/.calisma/t134"
KAYNAK = IS + "/kaynak"
CIK = IS + "/yokla"
ADLAR = ["KA", "KB", "KC", "KD", "NA", "NB", "VD"]
TEKRAR = 3


def yuk_sayisi():
    p = subprocess.run(["powershell", "-NoProfile", "-Command",
                        "(Get-Process ffmpeg -ErrorAction SilentlyContinue).Count"],
                       capture_output=True, text=True)
    try:
        return int(p.stdout.strip() or 0)
    except ValueError:
        return -1


def kos(ad, ss, kare):
    a = ["ffmpeg", "-hide_banner", "-nostdin", "-ss", str(ss),
         "-i", KAYNAK + "/" + ad + ".mkv",
         "-vf", "cropdetect=limit=24:round=2:reset=0:skip=0",
         "-frames:v", str(kare), "-f", "null", "-"]
    t0 = time.perf_counter()
    subprocess.run(a, capture_output=True, text=True, errors="replace")
    return time.perf_counter() - t0


def main():
    os.makedirs(CIK, exist_ok=True)
    out = {"ffmpeg_sayisi_basta": yuk_sayisi(), "tekrar": TEKRAR, "kaynak": {}}
    for ad in ADLAR:
        d = {}
        for mod, sure in (("pencere2sn", None), ("yayilmis10kare", None), ("tamklip", None)):
            olcum = []
            for _ in range(TEKRAR):
                if mod == "pencere2sn":
                    olcum.append(kos(ad, 10, 120))
                elif mod == "yayilmis10kare":
                    t = time.perf_counter()
                    for i in range(10):
                        kos(ad, round(0.5 + i * 19.0 / 9.0, 2), 1)
                    olcum.append(time.perf_counter() - t)
                else:
                    olcum.append(kos(ad, 0, 1200))
            olcum.sort()
            d[mod] = {"medyan": round(olcum[len(olcum) // 2], 3),
                      "min": round(olcum[0], 3), "maks": round(olcum[-1], 3)}
        out["kaynak"][ad] = d
        print(ad, json.dumps(d), flush=True)
    out["ffmpeg_sayisi_sonda"] = yuk_sayisi()
    with open(CIK + "/sure.json", "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1)
    print("SURE BITTI")


if __name__ == "__main__":
    main()
