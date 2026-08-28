#!/bin/sh
# CI'ın gördüğü hal: ffmpeg ve ffprobe PATH'te yok, donanım kodlayıcısı yok.
# Bu makinede yeşil olan süit CI'da kırmızı olabilir; mühürden önce bunu koş.
# Beklenen: atlanan sayısı CI'ınkiyle aynı çıkmalı.
cd "$(dirname "$0")/.."
PATH=$(printf '%s' "$PATH" | tr ':' '\n' | grep -v -i -e 'WinGet' -e 'ffmpeg' | paste -sd: -)
export PATH
if command -v ffmpeg >/dev/null 2>&1 || command -v ffprobe >/dev/null 2>&1; then
  echo "DURDU: ffmpeg ya da ffprobe hala PATH'te — bu kosum CI'in halini temsil etmez."
  command -v ffmpeg ffprobe
  exit 2
fi
dotnet test -c Release "$@"
