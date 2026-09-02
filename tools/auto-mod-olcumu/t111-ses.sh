#!/usr/bin/env bash
set -u
BASE="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$BASE/.calisma/t111"
mkdir -p sessiz vmaf
REF="gui/parca-2.mkv"; T="gui/parca-2_shrunk.mp4"
M="model=version=vmaf_v0.6.1neg:n_threads=4"
S="scale=w=1920:h=1080:flags=lanczos"

[ -f sessiz/ref-sessiz.mkv ] || ffmpeg -hide_banner -loglevel error -nostdin -i "$REF" -map 0:v:0 -c copy sessiz/ref-sessiz.mkv
[ -f sessiz/test-sessiz.mp4 ] || ffmpeg -hide_banner -loglevel error -nostdin -i "$T" -map 0:v:0 -c copy sessiz/test-sessiz.mp4

for f in "$REF" "$T" sessiz/ref-sessiz.mkv sessiz/test-sessiz.mp4; do
  echo "== $f"
  ffprobe -v error -show_entries stream=index,codec_type,start_time -of csv=p=0 "$f"
done

olc () {
  ad="$1"; t="$2"; r="$3"
  [ -f "vmaf/ses-$ad.json" ] && { echo "atlandi $ad"; return; }
  g="[0:v]${S}[t];[t][1:v]libvmaf=${M}:log_fmt=json:log_path=vmaf/ses-${ad}.json"
  ffmpeg -hide_banner -loglevel error -nostdin -threads 4 -i "$t" -i "$r" -lavfi "$g" -f null - \
    && echo "olculdu $ad" || echo "HATA $ad"
}
olc "ikisi-sesli"    "$T"                    "$REF"
olc "test-sessiz"    sessiz/test-sessiz.mp4  "$REF"
olc "ref-sessiz"     "$T"                    sessiz/ref-sessiz.mkv
olc "ikisi-sessiz"   sessiz/test-sessiz.mp4  sessiz/ref-sessiz.mkv
echo "SES BITTI $(date +%T)"
