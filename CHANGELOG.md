# Changelog

All notable changes to VidShrink Player are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow
[Semantic Versioning](https://semver.org/).

The history of the program under its old name, VidShrink, up to 1.2.24 and the changes
made after it, is in [`docs/CHANGELOG-VidShrink.md`](docs/CHANGELOG-VidShrink.md).

## [Unreleased]

### Added

- The recorder can hide the desktop icons while recording (Windows, off by default). The icons come
  back when the recording ends or is discarded, and on the next start if the program closed
  unexpectedly. Icons that were already hidden are left alone. On macOS and Linux the option is
  shown disabled with a hint. Muting notifications during recording is not offered: Windows has no
  documented way to do it. "Reset all data" brings hidden icons back first and then removes the
  saved state; the state is never part of a settings backup. To keep the recorder page free of
  scrolling at 1024 px, the cursor options sit closer together and the replay button lost its
  trailing gap.

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
