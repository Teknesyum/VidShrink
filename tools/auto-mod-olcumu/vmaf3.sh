#!/usr/bin/env bash
set -u
cd "C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/agent-a3f969792f618f57f/.calisma/t102"
mkdir -p vmaf
olc () {
  n="$1"; t="$2"
  if [ ! -f "$t" ]; then echo "YOK $n"; return; fi
  if [ -f "vmaf/${n}.json" ]; then echo "atlandi $n"; return; fi
  ffmpeg -hide_banner -loglevel error -nostdin -i "$t" -i gui/parca-2.mkv \
    -lavfi "[0:v]scale=w=1920:h=1080:flags=lanczos[t];[t][1:v]libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path=vmaf/${n}.json" \
    -f null - && echo "olculdu $n" || echo "HATA $n"
}
olc "e1-preset4"  "ciktilar/e1-preset4.mp4"
olc "e2-gop300"   "ciktilar/e2-gop300.mp4"
olc "e3-olcek810" "ciktilar/e3-olcek810.mp4"
olc "uzman-hb"    "ciktilar/uzman-hb.mp4"
echo "VMAF3 BITTI"
