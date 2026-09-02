# -*- coding: utf-8 -*-
import json, gzip, io, sys, os

FPS = 60000.0 / 1000.0

def kareler(yol):
    with gzip.open(yol, "rt", encoding="utf-8") as fh:
        fr = json.load(fh)["frames"]
    out = []
    for f in fr:
        m = f["metrics"]
        v = m.get("vmaf", m.get("vmaf_neg"))
        if v is not None:
            out.append((f["frameNum"], v))
    out.sort()
    return out

def p10esik(v):
    srt = sorted(v)
    r = 0.10 * (len(srt) - 1)
    lo, hi = int(r // 1), int(-(-r // 1))
    return srt[lo] if lo == hi else srt[lo] + (srt[hi] - srt[lo]) * (r - lo)

def kotu(kr):
    v = [x[1] for x in kr]
    e = p10esik(v)
    return e, sorted(n for n, s in kr if s <= e)

def kumele(idx, bosluk):
    if not idx:
        return []
    ku = [[idx[0], idx[0]]]
    for n in idx[1:]:
        if n - ku[-1][1] <= bosluk:
            ku[-1][1] = n
        else:
            ku.append([n, n])
    return ku

def sn(n):
    return n / FPS
