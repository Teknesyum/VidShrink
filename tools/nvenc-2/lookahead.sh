set -u
# fable'in ucuncu kosulu: lookahead_level 1 ile 3 karsilastirilsin, tabloya kodlama
# suresi ve gerceklesen I-kare sayisi girsin. Taban da tekrar kosuyor — ayni turda
# olculmeyen sure kiyaslanamaz.
D=.calisma/nvenc-2
B=tools/VidShrink.Bench/bin/Release/net8.0/VidShrink.Bench.dll
OUT=${OUT:-$D/lookahead.tsv}
echo -e "kesit\tkodek\tkol\tsaniye\tikare\tbayt\tkbps\tvmafneg_ort\tvmafneg_harm\tvmafneg_p10\txpsnr" > $OUT
kos() {
  kesit="$1"; kod="$2"; on="$3"; ad="$4"; shift 4
  src="$D/kesit-$kesit.mkv"
  out="$D/la-$kesit-$kod-$ad.mp4"
  t0=$(date +%s.%N)
  ffmpeg -hide_banner -nostdin -loglevel error -y -i "$src" -c:v $kod -preset $on \
    -b:v 2000k -maxrate 4000k -bufsize 4000k -rc vbr -multipass fullres -g 120 -pix_fmt yuv420p \
    "$@" -an "$out" || { echo -e "$kesit\t$kod\t$ad\tKODLAMA-RED" >> $OUT; return; }
  t1=$(date +%s.%N)
  ik=$(ffprobe -v error -select_streams v:0 -show_frames -show_entries frame=pict_type -of csv "$out" | grep -c ",I")
  j="$D/la-olcu-$kesit-$kod-$ad.json"
  dotnet $B measure-pair "$src" "$out" --fps 24/1 --out "$j" > /dev/null 2>&1 || { echo -e "$kesit\t$kod\t$ad\tOLCUM-RED" >> $OUT; return; }
  python -c "
import json
m=json.load(open(r'$j'))
print('\t'.join(['$kesit','$kod','$ad','%.1f'%($t1-$t0),'$ik',str(m['Bayt']),f\"{m['Kbps']:.1f}\",f\"{m['VmafNegMean']:.3f}\",f\"{m['VmafNegHarmonic']:.3f}\",f\"{m['VmafNegP10']:.3f}\",f\"{m['Xpsnr']:.3f}\"]))
" >> $OUT
  rm -f "$out"
  tail -1 $OUT
}
for k in karanlik parlak hareketli; do
  kos $k hevc_nvenc p4 taban
  kos $k hevc_nvenc p4 la1 -rc-lookahead 20 -lookahead_level 1
  kos $k hevc_nvenc p4 la3 -rc-lookahead 20 -lookahead_level 3
  kos $k h264_nvenc p4 taban
  kos $k h264_nvenc p4 la1 -rc-lookahead 20 -lookahead_level 1
  kos $k h264_nvenc p4 la3 -rc-lookahead 20 -lookahead_level 3
  kos $k av1_nvenc p6 taban
  kos $k av1_nvenc p6 la1 -rc-lookahead 20 -lookahead_level 1
  kos $k av1_nvenc p6 la3 -rc-lookahead 20 -lookahead_level 3
done
echo BITTI
