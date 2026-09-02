#!/usr/bin/env bash
set -u
cd "C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/agent-a3f969792f618f57f/.calisma/t102"
b () {
  [ -f "$2" ] || { echo "$1 yok"; return; }
  vb=$(ffprobe -v error -select_streams v:0 -show_entries packet=size -of csv=p=0 "$2" | awk '{s+=$1} END {printf "%.1f", s*8/60.442/1000}')
  ab=$(ffprobe -v error -select_streams a:0 -show_entries packet=size -of csv=p=0 "$2" | awk '{s+=$1} END {printf "%.1f", s*8/60.442/1000}')
  printf "%-16s video %8s kbit/s   ses %7s kbit/s\n" "$1" "$vb" "$ab"
}
b auto            gui/parca-2_shrunk.mp4
b e1-preset4      ciktilar/e1-preset4.mp4
b e2-gop300       ciktilar/e2-gop300.mp4
b e3-olcek810     ciktilar/e3-olcek810.mp4
b uzman-biz-2975  ciktilar/uzman-biz-2975.mp4
b uzman-biz-2775  ciktilar/uzman-biz-2775.mp4
b uzman-biz3      ciktilar/uzman-biz3.mp4
b uzman-hb        ciktilar/uzman-hb.mp4
b uzman-hb2       ciktilar/uzman-hb2.mp4
