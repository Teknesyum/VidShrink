# Code Signing And Antivirus False Positives

`VidShrink-Setup.exe` and the app are unsigned. A new unsigned executable that downloads
and extracts other executables is exactly what heuristic scanners flag, so SmartScreen
warnings and Avast/AVG detections are expected until the binaries are signed and have
built reputation. Nothing below has been submitted; each step needs the maintainer.

## SignPath Foundation (free signing for open source)

1. Check the criteria at https://signpath.org/terms: an OSI license, public source,
   no malware or PUA behavior, an active project, and release builds made by CI from the
   public repository (GitHub Actions qualifies).
2. Apply through the form linked from https://signpath.org/ ("Apply"). Give the repository
   URL, the license, the maintainers, and the artifacts to sign: `VidShrink-Setup.exe`,
   `VidShrink.exe` and `VidShrink.App.exe` from the `win-x64` release job.
3. After approval SignPath creates an organization and project. Install the SignPath
   GitHub App on the repository and add the `SIGNPATH_API_TOKEN` secret.
4. Add an artifact configuration in SignPath describing the zip/exe layout, and a signing
   policy (release-signing, tags only).
5. In `release.yml`, upload the unsigned files as a workflow artifact, call
   `signpath/github-action-submit-signing-request` with the organization, project, policy
   and artifact configuration slugs, wait for completion, and publish the signed output
   instead. Checksums must be computed after signing.
6. The certificate is issued to "SignPath Foundation"; the README should say so, as
   SignPath requires a code signing policy page.

## Reporting A False Positive To Avast (And AVG)

1. Open https://www.avast.com/false-positive-file-form.php.
2. Choose "File" for a detected executable or "Website" for a blocked download URL.
3. Attach the exact released `VidShrink-Setup.exe` (or give the GitHub release URL), the
   detection name Avast showed, and the sha256 from `checksums-win-x64.txt`.
4. Describe it briefly: open-source installer from github.com/Teknesyum/VidShrink, built
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

- Signing (above) is the only step that removes SmartScreen warnings for good.
- Keep the file name and publisher stable across releases so reputation accumulates.
- Avoid shipping a second, differently built installer next to the signed one.
