K=.calisma/nvenc-butce/kesit.mkv; O=.calisma/nvenc-butce
kos() { ffmpeg -hide_banner -loglevel error -y -i "$K" -vf scale=$4 -c:v $1 -preset $2 -rc vbr \
  -multipass fullres -g 120 -b:v $3k -maxrate $(($3*2))k -bufsize $(($3*2))k -an "$O/o.mkv"
  python -c "import os;print(f'{os.path.getsize(\"$O/o.mkv\")*8/10/1000:.1f}')" ; }
echo "kodek|geo|taban|teslim|hedef|istek|son|sapma%"
for kod in "hevc_nvenc p4" "av1_nvenc p6"; do set -- $kod; c=$1; p=$2
for geo in 1882x802 1382x588; do
for pair in "1200 1500" "1600 2000" "2200 2700" "3000 3600"; do
  set -- $pair; taban=$1; hedef=$2
  t=$(kos $c $p $taban $geo)
  istek=$(python -c "print(int($taban*$hedef/$t))")
  s=$(kos $c $p $istek $geo)
  python -c "print(f'$c|$geo|$taban|$t|$hedef|$istek|$s|{($s-$hedef)/$hedef*100:+.2f}')"
done; done; done
