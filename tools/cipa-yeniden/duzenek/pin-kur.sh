#!/usr/bin/env bash
set -eu
cd "$(dirname "$0")" || exit 1
P=pin
mkdir -p "$P"
ffmpeg -v error -y -f lavfi -i testsrc2=size=320x240:rate=30:duration=3 -c:v libx264 -crf 12 -threads 2 "$P/src.mp4"
ffmpeg -v error -y -i "$P/src.mp4" -c:v libx264 -crf 16 -threads 2 "$P/same.mp4"
ffmpeg -v error -y -f lavfi -i anullsrc=r=48000:cl=stereo -t 3 -c:a aac "$P/sessiz.m4a"
for k in 0 0.010 0.020 0.040 0.050; do
  ffmpeg -v error -y -itsoffset "$k" -i "$P/src.mp4" -i "$P/sessiz.m4a" -map 0:v -map 1:a -c copy "$P/k$k.mkv"
done
# belgedeki tarif: ilk kare atilip setpts=N/FR/TB
ffmpeg -v error -y -i "$P/src.mp4" -vf "select=gte(n\,1),setpts=N/FR/TB" -c:v libx264 -crf 16 -threads 2 "$P/kaymis-ref-select.mkv"
# iki kare atilmis hali — (1,2] bacaginin cipasi
ffmpeg -v error -y -i "$P/src.mp4" -vf "select=gte(n\,2),setpts=N/FR/TB" -c:v libx264 -crf 16 -threads 2 "$P/kaymis-ref-2kare.mkv"
# denetimin tarif ettigi hal: -itsoffset kopyasi (ses akisiyla, h264 kopya)
ffmpeg -v error -y -itsoffset 0.020 -i "$P/src.mp4" -i "$P/sessiz.m4a" -map 0:v -map 1:a -c copy "$P/kaymis-ref-itsoffset.mkv"
echo "pin hazir"
ls -la "$P"
