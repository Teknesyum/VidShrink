param(
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\VidShrink'),
    [switch]$NoLaunch,
    [switch]$SkipShortcuts
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Refresh-ProcessPath {
    $machine = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $user = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$machine;$user"
}

function Require-WinGet {
    if (-not (Get-Command winget.exe -ErrorAction SilentlyContinue)) {
        throw 'WinGet bulunamadı. Windows App Installer bileşenini yükleyip komutu yeniden çalıştırın.'
    }
}

function Install-WinGetPackage([string]$Id) {
    Require-WinGet
    & winget.exe install --id $Id --exact --scope user --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
    if ($LASTEXITCODE -ne 0) { throw "$Id kurulamadı. WinGet çıkış kodu: $LASTEXITCODE" }
    Refresh-ProcessPath
}

function Find-Tool([string]$Name) {
    $command = Get-Command "$Name.exe" -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $winGetLink = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Links\$Name.exe"
    if (Test-Path -LiteralPath $winGetLink) { return $winGetLink }
    return $null
}

Write-Host 'VidShrink kurulumu hazırlanıyor...' -ForegroundColor Cyan

if (-not (Get-Command dotnet.exe -ErrorAction SilentlyContinue) -or
    -not (& dotnet.exe --list-sdks | Select-String -Pattern '^8\.')) {
    Write-Host '.NET 8 SDK yükleniyor...' -ForegroundColor Cyan
    Install-WinGetPackage 'Microsoft.DotNet.SDK.8'
}

$ffmpeg = Find-Tool 'ffmpeg'
$ffprobe = Find-Tool 'ffprobe'
if (-not $ffmpeg -or -not $ffprobe) {
    Write-Host 'FFmpeg ve FFprobe yükleniyor...' -ForegroundColor Cyan
    Install-WinGetPackage 'Gyan.FFmpeg'
    $ffmpeg = Find-Tool 'ffmpeg'
    $ffprobe = Find-Tool 'ffprobe'
}
if (-not $ffmpeg -or -not $ffprobe) { throw 'FFmpeg veya FFprobe kurulumdan sonra bulunamadı.' }

$programsRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
$resolvedInstallRoot = [IO.Path]::GetFullPath($InstallRoot)
if ($resolvedInstallRoot -eq $programsRoot -or
    -not $resolvedInstallRoot.StartsWith($programsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Güvenlik nedeniyle kurulum yolu LocalAppData\Programs altında olmalıdır: $resolvedInstallRoot"
}

$workRoot = Join-Path ([IO.Path]::GetTempPath()) ("vidshrink-install-" + [Guid]::NewGuid().ToString('N'))
$zipPath = Join-Path $workRoot 'source.zip'
$extractRoot = Join-Path $workRoot 'source'
$publishRoot = Join-Path $workRoot 'publish'

try {
    New-Item -ItemType Directory -Path $workRoot, $extractRoot, $publishRoot -Force | Out-Null
    Write-Host 'Güncel VidShrink kaynakları indiriliyor...' -ForegroundColor Cyan
    Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/Teknesyum/VidShrink/archive/refs/heads/main.zip' -OutFile $zipPath
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractRoot -Force

    $sourceRoot = Get-ChildItem -LiteralPath $extractRoot -Directory | Select-Object -First 1
    if (-not $sourceRoot) { throw 'İndirilen kaynak paketi açılamadı.' }

    Write-Host 'VidShrink Release sürümü yayımlanıyor...' -ForegroundColor Cyan
    & dotnet.exe publish (Join-Path $sourceRoot.FullName 'src\VidShrink.App\VidShrink.App.csproj') `
        --configuration Release --runtime win-x64 --self-contained true `
        -p:PublishSingleFile=false --output $publishRoot
    if ($LASTEXITCODE -ne 0) { throw "VidShrink derlenemedi. dotnet çıkış kodu: $LASTEXITCODE" }

    $toolsRoot = Join-Path $publishRoot 'tools\ffmpeg'
    New-Item -ItemType Directory -Path $toolsRoot -Force | Out-Null
    Copy-Item -LiteralPath $ffmpeg -Destination (Join-Path $toolsRoot 'ffmpeg.exe') -Force
    Copy-Item -LiteralPath $ffprobe -Destination (Join-Path $toolsRoot 'ffprobe.exe') -Force

    $runningExecutable = Join-Path $resolvedInstallRoot 'VidShrink.App.exe'
    Get-Process VidShrink.App -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $runningExecutable } |
        Stop-Process -Force

    if (Test-Path -LiteralPath $resolvedInstallRoot) {
        Remove-Item -LiteralPath $resolvedInstallRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $resolvedInstallRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $publishRoot '*') -Destination $resolvedInstallRoot -Recurse -Force

    $installedExe = Join-Path $resolvedInstallRoot 'VidShrink.App.exe'
    if (-not (Test-Path -LiteralPath $installedExe)) { throw 'Kurulan VidShrink.App.exe bulunamadı.' }

    if (-not $SkipShortcuts) {
        $shell = New-Object -ComObject WScript.Shell
        $desktopShortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'VidShrink.lnk'))
        $desktopShortcut.TargetPath = $installedExe
        $desktopShortcut.WorkingDirectory = $resolvedInstallRoot
        $desktopShortcut.IconLocation = "$installedExe,0"
        $desktopShortcut.Save()

        $startMenuDirectory = Join-Path ([Environment]::GetFolderPath('Programs')) 'VidShrink'
        New-Item -ItemType Directory -Path $startMenuDirectory -Force | Out-Null
        $startMenuShortcut = $shell.CreateShortcut((Join-Path $startMenuDirectory 'VidShrink.lnk'))
        $startMenuShortcut.TargetPath = $installedExe
        $startMenuShortcut.WorkingDirectory = $resolvedInstallRoot
        $startMenuShortcut.IconLocation = "$installedExe,0"
        $startMenuShortcut.Save()
    }

    Write-Host "VidShrink kuruldu: $resolvedInstallRoot" -ForegroundColor Green
    if (-not $NoLaunch) { Start-Process -FilePath $installedExe }
}
finally {
    if (Test-Path -LiteralPath $workRoot) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
