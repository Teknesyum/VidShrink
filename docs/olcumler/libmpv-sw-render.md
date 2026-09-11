# libmpv Software Render Path

Player wave 0, step 1 (pilot 2). Question: is libmpv's software render API
(`MPV_RENDER_API_TYPE_SW`, BGRA into a caller-owned buffer) fast enough on this machine to
back the player without a GPU interop path? No UI and no Avalonia are involved; the tool
renders into memory only.

Every number below comes from `tools/VidShrink.MpvBench` output. Raw output is kept under
`.calisma/mpvbench/` (not in git): `runs.jsonl`, `runs.log`, `rejected.jsonl`,
`ffmpeg-decode-bench.txt`, `ffprobe.txt`, `machine.txt`, and the two contaminated runs with their `summary.md`.

## Verdict

On the quiet rep, every file clears the SW render threshold by 9-28x (1080p ≥55, 2160p ≥24
frames/s), and timed playback dropped no frames at 60 and 30 frames/s. The exact-seek
median passes ≤60 ms only for H.264 1080p (36.1 ms). HEVC 1080p sits on the line (61.7 ms
with `hwdec=no`, 57.5 ms with auto-copy), and 2160p misses (77.7-194.5 ms).

An exact seek decodes from the previous keyframe to the target, so at 2160p the miss is
decoder and GOP cost, not the render path. Decision (`docs/netlestirme/011-...`):

- libmpv stays;
- the seek threshold is split: 1080p median ≤60 ms, 2160p exact median ≤ full-GOP decode
  time (≤200 ms on this setup);
- scrubbing seeks with `absolute+keyframes`, release with `exact`;
- default `hwdec=no`, `auto-copy` stays an option.

## Machine

| item | value |
|---|---|
| CPU | AMD Ryzen 7 9700X, 8 cores / 16 logical, MaxClockSpeed 3800 MHz |
| GPU | NVIDIA GeForce RTX 5070 Ti, driver 32.0.16.1656 |
| RAM | 64 GB (TotalVisibleMemorySize 66692532 KB) |
| OS | Windows 11 Pro 10.0.22631 |
| .NET | net8.0, win-x64, workstation GC |
| libmpv | `libmpv-2.dll` (shinchiro build), `mpv v0.41.0-1023-g69e63f425`, ffmpeg `N-126390-g9fc8c785e`, client API 131077 (2.5) |

The DLL is not in the repository. The tool takes its path from `--lib` or the
`VIDSHRINK_LIBMPV` environment variable. P/Invoke signatures were taken from the shipped
`include/mpv/client.h` and `include/mpv/render.h`.

## Test Media

Generated with ffmpeg 9.0 (gyan.dev full build) from `testsrc2` video and a `sine` audio
source, 35 s each, into `.calisma/mpvbench/media/`. The x264 settings below are read from
the SEI string embedded in the files. The x265 files carry no settings string, so only
what ffprobe and the packet flags show is claimed for them.

| file | video | size | fps | frames | bitrate | keyframes | audio |
|---|---|---|---|---|---|---|---|
| h264_1080p60.mp4 | H.264 High, yuv420p, x264 core 165, crf 20, `me=hex subme=6 ref=2 bframes=3`, keyint 120 | 1920x1080 | 60 | 2100 | 13.2 Mb/s | 18, every 2 s | AAC LC mono 48 kHz 128 kb/s |
| h264_2160p30.mp4 | H.264 High, yuv420p, same x264 settings, keyint 60 | 3840x2160 | 30 | 1050 | 31.2 Mb/s | 18, every 2 s | same |
| hevc_1080p60.mp4 | HEVC Main, yuv420p | 1920x1080 | 60 | 2100 | 14.4 Mb/s | 18, every 2 s | same |
| hevc_2160p30.mp4 | HEVC Main, yuv420p | 3840x2160 | 30 | 1050 | 35.5 Mb/s | 18, every 2 s | same |

`ffprobe` summary (`-show_entries stream=codec_name,profile,width,height,pix_fmt,r_frame_rate,nb_frames`)
is in `.calisma/mpvbench/ffprobe.txt`; keyframe times come from
`ffprobe -select_streams v:0 -show_entries packet=pts_time,flags`.

