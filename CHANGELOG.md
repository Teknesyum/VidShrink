# Changelog

All notable changes to VidShrink Player are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow
[Semantic Versioning](https://semver.org/).

The history of the program under its old name, VidShrink, up to 1.2.24 and the changes
made after it, is in [`docs/CHANGELOG-VidShrink.md`](docs/CHANGELOG-VidShrink.md).

## [Unreleased]

### Added

- Linux media controls. The player shows up on the session bus as an MPRIS player, so media keys, the
  desktop's media widget and `playerctl` can play, pause, seek and open files. `VIDSHRINK_MPRIS=0` turns it
  off.
- Open in new window. The player's right-click menu and `--yeni-pencere` (`--new-window`) open a second,
  independent player window. It works on its own copy of the settings, so the two windows never write the
  same file; the queue and updates stay with the first window.
- The control channel's status reply carries `durationSeconds` and `title`.

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
