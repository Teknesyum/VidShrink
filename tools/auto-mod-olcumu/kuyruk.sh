#!/usr/bin/env bash
set -u
cd "C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/agent-a3f969792f618f57f/.calisma/t102"
while [ ! -f ciktilar/uzman-biz.mp4 ]; do sleep 20; done
sleep 10
echo "=== hb2 basliyor $(date +%T)"
HandBrakeCLI -i gui/parca-2.mkv -o ciktilar/uzman-hb2.mp4 \
  -e x265_10bit --encoder-preset slow \
  -b 1900 --multi-pass --turbo \
  -E ca_aac -B 128 --mixdown stereo \
  -w 1920 -l 1080 --crop-mode none -r 60 --cfr \
  -f av_mp4 -O 2>&1 | tail -4
echo "hb2 bitti $(stat -c %s ciktilar/uzman-hb2.mp4) $(date +%T)"
olc () {
  n="$1"; t="$2"
  if [ ! -f "$t" ]; then echo "YOK $n"; return; fi
  if [ -f "vmaf/${n}.json" ]; then echo "atlandi $n"; return; fi
  ffmpeg -hide_banner -loglevel error -nostdin -i "$t" -i gui/parca-2.mkv \
    -lavfi "[0:v]scale=w=1920:h=1080:flags=lanczos[t];[t][1:v]libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path=vmaf/${n}.json" \
    -f null - && echo "olculdu $n" || echo "HATA $n"
}
while [ ! -f vmaf/uzman-hb.json ]; do sleep 20; done
olc "uzman-biz"  "ciktilar/uzman-biz.mp4"
olc "uzman-hb2"  "ciktilar/uzman-hb2.mp4"
echo "KUYRUK BITTI"
