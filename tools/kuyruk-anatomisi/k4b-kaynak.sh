#!/usr/bin/env bash
set -euo pipefail
K="${1:-.calisma/kaynak/parca-2.mkv}"
C="${2:-.calisma/t122}"
mkdir -p "$C"
ffmpeg -v error -threads 4 -i "$K" -map 0:v:0 -an -sn \
  -f framehash -hash md5 "$C/kaynak-framehash.txt" -y
ffmpeg -v error -threads 4 -i "$K" -map 0:v:0 -an -sn \
  -vf "tblend=all_mode=difference,signalstats,metadata=print:file=$C/kaynak-fark.txt" \
  -f null -
python - "$C/kaynak-fark.txt" "$(dirname "$0")/hareket-t122/kaynak-fark.csv.gz" <<'PY'
import sys, gzip
y = {}
n = None
for l in open(sys.argv[1], encoding="utf-8", errors="replace"):
    l = l.strip()
    if l.startswith("frame:"):
        n = int(l.split()[0].split(":")[1])
    elif l.startswith("lavfi.signalstats.YAVG=") and n is not None:
        y[n + 1] = float(l.split("=")[1])
with gzip.open(sys.argv[2], "wt", encoding="utf-8", newline="\n") as fh:
    fh.write("kare;yavg\n")
    for k in sorted(y):
        fh.write("%d;%.6f\n" % (k, y[k]))
PY
