# Changelog

All notable changes to VidShrink Player are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow
[Semantic Versioning](https://semver.org/).

The history of the program under its old name, VidShrink, up to 1.2.24 and the changes
made after it, is in [`docs/CHANGELOG-VidShrink.md`](docs/CHANGELOG-VidShrink.md).

## [Unreleased]

### Added

- Auto zoom after recording. Turn on "Record pointer for auto zoom" from the arrow next to
  Magnifier. While recording, the pointer position, clicks and key presses go to a
  `<recording>.pointer.json` file next to the video. When recording ends, the Auto zoom
  button in the result panel suggests zoom regions from that file. Untick the regions you
  do not want, pick a zoom level from 1.5x to 3x, and Apply writes a new `-zoom` file. The
  original recording stays as it is.
- Pointer sampling runs at 30 Hz on a low-priority thread. Its CPU use stayed below what
  Windows can measure, and dropped frames did not rise
  (`docs/olcumler/imlec-ornekleme.md`).

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
