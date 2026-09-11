# Gate A: Embedding libmpv In The osx-arm64 Package

Player wave 0, gate A. Question: can libmpv and everything it links against be shipped inside
the self-contained osx-arm64 publish output, load from there with Homebrew gone, and draw a
frame through the software render API (`MPV_RENDER_API_TYPE_SW`, BGRA, stride 4*w, 64-byte
aligned buffer)? Decision rule (docs/netlestirme/010, 011): pass in one round keeps libmpv as
the engine; fail sends every platform to LibVLC.

**Result: PASSED.** First push, no fix-up pushes.

- Run: https://github.com/Teknesyum/VidShrink/actions/runs/34602526409 (conclusion `success`)
- Commit: `4c99fa23` on `worktree-agent-a230c98d55c76529a`
- Workflow: `.github/workflows/macos-libmpv-gate.yml`
- Tools: `tools/VidShrink.MpvSmoke/` (smoke program, `bundle-macos.py`)

Every number below is copied from that run's log.

## Runner

| Item | Value |
|---|---|
| Runner label | `macos-15` |
| macOS | ProductVersion 15.7.9, Darwin 24.6.0, arm64 (VMAPPLE) |
| Homebrew | 6.0.22, prefix `/opt/homebrew` |
| .NET | 8.0.30 (self-contained osx-arm64) |

`macos-15` rather than `macos-14`: Homebrew builds bottles only for its newest macOS
releases, and a Sonoma runner risked building mpv from source.

## Source And Version

libmpv comes from Homebrew bottles. No better prebuilt arm64 libmpv dylib source was tried in
this round (see open items).

| Formula | Installed | License (Homebrew metadata) |
|---|---|---|
| mpv | 0.41.0_9 | GPL-2.0-or-later AND LGPL-2.1-or-later |
| ffmpeg | 9.0.1_1 | GPL-3.0-or-later |

- `libmpv.2.dylib`: Mach-O 64-bit dynamically linked shared library arm64
- sha256 of the Homebrew original (`/opt/homebrew/Cellar/mpv/0.41.0_9/lib/libmpv.2.dylib`):
  `5d4d84b2742c19d25a11331c4e21ec8adafc49bf5678d58d4d1a2e6e995877b8`
- sha256 of the bundled copy after rewrite and re-sign:
  `82628e0de4c6453cf27942306625017a83fffe1759b3ed453917d6adc69b37da`
- `brew deps --installed mpv`: 73 formulae; 48 dylibs are actually reachable through load
  commands from `libmpv.2.dylib`.
- Reported at runtime: client API 2.5, `mpv v0.41.0`, ffmpeg `9.0.1`.

Homebrew verifies each bottle's sha256 on install. The two hashes above are recorded so a
release job can pin them.

## Steps

1. `brew install mpv` (with `HOMEBREW_NO_AUTO_UPDATE=1`).
2. Test clip: `ffmpeg -f lavfi -i testsrc2=size=640x360:rate=30 -t 3 -c:v libx264 -pix_fmt yuv420p clip.mp4`
   (296376 B).
3. `dotnet publish src/VidShrink.App -c Release -r osx-arm64 --self-contained -o publish/osx-arm64`,
   the same command release.yml runs, and the same for the smoke tool into `smoke/`.
4. `python3 tools/VidShrink.MpvSmoke/bundle-macos.py <brew libmpv.2.dylib> publish/osx-arm64`:
   a breadth-first walk over `otool -L`. It resolves `@rpath`/`@loader_path` against each
   file's own `LC_RPATH`. It copies every dependency under `/opt/homebrew/` or `/usr/local/`
   flat next to the app, then runs `install_name_tool -id @rpath/<name>`,
   `-change <old> @loader_path/<name>` and `-delete_rpath` on each foreign rpath. Last,
   `codesign --force --sign -` (ad-hoc).
5. Evidence pass over all 48 files: `otool -L`, `otool -l` (LC_RPATH, LC_BUILD_VERSION minos),
   `lipo -archs`, `codesign --verify`. The step fails if any `/opt/homebrew` or `/usr/local`
   path remains.
