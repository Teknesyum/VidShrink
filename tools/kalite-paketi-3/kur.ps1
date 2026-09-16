param([switch]$HandBrake, [switch]$Libmpv)

$ErrorActionPreference = 'Stop'
if (-not $env:GITHUB_ACTIONS) {
    Write-Error 'Bu kurulum yalniz GitHub Actions kosucusunda calisir.'
    exit 3
}

function Indir([string]$Url, [string]$Hedef, [string]$Sha) {
    Invoke-WebRequest -Uri $Url -OutFile $Hedef
    $gelen = (Get-FileHash -Path $Hedef -Algorithm SHA256).Hash
    if ($gelen -ne $Sha.ToUpperInvariant()) { throw "sha256 uyusmuyor: $Url beklenen=$Sha gelen=$gelen" }
}

$zip = Join-Path $env:RUNNER_TEMP 'ffmpeg.zip'
Indir 'https://github.com/GyanD/codexffmpeg/releases/download/9.0/ffmpeg-9.0-full_build.zip' $zip 'F42F0C4B04EAE3AC918707FF66E3E0FF0CEE527BFA6D322624D4BC1160D5055E'
Expand-Archive -Path $zip -DestinationPath $env:RUNNER_TEMP -Force
$bin = Join-Path $env:RUNNER_TEMP 'ffmpeg-9.0-full_build\bin'
if (-not (Test-Path (Join-Path $bin 'ffmpeg.exe'))) { throw "ffmpeg yok: $bin" }
Add-Content -Path $env:GITHUB_PATH -Value $bin

if ($Libmpv) {
    $arsiv = Join-Path $env:RUNNER_TEMP 'mpv-dev.7z'
    $sha = 'FAC135C68A35B7639E39D72C0C365104EDBAEBDEA39A0DFDD8C36E8C8E80FAEF'
    $tamam = $false
    foreach ($u in @('https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20260903/mpv-dev-x86_64-20260903-git-69e63f425a.7z', 'https://github.com/Teknesyum/VidShrink/releases/download/libmpv-mirror/mpv-dev-x86_64-20260903-git-69e63f425a.7z')) {
        try { Indir $u $arsiv $sha; $tamam = $true; break } catch { Write-Warning $_.Exception.Message }
    }
    if (-not $tamam) { throw 'libmpv indirilemedi' }
    $md = Join-Path $env:RUNNER_TEMP 'libmpv'
    7z x $arsiv "-o$md" -y | Out-Null
    $dll = Join-Path $md 'libmpv-2.dll'
    if ((Get-FileHash $dll -Algorithm SHA256).Hash -ne '673E6397920AB64A9C5B3A618F7F16D38854EFE72B58665F1F84E4E873B763A4') { throw 'libmpv-2.dll sha256 uyusmuyor' }
    Add-Content -Path $env:GITHUB_ENV -Value "VIDSHRINK_LIBMPV=$dll"
}

if ($HandBrake) {
    $hz = Join-Path $env:RUNNER_TEMP 'handbrake.zip'
    Indir 'https://github.com/HandBrake/HandBrake/releases/download/1.11.2/HandBrakeCLI-1.11.2-win-x86_64.zip' $hz '80bfe8d5f5d11cc3ef76b834add3ed4e82dee6523ffeb435c283f88b1a21f09d'
    $hd = Join-Path $env:RUNNER_TEMP 'handbrake'
    Expand-Archive -Path $hz -DestinationPath $hd -Force
    $exe = Get-ChildItem $hd -Recurse -Filter 'HandBrakeCLI.exe' | Select-Object -First 1
    if (-not $exe) { throw 'HandBrakeCLI.exe bulunamadi' }
    Add-Content -Path $env:GITHUB_ENV -Value "HANDBRAKE_CLI=$($exe.FullName)"
}
