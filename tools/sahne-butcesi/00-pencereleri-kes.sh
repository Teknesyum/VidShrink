#!/usr/bin/env bash
# Olcum pencerelerini keser. Pencere kaynagi ve zaman araligi tek yerden okunur:
# tools/sahne-butcesi/pencereler.tsv (VIDSHRINK_PENCERE_TABLOSU ile degistirilebilir).
set -euo pipefail
KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
HAVUZ="${VIDSHRINK_KAYNAK:-$KOK/.calisma/kaynak}"
IS="${VIDSHRINK_IS:-T114}"
TABLO="${VIDSHRINK_PENCERE_TABLOSU:-$KOK/tools/sahne-butcesi/pencereler.tsv}"
D="$KOK/.calisma/$IS/kaynak"
mkdir -p "$D"

[ -s "$TABLO" ] || { echo "pencere tablosu yok: $TABLO" >&2; exit 1; }
[ -d "$HAVUZ" ] || { echo "kaynak havuzu yok: $HAVUZ (VIDSHRINK_KAYNAK ile gosterin)" >&2; exit 1; }

eksik=0
while IFS=$'\t' read -r ad kaynak bas sure not; do
  case "$ad" in ''|\#*) continue;; esac
  [ -s "$HAVUZ/$kaynak" ] || { echo "kaynak dosya yok: $HAVUZ/$kaynak (pencere $ad)" >&2; eksik=1; }
done < "$TABLO"
[ "$eksik" = 0 ] || exit 1

MANIFEST="$KOK/.calisma/$IS/pencereler.json"
: > "$MANIFEST.yarim"
echo "[" >> "$MANIFEST.yarim"
ilk=1
while IFS=$'\t' read -r ad kaynak bas sure not; do
  case "$ad" in ''|\#*) continue;; esac
  if [ -s "$D/$ad.mkv" ]; then
    echo "$ad zaten var"
  else
    ffmpeg -y -v error -ss "$bas" -t "$sure" -i "$HAVUZ/$kaynak" -an \
      -c:v libx265 -preset veryfast -crf 12 -pix_fmt yuv420p10le \
      -x265-params pools=8:log-level=error -threads 8 "$D/$ad.yarim.mkv"
    mv "$D/$ad.yarim.mkv" "$D/$ad.mkv"
    echo "$ad hazir"
  fi
  [ "$ilk" = 1 ] || echo "," >> "$MANIFEST.yarim"
  ilk=0
  printf '  {"Ad":"%s","Dosya":"%s.mkv","Not":"%s"}' "$ad" "$ad" "$not" >> "$MANIFEST.yarim"
done < "$TABLO"
printf '\n]\n' >> "$MANIFEST.yarim"
mv "$MANIFEST.yarim" "$MANIFEST"

ls -l "$D"
