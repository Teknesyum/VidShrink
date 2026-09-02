#!/bin/sh
set -e
cd "$(dirname "$0")/../../.."
run() {
  name=$1; file=$2; old=$3; new=$4
  cp "$file" "$file.bak"
  python -c "
import sys
p=sys.argv[1]; old=sys.argv[2].encode(); new=sys.argv[3].encode()
b=open(p,'rb').read()
assert b.count(old)==1, (p, b.count(old))
open(p,'wb').write(b.replace(old,new,1))
" "$file" "$old" "$new"
  dotnet build VidShrink.sln -c Release --no-incremental > /dev/null 2>&1
  out=$(dotnet test VidShrink.sln -c Release --no-build --filter "PreviewSegmentTests|EncodeRunnerTests" 2>&1 | tr -d '\r')
  echo "== $name"
  echo "$out" | grep -aE "Toplam:" | tail -1
  echo "$out" | grep -a "\[FAIL\]" | sed 's/.*    //' | sort -u
  mv "$file.bak" "$file"
  touch "$file"
}
run "M7 SegmentEncoder.Describe scenes dusuruldu" src/VidShrink.App/Playback/SegmentEncoder.cs \
  "complexity: complexity, availability: Availability, scenes: Scenes);" \
  "complexity: complexity, availability: Availability);"
run "M8 PanelHost iletici koptu" src/VidShrink.App/Playback/PanelHost.cs \
  "        set => _segments.Scenes = value;" \
  "        set { }"
run "M9 MainWindow panele gecirmiyor" src/VidShrink.App/MainWindow.axaml.cs \
  "        if (_preview is not null) _preview.Scenes = _sceneMap?.Map;" \
  "        if (_preview is not null) _preview.Scenes = null;"
