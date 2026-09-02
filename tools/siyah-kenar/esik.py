import json, os, re, subprocess

KOK = "C:/Users/Administrator/Desktop/Projeler/Vidshrink"
IS = KOK + "/.claude/worktrees/T134/.calisma/t134"
HAVUZ = KOK + "/.calisma/kaynak"
KAYNAK = IS + "/kaynak"
CIK = IS + "/yokla"
KIRP = re.compile(r"crop=(\d+):(\d+):(\d+):(\d+)")
LIMITLER = [8, 16, 24, 32, 40, 48, 56, 64, 80, 96]
GERCEK = {"KD": (1920, 804, 0, 120), "KE": (1920, 804, 0, 120),
          "NA": (1920, 1080, 0, 0), "NB": (1920, 1080, 0, 0)}


def ke_uret():
    hedef = KAYNAK + "/KE.mkv"
    if os.path.exists(hedef):
        return
    a = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-nostdin",
         "-ss", "20", "-t", "20", "-i", HAVUZ + "/parca-1.mkv",
         "-f", "lavfi", "-t", "20",
         "-i", "color=c=#181818:s=1920x1080:r=60,format=yuv420p,noise=alls=22:allf=t+u,format=yuv420p10le",
         "-filter_complex",
         "[0:v]crop=1920:804:0:138[akt];[1:v][akt]overlay=0:120:format=yuv420p10[v]",
         "-map", "[v]", "-c:v", "libx264", "-preset", "veryfast", "-crf", "12",
         "-pix_fmt", "yuv420p10le",
         "-x264-params", "keyint=120:min-keyint=120:scenecut=0", "-an",
         "-color_primaries", "bt2020", "-color_trc", "smpte2084",
         "-colorspace", "bt2020nc", "-color_range", "pc", hedef]
    subprocess.run(a, capture_output=True, text=True)


def bant_luma(ad, h, y):
    a = ["ffmpeg", "-hide_banner", "-nostdin", "-i", KAYNAK + "/" + ad + ".mkv",
         "-vf", "crop=1920:%d:0:%d,signalstats,metadata=print:file=-" % (h, y),
         "-frames:v", "60", "-f", "null", "-"]
    p = subprocess.run(a, capture_output=True, text=True, errors="replace")
    d = {}
    for k in ("YMIN", "YAVG", "YMAX"):
        v = [float(m) for m in re.findall(r"lavfi\.signalstats\." + k + r"=([\d.]+)", p.stdout)]
        d[k] = round(sum(v) / len(v), 2) if v else None
    return d


def kos(ad, limit):
    a = ["ffmpeg", "-hide_banner", "-nostdin", "-ss", "10",
         "-i", KAYNAK + "/" + ad + ".mkv",
         "-vf", "cropdetect=limit=%d:round=2:reset=0:skip=0" % limit,
         "-frames:v", "120", "-f", "null", "-"]
    p = subprocess.run(a, capture_output=True, text=True, errors="replace")
    b = KIRP.findall(p.stderr)
    if not b:
        return None
    w, h, x, y = b[-1]
    return [int(w), int(h), int(x), int(y)]


def main():
    ke_uret()
    out = {}
    for ad, g in GERCEK.items():
        kayit = {"gercek": list(g), "limit": {}}
        if ad in ("KD", "KE"):
            kayit["ust_bant_luma"] = bant_luma(ad, 120, 0)
        for lim in LIMITLER:
            kayit["limit"][str(lim)] = kos(ad, lim)
        out[ad] = kayit
        print(ad, json.dumps(kayit["limit"]), flush=True)
    with open(CIK + "/limit.json", "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1)
    print("ESIK BITTI")


if __name__ == "__main__":
    main()
