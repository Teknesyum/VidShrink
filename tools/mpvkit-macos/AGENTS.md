# mpvkit-macos

macOS libmpv from MPVKit 1.0.0 static xcframeworks (LGPL product), macOS 13+ universal.

- `mpvkit-1.0.0.lock` — `name sha256 url`, 29 lines, checksums from MPVKit `Package.swift`;
  the `# output` line pins the published dylib (name, sha256, URL).
- **The link step is not byte-reproducible** (three macos-15 runs gave two distinct sha256s),
  so one build is linked once, published as the `deps-libmpv-macos-mpvkit-1.0.0` prerelease,
  and shipped from there. Never relink per release; a new build means a new pin plus a new
  sha256 in `install-vidshrink.sh` and in the lock's `# output` line.
- `mpvkit-macos.sh <lock> <work> <out>` — download and verify, `otool`/`lipo` table to
  `out/minos-lipo.tsv`, link `out/libmpv.2.dylib`, `out/verdict.txt`. macOS runner only.
  `MPVKIT_MIN_MACOS` (13.0) link target, `MPVKIT_MINOS_LIMIT` (14.0) criterion.

Run: `.github/workflows/macos-mpvkit.yml`. Result: `docs/olcumler/libmpv-macos-gomme.md`.
