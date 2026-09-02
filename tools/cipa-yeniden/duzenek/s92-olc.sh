#!/usr/bin/env bash
set -u
cd "$(dirname "$0")" || exit 1
N="zscale=w=1920:h=1080:min=bt2020nc:tin=smpte2084:pin=bt2020:rin=full:m=bt2020nc:t=smpte2084:p=bt2020:r=full,format=yuv420p10le"
for n in 1 2; do
  for kilit in kilitsiz kilitli; do
    L=""
    [ "$kilit" = "kilitli" ] && L=",settb=AVTB,setpts=N"
    ffmpeg -v error -y -i "s92/p$n-x265.mkv" -i "s92/ref$n.mkv" -lavfi \
      "[0:v]$N$L[t];[1:v]$N$L[r];[t][r]libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path=s92/vmaf-p$n-$kilit.json" \
      -f null - 2>/dev/null
  done
done
python - <<'PY'
import json
def load(p):
    d=json.load(open(p))
    return [x['metrics']['vmaf'] for x in d['frames']]
for n in (1,2):
    a=load('s92/vmaf-p%d-kilitsiz.json'%n)
    b=load('s92/vmaf-p%d-kilitli.json'%n)
    def st(f):
        s=sorted(f); k=(len(s)-1)*0.10; lo=int(k); hi=min(lo+1,len(s)-1)
        p10=s[lo]+(s[hi]-s[lo])*(k-lo)
        return len(f), sum(f)/len(f), p10, min(f)
    print('p%d kilitsiz kare=%d ort=%.4f p10=%.4f min=%.4f'%((n,)+st(a)))
    print('p%d kilitli  kare=%d ort=%.4f p10=%.4f min=%.4f'%((n,)+st(b)))
    m=min(len(a),len(b))
    diff=[(i,a[i],b[i]) for i in range(m) if abs(a[i]-b[i])>1e-9]
    print('  ayni uzunlukta karsilastirma: %d kare; farkli kare sayisi=%d, ayni=%d'%(m,len(diff),m-len(diff)))
    big=[d for d in diff if abs(d[1]-d[2])>1.0]
    print('  farki >1 puan olan kare sayisi=%d'%len(big))
    for i,x,y in diff[:20]:
        print('   kare %d: kilitsiz %.4f kilitli %.4f fark %.4f'%(i,x,y,y-x))
PY
