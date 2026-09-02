#!/usr/bin/env bash
set -u
BASE="$(cd "$(dirname "$0")/../.." && pwd)"
HEDEF="$BASE/tools/VidShrink.Bench/Program.cs"
CIKTI="$BASE/.calisma/t111/mutasyon"
mkdir -p "$CIKTI"
ASIL='public const string FrameLock = "settb=AVTB,setpts=N";'
FILTRE="FullyQualifiedName~VmafPoolingTests"

kos() {
  ad="$1"; yeni="$2"
  python -c "
import io,sys
p=sys.argv[1]; a=sys.argv[2]; b=sys.argv[3]
s=io.open(p,encoding='utf-8-sig').read()
assert s.count(a)==1, 'sabit %d kez bulundu' % s.count(a)
io.open(p,'w',encoding='utf-8',newline='\n').write(s.replace(a,b))
" "$HEDEF" "$ASIL" "$yeni" || { echo "$ad: yama tutmadi"; return 1; }
  echo "=== $ad :: $yeni"
  dotnet build "$BASE/VidShrink.sln" -c Release --no-incremental > "$CIKTI/$ad.build.log" 2>&1
  bd=$?
  if [ $bd -ne 0 ]; then echo "$ad DERLENMEDI"; else
    dotnet test "$BASE/VidShrink.sln" -c Release --no-build --filter "$FILTRE" > "$CIKTI/$ad.test.log" 2>&1
    grep -E "^(Passed!|Failed!|  Failed )" "$CIKTI/$ad.test.log" | head -20
    grep -oE "Passed: *[0-9]+|Failed: *[0-9]+|Skipped: *[0-9]+|Total: *[0-9]+" "$CIKTI/$ad.test.log" | tr '\n' ' '; echo
  fi
  python -c "
import io,sys
p=sys.argv[1]; a=sys.argv[2]; b=sys.argv[3]
s=io.open(p,encoding='utf-8-sig').read()
io.open(p,'w',encoding='utf-8',newline='\n').write(s.replace(b,a))
" "$HEDEF" "$ASIL" "$yeni"
}

kos "M0-taban"        'public const string FrameLock = "settb=AVTB,setpts=N";'
kos "M1-setpts-yok"   'public const string FrameLock = "settb=AVTB";'
kos "M2-kilit-yok"    'public const string FrameLock = "null";'
kos "M3-esit-kayma"   'public const string FrameLock = "settb=AVTB,setpts=N+1";'
kos "M4-settb-yok"    'public const string FrameLock = "setpts=N";'
git -C "$BASE" diff --stat -- tools/VidShrink.Bench/Program.cs
