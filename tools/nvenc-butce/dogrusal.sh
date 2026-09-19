set -e
K=.calisma/nvenc-butce/kesit.mkv
O=.calisma/nvenc-butce
echo "kodek|istenen|teslim|sapma%"
for kod in hevc_nvenc av1_nvenc; do
  if [ "$kod" = "av1_nvenc" ]; then pre=p6; else pre=p4; fi
  for k in 1000 1050 2000 2100 3500 3675; do
    mr=$((k*2)); bs=$((k*2))
    ffmpeg -hide_banner -loglevel error -y -i "$K" -vf scale=1882:802 -c:v $kod -preset $pre -rc vbr \
      -multipass fullres -g 120 -b:v ${k}k -maxrate ${mr}k -bufsize ${bs}k -an "$O/o.mkv"
    by=$(stat -c %s "$O/o.mkv")
    t=$(python -c "print(f'{$by*8/10/1000:.1f}')")
    s=$(python -c "print(f'{($by*8/10/1000-$k)/$k*100:+.2f}')")
    echo "$kod|$k|$t|$s"
  done
done
