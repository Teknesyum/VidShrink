#!/usr/bin/env bash
set -euo pipefail

lock="$1"
work="$2"
out="$3"
target="${MPVKIT_MIN_MACOS:-13.0}"
limit="${MPVKIT_MINOS_LIMIT:-14.0}"

mkdir -p "$work/zip" "$work/x" "$out"
report="$out/minos-lipo.tsv"
printf 'name\tkind\tarchs\tminos_arm64\tminos_x86_64\tmembers_without_build\tzip_bytes\n' > "$report"

ver_le() { [ "$(printf '%s\n%s\n' "$1" "$2" | sort -V | head -n1)" = "$1" ]; }

minos_of() {
  local bin="$1" arch="$2"
  otool -arch "$arch" -l "$bin" 2>/dev/null | awk '
    /cmd LC_BUILD_VERSION/ {b=1; next}
    /cmd LC_VERSION_MIN_MACOSX/ {v=1; next}
    b && $1=="minos" {print $2; b=0}
    v && $1=="version" {print $2; v=0}
  ' | sort -V | tail -n1
}

without_build() {
  local bin="$1" arch="$2"
  otool -arch "$arch" -l "$bin" 2>/dev/null | awk '
    /^[^ ].*\.o\):$/ || /^[^ ].*:$/ {if (n && !seen) c++; n=1; seen=0; next}
    /LC_BUILD_VERSION|LC_VERSION_MIN_MACOSX/ {seen=1}
    END {if (n && !seen) c++; print c+0}
  '
}

fail=0
archives=()
while read -r name sha url; do
  [ -z "$name" ] && continue
  z="$work/zip/$name.zip"
  if [ ! -f "$z" ] || [ "$(shasum -a 256 "$z" | awk '{print $1}')" != "$sha" ]; then
    curl -fsSL --retry 3 -o "$z" "$url"
  fi
  got=$(shasum -a 256 "$z" | awk '{print $1}')
  if [ "$got" != "$sha" ]; then
    echo "sha256 MISMATCH $name expected=$sha got=$got"
    exit 1
  fi
  echo "sha256 ok $name $got"
  rm -rf "$work/x/$name"
  mkdir -p "$work/x/$name"
  unzip -q "$z" -d "$work/x/$name"
  slice=$(find "$work/x/$name" -maxdepth 2 -type d -name 'macos-*' | head -n1)
  if [ -z "$slice" ]; then
    printf '%s\tNO-MACOS-SLICE\t-\t-\t-\t-\t%s\n' "$name" "$(stat -f %z "$z")" >> "$report"
    fail=1
    continue
  fi
  bin=""
  while IFS= read -r f; do
    if file -b "$f" | grep -qE 'Mach-O|ar archive'; then bin="$f"; break; fi
  done < <(find "$slice" \( -type f -o -type l \) ! -name '*.h' ! -name '*.plist' ! -name '*.modulemap' | sort)
  if [ -z "$bin" ]; then
    printf '%s\tNO-BINARY\t-\t-\t-\t-\t%s\n' "$name" "$(stat -f %z "$z")" >> "$report"
    fail=1
    continue
  fi
  kind=$(file -b "$bin" | grep -oE 'ar archive|dynamically linked shared library|bundle' | sort -u | tr '\n' ',' | sed 's/,$//')
  archs=$(lipo -archs "$bin")
  ma=$(minos_of "$bin" arm64); ma=${ma:-none}
  mx=$(minos_of "$bin" x86_64); mx=${mx:-none}
  wb="$(without_build "$bin" arm64)/$(without_build "$bin" x86_64)"
  printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\n' "$name" "$kind" "$archs" "$ma" "$mx" "$wb" "$(stat -f %z "$z")" >> "$report"
  case " $archs " in *" arm64 "*) ;; *) fail=1 ;; esac
  case " $archs " in *" x86_64 "*) ;; *) fail=1 ;; esac
  for m in "$ma" "$mx"; do
    if [ "$m" = none ] || ! ver_le "$m" "$limit"; then fail=1; fi
  done
  archives+=("$bin")
done < "$lock"

column -t -s $'\t' "$report"
echo "inputs-criteria fail=$fail (limit minos<=$limit, archs arm64+x86_64)"

libmpv=""
others=()
for a in "${archives[@]}"; do
  case "$(basename "$a")" in
    Libmpv) libmpv="$a" ;;
    *) others+=("$a") ;;
  esac
done
[ -n "$libmpv" ] || { echo "Libmpv binary not found"; exit 1; }

printf '_mpv_*\n' > "$work/exports.txt"
frameworks=(AppKit Foundation CoreFoundation CoreGraphics CoreText CoreServices IOKit IOSurface QuartzCore Metal
  AVFoundation CoreAudio AudioToolbox AudioUnit CoreMedia CoreVideo VideoToolbox Security SystemConfiguration
  OpenGL Carbon Cocoa)
fwflags=()
for f in "${frameworks[@]}"; do fwflags+=(-framework "$f"); done

swiftlib="$(dirname "$(xcrun -f swiftc)")/../lib/swift/macosx"
set +e
clang -dynamiclib -arch arm64 -arch x86_64 -mmacosx-version-min="$target" \
  -o "$out/libmpv.2.dylib" -install_name @rpath/libmpv.2.dylib \
  -Wl,-force_load,"$libmpv" "${others[@]}" \
  -Wl,-exported_symbols_list,"$work/exports.txt" -Wl,-dead_strip \
  "${fwflags[@]}" -L"$swiftlib" -L/usr/lib/swift -Wl,-rpath,/usr/lib/swift \
  -lbz2 -liconv -lexpat -lresolv -lxml2 -lz -lc++ 2> "$out/link.log"
rc=$?
set -e
echo "link rc=$rc warnings=$(grep -c 'warning' "$out/link.log" || true) newer-than-target=$(grep -c 'built for newer' "$out/link.log" || true)"
grep -v 'auto-linked' "$out/link.log" | head -n 80 || true
[ "$rc" -eq 0 ] || exit 1

d="$out/libmpv.2.dylib"
codesign --force --sign - "$d"
echo "--- output dylib"
file "$d"
lipo -info "$d"
for a in arm64 x86_64; do
  echo "$a: $(vtool -arch "$a" -show-build "$d" | awk '$1=="minos"||$1=="sdk"{printf "%s=%s ", $1, $2}')"
done
echo "bytes=$(stat -f %z "$d") sha256=$(shasum -a 256 "$d" | awk '{print $1}')"
otool -L "$d"
foreign=$(otool -arch all -L "$d" | grep -E '^[[:space:]]' | grep -vcE '^\s*(/usr/lib/|/System/Library/|@rpath/libmpv)' || true)
echo "non-system-refs=$foreign"
dm=$(for a in arm64 x86_64; do vtool -arch "$a" -show-build "$d" | awk '$1=="minos"{print $2}'; done | sort -V | tail -n1)
echo "output-minos-max=$dm"
if [ "$foreign" -ne 0 ] || ! ver_le "$dm" "$limit"; then fail=1; fi
echo "fail=$fail" > "$out/verdict.txt"
echo "verdict fail=$fail"
