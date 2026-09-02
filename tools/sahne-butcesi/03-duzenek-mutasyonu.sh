#!/usr/bin/env bash
set -uo pipefail
KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
IS="$KOK/.calisma/T114"
ARAC="$IS/arac-mutasyon"
KAYNAK="$KOK/tools/sahne-butcesi/Butce.cs"
YEDEK="$IS/Butce.cs.yedek"
CIKTI="$IS/duzenek-mutasyon.csv"

cp "$KAYNAK" "$YEDEK"
geri() { cp "$YEDEK" "$KAYNAK"; }
trap geri EXIT

kos() {
  dotnet build "$KOK/tools/sahne-butcesi/SahneButcesi.csproj" -c Release --no-incremental -o "$ARAC" --nologo >/dev/null 2>&1 || { echo "derleme-hatasi"; return; }
  if VIDSHRINK_KOK="$KOK" dotnet "$ARAC/SahneButcesi.dll" dogrula maks p1-karisik >/dev/null 2>&1; then echo "gecti"; else echo "kirildi"; fi
}

echo "mutasyon;ne_degisti;dogrulama" > "$CIKTI"

geri
echo "M0;temiz agac;$(kos)" >> "$CIKTI"

geri
sed -i 's|result\[i\] = Math.Clamp(raw\[i\] / mean, ZoneFloor, ZoneCeiling);|result[i] = Math.Clamp(raw[i], ZoneFloor, ZoneCeiling);|' "$KAYNAK"
echo "M1;normalizasyon kaldirildi (ortalama 1,0'a cekilmiyor);$(kos)" >> "$CIKTI"

geri
sed -i 's|raw\[i\] = c > 0 ? Math.Pow(c, gamma) : 1.0;|raw[i] = c > 0 ? Math.Pow(c, -gamma) : 1.0;|' "$KAYNAK"
echo "M2;us isareti ters (karmasik sahneye az bit);$(kos)" >> "$CIKTI"

geri
sed -i 's|var start = Math.Max(lastEnd + 1, (int)Math.Round(scene.Start \* fps));|var start = (int)Math.Round(scene.Start * fps) - 5;|' "$KAYNAK"
echo "M3;zone araliklari cakisabilir hale getirildi;$(kos)" >> "$CIKTI"

geri
cat "$CIKTI"
