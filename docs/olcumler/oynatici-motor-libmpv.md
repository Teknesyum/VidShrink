# Player Engine On libmpv

Player wave 0, part B. The player tab moves off the ffmpeg pipe (`DecoderPipe` plus the
NAudio `AudioSink`) onto libmpv, at today's feature parity. Every number below is copied
from test output written under `.calisma/oynatici-motor/` (not in git) by
`tests/VidShrink.Tests/OynaticiMotorTests.cs`, measured on commit `4ab27551` (after
merging `origin/main` at `0e9e84b2`). The loaded seek table and the threshold mutation
were measured earlier, on `ddb544be`, whose engine code differs only by the restart counter.
The seek completion rule changed after that (seeks now wait for their own
`MPV_EVENT_PLAYBACK_RESTART`); the table marked "after the restart condition" and the
landing race negative control were measured on `fa4e596d`, which carries that rule.

## Design

- **`src/VidShrink.Player`** (net8.0, new). `IPlaybackEngine` carries no libmpv type, so a
  LibVLC implementation can sit behind it if the osx-arm64 gate stays closed.
- **`MpvEngine`**:
  - `vo=libmpv`, `MPV_RENDER_API_TYPE_SW`, BGRA, stride `4*w`, buffers from
    `NativeMemory.AlignedAlloc(.., 64)`, double-buffered.
  - The update callback is `[UnmanagedCallersOnly]`; it only sets an event that wakes the
    `mpv-render` thread.
  - Per render.h "Threading", the render thread calls nothing but `mpv_render_*`. Video
    size is read on the `mpv-events` thread at `FILE_LOADED` / `VIDEO_RECONFIG`.
  - Audio is libmpv's own ao (`audio-fallback-to-null=yes` when no device exists).
- **Decoding and seeking.** `hwdec=no` by default, `auto-copy` as an option.
  `SeekPrecision.Exact` sends `absolute+exact`, `Keyframe` sends `absolute+keyframes`.
- **Seek completion** needs two things for the current generation, after its command
  reply and `MPV_EVENT_SEEK`:
  - the first new frame (`PRESENT` and not `REDRAW`) whose render started after the event,
    as in MpvBench;
  - that seek's `MPV_EVENT_PLAYBACK_RESTART`, at which the engine also re-reads `time-pos`.
  `Shown` is returned only when both have arrived, so `time-pos` and `PositionSeconds` read
  right after the return give the landed position. Latency runs from the command to the
  later of the two. The other outcomes are `Superseded`, `Failed` (error reply),
  `TimedOut` (3 s) and `Canceled`.
- **Handle lifetime.** Every caller-side libmpv call (`GetProperty`, `SetProperty`,
  commands, `AudioVideoOffsetSeconds`) runs under one lock and checks the handle inside it.
  `Dispose` joins both threads, then frees the render context and the handle under the
  same lock. A read racing `Dispose` gets `null` / `NaN`, never a freed handle.
- **`PlayerView`** keeps its `WriteableBitmap`. A 16 ms UI timer copies the latest frame
  through `TryCopyLatest`. Engine faults reuse the existing `main.error.unusable` text
  plus mpv's reason, so no locale key was added. The two player status texts that named
  the pipe were reworded in all 42 locale folders.
- **Unchanged.** `DecoderPipe`, `PreviewAudio` and the comparison panel stay on ffmpeg
  (wave 5). Shrink and Convert are untouched, and ffmpeg stays in the package.

## Supply

libmpv is not in any release archive, the same policy as FFmpeg: it arrives on the user's
machine at install time.

