set -u
D=.calisma/nvenc-2/kabul
mkdir -p $D
src="-f lavfi -i testsrc2=s=640x360:r=24:d=1"
kos() {
  ad="$1"; kod="$2"; shift 2
  out="$D/$ad-$kod.mp4"
  err="$D/$ad-$kod.err"
  ffmpeg -hide_banner -nostdin -loglevel error -y $src -c:v $kod -b:v 1000k "$@" "$out" 2>"$err"
  rc=$?
  if [ $rc -eq 0 ]; then
    pix=$(ffprobe -v error -select_streams v:0 -show_entries stream=pix_fmt,profile -of csv=p=0 "$out")
    bayt=$(stat -c %s "$out")
    echo "$kod | $ad | KABUL | $pix | $bayt bayt"
  else
    echo "$kod | $ad | RED | $(head -1 "$err")"
  fi
}
for kod in hevc_nvenc av1_nvenc; do
  kos taban $kod
  kos highbitdepth $kod -highbitdepth 1
  kos tf_level $kod -tf_level 4
  kos lookahead $kod -rc-lookahead 20 -lookahead_level 3
  kos spatialaq $kod -spatial-aq 1 -aq-strength 4
  kos NEGATIF-uydurma $kod -zipzop_level 4
  kos NEGATIF-sinirdisi $kod -aq-strength 99
done
