#!/usr/bin/env bash
set -euo pipefail
KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
python "$KOK/tools/sahne-butcesi/sayim-denetimi.py" \
  "$KOK/.calisma/T114" "$KOK/docs/olcumler/sahne-butcesi.md"