**testsrc2 is easy to decode.** A raw ffmpeg decode of the same files
(`ffmpeg -benchmark -i <file> -f null -`, 3 reps, `rtime`) takes 0.70-0.87 s for H.264
1080p, 1.39-1.44 s for H.264 2160p, 2.13-2.21 s for HEVC 1080p and 3.05-3.06 s for HEVC
2160p (`.calisma/mpvbench/ffmpeg-decode-bench.txt`). The untimed fps numbers
therefore measure the render path plus a light decoder, not a worst case for real footage.
Render cost per frame is reported separately so it can be read on its own.

## Method

Command:

    VidShrink.MpvBench.exe suite --lib <libmpv-2.dll> --media .calisma\mpvbench\media --out .calisma\mpvbench --reps 3

Each run is a separate child process, so startup and memory are per process. Order is
rep, then file, then hwdec (`no`, `auto-copy`), then mode (`fps`, `timed`). Seek targets
use seed = rep, so the three reps seek to three different target sets.

Render setup, identical in both modes: `mpv_render_context_create` with
`MPV_RENDER_PARAM_API_TYPE = "sw"`; each render passes `SW_SIZE` = source size
(`dwidth` x `dheight`), `SW_FORMAT = "bgra"`, `SW_STRIDE = 4 * width`, and `SW_POINTER` to
a buffer from `NativeMemory.AlignedAlloc(stride * height, 64)`. The update callback is an
`[UnmanagedCallersOnly]` cdecl function that signals the render thread, which calls
`mpv_render_context_update` and renders on `MPV_RENDER_UPDATE_FRAME`.

mpv options (defaults otherwise):

| mode | options |
|---|---|
| fps (untimed) | `vo=libmpv untimed=yes audio=no hwdec=<no or auto-copy> framedrop=no keep-open=yes terminal=no`; render without `BLOCK_FOR_TARGET_TIME` |
| timed | `vo=libmpv ao=null hwdec=<no or auto-copy> keep-open=yes terminal=no`; render with `BLOCK_FOR_TARGET_TIME = 1` |

Defaults read back from the player: `sws-fast=no`, `sws-allow-zimg=yes`,
`vd-lavc-threads=0` (auto). `hwdec=auto-copy` resolved to `d3d11va-copy` in every run.

What each number is:

- **Max SW render rate**: fps mode, whole file until end of file. fps = (frames − 1) /
  (last render end − first render end). Frame timing comes from `NEXT_FRAME_INFO`; a frame
  counts as new when it is `PRESENT` and not `REDRAW`.
- **Seek latency**: timed mode, after a 2 s warm-up and a 15 s playback window. 20 targets
  drawn uniformly from [1, duration − 3] s, sent as `seek <t> absolute+exact` with
  `mpv_command_async`, 400 ms apart. Latency runs from the command call to the end of the
  render of the first new frame whose render started after `MPV_EVENT_SEEK` arrived. A frame
  rendered between the command and the event is counted as a stale frame. `time-pos` is read
  after that render and compared with the target.
- **Startup**: `mpv_create` to the end of the first render. DLL load is timed separately.
- **Memory**: process working set sampled every 50 ms, plus `PeakWorkingSet64` at exit.
- **CPU**: process CPU over the 15 s timed playback window. Measured with
  `QueryProcessCycleTime` and converted to seconds with a cycle rate calibrated by a
  busy-spinning thread (`QueryThreadCycleTime`, best of 3 × 200 ms). The summary divides
  every run's cycles by the highest calibrated rate across all runs. The TSC is invariant,
  and preemption can only make a calibration read low. `GetProcessTimes` is reported
  beside it but is not used: it samples on the 15.625 ms scheduler tick and misses short
  render bursts (in a quiet check it read 0.146 % where cycles read 2.93 %).
- **System busy**: `GetSystemTimes` over the fps run, the timed window and the seek phase.
  It includes the tool itself.

### Quiet-machine gate

This machine runs unrelated batch jobs: MurphyEngine `base.exe`, `murphy-f1a.exe` and
`murphy-f2a.exe`, plus stockfish. They come in bursts of 15-35 processes that hold the
CPU near 100 % for minutes at a time. Two full suites were contaminated by them
(`.calisma/mpvbench/run1-loaded/`, `run2-partly-loaded/`), and the numbers from those
runs are not used as a whole. A third, gated suite was written for that reason:

- before starting, it waits until system busy over a 3 s sample is at most 10 %;
- after the run it rejects and repeats the run when that run's own system busy exceeds 30 %
  (fps) or 15 % (timed window and seek phase), up to 10 retries.

