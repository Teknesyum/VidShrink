#!/usr/bin/env bash
set -u
cd "C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/agent-a3f969792f618f57f/.calisma/t102"
ffmpeg -hide_banner -loglevel error -nostdin -i gui/parca-2_shrunk.mp4 -i gui/parca-2.mkv \
  -lavfi "[0:v][1:v]libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path=vmaf/auto-olceksiz.json" \
  -f null - && echo "olculdu auto-olceksiz" || echo "HATA"
