#!/usr/bin/env bash
# K1 izgarasi: kaynak x -g. Her hucre icin teslim boyutu, VMAF ortalama/p10/en kotu
# kare, I-kare sayisi ve gerceklesen aralik. Hucreler SIRAYLA kosar.
set -euo pipefail
. "$(dirname "$0")/ortak.sh"

gerekli ffmpeg; gerekli ffprobe

KAYNAK="${1:?kullanim: izgara.sh <kaynak.mkv> <bitrate_k> <tavan_sn...>}"
BITRATE="${2:?bitrate gerekli, ornek 2600}"
shift 2
TAVANLAR=("$@")

ad="$(basename "$KAYNAK" .mkv)"
is="$CALISMA/izgara/$ad"
mkdir -p "$is"
fps="$(fps_oku "$KAYNAK")"
sure="$(sure_oku "$KAYNAK")"

echo "# kaynak=$ad fps=$fps sure=$sure bitrate=${BITRATE}k"
echo "# $SURUM"
echo "tavan_sn	g	boyut_bayt	vmaf_ort	vmaf_p10	vmaf_min	ikare	gercek_aralik_sn	kosum"

for tavan in "${TAVANLAR[@]}"; do
  g="$(awk -v f="$fps" -v t="$tavan" 'BEGIN{ printf "%d", (f*t)+0.5 }')"
  cikti="$is/g$g.mkv"
  onek="$is/g$g-pass"

  # Iki gecis, sabit bitrate: boyut esitleme yontemi budur. scenecut kapatilmiyor -
  # uretim de kapatmiyor; olculen sey tavanin kendisi.
  ffmpeg -v error -y -i "$KAYNAK" -c:v libx264 -b:v "${BITRATE}k" -g "$g" \
    -pass 1 -passlogfile "$onek" -an -f null -
  ffmpeg -v error -y -i "$KAYNAK" -c:v libx264 -b:v "${BITRATE}k" -g "$g" \
    -pass 2 -passlogfile "$onek" -an "$cikti"

  boyut="$(boyut_oku "$cikti")"

  # libvmaf log_path surucu harfindeki iki noktayi filtre ayraci saniyor; hucre
  # klasorune girip ciplak dosya adiyla yaziyoruz.
  ( cd "$is" && ffmpeg -v error -y -i "g$g.mkv" -i "$KAYNAK" \
      -lavfi "[0:v]${KARE_KILIDI}[t];[1:v]${KARE_KILIDI}[r];[t][r]libvmaf=model=${VMAF_MODEL}:log_fmt=json:log_path=g$g.vmaf.json" \
      -f null - )

  read -r ort p10 mink < <(python "$(dirname "$0")/oz.py" "$is/g$g.vmaf.json")

  mapfile -t kk < <(anahtar_kare_zamanlari "$cikti")
  ikare="${#kk[@]}"
  aralik="$(printf '%s\n' "${kk[@]}" | awk 'NR>1{d=$1-p; if(d>m) m=d} {p=$1} END{ printf "%.3f", m+0 }')"

  printf "%s\t%s\t%s\t%s\t%s\t%s\t%s\t%s\t1\n" \
    "$tavan" "$g" "$boyut" "$ort" "$p10" "$mink" "$ikare" "$aralik"

  rm -f "$onek"-*.log "$onek"-*.log.mbtree
done
