param(
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\VidShrink'),
    [switch]$NoLaunch,
    [switch]$SkipShortcuts
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$repository = 'Teknesyum/VidShrink'

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

# Yayında yalnız win-x64 var. arm64 ya da x86 makinede x64 arşivini sessizce kurmak
# çalışan ama güncellenmeyen bir kurulum bırakır: güncelleyici kendi mimarisinin
# adını arar (UpdateCheck.Rid) ve o varlık yayında olmadığı için hiç güncelleme
# bulamaz. Onun yerine burada duruluyor.
function Get-RuntimeIdentifier {
    $architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    if ($architecture -ne [Runtime.InteropServices.Architecture]::X64) {
        throw "Bu mimari için yayın yok: $architecture. VidShrink şu an yalnız win-x64 için yayımlanıyor."
    }
    return 'win-x64'
}

function Find-Tool([string]$Name) {
    $command = Get-Command "$Name.exe" -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $winGetLink = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Links\$Name.exe"
    if (Test-Path -LiteralPath $winGetLink) { return $winGetLink }
    return $null
}

function Get-LatestRelease {
    $headers = @{ 'User-Agent' = 'VidShrink-Installer'; 'Accept' = 'application/vnd.github+json' }
    $response = Invoke-WebRequest -UseBasicParsing -Headers $headers -Uri "https://api.github.com/repos/$repository/releases/latest"
    $release = ConvertFrom-Json ([string]$response.Content)
    if (-not $release.tag_name) { throw 'Yayın bilgisi okunamadı: etiket adı yok.' }
    return $release
}

function Get-ReleaseAsset([string]$Tag, [string]$Name, [string]$Destination) {
    $uri = "https://github.com/$repository/releases/download/$Tag/$Name"
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $uri -OutFile $Destination
    }
    catch {
        throw "Yayın varlığı indirilemedi: $Name ($uri)"
    }
}

# checksums-<rid>.txt sha256sum biçimindedir: özet, iki boşluk, varlık adı.
function Read-Checksums([string]$Path) {
    $table = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $match = [regex]::Match($line, '^([0-9a-fA-F]{64})\s+\*?(.+?)\s*$')
        if ($match.Success) { $table[$match.Groups[2].Value] = $match.Groups[1].Value.ToLowerInvariant() }
    }
    return $table
}

function Assert-Checksum([hashtable]$Table, [string]$Name, [string]$Path) {
    if (-not $Table.ContainsKey($Name)) { throw "Sağlama listesinde $Name yok; indirilen dosya doğrulanamıyor." }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Table[$Name]) {
        throw "$Name sağlaması tutmuyor. Beklenen $($Table[$Name]), bulunan $actual. Kurulum durduruldu."
    }
}

Write-Host 'VidShrink kurulumu hazırlanıyor...' -ForegroundColor Cyan

$runtimeIdentifier = Get-RuntimeIdentifier

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

$archiveName = "vidshrink-$runtimeIdentifier.zip"
$launcherArchiveName = "vidshrink-launcher-$runtimeIdentifier.zip"
$checksumsName = "checksums-$runtimeIdentifier.txt"

$workRoot = Join-Path ([IO.Path]::GetTempPath()) ("vidshrink-install-" + [Guid]::NewGuid().ToString('N'))
$stageRoot = Join-Path $workRoot 'stage'

