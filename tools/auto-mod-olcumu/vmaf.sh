#!/usr/bin/env bash
set -u
cd "C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/agent-a3f969792f618f57f/.calisma/t102"
REF="gui/parca-2.mkv"
mkdir -p vmaf

olc () {
  name="$1"; test="$2"
  if [ ! -f "$test" ]; then echo "YOK $name"; return; fi
  if [ -f "vmaf/${name}.json" ]; then echo "atlandi $name"; else
    ffmpeg -hide_banner -loglevel error -nostdin -i "$test" -i "$REF" \
      -lavfi "[0:v]scale=w=1920:h=1080:flags=lanczos[t];[t][1:v]libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path=vmaf/${name}.json" \
      -f null -
  fi
  python -c "
import json,sys,os
n=sys.argv[1]; p=sys.argv[2]
s=[f['metrics'].get('vmaf', f['metrics'].get('vmaf_neg')) for f in json.load(open('vmaf/%s.json'%n))['frames']]
s=[x for x in s if x is not None]
srt=sorted(s); r=0.10*(len(srt)-1); lo=int(r//1); hi=-(-r//1); hi=int(hi)
p10=srt[lo] if lo==hi else srt[lo]+(srt[hi]-srt[lo])*(r-lo)
harm=len(s)/sum(1.0/max(x,1.0) for x in s)
print('%-14s | %10d | %6.3f | %6.3f | %6.3f | %d kare' % (n, os.path.getsize(p), sum(s)/len(s), harm, p10, len(s)))
" "$name" "$test"
}

echo "ad             |      bayt |   ort |  harm |   p10 | kare"
olc "auto"        "gui/parca-2_shrunk.mp4"
olc "e1-preset4"  "ciktilar/e1-preset4.mp4"
olc "e2-gop300"   "ciktilar/e2-gop300.mp4"
olc "e3-olcek810" "ciktilar/e3-olcek810.mp4"
olc "uzman-biz"   "ciktilar/uzman-biz.mp4"
olc "uzman-hb"    "ciktilar/uzman-hb.mp4"
