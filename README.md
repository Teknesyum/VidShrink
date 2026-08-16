# VidShrink

Shrink a video to a target file size with minimal quality loss — a free, offline,
ffmpeg-based video compressor for Windows, with optional AI-assisted encoding settings.

You drop in a video, say how many megabytes you want, and VidShrink works out the encoding
settings that fit the budget while giving up as little picture quality as possible.

## Two ways to decide the settings

**Automatic (default).** A built-in engine probes the file and derives the settings itself.
No AI, no network, no account. This path is complete on its own.

**AI-assisted (optional).** VidShrink writes a prompt describing your file and your target.
You paste it into any chat AI, paste the JSON answer back, and VidShrink validates it, shows you
exactly how it differs from the automatic plan, and applies it. Nothing is embedded and no API
key is needed. A malformed or unsafe answer is rejected and the automatic plan stays in place.

## How the automatic engine decides

1. `ffprobe` reads duration, resolution, frame rate, bitrate, codecs, and HDR status.
2. An audio budget is set (96–160 kbps depending on channels and intent, never more than a
   quarter of the total budget) and subtracted from the target.
3. The remaining budget becomes the video bitrate:
   `videoK = targetMB * 8192 / durationSec * 0.97 - audioK`
4. Bits-per-pixel (`videoK * 1000 / (width * height * fps)`) drives the rest:

   | bits/pixel | decision |
   |---|---|
   | ≥ 0.10 | keep resolution, quality-first CRF |
   | 0.05 – 0.10 | keep resolution, 2-pass VBR |
   | 0.03 – 0.05 | halve the frame rate if it is above 45 |
   | < 0.03 | step down the resolution ladder (up to 3 steps), then 2-pass |

5. Fixed quality rules: `preset slow`, `-pix_fmt yuv420p`, `-movflags +faststart`,
   keyframe interval of 2 seconds, Lanczos scaling.
6. If the result overshoots the target by more than 5%, the bitrate is corrected and the encode
   is retried — up to 3 attempts total.

## Codec options

- **Compatible** — `libx264`. Plays everywhere. Default.
- **Max compression** — `libx265`. Roughly 30–50% smaller at the same quality, slower.
- **Fast** — `h264_nvenc`. GPU-accelerated, much faster, worse quality per byte.

## Requirements

- Windows 10/11
- .NET 8 Desktop Runtime
- `ffmpeg.exe` and `ffprobe.exe`, either in `tools\ffmpeg` next to the executable or on `PATH`

## Build

```
dotnet build VidShrink.sln -c Release
```

## Project layout

```
src/VidShrink.Core     encoding math, plan model, prompt builder, JSON validator, argument builder
src/VidShrink.Ffmpeg   ffprobe/ffmpeg process wrappers with live progress
src/VidShrink.App      WPF user interface
```

## License

MIT