try {
    New-Item -ItemType Directory -Path $workRoot, $stageRoot -Force | Out-Null

    Write-Host 'Son yayın aranıyor...' -ForegroundColor Cyan
    $release = Get-LatestRelease
    $tag = [string]$release.tag_name
    $version = $tag.TrimStart('v')
    Write-Host "Kurulacak sürüm: $version" -ForegroundColor Cyan

    # Başlatıcı yayında yoksa kurulum yarım kalır: kısayolun göstereceği program olmaz
    # ve otogüncelleme hiç çalışmaz. Kaynaktan derlemeye düşmek yerine burada durulur.
    $assetNames = @($release.assets | ForEach-Object { [string]$_.name })
    foreach ($required in $archiveName, $launcherArchiveName, $checksumsName) {
        if ($assetNames -notcontains $required) {
            throw "Yayın $tag bu varlığı taşımıyor: $required. Başlatıcısız kurulum yapılmaz; başlatıcıyı da içeren bir yayın çıkana kadar bekleyin."
        }
    }

    $archivePath = Join-Path $workRoot $archiveName
    $launcherArchivePath = Join-Path $workRoot $launcherArchiveName
    $checksumsPath = Join-Path $workRoot $checksumsName

    Write-Host 'Yayın paketi indiriliyor...' -ForegroundColor Cyan
    Get-ReleaseAsset $tag $checksumsName $checksumsPath
    Get-ReleaseAsset $tag $archiveName $archivePath
    Get-ReleaseAsset $tag $launcherArchiveName $launcherArchivePath

    Write-Host 'İndirilenler doğrulanıyor...' -ForegroundColor Cyan
    $checksums = Read-Checksums $checksumsPath
    Assert-Checksum $checksums $archiveName $archivePath
    Assert-Checksum $checksums $launcherArchiveName $launcherArchivePath

    # Kurulum düzeni: kökte başlatıcı ve ffmpeg, app\ altında güncellenen uygulama.
    # Çalışan bir exe ve yüklü dll'ler üzerine yazılamadığı için güncelleme, uygulama
    # yüklenmeden önce başlatıcı tarafından app\ klasörüne uygulanır.
    $appStageRoot = Join-Path $stageRoot 'app'
    Expand-Archive -LiteralPath $archivePath -DestinationPath $appStageRoot -Force
    Expand-Archive -LiteralPath $launcherArchivePath -DestinationPath $stageRoot -Force

    # Kurulan sürümün işareti. Bu dosya olmadan ilk açılışta güncelleyici kurulu klasörü
    # yayınla dosya dosya karşılaştırır ve arşivin neredeyse tamamını yeniden indirir.
    Set-Content -LiteralPath (Join-Path $appStageRoot '.update-version') -Value $version -Encoding UTF8 -NoNewline

    $toolsRoot = Join-Path $stageRoot 'tools\ffmpeg'
    New-Item -ItemType Directory -Path $toolsRoot -Force | Out-Null
    Copy-Item -LiteralPath $ffmpeg -Destination (Join-Path $toolsRoot 'ffmpeg.exe') -Force
    Copy-Item -LiteralPath $ffprobe -Destination (Join-Path $toolsRoot 'ffprobe.exe') -Force

    foreach ($processName in 'VidShrink.App', 'VidShrink') {
        Get-Process $processName -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -and $_.Path.StartsWith($resolvedInstallRoot, [StringComparison]::OrdinalIgnoreCase) } |
            Stop-Process -Force
    }

    if (Test-Path -LiteralPath $resolvedInstallRoot) {
        Remove-Item -LiteralPath $resolvedInstallRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $resolvedInstallRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $stageRoot '*') -Destination $resolvedInstallRoot -Recurse -Force

    # Kısayollar başlatıcıyı gösterir; uygulamayı doğrudan gösterirlerse güncelleme hiç çalışmaz.
    $installedExe = Join-Path $resolvedInstallRoot 'VidShrink.exe'
    if (-not (Test-Path -LiteralPath $installedExe)) { throw 'Kurulan VidShrink.exe bulunamadı.' }
    if (-not (Test-Path -LiteralPath (Join-Path $resolvedInstallRoot 'app\VidShrink.App.exe'))) {
        throw 'Kurulan app\VidShrink.App.exe bulunamadı.'
    }

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

    Write-Host "VidShrink $version kuruldu: $resolvedInstallRoot" -ForegroundColor Green
    if (-not $NoLaunch) { Start-Process -FilePath $installedExe }
}
finally {
    if (Test-Path -LiteralPath $workRoot) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
