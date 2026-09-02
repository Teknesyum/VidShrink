#!/usr/bin/env bash
set -euo pipefail
KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ARAC="$KOK/.calisma/T114/arac"

dotnet build "$KOK/tools/sahne-butcesi/SahneButcesi.csproj" -c Release --no-incremental -o "$ARAC" --nologo

kos() { dotnet "$ARAC/SahneButcesi.dll" "$@"; }

bash "$KOK/tools/sahne-butcesi/00-pencereleri-kes.sh"

KOLLAR="maks uyumlu yedek"
PENCERELER="p1-karisik p2-durgun p3-hareketli"

for w in $PENCERELER; do kos harita maks "$w"; done
kos k4 maks p1-karisik

for kol in $KOLLAR; do for w in $PENCERELER; do kos plan "$kol" "$w"; done; done
for kol in $KOLLAR; do for w in $PENCERELER; do kos k1   "$kol" "$w"; done; done
for kol in $KOLLAR; do for w in $PENCERELER; do kos k5   "$kol" "$w"; done; done
for kol in $KOLLAR; do for w in $PENCERELER; do kos k7   "$kol" "$w"; done; done

kos rapor
