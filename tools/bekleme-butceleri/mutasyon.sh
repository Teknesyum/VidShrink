#!/bin/bash
set -u
W="$(cd "$(dirname "$0")/../.." && pwd)"
SRC=$W/src/VidShrink.Launcher/KurulumBekleyeni.cs
LOG=${1:-$W/.calisma/bekleme-butceleri/mutasyon.log}
mkdir -p "$(dirname "$LOG")"
: > "$LOG"
cp "$SRC" "$SRC.bak"

derle() {
  ad="${1:-derleme}"
  for proj in "$W/src/VidShrink.Launcher/VidShrink.Launcher.csproj" "$W/tests/VidShrink.Tests/VidShrink.Tests.csproj"; do
    if ! dotnet build -m:2 --no-incremental "$proj" > "$LOG.derleme" 2>&1; then
      echo "!!! $ad : DERLEME DUSTU ($(basename "$proj"))" >> "$LOG"
      grep -aE "error |Hata " "$LOG.derleme" | head -20 >> "$LOG"
      rm -f "$LOG.derleme"
      cp "$SRC.bak" "$SRC" 2>/dev/null
      rm -f "$SRC.bak"
      echo "DERLEME DUSTU" >> "$LOG"
      exit 1
    fi
  done
  rm -f "$LOG.derleme"
}

kos() {
  ad="$1"; fn="$2"; yeni="$3"
  cp "$SRC.bak" "$SRC"
  sed -i "s|internal static TimeSpan ${fn}(bool elle) => .*;|internal static TimeSpan ${fn}(bool elle) => ${yeni};|" "$SRC"
  echo "=== $ad : $(grep -n "TimeSpan ${fn}(bool elle)" "$SRC")" >> "$LOG"
  derle "$ad"
  dotnet test "$W/tests/VidShrink.Tests/VidShrink.Tests.csproj" --no-build \
    --filter "FullyQualifiedName~BaslaticiPanelsizTests" --logger "console;verbosity=detailed" 2>&1 \
    | grep -aE "Başarısız:|Atlanan:|Assert\.|Actual|Expected|hâlâ|^Başarılı!|^Başarısız!" >> "$LOG"
  echo "" >> "$LOG"
}

kos M1a YuvaBeklemesi "TimeSpan.Zero"
kos M1b YuvaBeklemesi "TimeSpan.FromSeconds(30)"
kos M2a IndirmeKilidi "TimeSpan.Zero"
kos M2b IndirmeKilidi "TimeSpan.FromSeconds(60)"
kos M3a KurulumKilidi "TimeSpan.Zero"
kos M3b KurulumKilidi "TimeSpan.FromSeconds(60)"

cp "$SRC.bak" "$SRC"
rm -f "$SRC.bak"
derle TEMIZ
echo "=== TEMIZ" >> "$LOG"
dotnet test "$W/tests/VidShrink.Tests/VidShrink.Tests.csproj" --no-build \
  --filter "FullyQualifiedName~BaslaticiPanelsizTests" 2>&1 \
  | grep -aE "^Başarılı!|^Başarısız!|Başarısız:|Atlanan:" >> "$LOG"
echo BITTI >> "$LOG"