The limits come from rep 1 of the contaminated run 2, which ran on a quiet machine: the
tool alone put system busy at 3.9-16.4 % during fps runs and 1.2-5.4 % during timed runs.
Rejected attempts are kept in `rejected.jsonl`.

The gated suite started at 15:49, again at 15:55, and was stopped both times with no
accepted run: the batch load did
not drop below the 10 % gate. Netlestirme 011 ruled that rep 1 of run 2 is enough for the
engine decision, and HEVC 1080p seek is measured again in wave 0 with the same tool.

## Results

The tables are run 2 (`.calisma/mpvbench/run2-partly-loaded/summary.md`), unchanged. **Read
rep 1 only.** Reps 2 and 3 ran under the batch load, so the median, min and max columns mix
loaded runs and are not used. The last column of every table is system busy per rep and
shows which cells are clean.

Rep 1 of `hevc_2160p30.mp4` was loaded too (system busy 86-100 % in the auto-copy fps run,
both seek phases and the `hwdec=no` timed window). Its numbers are an upper bound: load can
only make a seek or a frame slower.

### Max SW Render Rate (untimed)

| file | hwdec | hwdec-current | fps per rep | median | min | max | threshold | result | render ms/frame (median of reps) | frames per rep | stop | drops (vo/dec) | system busy % per rep |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| h264_1080p60.mp4 | no | no | 1547.4 / 1009.4 / 1046.1 | 1046.1 | 1009.4 | 1547.4 | >= 55 | PASS | 0.59 | 2098 / 2100 / 2099 | eof | 1/0 / 1/0 / 1/0 | 11.4 / 100.0 / 98.1 |
| h264_1080p60.mp4 | auto-copy | d3d11va-copy | 1569.3 / 934.3 / 982.9 | 982.9 | 934.3 | 1569.3 | >= 55 | PASS | 0.50 | 2099 / 2100 / 2100 | eof | 1/0 / 1/0 / 1/0 | 3.9 / 96.6 / 96.9 |
| h264_2160p30.mp4 | no | no | 396.5 / 266.6 / 269.2 | 269.2 | 266.6 | 396.5 | >= 24 | PASS | 2.31 | 1050 / 1050 / 1050 | eof | 1/0 / 1/0 / 1/0 | 16.4 / 98.5 / 98.3 |
| h264_2160p30.mp4 | auto-copy | d3d11va-copy | 429.8 / 254.8 / 302.5 | 302.5 | 254.8 | 429.8 | >= 24 | PASS | 2.01 | 1050 / 1050 / 1050 | eof | 1/0 / 1/0 / 1/0 | 4.7 / 100.0 / 98.3 |
| hevc_1080p60.mp4 | no | no | 1006.6 / 589.1 / 1008.3 | 1006.6 | 589.1 | 1008.3 | >= 55 | PASS | 0.37 | 2098 / 2099 / 2099 | eof | 1/0 / 1/0 / 1/0 | 5.9 / 98.6 / 5.8 |
| hevc_1080p60.mp4 | auto-copy | d3d11va-copy | 1477.3 / 701.7 / 1401.6 | 1401.6 | 701.7 | 1477.3 | >= 55 | PASS | 0.37 | 2099 / 2100 / 2099 | eof | 1/0 / 1/0 / 1/0 | 4.4 / 97.6 / 4.2 |
| hevc_2160p30.mp4 | no | no | 278.6 / 184.6 / 173.7 | 184.6 | 173.7 | 278.6 | >= 24 | PASS | 2.40 | 1050 / 1050 / 1050 | eof | 1/0 / 1/0 / 1/0 | 13.5 / 99.3 / 99.2 |
| hevc_2160p30.mp4 | auto-copy | d3d11va-copy | 215.0 / 228.8 / 214.0 | 215.0 | 214.0 | 228.8 | >= 24 | PASS | 2.24 | 1049 / 1050 / 1050 | eof | 1/0 / 1/0 / 1/0 | 100.0 / 98.6 / 99.1 |

### Seek Latency (timed, `seek <t> absolute+exact`, command to first rendered frame)

