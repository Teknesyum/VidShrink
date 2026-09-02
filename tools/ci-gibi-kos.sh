#!/bin/sh
cd "$(dirname "$0")/.." || exit 3

WORKFLOW=".github/workflows/ci.yml"
GATE_LINE=$(grep -E 'kosum-kapisi\.ps1' "$WORKFLOW" 2>/dev/null | tail -1)
MIN_TOTAL=$(printf '%s' "$GATE_LINE" | grep -oE -- '-MinimumTotal [0-9]+' | grep -oE '[0-9]+')
MAX_SKIPPED=$(printf '%s' "$GATE_LINE" | grep -oE -- '-MaximumSkipped [0-9]+' | grep -oE '[0-9]+')

selftest() {
  echo "KENDI-SINAMA: $WORKFLOW'daki kapi ile bu ortam karsilastiriliyor."
  ok=1

  if command -v ffmpeg >/dev/null 2>&1 && command -v ffprobe >/dev/null 2>&1; then
    echo "  ffmpeg/ffprobe PATH'te bulundu - CI'in T115 sonrasi hali ile ayni."
  else
    echo "  DUSTU: ffmpeg/ffprobe PATH'te yok. CI kurulumdan sonra gorur, bu ortam gormuyor."
    ok=0
  fi

  if [ -z "$MIN_TOTAL" ]; then
    echo "  DUSTU: -MinimumTotal $WORKFLOW icinde bulunamadi."
    ok=0
  else
    echo "  -MinimumTotal=$MIN_TOTAL (kaynak: $WORKFLOW)"
  fi

  if [ -z "$MAX_SKIPPED" ]; then
    echo "  UYARI: -MaximumSkipped $WORKFLOW icinde bulunamadi, sinirsiz kabul edilecek."
  else
    echo "  -MaximumSkipped=$MAX_SKIPPED (kaynak: $WORKFLOW)"
  fi

  if command -v dotnet >/dev/null 2>&1; then
    echo "  dotnet PATH'te bulundu."
  else
    echo "  DUSTU: dotnet PATH'te yok."
    ok=0
  fi

  if command -v powershell >/dev/null 2>&1 || command -v pwsh >/dev/null 2>&1; then
    echo "  powershell/pwsh PATH'te bulundu."
  else
    echo "  DUSTU: ne powershell ne pwsh PATH'te."
    ok=0
  fi

  if [ "$ok" = 1 ]; then
    echo "KENDI-SINAMA GECTI"
    exit 0
  fi
  echo "KENDI-SINAMA DUSTU"
  exit 1
}

case "$1" in
  --self-test)
    selftest
    ;;
  "")
    ;;
  *)
    echo "DURDU: bilinmeyen arguman: $1" >&2
    exit 3
    ;;
esac

if [ -z "$MIN_TOTAL" ]; then
  echo "DURDU: $WORKFLOW icinden -MinimumTotal okunamadi, kapi parametresi belirsiz." >&2
  exit 3
fi

SKIPPED_NOTE=""
GATE_ARGS="-MinimumTotal $MIN_TOTAL"
if [ -n "$MAX_SKIPPED" ]; then
  GATE_ARGS="$GATE_ARGS -MaximumSkipped $MAX_SKIPPED"
  SKIPPED_NOTE=" -MaximumSkipped $MAX_SKIPPED"
fi

echo "CI TEMSILI: ffmpeg PATH'te birakiliyor, kapi $WORKFLOW'dan okunuyor (-MinimumTotal $MIN_TOTAL$SKIPPED_NOTE)."

dotnet build VidShrink.sln -c Release -warnaserror || exit $?

if command -v pwsh >/dev/null 2>&1; then
  PS=pwsh
else
  PS=powershell
fi

"$PS" -NoProfile -ExecutionPolicy Bypass -File tools/kosum-kapisi/kosum-kapisi.ps1 $GATE_ARGS
