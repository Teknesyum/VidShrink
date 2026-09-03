#!/usr/bin/env bash
# T133 anahtar kare tavani olcumu - ortak tanimlar.
set -euo pipefail

HAVUZ="${T133_HAVUZ:-}"
CALISMA="${T133_CALISMA:-$PWD/.calisma/t133}"

# Kiyaslar surum sinirini gecmez: butun hucreler ayni ffmpeg ile kosar.
SURUM="$(ffmpeg -hide_banner -version | head -1)"

# Uretimin kare kilidi (MeasureFilterGraph.FrameLock ile birebir).
KARE_KILIDI="settb=AVTB,setpts=N"
VMAF_MODEL="version=vmaf_v0.6.1neg"

gerekli() {
  command -v "$1" >/dev/null 2>&1 || { echo "eksik arac: $1" >&2; exit 1; }
}

havuz_dogrula() {
  [ -n "$HAVUZ" ] || { echo "T133_HAVUZ tanimli degil" >&2; exit 1; }
  [ -d "$HAVUZ" ] || { echo "kaynak havuzu yok: $HAVUZ" >&2; exit 1; }
}

fps_oku() {
  ffprobe -v error -select_streams v:0 -show_entries stream=r_frame_rate \
    -of default=nw=1:nk=1 "$1" | awk -F/ '{ printf "%.6f", (NF==2 ? $1/$2 : $1) }'
}

sure_oku() {
  ffprobe -v error -show_entries format=duration -of default=nw=1:nk=1 "$1"
}

boyut_oku() {
  ffprobe -v error -show_entries format=size -of default=nw=1:nk=1 "$1"
}

ses_var_mi() {
  local n
  n="$(ffprobe -v error -select_streams a -show_entries stream=index -of csv=p=0 "$1" | wc -l)"
  [ "$n" -gt 0 ]
}

# Gercek anahtar kare yerlesimi - tavan bagladi mi, varsayimla degil sayimla.
anahtar_kare_zamanlari() {
  ffprobe -v error -select_streams v:0 -skip_frame nokey \
    -show_entries frame=pts_time -of csv=p=0 "$1" | tr -d ','
}
