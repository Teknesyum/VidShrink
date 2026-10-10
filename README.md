<!-- lang -->

[<img src="docs/gorseller/badge-lang.svg" alt="English selected, switch to Türkçe" width="124" height="44">](README.tr.md)

# VidShrink

**Free, open-source video player built on libmpv, with a target-size compressor, a format
converter, a screen recorder and a timeline editor in the same window — offline, on
Windows, macOS and Linux.**

**42 languages · 36 themes · 0 of 36 measured cases went over the target size · No ads · No
watermark · No account · No subscription · No telemetry**

[![Latest release](https://img.shields.io/github/v/release/Teknesyum/VidShrink?label=release)](https://github.com/Teknesyum/VidShrink/releases/latest)
[![License AGPL-3.0-or-later](https://img.shields.io/badge/license-AGPL--3.0--or--later-blue)](LICENSE)
[![Windows, macOS, Linux](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)](#install)

<a href="docs/gorseller/T201-oynatici-en.png"><img src="docs/gorseller/T201-oynatici-en.png" alt="The VidShrink Player tab in English: a video filling the window, the tab bar across the top with Player, Editor, Shrink, Convert, Recorder and Settings, and the control strip along the bottom with the time, volume, ten-second jumps, play and pause, speed, clip and full screen" width="800"></a>

It started as a tool to compress a video to a target file size. Today it is a media player
first: the mpv engine with subtitles, playlists and network streams, behind a window you
drive with the mouse.

The other tabs take over when a video needs work. Shrink it under an upload limit, convert
it, record the screen, or cut it on a timeline. Nothing leaves your machine unless you
ask — see [Privacy](#code-signing-policy).

## Install

No administrator rights, no .NET SDK. Windows 10 or 11, macOS 14 or newer, or a Linux
desktop on X11 or Wayland.

### Install On Windows

Three ways, in this order.

1. **Teknesyum Base — the recommended way.** Download and run
   [`Teknesyum-Base.exe`](https://github.com/Teknesyum/Teknesyum-Base/releases/latest/download/Teknesyum-Base.exe)
   ([`.sha256`](https://github.com/Teknesyum/Teknesyum-Base/releases/latest/download/Teknesyum-Base.exe.sha256)),
   find **VidShrink** in the list and install it. Base also updates and uninstalls it later.
   Base is not signed yet, so Windows SmartScreen may warn on first run: choose *More info*,
   then *Run anyway*. Details: [Teknesyum Base](https://github.com/Teknesyum/Teknesyum-Base).
2. **VidShrink-Setup.exe.** Download and run
   [`VidShrink-Setup.exe`](https://github.com/Teknesyum/VidShrink/releases/latest/download/VidShrink-Setup.exe).
   It is a small self-contained program: no PowerShell, no WinGet.
3. **One line of code.** Paste it into PowerShell or the Command Prompt. It downloads the
   latest `VidShrink-Setup.exe` to the temp folder and runs it — the same installer as way 2.

   ```powershell
   powershell -NoProfile -Command "[Net.ServicePointManager]::SecurityProtocol=3072; Set-Variable ProgressPreference SilentlyContinue; Set-Location ([IO.Path]::GetTempPath()); irm https://github.com/Teknesyum/VidShrink/releases/latest/download/VidShrink-Setup.exe -OutFile VidShrink-Setup.exe; .\VidShrink-Setup.exe"
   ```

Until releases are signed, a PC with Smart App Control turned on blocks VidShrink whichever
way it is installed.

**Uninstall.** `VidShrink-Setup.exe --uninstall` removes the shortcuts, the right-click
entries, the Open With registration and the install folder.

### Install On macOS And Linux

```bash
curl -fsSL https://raw.githubusercontent.com/Teknesyum/VidShrink/main/install-vidshrink.sh | sh
```

To uninstall, run the same installer with `--uninstall`:

```bash
curl -fsSL https://raw.githubusercontent.com/Teknesyum/VidShrink/main/install-vidshrink.sh | sh -s -- --uninstall
```

<details>
<summary>Requirements, what the installer downloads, and the PowerShell script</summary>

Every release builds four targets from one version number: `win-x64`, `osx-arm64`,
`osx-x64`, `linux-x64`. Besides the system above you need `ffmpeg` and `ffprobe`. FFmpeg and
libmpv never travel in a release; the installer downloads them against pinned SHA-256
digests on Windows and prints your package manager's command elsewhere. Windows releases are
signed under the [Code Signing Policy](#code-signing-policy) once the certificate is issued.

On Windows the `Install-VidShrink.ps1` script does the same install, and downloads FFmpeg
from the pinned archive when WinGet is missing:

```powershell
powershell -NoProfile -Command "[Net.ServicePointManager]::SecurityProtocol=3072; iex (irm https://raw.githubusercontent.com/Teknesyum/VidShrink/main/Install-VidShrink.ps1)"
```

Checksum verification, the right-click entry, the self-update flow and every switch are in
[`docs/kurulum.md`](docs/kurulum.md).

</details>

## Video Player With Subtitles, Playlists And Network Streams

The Player tab is built on **libmpv**, the engine inside mpv. It opens MKV, MP4, WebM, AVI,
MOV, TS and the rest, and plays music files with a cover card.

Open a file by dragging it in, from the Explorer right-click menu on Windows, or from the
command line. Playback resumes where you stopped, for the last five videos you watched.

- **Subtitles** — sidecar files load by themselves and a dropped subtitle file just works.
  Shift the timing in steps, show a second subtitle beside the first, and set the font,
  colour, outline, shadow and background. **OpenSubtitles search and download** sit in the
  window, with your own account.
- **Sound** — audio track and audio delay, a 10-band equaliser with ready presets, loudness
  levelling, and volume up to 200% when you allow it.
- **Picture** — brightness, contrast, saturation, gamma, hue, sharpness, deinterlace, crop,
  rotate, mirror, aspect ratio and zoom. Hardware decoding is a switch.
- **Seeking** — jumps of 1, 10, 60 and 300 seconds, frame-by-frame steps, chapters,
  bookmarks, an A-B loop, and a thumbnail that follows the pointer along the seek bar.
- **Speed** — 0.25× to 4× in steps of 0.05, and one key that flips between two speeds you
  set.
- **Playlists** — opens M3U, PLS, WPL and ASX, saves the queue as M3U8, shuffles and
  repeats. Next and previous file in the folder, recent files, and the keyboard's media
  keys.
- **Network streams** — paste, drag or type an address: http, https, rtsp, rtmp, srt or
  udp. It plays a direct media address; it does not resolve a page link such as a
  YouTube URL.
- **Capture** — a screenshot as PNG or JPG, the current frame to the clipboard, and a clip
  or a GIF cut straight from what is playing.
- **Information** — a panel with the codec, bit depth and SDR, HDR10, HLG or Dolby Vision,
  plus live figures such as dropped frames.
- **Window** — full screen, mini mode, always on top, and a control strip that hides
  itself. Keyboard shortcuts can be reassigned.

One key sends the playing file to the Editor at the same position.

## Compress A Video To A Target File Size

<table>
<tr>
<td><a href="docs/gorseller/T201-kucult-onizleme-en.png"><img src="docs/gorseller/T201-kucult-onizleme-en.png" alt="The Shrink tab with sunum-prototip.mp4 loaded and a 0.15 MB target: source details and the target and quality sliders on the left, the comparison panel in the middle with the source on the Original side of the blue split line and the planned output on the Processed side at CRF 43, the What It Will Do panel below it spelling out libsvtav1, 72 kbit/s two-pass, 666x374 and 30 FPS, and the Output panel on the right predicting quality 65.1/100" width="400"></a></td>
<td><a href="docs/gorseller/T201-kucult-yakin-en.png"><img src="docs/gorseller/T201-kucult-yakin-en.png" alt="The same Shrink tab with the comparison panel zoomed in to 196% inside the app: left of the split line the source keeps the edges of the book spines, right of it the 0.15 MB output lets them go soft" width="400"></a></td>
</tr>
</table>

Drag a video in, tap a size, press start. The automatic mode picks the codec, the quality
level, the resolution and the frame rate for **your** file and tells you the expected size
before anything runs. Over 36 measured cases the target was crossed **zero** times.

- **Sizes** — chips for 8 MB (strict e-mail gateways), 16 (WhatsApp), 25 (Gmail) and 180
  (WhatsApp Web), and a slider for everything else. On Windows the Explorer right-click
  menu has "Shrink with VidShrink" with six sizes: 8, 16, 20, 25, 50 and 100 MB.
- **Before and after** — a split panel shows the source beside the planned output, with
  zoom, before you commit to the encode.
- **Encoders** — software, plus NVENC, Quick Sync and AMF, each probed on your own machine
  first. VideoToolbox on macOS and VAAPI on Linux can be chosen with the codec lock; they
  are not in the automatic order.
- **Quality score** — the result is scored with **VMAF-NEG**: mean, harmonic mean, 10th
  percentile and worst frame.
- **Batch** — a whole folder goes through the queue, which opens the folder, sleeps or
  shuts the computer down when it finishes.

## Convert Video And Audio Formats

<table>
<tr>
<td><a href="docs/gorseller/T201-donustur-en.png"><img src="docs/gorseller/T201-donustur-en.png" alt="The Convert tab with sunum-prototip.mp4 loaded: container, codec, quality mode, resolution, frame rate and trim fields, and the FFmpeg command and progress panels beside them" width="400"></a></td>
<td><a href="docs/gorseller/T201-gelismis-en.png"><img src="docs/gorseller/T201-gelismis-en.png" alt="The hidden Advanced tab, holding the FFmpeg command box that shows the exact command that will run, the AI settings box and the Performance Check box" width="400"></a></td>
</tr>
</table>

MP4, MKV, WebM, MOV, MXF, AVI, GIF, animated WebP and animated AVIF; MP3, M4A, WAV and FLAC
for sound alone. H.264, H.265, VP9, AV1 or a straight stream copy, trimming, and audio
extraction. For editing there are the intermediate codecs: ProRes (Proxy, LT, 422, HQ, 4444)
into MOV, and DNxHR (LB, SQ, HQ, HQX, 444) into MOV or MXF
([`k12-ara-kodekler.md`](docs/olcumler/k12-ara-kodekler.md)). **Eighteen ready-made targets** — WhatsApp, Discord, Telegram, Gmail, Outlook,
Chromecast, Nest Hub, Apple TV and more — set every field for you, and the exact FFmpeg
command that will run is on screen.

## Record Your Screen

<a href="docs/gorseller/T201-kaydedici-en.png"><img src="docs/gorseller/T201-kaydedici-en.png" alt="The Recorder tab in the Advanced layout with the program picking the settings: the source, screen and countdown pickers, the approximate length and size fields with Measure Again, the microphone and system sound pickers, and the Advanced Encoding, Webcam and Replay Buffer panels" width="800"></a>

- **What** — the whole screen, one window or a region you draw. Windows captures with
  ddagrab at up to 60 fps and falls back to gdigrab with a notice; macOS uses avfoundation,
  Linux x11grab. At a 60 fps request ddagrab delivered 59.8 fps where gdigrab gave 24.2 and
  44.8 ([`kaydedici-ddagrab.md`](docs/olcumler/kaydedici-ddagrab.md)).
- **Sound** — microphone and system sound chosen by name, so a reordered device list cannot
  quietly swap your microphone; gain, a noise gate and noise suppression.
- **Webcam** — an overlay with its own size and corner, and a green-screen option.
- **For tutorials** — cursor, click rings and click sounds, the keys you press on screen, a
  magnifier, and a live preview.
- **Control** — global hotkeys F7 to F11 on Windows, with F6 dropping a chapter mark; a
  countdown, a time limit, splitting by time or size, a replay buffer that keeps the last
  moments, tray and mini-recorder modes.
- **Encoders** — x264, x265, SVT-AV1 and VP9, plus NVIDIA NVENC, Intel Quick Sync and AMD
  AMF. **Automatic mode** records three real seconds per candidate on your machine, reads
  ffmpeg's dropped-frame counter and keeps the one that drops none.
- **Output** — MP4, MKV, MOV or GIF, a target size or length budget if you want one, and a
  file that is closed properly when you stop. One click sends it on to the Editor, the
  Player or Share.

## Trim And Edit Video

<a href="docs/gorseller/T201-duzenleyici-en.png"><img src="docs/gorseller/T201-duzenleyici-en.png" alt="The Editor tab: the video on top, and below it the toolbar with Split, Delete, Speed, Undo, Redo, zoom, Save, Save As and Share the File, over a timeline split into three clips" width="800"></a>

Open a video from any tab and cut it on a timeline: split, delete a clip or a range, move
clips, set the speed of each clip anywhere from 0.01× to 100×, play it in reverse, undo and
redo. Then **Save**, **Save As** or **Share** in one of three export modes:

- **Fast** — cuts on keyframes and copies the streams; no re-encode, no quality lost.
- **Smart** — copies what it can and re-encodes only what the cuts need.
- **Full** — re-encodes the whole result.

## Command Line

The same package carries a headless CLI beside the app: `vidshrink` on Windows and Linux,
`vidshrink-cli` on macOS. It calls the same decision engine as the window, so the same
input gives the same ffmpeg arguments; a test holds the two against each other.

```bash
vidshrink kucult clip.mp4 --hedef 25MB              # shrink to a size
vidshrink kucult clip.mp4 --kalite 80 --kodek av1   # shrink to a quality score
vidshrink plan clip.mp4 --hedef 8MB --json          # plan and arguments only, no encode
```

Every long option also answers to an English alias (`--hedef` is `--target`, `--kodek` is
`--codec`). Every option, the full alias table and the exit codes:
[`docs/cli.md`](docs/cli.md).

### Watch Folder

```bash
vidshrink izle ~/Gelen --cikti ~/Giden --hedef 25MB             # run until Ctrl+C
vidshrink izle ~/Gelen --cikti ~/Giden --hedef 25MB --bir-kez   # drain the folder, then exit
```

`izle` shrinks every video that lands in the folder. `--cikti` is the output folder and is
required; it cannot be the watched folder. When a file is taken, where progress is kept,
letter case and exit codes: [`docs/cli.md`](docs/cli.md#watch-folder).

## Share A Large File As A Link

Press **Share** in Shrink, the Recorder or the Editor and the file goes up as a link:
**storage.to** for files up to 25 GB, kept one to seven days, or **uguu.se** for files up to
128 MB, kept three hours. The link comes with a QR code for your phone, and a dropped
upload can be retried. Share targets and their measured size ceilings live in
[`paylasim-hedefleri.json`](paylasim-hedefleri.json).

## How It Compares To mpv, HandBrake, OBS And LosslessCut

Each of these does its one job with more depth than this app does. The rows are from a
written survey of the versions named ([`tarama-2026-10-05.md`](docs/piyasa/tarama-2026-10-05.md)).

| Job | Here | Elsewhere |
|---|---|---|
| Playback | libmpv with the controls above | mpv 0.41.0 adds HDR tone mapping, shaders, interpolation, scripts and yt-dlp |
| Target size in MB | Yes; never crossed in 36 measured cases | HandBrake 1.11.2 has no target-size field. Shutter Encoder 20.4 and FFmpeg Batch have one; Shutter says it does not guarantee the size |
| Preview before encoding | Split before/after panel | HandBrake encodes a sample to preview |
| Quality at equal bytes | Ahead of HandBrake x265 `slow` on 8 of 8 SDR rows and 3 of 4 HDR10 rows | HandBrake bands less in dark scenes |
| Encoding speed | Slower on 8 of 8 rows; total time 1.64 to 3.73 times HandBrake's | HandBrake |
| Hardware encoders | NVENC, Quick Sync, AMF; VideoToolbox (macOS) and VAAPI (Linux) by codec lock only, and VAAPI has not yet encoded on real hardware here | HandBrake offers VideoToolbox and VAAPI in its normal encoder list |
| Several encodes at once | No, one job at a time | HandBrake, FFmpeg Batch |
| Screen recording | Screen, window, region, webcam | OBS 32.2.2 adds game capture, real desktop-audio capture, scenes and streaming |
| Cutting without re-encoding | Fast and Smart export | LosslessCut 3.69.0 adds merge, track management and EDL/CSV; it calls its own smart cut experimental |

<sub>Product names belong to their owners; VidShrink is not affiliated with any of them.</sub>

## What It Does Not Do

- The player has no HDR tone mapping controls, shaders, frame interpolation or scripts.
- No downloading from a URL. The player opens a direct media address, and does not resolve
  page links; there is no yt-dlp.
- No casting to Chromecast from the player, and no automatic captions.
- No stabilisation, LUTs or watermarking, and no FFV1 output. ProRes and DNxHR are in the
  Convert tab only; they cannot be driven to a target size.
- No file merging or image sequences.
- No live streaming, scenes or game capture. Window capture crops the window's rectangle
  out of the screen, and system sound is found by device name, not by a real loopback.
- No screenshots tool, scrolling capture, OCR or annotation.
- On macOS and Linux the recorder captures, but has no webcam overlay, global hotkeys or
  click and key display.

## Measured Results

Over 36 measured cases the target size was never crossed — **0/36 over** — and the two places
where we lose are published beside it.

<details>
<summary>The table, the gates, where we lose and how the rig is kept honest</summary>

There is no lookup table. Before a decision is made ffmpeg runs against your actual file:
short sample encodes at two resolutions and two CRFs, a scene map, then a calibration pass
that re-encodes the same windows four CRF steps apart and reads the bit-cost curve off the
two results. Software encoders then run two passes, hardware a single VBR pass, and an
overshoot stops and asks before a second attempt.

The 36 cases are three 1080p30 clips (high detail, high motion, heavy noise) × three targets
× software and hardware arms × **two repeats**, on an AMD Ryzen 7 9700X, an RTX 5070 Ti and
ffmpeg 9.0-full ([`docs/olcumler/bench-2026-09-13.md`](docs/olcumler/bench-2026-09-13.md)):

| Target | Clip | Encoder | Landed (run 1 / run 2) | Budget fill | Attempts |
|---|---|---|---|---|---|
| 100 MB | high detail | libx264 | 97.30 / 97.37 MB | 97.3% | 1 |
| 100 MB | heavy noise | h264_nvenc | 98.63 / 98.63 MB | 98.6% | 3 |
| 50 MB | high motion | h264_nvenc | 49.88 / 49.88 MB | 99.8% | 1 |
| 25 MB | high motion | libx264 | 24.24 / 24.26 MB | 97.0% | 1 |
| 25 MB | heavy noise | libsvtav1 | 24.28 / 24.28 MB | 97.1% | 1 |
| 8 MB | heavy noise | libsvtav1 | 7.67 / 7.67 MB | 95.9% | 1 |
| 8 MB | high motion | h264_nvenc | 7.71 / 7.71 MB | 96.3% | 1 |

Calibration held in 36/36 and two-pass was picked in 36/36. The hardware arm is bit-identical
across repeats — 0.000 MB drift in 18/18 — and `libx264` drifted at most 0.074 MB.

**The AV1 arm falls short of the target.** 31 of the 36 cases stayed in band; **all five
misses, and the one hard floor breach (6.68 MB against a 6.80 MB floor), are `libsvtav1`** —
`libx264` is 6/6 and `h264_nvenc` 18/18 in band. Repeat-to-repeat drift sits there too, up
to 2.484 MB and 73.5 seconds.

**HandBrake: ahead at equal size, behind on dark banding.** Against HandBrakeCLI 1.11.2's
x265 `slow` preset at equal delivered bytes we lead on 8 of 8 SDR rows
([`handbrake-kiyas-b1-sdr.md`](docs/olcumler/handbrake-kiyas-b1-sdr.md)) and 3 of 4 HDR10
rows ([`handbrake-kiyas-b4-hdr.md`](docs/olcumler/handbrake-kiyas-b4-hdr.md)). Dark scenes
still band more: our AV1 output scores CAMBI 9.31 and 9.39 where HandBrake scores 6.48 and
6.49, lower being better
([`handbrake-kiyas-b7-aciklar.md`](docs/olcumler/handbrake-kiyas-b7-aciklar.md)). We are
also slower: total time 1.64 to 3.73 times HandBrake's on 8 of 8 rows
([`handbrake-kiyas-cli.md`](docs/olcumler/handbrake-kiyas-cli.md)).

**The rig** ([`docs/olcumler/ab-duzenegi.md`](docs/olcumler/ab-duzenegi.md)) prints no
number when the colour space, transfer, primaries or pixel format of an output does not
match the reference. It scores **VMAF-NEG**, **XPSNR** and **SSIM** over frame-locked
streams, on three 60-second parts of a 17-minute source cut on real keyframes, each landing
0.4 s before the requested second. It has to tell a 60 MB encode from a 600 MB one by at
least 1.00 VMAF-NEG point; measured, **+39.26** for HandBrake and **+39.85** for VidShrink.
One known flaw is published as it stands: when a plan lowers the frame rate the rig compares
different moments — an `fps=30` resample alone moved XPSNR **from 1.70 dB to 21.14 dB** —
and verdicts that rested on that path are marked *unfounded* in their own documents.

The recorder's automatic mode is measured the same way:
[three seconds of real recording per candidate](src/VidShrink.Ffmpeg/RecorderAutoProbe.cs),
re-measured in [`auto-mod-yeni-taban.md`](docs/olcumler/auto-mod-yeni-taban.md).

</details>

The long version — calibration, the stopping rule, HDR, perceptual scoring, current limits:
[`docs/motor.md`](docs/motor.md). Every number here comes from a document under
[`docs/olcumler/`](docs/olcumler/).

## 42 Languages And 36 Themes

<a href="docs/gorseller/T201-ayarlar-en.png"><img src="docs/gorseller/T201-ayarlar-en.png" alt="The Settings tab: language and theme pickers, the right-click menu entries, the automatic update switch, and the output, subtitle and share settings" width="800"></a>

The whole window speaks 42 languages, from Arabic to Vietnamese, and ships 36 colour
themes — Catppuccin, Dracula, Gruvbox, Nord, Rose Pine, Solarized, Tokyo Night and
twenty-nine more, light and dark. Pick both in Settings; nothing restarts. A language
missing, or a translation reading badly?
[Open an issue](https://github.com/Teknesyum/VidShrink/issues/new).

**What it changes on your system, and nothing more.** On Windows the installer adds Start
menu and desktop shortcuts, an "Open this video with VidShrink" entry in the Explorer
right-click menu (in the primary menu on Windows 11), and VidShrink in the **Open with**
list for video files — [written per user](docs/olcumler/kabuk-menusu.md), no administrator
rights, and your default player stays your default. The app checks GitHub for a new release
and offers the update with two buttons, Update and Later; that check is on by default on
Windows and switches off in Settings. All of it comes off again with one command — see
[Install](#install).

## Build From Source

Building from a clone needs the .NET 8 SDK, plus `ffmpeg`, `ffprobe` and libmpv on the
machine to run what you built.

```bash
dotnet build VidShrink.sln -c Release
dotnet test VidShrink.sln
```

The project layout and the design rules are in [`CONTRIBUTING.md`](CONTRIBUTING.md).

## Documentation

[Usage](docs/kullanim.md) · [Command line](docs/cli.md) · [Engine](docs/motor.md) ·
[Install and update](docs/kurulum.md) · [Measurements](docs/olcumler/) ·
[Roadmap](docs/YOL-HARITASI.md) · [Changelog](CHANGELOG.md) · [Contributing](CONTRIBUTING.md)

## Roadmap

Measured, open, in this order — the detail is in [`docs/YOL-HARITASI.md`](docs/YOL-HARITASI.md).

- **Close the last gap to HandBrake: banding in dark scenes** (CAMBI 9.31 and 9.39 against
  6.48 and 6.49). The bar is the same rig, the same source, and that gap at zero.
- **The AV1 arm's undershoot** — all five band misses are `libsvtav1`.
- **Time-align the measurement rig**, so plans that lower the frame rate can be measured.
- **Open the peak-rate cap**, which also fixes hardware overshoot at small targets.
- **Calibrate the scale and frame-rate penalties against measured quality**.
- **Per-scene encoding** — measured, and not shipped: it did not pass the quality gate
  ([`sahne-butcesi.md`](docs/olcumler/sahne-butcesi.md)).

## Contributing

Open an issue first, keep a pull request to one concern, run `dotnet test VidShrink.sln` —
nothing merges red — and sign off each commit under the
[Developer Certificate of Origin](DCO) with `git commit -s`. Build instructions, the project
layout and the design rules are in [`CONTRIBUTING.md`](CONTRIBUTING.md).

## License

[AGPL-3.0-or-later](LICENSE). Copyright (C) 2026 Teknesyum.

FFmpeg and libmpv are separate programs under their own licences; VidShrink redistributes
neither and links no GPL code into itself ([`docs/kurulum.md`](docs/kurulum.md)).

Third-party material distributed inside the source tree — the Fluent UI System Icons the
icon set is cut from (MIT) — is listed in [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

## Code Signing Policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by
[SignPath Foundation](https://signpath.org). The application is pending; until it is
approved, Windows releases ship unsigned.

**Privacy.** This program will not transfer any information to other networked systems
unless specifically requested by the user or the person installing or operating it.

<details>
<summary>Who approves a signature, and every request the app can make</summary>

- Committers, reviewers and approvers: [Teknesyum](https://github.com/Teknesyum)

Only artifacts built from this repository by GitHub Actions are signed, and every signing
request is approved by hand. Everyone on the team signs in with multi-factor authentication.

The requests it can make, what triggers each, and whose privacy policy applies:

- **Update check** — asks GitHub for the latest release. On by default on Windows; switch
  it off in Settings. [GitHub Privacy Statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement)
- **Installer** — downloads FFmpeg and libmpv from GitHub releases, pinned by SHA-256.
  [GitHub Privacy Statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement)
- **Share** — uploads a file only when you press Share, to the host you pick.
  [storage.to privacy](https://storage.to/privacy) · [uguu.se FAQ](https://uguu.se/faq)
- **OpenSubtitles** — signs in and searches only when you use it.
  [OpenSubtitles privacy policy](https://www.opensubtitles.com/en/privacy/)
- **Network streams** — the player connects to an address only when you open one.

</details>

<!-- signature -->
<div align="center">

<a href="https://github.com/sponsors/Teknesyum"><img src="docs/gorseller/badge-sponsor.svg" alt="Support Teknesyum" height="38"></a>
&nbsp;
<a href="LICENSE"><img src="docs/gorseller/badge-license.svg" alt="License AGPL-3.0" height="38"></a>

</div>
