#!/usr/bin/env bash
# Kaynaklari izgaraya hazirlar: ses akisini atar, hizalamayi dogrular.
# Ses asimetrisi bu depoda bir A/B'yi haksiz yapti (ab-duzenegi.md:169-180).
set -euo pipefail
. "$(dirname "$0")/ortak.sh"

gerekli ffmpeg; gerekli ffprobe; havuz_dogrula
mkdir -p "$CALISMA/kaynak"

hazirla_bir() {
  local girdi="$1" cikti="$2"
  if [ ! -f "$girdi" ]; then echo "kaynak yok: $girdi" >&2; return 1; fi
  # -c:v copy: goruntu akisi yeniden kodlanmiyor, yalniz ses dusuyor.
  ffmpeg -v error -y -i "$girdi" -an -c:v copy "$cikti"
  local a b
  a="$(ffprobe -v error -select_streams v:0 -count_frames \
        -show_entries stream=nb_read_frames -of default=nw=1:nk=1 "$girdi")"
  b="$(ffprobe -v error -select_streams v:0 -count_frames \
        -show_entries stream=nb_read_frames -of default=nw=1:nk=1 "$cikti")"
  if [ "$a" != "$b" ]; then
    echo "HIZALAMA KAPISI KAPALI: $girdi $a kare, $cikti $b kare" >&2; return 1
  fi
  if ses_var_mi "$cikti"; then echo "ses hala var: $cikti" >&2; return 1; fi
  echo "hazir: $(basename "$cikti")  kare=$b  ses=yok"
}

for n in 1 2 3; do
  hazirla_bir "$HAVUZ/parca-$n.mkv" "$CALISMA/kaynak/parca-$n-video.mkv"
done

# Tam kaynagin sessiz surumu havuzda zaten var; kopyalanmiyor, oldugu yerden okunuyor.
tam="$HAVUZ/kaynak-1080p60-hdr-17dk-yalniz-video.mkv"
if [ -f "$tam" ]; then
  ses_var_mi "$tam" && { echo "tam kaynakta ses var: $tam" >&2; exit 1; }
  echo "hazir: $(basename "$tam")  (havuzdan, oldugu gibi)"
else
  echo "tam kaynak yok: $tam" >&2; exit 1
fi