6. `sudo mv /opt/homebrew /opt/homebrew-hidden`. `/usr/local/Cellar`, `/usr/local/opt` and
   `/usr/local/lib` were queued to be hidden too, but none of them existed on this runner. Log:
   `/opt/homebrew exists: no`.
7. Smoke run from `smoke/` with `--lib publish/osx-arm64/libmpv.2.dylib`, run a second time
   under `DYLD_PRINT_LIBRARIES=1`.

## otool Evidence

`libmpv.2.dylib` before (Homebrew) and after (bundled). Both list the install id plus the same 61
dependency entries in the same order; only the paths shown below differ. The system frameworks, `/usr/lib/*` and `/usr/lib/swift/*`
entries are unchanged and left out below:

```
before                                                    after
/opt/homebrew/opt/mpv/lib/libmpv.2.dylib (id)             @rpath/libmpv.2.dylib (id)
/opt/homebrew/opt/libass/lib/libass.9.dylib               @loader_path/libass.9.dylib
/opt/homebrew/opt/ffmpeg/lib/libavcodec.63.dylib          @loader_path/libavcodec.63.dylib
/opt/homebrew/opt/ffmpeg/lib/libavfilter.12.dylib         @loader_path/libavfilter.12.dylib
/opt/homebrew/opt/ffmpeg/lib/libavformat.63.dylib         @loader_path/libavformat.63.dylib
/opt/homebrew/opt/ffmpeg/lib/libavutil.61.dylib           @loader_path/libavutil.61.dylib
/opt/homebrew/opt/libplacebo/lib/libplacebo.360.dylib     @loader_path/libplacebo.360.dylib
/opt/homebrew/opt/ffmpeg/lib/libswresample.7.dylib        @loader_path/libswresample.7.dylib
/opt/homebrew/opt/ffmpeg/lib/libswscale.10.dylib          @loader_path/libswscale.10.dylib
/opt/homebrew/opt/mujs/lib/libmujs.dylib                  @loader_path/libmujs.dylib
/opt/homebrew/opt/little-cms2/lib/liblcms2.2.dylib        @loader_path/liblcms2.2.dylib
/opt/homebrew/opt/libarchive/lib/libarchive.13.dylib      @loader_path/libarchive.13.dylib
/opt/homebrew/opt/ffmpeg/lib/libavdevice.63.dylib         @loader_path/libavdevice.63.dylib
/opt/homebrew/opt/libbluray/lib/libbluray.4.dylib         @loader_path/libbluray.4.dylib
/opt/homebrew/opt/luajit/lib/libluajit-5.1.2.dylib        @loader_path/libluajit-5.1.2.dylib
/opt/homebrew/opt/rubberband/lib/librubberband.3.dylib    @loader_path/librubberband.3.dylib
/opt/homebrew/opt/uchardet/lib/libuchardet.0.dylib        @loader_path/libuchardet.0.dylib
/opt/homebrew/opt/zimg/lib/libzimg.2.dylib                @loader_path/libzimg.2.dylib
/opt/homebrew/opt/jpeg-turbo/lib/libjpeg.8.dylib          @loader_path/libjpeg.8.dylib
/opt/homebrew/opt/vulkan-loader/lib/libvulkan.1.dylib     @loader_path/libvulkan.1.dylib
```

Across all 48 bundled files, per-file result lines have the form
`lib <name> archs=arm64 minos=<v> foreign-refs=0 foreign-rpaths=0 sig=ok`, and the combined
grep over every `otool -L` and `otool -l` output for `/opt/homebrew|/usr/local` printed
`(none)`, then `foreign-total=0`.

## Smoke Output

Run with `/opt/homebrew` renamed away:

