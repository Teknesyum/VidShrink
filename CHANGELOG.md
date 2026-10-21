# Changelog

All notable changes to VidShrink Player are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow
[Semantic Versioning](https://semver.org/).

The history of the program under its old name, VidShrink, up to 1.2.24 and the changes
made after it, is in [`docs/CHANGELOG-VidShrink.md`](docs/CHANGELOG-VidShrink.md).

## [Unreleased]

### Added

- Editor preview now shows transitions. The few seconds where two clips overlap are rendered
  in the background into a small file in the app's cache folder; until it is ready the preview
  plays a plain cut as before. The files are removed when the editor closes or the app exits.
- The editor has a second video track, V2, for picture-in-picture. Add a video from the
  timeline menu, drag it along the track, and pick its size, corner and green-screen removal.
  Export lays it over the main video under any text; the preview shows it after a short render.

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
