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

- `T86` — open, round 2 — Düzeltme turu 2
- `T87` — open, round 3 — Düzeltme turu 2
- `T88` — open, round 3 — Düzeltme turu 2
- `T89` — open, round 1
- `T90` — open, round 1

## Closed last

- `T85` — done — 2026-09-01 16:52
- `T84` — done — 2026-09-01 11:31
- `T83` — done — 2026-09-01 07:48
- `T82` — done — 2026-08-30 12:08
- `T81` — done — 2026-08-30 11:30

## Tree

- branch: `main`
- head: `86c56b5 muhur: T85 kapandi — kosum kapisi ve olcum yalitimi`
- uncommitted files: 1
