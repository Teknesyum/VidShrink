# VidShrink Command Line

The long version of the *Command Line* section of the [README](../README.md).
[Türkçe](cli.tr.md)

The same package carries a headless CLI beside the app: `vidshrink` on Windows and Linux,
`vidshrink-cli` on macOS. It calls the same decision engine as the window, so the same
input gives the same ffmpeg arguments; a test holds the two against each other.

```bash
vidshrink kucult clip.mp4 --hedef 25MB              # shrink to a size
vidshrink kucult clip.mp4 --kalite 80 --kodek av1   # shrink to a quality score
vidshrink plan clip.mp4 --hedef 8MB --json          # plan and arguments only, no encode
```

## Options, English Aliases And Exit Codes

Options: `--kodek auto|h264|hevc|av1`, `--cikti <path>`, `--json`, `--olcumsuz` (skip the
probe encodes), `--vmaf` (measure the result when ffmpeg has libvmaf), `--hizli`. Progress
goes to stderr; size, duration, attempts and VMAF go to stdout. Help follows the system
language, Turkish or English. `--dil en` (`--lang en`) overrides it for one run; the
known codes are `en` and `tr`, and an unknown code is a usage error rather than a silent
fall back to English.

`--crf N` and `--on-ayar NAME` lock what the Advanced panel locks in the window: the
quality value (0-63) and the encoder preset. The preset name has to belong to the codec
the plan picks — `slow` for x264/x265, `p5` for NVENC, `8` for SVT-AV1 — and a name that
does not belong is dropped with a line in the plan reason, not an error.

`--modul N` (`--modulus N`) rounds the scaled edges down to a multiple of 2, 4, 8 or 16.
The default is 2, because an encoder cannot take an odd edge; a larger modulus is what old
hardware encoders want and crops the edge a little more. Any other number is a usage error.

An anamorphic source — a DVD whose stored pixels are not square — is de-anamorphosed
before scaling: the height is kept, the width becomes the width the frame is displayed
at, and the output carries square pixels. Nothing changes for a square-pixel source.

`--kes <start>-<end>` trims before the encode, so the target size is spent on the part you
keep: `--kes 10-40`, `--kes 0:10-0:40`, `--kes 1:02:03-1:02:04`, or `--kes 90-` to the end.

Either end may be a frame number instead of a clock: `--kes 300f-900f` is frames 300 to 900,
converted with the source frame rate. `--bolum 2` and `--bolum 2-4` build the same window from the
source's chapter marks; the two options cannot be given together.

`--yak N` (`--burn N`) burns the source's Nth subtitle into the picture. A text subtitle is
drawn by libass; an image subtitle (PGS, VOBSUB, DVB) is laid over the frame, scaled to its
width and bottom-aligned. MP4 cannot carry image subtitles, so without `--yak` they are
dropped and the plan says so, naming the option. The app does the same from its "Burn into
video" list, which offers text and image subtitles alike.

`--altyazi-tara` (`--subtitle-scan`) picks that track for you: the subtitle that only translates
the foreign-language parts of a film. A track flagged as forced wins; otherwise subtitle packets
are counted and the one track holding at most 10% of the fullest same-language track is burned
in. When the answer is not clear-cut nothing is burned and a line says why. It cannot be combined
with `--yak`. In the app the "Find foreign-language subtitle" button next to the "Burn into video"
list does the same and selects the track it finds.

Cover art is kept in MP4 and MKV output (png and jpeg in MKV, as an attachment). MOV cannot
carry it; the plan says so.

The long options below each answer to an English alias; the two spellings are the same
option, and a script may use either. `--crf` and `--json` and `--vmaf` are the exceptions: they have a
single spelling.

| Turkish | English |
|---|---|
| `--yardim` | `--help` |
| `--surum` | `--version` |
| `--hedef` | `--target` |
| `--kalite` | `--quality` |
| `--kodek` | `--codec` |
| `--on-ayar` | `--preset` |
| `--modul` | `--modulus` |
| `--bolum` | `--chapters` |
| `--cikti` | `--output` |
| `--kes` | `--cut` |
| `--aralik` | `--interval` |
| `--bir-kez` | `--once` |
| `--gunluk` | `--log` |
| `--tarama` | `--scan` |
| `--baslik` | `--title` |
| `--ana-icerik` | `--main-feature` |
| `--asgari-sure` | `--min-duration` |
| `--azami-sure` | `--max-duration` |
| `--suzgec` | `--filters` |
| `--kirp` | `--crop` |
| `--kirpma-kipi` | `--crop-mode` |
| `--profil` | `--profile` |
| `--profil-dosyasi` | `--preset-file` |
| `--ses-kodek` | `--audio-codec` |
| `--ses-normal` | `--loudnorm` |
| `--ses-kazanc` | `--gain` |
| `--altyazi` | `--subtitle` |
| `--yan-altyazi` | `--sidecar-subtitles` |
| `--yak` | `--burn` |
| `--yak-srt` | `--srt-burn` |
| `--yak-ass` | `--ssa-burn` |
| `--meta-yok` | `--no-metadata` |
| `--altyazi-dil` | `--subtitle-lang` |
| `--ilk-altyazi` | `--first-subtitle` |
| `--altyazi-tara` | `--subtitle-scan` |
| `--sabit-kare` | `--cfr` |
| `--tavan-kare` | `--pfr` |
| `--kare-hizi` | `--fps` |
| `--ses-hizi` | `--arate` |
| `--ses-drc` | `--drc` |
| `--profiller` | `--presets` |
| `--olcumsuz` | `--no-measure` |
| `--hizli` | `--fast` |
| `--dil` | `--lang` |

Exit codes: `0` in band, `2` under the band (quality saturated, the smaller file kept), `3`
size ceiling exceeded (the smallest result is still written; JSON carries `output` and `overTarget: true`), `1` error, `64` wrong usage, `130` cancelled.

## Watch Folder

```bash
vidshrink izle ~/Gelen --cikti ~/Giden --hedef 25MB             # run until Ctrl+C
vidshrink izle ~/Gelen --cikti ~/Giden --hedef 25MB --bir-kez   # drain the folder, then exit
```

`izle` shrinks every video that lands in the folder. `--cikti` is the output folder and is
required; it cannot be the watched folder.

`--aralik <seconds>` sets the scan interval
(default, unmeasured): 2, `--bir-kez` exits once nothing is left to wait for, and the other `kucult`
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
