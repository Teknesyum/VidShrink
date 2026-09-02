#!/usr/bin/env bash
set -u
cd "C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/agent-a3f969792f618f57f/.calisma/t102"
k () {
  [ -f "$2" ] || { echo "$1 yok"; return; }
  ffprobe -v error -select_streams v:0 -skip_frame nokey -show_entries frame=pts_time -of csv=p=0 "$2" \
    | awk -v ad="$1" 'NF{n++; t=$1+0; if(n>1){d=t-p; s+=d; if(d<mn||mn==0)mn=d; if(d>mx)mx=d} p=t}
        END {printf "%-16s anahtar kare %4d   ortalama aralik %6.2f s   en kisa %5.2f   en uzun %6.2f\n", ad, n, (n>1? s/(n-1):0), mn, mx}'
}
k auto           gui/parca-2_shrunk.mp4
k e1-preset4     ciktilar/e1-preset4.mp4
k e2-gop300      ciktilar/e2-gop300.mp4
k e3-olcek810    ciktilar/e3-olcek810.mp4
k uzman-biz-2775 ciktilar/uzman-biz-2775.mp4
k uzman-biz3     ciktilar/uzman-biz3.mp4
k uzman-hb       ciktilar/uzman-hb.mp4
k uzman-hb2      ciktilar/uzman-hb2.mp4
