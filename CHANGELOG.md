# Changelog

All notable changes to VidShrink are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). `0.1.0` is the first tagged
release; the dated sections below it are the development record that led up to it and
ship as part of it.

## [Unreleased]

## [0.1.1] - 2026-08-26

### Changed

- Both installers download the published release instead of building it. They ask GitHub
  for the latest release, fetch the archive for the machine's target, verify its SHA-256
  against the release's own `checksums-<rid>.txt` and stop if the digest differs. The .NET
  SDK bootstrap is gone from both: `Find-DotNetSdk8`, `Install-DotNetSdk8`, the
  `dotnet publish` calls and the `main` source download no longer exist. Installing took
  minutes, left an SDK on the machine and produced a binary nobody had tested; it now
  installs the same binary the release pipeline tested.
- The installers write `.update-version` into the installed application folder. Without
  that marker the first launch after a fresh install compared the installed folder against
  the release file by file and downloaded almost the whole archive again — 191 of 220 files
  differ between a local build and the CI build, because the build is not deterministic.
- The installers stop on an architecture that has no release rather than installing a
  different one. Only `win-x64`, `osx-arm64`, `osx-x64` and `linux-x64` are published; on
  anything else the update check would look for an asset that does not exist and would
  silently never find an update.
- The Windows installer stops when the release does not carry the launcher, instead of
  leaving an installation whose shortcuts have nothing to point at.

### Added

- The release workflow publishes the launcher for `win-x64` as
  `vidshrink-launcher-win-x64.zip` and lists it in `checksums-win-x64.txt`. The launcher is
  what the shortcuts point at and what applies an update before the application is loaded;
  releases carried the application only, so an installer that stops compiling had nowhere
  to get it from.
- The preview panel grows in three steps instead of two. Zooming used to enlarge the video
  inside a panel that kept its band height until the video hit maximum, at which point the
  panel jumped over the others. There is now a middle step at 90% of the window between the
  two, with hysteresis on both descents so a notch of scroll cannot make the panel flicker
  between steps.
- The preview panel returns to its band on its own. Two seconds after the pointer leaves
  it — including when the pointer leaves the window entirely — it descends to the size it
  opened at. Dragging the separator, panning, and keyboard focus inside the panel hold the
  countdown.

### Fixed

- The blue outline is visible at the preview panel's corners while it is collapsed. The
  stage behind it painted a square background over the rounded border; the stage is now
  clipped to the same 16px radius, which covers its children as well.

## [0.1.0] - 2026-08-26

### Changed

- The window opens maximized. It used to open at a fixed 1560x1060 in the middle of the
  screen, which squeezed the panels while the space above and below them went unused; the
  plan panel in particular fell into a scrollbar as soon as a file was loaded. The normal
  size is still defined and is what the window returns to when it is restored.
- The plan panel grows into the height it is given instead of stopping at a fixed 640 px
  cap. Its scroll view is still there for a very long plan, but an ordinary plan no longer
  scrolls at either the maximized size or the minimum window size.
- The AI settings panel is a single line. The heading, a one-line summary of what the panel
  does, and a disclosure arrow are all that show until it is opened; the prompt buttons,
  the JSON box and the status line appear underneath once it is. Opening it reveals the
  full sentence, so the explanation is readable in both states and is never shown twice.
- The Teknesyum signature in the title bar no longer carries a `<>` icon. The interface
  standard gives that label no icon; the coffee cup on the support label stays.

- `Install-VidShrink.ps1` no longer writes Microsoft's `dotnet-install.ps1` to a temporary
  file before running it. Executing that file failed on any machine left at Windows'
  default `Restricted` execution policy, which aborted the install with
  `PSSecurityException` right after the .NET 8 SDK step began. The bootstrapper is now
  built with `[scriptblock]::Create` and invoked in memory, where execution policy does
  not apply. Windows PowerShell 5.1 returns the download as a `byte[]` for that URL, so
  the content is decoded as UTF-8 before it is parsed.
- The interface moved from WPF to Avalonia 11.3.20 and the application now targets
  `net8.0` instead of `net8.0-windows`. One source tree publishes for `win-x64`,
  `osx-arm64`, `osx-x64` and `linux-x64`. The neon theme was rebuilt as Avalonia
  `ControlTheme` resources with the base palette carried over unchanged; disabled-state
  contrast rose from 2.5:1 to 4.2:1 and the `?` badges became keyboard reachable.
- Revealing a finished file works on all three platforms: `explorer /select,` on Windows,
  `open -R` on macOS, `xdg-open` on Linux. `xdg-open` cannot highlight a file, so on
  Linux the containing folder opens instead of the file being selected.

