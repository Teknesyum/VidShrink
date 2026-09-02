#!/usr/bin/env bash
# Butun olcumu bastan kosar. Ara ciktilar .calisma/T114 altinda ve varsa atlanir;
# yeniden olcmek icin o dosyalari silin.
set -euo pipefail
KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ARAC="$KOK/.calisma/T114/arac"

# Mutasyon ve bayat ikili tuzagi: artimli derleme degisikligi tasimiyor.
dotnet build "$KOK/tools/sahne-butcesi/SahneButcesi.csproj" -c Release --no-incremental -o "$ARAC" --nologo

kos() { dotnet "$ARAC/SahneButcesi.dll" "$@"; }

bash "$KOK/tools/sahne-butcesi/00-pencereleri-kes.sh"

for w in p1-karisik p2-durgun p3-hareketli; do kos harita maks "$w"; done
kos k4 maks p1-karisik

for kol in maks uyumlu; do
  for w in p2-durgun p1-karisik p3-hareketli; do kos k1 "$kol" "$w"; done
done

for kol in maks uyumlu; do
  for w in p1-karisik p2-durgun p3-hareketli; do kos k5 "$kol" "$w"; done
  for w in p1-karisik p2-durgun p3-hareketli; do kos k7 "$kol" "$w"; done
done

kos rapor
