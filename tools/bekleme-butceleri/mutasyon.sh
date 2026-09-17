#!/bin/bash
set -u
W="$(cd "$(dirname "$0")/../.." && pwd)"
SRC=$W/src/VidShrink.Launcher/KurulumBekleyeni.cs
LOG=${1:-$W/.calisma/bekleme-butceleri/mutasyon.log}
mkdir -p "$(dirname "$LOG")"
: > "$LOG"
cp "$SRC" "$SRC.bak"

derle() {
  dotnet build -m:2 --no-incremental "$W/src/VidShrink.Launcher/VidShrink.Launcher.csproj" > /dev/null 2>&1
  dotnet build -m:2 --no-incremental "$W/tests/VidShrink.Tests/VidShrink.Tests.csproj" > /dev/null 2>&1
}

kos() {
  ad="$1"; fn="$2"; yeni="$3"
  cp "$SRC.bak" "$SRC"
  sed -i "s|internal static TimeSpan ${fn}(bool elle) => .*;|internal static TimeSpan ${fn}(bool elle) => ${yeni};|" "$SRC"
  echo "=== $ad : $(grep -n "TimeSpan ${fn}(bool elle)" "$SRC")" >> "$LOG"
  derle
  dotnet test "$W/tests/VidShrink.Tests/VidShrink.Tests.csproj" --no-build \
    --filter "FullyQualifiedName~BaslaticiPanelsizTests" --logger "console;verbosity=detailed" 2>&1 \
    | grep -aE "Başarısız |Assert\.|Actual|Expected|hâlâ|^Başarılı!|^Başarısız!" >> "$LOG"
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
derle
echo "=== TEMIZ" >> "$LOG"
dotnet test "$W/tests/VidShrink.Tests/VidShrink.Tests.csproj" --no-build \
  --filter "FullyQualifiedName~BaslaticiPanelsizTests" 2>&1 | grep -aE "^Başarılı!|^Başarısız!" >> "$LOG"
echo BITTI >> "$LOG"
