#!/usr/bin/env bash
# T103 K5: ayni hedef boyutta taban ile dal planlarinin VMAF-NEG karsilastirmasi.
# Olcum ortak aletle yapilir: tools/VidShrink.Ab (origin/main). Ab planlayiciyi
# surec icinde cagirdigi icin, her calisma agacinda o agacin kodunu olcer.
#
# Kullanim: plan-kazanci.sh <taban-ref> <dal-ref> <is-kok> <hedefMb> <kaynak...>
set -euo pipefail

TABAN_REF="$1"; DAL_REF="$2"; KOK="$3"; HEDEF="$4"; shift 4
cd "$(dirname "$0")/../.."

git fetch origin main -q

for yan in taban dal; do
  ref="$TABAN_REF"; [ "$yan" = dal ] && ref="$DAL_REF"
  d="$KOK/ab-$yan"
  if [ ! -d "$d" ]; then
    git worktree add --detach "$d" "$ref" -q
    git -C "$d" checkout origin/main -- tools/VidShrink.Ab
  fi
  mkdir -p "$d/.calisma"
  (cd "$d" && dotnet build tools/VidShrink.Ab/VidShrink.Ab.csproj -c Release --no-incremental -v q --nologo)
  echo "$yan = $(git -C "$d" rev-parse --short HEAD)"
done

for src in "$@"; do
  ad="$(basename "${src%.*}")"
  for yan in taban dal; do
    d="$KOK/ab-$yan"
    echo "--- $ad $yan"
    (cd "$d" && dotnet tools/VidShrink.Ab/bin/Release/net8.0/VidShrink.Ab.dll kos \
      --kaynak "$src" --hedef-mb "$HEDEF" --yarismaci vidshrink \
      --cikti ".calisma/ab-out-$ad" --gunluk ".calisma/ab-log-$ad" \
      --json ".calisma/ab-$ad.json")
  done
done

python - "$KOK" "$@" <<'PY'
import json, io, os, sys
kok = sys.argv[1]
print(f"{'kaynak':<10}{'d-ort':>9}{'d-harm':>9}{'d-p10':>9}{'boyut':>9}  plan degisti mi")
for src in sys.argv[2:]:
    ad = os.path.splitext(os.path.basename(src))[0]
    try:
        t = json.load(io.open(f"{kok}/ab-taban/.calisma/ab-{ad}.json", encoding="utf-8"))["Measurements"][0]
        d = json.load(io.open(f"{kok}/ab-dal/.calisma/ab-{ad}.json", encoding="utf-8"))["Measurements"][0]
    except FileNotFoundError:
        print(f"{ad:<10} sonuc yok"); continue
    degisti = "evet" if t["Settings"] != d["Settings"] else "hayir"
    print(f"{ad:<10}{d['VmafNegMean']-t['VmafNegMean']:>+9.3f}{d['VmafNegHarmonic']-t['VmafNegHarmonic']:>+9.3f}"
          f"{d['VmafNegP10']-t['VmafNegP10']:>+9.3f}{(d['Bytes']-t['Bytes'])/t['Bytes']*100:>+8.2f}%  {degisti}")
PY
