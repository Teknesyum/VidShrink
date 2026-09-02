#!/usr/bin/env bash
set -u
cd "$(dirname "$0")" || exit 1
P=pin
olc () { # $1=test $2=ref $3=kilit(0|1) $4=etiket
  local L=""
  [ "$3" = "1" ] && L=",settb=AVTB,setpts=N"
  ffmpeg -v error -y -i "$1" -i "$2" -lavfi \
    "[0:v]null$L[t];[1:v]null$L[r];[t][r]libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path=$P/$4.json" \
    -f null - 2>/dev/null
  python -c "
import json,sys
d=json.load(open('$P/$4.json'))
f=[x['metrics']['vmaf'] for x in d['frames']]
print('$4', 'kare=%d'%len(f), 'ort=%.4f'%(sum(f)/len(f)), 'min=%.4f'%min(f))
"
}
for k in 0 0.010 0.020 0.040 0.050; do
  olc "$P/same.mp4" "$P/k$k.mkv" 0 "k$k-kilitsiz"
  olc "$P/same.mp4" "$P/k$k.mkv" 1 "k$k-kilitli"
done
olc "$P/same.mp4" "$P/kaymis-ref-select.mkv"    0 "ref1kare-select-kilitsiz"
olc "$P/same.mp4" "$P/kaymis-ref-2kare.mkv"     0 "ref2kare-kilitsiz"
olc "$P/same.mp4" "$P/kaymis-ref-itsoffset.mkv" 0 "refitsoffset-kilitsiz"
olc "$P/same.mp4" "$P/kaymis-ref-select.mkv"    1 "ref1kare-select-kilitli"
olc "$P/same.mp4" "$P/kaymis-ref-2kare.mkv"     1 "ref2kare-kilitli"
