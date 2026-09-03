#!/usr/bin/env bash
set -uo pipefail
KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
HEDEF="$KOK/src/VidShrink.Core/SceneBudget.cs"
SUZGEC="SceneBudgetTests"
CIKTI="$KOK/.calisma/T114/mutasyon.csv"

if [ ! -f "$HEDEF" ]; then
  echo "$HEDEF yok — dagitim kurali koda girmedi, mutasyon kaniti uygulanamaz." >&2
  exit 3
fi

mkdir -p "$(dirname "$CIKTI")"
echo "parca;yon;eski;yeni;olcu_kirildi" > "$CIKTI"
YEDEK="$(mktemp)"
cp "$HEDEF" "$YEDEK"
geri() { cp "$YEDEK" "$HEDEF"; }
trap geri EXIT

kos() {
  dotnet build "$KOK/VidShrink.sln" -c Release --no-incremental --nologo >/dev/null 2>&1 || return 2
  dotnet test "$KOK/VidShrink.sln" -c Release --no-build --nologo --filter "$SUZGEC" >/dev/null 2>&1
}

dene() {
  local parca="$1" yon="$2" eski="$3" yeni="$4"
  geri
  python - "$HEDEF" "$eski" "$yeni" <<'PY'
import io,sys
p,eski,yeni=sys.argv[1],sys.argv[2],sys.argv[3]
s=io.open(p,encoding='utf-8').read()
if eski not in s:
    sys.stderr.write("bulunamadi: %s\n" % eski); sys.exit(4)
io.open(p,'w',encoding='utf-8').write(s.replace(eski,yeni,1))
PY
  if [ $? -ne 0 ]; then echo "$parca;$yon;$eski;$yeni;YAMA-TUTMADI" >> "$CIKTI"; return; fi
  if kos; then echo "$parca;$yon;$eski;$yeni;hayir" >> "$CIKTI"
  else echo "$parca;$yon;$eski;$yeni;evet" >> "$CIKTI"; fi
}

# Once temiz agacta olculer gecmeli; gecmiyorsa mutasyon sonucu anlamsiz.
geri
if ! kos; then echo "temiz agacta $SUZGEC dusuyor — once onu duzelt." >&2; exit 4; fi

dene "gamma"        "buyut" "1.0 - qcomp" "1.3 - qcomp"
dene "gamma"        "kucult" "1.0 - qcomp" "0.7 - qcomp"
dene "qcomp"        "buyut" "DefaultQcomp = 0.60" "DefaultQcomp = 0.75"
dene "qcomp"        "kucult" "DefaultQcomp = 0.60" "DefaultQcomp = 0.45"
dene "kiskac-ust"   "buyut" "ZoneCeiling = 4.0" "ZoneCeiling = 8.0"
dene "kiskac-ust"   "kucult" "ZoneCeiling = 4.0" "ZoneCeiling = 2.0"
dene "kiskac-alt"   "buyut" "ZoneFloor = 0.25" "ZoneFloor = 0.50"
dene "kiskac-alt"   "kucult" "ZoneFloor = 0.25" "ZoneFloor = 0.10"

geri
column -s';' -t < "$CIKTI" 2>/dev/null || cat "$CIKTI"
