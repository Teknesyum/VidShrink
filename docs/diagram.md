# Diagrams this repository's READMEs carry

Asked once, answered once. Do not ask again for VidShrink.

The answer, given with the README rewrite (T0, branch `t0/readme`): **four diagrams, all
Mermaid, rendered by GitHub from the README source rather than committed as SVG files.**
Mermaid was chosen because the engine changes often and a picture that lives in the text
gets updated with the text.

1. **User flow.** Drop a file, pick a target, probe, scene map, draft plan, calibration,
   settled plan, encode, deliver — with the overshoot question as the one branch.
2. **Architecture.** `VidShrink.Launcher` and `VidShrink.ShellExtension` feeding
   `VidShrink.App`; `VidShrink.App` reading decisions from `VidShrink.Core` and running
   processes through `VidShrink.Ffmpeg`; `VidShrink.Ffmpeg` calling out to ffmpeg and
   ffprobe.
3. **Calibration and the two passes.** Anchor CRF, the two-CRF sample encode, the settled
   plan, then the software turbo first pass against the hardware single VBR pass, ending
   at the "check what ffmpeg actually applied" step.
4. **The launcher update path.** Once-a-day gate, manifest fetch with its timeout, per-file
   SHA-256 comparison, staging, verify, atomic move — every failure edge going straight to
   "start the installed app".

Mermaid node labels are in the language of the file they sit in: English labels in
`README.md`, Turkish labels in `README.tr.md`. Same four diagrams, same order, both files.

Screenshots are a separate matter and all live under `docs/gorseller/`. English windows go
in `README.md`, Turkish windows in `README.tr.md`; where only a Turkish window exists, the
English caption says so rather than pretending otherwise.
