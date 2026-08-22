# Changelog

All notable changes to VidShrink are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). The project has no version
tags yet, so releases are grouped by the day their work landed on `main`.

## [Unreleased]

### Fixed

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

### Known gaps

- Targets of 50 MB and above spend all three encode attempts. The fill band is 2.8%
  wide at that size while one integer CRF step moves the output by roughly 12%, so the
  single-pass CRF path cannot land inside the band. Moving the first plan to two-pass
  fixes it and is tracked for the next engine round.
- Hardware VBR delivery is not measured yet. `av1_nvenc` overshoots its request on the
  first two-pass attempt, and the 1.06 correction factor only engages when calibration
  is unavailable.

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
