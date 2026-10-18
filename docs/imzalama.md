# Code Signing And Antivirus False Positives

`VidShrinkPlayer-Setup.exe` and the app are unsigned. A new unsigned executable that downloads
and extracts other executables is exactly what heuristic scanners flag, so SmartScreen
warnings and Avast/AVG detections are expected until the binaries are signed and have
built reputation. Nothing below has been submitted or bought; each step needs the maintainer.

## SignPath Foundation

The SignPath Foundation application was rejected in 2026-10. The release workflow has no
SignPath step, and the README makes no signing claim.

## Other Low-Cost Signing Routes (Checked 2026-10-15, None Applied)

| Route | Cost | Fits a maintainer in Türkiye? | SmartScreen |
|-------|------|-------------------------------|-------------|
| Azure Artifact Signing (formerly Trusted Signing), Basic | 9.99 USD a month, paid Azure subscription required; free, trial and sponsored subscriptions are not accepted | No: public-trust identity validation for individuals is open only in the USA and Canada; organisations need a verifiable legal entity in the supported countries | Not an EV certificate; reputation still builds with downloads |
| Certum Open Source Code Signing | 69 EUR for the first year with a cryptoCertum card and reader; renewal about 25 EUR (second-hand source) | Yes: issued to a natural person running an open-source project | Standard certificate; reputation builds with downloads |
| Unsigned (today) | 0 | — | Warnings until reputation builds; false positives are reported by hand (below) |

Sources: https://learn.microsoft.com/azure/artifact-signing/faq,
https://azure.microsoft.com/products/artifact-signing (pricing),
https://shop.certum.eu/open-source-code-signing.html,
https://github.com/daisy/ace-gui/issues/15 (Certum renewal price, not from Certum).
MSYS2 reports paying about 8.60 EUR a month for Artifact Signing
(https://www.msys2.org/dev/code-signing/).

The Certum key lives on a physical card, so signing in GitHub Actions would need the card
on a self-hosted runner or a manual signing step after the build; that was not measured.

## Reporting A False Positive To Avast (And AVG)

1. Open https://www.avast.com/false-positive-file-form.php.
2. Choose "File" for a detected executable or "Website" for a blocked download URL.
3. Attach the exact released `VidShrinkPlayer-Setup.exe` (or give the GitHub release URL), the
   detection name Avast showed, and the sha256 from `checksums-win-x64.txt`.
4. Describe it briefly: open-source installer from github.com/Teknesyum/VidShrink-Player, built
   by GitHub Actions, downloads only release assets verified by sha256.
5. Each release is a new file, so report again if a new version is flagged. Microsoft
   (https://www.microsoft.com/wdsi/filesubmission) accepts the same kind of report.

## What The Repository Already Does

- VERSIONINFO is filled: `Directory.Build.props` sets Company, Product, Authors and Copyright,
  the version comes from the same file, and the setup project sets its Description.
- `app.manifest` requests `asInvoker` (no elevation), declares Windows 10+ and long paths.
- The exe carries the VidShrink icon and a stable name; no packer or obfuscator is used
  (the single-file bundle is the standard .NET host).
- Every downloaded file is verified against pinned or published sha256 values, and the
  release publishes `checksums-win-x64.txt` including the setup program.

## What Would Still Help

- A code signing certificate (one of the routes above) is the precondition for SmartScreen
  reputation that survives new releases; without it every release starts from zero.
- Keep the file name and publisher stable across releases so reputation accumulates.
- Avoid shipping a second, differently built installer next to the signed one.
