# VidShrink

Free, offline Windows app that shrinks a video to a target file size — and loses the least of what a person can actually see while doing it.

Give it a file and a ceiling in megabytes. It never returns a file larger than you asked for, and it tells you the expected size before you press start.

The interface starts in Turkish and switches to English instantly with the `TR` / `EN` buttons.

![VidShrink current interface](docs/assets/vidshrink-current.png)

### Measured compression flow

![VidShrink measured compression engine](docs/assets/vidshrink-neon.svg)

## One-command Windows installation

Open PowerShell and run:

```powershell
irm https://raw.githubusercontent.com/Teknesyum/VidShrink/main/Install-VidShrink.ps1 | iex
```

The installer checks for the .NET 8 SDK and FFmpeg/FFprobe, installs missing dependencies through WinGet, downloads the latest `main` source, publishes a self-contained Windows x64 Release build, installs it under `%LOCALAPPDATA%\Programs\VidShrink`, and creates Desktop and Start Menu shortcuts. Running the same command again replaces the installed app with the newest version.

If PowerShell script execution is restricted by organizational policy, download and inspect [`Install-VidShrink.ps1`](Install-VidShrink.ps1), then run it from an allowed PowerShell session.

### README image portability

The screenshot above is committed at `docs/assets/vidshrink-current.png` and referenced with a repository-relative path. Do not replace it with a path such as `C:\Users\...`, a temporary Codex attachment path, or a `file://` URL: those addresses exist only on the computer that created them. On GitHub, also keep filename capitalization identical and make sure the image file is committed and pushed rather than merely present in the local working folder.

## Why this one

Most size-target compressors apply a lookup table. So many megabytes per minute becomes so much resolution, regardless of whether you handed it a static screen recording or a handheld night shot. That table is wrong for every clip that is not average — which is most clips.

**VidShrink measures your actual file instead of guessing from its bitrate.**

Before planning anything, it encodes short samples of your clip at two different resolutions and reads two numbers off the result:

- **How many bits this content really costs.** A gradient and a confetti cannon at the same 1080p30 are not the same encoding problem. The source bitrate does not tell you which one you have — the source could have been encoded badly, or with a different codec, or twice.
- **How much of that cost disappears when the picture is scaled down.** This varies enormously between clips. On real measurements taken during development, one test clip lost 87% of its per-pixel cost when halved; another lost 22%. A fixed assumption is wrong for both.

That second number is the one nearly nobody measures, and it is what decides the single most consequential question in size-target compression: *is it better to keep the resolution and encode it worse, or drop the resolution and encode it well?* VidShrink answers that per clip, from measurement, instead of from a rule.

### What follows from measuring

| | Typical size-target tool | VidShrink |
|---|---|---|
| Content complexity | inferred from source bitrate | measured by encoding samples of your file |
| Detail falloff on downscale | fixed assumption, or ignored | measured per clip at two resolutions |
| Resolution choice | fixed ladder (1080 → 720 → 480) | continuous search, any scale that fits |
| Frame rate choice | applied after resolution, if at all | searched jointly with resolution |
| Codec choice | whatever you picked | can be chosen from how hard the target actually is |
| Audio budget | fixed bitrate | share that shrinks as the target tightens |
| Size estimate | often none for quality mode | measured number with a stated range, shown up front |
| Overshoot handling | retry loop | plan lands in one pass; retry is the fallback, not the plan |

### It knows when to stop

Filling the target is not the goal — hitting the quality ceiling is. Once more bits stop buying anything a viewer could see, VidShrink hands back a smaller file rather than padding it out to the number you typed. Ask for 25 MB on an easy clip and you may get 9 MB that looks identical to the source.

The reverse also holds: when the target genuinely constrains quality, it spends the whole budget rather than leaving a third of it unused.

### It adapts to how hard you are pushing

A 1.2× reduction and a 600× reduction are different problems and get different treatment:

