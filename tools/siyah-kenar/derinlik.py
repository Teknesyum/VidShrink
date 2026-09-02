import json, os, re, subprocess

KOK = "C:/Users/Administrator/Desktop/Projeler/Vidshrink"
IS = KOK + "/.claude/worktrees/T134/.calisma/t134"
KAYNAK = IS + "/kaynak"
CIK = IS + "/yokla"
GECICI = IS + "/derinlik"
LIMITLER = [8, 12, 16, 24, 32, 40, 48, 56, 64, 80]
GERCEK = {"KD": [1920, 804, 0, 120], "NA": [1920, 1080, 0, 0]}


def sekiz_bit(ad):
    h = GECICI + "/" + ad + "-8bit.mkv"
    if os.path.exists(h):
        return h
    os.makedirs(GECICI, exist_ok=True)
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-nostdin",
                    "-i", KAYNAK + "/" + ad + ".mkv", "-vf", "format=yuv420p",
                    "-c:v", "libx264", "-preset", "veryfast", "-crf", "12",
                    "-an", "-y", h], capture_output=True)
    return h


def kutu(yol, limit):
    a = ["ffmpeg", "-hide_banner", "-nostdin", "-ss", "10", "-i", yol,
         "-vf", "cropdetect=limit=%d:round=2:reset=0:skip=0" % limit,
         "-frames:v", "120", "-f", "null", "-"]
    p = subprocess.run(a, capture_output=True, text=True, errors="replace")
    m = re.findall(r"crop=(\d+):(\d+):(\d+):(\d+)", p.stderr)
    return [int(x) for x in m[-1]] if m else None


def main():
    out = {"limitler": LIMITLER, "kaynak": {}}
    for ad in GERCEK:
        d = {"gercek": GERCEK[ad], "derinlik": {}}
        for etiket, yol in (("10bit", KAYNAK + "/" + ad + ".mkv"),
                            ("8bit", sekiz_bit(ad))):
            d["derinlik"][etiket] = {str(L): kutu(yol, L) for L in LIMITLER}
            print(ad, etiket, "bitti", flush=True)
        out["kaynak"][ad] = d
    os.makedirs(CIK, exist_ok=True)
    with open(CIK + "/derinlik.json", "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1)
    print("DERINLIK BITTI")


if __name__ == "__main__":
    main()
