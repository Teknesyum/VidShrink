#!/usr/bin/env bash
set -u
cd "C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/agent-a3f969792f618f57f/.calisma/t102"
while [ ! -f vmaf/e1-preset4.json ]; do sleep 15; done
for pair in "e2-gop300:ciktilar/e2-gop300.mp4" "e3-olcek810:ciktilar/e3-olcek810.mp4"; do
  n="${pair%%:*}"; t="${pair#*:}"
  [ -f "vmaf/${n}.json" ] && { echo "atlandi $n"; continue; }
  ffmpeg -hide_banner -loglevel error -nostdin -i "$t" -i gui/parca-2.mkv \
    -lavfi "[0:v]scale=w=1920:h=1080:flags=lanczos[t];[t][1:v]libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path=vmaf/${n}.json" \
    -f null -
  echo "olculdu $n"
done
echo "VMAF2 BITTI"
