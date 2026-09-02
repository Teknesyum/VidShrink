import hashlib, json, os, re, subprocess, sys

KOK = "C:/Users/Administrator/Desktop/Projeler/Vidshrink"
IS = KOK + "/.claude/worktrees/T134/.calisma/t134"
KAYNAK = IS + "/kaynak"
CIK = IS + "/olcu"

AKTIF = {
    "KA": (804, 138), "KB": (872, 104), "KC": (1036, 22), "KD": (804, 120),
    "NA": (1080, 0), "NB": (1080, 0), "VD": (804, 138),
}
TABAN = {"KA": "parca-1.mkv", "KB": "parca-2.mkv", "KC": "parca-3.mkv",
         "KD": "parca-1.mkv", "NA": "parca-1.mkv", "NB": "parca-2.mkv",
         "VD": "parca-3.mkv"}


def sha(yol):
    h = hashlib.sha256()
    with open(yol, "rb") as f:
        for p in iter(lambda: f.read(1 << 20), b""):
            h.update(p)
    return h.hexdigest()


def ffprobe(yol):
    a = ["ffprobe", "-v", "error", "-select_streams", "v:0", "-count_frames",
         "-show_entries", "stream=width,height,codec_name,pix_fmt,r_frame_rate,nb_read_frames",
         "-show_entries", "format=duration,size", "-of", "json", yol]
    return json.loads(subprocess.run(a, capture_output=True, text=True).stdout)


def istatistik(yol, h, y):
    a = ["ffmpeg", "-hide_banner", "-nostdin", "-i", yol,
         "-vf", "crop=1920:%d:0:%d,signalstats,metadata=print:file=-" % (h, y),
         "-f", "null", "-"]
    p = subprocess.run(a, capture_output=True, text=True, errors="replace")
    d = {}
    for k in ("YMIN", "YAVG", "YMAX"):
        v = [float(m) for m in re.findall(r"lavfi\.signalstats\." + k + r"=([\d.]+)", p.stdout)]
        d[k] = (round(min(v), 2), round(sum(v) / len(v), 2), round(max(v), 2)) if v else None
    return d


def framemd5(yol, h, y):
    a = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-nostdin", "-i", yol,
         "-vf", "crop=1920:%d:0:%d" % (h, y), "-f", "framemd5", "-"]
    p = subprocess.run(a, capture_output=True, text=True)
    satir = [s for s in p.stdout.splitlines() if s and not s.startswith("#")]
    ozet = hashlib.sha256("\n".join(satir).encode()).hexdigest()
    pts = [s.split(",")[1].strip() for s in satir]
    return ozet, len(satir), (pts[0], pts[-1]) if pts else None


def main():
    os.makedirs(CIK, exist_ok=True)
    hedef = CIK + "/kaynak-kanit.json"
    out = {}
    if os.path.exists(hedef):
        with open(hedef, encoding="utf-8") as f:
            out = json.load(f)
    istenen = sys.argv[1:] or list(AKTIF)
    for ad, (h, y) in AKTIF.items():
        if ad not in istenen:
            continue
        yol = KAYNAK + "/" + ad + ".mkv"
        pr = ffprobe(yol)
        st = pr["streams"][0]
        kayit = {
            "taban": TABAN[ad],
            "sha256": sha(yol),
            "cozunurluk": "%dx%d" % (st["width"], st["height"]),
            "aktif": "1920x%d" % h,
            "bant_yuzde": round((1080 - h) * 100.0 / 1080, 2),
            "sure": round(float(pr["format"]["duration"]), 3),
            "kare": int(st["nb_read_frames"]),
            "fps": st["r_frame_rate"],
            "kodek": st["codec_name"],
            "pix_fmt": st["pix_fmt"],
            "boyut": int(pr["format"]["size"]),
        }
        kayit["aktif_stat"] = istatistik(yol, h, y)
        if h < 1080:
            kayit["ust_bant_stat"] = istatistik(yol, y, 0) if y > 0 else None
            alt = 1080 - h - y
            kayit["alt_bant_stat"] = istatistik(yol, alt, h + y) if alt > 0 else None
            kayit["ust_bant_px"] = y
            kayit["alt_bant_px"] = alt
        a_ozet, a_kare, a_pts = framemd5(yol, h, y)
        b_ozet, b_kare, b_pts = framemd5(yol, h, y)
        kayit["cozme_determinizmi"] = {
            "kirpilmis_alan_md5": a_ozet, "tekrar_md5": b_ozet,
            "esit": a_ozet == b_ozet, "kare": a_kare, "pts": a_pts,
            "tekrar_kare": b_kare, "tekrar_pts": b_pts,
        }
        out[ad] = kayit
        print(ad, "bitti", flush=True)
    with open(hedef, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1)
    print("KANIT BITTI")


if __name__ == "__main__":
    main()
