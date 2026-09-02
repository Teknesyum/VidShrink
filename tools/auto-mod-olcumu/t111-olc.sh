#!/usr/bin/env bash
set -u
BASE="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$BASE/.calisma/t111"
REF="gui/parca-2.mkv"
TH=4
mkdir -p vmaf

M="model=version=vmaf_v0.6.1neg:n_threads=4"
SCALE="scale=w=1920:h=1080:flags=lanczos"
LOCK="settb=AVTB,setpts=N"

olc () {
  n="$1"; t="$2"; olcekle="$3"
  [ -f "$t" ] || { echo "YOK $n ($t)"; return; }
  if [ ! -f "vmaf/${n}-kilitsiz.json" ]; then
    if [ "$olcekle" = 1 ]; then g="[0:v]${SCALE}[t];[t][1:v]libvmaf=${M}:log_fmt=json:log_path=vmaf/${n}-kilitsiz.json"
    else g="[0:v][1:v]libvmaf=${M}:log_fmt=json:log_path=vmaf/${n}-kilitsiz.json"; fi
    ffmpeg -hide_banner -loglevel error -nostdin -threads $TH -i "$t" -i "$REF" -lavfi "$g" -f null - \
      && echo "olculdu $n kilitsiz" || echo "HATA $n kilitsiz"
  else echo "atlandi $n kilitsiz"; fi
  if [ ! -f "vmaf/${n}-kilitli.json" ]; then
    if [ "$olcekle" = 1 ]; then g="[0:v]${SCALE},${LOCK}[t];[1:v]${LOCK}[r];[t][r]libvmaf=${M}:log_fmt=json:log_path=vmaf/${n}-kilitli.json"
    else g="[0:v]${LOCK}[t];[1:v]${LOCK}[r];[t][r]libvmaf=${M}:log_fmt=json:log_path=vmaf/${n}-kilitli.json"; fi
    ffmpeg -hide_banner -loglevel error -nostdin -threads $TH -i "$t" -i "$REF" -lavfi "$g" -f null - \
      && echo "olculdu $n kilitli" || echo "HATA $n kilitli"
  else echo "atlandi $n kilitli"; fi
}

olc "auto"                "gui/parca-2_shrunk.mp4"      1
olc "auto-olceksiz"       "gui/parca-2_shrunk.mp4"      0
olc "e1-preset4"          "ciktilar/e1-preset4.mp4"     1
olc "e2-gop300"           "ciktilar/e2-gop300.mp4"      1
olc "e3-olcek810"         "ciktilar/e3-olcek810.mp4"    1
olc "uzman-biz3"          "ciktilar/uzman-biz3.mp4"     1
olc "uzman-hb"            "ciktilar/uzman-hb.mp4"       1
olc "uzman-hb2"           "ciktilar/uzman-hb2.mp4"      1
olc "y1-g300-izgara"      "ciktilar/y1-g300-izgara.mp4" 1
olc "y2-g300-hizali"      "ciktilar/y2-g300-hizali.mp4" 1
olc "y3-hizali-boyutesit" "ciktilar/y3-hizali-boyutesit.mp4" 1
olc "uzman-biz4"          "ciktilar/uzman-biz4.mp4"      1
olc "uzman-hb3"           "ciktilar/uzman-hb3.mp4"      1
olc "uzman-biz5"          "ciktilar/uzman-biz5.mp4"     1
echo "OLCUM BITTI $(date +%T)"
