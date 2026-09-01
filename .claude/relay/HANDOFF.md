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

- `T83` — submitted, round 1
- `T84` — submitted, round 1

## Closed last

- `T82` — done — 2026-08-30 12:08
- `T81` — done — 2026-08-30 11:30
- `T83` — stale — 2026-08-30 11:25
- `T82` — stale — 2026-08-30 11:16
- `T81` — stale — 2026-08-30 11:16

## Tree

- branch: `sole/sagtik-artiklari`
- head: `d4f69a9 test: sag tik paketinin davranisini olc`
- uncommitted files: 0
