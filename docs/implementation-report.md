# VidShrink v0.2 implementation report

This report is intended for an independent code and product review.

## Product behavior

VidShrink now has three neon-themed tabs:

- **SHRINK / KÜÇÜLT** performs target-size compression through the complete offline automatic engine, with an optional validated AI-plan override.
- **CONVERT / DÖNÜŞTÜR** converts to MP4, MKV, WebM, MOV, AVI, GIF, MP3, M4A, or WAV using the same process runner.
- **ABOUT / HAKKINDA** explains the decision engine, AI boundary, codec choices, runtime versions, repository, and support links.

The application starts in Turkish. `TR` and `EN` buttons switch the complete static interface at runtime. Dynamic status, probe, AI validation, progress, cancellation, and completion messages follow the selected language. Language switching does not rebuild the loaded media plan or change selected encoding values.

## Readability correction

The original implicit WPF `ComboBox` style requested a light foreground while Windows rendered a light native control background. Values such as `Sharing` were therefore nearly invisible.

The shared theme now explicitly applies:

- opaque dark-teal background `#FF07181B`;
- light body text for high contrast;
- neon-blue border;
- neon-blue highlighted rows with black text;
- a translucent neon selected state inside the drop-down.

This is applied globally to every `ComboBox` and `ComboBoxItem`, including codec, intent, container, quality, resolution, frame-rate, and audio selectors. No selector uses a white background.

## Processing corrections

- Extra ffmpeg arguments are placed before every output target, including first-pass null output.
- Cancellation registration is installed immediately after process start and kills the complete ffmpeg process tree.
- Concurrent stderr tail collection removes the former cross-thread queue race.
- Cancelled conversions remove partial output files.
- Presets are mapped and validated per encoder family: x264/x265, SVT-AV1, VP9, NVENC, and QSV.
- CRF plans no longer claim a calculated output size; the UI presents the target as a ceiling and the runner corrects an overshoot with measured two-pass encoding.
- Clipboard contention is caught and reported instead of crashing the application.
- Target presets expand the slider range when necessary.
- Output names are collision-safe. Reloading `name_shrunk.mp4` produces `name_shrunk_2.mp4`.
- AI JSON is revalidated whenever target or planning options change. A stale plan falls back to automatic mode.

## Input and metadata handling

Input acceptance is decided by `ffprobe`, not the filename extension. The browse filter remains a convenience and includes common and uncommon media extensions plus all files.

The probe path safely supports:

- files without audio;
- variable frame rate;
- animated or unusual decodable inputs;
- attached-picture streams without mistaking them for the main video;
- rotation from `rotate` tags or display-matrix side data;
- displayed width/height after 90-degree or 270-degree rotation.

ffmpeg autorotation remains enabled, so rotated sources are encoded upright.

## Conversion design

`ConversionPlan` and `ConversionArguments` represent conversion decisions independently from target-size `EncodePlan`. `EncodeRunner.ConvertAsync` reuses the existing progress, cancellation, stderr, and process-lifetime infrastructure.

- Stream copy emits real `-c:v copy` and/or `-c:a copy`.
- Invalid source-codec/container copy combinations are blocked before execution with an explanation.
- MP3, M4A, and WAV are audio-only outputs.
- Optional start and end times are supported.
- GIF uses a palette-generation pass followed by `paletteuse`.
- The full ffmpeg command is visible before execution.

## Measured verification

Release build result: **0 warnings, 0 errors**.

Target-size runs:

| Source case | Input | Output | Target | Attempts |
|---|---:|---:|---:|---:|
| MKV with AAC audio | 1.439 MB | 0.786 MB | 0.792 MB | 1 |
| Variable-frame-rate MOV | 0.256 MB | 0.109 MB | 0.141 MB | 2 |
| Silent WebM | 0.270 MB | 0.082 MB | 0.148 MB | 2 |
| Rotated MP4 | 0.213 MB | 0.106 MB | 0.117 MB | 1 |
| 3840x2160 MP4 | 6.423 MB | 3.008 MB | 3.533 MB | 1 |

Conversion runs from the 1.439 MB test source:

| Output | Measured size |
|---|---:|
| MP4 | 0.362 MB |
| MKV | 0.378 MB |
| WebM | 0.599 MB |
| MOV | 0.362 MB |
| AVI | 0.392 MB |
| GIF | 1.019 MB |
| MP3 | 0.093 MB |
| M4A | 0.076 MB |
| WAV | 0.551 MB |

Cancellation was tested during a 4K SVT-AV1 conversion. The operation raised cancellation, the partial output did not remain, and the active ffmpeg process count returned to **0**.

## UI verification

The WPF application was launched from the Release output and inspected at 1220x900. The following were visually checked:

- Turkish is the initial language;
- `Paylaşım` and `Uyumlu - H.264` are clearly readable in the selectors;
- TR and EN controls show the active language through opacity;
- switching to English updates the SHRINK tab, status bar, buttons, labels, and selector values;
- the quality slider is present in CONVERT;
- SHRINK, CONVERT, and ABOUT layouts fit without overlapping controls;
- the signature/support block appears only once, at the bottom of ABOUT.

## Conversion-form spacing follow-up

The two-column conversion form originally had no gutter between adjacent fields and no vertical rhythm between rows. Labels and controls visually touched at the center seam.

The form now uses a 16 px horizontal gutter, 18 px row spacing, 6 px label-to-control spacing, and separate spacing between the CRF slider and its numeric field. The existing tab-level scroll viewer handles the additional height without overlap. The Turkish layout, which contains the longest labels, was visually verified at the application's minimum supported window size.

## Files central to review

- `src/VidShrink.App/LanguageCatalog.cs`
- `src/VidShrink.App/MainWindow.xaml`
- `src/VidShrink.App/MainWindow.xaml.cs`
- `src/VidShrink.App/App.xaml`
- `src/VidShrink.Core/ConversionPlan.cs`
- `src/VidShrink.Core/ConversionArguments.cs`
- `src/VidShrink.Core/FfmpegArguments.cs`
- `src/VidShrink.Ffmpeg/EncodeRunner.cs`
- `src/VidShrink.Ffmpeg/FfprobeClient.cs`
