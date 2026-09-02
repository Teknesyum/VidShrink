#!/usr/bin/env bash
# K1 izgarasi: kaynak x -g. Bitrate kaynak basina sabit, iki gecis; boyut
# esitligi bu yolla saglanir ve sonradan hucre basina dogrulanir.
set -u
. "$(dirname "$0")/ortak.sh"
OZ="$(dirname "$0")/oz.py"
ENC=${ENC:-libx264}
ETI=${ETI:-x264}
CSV="$ROOT/.calisma/t133/izgara-$ETI.csv"
: > "$CSV"
echo "kaynak,g_sn,g_kare,kmin_kare,bitrate_k,boyut_bayt,ikare,gerc_aralik_sn,kare,mean,p10,worst" >> "$CSV"
while read -r s br; do
  [ -z "$s" ] && continue
  case "$s" in \#*) continue;; esac
  src="$KAY/$s.mkv"; fps=$(fps_of "$src")
  for sn in 2 5 10 15 20; do
    g=$(python -c "print(round($fps*$sn))"); kmin=$(python -c "print(round($fps*1))")
    ad="$ETI-$s-g${sn}"
    kodla "$ad" "$src" "$g" "$kmin" "$br" "$ENC" >/dev/null || { echo "$s,$sn,,,,KODLAMA-HATASI" >> "$CSV"; continue; }
    olc "$ad" "$src" >/dev/null || { echo "$s,$sn,,,,VMAF-HATASI" >> "$CSV"; continue; }
    read -r ik ar <<EOF2
$(anahtar_kare "$ad")
EOF2
    echo "$s,$sn,$g,$kmin,$br,$(stat -c %s "$ISI/$ad.mp4"),$ik,$ar,$(python "$OZ" "$VMD/$ad.json" | tr ' ' ',')" >> "$CSV"
    echo "bitti $ad"
  done
done < "${BR_FILE:-$ROOT/.calisma/t133/bitrate.txt}"
echo "IZGARA-BITTI $ETI"
