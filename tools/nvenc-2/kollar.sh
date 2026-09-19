set -u
D=.calisma/nvenc-2
B=tools/VidShrink.Bench/bin/Release/net8.0/VidShrink.Bench.dll
OUT=$D/kollar.tsv
echo -e "kesit\tkodek\tkol\tbayt\tkbps\tvmafneg_ort\tvmafneg_harm\tvmafneg_p10\txpsnr" > $OUT
kos() {
  kesit="$1"; kod="$2"; on="$3"; ad="$4"; shift 4
  src="$D/kesit-$kesit.mkv"
  out="$D/kol-$kesit-$kod-$ad.mp4"
  ffmpeg -hide_banner -nostdin -loglevel error -y -i "$src" -c:v $kod -preset $on \
    -b:v 2000k -maxrate 4000k -bufsize 4000k -rc vbr -multipass fullres -g 120 -pix_fmt yuv420p \
    "$@" -an "$out" || { echo -e "$kesit\t$kod\t$ad\tKODLAMA-RED" >> $OUT; return; }
  j="$D/olcu-$kesit-$kod-$ad.json"
  dotnet $B measure-pair "$src" "$out" --fps 24/1 --out "$j" > /dev/null 2>&1 || { echo -e "$kesit\t$kod\t$ad\tOLCUM-RED" >> $OUT; return; }
  python -c "
import json,sys
m=json.load(open(r'$j'))
print('\t'.join(['$kesit','$kod','$ad',str(m['Bayt']),f\"{m['Kbps']:.1f}\",f\"{m['VmafNegMean']:.3f}\",f\"{m['VmafNegHarmonic']:.3f}\",f\"{m['VmafNegP10']:.3f}\",f\"{m['Xpsnr']:.3f}\"]))
" >> $OUT
  rm -f "$out"
  tail -1 $OUT
}
for k in karanlik parlak hareketli; do
  kos $k hevc_nvenc p4 taban
  kos $k hevc_nvenc p4 highbitdepth -highbitdepth 1
  kos $k hevc_nvenc p4 lookahead -rc-lookahead 20 -lookahead_level 3
  kos $k hevc_nvenc p4 spatialaq -spatial-aq 1 -aq-strength 4
  kos $k av1_nvenc p6 taban
  kos $k av1_nvenc p6 highbitdepth -highbitdepth 1
  kos $k av1_nvenc p6 tf_level -tf_level 4
  kos $k av1_nvenc p6 lookahead -rc-lookahead 20 -lookahead_level 3
  kos $k av1_nvenc p6 spatialaq -spatial-aq 1 -aq-strength 4
done
echo BITTI
