<!-- lang -->

[<img src="docs/gorseller/badge-lang.svg" alt="English selected, switch to Türkçe" width="124" height="44">](README.tr.md)

# VidShrink

**Shrink a video to an exact file size, record your screen, play it back and share it —
from one free, offline window.**

**Free forever · No ads · No account · No subscription · No telemetry · Works with the
internet off · 42 languages · 26 themes · Open source**

[![Latest release](https://img.shields.io/github/v/release/Teknesyum/VidShrink?label=release)](https://github.com/Teknesyum/VidShrink/releases/latest)
[![License AGPL-3.0-or-later](https://img.shields.io/badge/license-AGPL--3.0--or--later-blue)](LICENSE)
[![Windows, macOS, Linux](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)](#install)

![The VidShrink Shrink tab in English: the source drop zone on the left, the target size chips and slider below it, the What It Will Do panel in the middle spelling out codec, CRF, resolution and frame rate, and the Output panel with the size estimate on the right](docs/gorseller/T191-kucult-en.png)

## You Do Not Have To Know Any Of This

Drag a video in, tap a size, press start. That is the whole job, and the automatic mode
does the rest — it picks the codec, the quality level, the resolution and the frame rate
for **your** file, and it tells you the expected size before anything runs. You never get a
file larger than the number you asked for.

The screen recorder works the same way: one checkbox, and the program works out the
encoder, the frame rate and the capture size for your machine instead of asking you to.

**The whole window speaks 42 languages** — every button, every warning, every tooltip, from
Arabic to Vietnamese — and **26 colour themes** ship with it (Catppuccin, Dracula, Gruvbox,
Nord, Rose Pine, Solarized, Tokyo Night and twenty more, light and dark). Pick both in
Settings; nothing restarts. Your language missing, or a translation reading badly in yours?
[Open an issue](https://github.com/Teknesyum/VidShrink/issues/new) and it goes into the next
release — that is the whole process.

## Four Tools, One Window

**Shrink** — a target size in megabytes, and a video that lands just under it. Chips for
the sizes people actually need (8 for strict e-mail gateways, 16 for WhatsApp, 25 for Gmail, 180 for
WhatsApp Web) and a slider for everything else. Twelve encoders — software, NVENC, Quick
Sync, AMF — each [probed on your own machine](docs/olcumler/kodek-matris.md) first.

**Record** — the whole screen, a single window or a region, through the capture backend
each platform actually has: gdigrab on Windows, avfoundation on macOS, x11grab on Linux.
Microphone and system sound chosen by name, so a reordered device list cannot quietly swap
your microphone. Stopping closes the file properly, so a stopped recording plays back.

**Play** — the source plays in the window through a decoder pipe that
[stays open between seeks](docs/olcumler/oynatici-motor-libmpv.md) instead of launching
ffmpeg for every scrub, plus a comparison panel for before-and-after.

**Share and right-click** — "Open this video with VidShrink" sits in the Windows Explorer
menu, in the primary menu on Windows 11, [written per user](docs/olcumler/kabuk-menusu.md)
with no administrator rights and no change to your file associations. Share targets and
their real size ceilings live in [`paylasim-hedefleri.json`](paylasim-hedefleri.json).

Plus a Convert tab (MP4, MKV, WebM, MOV, AVI, GIF, MP3, M4A, WAV; H.264, H.265, VP9, AV1
or stream copy; trimming and audio extraction) and a hidden Advanced tab holding the exact
ffmpeg command. Full tour: [`docs/kullanim.md`](docs/kullanim.md).

## Every Tab

<details>
<summary>Screenshots of all six tabs, the preview and the hidden Advanced tab</summary>

**Player** — the source plays in the window; the comparison panel sits beside it.

![The Player tab: transport bar, volume and speed sliders with their numbers, and the comparison panel](docs/gorseller/T191-oynatici-en.png)

**Shrink** — source on the left, target size and quality in the middle, the estimate on the right.

![The Shrink tab: source facts, target size chips and slider, the plan panel and the output estimate](docs/gorseller/T191-kucult-en.png)

**Preview** — what the plan will actually produce, before it runs.

![The preview panel showing the planned output](docs/gorseller/T191-onizleme-en.png)

**Convert** — container, codec, trimming and audio extraction.

![The Convert tab: output format, codec, trim fields and audio extraction](docs/gorseller/T191-donustur-en.png)

**Recorder** — automatic mode ticked; the chosen encoder, frame rate and capture size are written under the checkbox.

![The Recorder tab in automatic mode with the reason line under the checkbox](docs/gorseller/T191-kaydedici-en.png)

**Settings** — language, theme, right-click menu, update behaviour.

![The Settings tab: language and theme pickers, right-click menu switch, update settings](docs/gorseller/T191-ayarlar-en.png)

**About** — version, licence, the projects VidShrink stands on.

![The About tab: version, licence and credits](docs/gorseller/T191-hakkinda-en.png)

**Advanced** (hidden until you unlock it) — the exact ffmpeg command that will run.

![The hidden Advanced tab showing the exact ffmpeg command line](docs/gorseller/T191-gelismis-en.png)

</details>

## Install

On Windows, download and run
[`VidShrink-Setup.exe`](https://github.com/Teknesyum/VidShrink/releases/latest/download/VidShrink-Setup.exe)
(published from 0.8.3 on). It is a small self-contained program: no PowerShell, no
administrator rights, and about half the time of the script below. The script still works
and installs exactly the same thing.

```powershell
# Windows
irm https://raw.githubusercontent.com/Teknesyum/VidShrink/main/Install-VidShrink.ps1 | iex
```

```bash
# macOS / Linux
curl -fsSL https://raw.githubusercontent.com/Teknesyum/VidShrink/main/install-vidshrink.sh | sh
```

No administrator rights, no .NET SDK. Every release publishes four targets — `win-x64`,
`osx-arm64`, `osx-x64`, `linux-x64` — from one version number. Requirements: Windows 10 or
11, macOS 14 or newer, or a Linux desktop on X11 or Wayland, plus `ffmpeg` and `ffprobe`.
FFmpeg and libmpv never travel in a release; the installer fetches them against pinned
SHA-256 digests on Windows and prints your package manager's command elsewhere. Checksum
verification, the right-click entry, the self-update flow and the uninstall switches are
all in [`docs/kurulum.md`](docs/kurulum.md).

## Command Line

The same package carries a headless CLI beside the app: `vidshrink` on Windows and Linux,
`vidshrink-cli` on macOS. It calls the same decision engine as the window, so the same
input gives the same ffmpeg arguments; a test holds the two against each other.

```bash
vidshrink kucult clip.mp4 --hedef 25MB              # shrink to a size
vidshrink kucult clip.mp4 --kalite 80 --kodek av1   # shrink to a quality score
vidshrink plan clip.mp4 --hedef 8MB --json          # plan and arguments only, no encode
```

Options: `--kodek auto|h264|hevc|av1`, `--cikti <path>`, `--json`, `--olcumsuz` (skip the
probe encodes), `--vmaf` (measure the result when ffmpeg has libvmaf), `--hizli`. Progress
goes to stderr; size, duration, attempts and VMAF go to stdout. Help follows the system
language, Turkish or English. `--dil en` (`--lang en`) overrides it for one run; the
known codes are `en` and `tr`, and an unknown code is a usage error rather than a silent
fall back to English.

`--kes <start>-<end>` trims before the encode, so the target size is spent on the part you
keep: `--kes 10-40`, `--kes 0:10-0:40`, `--kes 1:02:03-1:02:04`, or `--kes 90-` to the end.

The long options below each answer to an English alias; the two spellings are the same
option, and a script may use either. `--json` and `--vmaf` are the exceptions: they have a
single spelling.

| Turkish | English |
|---|---|
| `--yardim` | `--help` |
| `--surum` | `--version` |
| `--hedef` | `--target` |
| `--kalite` | `--quality` |
| `--kodek` | `--codec` |
| `--cikti` | `--output` |
| `--kes` | `--cut` |
| `--aralik` | `--interval` |
| `--bir-kez` | `--once` |
| `--olcumsuz` | `--no-measure` |
| `--hizli` | `--fast` |
| `--dil` | `--lang` |

Exit codes: `0` in band, `2` under the band (quality saturated, the smaller file kept), `3`
size ceiling exceeded (the smallest result is still written; JSON carries `output` and `overTarget: true`), `1` error, `64` wrong usage, `130` cancelled.

### Watch Folder

```bash
vidshrink izle ~/Gelen --cikti ~/Giden --hedef 25MB             # run until Ctrl+C
vidshrink izle ~/Gelen --cikti ~/Giden --hedef 25MB --bir-kez   # drain the folder, then exit
```

`izle` shrinks every video that lands in the folder. `--cikti` is the output folder and is
required; it cannot be the watched folder. `--aralik <seconds>` sets the scan interval
(default 2), `--bir-kez` exits once nothing is left to wait for, and the other `kucult`
options apply to each file. With `--json`, stdout is NDJSON: one compact JSON object per file.

A file is taken only when its size and modification time stay the same over two consecutive scan
intervals - the third scan takes it - and no writer holds it. If the source changes while it is being encoded, the output is
deleted and the file is picked up again once it settles.

Progress is kept in `.vidshrink-izle.json` inside the watched folder, by name and size; a
renamed or resized file counts as new. If that folder is read-only the state goes to the
output folder as `.vidshrink-izle-<hash>.json`, and failing that to the settings folder as
`izle-<hash>.json`. A file that failed is retried once on the next start. To process
everything again, delete the state file.

`<hash>` is the first 16 hex characters, lowercase, of the SHA-256 of the watched folder's
path. The path enters the hash under the same rule as the two comparisons below: upper-cased
first where the running system ignores case (Windows and macOS), taken as it stands on
Linux. So `/gelen` and `/Gelen` get one shared state file on Windows and macOS and two
separate ones on Linux, and the same folder always produces the same name.

Exactly two comparisons follow the rule of the running system: the watched folder against
the output folder, and a candidate against the output names this run has written. Those are
`Ordinal` on Linux and `OrdinalIgnoreCase` on Windows and macOS.

The macOS half of that rule assumes the default APFS volume, which is case-insensitive but
case-preserving. APFS can also be formatted case-sensitive, and on such a volume — or on a
case-sensitive external volume — the assumption does not hold.

Every other use of a file's name ignores case **on every platform, Linux included**: the
pending, retry and skip tables, the scan order, and the record of what has been processed.
So `Klip.mp4` and `klip.mp4` in one watched folder collide even where the filesystem keeps
them apart as two separate files.

A collision is resolved in the scan, not left to rot. Of the colliding names the ordinally
smallest wins and is shrunk as usual; the others are skipped with one warning line each,
`Skipped: klip.mp4 — its name collides with Klip.mp4 (letter case only). Rename one of
them.`, written once per file and not repeated on later scans. The winner is the same on
every scan, so the run is deterministic. Rename the skipped file and the watcher picks it
up as a new file.

Exit codes: `0` finished, `4` `--bir-kez` finished but at least one file failed or was
skipped for a name collision, `1` error, `64` wrong usage, `130` stopped with Ctrl+C. After
`--bir-kez` a summary line reports how many files were skipped.

## The Numbers

Nothing here is a lookup table. Every step that says *measure* runs ffmpeg against your
actual file first — short sample encodes at two resolutions and two CRFs, a scene map, then
a calibration pass that re-encodes the same windows four CRF steps apart and reads the
bit-cost curve off the two results.

What that buys, re-measured on today's engine over **36 cases** — three 1080p30 clips
(high detail, high motion, heavy noise) × three targets × a software and a hardware arm
× **two repeats each** — on an AMD Ryzen 7 9700X with an RTX 5070 Ti and ffmpeg 9.0-full
([`docs/olcumler/bench-2026-09-13.md`](docs/olcumler/bench-2026-09-13.md)):

| Target | Clip | Encoder | Delivered (run 1 / run 2) | Budget fill | Attempts |
|---|---|---|---|---|---|
| 100 MB | high detail | libx264 | 97.30 / 97.37 MB | 97.3% | 1 |
| 100 MB | heavy noise | h264_nvenc | 98.63 / 98.63 MB | 98.6% | 3 |
| 50 MB | high motion | h264_nvenc | 49.88 / 49.88 MB | 99.8% | 1 |
| 25 MB | high motion | libx264 | 24.24 / 24.26 MB | 97.0% | 1 |
| 25 MB | heavy noise | libsvtav1 | 24.28 / 24.28 MB | 97.1% | 1 |
| 8 MB | heavy noise | libsvtav1 | 7.67 / 7.67 MB | 95.9% | 1 |
| 8 MB | high motion | h264_nvenc | 7.71 / 7.71 MB | 96.3% | 1 |

The gates over all 36: **the target was never crossed — 0/36 over.** Calibration held
36/36. Two-pass was chosen 36/36; single-pass CRF never won. The hardware arm was
bit-identical across repeats — 0.000 MB of drift in 18/18 cases — and `libx264` drifted at
most 0.074 MB. And when more bits stop buying anything a viewer could see, the run stops
early: ask for 25 MB on an easy clip and you may get 9 MB that looks identical.

We publish where we lose, and there are two places.

**The AV1 branch undershoots.** 31 of 36 landed inside the fill band; **all five misses,
and the single hard-floor violation (6.68 MB against a 6.80 MB floor), were `libsvtav1`** —
`libx264` was 6/6 in band, `h264_nvenc` 18/18. The same branch is also where run-to-run
drift collects (up to 2.484 MB and 73.5 s), because the same input takes a different number
of correction rounds on different runs. Two repeats made that visible; one run would have
read as a stable number.

**HandBrake is still ahead on perceptual quality.** At an equal delivered size (±2%) on a
17-minute 1080p60 HDR source, HandBrake's x265 preset wins by **8.79 mean VMAF-NEG**,
**2.60 dB XPSNR** and **0.0299 SSIM**
([`docs/olcumler/handbrake-acigi.md`](docs/olcumler/handbrake-acigi.md)) — psy-rd, psy-rdoq
and adaptive quantisation, which our arguments do not carry yet. Closing that is the first
item on the roadmap.

## How We Measure

The rig took longer to build than the feature it judges, because a rig that cannot tell two
encodes apart prints numbers forever and never says it is wrong.

<details>
<summary>The six things the rig does so a number can be trusted</summary>

- **A colour gate that refuses to answer.** Every output's colour space, transfer,
  primaries and pixel format are read with ffprobe and compared to the reference. Untagged
  output, PQ against HLG, an SDR reference against an HDR result: the tool prints *no
  number at all* rather than a plausible one. An earlier, ungated table was thrown out for
  exactly this — it returned a near-constant XPSNR while the target size moved tenfold.
- **Three measures, and VMAF as four numbers.** **VMAF-NEG**, **XPSNR** and **SSIM**, with
  VMAF as mean, harmonic mean, 10th percentile and frame minimum. The average hides the
  frames that actually look bad, and those are the frames a viewer notices.
- **A frame lock**, so both sides are scored on the same frames. Numbers measured before it
  landed are stamped as suspect in their own documents and are not quoted here.
- **Verified cuts.** A 17-minute source is measured in three 60-second parts cut on real
  keyframes, each start verified against the source by frame hash — all three landed 0.4 s
  before the requested second, as a `-c copy` cut should.
- **A known defect, published.** The rig locks the two streams by frame index, so when the
  plan lowers the frame rate it compares different moments and exaggerates the loss — same
  file, `fps=30` resampling alone moved XPSNR from **1.70 dB to 21.14 dB**. Rows with no
  frame-rate drop are unaffected (34.5557 against 34.56). Findings that rested on the broken
  path are marked *unfounded* in their own documents rather than quietly restated.
- **A sensitivity proof.** The rig must separate a 60 MB encode from a 600 MB one by at
  least 1.00 VMAF-NEG point or it marks itself insensitive. Measured: **+39.26** for
  HandBrake, **+39.85** for VidShrink — forty times the threshold.

</details>

Full rig: [`docs/olcumler/ab-duzenegi.md`](docs/olcumler/ab-duzenegi.md). The recorder's
automatic mode is measured the same way — a candidate ladder built from the machine, then
[three real seconds of recording per candidate](src/VidShrink.Ffmpeg/RecorderAutoProbe.cs)
and a dropped-frame count, re-measured against the current engine in
[`docs/olcumler/auto-mod-yeni-taban.md`](docs/olcumler/auto-mod-yeni-taban.md). Over a
hundred such documents live in [`docs/olcumler/`](docs/olcumler/); every number here comes
from one of them.

## Under The Hood

<details>
<summary>What runs between dropping a file in and getting one out</summary>

```mermaid
flowchart LR
    A["Source + target size"] --> B["ffprobe reads the file"]
    B --> C["ComplexityProbe: sample encodes<br/>at two resolutions, two CRFs"]
    C --> D["SceneDetector builds a scene map"]
    D --> E["CalibrationProbe re-encodes the same<br/>windows four CRF steps apart"]
    E --> F["PlanCalculator settles codec,<br/>CRF, resolution, frame rate"]
    F --> G["Shown to you with the size estimate"]
    G --> H["Encode: two-pass on software,<br/>single VBR on hardware"]
    H --> I{"Under the target?"}
    I -->|yes| J["Deliver"]
    I -->|no| K["Stop, show the overshoot,<br/>ask before a second attempt"]
```

```mermaid
flowchart LR
    A["Automatic mode ticked"] --> B["Candidate ladder built<br/>from this machine"]
    B --> C["Record three real seconds<br/>per candidate"]
    C --> D["Read ffmpeg's dropped-frame counter"]
    D --> E{"Zero dropped frames?"}
    E -->|yes| F["Wins outright"]
    E -->|"no, none of them"| G["Lowest drop ratio wins"]
    F --> H["Settings and the reason for each<br/>written under the checkbox"]
    G --> H
```

</details>

Long version — calibration, the stopping rule, the four compression regimes, HDR handling,
perceptual scoring, today's limits: [`docs/motor.md`](docs/motor.md).

## Documentation

[Using VidShrink](docs/kullanim.md) · [The engine](docs/motor.md) ·
[Install and update](docs/kurulum.md) · [Measurements](docs/olcumler/) ·
[Roadmap](docs/YOL-HARITASI.md) · [Changelog](CHANGELOG.md) · [Contributing](CONTRIBUTING.md)

## Roadmap

Measured, open, in this order — detail in [`docs/YOL-HARITASI.md`](docs/YOL-HARITASI.md).

- **Beating HandBrake on perceptual quality too.** We already win on hitting a target size;
  the 8.79 VMAF-NEG gap above is psy-rd, psy-rdoq and adaptive quantisation, which our
  arguments do not carry yet. The bar is the same rig, the same source, the gap at zero and
  then on our side — not a claim, a measurement.
- **The AV1 branch's undershoot** — five band misses out of five are `libsvtav1`, and the
  correction rounds do not close them.
- **Time-aligning the measurement rig**, so frame-rate-lowering plans can be scored at all.
- **Opening the peak-rate ceiling**, which also fixes the hardware overshoot at small targets.
- **Calibrating the scaling and frame-rate penalties against measured quality**, replacing
  the fixed constants the planner uses today.
- **Encoding by scene instead of by clip** — the largest structural gain left.

## Contributing

Open an issue first, keep a pull request to one concern, run `dotnet test VidShrink.sln` —
nothing merges red — and sign every commit off under the
[Developer Certificate of Origin](DCO) with `git commit -s`. Build instructions, project
layout and design rules: [`CONTRIBUTING.md`](CONTRIBUTING.md).

## License

[AGPL-3.0-or-later](LICENSE). Copyright (C) 2026 Teknesyum.

FFmpeg and libmpv are separate programs under their own licenses; VidShrink redistributes
neither and links no GPL code in ([`docs/kurulum.md`](docs/kurulum.md)).

Third-party material that ships inside the source tree — the Fluent UI System Icons (MIT)
the icon set is cut from — is listed in
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

<!-- signature -->
<div align="center">

<a href="https://github.com/sponsors/Teknesyum"><img src="docs/gorseller/badge-sponsor.svg" alt="Support Teknesyum" height="38"></a>
&nbsp;
<a href="LICENSE"><img src="docs/gorseller/badge-license.svg" alt="License AGPL-3.0" height="38"></a>

</div>