| file | hwdec | seeks ok/failed | median of all | p90 of all | min | max | per-rep medians | threshold | result | restart-event median | time-pos - target ms (min..max) | stale frames | system busy % during seeks per rep |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| h264_1080p60.mp4 | no | 60/0 | 38.0 | 63.5 | 20.1 | 86.8 | 36.1 / 41.0 / 33.5 | <= 60 | PASS | 37.9 | 0.0..43.0 | 60 | 1.7 / 80.6 / 85.3 |
| h264_1080p60.mp4 | auto-copy | 60/0 | 54.4 | 116.6 | 16.8 | 160.6 | 43.2 / 72.2 / 40.0 | <= 60 | PASS | 54.4 | 0.0..27.7 | 60 | 2.0 / 73.6 / 75.0 |
| h264_2160p30.mp4 | no | 60/0 | 107.8 | 201.7 | 52.9 | 249.5 | 77.7 / 125.4 / 122.1 | <= 60 | FAIL | 107.5 | 0.0..61.0 | 61 | 3.7 / 95.3 / 92.4 |
| h264_2160p30.mp4 | auto-copy | 60/0 | 102.8 | 210.8 | 41.5 | 232.6 | 100.1 / 114.0 / 99.0 | <= 60 | FAIL | 101.8 | 0.0..61.0 | 60 | 2.4 / 84.0 / 27.3 |
| hevc_1080p60.mp4 | no | 60/0 | 70.0 | 153.9 | 17.6 | 191.4 | 61.7 / 104.0 / 57.0 | <= 60 | FAIL | 69.9 | 0.0..44.0 | 60 | 2.1 / 86.7 / 1.7 |
| hevc_1080p60.mp4 | auto-copy | 60/0 | 84.7 | 164.6 | 19.6 | 192.8 | 57.5 / 96.5 / 88.9 | <= 60 | FAIL | 84.9 | -3.3..42.7 | 60 | 5.4 / 79.4 / 82.0 |
| hevc_2160p30.mp4 | no | 60/0 | 174.4 | 280.7 | 72.8 | 317.5 | 172.0 / 182.9 / 172.7 | <= 60 | FAIL | 176.0 | 0.0..145.3 | 61 | 100.0 / 96.7 / 99.1 |
| hevc_2160p30.mp4 | auto-copy | 60/0 | 193.0 | 330.6 | 51.7 | 377.9 | 194.5 / 208.4 / 187.4 | <= 60 | FAIL | 192.4 | 0.0..145.3 | 61 | 99.9 / 84.9 / 87.7 |

### Startup (`mpv_create` to first rendered frame, ms; DLL load excluded)

| file | hwdec | mode | per rep | median | min | max | DLL load ms (median) |
|---|---|---|---|---|---|---|---|
| h264_1080p60.mp4 | no | fps | 242.1 / 372.8 / 296.6 | 296.6 | 242.1 | 372.8 | 13.7 |
| h264_1080p60.mp4 | no | timed | 245.6 / 396.7 / 346.9 | 346.9 | 245.6 | 396.7 | 13.1 |
| h264_1080p60.mp4 | auto-copy | fps | 342.7 / 476.9 / 470.7 | 470.7 | 342.7 | 476.9 | 16.0 |
| h264_1080p60.mp4 | auto-copy | timed | 334.8 / 468.1 / 469.1 | 468.1 | 334.8 | 469.1 | 12.5 |
| h264_2160p30.mp4 | no | fps | 313.4 / 310.6 / 318.7 | 313.4 | 310.6 | 318.7 | 14.1 |
| h264_2160p30.mp4 | no | timed | 307.7 / 356.7 / 345.9 | 345.9 | 307.7 | 356.7 | 11.7 |
| h264_2160p30.mp4 | auto-copy | fps | 392.9 / 608.7 / 500.5 | 500.5 | 392.9 | 608.7 | 12.1 |
| h264_2160p30.mp4 | auto-copy | timed | 358.2 / 520.0 / 500.2 | 500.2 | 358.2 | 520.0 | 14.0 |
| hevc_1080p60.mp4 | no | fps | 241.4 / 281.5 / 249.5 | 249.5 | 241.4 | 281.5 | 7.2 |
| hevc_1080p60.mp4 | no | timed | 250.8 / 320.5 / 248.2 | 250.8 | 248.2 | 320.5 | 8.0 |
| hevc_1080p60.mp4 | auto-copy | fps | 340.8 / 467.1 / 341.7 | 341.7 | 340.8 | 467.1 | 6.2 |
| hevc_1080p60.mp4 | auto-copy | timed | 358.7 / 560.9 / 332.4 | 358.7 | 332.4 | 560.9 | 6.9 |
| hevc_2160p30.mp4 | no | fps | 285.3 / 433.7 / 345.8 | 345.8 | 285.3 | 433.7 | 12.1 |
| hevc_2160p30.mp4 | no | timed | 317.0 / 407.6 / 368.9 | 368.9 | 317.0 | 407.6 | 13.6 |
| hevc_2160p30.mp4 | auto-copy | fps | 705.9 / 504.1 / 553.5 | 553.5 | 504.1 | 705.9 | 14.5 |
| hevc_2160p30.mp4 | auto-copy | timed | 554.8 / 509.0 / 527.1 | 527.1 | 509.0 | 554.8 | 13.8 |

