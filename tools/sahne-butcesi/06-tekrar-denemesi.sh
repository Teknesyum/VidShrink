#!/usr/bin/env bash
set -u
KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
IS="$KOK/.calisma/T114"
DENEME="$IS/tekrar-deneme"
ARAC="$IS/arac-tekrar-deneme"
CIKTI="$IS/tekrar-denemesi.csv"

dotnet build "$KOK/tools/sahne-butcesi/SahneButcesi.csproj" -c Release --no-incremental -o "$ARAC" >/dev/null || exit 1

mkdir -p "$DENEME"
BEKLENEN=$(python "$KOK/tools/sahne-butcesi/tekrar-fikstur.py" "$DENEME") || exit 1

echo "senaryo;kosum1;kosum2;beklenen;cikan;sonuc" > "$CIKTI"
hata=0
while IFS=';' read -r ad beklenen; do
  ad="${ad%$''}"; beklenen="${beklenen%$''}"
  [ -z "$ad" ] && continue
  d="$DENEME/$ad"
  cp "$IS"/k1-*.json "$IS"/k4b-*.csv "$IS"/harita-*.json "$IS"/k4-izgara.csv "$d/.calisma/T114/" 2>/dev/null
  VIDSHRINK_KOK="$d" dotnet "$ARAC/SahneButcesi.dll" rapor >/dev/null 2>&1
  sayfa="$d/docs/olcumler/sahne-butcesi.md"
  satir=$(grep -m1 "zones\`in kazandigi" "$sayfa" 2>/dev/null || true)
  m1=$(awk -F'|' '/^\| yedek \| `p1-karisik` \| (ayni|farkli)/{gsub(/ /,"",$6);print $6;exit}' "$sayfa")
  m2=$(awk -F'|' '/^\| yedek \| `p1-karisik` \| (ayni|farkli)/{gsub(/ /,"",$7);print $7;exit}' "$sayfa")
  if echo "$satir" | grep -q "altinda"; then cikan="altinda"
  elif echo "$satir" | grep -q "ustunde"; then cikan="ustunde"
  else cikan="okunamadi"; fi
  if [ "$cikan" = "$beklenen" ]; then sonuc="gecti"; else sonuc="KALDI"; hata=1; fi
  echo "$ad;$m1;$m2;$beklenen;$cikan;$sonuc" >> "$CIKTI"
done <<< "$BEKLENEN"

cat "$CIKTI"
exit $hata
