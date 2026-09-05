<!-- teknesyum:handoff -->
# Handoff

Where this project stands. The facts below are written by a hook and cost
nothing; the intent under them is written by hand, once, when it changes.

## Intent

A 17-minute video came out visibly worse than HandBrake's output at the same delivered
size, and the engine is now the whole job. The first pass reproduced the complaint down
to the command line and found two separate causes, not one: the default fast path
silently tone-maps HDR to 8-bit BT.709 because `av1_nvenc` was missing from the codec
list, and the quality rig itself was comparing files across mismatched colour spaces, so
the numbers it produced measured the mismatch instead of the compression. Neither cause
can be traded off against the other, and the engine's own penalty constants stay frozen
until the rig can be trusted.

Platform work is finished and out of the way: v0.2.5 runs on macOS, installs a real
`.app` bundle that now swaps itself on update, and gives Windows 11 a first-class
right-click menu.

## Contracts open

- `T158` — active, round 1
- `T171` — active, round 1
- `T172` — active, round 1
- `T173` — active, round 1
- `T174` — active, round 1
- `T175` — active, round 1

## Closed last

- `T175` — stale — 2026-09-05 18:10
- `T172` — stale — 2026-09-05 18:10
- `T171` — stale — 2026-09-05 18:10
- `T171` — stale — 2026-09-05 17:31
- `T163` — passed — 2026-09-05 13:51

## Tree

- branch: `main`
- head: `c303bd8 T172-T176 sozlesmeleri acildi: ses tabani, ayar kaliciligi, onizleme barinagi, oynatici boru ve girdi`
- uncommitted files: 2