| Scenario | Reduction | Engine behaviour |
|---|---|---|
| Light | under 1.5× | keeps resolution and frame rate, simply spends the budget |
| Balanced | 1.5–6× | allows resolution scaling |
| Aggressive | 6–30× | unlocks frame-rate reduction, moves to H.265, trims audio share |
| Extreme | over 30× | maximum compression, mono or dropped audio, and it says so |

Whatever it changes, it explains — in plain language, in the app, before you start.

### Where the loss goes

Below a certain bit budget something has to give. VidShrink spends the loss where the eye is least sensitive: softness before blocking, fewer pixels before broken pixels, mono audio before a starved picture.

### Measured results

Development benchmark over three synthetic clips (easy gradients, standard test pattern, pure noise) at four targets each:

- **8 of 8 runs landed under target on the first attempt** — no retry needed
- **Size estimate accurate to within 8%**, typically within 4%
- **Budget fill 92–99%** on constrained targets

## Shrink

Drop or browse to any file `ffprobe` recognizes as containing a video stream. The filename extension is never an acceptance gate. Silent video, variable frame rate, animated GIF, rotation metadata, and uncommon containers all work whenever the installed ffmpeg can decode them.

Defaults are set for the most common case: **16 MB, Sharing intent, automatic codec.** WhatsApp accepts files up to 2 GB, but it re-compresses any video you send in chat with its own weak encoder. Staying at or below 16 MB usually gets your file through with far less damage, so the other side sees VidShrink's quality rather than WhatsApp's. To bypass WhatsApp's re-encode entirely, send the result as a document.

Every technical control carries a `?` badge explaining, in both languages, what it does, whether it affects sending to WhatsApp, and whether phones support the result.

AI mode is optional and not embedded. VidShrink writes a prompt you can paste into any chat AI, then validates the JSON you paste back against the current source and options. It stays offline, needs no API key, and falls back to the automatic plan when a response is malformed or stale.

## Convert

The CONVERT tab supports MP4, MKV, WebM, MOV, AVI, GIF, MP3, M4A, and WAV. Choose H.264, H.265, VP9, AV1, or stream copy; CRF or fixed bitrate; source, preset, or custom resolution and frame rate; audio encoding, copy, or removal; and optional trimming. MP3, M4A, and WAV extract audio only. GIF conversion uses `palettegen` followed by `paletteuse`.

Stream copy uses real `-c:v copy` and `-c:a copy`. Incompatible container and source-codec combinations are blocked before execution. The exact ffmpeg command is visible for every operation.

## Codec guidance

- **H.264** plays on essentially every device ever made and is what WhatsApp expects.
- **H.265** needs roughly a third fewer bits for the same picture; every phone since about 2016 decodes it in hardware, but older handsets and some web players will not.
- **VP9** is a browser and WebM format.
- **AV1** compresses best and encodes slowest; only recent phones decode it.
- **Stream copy** is instant and lossless when the destination accepts the source streams.

## Requirements and development build

- Windows 10 or 11
- .NET 8 Desktop Runtime
- `ffmpeg.exe` and `ffprobe.exe` in `tools\ffmpeg` beside the application, or on `PATH`

```powershell
dotnet build VidShrink.sln -c Release
dotnet test VidShrink.sln
```

## Project layout

```text
src/VidShrink.Core     complexity model, strategy, planning, ffmpeg argument construction
src/VidShrink.Ffmpeg   ffprobe, complexity probe, encode execution
src/VidShrink.App      WPF user interface
tests/VidShrink.Tests  engine and argument-generation regression tests
```

The current engine audit, fixed defects, benchmark requirements, and quality roadmap are documented in [`docs/claude-engine-audit-report.md`](docs/claude-engine-audit-report.md). A market-leading claim is intentionally deferred until the HDR/10-bit, perceptual-metric, and competitor benchmark gates in that report are met.

## License

MIT
