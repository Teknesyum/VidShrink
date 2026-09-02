#!/usr/bin/env bash
set -u
cd "C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/agent-a3f969792f618f57f/.calisma/t102"
while [ ! -f vmaf/uzman-hb.json ]; do sleep 20; done
while [ ! -f ciktilar/uzman-biz3.mp4 ]; do sleep 20; done
until ffprobe -v error -select_streams v:0 -show_entries stream=nb_frames -of csv=p=0 ciktilar/uzman-biz3.mp4 >/dev/null 2>&1; do sleep 20; done
ffmpeg -hide_banner -loglevel error -nostdin -i ciktilar/uzman-biz3.mp4 -i gui/parca-2.mkv \
  -lavfi "[0:v]scale=w=1920:h=1080:flags=lanczos[t];[t][1:v]libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path=vmaf/uzman-biz3.json" \
  -f null - && echo "olculdu uzman-biz3" || echo "HATA uzman-biz3"
echo "SON BITTI"
