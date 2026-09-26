# Changelog

All notable changes to VidShrink are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). `0.1.0` is the first tagged
release; the dated sections below it are the development record that led up to it and
ship as part of it.

## [Unreleased]

## [0.9.6] - 2026-09-26

### Added

- A share link now comes with a QR code to scan with a phone, on the recorder result, the Shrink tab and the Shrink window. It shows only while a link is ready (not during upload or after an error), is drawn dark on a light plate with a four-module quiet zone in every palette (contrast at least 7:1), and has an accessible name.

### Fixed

- Region recording no longer bogs the PC down: on Windows, automatic mode caps the capture at 30 fps instead of following the display refresh rate (60/120), which kept gdigrab copying the desktop nonstop and still delivered only about 80 of 120 frames. The recording frame is now only its edge strip, so clicks and the mouse wheel reach the app under the region even if the click-through style is lost, and the region toolbar no longer takes focus while recording.

### Changed

- Automatic updates are on by default on Windows. Settings files written before this version are switched on once, because they stored the old default and a deliberate "off" the same way; turning it off again from then on sticks. The update downloads quietly after the window has opened, the badge says it will install when you close VidShrink, and it installs on exit by swapping the files in place without reopening the app (about 0.3 s for a full release). If the swap cannot finish, the launcher waits for the app to exit and installs then. The launcher no longer downloads in the background at startup. macOS and Linux are unchanged.

## [0.9.5] - 2026-09-25

### Added

- The update notice has a second button, Download and Install: the update downloads and installs itself as soon as it is ready; cancelling or a failed download brings both buttons back.

### Changed

- The updated-to-new-version notice closes by itself after 5 seconds; hovering it or keyboard focus inside pauses the countdown, which resumes from the time left.
- Sliders share one theme: 40 px hit area across the whole track height, filled track and knob that grow on hover/press/focus, a gray disabled state, keyboard steps derived from the range (arrows small, PageUp/PageDown large), and the knob glides to a new value set by keyboard or the value box; the player's seek bar uses the same family.

## [0.9.4] - 2026-09-25

### Added

- `VidShrink-Setup.exe` opens a graphical setup panel when started without arguments (native Win32/GDI+, no WinForms/WPF): five steps (download, sha256 verification, placement, shell registration, shortcuts) with check/number/failure marks, a gradient progress bar with percentage, a fading monospace log, an install location row with a Change button, and the Install / Installing / Close + Open app / Retry button flow. Any flag or `--console` keeps the console installer; `VIDSHRINK_SETUP_PROVA` runs a rehearsal into a temporary folder; the log is also written to `%LOCALAPPDATA%\VidShrink\kurulum.log`.
- While recording, the region editor's toolbar shows stop and pause/resume, using the
  recorder's own stop path; during the countdown stop cancels it.

### Changed

- The Windows launcher is now NativeAOT, so a double-click starts the application about 60 ms sooner warm and 90 ms sooner cold.
- Screen recordings are now delivered as MP4 by default, so they open directly in
  WhatsApp, browsers and social apps. MP4 and MOV are captured in Matroska and remuxed
  with `-c copy -movflags +faststart` on stop, so a killed recording still yields a
  playable MP4. The choice is stored as `containerFormat`; the old default `Mkv`
  written by earlier versions reads as MP4.
- The region editor's toolbar is icon-only and much smaller (379x49 to 78x30 px); the
  region size moved into the start button's tooltip.

### Fixed

- Windows 10 with an old `vulkan-1.dll` no longer shows the "entry point vkGetPhysicalDeviceProperties2 not found" system dialog when a video opens: the installer and CI now place a pinned Vulkan loader (1.4.357.0) next to `libmpv-2.dll`, and if libmpv still fails to load the player is disabled with an in-app error instead of a system dialog.
- A recording killed within its first seconds no longer leaves a header-only Matroska file:
  the muxer now closes a cluster every 500 ms (`-cluster_time_limit`) instead of every 5 s.

## [0.9.3] - 2026-09-25

### Fixed

- The Windows installer no longer stops at "libmpv arşivi açılamadı (tar çıkış kodu 1)" on
  machines whose built-in `tar` cannot read 7z (Windows 10). libmpv now comes as a zip
  opened by .NET itself; the 7z archive stays as the fallback. The PowerShell script
  follows the same path.

### Added

- A one-line PowerShell install command at the end of the README.

## [0.9.2] - 2026-09-25

### Added

- A drawn recording region stays on screen: drag its edges or corners to resize it,
  drag the frame to move it, and use the thin toolbar above it to see the size,
  start recording, open the settings or close it. Clicks inside the region reach
  the app underneath, and the frame itself is kept out of the recording (Windows).
- The mini recorder strip has a Select region button.

### Changed

- Recorder options sit in fixed-width cells that wrap, instead of combo boxes
  stretched across the whole window; the language and theme boxes in Settings too.
- The button that switches to the mini recorder is labelled and highlighted.

## [0.9.1] - 2026-09-25

### Changed

- Install swaps the files in place: each running file is renamed to `.old`, the
  verified staged file takes its name and the app reopens itself, without handing
  over to the launcher. Measured from Install to the new process: 68 ms instead of
  114 ms, also while the background launcher waits at the door.
- A manual update download runs on six lanes instead of one; the 4 MB/s cap while
  playing stays.
- The progress bar settles at 100% as soon as the work ends.

### Removed

- The Matrix rain behind the update panel.

## [0.9.0] - 2026-09-24

### Added

