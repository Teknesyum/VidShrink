#!/usr/bin/env bash
# Atlama maliyeti. Iki olcu, ikisi de ayni hedef kumesinde (tohum 1733, 24 hedef):
#  yapisal : hedeften geriye en yakin anahtar kareye kadarki mesafe (sn / kare).
#            Dosyadan tam hesaplanir, saat gurultusu yok.
#  net p50 : `ffmpeg -ss T -frames:v 1` p50 suresi eksi ayni hedeflerde
#            `-c:v copy` p50 suresi. T113'un `harita-baglantisi.md` olcusuyle
#            ayni tanim; oradaki gurultu tabani 0,5-4,0 ms.
set -u
. "$(dirname "$0")/ortak.sh"
N=24
ad=$1
f="$ISI/$ad.mp4"
[ -f "$f" ] || { echo "$ad DOSYA-YOK"; exit 1; }
kf="$ROOT/.calisma/t133/log/$ad.kf"
ffprobe -v error -select_streams v:0 -skip_frame nokey -show_entries frame=pts_time -of csv=p=0 "$f" | sort -n > "$kf"
fps=$(fps_of "$f")

hedefler=$(python -c "
import random
random.seed(1733)
print(' '.join('%.4f' % random.uniform(0.5, 19.5) for _ in range($N)))
")

olc_mod () {
  for t in $hedefler; do
    bas=$(date +%s%N)
    if [ "$1" = coz ]; then
      ffmpeg -hide_banner -v error -nostdin -ss "$t" -i "$f" -frames:v 1 -f null - >/dev/null 2>&1
    else
      ffmpeg -hide_banner -v error -nostdin -ss "$t" -i "$f" -frames:v 1 -c:v copy -f null - >/dev/null 2>&1
    fi
    echo $(( ($(date +%s%N) - bas) / 1000 ))
  done
}
olc_mod coz   > "$ROOT/.calisma/t133/log/$ad.coz"
olc_mod kopya > "$ROOT/.calisma/t133/log/$ad.kopya"

python - "$kf" "$ROOT/.calisma/t133/log/$ad.coz" "$ROOT/.calisma/t133/log/$ad.kopya" "$fps" "$ad" <<'PY'
import sys, random, statistics
kf = sorted(float(x) for x in open(sys.argv[1]) if x.strip())
coz = [int(x) / 1000.0 for x in open(sys.argv[2]) if x.strip()]
kop = [int(x) / 1000.0 for x in open(sys.argv[3]) if x.strip()]
fps = float(sys.argv[4]); ad = sys.argv[5]
random.seed(1733)
mes = []
for _ in range(24):
    t = random.uniform(0.5, 19.5)
    mes.append(t - max([k for k in kf if k <= t], default=0.0))
print("%s ikare=%d yapisal_sn=%.3f yapisal_kare=%.1f netp50_ms=%.1f coz_p50=%.1f kopya_p50=%.1f"
      % (ad, len(kf), statistics.mean(mes), statistics.mean(mes) * fps,
         statistics.median(coz) - statistics.median(kop),
         statistics.median(coz), statistics.median(kop)))
PY