### Memory (process working set, MB; sampled every 50 ms, and PeakWorkingSet64)

| file | hwdec | mode | sampled peak per rep | median | min | max | PeakWorkingSet64 median | baseline before mpv_create (median) |
|---|---|---|---|---|---|---|---|---|
| h264_1080p60.mp4 | no | fps | 179.6 / 180.4 / 180.7 | 180.4 | 179.6 | 180.7 | 180.4 | 27.7 |
| h264_1080p60.mp4 | no | timed | 186.3 / 186.6 / 186.5 | 186.5 | 186.3 | 186.6 | 187.2 | 29.0 |
| h264_1080p60.mp4 | auto-copy | fps | 144.1 / 141.3 / 142.3 | 142.3 | 141.3 | 144.1 | 142.4 | 27.7 |
| h264_1080p60.mp4 | auto-copy | timed | 162.5 / 161.8 / 159.4 | 161.8 | 159.4 | 162.5 | 161.8 | 29.0 |
| h264_2160p30.mp4 | no | fps | 552.7 / 554.9 / 555.1 | 554.9 | 552.7 | 555.1 | 554.9 | 27.7 |
| h264_2160p30.mp4 | no | timed | 556.1 / 555.3 / 555.1 | 555.3 | 555.1 | 556.1 | 556.0 | 29.0 |
| h264_2160p30.mp4 | auto-copy | fps | 321.3 / 309.5 / 309.6 | 309.6 | 309.5 | 321.3 | 309.6 | 27.7 |
| h264_2160p30.mp4 | auto-copy | timed | 353.2 / 357.5 / 365.6 | 357.5 | 353.2 | 365.6 | 358.3 | 29.1 |
| hevc_1080p60.mp4 | no | fps | 206.0 / 206.3 / 206.1 | 206.1 | 206.0 | 206.3 | 207.1 | 27.7 |
| hevc_1080p60.mp4 | no | timed | 218.5 / 218.6 / 218.3 | 218.5 | 218.3 | 218.6 | 219.3 | 29.1 |
| hevc_1080p60.mp4 | auto-copy | fps | 142.7 / 140.4 / 142.2 | 142.2 | 140.4 | 142.7 | 142.2 | 27.7 |
| hevc_1080p60.mp4 | auto-copy | timed | 168.8 / 169.1 / 166.7 | 168.8 | 166.7 | 169.1 | 168.8 | 29.1 |
| hevc_2160p30.mp4 | no | fps | 672.4 / 673.1 / 673.1 | 673.1 | 672.4 | 673.1 | 673.1 | 27.6 |
| hevc_2160p30.mp4 | no | timed | 705.5 / 706.0 / 706.0 | 706.0 | 705.5 | 706.0 | 706.0 | 29.0 |
| hevc_2160p30.mp4 | auto-copy | fps | 324.0 / 323.3 / 323.7 | 323.7 | 323.3 | 324.0 | 323.7 | 27.7 |
| hevc_2160p30.mp4 | auto-copy | timed | 414.6 / 415.0 / 416.9 | 415.0 | 414.6 | 416.9 | 415.2 | 29.0 |

### CPU (timed playback window; QueryProcessCycleTime cycles / 3795 MHz, the highest calibrated cycle rate of all runs / wall time; 16 logical cores)

