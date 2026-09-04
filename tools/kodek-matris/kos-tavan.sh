set -e
O=".calisma/kodek-matris"
K2=".calisma/kaynak/parca-2-yalniz-video.mkv"
K3=".calisma/kaynak/parca-3-yalniz-video.mkv"
L="$O/tavan-log.txt"
M="model=version=vmaf_v0.6.1neg"

tavan() {
  local tag=$1 src=$2 w=$3 h=$4
  echo "### tavan $tag  $w x $h ###" | tee -a "$L"
  ffmpeg -hide_banner -y -i "$src" -i "$src" -lavfi \
"[1:v]scale=$w:$h:flags=lanczos,scale=1920:1080:flags=bicubic[d];[d][0:v]libvmaf=$M:log_fmt=json:log_path=$O/tv_$tag.json" \
    -f null - >"$O/log_tv_$tag.txt" 2>&1
  python -c "
import json,sys
d=json.load(open('$O/tv_$tag.json'))
s=[f['metrics']['vmaf'] for f in d['frames']]
print('$tag harm=%.2f mean=%.2f'%(len(s)/sum(1/max(x,1.0) for x in s), sum(s)/len(s)))
" | tee -a "$L"
}

tavan p2_882  "$K2" 882 496
tavan p2_1280 "$K2" 1280 720
tavan p2_1650 "$K2" 1650 928
tavan p3_882  "$K3" 882 496
tavan p3_1280 "$K3" 1280 720
echo "TAVAN TAMAM" | tee -a "$L"
