#!/usr/bin/env bash
set -u
. "$(dirname "$0")/ortak.sh"
mkdir -p "$CIKTI" "$IS/gecis"

KOD="-c:v libx264 -preset slow -pix_fmt yuv420p10le -an"

kodla () {
  ad="$1"; kol="$2"; hiz="$3"; vf="$4"
  cik="$CIKTI/${ad}-${kol}-${hiz}.mp4"
  if [ -f "$cik" ]; then echo "atlandi ${ad}-${kol}-${hiz}"; return; fi
  gl="$IS/gecis/${ad}-${kol}-${hiz}"
  ffmpeg -hide_banner -loglevel error -nostdin -y -i "$KAYNAK/$ad.mkv" -vf "$vf" \
    $KOD -b:v "${hiz}k" -pass 1 -passlogfile "$gl" $RENK -f mp4 NUL 2>/dev/null
  ffmpeg -hide_banner -loglevel error -nostdin -y -i "$KAYNAK/$ad.mkv" -vf "$vf" \
    $KOD -b:v "${hiz}k" -pass 2 -passlogfile "$gl" $RENK -movflags +faststart "$cik"
  if [ -f "$cik" ]; then echo "kodlandi ${ad}-${kol}-${hiz}"; else echo "HATA ${ad}-${kol}-${hiz}"; fi
}

for s in "KA|804|138" "KB|872|104" "KC|1036|22" "KD|804|120" "VD|804|138"; do
  ad="${s%%|*}"; rest="${s#*|}"; h="${rest%%|*}"; y="${rest#*|}"
  if [ "$ad" = "VD" ]; then hizlar="2000"; else hizlar="1000 2000 4000"; fi
  for hiz in $hizlar; do
    kodla "$ad" "duz"  "$hiz" "null"
    kodla "$ad" "kirp" "$hiz" "crop=1920:$h:0:$y"
  done
done
echo "KOS BITTI"