### Added

- Fast shrink (GPU) turns itself on when the machine can carry it. The probe no longer
  only reports that an encoder exists; it decides whether the encoder is good enough,
  from four measured facts - is the chosen encoder hardware, did the probe encode pass,
  how long did it take, and does the bitrate the plan asks for sit above the floor that
  encoder can actually follow. There is no list of graphics card model names, because
  such a list goes stale on the first card that ships after it. When anything is
  uncertain the box stays off, which is the behaviour that already worked. The decision
  is written once next to the settings file and a choice made by hand is never
  overwritten; the tip beside the box says why it opened or why it stayed shut. On the
  machine it was measured on: `av1_nvenc`, probe passed in 193 ms, box opened.
- `install-vidshrink.sh`, a one-command installer for macOS and Linux. It bootstraps the
  .NET 8 SDK into `~/.dotnet` when the machine has none, reads the runtime identifier
  from `uname`, publishes a self-contained build into `~/.local/share/vidshrink` and
  links `~/.local/bin/vidshrink`. It never installs FFmpeg itself: when `ffmpeg` or
  `ffprobe` is missing it prints the package manager command and stops before
  downloading anything.
- A license section in the README separating VidShrink's MIT terms from FFmpeg's. The
  FFmpeg binary arrives on the user's own machine under its own license and is not
  redistributed by this project; a packaged release would change that and has not been
  built.

### Fixed

- Launching the application no longer opens a console window beside it. The project was
  built as `OutputType Exe`, which is the console subsystem on Windows; a desktop
  Avalonia application has to be `WinExe`. Measured in the PE header: the executable's
  subsystem field went from 3 (console) to 2 (GUI).

- The application no longer crashes on startup. Setting `WindowState` in XAML makes
  `OnPropertyChanged` run before `InitializeComponent` has finished, and the maximize
  button was still null when the handler reached for it. The build did not show this; it
  only appeared when the application was actually launched.

- Targets of 50 MB and above now land inside the fill band on the first attempt. The
  first plan switches to two-pass whenever the band is narrower than one CRF step, the
  retry aim is clamped to the band, `KbitPerMib` is the exact 8388.608, and the source
  size is capped at ×0.95. Measured end to end: 180 MB → 178.35, 100 MB → 99.16,
  25 MB → 24.63, 8 MB → 7.85. All four landed inside the band on the first attempt and
  the ceiling was never crossed.
- The installer no longer asks WinGet for the .NET 8 SDK. `Microsoft.DotNet.SDK.8`
  ships no user-scope installer, so `--scope user` aborted with
  `NO_APPLICABLE_INSTALLER` (`-1978335216`) before any other step could run — the
  installer failed on every machine, not just some. The SDK is now bootstrapped with
  Microsoft's own `dotnet-install.ps1` into `%LOCALAPPDATA%\Microsoft\dotnet`: no
  administrator rights, no scope filter, correct architecture.
- An already-installed .NET 8 SDK is detected first and kept by full path, so a `PATH`
  refresh later in the script cannot lose it.
- WinGet is now used only for FFmpeg, and retries at machine scope if the same
  no-applicable-installer error appears.
- The published build uses the host's runtime identifier instead of a fixed `win-x64`,
  so ARM64 machines get a native build.

- Every target now lands inside the fill band on the first attempt, on the graphics
  card as well as the processor. The cause was the peak rate, not the size estimate: a
  peak pinned close to the request stops the encoder overshooting but also stops it
  filling the stretches it could have filled, and the further the request sits above the
  encoder's own floor the more of the clip saturates. The peak is now derived from that
  ratio rather than from an absolute knee. Measured end to end on a 400 s 1920x1080@60
  source: on `av1_nvenc` 180, 100, 50, 25 and 8 MB, on `libx264` 180, 100, 25 and 8 MB -
  nine targets, nine first attempts, no ceiling crossed.
- What the encoder can actually deliver is now measured rather than assumed.
  `CodecModel.MinBitrateK` comes from nine layouts encoded at the hardware floor; the
  fit `kbit/s per Mpx = 4.29 x fps + 75.6` is carried 15 percent high because the worst
  residual was 11 percent. The layout search skips any shape the request cannot clear by
  twice that floor, so a plan is never built on a bitrate the card would ignore.
- The mp4 container costs a flat 9.0 kbit/s at every target, not a percentage. That is
  0.7 percent of a 100 MB budget and 9 percent of an 8 MB one, and it is now held back
  on the hardware path instead of being absorbed by the video stream.