- Media Foundation encoders (`h264_mf`, `hevc_mf`, `av1_mf`) can be picked by
  hand on Windows: hardware encoding, NV12, VBR, no CRF, off by default in
  Auto (HandBrake #15).
- SVT-AV1 writes HDR10 static metadata explicitly - mastering-display and
  content-light values - instead of leaving it to the container (HandBrake
  #28).
- An HDR10+ bridge carries the source's dynamic metadata frame by frame
  through x265's `dhdr10-info`; Dolby Vision 8.1 is carried through x265 and
  SVT-AV1 (MP4 marks it `-strict unofficial`), and HDR10+ or an unmovable DV
  profile falls back to a reason line instead of being dropped silently.
- A VP9 shrink path: `libvpx-vp9`, two-pass, muxed as WebM with opus audio.
- "Crop black bars" in the Advanced filter panel probes and removes letterbox
  bars automatically (HandBrake #36).
- Anamorphic sources keep their pixel aspect ratio: it is read from the
  source and the output is squared instead of being stretched.
- Dropping a folder or several video files at once queues all of them, using
  the window's own options for each.
- The queue can be reordered and edited, and an action for when the queue
  finishes can be set.
- The Advanced panel gained a filter surface, and the audio/subtitle track
  list moved into the main window.
- HandBrake parity's remaining six items: flac audio, loudnorm/gain, external
  subtitle files, cover art, the forced-subtitle flag, and subtitle burn-in.
- NVENC budget-filling opens with a 0.97 margin on the retry aim.
- A Neon palette: green background, cyan-to-purple accent gradient.

### Changed

- Target size is decimal MB (1 MB = 1,000,000 bytes) everywhere, not MiB; the
  quick-target chips use decimal GB (1000/2000 instead of 1024/2048).
- Split recordings: each part carries its own `-t`/`-fs` limit instead of a
  shared overshoot budget, and only the part that actually reaches the split
  boundary opens the next one - an unsplit 5 s recording no longer comes out
  as two parts under CI load.
- Double-click detection runs its single-click timer at Default priority;
  pending input no longer delays or drops the single click.
- Popups, flyouts, menus and the progress bar fade or slide in and respect
  "reduce motion"; many UI layout issues across all 42 languages were
  found and fixed by an automated layout audit.
- Command box, number-unit pairs, Arabic/Hebrew bidi isolation and several
  alignments were reworked across the interface (UI pass 3).

### Fixed

- The CLI, the recorder and the plan reasons now say only what actually
  happened: a recording that could not be deleted is reported with its path, a half-finished
  recording is checked with ffprobe before being called complete, GIF and
  buffer results state the real outcome, an unreadable half MKV no longer
  suggests reopening it as MKV, and a fallback encoder or container/track
  choice is named plainly instead of implied.
- MKV keeps AAC audio when the source is already AAC instead of re-encoding
  it needlessly; a fast H.264 fallback stays on hardware instead of quietly
  falling back to software.
- Ceiling-exceeding results no longer say "never" in any of the 38 covered
  languages.
- With "Keep tracks" already on, the image-subtitle note names the container
  that dropped the track instead of telling you to tick the box.
- HandBrake #40: anamorphic DAR was distorted on the shrink path; the source
  aspect ratio is now preserved.

### Removed

- The CLI's `--aci` flag was removed; the plan reason line already carries
  the same information.

## [0.8.5] - 2026-09-18

### Added

- `--kes` now takes frames as well as seconds and timestamps: `--kes 300f-900f`. The two ends
  are independent, so `0:10-900f` is valid. A number without the `f` suffix is still seconds.
  A new `--bolum` (`--chapters`) builds the cut window from the source's own chapter marks:
  `--bolum 2` or `--bolum 2-4`. The two options cannot be given together, since both set the
  same window. This closes the last two `--cut` gaps against HandBrake 1.11.2.

- The Advanced panel takes a `-tune`. The ladder is per encoder and measured, not copied from
  the recorder: x264 takes `film|animation|grain`, x265 has no `film`, and SVT-AV1 takes `0|1|2`
  written into `-svtav1-params` — its `tune=3` cannot open the encoder for ordinary encodes.
  A value that does not belong to the codec the plan picks is dropped with a line in the reason.

- The CLI locks quality and preset like the window does: `--crf N` (0-63) and
  `--on-ayar NAME` (`--preset`) reach the same locks the Advanced panel writes. A preset
  name that does not belong to the codec the plan picks is dropped with a line in the plan
  reason instead of throwing, which also fixes the same crash in the window when the codec
  was changed after a preset had been chosen.

- Track names and subtitle flags survive the shrink. A track's title is carried over as it is,
  and a subtitle's default/forced flag is written out explicitly instead of being left to the
  muxer. In MP4 the title lands in the `name` atom; the `default` flag there is ffmpeg's own
  (`docs/olcumler/e6-altyazi-bayragi-iz-adi.md`).
- MOV is a shrink-path container. Name the output `.mov` and the file is muxed as MOV instead of
  being silently treated as MP4: `+faststart` and `mov_text` subtitles carry over from the MP4
  family, and HandBrake's `av_mov` presets import. MOV cannot carry opus or flac, so such a track
  is re-encoded to aac and the plan says so on the reason line
  (`StreamMapping.cs`, `docs/netlestirme/022-mov-kabi-kucultmede.md`).
- Save the current settings as your own preset: the "+" chip at the end of the chip strip asks
  for a name only, your presets sit between a separator and that button, clicking one applies it
  and "×" deletes it with an undo line — no confirmation dialog (`MainWindow.OnAyar.cs`).
- `--dil` / `--lang` on the command line: the help and the messages follow the flag instead of
  the system language for that run. `en` and `tr` are known, `tr-TR` and `TR` normalise, and an
  unknown code is a usage error (`CliText.cs`, `CliApp.cs`).
- A screen recorder tab. Pick a screen, a window or drag a region with an aspect-ratio lock
  and preset sizes; start after an optional 3/5/10 s countdown; F7-F11 work as global hotkeys
  even when the window is not focused (`RecorderHotkeys.cs:32-39`).
- Recordings go to Matroska by default and can be remuxed to MP4 from the result panel; MOV
  and GIF are also available (`RecorderArguments.cs:798-809`).
- While recording: a click-through frame outside the captured area (F9 hides it), a tray icon
  with elapsed time and written megabytes, a webcam overlay with optional background removal,
  a cursor magnifier, click rings with a sound, on-screen keystrokes, a live thumbnail and a
  rolling replay buffer saved with F11 (`RecorderView.Tampon.cs:95-178`).
- Grab a still frame without interrupting the recording; split by duration or size with the
  overall limits counted across parts (`RecorderArguments.cs:1075-1094`); stop at a size cap;
  open the output folder when done; discard everything with F10.
- One click removes frozen, idle stretches from a finished recording (`IdleTrim.cs:24`).
- With "follow recording" on, a finished recording is loaded into the Shrink tab and opened
  paused in the player without switching tabs (`MainWindow.OdakTakibi.cs:59-82`).
- Window capture also works on macOS (CGWindowList) and X11 Linux, and refuses under Wayland
  (`RecorderWindowsMac.cs:22`, `RecorderWindowsX11.cs:22-24`).
- A headless CLI (`vidshrink-cli`, shipped with every release): `kucult`/`shrink`, `plan` and
  `izle`/`watch`, with TR/EN flag pairs (`--hedef`/`--target`, `--kes`/`--cut`) and
  script-friendly exit codes - 0 in band, 2 under, 3 ceiling exceeded, 4 watch errors, 64
  usage (`CliRequest.cs:10-19`). The GUI and the CLI share one decision engine, `ShrinkEngine`.
- `izle` watches a folder: it waits for a file to stop growing, never re-shrinks its own
  output, keeps its state in `.vidshrink-izle.json` and warns but keeps running when the
  folder is read-only (`WatchFolder.cs:198-231`).
- `--kes 30-90` shrinks only a window of the source: the target size applies to that window,
  and a source that already fits is stream-copied into the trimmed window instead of being
  copied whole (`TrimWindow.cs:56-67`).
- The player downloads subtitles from OpenSubtitles: search and download from the track menu,
  sign in from Settings, and a 401 on download says the file needs an account instead of
  blaming the key (`OpenSubtitlesProvider.cs:403-420`).
- Keep all tracks: an option that survives restarts, moves every audio and subtitle track into
  MKV with explicit `-map`, matches the primary audio by language and subtracts the side
  tracks from the budget; eight stream notes explain the choice (`StreamMapping.cs:115-165`).
- Overshooting the target by more than 3% asks what to do: retry, trim, accept the larger
  file, or leave it (`OvershootTrim.cs:22`).
- Fixed output sizes (1080/720/480) when dynamic resolution is off, plus a WhatsApp
  compatibility box and a "send as document" hint (`MainWindow.axaml:485-494`).
- Releases now ship win-arm64 and linux-arm64 alongside x64, and the installer picks the
  package matching the machine (`release.yml:204`, `UpdateCheck.cs:162`).
- "Reset all data" deletes every file the app writes to the user's folder, the stored
  OpenSubtitles session included (`AppDataReset.cs:19-31`).

### Changed

- Interlaced sources are detected (idet) and deinterlaced, and the crop is probed, on the
  ordinary encode path (`VideoFilterChain.cs:105-126`).
- On a dark source Auto switches from AV1 to H.265 turbo to avoid banding and says so in the
  reason line; HDR (PQ/HLG) sources are excluded (`DarkContentSwitch.cs:5-25`).
- The codec strip now offers AV1 as the smallest option - the old "H.265 (smallest)" label
  named a codec the engine did not use - with a tooltip about device compatibility
  (`PlanCalculator.cs:1404`).
- When the delivered file misses the budget the engine fills it with one step up, and a
  ceiling guard aims 0.90 x ceiling on the last attempt and still delivers the smallest
  result (`BudgetFill.cs`, `CeilingGuard.cs`).
- A saturation rule stops attempts once bits stop buying quality and steps the layout down
  (`Saturation.cs`).
- Automatic plans no longer drop the frame rate (`PlanCalculator.cs`).
- Player shortcuts follow the GOM layout: rotation on a key, in the menu and in the tooltip, a
  0.05 speed step, the double-click time read from the system, and the window snapping to the
  screen centre while dragged (`Keymap.cs:53`, `PlayerInputMap.cs:257-271`).
- Alt+wheel resizes the frame, and the right-click menu carries Settings and a player shortcut
  list as submenus without leaving the player (`PlayerView.axaml.cs:412-447`).
- The top bar opens in the same band as the bottom strip and stays open while paused; seek
  buttons print the seconds they skip; the strip opens spreading from the pointer
  (`HoverZone.cs:116-119`).
- The icon set moved to Fluent UI System Icons (`Themes/Icons.axaml`).
- Settings: two-option lists became radio strips, language and theme sit side by side, and the
  share target is a radio strip (`MainWindow.axaml:1195-1302`).
- The comparison badge shows only the CRF; ORIGINAL/PROCESSED labels moved above the panel.
- Shared files default to one day of retention instead of three (`paylasim-hedefleri.json:9`).
- The Shrink job window can share the finished file (`ShrinkJobWindow.Paylas.cs:22-62`).
- The macOS installer downloads the MPVKit libmpv dylib with a sha256 check; macOS 14 is the
  supported baseline (`install-vidshrink.sh:298-320`).
- The win-x64 publish is ReadyToRun composite; a file opened from the shell reaches the first
  frame about 120 ms sooner (`VidShrink.App.csproj:17-24`).
- The launcher no longer shows a panel on an ordinary start: the app is spawned first and
  maintenance runs behind it, a manual Install first finishes a pending staged update, and an
  app-folder gate keeps the folder from being written while a copy runs
  (`Launcher/Program.cs:62-98`).
- The Linux/macOS installer uses `GITHUB_TOKEN` when present so the release query does not hit
  the rate limit (`install-vidshrink.sh:397-399`).
- Units, abbreviations and the new keys are translated in 42-43 languages.

### Fixed

- Opening Settings no longer crashes. The `-tune` box was added to the Advanced panel as the
  third control, but the settings round trip still read the boxes by position over a
  seven-element array, so restoring threw `IndexOutOfRangeException` and every mapping after
  the tune box was scrambled in both directions. The boxes are now matched by name, and the
  tune lock is persisted as `advTune` - it was not written to the settings file at all
  (`MainWindow.axaml.cs`, `AppSettings.cs`).

- `izle --bir-kez` exits on a case-variant pair. `Klip.mp4` and `klip.mp4` in one watched
  folder used to reset each other's stability counter forever, so neither was shrunk and the
  run never ended. The scan now settles the collision: the ordinally smallest name wins and is
  shrunk, the others are skipped with one warning line each, and the run ends with exit code 4
  plus a summary of how many files were skipped (`WatchFolder.cs`, `CliApp.cs`).
- A killed recording stays playable: Matroska is written with `-flush_packets 1`
  (`RecorderArguments.cs:1029`).
- A recording that ended normally is no longer reported as a start-up failure
  (`RecorderSession.cs`).
- An interlaced x264 source has its height cropped to a multiple of 4 instead of failing.
- ffmpeg 8 rejecting `-top` no longer breaks the deinterlace path.
- Screen capture on a multi-monitor desktop no longer records the whole virtual desktop when
  the first monitor is selected. `gdigrab` takes no screen index, so every monitor - index 0
  included - is now converted to an offset region; a single-monitor machine keeps the
  offset-free `-i desktop` arguments.
- A capture region on a monitor placed left of or above the primary one is accepted. The blanket
  "region offsets cannot be negative" rule closed region capture on those monitors outright; the
  rule now asks whether the enumerated monitors cover the rectangle instead of looking at the
  sign. On macOS, where a region is a crop inside the captured frame, negative offsets stay
  rejected.
- A region that falls into the gap between monitors, or past the edge of the desktop, is
  rejected with a named error instead of being recorded as black.
- The region-drawing overlay covers the desktop on a mixed-DPI setup. It divided the desktop
  size by the primary monitor's scale while the window itself was born on - and scaled by - the
  monitor holding the desktop's top-left corner; with a 1.0 primary next to a 1.5 monitor at
  negative X the overlay covered 7200x2430 px instead of 4800x1620 px.

Measurements: `docs/olcumler/kaydedici-coklu-ekran.md`, harness `tools/kaydedici-yerlesim`.


## [0.8.4] - 2026-09-17

### Added

- `VidShrink-Setup.exe`: a native Windows installer that installs in about 6 s instead of 12 s, writes the right-click menu and file associations in about 0.1 s instead of 3.3 s, and closes a running VidShrink before replacing it. The PowerShell installer keeps working.

### Changed

- SVT-AV1 encodes run with variance boost off; at the same target size the measured quality no longer drops and the target band holds.

## [0.8.3] - 2026-09-16

### Fixed

- The Windows installer no longer stops when VidShrink is still running: it closes the program and waits up to 120 s for an antivirus scan (for example Avast CyberCapture) to release it before giving up.
- On Windows 11 without developer mode the unsigned primary right-click package is refused; the installer now keeps the classic menu and finishes instead of failing.
- The right-click "Open with VidShrink" entry written from Settings points at the launcher, so updates and repair run.
- A cancelled update download removes its partial `.part` file.
- A loaded event arriving after the render update no longer loses the first frame.
- Source info boxes stay on one line in a narrow window; glow shadows follow a palette change without a restart.

### Changed

- The launcher shows its maintenance panel only for a manual update, never on an ordinary start.
- Opening a video from the shell reaches the first frame about 992 ms sooner than before the F wave (median of 14 paired runs, 14 of 14; `docs/olcumler/acilis-hizi.md`).
- libmpv downloads fall back to a GitHub release copy.

## [0.8.2] - 2026-09-16

### Changed

- Opening a video from the shell reaches the first frame about 455 ms sooner (median of 14 paired runs, 14 of 14 in favour; see `docs/olcumler/acilis-hizi.md`).
- On Windows the window icon loads from the ICO instead of decoding the 1254 px PNG.
- The title-bar logo decodes in the background.
- Language names are read without loading all 42 catalogues.
- A file opened from the shell selects the player tab in the constructor, so the Shrink tab is no longer laid out first.

## [0.8.1] - 2026-09-16

### Fixed

- The raw pointer player tests create their evidence folder, so they pass on a clean CI checkout. `v0.7.3` and `v0.8.0` failed CI on that and shipped no release; their changes ship here.

## [0.8.0] - 2026-09-16

### Added

- Two-step updates: the title-bar badge turns yellow when a version is available (Download) and green once it is staged (Install). Installing is a separate click.
- The download runs on one lowest-priority thread in 64 KB reads, one file at a time, capped at 4 MiB/s while the player is playing; the launcher reuses the staged files.
- The update panel shows the app icon, a palette-coloured character rain behind the log lines, and a quick first stretch of the progress bar before the ceiling rule takes over.

## [0.7.3] - 2026-09-16

### Fixed

- The player input test saves its evidence frame with the PNG encoder overload, so the `-warnaserror` build passes. `v0.7.2` failed CI on that warning and shipped no release; its changes ship here.

## [0.7.2] - 2026-09-16

### Changed

- The player strip sliders use the timeline look: blue track, blue fill and a square thumb, no pink fill.
- Volume and speed readings sit in blue value chips; speed reads `1.00×` instead of `1x`.
- Top tabs share the title-bar button face: no fill, blue label, pink hover.
- The strip outline is drawn with the strong blue border, so its top edge no longer fades into the veil.
- With no media loaded the title bar stays open on the player tab too.

### Fixed

- The mute icon now follows mute, not only a zero volume.
- Raw pointer tests drive mute, volume, speed and play through the real input path in the main window.

## [0.7.1] - 2026-09-16

### Fixed

- Release 0.7.0 was tagged but never published: three source pins read attribute order in
  `MainWindow.axaml` and the tab-selection hook in `MainWindow.axaml.cs`, and the startup
  probe marks broke that order. The marks now sit after the pinned attributes and the
  Recorder hook has its own subscription. Everything listed under 0.7.0 ships in this
  release.

## [0.7.0] - 2026-09-16

### Added

- A startup probe. An attached property, `AcilisIsareti.Ad`, can be placed on any XAML
  element; when the element is constructed it writes a line into the startup trace. This
  gives per-subtree costs inside `InitializeComponent` without touching the visual tree.
  Nine marks now sit on the tab control and its children, and a pin keeps them in step
  with the measurement script's step list.

### Changed

- The Recorder tab is built the first time it is selected instead of at startup. The view
  was inline in `MainWindow.axaml`; it now moves into `MainWindow.TembelSekme.cs` and is
  constructed on first selection, with its margin read from the `SectionMargin` token.
  Visible behaviour: the first switch to the Recorder tab does the work that used to
  happen during startup.
- When a file arrives from the shell, the playback engine is opened while the window is
  still being built. `AcilisMotoru` runs `mpv_create` and `loadfile` in the background as
  soon as libmpv is warm; the player takes the ready engine over if it asks for the same
  path, and an unclaimed engine is disposed.

### Measured

- Paired warm measurement against 0.6.0, 14 repetitions on an idle machine: shell clock
  double-click to first frame 1239.8 ms to 1019.3 ms, paired median difference
  -213.7 ms, 13 of 14 pairs in favour of the new build. The engine step
  (`sekme` to `kare-kaynagi`) fell from 268.8 ms to 52.7 ms and the XAML step by
  89.6 ms. The perception clock (`perde`) is 79.3 ms.
- Hardware decoding (`hwdec=auto-copy`) was measured and reverted: it raised the engine
  step from 272.8 ms to 297.0 ms, about +24 ms.
- The probe retired a planned change: the 215 ms of `InitializeComponent` is not spread
  evenly over the seven tabs. Six of them total about 50 ms; the Recorder view alone was
  89.4 ms. Splitting every tab into its own `UserControl` would have bought nothing.

## [0.6.0] - 2026-09-16

### Added

- A startup curtain. The launcher now raises its own panel the moment it spawns the
  application and holds it until the first video frame is on screen, so the wait is
  covered by something visible instead of an empty desktop. It is the same bare Win32
  panel the installer already uses, armed with a zero threshold; the application signals
  it through a per-launch named event carried in `VIDSHRINK_ACILIS_PERDESI`. Both launcher
  paths — the double-click fast path and the ordinary one — raise and release it, and the
  400 ms threshold of the install panel is unchanged.

### Changed

- Release builds are pre-compiled. `PublishReadyToRun` and `TieredPGO` are on for the
  application and the launcher, so startup no longer JITs the whole of the IL. Development
  builds are untouched.
- Selecting the palette that is already current does nothing. `PaletteCatalog.Use` returned
  early only after running the full merge, which is the normal case at startup because
  `App.axaml` already declares the opening palette.

### Measured

- Double-click to first frame, paired warm run of 14 repetitions: a median paired
  difference of -2603.3 ms, with 14 of 14 pairs favouring the new build, range -4422.7 to
  -1402.8 ms. The palette step alone fell from 1183.6 ms to 1.2 ms in this session's units.
- The curtain appears at a median of 211.4 ms (minimum 149.9, p95 669.1). The machine ran
  about 3.6 times slower this session than when 0.5.5 was measured, so absolute numbers are
  not comparable across sessions; only the paired difference is. See
  `docs/olcumler/acilis-hizi.md`.
- The publish grew: `app/` from 207 to 224 MB, `VidShrink.exe` from 64.9 to 65.6 MB.

## [0.5.5] - 2026-09-16

### Changed

- The launcher no longer stands between a double-click and the window. When a file is
  passed on the command line it starts the application first and does its housekeeping —
  repair, version marker, update check — behind it. Pending file moves are skipped on
  that path and left to the next ordinary launch.
- The playback engine is created on a worker thread, the recent-files write and settings
  read moved behind playback, and the render clock no longer waits a full frame for the
  first picture.
- Updates download in six lanes instead of one at a time, and each file now costs a
  single HTTP range request instead of two: the end of a file's payload is derived from
  the central directory, so the local header and the payload arrive together. On the
  measured 0.3.0 to 0.4.1 difference that removes 375 round trips.
- The install bar advances by elapsed time rather than by frame count, with the same
  constants. One frame's worth of time yields exactly the old step, so the law did not
  change — only what it is multiplied by. A late frame no longer makes the bar stutter.

### Measured

- Double-click to first frame, paired warm run of 14 repetitions: 1796.5 ms before,
  1747.7 ms after, a median paired difference of -71.0 ms with 9 of 14 pairs favouring
  the new build. The remaining 1.7 seconds sit inside the application. See
  `docs/olcumler/acilis-hizi.md`.

## [0.5.4] - 2026-09-15

### Added

- Mini mode for the Recorder tab: a borderless TinyTask-sized strip carrying only the
  live dot, the elapsed counter, a single start/pause/resume button, stop and expand.
  The counter doubles as the drag handle, the strip stays on top only while a recording
  runs, and it places itself outside the capture frame. `F7` toggles, `F8` stops.
- The install panel keeps the window open while progress is still moving and runs the
  bar to full in a short sweep before closing.
- After a recording finishes, the "open target folder" and "share" buttons are
  emphasised in the result panel.
- A large translucent pause glyph fades in and out over the player for half a second
  when playback is paused; play stays clean so nothing covers the picture.

### Fixed

- The top strip no longer swallows clicks aimed at the controls beneath it.

### Research

- `docs/plan.md` plans the Recorder tab up to OBS/Bandicam level over four waves,
  grounded in 69 source-read projects across three reports under `docs/arastirma/`.

## [0.5.3] - 2026-09-13

### Changed

- The top strip only slides away on the player tab. It used to hide itself everywhere, so a
  pointer that drifted below the title bar on the shrink, recorder or settings tabs took the
  tabs with it. Hiding is the player's own need; on every other tab the strip stays put.
- The right-click menu is now two separate settings. One checkbox writes the "open with
  VidShrink" entry, the other writes the "shrink with VidShrink" submenu, and each one reads
  and writes its own registry branch - wanting one without the other no longer means taking
  both. Both checkboxes carry the exact text the menu entry will show, in the interface
  language.

### Fixed

- Title bar buttons on the player tab are now reachable: the bar's content moved above the tab content, so the full-window player surface no longer swallows the clicks.

- The Windows 11 primary menu entry now follows the interface language. Its title lives in
  the C++ shell extension, which drew it from the Windows UI language and knew only Turkish
  or English: a French user read English, while `shell.menu.open` has been translated in all
  42 locales all along. The app now leaves the chosen label in the registry and the extension
  reads it from there. Every quick-size entry carries the launcher icon too.
- A manual update says what it is doing. The launcher panel showed one sentence -
  "Güncelleme uygulanıyor" - for the whole download; the updater now reports each stage into
  the shared progress object: the version it found, how many files change, the name of each
  file as it comes down, and the move into place. The full log is written to
  `update-log.txt` beside the launcher, since the panel only holds nine lines.
- `VIDSHRINK_UPDATE_PROVA=1` runs the panel end to end without installing anything. The
  files are downloaded and verified, nothing is moved into place and no launcher swap is
  armed.
- The screenshot tool no longer writes the settings of the copy installed on the machine.
  `MainWindow` opens `%APPDATA%/VidShrink/settings.json` when it is handed no path, and the
  player history, the recent list and the recorder settings sit beside it. Shooting the T191
  set on 13 September 2026 switched the window to English and saved that file back with
  `language: en` and `autoUpdate: false`, so the installed copy stopped following releases -
  0.5.2 did not arrive on its own - and the test clips landed in the user's recent list. The
  tool now keeps its own file under `.calisma/`.
- A tag whose release run fails now pushes a notification. 0.5.0 and 0.5.1 both have tags and
  neither published an asset, so installed copies kept reading 0.4.5 from
  `releases/latest` with nothing saying anything was wrong.

## [0.5.2] - 2026-09-13

### Fixed

- The player strip's seek check no longer goes red on a loaded machine. It compares the
  target the button asked for against the position read back from libmpv, and those are two
  separate clock reads: on the release runner the engine advanced 0.066667 s - exactly two
  frames of the 30 fps clip - between them. The margin was one frame; it is now two
  (0.08 s), which still rejects a third frame at 0.1 s. The 0.5.1 tag built green on CI and
  red on the release run for this one reason, so it published no assets; 0.5.2 supersedes
  it.

## [0.5.1] - 2026-09-13

### Changed

- Every screenshot in the repository was retaken as the **T191** set. The T190 set, shot
  earlier the same day, showed no title bar and no tab strip at all: the top strip now
  starts hidden and appears on hover, and a headless capture has no pointer. The shot tool
  drops the `chrome-hidden` class before it renders, so the eight screens carry the window
  the way it looks in use. T190 moved to `trash/gorseller-T190/`.
- README leads with what the program costs and what it refuses to do - free for good, no
  ads, no account, no subscription, no telemetry, works offline - and says plainly that the
  window speaks 42 languages and ships 26 themes, with a one-click issue link for a missing
  language. Every tab now has a current screenshot behind a collapsed gallery, and the two
  heaviest sections (the rig's six guarantees, the pipeline diagrams) are collapsed so the
  first screen stays readable.
- The roadmap says the goal out loud: **HandBrake is to be passed on perceptual quality
  too**, measured on the same rig against the same source, gap to zero and then past it.

### Fixed

- The 0.5.0 release never reached users: its release run stopped at the test gate. Six
  measurements still described the layout the way it looked before the title bar and the
  tab strip became an overlay, and the shell-menu keys added in 0.5.0 moved two key
  counts. The page now fits at 944 px empty and 948 px loaded (was 1012 / 1020), the
  Shrink tab's overflow at the window's floor is 221 px (was 251), and the localized key
  counts are 31261 walked and 1172 title-cased.
- The notice layer's pin asked whether the notice and the content sat in the same grid
  cell. Since the content spans both rows, that question had no true answer any more; it
  now asks whether the notice's row falls inside the content's span, which is what "the
  notice does not push the player down" actually means.
- A timed wait in the playback resume measurements threw its result away and then awaited
  the task anyway, so a timeout became an indefinite hang - the 0.4.5 run was aborted
  after ten idle minutes with a hang dump. The wait now fails with its own message.

## [0.5.0] - 2026-09-13

### Added
- Recording hand-off. When a recording finishes, the result panel now offers **Send to Shrink**,
  **Open in player** and **Share** beside Show folder, so the file you just captured goes straight
  into the next job without a trip through the file manager.
- Sharing after a recording runs through the same upload layer as sharing after a shrink: one
  provider table, one progress bar, one cancel button, one link to copy.
- **Settings → Right-Click Menu.** A single checkbox adds or removes the Windows context-menu
  entries. It writes to `HKCU\Software\Classes` only, needs no administrator rights, and removal
  also unregisters the Windows 11 menu package. No installer script, no command line.
- The context-menu labels follow the interface language across all 42 shipped languages, and are
  rewritten as soon as the language changes.

### Changed
- The title bar and the tab strip are now an overlay that stays hidden. Move the pointer to the top
  edge and they appear; move away and they go. The full window height belongs to the content, and
  nothing shifts when the strip comes and goes.
- No window outline while the player tab is selected.

### Documentation
- The README was rewritten around what a reader needs in the first minute: what the program does,
  the four tools, install, then the numbers. It is now 200 lines instead of 652; the engine
  walkthrough, the usage tour and the install detail moved to `docs/motor.md`, `docs/kullanim.md`
  and `docs/kurulum.md`, and the development notes to `CONTRIBUTING.md`. Screenshots are current
  and the two remaining diagrams are compact.
- The benchmark baseline was re-measured on today's engine: 36 cases over three clips, three
  targets, a software and a hardware arm, two repeats each
  (`docs/olcumler/bench-2026-09-13.md`). The 0.1.0-era numbers that README and `docs/motor.md`
  quoted are replaced and the old result files are marked stale.
- Two findings are published rather than buried: the AV1 branch undershoots the fill band (all
  five misses and the single hard-floor violation are `libsvtav1`), and the measurement rig locks
  the two streams by frame index, which exaggerates loss whenever a plan lowers the frame rate.

### Fixed
- The share target table (`paylasim-hedefleri.json`) is now part of the published package. In an
  installed build the Share button could not find a target, which made sharing fail with
  "no targets" no matter how it was configured.

## [0.4.5] - 2026-09-13

### Changed

- **One outline language for every button.** At rest a title-bar button, a tab and a page
  button all carry the same faint `HeaderRestBorder` hairline; on pointer-over a
  transparent overlay ring inside the template picks up colour at `BorderStrong` (2). The
  ring is an overlay rather than a thicker real border, so nothing shifts by a pixel when
  the pointer arrives. Tabs and title-bar buttons moved from `RadiusChip` (6) to the new
  `RadiusSquare` (3).
- **Icon geometry now follows measured numbers.** `IconStroke` went from 1.5 to 2, the
  value the 24-unit grid is drawn for; the title-bar trio moved from 14 : 16 : 16 to
  12 : 18 : 14; pause, play, speed, coffee, volume, both chevrons and restart were recentred
  on the design box. Play keeps a deliberate rightward optical offset. The Teknesyum glyph
  is a technetium atom instead of `<>`, and Settings is a ringed cog instead of a sunburst.
  Every number comes from `docs/arastirma/ikon-estetigi.md`, and `IkonKutusuTests` parses
  all 26 paths to pin the 2-unit margin and the centre.
- **The player's sliders are longer and snap.** Both are `PlaybackSliderWidth` (192) wide;
  volume snaps to multiples of 5 and speed to multiples of 0.05, so 1.00 is exactly
  reachable. The speed icon became a button: press it at any other speed to go to 1, press
  it again to return to the speed you left.
- **The update panel says where it is.** The bar is no longer an indeterminate sweep: a
  single bridge object carries the percentage, a ceiling, the sentence and the log, and the
  work speaks to the screen only through it. The bar closes on the percentage quickly and
  creeps towards the ceiling when a step runs long, so it stays alive without eating the
  next step's room; the percentage never goes backwards. Three things are always on screen -
  the sentence, the percentage and the last nine log lines, the newest bright and the rest
  dim, trimmed with an ellipsis rather than wrapped. Green when it finishes, ember when it
  fails.
- **The update notice has one action.** The shell-command box and its copy button are gone;
  a single **Install** button remains. Where no launcher is installed the same button opens
  the releases page.

### Added

- **The recorder picks its own settings.** The encoder, frame rate, capture size, container
  and preset are no longer questions. A candidate ladder is built from what the machine
  reports - a hardware encoder only after a real ffmpeg probe encodes with it - and the
  winner is decided by a short trial recording, not by a table. Tick **I will pick the
  settings myself** to get the old controls back.
- **An approximate length and size, if you want one.** Give both and the recording aims for
  that file size; the bitrate comes out of OBS's own writing of the formula
  (`MB x 8 x 1024 x 1024 / 1000 / seconds - audio`). Real-time capture rules out two passes,
  so the budget becomes a capped bitrate with a buffer of twice the rate. A target below the
  floor is refused out loud instead of quietly producing a broken recording. Leave both
  empty and the program picks the best settings.
- **A hidden developer tab.** Advanced no longer shows in the strip. Clicking the
  system-status line in About seven times, with no more than two seconds between clicks,
  reveals it and selects it; a button inside the tab puts it away again. The counter is
  `DeveloperUnlock` in Core and takes the clock from its caller, so the threshold and the
  window are measured rather than guessed.

## [0.4.4] - 2026-09-12

### Changed

- **One icon set for the whole program.** `Themes/Icons.axaml` holds every glyph as a
  stroke-only geometry: colour comes from the owning control's `Foreground`, thickness from
  the `IconStroke` token, so an icon follows the palette instead of a font. Every tab now
  carries its own icon, the maximise/restore/close glyphs became geometry, and the player's
  volume emoji went with them. The last text glyphs went too: the expander arrows on the
  quality, audio, frame, advanced, command, plan-reasons, performance and AI headers are
  chevron geometries, and the control strip's play/pause and restart buttons are drawn
  rather than typed. Shapes are from the Lucide set (ISC licence).
- **Settings is a tab, not a separate panel.** The gear and its button left the title bar;
  Settings is the last tab of the strip, with the same gear as its icon. `main.language.settings`
  had no owner left and was removed from all 43 languages.
- **The player tab is the video and nothing else.** The title line, the subtitle and audio
  track buttons and the overflow menu are gone - the right-click menu already carried all
  three. Volume and speed are sliders with their numbers beside them, -10 / play / +10 sit
  centred with play the largest target, and the player fills the whole workspace. A notice
  now floats over the player instead of pushing it down. `main.player.title` and
  `main.player.menu` were removed from all 43 languages.

### Added

- **Ten more arms on the recording command.** The recorder can now choose its container
  (a recording killed by a timeout is playable only in Matroska, and the engine knows which
  containers survive that), scale the capture after the crop, set the keyframe interval,
  profile and tune, record to a target bitrate instead of a quality figure, write the pixel
  format, colour space and colour range, stop at a duration, split into segments, keep audio
  devices as separate tracks rather than mixing them, and take a single frame of the target.
  A capture on a second monitor is translated into an offset region, so the picture is the
  monitor asked for.
- **An automatic mode in the recorder.** One checkbox and the program writes the encoding
  itself. The decision is in two parts: `RecorderAutoPlan` builds a candidate ladder — the
  first hardware H.264 encoder the probe has actually seen encode (NVENC, then QSV, then
  AMF) or `libx264` where none works, a frame rate rounded down to the closed ladder
  {24, 30, 60, 120} from the screen's refresh rate, the capture size and then half of it,
  Matroska throughout, and a preset taken from the chosen encoder's own vocabulary rather
  than x264's. `RecorderAutoProbe` then records three real seconds per candidate and reads
  the dropped-frame counter: the first candidate that drops nothing wins outright,
  otherwise the lowest drop ratio wins with ties broken by ladder order. No acceptable
  drop-frame figure is invented, because none has been measured here. While the mode is on
  the manual options are hidden and the chosen settings, with the reason for each, are
  written in one line under the checkbox. The screen's refresh rate is read for the first
  time (`EnumDisplaySettings` on Windows); where it cannot be read the ladder falls to 30.
- **An Install button in the new-version notice.** The notice used to hand out a PowerShell
  one-liner and ask the user to run it. Wherever a launcher is installed it now shows a
  button instead: the application starts `VidShrink.exe --update-now <pid>` and closes, the
  launcher waits for that process to exit, downloads and applies the update behind the
  startup panel, and opens the new version. The version on screen after the click is the new
  one. The button neither reads nor writes the automatic-update switch, so installing once by
  hand does not change the preference. Where `LauncherUpdate.LocateLauncher` finds no
  launcher — a Linux installation, a plain macOS copy — the command is still shown, because
  there is nothing there to drive. `main.action.install` is translated in all 42 languages.

## [0.4.3] - 2026-09-12

An installation that could not reach a newer release now does. 0.4.1 shipped a self-update
that a 0.3.0 installation was unable to finish, so a desktop shortcut kept opening the old
build no matter how many releases came out.

### Fixed

- **The update now converges instead of restarting.** Three measured defects made it
  impossible for a 0.3.0 installation to reach 0.4.1. The manifest gate was 800 ms while the
  published `manifest-win-x64.json` (90589 bytes) took 1817 ms to fetch cold, so most
  launches gave up before reading it. The whole update, download included, had a 90 s budget
  while the measured 0.3.0 → 0.4.1 difference was 375 files and 134.8 MB. And any failure
  discarded the staging folder, so no progress survived the round. The manifest timeout is
  now 5 s, staging persists and is resumed by digest, and it is discarded only when it was
  collected for a different version.
- **The download left the startup path.** Before the application opens, the launcher now
  only does local work — finishing a half-done launcher swap and moving staged files into
  place. The manifest fetch and the download run after the window is up, under a 30 minute
  budget, and what they collect is applied on the next launch. A second launcher finds the
  work already running through a named mutex and does nothing.
- **Releases no longer carry debug symbols.** The six `.pdb` files weighed 100.45 MB of a
  205.22 MB payload, `libSkiaSharp.pdb` alone 84 MB, and an installation that never had them
  counted every one as a missing file. They are deleted from the publish folder before the
  manifest is written, so neither the manifest nor the archive lists them.
- **A red gate on `main`.** `PaletteCatalog.Address` built the theme address as one
  interpolated literal, which the localization scan read as a sentence left in code. The
  file name is now a constant, so the literal carries no sentence. 0.4.2 was tagged with
  this still red and produced no release; 0.4.3 is the same work with the gate green.

## [0.4.1] - 2026-09-12

VidShrink learned to record. 0.4.0 handed the window to the player; this release adds the
screen recorder beside it — capture, sound, and a tab that exposes both without asking the
user to know an ffmpeg flag.

### Added

- **A screen recorder.** The whole screen, a single window, or a region, on each platform
  through the capture backend it actually has: gdigrab on Windows, avfoundation on macOS,
  x11grab on Linux. Stopping is gentle — ffmpeg is asked to close the file rather than
  killed — so a stopped recording plays back, and a recording killed by a timeout is
  reported as partial instead of being handed over as a broken file.
- **Sound in the recording.** The recorder lists the machine's audio devices and records a
  microphone, the system output, or both. Choosing both records two inputs and mixes them
  into one track. The chosen device is remembered by name rather than by position, so a
  device list that reorders between two launches cannot quietly select a different
  microphone, and a device that is gone is reported instead of falling back to silence.
- **The Recorder tab**, in all 42 languages: a control strip with elapsed time and frame
  counters, panels for the target and the encoding options (frame rate, quality, encoder,
  preset, mouse cursor), an output folder, and the audio panel with a button that reloads
  the device list for a microphone plugged in while the program is running.

### Fixed

- The audio plan reached ffmpeg only in part. The recorder passed the audio inputs but
  never the filter graph or the stream maps, so selecting two devices still produced a
  single stream — the second was dropped without a word. A complex filter graph also means
  ffmpeg no longer maps the video stream on its own, so the video mapping is now written
  explicitly alongside every audio mapping.

### Known limitation

- Written size does not stream while a recording runs: the `total_size` field stays at zero
  in ffmpeg's progress output for a gdigrab capture, measured across eleven blocks. Elapsed
  time and the frame counters do stream; the size is read from the finished file.

## [0.4.0] - 2026-09-12

The player stopped being a tab and became the window. 0.3.0 could play the source; this
release hands it the screen, puts every control where the hand already is, and measures
the cost of the first frame instead of guessing at it.

### Added

- **A real playback engine.** The player runs on libmpv instead of an ffmpeg pipe, with
  software rendering as the guaranteed floor. Seeks land on the frame asked for, the
  engine survives a late seek event, and the handle is locked against a dispose race.
- **The mouse does what a player's mouse does.** Left click pauses and resumes. Holding
  left and dragging moves the window when it is windowed and pans the picture when it is
  maximised or full screen, and a picture dragged near the centre snaps to it. Right click
  opens the menu at the pointer, with Settings as its first row.
- **A bottom control strip.** Play, ten seconds back, ten seconds forward, volume and
  speed sit in reach along the bottom edge, with elapsed and remaining time beside them.
  The strip appears instantly on hover and fades after a beat.
- **A seekable timeline with a live preview.** Clicking the bar jumps there; dragging it
  shows a preview thumbnail that follows the pointer.
- **Subtitle and audio track selection**, per-track delay, encoding override, and
  embedded-subtitle handling for files that carry more than one of each.
- **A comparison panel.** Source and result play side by side from two engine instances,
  frame-paired before either half is published.
- **Advanced picture and sound controls** — every setting written to the engine and read
  back from it, resettable in one move.
- **Player tools** — thumbnail strip, clip and GIF export, a mini mode, and opening a
  video straight from a URL.
- **Shell integration.** Double-clicking a video opens it in the running instance through
  a single queue, so a multi-file selection arrives as several jobs.
- **Forty-two interface languages**, with right-to-left layout for Arabic, Persian, Hebrew
  and Urdu.
- **Twenty-six theme palettes**, six of them light, applied while the program is running
  rather than at the next start.

### Changed

- **Avalonia 12.** The whole interface moved to 12.1.2.
- **Double-click to first frame is measurably faster**: a paired measurement over twenty
  runs puts it 241 ms ahead warm and 177 ms ahead cold, with not one repeat favouring the
  old build. Temp-folder cleanup left the startup thread, libmpv now loads while the
  window is being built, and the default-app suggestion and update check moved behind the
  first frame. The measurement rig and the numbers are in `docs/olcumler/acilis-hizi.md`.
- **The player fills the window.** The tab's own margins and padding are gone and the
  controls became layers over the picture, which grew the video surface by 23,9 percent.
- **The title bar** carries the tabs and the language picker; the job window dropped the
  system chrome.

### Fixed

- Choosing a theme now repaints the running window instead of waiting for a restart.
- The brand name on the support button is a fixed string again, not a translated entry.
- Interface-guideline violations in the main window, the shared control themes and the
  player panels — focus rings, unnamed interactive elements and motionless components.

## [0.3.0] - 2026-09-06

The engine stopped guessing. Where 0.2.x picked constants that looked reasonable, this
release measures the source and lets the measurement decide - and where a decision still
cannot be measured, it now says so instead of pretending.

### Added

- **A player tab.** The window plays the source itself, through a decoder pipe that stays
  open between seeks instead of launching ffmpeg for every scrub. Wheel steps one second,
  Ctrl ten, Shift sixty, Ctrl+Shift five minutes; Alt+wheel zooms; right-click and space
  toggle playback; middle-click toggles full screen and returns the window to where it
  was. The context menu carries the same three actions.
- **Shrink from the shell.** The installer adds "Shrink with VidShrink" to the right-click
  menu, with a submenu of quick target sizes. The request is consumed by the running
  instance through a single queue, so several files selected together arrive as several
  jobs rather than one truncated argument.
- **Manual override on every engine decision.** A deliberate user can take the mode, the
  CRF, the preset and the audio bitrate away from the engine. Each override is recorded
  with what the engine would have chosen, so the plan panel shows both numbers.
- **Codec lock.** Picking a codec no longer drags the rest of the plan with it - the user
  chooses the codec, the engine keeps the decisions that depend on measurement.
- **An advanced-settings section and a reasons overlay** on the preview panel, so the
  numbers behind a plan are reachable without leaving the tab.
- **The settings tab is populated** and remembers the advanced choices across restarts.
- **Scene-aware bit allocation.** The scene map is built from the source and drives the
  per-scene bit budget in production, not only in the measurement harness.

### Changed

- **Quality is measured, not assumed.** The plan reads a measured quality score for the
  candidate settings instead of the hand-picked constants that stood in for it. The
  sampling loop that produces the score samples on scene boundaries rather than a fixed
  two-second grid.
- **Hardware encoders are tested, not trusted.** Selection used to accept an encoder
  because ffmpeg listed it. It now probes it, moves to the next candidate when the probe
  fails, and never labels an untested candidate as unusable on this machine.
- **"Could not measure" reaches the user.** The probe used to answer yes or no; a failed
  measurement collapsed into "no encoder". The third answer now survives the whole chain
  and the screen says which of the three it is.
- **The first pass of a two-pass run is a real turbo pass.** It used to run at the same
  preset as the final pass, so the analysis cost as much as the encode.
- **GOP length and the CRF ceiling follow the content** instead of sitting at fixed
  values, and the peak-rate ceiling and the psy/AQ flags carry the values that were
  measured rather than the ones that were guessed.
- **Audio is never dropped.** No target, however tight, silences the track any more; the
  audio floor was removed and the video bitrate carries the shortfall.
- **Encoding success is no longer just an exit code.** ffmpeg returns 0 while silently
  dropping a parameter it did not understand; the run is now checked against what was
  actually applied.

### Fixed

- **A failed probe locked the Start button permanently** and swallowed the exception.
- **The preview restarted from the beginning** whenever playback was paused and resumed,
  even though no setting had changed.
- **A maximized preview panel did not shrink** when the user clicked outside it.
- **A shrink request from the shell lost its target size** on the way into the engine.
- **Frame-rate reduction shifted the output's duration.**
- **Text that no locale key reached** - embedded strings, English keys and sample errors
  that came out Turkish in an English window - is bound and translated. The title-case
  rule no longer rewrites body sentences: a sentence stays as the locale file wrote it.
- **Three tests were red on CI for six pushes.** The chip scan started from a name that a
  later commit moved above it, six tooltip lines overflowed onto a single orphan word,
  and the audio-video drift measure ran against a sound device that does not exist on a
  CI runner - measuring the raw video timestamp against a clock stuck at zero.

## [0.2.5] - 2026-08-30

### Added

- **Every string on screen now comes from a locale file.** `Locales/<language>/<area>.json`
  holds the text; the window reads it by key instead of walking the visual tree and
  looking up the English it finds there. Anything the old dictionary did not carry stayed
  English no matter which language was selected - that is why the Quality and Performance
  sections showed English under a Turkish window. Adding a language is now copying a
  folder and translating it; the app discovers it on its own.
- **Settings survive a restart.** Twenty-five values are written to disk, the selected
  language among them, so the language no longer has to be set on every launch. A button
  in Settings clears everything that was saved and puts the app back to its first-run
  state, after a confirmation.
- **The comparison panel has maximize and full-screen buttons.** Panel size is a decision
  the buttons make, not something the pointer's position decides for you.
- **macOS installs a real application bundle.** `install-vidshrink.sh` wraps the download
  in an ad-hoc signed `~/Applications/VidShrink.app` that opens from Finder with its own
  name and icon; the icon is generated from the artwork already in the repository. The
  script's `--uninstall` removes the bundle, the payload and the shortcut together.

### Changed

- **The comparison panel no longer grows when the pointer crosses it.** The hover-growth
  path is gone, along with the circular animation that came with it. When the panel is
  promoted its backdrop is opaque black; at its normal size its transparency matches the
  other panels.
- **The version string no longer carries the commit hash.** It reads `0.2.5`, not
  `0.2.5+3e26738`.
- **Info boxes are wider and the `?` badge is smaller** (20 px to 18 px), so a single
  item fits on a single line.
- **The measurement suite runs on macOS and Linux.** The windowing backend is chosen per
  platform: Win32 on Windows as before, Avalonia's headless backend elsewhere, still
  drawing through Skia. Before this the suite could not finish outside Windows at all.
- **The release workflow publishes the release itself.** It used to leave a draft, and a
  draft is invisible to `releases/latest/download` - so every release reached nobody
  until it was published by hand, and installs kept fetching the previous version.

### Fixed

- **VidShrink never started on macOS.** The kernel kills any non-notarized executable
  whose name ends in `.app` or `.App` at exec time, silently. The published binary is
  renamed on macOS targets only.
- **ffmpeg is found on macOS** when it is not on `PATH`, through the usual Homebrew and
  MacPorts locations.
- **Quality measurement died on any ffmpeg built without libzimg.** The comparison chain
  asked for the `zscale` filter without checking it existed; it now falls back to `scale`.
- **Text that the localization merge left unbound** - the tab title, the automatic plan
  row and four settings-reset strings - is bound again.
- **The window painted transparent pixels** after the merge and let the desktop show
  through. It is fully opaque again.
- **The release workflow claimed the project is MIT.** The repository is
  AGPL-3.0-or-later.

## [0.2.4] - 2026-08-30

### Changed

- **The phoenix behind the workspace was redrawn.** It used to be a handful of flat
  slices; it is now built from layered feathers whose edges undulate along their length
  and split into uneven fringes at the tip, with irregular embers drifting up from the
  body and a soft radial glow that no longer shows a hard rim. The wings are spread
  rather than folded, and the two sides are no longer mirror images of each other. The
  palette did not grow: the glow and the embers are opacity variations of the nine
  existing ember tokens, and the worst-case body-text contrast stays above 4.5:1.
- **The preview now runs five seconds ahead instead of two.** Two seconds was too short
  to judge a scene against its compressed twin.
- **Hovering the preview panel grows it immediately and shrinks it as soon as the pointer
  leaves.** The two-second delay and the circular countdown that visualised it are gone.
- **The zoom buttons always change the panel size.** On a panel already grown by hover,
  the height had been pinned at its ceiling from the first moment, so presses past a
  certain point changed nothing at all. A notch on a promoted panel now always moves.
- **An empty preview panel is transparent.** Both of the veils that used to sit over the
  backdrop while no frame was present are lifted, so the phoenix shows through; they come
  back the moment a frame arrives.
- The scrim inside the panel fades out towards its top edge instead of ending in a
  straight line.

## [0.2.3] - 2026-08-29

### Fixed

- **Re-running the installer no longer fails on a file that is only briefly locked.** The
  step that clears the install folder used to give up on the first refusal, which put a
  raw .NET exception on screen twice in a row over a DLL that a background scanner was
  still reading. It now retries six times over 6.2 seconds with a doubling wait, and if
  the lock outlives that it says what happened instead of printing the exception. A lock
  held by a genuinely running VidShrink is reported separately, by name and process id.
- A staged launcher that cannot be replaced no longer cancels the application update
  along with it: the application files land, and only the launcher step stands down.
- The launcher archive is no longer downloaded again on every start while a swap is
  waiting to be committed.
- A pending launcher swap is not committed over an installation that is already newer.

## [0.2.2] - 2026-08-29

### Fixed

- **The launcher can now update itself.** Until this release the self-update replaced only
  the application folder: the launcher binary sat outside it and was never listed in the
  manifest, so any fix to the update logic itself could only reach an installation by
  re-running the installer by hand. The manifest now carries the launcher in its own
  top-level field, which older clients ignore, so installations on 0.2.0 and 0.2.1 keep
  updating exactly as before.
- The swap never leaves the launcher name absent. The incoming binary lands beside the
  target, the running launcher starts the application and exits, and the new binary then
  renames itself onto the target name in one atomic move. A probe that scans the folder
  throughout the swap saw the name present in all 91,768 samples; the same probe run
  against the previous two-step procedure missed it 845 times.
- A launcher version marker is no longer written for a launcher that was never verified,
  and a pending swap is dropped when the installer has already moved the launcher forward.

## [0.2.1] - 2026-08-29

### Added

- The comparison panel now shows a **countdown ring** while the pointer rests on it. The
  ring closes clockwise over the two seconds before the panel grows, so the wait is
  visible instead of feeling like a stall. It clears the moment the pointer leaves.

### Changed

- The **phoenix behind the workspace** was redrawn from scratch. It is built from
  twenty-four separate feather paths rather than four filled plates: the near wing carries
  eight feathers, the far wing six and shorter, the tail five at different depths. Each
  feather burns from its own root to its own tip, so the fire reads as one rising bird
  instead of a symmetric silhouette.
- Panels are now slightly translucent, letting the phoenix show through without costing
  legibility: body text over the brightest flame measures 17.77:1 against a 4.5:1 floor.
- **The launcher checks for a new version on every start.** It used to check once a day,
  which meant a freshly published release could stay unseen for up to twenty-four hours.
  An unreachable network still costs a start no more than the manifest timeout - measured
  at 810 ms against an 800 ms budget.
- Hovering the comparison panel grows it to the tallest size the window can hold, worked
  out from the window rather than from a fixed multiplier, and stops one notch short of
  the full stage so hovering can never take over the whole workspace.
- The redundant **Preview** heading is gone and the three columns now start at the same
  top edge; the gap between the tab row and the panels was narrowed by one step on the
  spacing scale.

## [0.2.0] - 2026-08-29

### Added

- A **performance check** in the Advanced section answers one question with measurement
  rather than guesswork: what does encoding cost this machine, and would recording a game
  cost it frames. It runs six passes - counter calibration, sample clip, baseline decode,
  two hardware encoder passes and a software pass - reports what each leg actually cost,
  and says plainly when a leg could not be measured instead of reporting a silent zero.
  The panel's headline is built from the findings, never from the summary verdict, because
  the verdict can read "no hardware" while a hardware encoder is running.
- Right-click a video in Explorer and **Open this video with VidShrink** is there, on the
  same 24 extensions the application itself opens. The Windows installer writes the entry
  under `HKCU\Software\Classes\SystemFileAssociations`, so it needs no administrator rights
  and does not take over the default player - it adds a line to the menu and leaves the
  file association alone. The entry points at `VidShrink.exe`, the launcher, for the reason
  the shortcuts do: an entry aimed straight at the application would never update.
  The label follows the system interface language, Turkish or English, and
  `-MenuLanguage tr|en` forces it. `-RemoveShellMenu` clears every entry in one step,
  `-ShellMenuOnly` rewrites them without reinstalling, and `-SkipShortcuts` now means the
  shell is not touched at all.
- A file path handed to VidShrink on the command line - by the shell menu, a shortcut or a
  drag onto the executable - now loads through the same path a dropped file takes. A path
  broken into pieces on its spaces resolves too, which the old single-argument lookup could
  not do. The list of media extensions moved to `VidShrink.Core` so the file picker and the
  installer's registry entries read one list instead of two copies.

### Changed

- The background phoenix burns. Its fill went from one flat red at 6% opacity to four
  gradients running yellow to orange to red and out to transparent, at 30% opacity. The two
  new warm tones are derived from the existing ember red rather than invented: same
  saturation, same lightness, hue stepped evenly toward yellow. Body text keeps 8.41:1
  contrast over the brightest flame pixel - above the AAA threshold, not just AA.
- The comparison panel now shrinks the moment the pointer leaves and waits two seconds
  before growing. Leaving before the two seconds are up cancels the pending growth
  entirely. The playback control strip is unchanged.

### Fixed

- The mouse wheel's three zoom stages collapsed to two on short windows: the middle stage's
  ceiling rose to the band itself when the band was taller than the window's share, so the
  middle stage matched the full stage exactly. The share cap is now unconditional.
- Unit and codec names are no longer rewritten by the display-casing pass. The token that
  matched identifiers stopped at the underscore, so `h264_nvenc` was being read as `h264`.
- The smallest-size clipping measure never protected anything: it compared a control's
  desired size against its bounds, and in Avalonia bounds are never smaller than desired.
  The working criterion exposed a real 15px overflow of a hint button, now fixed.

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
