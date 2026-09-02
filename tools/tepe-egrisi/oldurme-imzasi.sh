#!/usr/bin/env bash
# Dis oldurmenin imzasini olcer: kendi baslattigim ffmpeg'i kendi PID'iyle
# hedefli oldurur, cikis kodunu ve stderr kuyrugunu kaydeder. Toplu oldurme yok.
set -u
KOK="$(cd "$(dirname "$0")/../.." && pwd)"
CIK="$KOK/.calisma/t108/oldurme"
KAYNAK="$KOK/.calisma/t108/kaynak/hareketli-20sn.mkv"
mkdir -p "$CIK"
echo "no;pid;cikis" > "$CIK/sonuc.csv"
for i in 1 2 3; do
  ffmpeg -hide_banner -y -hwaccel auto -i "$KAYNAK" \
    -c:v av1_nvenc -preset p5 -b:v 4000k -pix_fmt p010le -an "$CIK/k$i.mp4" \
    >"$CIK/k$i.err" 2>&1 &
  pid=$!
  sleep 6
  wpid=$(powershell.exe -NoProfile -Command "(Get-CimInstance Win32_Process -Filter \"Name='ffmpeg.exe'\" | Where-Object { \$_.CommandLine -like '*t108/oldurme/k$i.mp4*' }).ProcessId" | tr -d '\r\n ')
  echo "kol $i: bash pid $pid, windows pid '$wpid'"
  if [ -n "$wpid" ]; then taskkill //F //PID "$wpid" > "$CIK/k$i.taskkill" 2>&1; fi
  wait $pid; k=$?
  echo "$i;$wpid;$k" >> "$CIK/sonuc.csv"
done
echo "oldurme bitti"
