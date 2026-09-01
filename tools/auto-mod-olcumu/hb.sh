#!/usr/bin/env bash
set -u
cd "C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/agent-a3f969792f618f57f/.calisma/t102"
mkdir -p ciktilar
HandBrakeCLI -i gui/parca-2.mkv -o ciktilar/uzman-hb.mp4 \
  -e x265_10bit --encoder-preset slow \
  -b 2026 --multi-pass --turbo \
  -E ca_aac -B 128 --mixdown stereo \
  -w 1920 -l 1080 --crop-mode none -r 60 --cfr \
  -f av_mp4 -O 2>&1 | tail -20
echo "hb bitti $(stat -c %s ciktilar/uzman-hb.mp4)"