```
smoke: os=Darwin 24.6.0 ... RELEASE_ARM64_VMAPPLE arch=Arm64 runtime=.NET 8.0.30
smoke: lib=/Users/runner/work/VidShrink/VidShrink/publish/osx-arm64/libmpv.2.dylib
smoke: client-api=2.5
smoke: mpv-version=mpv v0.41.0
smoke: ffmpeg-version=9.0.1
smoke: buffer 640x360 stride=2560 aligned64=True
smoke: file-loaded at 303 ms
smoke: frames-rendered=3 last-rc=0 nonzero-pixels=230400/230400
smoke: center-pixel bgra=(255,0,0,255) fnv1a=5e09f22a923ddf6b
smoke: PASS
```

Options: `config=no vo=libmpv hwdec=no ao=null audio=no`. The second run under
`DYLD_PRINT_LIBRARIES=1` also printed `smoke: PASS` and
`dyld loaded images total=1640 from-bundle=48 from-homebrew=0`. All 48 bundled dylibs were
loaded from `publish/osx-arm64/`, none from `/opt/homebrew` or `/usr/local`. The total
includes the system and .NET runtime images.

## Size

Measured in the same run, before and after bundling. The zip is built with the same
`cd publish/<rid> && zip -qr` release.yml uses.

| | Files | Unpacked bytes | Zip bytes |
|---|---|---|---|
| osx-arm64 publish, before | 398 | 116,385,991 | 45,783,852 |
| osx-arm64 publish, after | 446 | 181,630,439 | 72,909,508 |
| Difference | +48 | +65,244,448 (+56.1%) | +27,125,656 (+59.2%) |
| The 48 dylibs alone | 48 | 65,244,448 | 27,125,678 |

The brief's earlier figure of 116,436,898 B came from a different commit; this run's
"before" row is the baseline for the comparison.

Largest bundled files: libavcodec.63 9,849,344; libx265.217 7,701,744; libshaderc_shared.1
7,661,504; libcrypto.3 4,888,816; libmpv.2 4,512,080; libSvtAv1Enc.4 3,108,528;
libavfilter.12 3,064,784. Encoder libraries pulled in by Homebrew's ffmpeg (x265, SVT-AV1,
x264, lame, vmaf) add 13,260,640 B and play no part in decoding. This round did not test
dropping them.

## Open Items

- **Signing and notarization.** Only ad-hoc signatures were tested. A release needs Developer
  ID signing of all 48 dylibs plus the app host. Under hardened runtime, library validation
  requires the same Team ID on every dylib, and the package then needs notarization. Neither
  was tried. Gatekeeper quarantine was not exercised either: CI files carry no quarantine
  attribute.
- **Minimum macOS 15.** 47 of 48 dylibs carry `minos=15.0` (librubberband 11.0), because
  Homebrew bottles target the runner's OS. This package will not load on macOS 13 or 14.
  Lowering that needs a source build with `MACOSX_DEPLOYMENT_TARGET`, or another binary
  source.
- **Not universal.** Every file is `arm64` only. osx-x64 needs its own x86_64 set from an
  Intel runner; that was not tested.
- **Supply-chain pinning.** Homebrew bottles roll forward (mpv revision 9 today). The release
  job would have to pin bottle URLs and sha256, or build libmpv itself, to be reproducible.
- **Size trimming.** An LGPL, decode-only libmpv/ffmpeg build without the encoders, shaderc
  or vulkan-loader would be far smaller. Candidate prebuilt source not tried here: MPVKit
  xcframeworks.
- **License.** Homebrew's ffmpeg is GPL-3.0-or-later (x264, x265 enabled) and mpv is
  GPL-2.0-or-later AND LGPL-2.1-or-later. Both are compatible with AGPL-3.0 through GPLv3
  section 13. Distribution then requires shipping every bundled library's license text and
  the corresponding source (or a written offer) for all 48. OpenSSL 3 is Apache-2.0, which
  is compatible with GPLv3.
- **Scope.** The smoke tool loaded the libmpv bundled into the application's publish folder.
  VidShrink.App itself does not load libmpv yet, and no `.app` bundle
  (`Contents/Frameworks`) layout was tested; `@loader_path` holds in either layout as long
  as the dylibs stay together.
