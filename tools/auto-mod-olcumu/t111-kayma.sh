#!/usr/bin/env bash
set -u
BASE="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$BASE/.calisma/t111"
REF="gui/parca-2.mkv"

akislar () {
  ffprobe -v error -show_entries stream=index,codec_type,start_time -of csv=p=0 "$1"
}

enerken () {
  akislar "$1" | awk -F, '$3!="N/A"{v=$3+0; if(!s||v<m){m=v;s=1}} END{if(s)printf "%.6f", m; else printf "N/A"}'
}

video () {
  ffprobe -v error -select_streams v:0 -show_entries stream=start_time -of csv=p=0 "$1"
}

sat () {
  ad="$1"; f="$2"
  [ -f "$f" ] || { printf "%-22s | %s\n" "$ad" "dosya yok"; return; }
  v=$(video "$f"); e=$(enerken "$f")
  n=$(akislar "$f" | wc -l)
  k=$(awk -v a="$v" -v b="$e" 'BEGIN{printf "%.6f", a-b}')
  kare=$(awk -v k="$k" 'BEGIN{printf "%.3f", k*60}')
  printf "%-22s | akis %d | video %s | en erken %s | kayma %s s = %s kare\n" "$ad" "$n" "$v" "$e" "$k" "$kare"
}

echo "=== kaynak"
sat "kaynak parca-2.mkv" "$REF"
echo "=== cikti"
sat "auto"                "gui/parca-2_shrunk.mp4"
for n in e1-preset4 e2-gop300 e3-olcek810 uzman-biz3 uzman-hb uzman-hb2 y1-g300-izgara y2-g300-hizali y3-hizali-boyutesit; do
  sat "$n" "ciktilar/${n}.mp4"
done
