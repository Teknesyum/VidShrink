#!/usr/bin/env bash
# T103 K5: ayni hedef boyutta taban ile dal planlarinin VMAF-NEG karsilastirmasi.
# Kullanim: plan-kazanci.sh <taban-kok> <dal-kok> <cikti-kok> <hedefMb> <kaynak...>
set -euo pipefail

TABAN="$1"; DAL="$2"; OUT="$3"; HEDEF="$4"; shift 4

TABAN_DLL="$TABAN/tools/VidShrink.Bench/bin/Release/net8.0/VidShrink.Bench.dll"
DAL_DLL="$DAL/tools/VidShrink.Bench/bin/Release/net8.0/VidShrink.Bench.dll"
[ -f "$TABAN_DLL" ] || { echo "taban ikilisi yok: $TABAN_DLL" >&2; exit 1; }
[ -f "$DAL_DLL" ] || { echo "dal ikilisi yok: $DAL_DLL" >&2; exit 1; }

mkdir -p "$OUT"
echo "taban=$(git -C "$TABAN" rev-parse --short HEAD) dal=$(git -C "$DAL" rev-parse --short HEAD) hedef=${HEDEF}MB"

for src in "$@"; do
  ad="$(basename "${src%.*}")"
  for yan in taban dal; do
    dll="$TABAN_DLL"; [ "$yan" = dal ] && dll="$DAL_DLL"
    d="$OUT/$ad-$yan"
    mkdir -p "$d"
    echo "--- $ad $yan"
    dotnet "$dll" shrink "$src" "$HEDEF" --out "$d" --measured-quality \
      --results "$OUT/$ad-$yan.json"
  done
done
