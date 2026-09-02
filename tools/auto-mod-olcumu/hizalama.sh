#!/usr/bin/env bash
set -u
cd "C:/Users/Administrator/Desktop/Projeler/Vidshrink/.claude/worktrees/agent-a3f969792f618f57f/.calisma/t102"
SRC="gui/parca-2.mkv"
mkdir -p ciktilar log vmaf

PSY="tune=0:enable-variance-boost=1:variance-boost-strength=2:lp=4"
COL="-color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc"
TH=4

enc () {
  name="$1"; fkf="$2"
  out="ciktilar/${name}.mp4"
  if [ -f "$out" ]; then echo "atlandi $name"; return; fi
  fkfarg=()
  if [ -n "$fkf" ]; then fkfarg=(-force_key_frames "$fkf"); fi
  echo "=== $name fkf=${fkf:-yok} $(date +%T)"
  ffmpeg -hide_banner -loglevel error -y -nostdin -threads $TH -i "$SRC" \
    -c:v libsvtav1 -preset 6 -b:v 2026k -pass 1 -passlogfile "log/${name}" \
    -g 300 "${fkfarg[@]}" -pix_fmt p010le -svtav1-params "$PSY" $COL -an -f null NUL
  ffmpeg -hide_banner -loglevel error -y -nostdin -threads $TH -i "$SRC" \
    -c:v libsvtav1 -preset 6 -b:v 2026k -pass 2 -passlogfile "log/${name}" \
    -g 300 "${fkfarg[@]}" -pix_fmt p010le -svtav1-params "$PSY" $COL \
    -c:a aac -b:a 128k -movflags +faststart "$out"
  echo "bitti $name $(stat -c %s "$out") $(date +%T)"
}

olc () {
  n="$1"
  [ -f "vmaf/${n}.json" ] && { echo "atlandi vmaf $n"; return; }
  ffmpeg -hide_banner -loglevel error -nostdin -threads $TH -i "ciktilar/${n}.mp4" -i "$SRC" \
    -lavfi "[0:v]scale=w=1920:h=1080:flags=lanczos[t];[t][1:v]libvmaf=model=version=vmaf_v0.6.1neg:n_threads=4:log_fmt=json:log_path=vmaf/${n}.json" \
    -f null - && echo "olculdu $n" || echo "HATA $n"
}

SEL="select='gt(scene,0.2)',metadata=print:file=-"
echo "=== sahne kesmeleri (esik 0,2)"
ffmpeg -hide_banner -nostdin -threads $TH -i "$SRC" -vf "$SEL" -an -f null - 2>/dev/null | grep -E "pts_time|scene_score"

enc "y1-g300-izgara" ""
enc "y2-g300-hizali" "28.353,56.870"
olc "y1-g300-izgara"
olc "y2-g300-hizali"
echo "HIZALAMA BITTI $(date +%T)"

enc2 () {
  name="$1"; fkf="$2"; br="$3"
  out="ciktilar/${name}.mp4"
  if [ -f "$out" ]; then echo "atlandi $name"; return; fi
  echo "=== $name fkf=$fkf br=${br}k $(date +%T)"
  for p in 1 2; do
    if [ "$p" = 1 ]; then extra=(-an -f null NUL); else extra=(-c:a aac -b:a 128k -movflags +faststart "$out"); fi
    ffmpeg -hide_banner -loglevel error -y -nostdin -threads $TH -i "$SRC" \
      -c:v libsvtav1 -preset 6 -b:v "${br}k" -pass $p -passlogfile "log/${name}" \
      -g 300 -force_key_frames "$fkf" -pix_fmt p010le -svtav1-params "$PSY" $COL "${extra[@]}"
  done
  echo "bitti $name $(stat -c %s "$out") $(date +%T)"
}

enc2 "y3-hizali-boyutesit" "28.353,56.870" 2144
olc "y3-hizali-boyutesit"
echo "Y3 BITTI $(date +%T)"
