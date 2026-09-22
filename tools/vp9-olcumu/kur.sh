#!/usr/bin/env bash
set -euo pipefail
url='https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-09-22-13-18/ffmpeg-n9.0.2-3-ga5923073bf-linux64-gpl-9.0.tar.xz'
sha='6cb8d11e4ce7f067079a6866b94145918d1c121604d578236015181f9677d5fd'
arsiv="$RUNNER_TEMP/ffmpeg.tar.xz"
curl -fsSL -o "$arsiv" "$url"
echo "$sha  $arsiv" | sha256sum -c -
tar -xJf "$arsiv" -C "$RUNNER_TEMP"
bin="$RUNNER_TEMP/ffmpeg-n9.0.2-3-ga5923073bf-linux64-gpl-9.0/bin"
test -x "$bin/ffmpeg"
echo "$bin" >> "$GITHUB_PATH"
"$bin/ffmpeg" -hide_banner -version | head -n 1
"$bin/ffmpeg" -hide_banner -encoders | grep -q libvpx-vp9 || { echo 'libvpx-vp9 yok'; exit 1; }
"$bin/ffmpeg" -hide_banner -filters | grep -q libvmaf || { echo 'libvmaf yok'; exit 1; }
