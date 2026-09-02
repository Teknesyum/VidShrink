#!/usr/bin/env bash
# T103 K8: ornekleme kuralinin mutasyon kaniti.
# Her mutasyonda tam yeniden derleme (--no-incremental); mtime'a guvenilmiyor.
set -uo pipefail
cd "$(dirname "$0")/../.."

KILIT=.calisma/t103/mutasyon.kilit
mkdir -p .calisma/t103
if ! mkdir "$KILIT" 2>/dev/null; then
  echo "DURDU: baska bir mutasyon kosumu surüyor ($KILIT). Iki kosum ayni dosyayi yamalar." >&2
  exit 3
fi
trap 'git checkout -- "$PROBE" 2>/dev/null; rmdir "$KILIT" 2>/dev/null' EXIT INT TERM

PROBE=src/VidShrink.Ffmpeg/ComplexityProbe.cs
if ! git diff --quiet -- "$PROBE"; then
  echo "DURDU: $PROBE zaten kirli. Temiz agactan basla." >&2
  exit 4
fi
FILTER='ComplexityProbeTests|ComplexityScanTests'

mutate() {
  local ad="$1" eski="$2" yeni="$3"
  python - "$PROBE" "$eski" "$yeni" <<'PY'
import io, sys
p, old, new = sys.argv[1], sys.argv[2], sys.argv[3]
s = io.open(p, encoding="utf-8").read()
if old not in s:
    print("YAMA TUTMADI: " + old, file=sys.stderr)
    sys.exit(2)
io.open(p, "w", encoding="utf-8").write(s.replace(old, new, 1))
PY
}

kos() {
  local ad="$1"
  if ! dotnet build VidShrink.sln -c Release --no-incremental -v q --nologo > /tmp/t103-build.log 2>&1; then
    echo "$ad: DERLENMEDI"
    return
  fi
  if dotnet test tests/VidShrink.Tests/VidShrink.Tests.csproj -c Release --no-build \
       --filter "$FILTER" --logger "console;verbosity=minimal" > /tmp/t103-test.log 2>&1; then
    echo "$ad: HAYATTA KALDI (testler yesil)"
  else
    echo "$ad: OLDU -> $(grep -oE '[A-Za-z_]+ \[FAIL\]' /tmp/t103-test.log | head -3 | tr '\n' ' ')"
  fi
}

echo "== taban =="
kos "mutasyonsuz"

declare -a ADLAR=(
  "M1-yerlesim-icerigi-izlemiyor"
  "M2-sayi-icerige-baglanmiyor"
  "M3-ust-sinir-yok"
  "M4-pencereler-ortusebilir"
  "M5-tabaka-agirliklari-esitlendi"
  "M6-sahne-siniri-yok"
  "M7-geri-donus-bos"
  "M8-agirliklar-kestirimde-yok"
)
declare -a ESKI=(
  'var distance = Math.Abs(windowBits[start] - target);'
  'internal const double WindowsPerHeterogeneity = 3.0;'
  'internal const int MaxPlannedWindows = 8;'
  'if (Math.Abs(start - other) < windowSeconds) { clash = true; break; }'
  'windows.Add(new SampleWindow(ClampStart(pick, duration, windowSeconds), windowSeconds, to - from));'
  'var centre = scenes[best].Start + (scenes[best].Length - windowSeconds) / 2.0;'
  'if (plan == SamplingPlan.Fixed || secondBits is null || secondBits.Count < MinProfileSeconds)
            return FixedWindows(duration, windowSeconds);'
  'var w = windows[i].Weight > 0 ? windows[i].Weight : 1.0;'
)
declare -a YENI=(
  'var distance = (double)start;'
  'internal const double WindowsPerHeterogeneity = 0.0;'
  'internal const int MaxPlannedWindows = 100;'
  'if (Math.Abs(start - other) < 0.0) { clash = true; break; }'
  'windows.Add(new SampleWindow(ClampStart(pick, duration, windowSeconds), windowSeconds, 1.0));'
  'var centre = 0.0;'
  'if (plan == SamplingPlan.Fixed || secondBits is null || secondBits.Count < MinProfileSeconds)
            return Array.Empty<SampleWindow>();'
  'var w = 1.0;'
)

for i in "${!ADLAR[@]}"; do
  echo "== ${ADLAR[$i]} =="
  if mutate "${ADLAR[$i]}" "${ESKI[$i]}" "${YENI[$i]}"; then
    kos "${ADLAR[$i]}"
  else
    echo "${ADLAR[$i]}: YAMA TUTMADI"
  fi
  git checkout -- "$PROBE"
done

dotnet build VidShrink.sln -c Release --no-incremental -v q --nologo > /dev/null 2>&1
echo "== bitti, kaynak geri alindi =="
