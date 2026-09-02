#!/usr/bin/env bash
set -u
SRC="C:/Users/Administrator/Desktop/Projeler/Vidshrink/.calisma/kaynak/kaynak-1080p60-hdr-17dk-yalniz-video.mkv"
for t in "$@"; do
  m=$(ffmpeg -hide_banner -nostdin -ss "$t" -t 20 -i "$SRC" \
        -vf "scale=320:180,fps=10,vmafmotion" -f null - 2>&1 \
      | sed -n 's/.*VMAF Motion avg: \([0-9.]*\).*/\1/p' | tail -1)
  c=$(ffmpeg -hide_banner -v error -nostdin -ss "$t" -t 20 -i "$SRC" \
        -vf "scale=320:180,select='gt(scene,0.105)',metadata=print:file=-" -an -f null - 2>&1 \
      | grep -c "lavfi.scene_score")
  echo "$t hareket=${m:-NA} kesim=$c"
done
