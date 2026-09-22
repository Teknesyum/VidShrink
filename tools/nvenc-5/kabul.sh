set -u
D=${D:-.calisma/nvenc-5}/kabul
mkdir -p $D
src="-f lavfi -i testsrc2=s=640x360:r=24:d=1"
urun="-rc vbr -multipass fullres -maxrate 2000k -bufsize 2000k -rc-lookahead 20 -lookahead_level 3"
kos() {
  ad="$1"; kod="$2"; shift 2
  out="$D/$ad-$kod.mp4"
  err="$D/$ad-$kod.err"
  ffmpeg -hide_banner -nostdin -loglevel error -threads 4 -y $src -c:v $kod -b:v 1000k "$@" "$out" 2>"$err"
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
  if [ $kod = av1_nvenc ]; then p=p6; else p=p4; fi
  kos taban $kod
  kos uhq $kod -tune uhq
  kos p7 $kod -preset p7
  kos urun $kod -preset $p $urun
  kos urun+uhq $kod -preset $p $urun -tune uhq
  kos urun+p7 $kod -preset p7 $urun
  kos urun+uhq+p7 $kod -preset p7 $urun -tune uhq
  kos NEGATIF-uydurma $kod -tune zipzop
  kos NEGATIF-sinirdisi $kod -tune 9
done
