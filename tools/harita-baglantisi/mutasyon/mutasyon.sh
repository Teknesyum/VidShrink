#!/usr/bin/env bash
set -u
W="$(cd "$(dirname "$0")/../../.." && pwd)"
cd "$W"
OUT="$W/.calisma/t113/mutasyon"
mkdir -p "$OUT"
FILTER='EncodeRunnerTests|PreviewSegmentTests|FfmpegArgumentsTests'

run() {
  local id="$1" file="$2" old="$3" new="$4"
  echo "########## $id  $file  $(date +%H:%M:%S)"
  cp "$file" "$OUT/$id.bak"
  python - "$file" "$old" "$new" <<'PY'
import sys
p, old, new = sys.argv[1], sys.argv[2], sys.argv[3]
raw = open(p, 'rb').read()
bom = raw.startswith(b'\xef\xbb\xbf')
s = raw.decode('utf-8-sig')
assert s.count(old) == 1, f'{p}: {s.count(old)} eslesme'
s = s.replace(old, new)
open(p, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + s.encode('utf-8'))
PY
  if [ $? -ne 0 ]; then echo "$id: yama tutmadi"; cp "$OUT/$id.bak" "$file"; return; fi
  dotnet build VidShrink.sln -c Release --no-incremental -v q --nologo > "$OUT/$id-build.log" 2>&1
  if [ $? -ne 0 ]; then echo "$id: DERLEME KIRMIZI"; tail -3 "$OUT/$id-build.log"; cp "$OUT/$id.bak" "$file"; return; fi
  dotnet test tests/VidShrink.Tests/VidShrink.Tests.csproj -c Release --no-build --filter "$FILTER" > "$OUT/$id-test.log" 2>&1
  grep -oE '(Basarisiz|Failed|Basarili|Passed|Atlandi|Skipped)!?[^,]*' "$OUT/$id-test.log" | tail -2
  grep -E '^\s*(Basarisiz|Failed)\s' "$OUT/$id-test.log" | sed 's/^/    /' | head -20
  cp "$OUT/$id.bak" "$file"
}

run M1 src/VidShrink.App/MainWindow.axaml.cs '_encoders, _sceneMap?.Map));' '_encoders, null));'
run M2 src/VidShrink.App/MainWindow.axaml.cs 'AskBeforeRetryAsync, _sceneMap?.Map);' 'AskBeforeRetryAsync, null);'
run M3 src/VidShrink.App/MainWindow.axaml.cs '_sceneMap = await EncodeRunner.TryBuildSceneMapAsync(info, ct: cts.Token);' '_sceneMap = null; await Task.Yield();'
run M4 src/VidShrink.Core/PreviewSegment.cs 'outputPath, availability, scenes)' 'outputPath, availability, null)'
run M5 src/VidShrink.Ffmpeg/EncodeRunner.cs 'passLogPrefix, availability, scenes);' 'passLogPrefix, availability, null);'
run M6 src/VidShrink.Ffmpeg/EncodeRunner.cs 'EncoderCapabilities.Instance, scenes);' 'EncoderCapabilities.Instance, null);'

echo "########## kaynak geri alindi, temiz derleme $(date +%H:%M:%S)"
git status --short
dotnet build VidShrink.sln -c Release --no-incremental -v q --nologo > "$OUT/temiz-build.log" 2>&1
echo "temiz derleme exit=$?"
