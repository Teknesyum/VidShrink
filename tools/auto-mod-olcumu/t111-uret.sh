#!/usr/bin/env bash
set -u
BASE="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$BASE/.calisma/t111"
SRC="gui/parca-2.mkv"
mkdir -p ciktilar log vmaf

PSY="tune=0:enable-variance-boost=1:variance-boost-strength=2:lp=4"
COL="-color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc"
TH=4

enc () {
  name="$1"; preset="$2"; gop="$3"; br="$4"; vf="$5"; fkf="$6"
  out="ciktilar/${name}.mp4"
  if [ -f "$out" ]; then echo "atlandi $name"; return; fi
  vfarg=(); [ -n "$vf" ] && vfarg=(-vf "$vf")
  fkfarg=(); [ -n "$fkf" ] && fkfarg=(-force_key_frames "$fkf")
  tmp="ciktilar/.${name}.yaziliyor.mp4"
  rm -f "$tmp"
  echo "=== $name preset=$preset gop=$gop br=${br}k vf=${vf:-yok} fkf=${fkf:-yok} $(date +%T)"
  for p in 1 2; do
    if [ "$p" = 1 ]; then extra=(-an -f null NUL); else extra=(-c:a aac -b:a 128k -movflags +faststart "$tmp"); fi
    ffmpeg -hide_banner -loglevel error -y -nostdin -threads $TH -i "$SRC" "${vfarg[@]}" \
      -c:v libsvtav1 -preset "$preset" -b:v "${br}k" -pass $p -passlogfile "log/${name}" \
      -g "$gop" "${fkfarg[@]}" -pix_fmt p010le -svtav1-params "$PSY" $COL "${extra[@]}" || { echo "HATA $name pass$p"; return; }
  done
  mv -f "$tmp" "$out" || { echo "HATA $name tasima"; return; }
  echo "bitti $name $(stat -c %s "$out") $(date +%T)"
}

hb () {
  name="$1"; br="$2"
  out="ciktilar/${name}.mp4"
  if [ -f "$out" ]; then echo "atlandi $name"; return; fi
  tmp="ciktilar/.${name}.yaziliyor.mp4"
  rm -f "$tmp"
  echo "=== $name HandBrake b=${br} $(date +%T)"
  HandBrakeCLI -i "$SRC" -o "$tmp" \
    -e x265_10bit --encoder-preset slow --encopts "pools=4" \
    -b "$br" --multi-pass --turbo \
    -E ca_aac -B 128 --mixdown stereo \
    -w 1920 -l 1080 --crop-mode none -r 60 --cfr \
    -f av_mp4 -O >"log/${name}.log" 2>&1 || { echo "HATA $name HandBrake"; return; }
  mv -f "$tmp" "$out" || { echo "HATA $name tasima"; return; }
  echo "bitti $name $(stat -c %s "$out") $(date +%T)"
}

enc "e1-preset4"          4 120 2026 "" ""
enc "e2-gop300"           6 300 2026 "" ""
enc "e3-olcek810"         6 120 2026 "scale=1440:810:flags=lanczos" ""
enc "uzman-biz3"          4 300 2605 "" ""
enc "y1-g300-izgara"      6 300 2026 "" ""
enc "y2-g300-hizali"      6 300 2026 "" "28.353,56.870"
enc "y3-hizali-boyutesit" 6 300 2144 "" "28.353,56.870"
hb  "uzman-hb"  2026
hb  "uzman-hb2" 1900
echo "URETIM BITTI $(date +%T)"
