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

None.

## Closed last

- `T125` — done — 2026-09-04 17:02
- `T133` — stale — 2026-09-03 20:51
- `T133` — done — 2026-09-04 16:41
- `T159` — done — 2026-09-03 15:34
- `T160` — done — 2026-09-03 14:38

## Tree

- branch: `HEAD`
- head: `5aae56a T125 ham olcum verisi tools/VidShrink.Ab/veri/t125 altina alindi`
- uncommitted files: 2