| file | hwdec | % of all cores per rep | median | min | max | % of one core (median) | tick-based GetProcessTimes % of all cores (median) | calibrated MHz per rep | window s (median) | frames rendered in window per rep | window fps (median) | drops vo/dec per rep | system busy % in window per rep |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| h264_1080p60.mp4 | no | 2.72 / 3.76 / 3.77 | 3.76 | 2.72 | 3.77 | 60.1 | 1.91 | 3679 / 3544 / 3792 | 15.02 | 901 / 901 / 901 | 60.0 | 0/0 / 0/0 / 0/0 | 1.2 / 82.4 / 76.8 |
| h264_1080p60.mp4 | auto-copy | 2.60 / 2.78 / 2.80 | 2.78 | 2.60 | 2.80 | 44.5 | 1.20 | 3779 / 3684 / 3720 | 15.02 | 900 / 901 / 901 | 60.0 | 0/0 / 0/0 / 0/0 | 1.6 / 70.4 / 71.0 |
| h264_2160p30.mp4 | no | 5.57 / 7.01 / 7.07 | 7.01 | 5.57 | 7.07 | 112.2 | 5.16 | 3782 / 3646 / 3709 | 15.00 | 450 / 450 / 451 | 30.0 | 0/0 / 0/0 / 0/0 | 2.9 / 85.6 / 86.6 |
| h264_2160p30.mp4 | auto-copy | 3.94 / 4.19 / 4.59 | 4.19 | 3.94 | 4.59 | 67.1 | 3.20 | 3778 / 3716 / 3714 | 15.00 | 451 / 450 / 450 | 30.0 | 0/0 / 0/0 / 0/0 | 2.5 / 87.3 / 75.0 |
| hevc_1080p60.mp4 | no | 2.99 / 4.27 / 2.81 | 2.99 | 2.81 | 4.27 | 47.8 | 0.28 | 3795 / 3139 / 3770 | 15.02 | 901 / 900 / 901 | 60.0 | 0/0 / 1/0 / 0/0 | 2.0 / 83.9 / 1.0 |
| hevc_1080p60.mp4 | auto-copy | 2.61 / 2.88 / 2.60 | 2.61 | 2.60 | 2.88 | 41.8 | 1.40 | 3736 / 3769 / 3784 | 15.02 | 900 / 901 / 901 | 60.0 | 0/0 / 0/0 / 0/0 | 63.5 / 67.9 / 36.4 |
| hevc_2160p30.mp4 | no | 8.13 / 8.36 / 8.35 | 8.35 | 8.13 | 8.36 | 133.6 | 7.77 | 3792 / 3573 / 3709 | 15.03 | 451 / 451 / 451 | 30.0 | 0/0 / 0/0 / 0/0 | 86.4 / 93.4 / 95.0 |
| hevc_2160p30.mp4 | auto-copy | 4.49 / 4.97 / 4.83 | 4.83 | 4.49 | 4.97 | 77.2 | 3.96 | 3644 / 3723 / 3723 | 15.03 | 450 / 451 / 451 | 30.0 | 0/0 / 0/0 / 0/0 | 99.9 / 74.3 / 80.7 |

### Sanity

- runs: 48 (fps 24, timed 24)
- mpv-version: mpv v0.41.0-1023-g69e63f425
- ffmpeg-version: N-126390-g9fc8c785e
- sw-fast: ; sws-fast: no; sws-allow-zimg: yes; vd-lavc-threads: 0
- current-vo: libmpv; current-ao (timed): null
- render size / stride / 64-byte aligned buffer: 1920x1080/7680/True, 3840x2160/15360/True
- NEXT_FRAME_INFO rc: 0; render errors total: 0
- options fps: vo=libmpv untimed=yes audio=no hwdec=no framedrop=no keep-open=yes terminal=no | vo=libmpv untimed=yes audio=no hwdec=auto-copy framedrop=no keep-open=yes terminal=no
- options timed: vo=libmpv ao=null hwdec=no keep-open=yes terminal=no | vo=libmpv ao=null hwdec=auto-copy keep-open=yes terminal=no
- mpv warnings/errors logged: 0

## Caveats

- **Content.** testsrc2 decodes far faster than real footage. The fps margins are an upper
  bound. The per-frame render cost (`render ms/frame`) is the part that carries over.
- **GOP.** Every file has a keyframe every 2 s, so an exact seek decodes up to 2 s of
  frames. At 2160p30 that is up to 60 frames. Longer GOPs in real files will seek slower.
- **LibVLC comparison.** The LibVLC numbers in `oynatici-hatti.md` (K1: medians
  57.2 / 48.2 / 54.5 / 38.9 ms for 1 / 10 / 60 / 300 s jumps) were measured differently:
  real content (`parca-1.mkv`, `parca-1-uzun.mkv`), 5 repetitions, and frame fingerprint
  correlation as the end criterion. Comparing them with these numbers is indicative only.
- **auto-copy is d3d11va-copy.** The hardware decoder copies frames back to system memory,
  and the software renderer then converts them to BGRA on the CPU. It is not a zero-copy path.
