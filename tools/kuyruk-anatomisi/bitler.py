# -*- coding: utf-8 -*-
import subprocess, os

def paketler(mp4):
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
