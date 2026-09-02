#!/usr/bin/env bash
set -u
cd "C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/agent-a3f969792f618f57f/.calisma/t102"
p () {
  ffmpeg -hide_banner -loglevel error -nostdin -i "$2" -i gui/parca-2.mkv \
    -lavfi "[0:v]trim=start_frame=3380:end_frame=3416,setpts=N/FRAME_RATE/TB[t];[1:v]trim=start_frame=3380:end_frame=3416,setpts=N/FRAME_RATE/TB[r];[t][r]psnr=stats_file=psnr-$1.log" \
    -f null - 2>/dev/null
  echo "--- $1"
  awk '{for(i=1;i<=NF;i++) if($i ~ /^psnr_avg:/){split($i,a,":"); printf "%s ", a[2]}} END{print ""}' "psnr-$1.log"
}
p auto gui/parca-2_shrunk.mp4
p hb   ciktilar/uzman-hb.mp4
