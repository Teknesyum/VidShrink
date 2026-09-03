#!/usr/bin/env bash
# Atlama maliyeti. Iki olcu, ikisi de ayni hedef kumesinde (tohum 1733, 24 hedef):
#  yapisal : hedeften geriye en yakin anahtar kareye kadarki mesafe (sn / kare).
#            Dosyadan tam hesaplanir, saat gurultusu yok.
#  net     : ayni hedefte `ffmpeg -ss T -i f -frames:v 1` suresi eksi
#            `-c:v copy` suresi. Ikisi ust uste kosulur (esli tasarim), boylece
#            surec baslatma maliyetinin yavas kaymasi farkta birbirini goturur.
#            Raporlanan sayi eslerin medyani; yaninda IQR ile gurultu tabani.
# Duvar saati olculeri diske yazilir; tam ve esli olan varsa tekrar kosulmaz.
set -u
. "$(dirname "$0")/ortak.sh"
N=24
ad=$1
f="$ISI/$ad.mp4"
[ -f "$f" ] || { echo "$ad DOSYA-YOK"; exit 1; }
LOG="$ROOT/.calisma/t133/log"
kf="$LOG/$ad.kf"
ffprobe -v error -select_streams v:0 -skip_frame nokey -show_entries frame=pts_time -of csv=p=0 "$f" \
  | tr -d '\r' | tr -d ',' | awk 'NF' | sort -n > "$kf"
fps=$(fps_of "$f")

hedefler=$(python -c "
import random
random.seed(1733)
print(' '.join('%.4f' % random.uniform(0.5, 19.5) for _ in range($N)))
")

esli="$LOG/$ad.esli"
if [ "$(awk 'NF' "$esli" 2>/dev/null | wc -l)" != "$N" ]; then
  : > "$esli"
  for t in $hedefler; do
    b1=$(date +%s%N)
    ffmpeg -hide_banner -v error -nostdin -ss "$t" -i "$f" -frames:v 1 -f null - >/dev/null 2>&1
    s1=$(( ($(date +%s%N) - b1) / 1000 ))
    b2=$(date +%s%N)
    ffmpeg -hide_banner -v error -nostdin -ss "$t" -i "$f" -frames:v 1 -c:v copy -f null - >/dev/null 2>&1
    s2=$(( ($(date +%s%N) - b2) / 1000 ))
    echo "$s1 $s2" >> "$esli"
  done
fi

python - "$kf" "$esli" "$fps" "$ad" "$N" <<'PY'
import sys, random, statistics

def kf_oku(yol):
    d = []
    for satir in open(yol, encoding="utf-8", errors="replace"):
        s = satir.strip().strip(",").strip()
        if s:
            d.append(float(s))
    return sorted(d)

kf = kf_oku(sys.argv[1])
coz, kop = [], []
for satir in open(sys.argv[2], encoding="utf-8", errors="replace"):
    p = satir.split()
    if len(p) == 2:
        coz.append(int(p[0]) / 1000.0)
        kop.append(int(p[1]) / 1000.0)
fps = float(sys.argv[3]); ad = sys.argv[4]; n = int(sys.argv[5])
random.seed(1733)
mes = []
for _ in range(n):
    t = random.uniform(0.5, 19.5)
    mes.append(t - max([k for k in kf if k <= t], default=0.0))
fark = sorted(c - k for c, k in zip(coz, kop))
q = statistics.quantiles(fark, n=4) if len(fark) >= 4 else [0.0, 0.0, 0.0]
print("%s ikare=%d yapisal_sn=%.3f yapisal_kare=%.1f netp50_ms=%.1f netiqr_ms=%.1f "
      "coz_p50=%.1f kopya_p50=%.1f"
      % (ad, len(kf), statistics.mean(mes), statistics.mean(mes) * fps,
         statistics.median(fark), q[2] - q[0],
         statistics.median(coz), statistics.median(kop)))
PY
