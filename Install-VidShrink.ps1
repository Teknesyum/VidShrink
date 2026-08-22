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
    $noApplicableInstaller = -1978335216
    & winget.exe install --id $Id --exact --scope user --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
    if ($LASTEXITCODE -eq $noApplicableInstaller) {
        Write-Host "$Id kullanıcı kapsamında sunulmuyor, makine kapsamı deneniyor (yönetici onayı isteyebilir)..." -ForegroundColor Yellow
        & winget.exe install --id $Id --exact --silent --accept-package-agreements --accept-source-agreements
    }
    if ($LASTEXITCODE -ne 0) { throw "$Id kurulamadı. WinGet çıkış kodu: $LASTEXITCODE" }
    Refresh-ProcessPath
}

function Get-RuntimeIdentifier {
    switch ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
        'Arm64' { return 'win-arm64' }
        'X86'   { return 'win-x86' }
        default { return 'win-x64' }
    }
}

function Find-DotNetSdk8 {
    $candidates = New-Object System.Collections.Generic.List[string]
    $command = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($command) { $candidates.Add($command.Source) }
    $candidates.Add((Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'))
    if ($env:ProgramFiles) { $candidates.Add((Join-Path $env:ProgramFiles 'dotnet\dotnet.exe')) }

    foreach ($candidate in $candidates) {
        if (-not (Test-Path -LiteralPath $candidate)) { continue }
        $sdks = & $candidate --list-sdks 2>$null
        if ($LASTEXITCODE -eq 0 -and ($sdks | Select-String -Pattern '^8\.' -Quiet)) { return $candidate }
    }
    return $null
}

function Install-DotNetSdk8 {
    $installDirectory = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
    $bootstrapper = Join-Path ([IO.Path]::GetTempPath()) ('dotnet-install-' + [Guid]::NewGuid().ToString('N') + '.ps1')
    try {
        Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $bootstrapper
        & $bootstrapper -Channel '8.0' -InstallDir $installDirectory -NoPath
    }
    finally {
        Remove-Item -LiteralPath $bootstrapper -Force -ErrorAction SilentlyContinue
    }

    $executable = Join-Path $installDirectory 'dotnet.exe'
    if (-not (Test-Path -LiteralPath $executable)) { throw '.NET 8 SDK kurulumdan sonra bulunamadı.' }
    return $executable
}

function Find-Tool([string]$Name) {
    $command = Get-Command "$Name.exe" -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $winGetLink = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Links\$Name.exe"
    if (Test-Path -LiteralPath $winGetLink) { return $winGetLink }
    return $null
}

Write-Host 'VidShrink kurulumu hazırlanıyor...' -ForegroundColor Cyan

$dotnet = Find-DotNetSdk8
if (-not $dotnet) {
    Write-Host '.NET 8 SDK yükleniyor (kullanıcı kapsamı, yönetici gerekmez)...' -ForegroundColor Cyan
    $dotnet = Install-DotNetSdk8
}
$env:DOTNET_ROOT = Split-Path -Parent $dotnet
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

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

    $runtimeIdentifier = Get-RuntimeIdentifier
    Write-Host "VidShrink Release sürümü yayımlanıyor ($runtimeIdentifier)..." -ForegroundColor Cyan
    & $dotnet publish (Join-Path $sourceRoot.FullName 'src\VidShrink.App\VidShrink.App.csproj') `
        --configuration Release --runtime $runtimeIdentifier --self-contained true `
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
