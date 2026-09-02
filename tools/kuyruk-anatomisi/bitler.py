# -*- coding: utf-8 -*-
import subprocess, os, gzip

KOK = os.path.dirname(os.path.abspath(__file__))

def _arsivden(ad):
    yol = os.path.join(KOK, "paket-t122", ad + ".csv.gz")
    if not os.path.exists(yol):
        return None
    out = []
    with gzip.open(yol, "rt", encoding="utf-8") as fh:
        next(fh)
        for l in fh:
            i, s, k = l.strip().split(";")
            out.append((int(i), int(s), k == "1"))
    return out

def _ffprobe(mp4):
    out = subprocess.run(
        ["ffprobe", "-v", "error", "-select_streams", "v:0",
         "-show_entries", "packet=pts_time,size,flags",
         "-of", "csv=p=0", mp4],
        capture_output=True, text=True, check=True).stdout
    kayit = []
    for l in out.splitlines():
        p = l.split(",")
        if len(p) < 3 or not p[0] or p[0] == "N/A":
            continue
        kayit.append((float(p[0]), int(p[1]), "K" in p[2]))
    kayit.sort(key=lambda x: x[0])
    return [(i, s, k) for i, (t, s, k) in enumerate(kayit)]

def paketler(mp4):
    ad = os.path.splitext(os.path.basename(mp4))[0]
    if os.path.exists(mp4):
        return _ffprobe(mp4)
    a = _arsivden(ad)
    if a is None:
        raise SystemExit("ne mp4 ne arsiv var: %s" % ad)
    return a
