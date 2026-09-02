#!/usr/bin/env bash
set -eu
cd "$(dirname "$0")" || exit 1
K=/c/Users/Administrator/Desktop/Projeler/Vidshrink/.calisma/kaynak
mkdir -p s92
ffmpeg -v error -y -t 4 -i "$K/parca-1.mkv" -c copy s92/ref1.mkv
ffmpeg -v error -y -t 4 -i "$K/parca-2.mkv" -c copy s92/ref2.mkv
for n in 1 2; do
  ffmpeg -v error -y -i "s92/ref$n.mkv" -c:v libx265 -crf 32 -preset veryfast -x265-params pools=2:log-level=error -threads 2 -an "s92/p$n-x265.mkv"
done
echo hazir
ls -la s92
