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

- `T125` — active, round 1 — T125 — A/B'nin bizim tarafimiz auto modu kosmuyor
- `T133` — active, round 1
- `T152` — open, round 1

## Closed last

- `T146` — done — 2026-09-03 10:25
- `T147` — done — 2026-09-03 10:10
- `T146` — stale — 2026-09-03 09:17
- `T151` — done — 2026-09-03 09:10
- `T145` — done — 2026-09-03 09:08

## Tree

- branch: `main`
- head: `f337e88 rele: T146 muhur kaydi, T152 beklemeden cikti`
- uncommitted files: 0