### Known gaps

- The encoder floor is measured for `av1_nvenc` only. AMF and QSV fall over on the
  machine the measurements were taken on (`AMFQueryVersion failed with error 1`), so
  their floor is the NVENC fit until someone measures them.

## 2026-08-22 — Fast Shrink (GPU)

### Added

- **Fast Shrink (GPU)** checkbox in the interface. It is disabled with a visible reason
  when no working hardware encoder is present, and the GPU probe runs in the background
  during window load so the interface never waits on it.
- Hardware encoder family: `av1_nvenc`, `hevc_nvenc`, `av1_qsv`, `hevc_qsv`, `av1_amf`,
  `hevc_amf`, `h264_nvenc`, each with its own relative bitrate need, quality limit and
  preset table read from FFmpeg rather than assumed.
- Real hardware probing in `EncoderCapabilities`: a 256×256 source is encoded with a
  4-second timeout and the result is cached for the session. Listing an encoder in
  `ffmpeg -encoders` no longer counts as having the hardware.
- `-hwaccel auto` before the input, for the measured decode saving.
- `SpeedMode` with a `Fast` encoder ordering, and a repeatable live band test gated
  behind the `VIDSHRINK_LIVE_SOURCE` environment variable. The test now reports
  `Skipped` instead of passing silently.

### Changed

- The fill retry aims using the encoder yield measured on the previous completed
  two-pass attempt, so the clamp sits on the predicted delivered size rather than on the
  requested bitrate.
- The complexity probe runs its windows in parallel and splits them from a single decode
  pass: 15.9% faster measurement with the measured values unchanged to within 0.002%.
- `Fast - NVENC` was removed from the codec list; speed is no longer a codec choice.
- The single remaining two-pass uncertainty constant is `TwoPassUncertainty = 0.04`;
  `CalibratedRetrySpread` was removed.

### Fixed

- Hardware encoders no longer run a fake two-pass. NVENC's first pass produced a
  zero-byte statistics file, so every hardware encode was being run twice for nothing.
  Rate control is now `-rc vbr -multipass fullres` for NVENC, `-rc vbr_peak` for AMF and
  `-look_ahead 1` for `h264_qsv` only — each verified against FFmpeg.
- The under-target reason text names the band's lower edge instead of the hard floor.

## 2026-08-20 — The size ceiling becomes a promise

### Added

- Fill-target policy: the encoder aims for the band between 92% and 100% of the target
  (97.2% and above once the target reaches 50 MB) instead of stopping at the
  transparency ceiling.
- GPU encoding measurements recorded in `docs/gpu-kodlama-bulgusu.md`.
- Neon engine diagram in the README.

### Changed

- The probe window bias is corrected against the whole file using ffprobe packet data
  and warmed spot sampling.

### Fixed

- The target is now an absolute ceiling. When three attempts all land above it, no file
  is written at all and the interface says why; the last under-band result is kept as a
  fallback so a ceiling breach never leaves the user empty-handed.

## 2026-08-19 — Measurement engine

### Added

- `QualityMeter`, the bench tool and the calibration probe.
- Localized reason codes explaining every plan decision in Turkish and English.
- Atomic output writing, a disk-space guard before the run, and an encoder capability
  cache.
- HDR and 10-bit policy: preserve HDR10 on encoders that carry it, otherwise tone-map to
  SDR with `zscale`/`tonemap=hable` and say that the policy changed.

### Fixed

- Atomic output no longer breaks FFmpeg muxer selection.

## 2026-08-18 — Installation and audit

### Added

- One-command Windows installer and a portable screenshot.
- Engine roadmap and the engine audit report in `docs/claude-engine-audit-report.md`.

### Changed

- The encoding engine was audited and hardened.

## 2026-08-17 — Neon interface

### Added

- Turkish localization with instant `TR` / `EN` switching.
- A `?` badge on every technical control explaining what it does, whether it affects
  sending to WhatsApp, and whether phones support the result.
- Custom neon window chrome, transparent application icon, and an always-current
  desktop launcher.
- WhatsApp defaults: 16 MB, Sharing intent, automatic codec.
- Complete UI requirements history in `docs/ui-requirements-history.md`.

### Changed

- Per-title detail falloff, a budget-filling CRF ceiling and regime-aware strategy in
  the engine.
- Typography, spacing, gradients and control outlines settled into the neon dark theme.

## 2026-08-16 — Initial

### Added

- Target-size video compressor: complexity model, strategy, planning and FFmpeg argument
  construction, with a WPF interface.
- Conversion workflow with hardened media processing.
