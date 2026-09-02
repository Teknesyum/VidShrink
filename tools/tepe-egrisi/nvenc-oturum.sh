#!/usr/bin/env bash
# nvenc es zamanli oturum sinirini ve sinir asildiginda olusan imzayi olcer.
set -u
KOK="$(cd "$(dirname "$0")/../.." && pwd)"
CIK="$KOK/.calisma/t108/nvenc"
KAYNAK="$KOK/.calisma/t108/kaynak/hareketli-20sn.mkv"
mkdir -p "$CIK"
N=${1:-14}
echo "no;pid;cikis;stderr_bayt" > "$CIK/sonuc.csv"
pids=()
for i in $(seq 1 "$N"); do
  ffmpeg -nostdin -hide_banner -y -hwaccel auto -t 8 -i "$KAYNAK" \
    -c:v av1_nvenc -preset p5 -b:v 4000k -pix_fmt p010le -an "$CIK/o$i.mp4" \
    >"$CIK/o$i.err" 2>&1 &
  pids+=("$i:$!")
done
sleep 3
nvidia-smi --query-gpu=encoder.stats.sessionCount --format=csv,noheader > "$CIK/oturum-sayisi.txt" 2>&1
for e in "${pids[@]}"; do
  i=${e%%:*}; p=${e##*:}
  wait "$p"; k=$?
  echo "$i;$p;$k;$(wc -c < "$CIK/o$i.err")" >> "$CIK/sonuc.csv"
done
echo "es zamanli oturum: $(cat "$CIK/oturum-sayisi.txt")"
echo "nvenc bitti"
