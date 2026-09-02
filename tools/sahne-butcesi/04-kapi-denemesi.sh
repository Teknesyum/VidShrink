#!/usr/bin/env bash
set -euo pipefail

KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
IS="$KOK/.calisma/T114"
DENEME="$IS/kapi-denemesi"
ARAC="$DENEME/arac"
CIKTI="$IS/kapi-denemesi.csv"

rm -rf "$DENEME"
mkdir -p "$ARAC"
dotnet build "$KOK/tools/sahne-butcesi/SahneButcesi.csproj" -c Release --no-incremental -o "$ARAC" >/dev/null

echo "senaryo;ne_degisti;beklenen;cikan;sonuc" > "$CIKTI"

kos() {
  local ad="$1" ne="$2" bekle="$3"
  local kok="$DENEME/$ad"
  mkdir -p "$kok/.calisma/T114" "$kok/docs/olcumler"
  python "$KOK/tools/sahne-butcesi/kapi-fikstur.py" "$ad" "$kok/.calisma/T114"
  VIDSHRINK_KOK="$kok" dotnet "$ARAC/SahneButcesi.dll" rapor >/dev/null
  local satir
  satir=$(grep -m1 -E '^\*\*(Dagitim koda (girer|girmez)|Karar verilemedi)' "$kok/docs/olcumler/sahne-butcesi.md" || echo "SATIR YOK")
  local cikan="belirsiz"
  case "$satir" in
    *"koda girer"*)        cikan="girer" ;;
    *"koda girmez"*)       cikan="girmez" ;;
    *"Karar verilemedi"*)  cikan="karar-yok" ;;
  esac
  local sonuc="KIRIK"
  [ "$cikan" = "$bekle" ] && sonuc="gecti"
  echo "$ad;$ne;$bekle;$cikan;$sonuc" >> "$CIKTI"
  echo "$ad: bekle=$bekle cikan=$cikan -> $sonuc"
}

kos temiz        "dort kapi da saglaniyor"                         girer
kos p10-kaybi    "bir pencerede p10 kaybi 0,50 (esik 0,30)"        girmez
kos band-asan    "bir kosum hedef bandin ustunde"                  girmez
kos k7-bedeli    "bozuk harita kaybi kendi hucresinin kazancini asiyor" girmez
kos olcum-yok    "k5 ve k7 dosyasi yok"                            karar-yok

echo
cat "$CIKTI"
if grep -q "KIRIK" "$CIKTI"; then
  echo "KAPI DENEMESI DUSTU" >&2
  exit 1
fi
echo "kapi denemesi: hepsi gecti"
