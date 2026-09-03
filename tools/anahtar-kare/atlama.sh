#!/usr/bin/env bash
# K2: her -g degeri icin atlama (seek) maliyeti. Kod yazilmadan, ffmpeg ile.
# Uzun -g dosyayi kucultur ama atlamayi pahalilastirir; bu puanda gorunmez.
#
# Iki olcu birden verilir:
#   cozulen_kare - hedefe varmak icin cozulmesi gereken kare sayisi. Deterministik,
#                  surec baslatma gurultusunden bagimsiz; asil buyukluk budur.
#   gecikme_ms   - duvar saati, TABANI CIKARILMIS. Cikarilan taban ayni dosyada
#                  -ss 0 kosumudur; boylece ffmpeg'in acilis maliyeti dusuyor ve
#                  geriye yalnizca atlamanin kendisi kaliyor.
set -euo pipefail
. "$(dirname "$0")/ortak.sh"

gerekli ffmpeg; gerekli ffprobe
DOSYA="${1:?kullanim: atlama.sh <kodlanmis.mkv> [tekrar]}"
TEKRAR="${2:-9}"
sure="$(sure_oku "$DOSYA")"

bir_kosum() {
  local ss="$1" bas son
  bas="$(date +%s%3N)"
  ffmpeg -v error -y -ss "$ss" -i "$DOSYA" -frames:v 1 -f null - >/dev/null 2>&1
  son="$(date +%s%3N)"
  echo "$((son - bas))"
}

# Taban: acilis + tek kare cozme, atlama yok. Uc kez, en kucugu alinir.
taban=999999
for _ in 1 2 3; do
  t="$(bir_kosum 0)"
  [ "$t" -lt "$taban" ] && taban="$t"
done
echo "# taban_ms=$taban  (ffmpeg acilis maliyeti, cikariliyor)"

# Anahtar kare zamanlari: hedefin hangi anahtar kareden sonra kac kare uzakta
# oldugunu cozmek icin.
mapfile -t kk < <(anahtar_kare_zamanlari "$DOSYA")
fps="$(fps_oku "$DOSYA")"

echo "hedef_sn	onceki_anahtar_sn	cozulen_kare	ham_ms	gecikme_ms"
for i in $(seq 1 "$TEKRAR"); do
  hedef="$(awk -v s="$sure" -v i="$i" -v n="$TEKRAR" 'BEGIN{ printf "%.3f", s*i/(n+1) }')"
  onceki="$(printf '%s\n' "${kk[@]}" | awk -v h="$hedef" '$1<=h{p=$1} END{ printf "%.3f", p+0 }')"
  cozulen="$(awk -v h="$hedef" -v p="$onceki" -v f="$fps" 'BEGIN{ printf "%d", (h-p)*f+0.5 }')"
  ham="$(bir_kosum "$hedef")"
  net="$((ham - taban))"
  [ "$net" -lt 0 ] && net=0
  printf "%s\t%s\t%s\t%s\t%s\n" "$hedef" "$onceki" "$cozulen" "$ham" "$net"
done
