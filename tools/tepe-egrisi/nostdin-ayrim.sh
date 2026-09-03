#!/usr/bin/env bash
# -nostdin ile dis oldurmeyi ayirir: her turda iki kodlama es zamanli baslar,
# biri -nostdin'li biri -nostdinsiz. Dis toplu oldurme ikisini de vurur;
# stdin kusuru yalniz -nostdinsiz kolu vurur.
set -u
KOK="$(cd "$(dirname "$0")/../.." && pwd)"
CIK="$KOK/.calisma/t108/nostdin"
KAYNAK="$KOK/.calisma/t108/kaynak/hareketli-20sn.mkv"
mkdir -p "$CIK"
TUR=${1:-10}
echo "tur;kol;pid;cikis;baslangic;bitis" > "$CIK/sonuc.csv"

kos() {
  local tur=$1 kol=$2
  local bas son kod
  bas=$(date +%H:%M:%S)
  if [ "$kol" = "nostdinli" ]; then
    ffmpeg -nostdin -hide_banner -y -hwaccel auto -t 3 -i "$KAYNAK" \
      -c:v av1_nvenc -preset p5 -b:v 4000k -maxrate 4400k -bufsize 8800k \
      -pix_fmt p010le -an "$CIK/t${tur}-${kol}.mp4" >"$CIK/t${tur}-${kol}.err" 2>&1 &
  else
    ffmpeg -hide_banner -y -hwaccel auto -t 3 -i "$KAYNAK" \
      -c:v av1_nvenc -preset p5 -b:v 4000k -maxrate 4400k -bufsize 8800k \
      -pix_fmt p010le -an "$CIK/t${tur}-${kol}.mp4" >"$CIK/t${tur}-${kol}.err" 2>&1 &
  fi
  local pid=$!
  echo "$pid" > "$CIK/t${tur}-${kol}.pid"
  wait $pid; kod=$?
  son=$(date +%H:%M:%S)
  echo "$tur;$kol;$pid;$kod;$bas;$son" >> "$CIK/sonuc.csv"
}

for t in $(seq 1 "$TUR"); do
  kos "$t" nostdinsiz &
  A=$!
  kos "$t" nostdinli &
  B=$!
  wait $A; wait $B
  echo "tur $t bitti"
done
echo "ayrim bitti"
