#!/usr/bin/env bash
set -u
. "$(dirname "$0")/ortak.sh"
mkdir -p "$KAYNAK"

uret () {
  ad="$1"; taban="$2"; ss="$3"; sure="$4"; h="$5"; ust="$6"; alt="$7"; tip="$8"
  hedef="$KAYNAK/$ad.mkv"
  if [ -f "$hedef" ]; then echo "atlandi $ad"; return; fi
  yoff=$(( (1080 - h) / 2 ))
  case "$tip" in
    kenarsiz)
      vf="null"
      ffmpeg -hide_banner -loglevel error -nostdin -ss "$ss" -t "$sure" -i "$HAVUZ/$taban" \
        -vf "$vf" $ARA_KOD $RENK "$hedef" ;;
    duz)
      if [ "$ad" = "KA" ]; then ek=",fade=t=in:st=0:d=1.5:color=black"; else ek=""; fi
      vf="crop=1920:$h:0:$yoff,pad=1920:1080:0:$ust:black$ek"
      ffmpeg -hide_banner -loglevel error -nostdin -ss "$ss" -t "$sure" -i "$HAVUZ/$taban" \
        -vf "$vf" $ARA_KOD $RENK "$hedef" ;;
    gurultulu)
      ffmpeg -hide_banner -loglevel error -nostdin -ss "$ss" -t "$sure" -i "$HAVUZ/$taban" \
        -f lavfi -t "$sure" -i "color=c=black:s=1920x1080:r=60,format=yuv420p10le,noise=alls=9:allf=t+u" \
        -filter_complex "[0:v]crop=1920:$h:0:$yoff[akt];[1:v][akt]overlay=0:$ust:format=yuv420p10[v]" \
        -map "[v]" $ARA_KOD $RENK "$hedef" ;;
    degisken)
      yari=$(( sure / 2 ))
      ikinci=$(( ss + yari ))
      ffmpeg -hide_banner -loglevel error -nostdin -ss "$ss" -t "$yari" -i "$HAVUZ/$taban" \
        -ss "$ikinci" -t "$yari" -i "$HAVUZ/$taban" \
        -filter_complex "[0:v]crop=1920:$h:0:$yoff,pad=1920:1080:0:$ust:black[a];[1:v]null[b];[a][b]concat=n=2:v=1:a=0[v]" \
        -map "[v]" $ARA_KOD $RENK "$hedef" ;;
  esac
  if [ -f "$hedef" ]; then echo "uretildi $ad"; else echo "HATA $ad"; fi
}

satirlar | while IFS= read -r s; do
  uret "$(alan "$s" 1)" "$(alan "$s" 2)" "$(alan "$s" 3)" "$(alan "$s" 4)" \
       "$(alan "$s" 5)" "$(alan "$s" 6)" "$(alan "$s" 7)" "$(alan "$s" 8)"
done
echo "KAYNAK BITTI"
