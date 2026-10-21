# Changelog

All notable changes to VidShrink Player are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow
[Semantic Versioning](https://semver.org/).

The history of the program under its old name, VidShrink, up to 1.2.24 and the changes
made after it, is in [`docs/CHANGELOG-VidShrink.md`](docs/CHANGELOG-VidShrink.md).

## [Unreleased]

## [0.0.2] - 2026-10-21

### Added

### Changed

- Every labelled field is now a single Tab stop: the "?" badge beside a field label no
  longer takes focus from the keyboard. Its help is read by screen readers as the field's
  description, and F1 on the field opens it.
- Screen readers read the target size chips with their unit ("8 MB") and the share link
  boxes by name.

### Fixed

- Changing the colour matrix (Convert's BT.601/709/2020 choice and Shrink's colour space
  filter) now resamples colour with Lanczos instead of bilinear, so sharp colour edges stay
  cleaner.
- Recorder: on several monitors the drawing layer opens on the right screen. Without a capture area it covers the
  screen under the pointer instead of always the primary one; an area spanning monitors with different scaling is
  trimmed to one monitor so the layer is not sized with the wrong scale.
- The installers (setup program, silent install and `Install-VidShrink.ps1`) now write the
  Windows 11 context menu labels in the install language, so the first menu no longer shows the
  built-in English names until the app is opened once. Uninstall removes them.
- Closing the player ends its Windows media controls session; pressing a stale one could crash the app.

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
