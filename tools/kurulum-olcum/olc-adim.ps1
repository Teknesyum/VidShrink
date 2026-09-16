$cikti = Join-Path $PSScriptRoot '..\..\.calisma\kurulum-olcum'
New-Item -ItemType Directory -Force $cikti | Out-Null
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$d = Join-Path $cikti 'adim'
if (Test-Path $d) { Remove-Item $d -Recurse -Force }
New-Item -ItemType Directory $d | Out-Null
$tag = 'v0.8.2'
function Zaman([string]$ad, [scriptblock]$is) {
    $s = [Diagnostics.Stopwatch]::StartNew(); & $is | Out-Null
    '{0,-40} {1,8:N0} ms' -f $ad, $s.Elapsed.TotalMilliseconds
}
$app = Join-Path $d 'vidshrink-win-x64.zip'
$lau = Join-Path $d 'vidshrink-launcher-win-x64.zip'
Zaman 'indir app zip (IWR)' { Invoke-WebRequest -UseBasicParsing "https://github.com/Teknesyum/VidShrink/releases/download/$tag/vidshrink-win-x64.zip" -OutFile $app }
Zaman 'indir launcher zip (IWR)' { Invoke-WebRequest -UseBasicParsing "https://github.com/Teknesyum/VidShrink/releases/download/$tag/vidshrink-launcher-win-x64.zip" -OutFile $lau }
Zaman 'Get-FileHash app+launcher' { Get-FileHash $app -Algorithm SHA256; Get-FileHash $lau -Algorithm SHA256 }
Zaman 'Expand-Archive app' { Expand-Archive -LiteralPath $app -DestinationPath (Join-Path $d 'stage\app') -Force }
Zaman 'Expand-Archive launcher' { Expand-Archive -LiteralPath $lau -DestinationPath (Join-Path $d 'stage') -Force }
$ff = (Get-Command ffmpeg.exe).Source; $fp = (Get-Command ffprobe.exe).Source
New-Item -ItemType Directory (Join-Path $d 'stage\tools\ffmpeg') | Out-Null
Zaman 'ffmpeg+ffprobe kopya' { Copy-Item $ff (Join-Path $d 'stage\tools\ffmpeg\ffmpeg.exe'); Copy-Item $fp (Join-Path $d 'stage\tools\ffmpeg\ffprobe.exe') }
'ffmpeg+ffprobe bayt: ' + ((Get-ChildItem (Join-Path $d 'stage\tools\ffmpeg') | Measure-Object Length -Sum).Sum)
Zaman 'Copy-Item stage -> kok' { New-Item -ItemType Directory (Join-Path $d 'kok') | Out-Null; Copy-Item -Path (Join-Path $d 'stage\*') -Destination (Join-Path $d 'kok') -Recurse -Force }
Zaman 'Remove-Item kok' { Remove-Item (Join-Path $d 'kok') -Recurse -Force }
'stage dosya: ' + (Get-ChildItem (Join-Path $d 'stage') -Recurse -File).Count
$mpv = Join-Path $d 'mpv.7z'
Zaman 'indir libmpv 7z (IWR)' { Invoke-WebRequest -UseBasicParsing 'https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20260903/mpv-dev-x86_64-20260903-git-69e63f425a.7z' -OutFile $mpv }
Zaman 'Get-FileHash libmpv 7z' { Get-FileHash $mpv -Algorithm SHA256 }
New-Item -ItemType Directory (Join-Path $d 'mpv') | Out-Null
Zaman 'tar -x libmpv-2.dll' { & "$env:SystemRoot\System32\tar.exe" -xf $mpv -C (Join-Path $d 'mpv') libmpv-2.dll }
Zaman 'indir libmpv 7z (curl.exe)' { & curl.exe -sL -o (Join-Path $d 'mpv2.7z') 'https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20260903/mpv-dev-x86_64-20260903-git-69e63f425a.7z' }
Zaman 'indir app zip (curl.exe)' { & curl.exe -sL -o (Join-Path $d 'app2.zip') "https://github.com/Teknesyum/VidShrink/releases/download/$tag/vidshrink-win-x64.zip" }
