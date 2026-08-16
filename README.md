# VidShrink

VidShrink is a free, offline Windows application for shrinking video to a target file size and converting media with ffmpeg.

## Shrink

Drop or browse to any file that `ffprobe` recognizes as containing a video stream. The filename extension is never an acceptance gate. Silent video, variable-frame-rate sources, animated GIF, rotation metadata, and uncommon containers work whenever the installed ffmpeg build can decode them.

The automatic engine is the default and works without AI or a network connection:

1. Reserve audio bitrate and subtract it from the target budget.
2. Convert the remainder to video bitrate.
3. Apply the bits-per-pixel decision table.

   | Bits per pixel | Decision |
   |---|---|
   | ≥ 0.10 | Keep resolution and use quality-first CRF |
   | 0.05–0.10 | Keep resolution and use two-pass VBR |
   | 0.03–0.05 | Halve a high frame rate when necessary |
   | < 0.03 | Step down the resolution ladder, then use two-pass VBR |

4. If output exceeds tolerance, correct the measured bitrate and retry, up to three attempts.

CRF output size is not shown as an exact estimate. VidShrink presents the target as a ceiling and performs a two-pass correction when needed.

AI mode is optional. VidShrink creates a prompt for any chat AI and validates pasted JSON against the current source and options. No AI is embedded: it stays offline, needs no API key, and safely returns to the automatic plan when a response is malformed or stale.

## Convert

The CONVERT tab supports MP4, MKV, WebM, MOV, AVI, GIF, MP3, M4A, and WAV. Choose H.264, H.265, VP9, AV1, or stream copy; CRF or fixed bitrate; source, preset, or custom resolution and frame rate; audio encoding, copy, or removal; and optional trimming. MP3, M4A, and WAV extract audio only. GIF conversion uses `palettegen` followed by `paletteuse`.

Stream copy uses real `-c:v copy` and/or `-c:a copy`. Incompatible container and source-codec combinations are blocked before execution. The exact ffmpeg command is visible for every operation.

## About

The ABOUT tab explains the size engine, AI design, and codec choices. It also shows the ffmpeg path and version, .NET version, application version, project link, and support link.

## Codec guidance

- H.264 offers the broadest compatibility.
- H.265 improves compression where modern playback is available.
- VP9 is a natural WebM choice.
- AV1 offers high efficiency at higher CPU cost.
- Stream copy is fastest and lossless when the destination supports the source stream.

## Requirements and build

- Windows 10 or 11
- .NET 8 Desktop Runtime
- `ffmpeg.exe` and `ffprobe.exe` in `tools\ffmpeg` beside the application, or on `PATH`

```powershell
dotnet build VidShrink.sln -c Release
```

## Project layout

```text
src/VidShrink.Core     planning, validation, and ffmpeg argument construction
src/VidShrink.Ffmpeg   ffprobe and shared ffmpeg process execution
src/VidShrink.App      WPF user interface
```

## License

MIT
