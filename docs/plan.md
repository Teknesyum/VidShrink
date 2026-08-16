# VidShrink roadmap

## Product principles

VidShrink is an offline-first Windows media tool. The automatic target-size engine is complete and remains the default. AI is never embedded: the application only creates a prompt, validates pasted JSON, and falls back to automatic decisions when validation fails.

## Architecture

- `VidShrink.Core`: models, automatic size calculation, AI-plan validation, and shrink/conversion argument construction.
- `VidShrink.Ffmpeg`: discovery, ffprobe analysis, shared execution, progress, cancellation, GIF palette passes, and size correction.
- `VidShrink.App`: neon WPF interface with SHRINK, CONVERT, and ABOUT tabs.

## Releases

- **v0.1** — ffprobe analysis, automatic target-size engine, single-file encoding, and progress.
- **v0.2** — validated AI override, broad ffprobe-based input, stale-plan protection, codec-aware presets, safe cancellation, three tabs, format conversion, audio extraction, trimming, stream-copy checks, palette GIF output, and system information.
- **v0.3** — batch queue, multi-file drag and drop, and saved profiles.
- **v0.4** — VMAF reports and expanded hardware encoder discovery.
- **v1.0** — installer, optional bundled ffmpeg, and Explorer integration.

## v0.2 verification scope

Release verification covers a warning-free build, target-size runs across multiple containers, silent and variable-frame-rate input, rotation metadata, 4K input, every conversion container, GIF palette generation, and cancellation that terminates the ffmpeg process tree.
