# Changelog

All notable changes to VidShrink Player are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow
[Semantic Versioning](https://semver.org/).

The history of the program under its old name, VidShrink, up to 1.2.24 and the changes
made after it, is in [`docs/CHANGELOG-VidShrink.md`](docs/CHANGELOG-VidShrink.md).

## [Unreleased]

### Added

- An offline installer archive for Windows, `VidShrinkPlayer-Offline-win-x64.zip` and
  `VidShrinkPlayer-Offline-win-arm64.zip`. It carries the installer, the application, FFmpeg and
  libmpv; the installer reads the `vidshrink-offline.txt` marker beside it and installs the tools
  from the archive without downloading. Each tool is checked against the sha256 pinned in the
  installer; a missing or mismatched file is downloaded instead, and with no connection the
  installer stops with a clear error. An archive for the other architecture is refused by name.
- The FFmpeg (GPLv3) and libmpv (GPLv2+) licence texts, the Vulkan loader licence and a
  `THIRD-PARTY-NOTICES.txt` with the build and source links ship in the archive's `licenses`
  folder and beside it in the release as `offline-<rid>-*.txt`.

## [0.0.1]

### Changed

- VidShrink is now VidShrink Player. The program window and the documents use the new
  name.
- The repository is now [Teknesyum/VidShrink-Player](https://github.com/Teknesyum/VidShrink-Player).
- Version numbers start again at `0.0.1`. The last release under the old name was 1.2.24;
  everything listed under its Unreleased section in the old changelog ships in this release.

### Moving From VidShrink

- One manual step, once. If your VidShrink install does not offer VidShrink Player as an
  update, install it once with any of the ways in the [README](README.md#install). Updates
  after that arrive as before.
