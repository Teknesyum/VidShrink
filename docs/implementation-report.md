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

## Typography follow-up

Text glow was removed from neon-blue section headings. Typography now uses flat, sharp foreground colors for improved readability; neon emphasis remains on borders, selection states, and controls rather than blurred text shadows.

Normal interface typography now uses a 16 DIP minimum: labels, body copy, buttons, selectors, text fields, checkboxes, tabs, and mono values are all 16 DIP or larger. Section headings are 20 DIP. Only secondary hint and status copy may use the 14 DIP exception, and no application text style uses less than 14 DIP.

All neutral text tokens now use pure white `#FFFFFFFF`; the interface no longer uses gray typography. Neon blue headings, neon-purple actions, and neon-pink mono values remain semantic accent colors. The first maximum-legibility typography trial replaces Segoe UI with the bundled Atkinson Hyperlegible Next family for interface text. Commands and numeric values continue to use Consolas with Cascadia Mono as fallback.

Atkinson Hyperlegible Next was selected because its letterforms are intentionally differentiated for low-vision legibility, its expanded character set supports Turkish, and its SIL Open Font License permits bundling with the application. Regular, SemiBold, and Bold TTF files plus the license are embedded in the WPF project, so the result does not depend on a system font installation.

## Application icon

The selected compression-bracket and play-symbol prototype is now the product icon. Its outer canvas uses genuine PNG alpha transparency, while the symbol's intentional dark internal outlines remain intact. The same asset is embedded as a full-resolution PNG for the custom title panel and as a multi-resolution Windows ICO containing 16, 20, 24, 32, 40, 48, 64, 128, and 256 px layers for the executable and window identity.

## Title-case and contextual-help refinement

Short interface headings, tabs, labels, actions, options, and status names now capitalize the first letter of every word. Long explanations, help text, and paragraphs retain natural sentence casing. Technical abbreviations such as CRF, FPS, HDR, AI, TR/EN, codecs, and file formats retain their correct capitalization. The Turkish and English catalogs are updated together, and runtime startup checks guard against ambiguous Turkish reverse-translation keys.

Contextual-help badges were reduced from 22×22 to 16×16 DIP and positioned as superscript marks at the upper-right of their labels. Their hover delay is 120 ms and the tooltip remains available for up to 30 seconds, using the existing 16 DIP white-on-dark neon tooltip style.

Pink and purple are no longer persistent text colors. Section and field headings use neon blue, while values, secondary actions, and window controls use white. Pink remains an interaction accent for hover, focus, selection, borders, and slider details.

The desktop `VidShrink` shortcut targets `Launch-VidShrink.ps1` instead of a copied executable. The launcher uses `dotnet run` against the application project, so changed source is rebuilt when needed and the shortcut always opens the newest local project version.

The custom title bar keeps the mixed-case `VidShrink` brand name. The duplicate application name below the title bar was removed, leaving only the product description beside the language controls.

The Turkish product description is `Boyut Hedefli Media Sıkıştırma & Media Converter`; the English catalog counterpart is `Target Size Media Compression & Media Converter`.

Sliders now use a pink filled track with a blue outline and an inverse blue thumb with a pink outline. Hovering the control strengthens the thumb outline and adds a pink neon response without changing text colors.

The Output panel labels are explicitly neon blue. Its height is bound to the Target panel on the left so the paired panels retain equal height as content or language changes.

## August 2026 UI usability follow-up

The latest pass is aimed at users who do not already know codec or encoding terminology.

- CONVERT now contains a bilingual Quick Settings Guide. It explains H.264, H.265, VP9, AV1, stream copy, CRF, fixed bitrate, source/custom values, audio copy/drop, and trim times in plain language.
- Turkish remains the startup language; the guide and its headings switch with the existing TR/EN controls.
- The startup window requests 1440x1000 and is capped to the available Windows work area. On a 1440x1000 work area the SHRINK layout is visible without vertical scrolling.
- Field headings use neon purple, section headings use neon blue, and their child values or explanatory text remain pure white. This establishes a visible hierarchy without gray text or text glow.
- Sliders use a dark cyan rail, neon-cyan filled range, and a pink/cyan thumb. Progress bars use the matching neon rail and fill treatment.
- User-facing selector values and idle/tool statuses were normalized to initial capitals in both Turkish and English, including `Sosyal Medya`, `En Yüksek Sıkıştırma`, `Sabit Bit Hızı`, `Boşta`, and `FFmpeg: Hazır`.
- The Debug build completed with **0 warnings and 0 errors**. The Turkish SHRINK screen was visually inspected at 1425x993; controls were separated, the neon slider rendered correctly, text was readable, and no scroll was required.

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
