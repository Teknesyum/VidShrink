#!/usr/bin/env bash
set -u
cd "$(dirname "$0")" || exit 1
ABS="$(cygpath -w "$PWD/pin/elle-mutlak.json")"
ESC=$(python esc.py "$ABS")
echo "windows yolu : $ABS"
echo "filtreye giren: $ESC"
rm -f pin/elle-mutlak.json
ffmpeg -y -i pin/same.mp4 -i pin/src.mp4 -lavfi \
  "[0:v]null,settb=AVTB,setpts=N[t];[1:v]null,settb=AVTB,setpts=N[r];[t][r]libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path=$ESC" \
  -f null - 2>pin/elle-mutlak.stderr
echo "ffmpeg rc=$?"
echo "--- stderr'de hata araniyor ---"
grep -icE "error|invalid|unable|no such file" pin/elle-mutlak.stderr
grep -iE "error|invalid|unable|no such file" pin/elle-mutlak.stderr | head -5
echo "--- gunluk dosyasi ---"
ls -la pin/elle-mutlak.json 2>&1
python -c "
import json
d=json.load(open('pin/elle-mutlak.json'))
f=[x['metrics']['vmaf'] for x in d['frames']]
print('kare=%d ort=%.4f'%(len(f),sum(f)/len(f)))
"
