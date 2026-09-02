# -*- coding: utf-8 -*-
import os, gzip

_KOK = os.path.dirname(os.path.abspath(__file__))

def yavg():
    out = {}
    with gzip.open(os.path.join(_KOK, "hareket-t122", "kaynak-fark.csv.gz"), "rt", encoding="utf-8") as fh:
        next(fh)
        for l in fh:
            a, b = l.strip().split(";")
            out[int(a)] = float(b)
    return out