- **Windows.** `Install-VidShrink.ps1` downloads the pinned shinchiro build below, checks
  the archive hash, extracts `libmpv-2.dll` with the system `tar.exe`, checks the DLL hash
  and installs it to `<install root>\tools\libmpv\`. A re-run reuses an installed DLL whose
  hash matches. A test pins the installer's URL and both hashes to `ci.yml` and
  `release.yml`.
- **macOS.** `install-vidshrink.sh` looks for `libmpv.2.dylib` in `/opt/homebrew/lib`,
  `/usr/local/lib` and `/opt/local/lib`. If it is missing, it prints `brew install mpv`
  and stops before downloading anything, as it does for FFmpeg.
- **Linux.** The same script looks for `libmpv.so.2` via `ldconfig -p` and the usual lib
  folders, and otherwise prints the package command (`libmpv2` on apt and zypper,
  `mpv-libs` on dnf, `mpv` on pacman) and stops.
- **CI.** `ci.yml` and the release test job (`release.yml`) download the same pinned
  build, check both hashes and export `VIDSHRINK_LIBMPV`.

Locator order: `VIDSHRINK_LIBMPV` (a file or a folder); the app folder; its `tools/libmpv`;
the parent folder's `tools/libmpv` (the Windows install layout, where the app lives in
`app\`); on macOS, the Homebrew and MacPorts lib folders. The system search path comes last,
and is used only when the variable is unset.

Not done: bundling libmpv into the `osx-arm64` archive. Wave 0 A proved a Homebrew-based
bundle loads (`docs/olcumler/libmpv-macos-gomme.md`). But the same document leaves open:
- notarization;
- `minos` 15 on 47 of 48 dylibs;
- `osx-x64`;
- pinning a rolling Homebrew bottle;
- the licence texts and source offer for 48 GPL/LGPL libraries.

Shipping it is a release decision, not a player change. Windows installs that update
only through the launcher get libmpv when the installer is run once more.

| item | value |
|---|---|
| asset | `mpv-dev-x86_64-20260903-git-69e63f425a.7z`, release `20260903` of `shinchiro/mpv-winbuild-cmake` |
| archive sha256 | `FAC135C68A35B7639E39D72C0C365104EDBAEBDEA39A0DFDD8C36E8C8E80FAEF` (31363218 bytes) |
| `libmpv-2.dll` sha256 | `673E6397920AB64A9C5B3A618F7F16D38854EFE72B58665F1F84E4E873B763A4` |

**Risk.** shinchiro keeps a rolling window of about 30 releases, so this asset will
likely disappear around December 2026. The CI step then fails red on the download; it
does not skip. Re-pin to a newer asset, or mirror the archive to a repository release.

**Missing binary is red.** With `VIDSHRINK_LIBMPV=C:\yok\libmpv-2.dll`, two engine
tests ran: 2 failed, 0 skipped. Both failed with `PlaybackEngineUnavailableException`,
which lists the three paths tried.

## Acceptance

| item | result | number |
|---|---|---|
| headless frame decode | PASS | 320x180, stride 1280, 7625 distinct colours in the first frame, no Avalonia |
| frame reaches `PlayerView` | PASS | `WriteableBitmap` 320x180, 3 ms after open, playing |
| broken file fails on open | PASS | `PlaybackOpenException` in 4 ms, "unrecognized file format" |
| 10 fast wheel ticks (the pipe test's assertions, unchanged) | PASS | target 10, position 10, 2 seeks, 2.1 / 0.5 ms, none over 150 ms, none unshown |
| exact vs keyframe landing (GOP 2 s, paused, read at return) | PASS | exact 5.5 lands on 5.500; six keyframe seeks land on the keyframe below (5.5 → 4.000, 21.5 → 20.000); restart seen at every return; the frame differs from the opening frame |
| reads racing `Dispose` | PASS | 6 rounds, 3 reader threads: 0 exceptions, 50726-492745 reads per round returned `null` after `Dispose`, no crash |
| A/V after 10 s of playback ≤40 ms | PASS | median `time-pos - audio-pts` 23.4 ms (min 2.2, max 47.5, 100 samples, ao wasapi) |
| A/V negative control | PASS | `audio-delay=0.2` moves the median to 223.1 ms, a shift of 199.7 ms |

**Landing race (found in CI).** mpv reports `time-pos` as the seek target until playback
restarts. On the first CI run the keyframes seek rendered its frame before the core had
updated the position, so the test read 5.5 and failed. The engine now waits for that
seek's `MPV_EVENT_PLAYBACK_RESTART` before it returns `Shown` (see Seek completion).

The landing test no longer waits on its own. It runs one exact seek and six keyframe seeks.
Right at each return it reads `time-pos` and `PositionSeconds`, and asserts that
`PlaybackRestarts` had already grown. Results:

- exact 5.5 lands on 5.500;
- keyframes 5.5, 9.3, 13.1, 3.1, 17.1 and 21.5 land on 4, 8, 12, 2, 16 and 20;
- both readings are equal on every seek.

Negative control: the restart condition was removed from the engine, so it completed on the
frame alone, as before. The landing test was then run five times: 3 failed, 2 passed. In
every failure `Shown` came back before the restart. The reading was the target instead of
the landing: 13.100, 21.500 and 17.100 where a keyframe was due. In one failure
`PositionSeconds` (9.300) disagreed with `time-pos` (8.000). With the condition restored,
the test passed.

### Seek Latency

Protocol (MpvBench timed mode):
- 2 s of warm-up playback;
- then, while playing, 20 `absolute+exact` seeks to `1.0 + rng*(duration-4.0)` with
  `Random(1)`, 400 ms apart;
- `ao=null`, `hwdec=no`.

Media: testsrc2 plus sine, 35 s, GOP 2 s. H.264 uses x264 `fast`, crf 20; HEVC uses
x265 `fast`, crf 22. System busy % is taken from `GetSystemTimes` over the seek window.

Loaded machine (25-37 CPU-heavy processes running, so these are not the quiet reading):

| file | shown | median ms | min | max | busy % | threshold | result |
|---|---|---|---|---|---|---|---|
| h264_1080p60 | 20/20 | 34.1 | 22.8 | 67.9 | 78.4 | ≤60 | PASS |
| h264_2160p30 | 20/20 | 119.9 | 42.7 | 281.2 | 100.0 | ≤200 | PASS |
| hevc_1080p60 | 20/20 | 90.8 | 27.0 | 174.0 | 99.8 | none (pilot 2: 61.7) | reported |

Loaded machine, after the restart condition (merge commit `fa4e596d`, Debug build). Latency
now ends at the later of the first new frame and `MPV_EVENT_PLAYBACK_RESTART`.
`PerformanceProbe` let the two threshold tests run, but the seek window itself was 56-75 %
busy, so this is not the quiet reading either:

| file | shown | median ms | min | max | busy % | threshold | result |
|---|---|---|---|---|---|---|---|
| h264_1080p60 | 20/20 | 30.0 | 11.9 | 60.2 | 59.5 | ≤60 | PASS |
| h264_2160p30 | 20/20 | 107.1 | 49.8 | 206.2 | 74.8 | ≤200 | PASS |
| hevc_1080p60 | 20/20 | 86.7 | 22.8 | 183.4 | 56.4 | none (pilot 2: 61.7) | reported |

Quiet machine. The two threshold tests (1080p, 2160p) are `[HedefMakineFact]`: they always
skip in CI (`GITHUB_ACTIONS`) and run locally only when `PerformanceProbe` reads a light
software load. The HEVC test
has no threshold and stays a plain fact, so it runs in CI too:

| file | shown | median ms | min | max | busy % | threshold | result |
|---|---|---|---|---|---|---|---|
| h264_1080p60 | 20/20 | 31.9 | 17.4 | 59.6 | 1.7 | ≤60 | PASS |
| h264_2160p30 | 20/20 | 63.8 | 43.2 | 98.4 | 2.6 | ≤200 | PASS |
| hevc_1080p60 | 20/20 | 55.2 | 16.2 | 115.2 | 1.4 | none (pilot 2: 61.7) | reported |

HEVC 1080p re-measured: median 55.2 ms against the pilot's 61.7 ms, on the same protocol.

Quiet machine, after the restart condition (commit `ea8e25bc`, Release build, started after
three 5 s CPU samples at 1 %; all three tests ran, 3/3 passed):

| file | shown | median ms | min | max | busy % | threshold | result |
|---|---|---|---|---|---|---|---|
| h264_1080p60 | 20/20 | 32.8 | 17.7 | 57.2 | 1.5 | ≤60 | PASS |
| h264_2160p30 | 20/20 | 66.2 | 42.3 | 97.1 | 1.9 | ≤200 | PASS |
| hevc_1080p60 | 20/20 | 60.1 | 16.5 | 117.1 | 1.1 | none (pilot 2: 61.7) | reported |

Waiting for the restart moved the quiet medians by +0.9, +2.4 and +4.9 ms.

**CI runner.** On the first branch run
(https://github.com/Teknesyum/VidShrink/actions/runs/34605972205, `windows-latest`) the
same tests were plain facts and failed: 1080p median 85.2 ms (> 60) and 2160p median
273.9 ms (> 200). That is the hosted runner's decode speed, not a quiet desktop. So the
thresholds follow the repository's timing convention (`[QuietMachineFact]`, as in
`FrameGrabberTests`): the number is judged on a quiet machine, and the hosted runner is
not held to it. Missing libmpv still turns them red wherever they run.

`[QuietMachineFact]` alone was not enough: on main run
https://github.com/Teknesyum/VidShrink/actions/runs/34614432487 (`476b5d24`) the hosted
runner read as lightly loaded, both tests ran and failed at 75.6 ms (1080p) and 273.8 ms
(2160p). The gate now names the runner itself, `GITHUB_ACTIONS=true`, instead of inferring
it from load.

**Negative control for the threshold.** Adding `vd-lavc-threads=1` to the engine options
(a real regression: single-threaded decode) turned the 2160p test red. A matched
unmutated run under the same load stayed green:

| run | busy % | median ms | max | result |
|---|---|---|---|---|
| unmutated | 94.3 | 119.8 | 210.7 | PASS |
| `vd-lavc-threads=1` | 95.9 | 410.3 | 921.3 | FAIL (> 200) |

## Existing Player Tests

The player, `Oynatici*` and `Bicimin*` tests were run filtered, with the timing measures
excluded. The engine-backed tests passed. The ffmpeg pipe tests (`OynaticiBoruTests_*`,
`OynaticiGirdiTestsGercekBoru`) also ran; they test `DecoderPipe` itself, which this change
does not touch. Under load, three of them missed their timing and process limits in one
run (a 1786.5 ms pipe seek, 2 processes instead of 1, no `Faulted` within its window).
They are judged in CI, not locally under load.

## CI

Branch run on `4ab27551`: https://github.com/Teknesyum/VidShrink/actions/runs/34609230675,
`success`. Full suite: Failed 0, Passed 1987, Skipped 22, Total 2009 (run gate: minimum
total 1143, maximum skipped 30). The libmpv step downloaded the pinned build and both
hashes matched. The previous branch run skipped 20. In this run the only engine tests in
the skip list are the two threshold tests, and the run had 0 failures. The console log does
not name passed tests; the per-test record is the run's `kosum-sonuc` trx artifact.

Branch run on `ea8e25bc` (restart condition, handle lock, installer libmpv, merged with
`origin/main` at `7d404fc7`): https://github.com/Teknesyum/VidShrink/actions/runs/34612285286,
`success`. Full suite: Failed 0, Passed 1990, Skipped 22, Total 2012; the gate printed
`failed=0 total=2012 min=1143 skipped=22 max=30`. The only engine tests in the skip list are
again the two threshold tests.
