#!/usr/bin/env bash
# K2 ucuncu sorusu: libsvtav1 verilen anahtari gercekten okuyor mu?
# DUZENEK "K4: cikis kodu destek demek degil" kapisi:
# ayni parametreyle iki kosumdan tekrar gurultusu, iki farkli degerden fark;
# destek ancak fark > gurultu*2 VE fark > cikti/100 iken yazilir.
set -euo pipefail
KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
IS="${VIDSHRINK_IS:-T114}"
D="$KOK/.calisma/$IS/k2kapi"
S="$KOK/.calisma/$IS/kaynak/p1-karisik.mkv"
mkdir -p "$D"
[ -s "$S" ] || { echo "pencere yok: $S" >&2; exit 1; }

SURE=6
BITRATE=8230k
PRESET=6
LP=6

kodla() { # ad params
  local ad="$1" params="$2" log="$D/gecis-$1"
  local out="$D/$ad.mkv"
  if [ -s "$out" ]; then echo "$ad zaten var" >&2; return 0; fi
  ffmpeg -hide_banner -v error -y -ss 0 -t "$SURE" -i "$S" -an \
    -c:v libsvtav1 -b:v "$BITRATE" -preset "$PRESET" -pix_fmt yuv420p10le \
    -svtav1-params "$params" -pass 1 -passlogfile "$log" -f null - 
  ffmpeg -hide_banner -v error -y -ss 0 -t "$SURE" -i "$S" -an \
    -c:v libsvtav1 -b:v "$BITRATE" -preset "$PRESET" -pix_fmt yuv420p10le \
    -svtav1-params "$params" -pass 2 -passlogfile "$log" "$out.yarim.mkv"
  mv "$out.yarim.mkv" "$out"
}

bayt() { stat -c %s "$D/$1.mkv"; }

kodla kontrol-1 "lp=$LP"
kodla kontrol-2 "lp=$LP"
kodla kontrol-3 "lp=$LP"
kodla kontrol-4 "lp=$LP"
kodla qcomp-a    "lp=$LP:qcomp=0.40"
kodla qcomp-b    "lp=$LP:qcomp=0.75"
kodla qpscs-a    "lp=$LP:qp-scale-compress-strength=0"
kodla qpscs-b    "lp=$LP:qp-scale-compress-strength=3"
kodla zones-a    "lp=$LP:zones=0,179,b=2.00"
kodla zones-b    "lp=$LP:zones=0,179,b=0.50"

# Tekrar gurultusu dort kontrol kosumunun en genis araligidir; tek cift
# gurultuyu oldugundan kucuk gosterebilir (T193'te 456 B'ye karsi gercek 15 KB).
MIN=""; MAX=""
for i in 1 2 3 4; do
  v=$(bayt "kontrol-$i")
  [ -z "$MIN" ] && MIN=$v && MAX=$v
  [ "$v" -lt "$MIN" ] && MIN=$v
  [ "$v" -gt "$MAX" ] && MAX=$v
done
GURULTU=$(( MAX - MIN ))
K1=$MIN; K2=$MAX

CSV="$D/kapi.csv"
echo "anahtar;a_deger;b_deger;a_bayt;b_bayt;fark_bayt;gurultu_bayt;esik_gurultu2;esik_yuzde1;destek" > "$CSV"
echo "kontrol;lp=$LP x4 (min);lp=$LP x4 (max);$K1;$K2;$GURULTU;$GURULTU;;;-" >> "$CSV"

satir() { # anahtar a_deger b_deger dosya_a dosya_b
  local A B F E1 E2 D1
  A=$(bayt "$4"); B=$(bayt "$5")
  F=$(( A > B ? A - B : B - A ))
  E1=$(( GURULTU * 2 )); E2=$(( A / 100 ))
  if [ "$F" -gt "$E1" ] && [ "$F" -gt "$E2" ]; then D1=evet; else D1=hayir; fi
  echo "$1;$2;$3;$A;$B;$F;$GURULTU;$E1;$E2;$D1" >> "$CSV"
}

satir qcomp "qcomp=0.40" "qcomp=0.75" qcomp-a qcomp-b
satir qp-scale-compress-strength "=0" "=3" qpscs-a qpscs-b
satir zones "b=2.00" "b=0.50" zones-a zones-b

cat "$CSV"
