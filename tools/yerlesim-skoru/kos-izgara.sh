#!/bin/sh
# Bir kaynak icin yerlesim izgarasini kosar. TSV satirlari stdout'a.
# Kullanim: kos-izgara.sh <kaynak> <etiket> <kodlayici> <kbit> <cikti-dizini> [ek-satirlar-dosyasi]
SRC=$1; TAG=$2; ENC=$3; KBIT=$4; OUT=$5; EXTRA=$6
HERE=$(dirname "$0")
for wh in "1920 1080" "1600 900" "1280 720" "960 540"; do
  for f in 60 30 24; do
    set -- $wh
    "$HERE/olc.sh" "$SRC" "$TAG" "$1" "$2" "$f" "$KBIT" "$ENC" "$OUT" || echo "HATA $TAG $1x$2@$f" >&2
  done
done
if [ -n "$EXTRA" ] && [ -f "$EXTRA" ]; then
  while read -r w h f k; do
    [ -z "$w" ] && continue
    case "$w" in \#*) continue ;; esac
    "$HERE/olc.sh" "$SRC" "$TAG" "$w" "$h" "$f" "$k" "$ENC" "$OUT" || echo "HATA $TAG $w x$h@$f $k" >&2
  done < "$EXTRA"
fi
