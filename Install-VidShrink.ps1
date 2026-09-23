param(
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\VidShrink'),
    [switch]$NoLaunch,
    [switch]$SkipShortcuts,
    [switch]$ShellMenuOnly,
    [switch]$RemoveShellMenu,
    [switch]$RemoveFileAssociation,
    [switch]$Uninstall,
    [ValidateSet('auto', 'tr', 'en')]
    [string]$MenuLanguage = 'auto',
    [string]$RegistryRoot = 'HKCU:\Software\Classes',
    # Yalnız bağımlılık kolu: ffmpeg ve libmpv verilen klasöre indirilip sha256'ları
    # doğrulanır, kurulum yapılmaz. CI bu kolu gerçek bir arm64 koşucusunda koşuyor.
    [string]$DepsOnly,
    # Yalnız negatif kontrol için: beklenen sha256'yı bilerek bozar, doğrulamanın
    # gerçekten durdurduğunu gösterir.
    [string]$DepsSha256Override
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$repository = 'Teknesyum/VidShrink'

$script:RemoveAttempts = 6
$script:RemoveFirstDelayMilliseconds = 200
$script:RemoveHolderWaitSeconds = 120

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

# Aynı mimari adı kaynağa göre başka yazılıyor: .NET 'X64' der, PROCESSOR_ARCHITECTURE
# 'AMD64'. Tanınmayan ad null döner; tanınmamak reddedilmek değildir.
# Bu tablonun eşi UpdateCheck.cs içindeki ArchitectureChoice.Recognize.
function ConvertTo-ArchitectureName([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    switch ($Value.Trim().ToUpperInvariant()) {
        'X64' { return 'x64' }
        'AMD64' { return 'x64' }
        'X86_64' { return 'x64' }
        'EM64T' { return 'x64' }
        'ARM64' { return 'arm64' }
        'AARCH64' { return 'arm64' }
        'X86' { return 'x86' }
        'I386' { return 'x86' }
        'I486' { return 'x86' }
        'I586' { return 'x86' }
        'I686' { return 'x86' }
        'ARM' { return 'arm' }
        'ARMV6L' { return 'arm' }
        'ARMV7L' { return 'arm' }
    }
    return $null
}

# Mimari tek bir okumaya bırakılmıyor. Sıra ve gerekçesi:
# 1. RuntimeInformation.OSArchitecture — işletim sisteminin kendi mimarisi; 64 bit
#    Windows'ta koşan 32 bit bir süreçte bile doğrusunu verir, o yüzden ilk sırada.
#    Ama her makinede okunamıyor: Windows PowerShell 5.1'in altındaki .NET Framework
#    4.7.1'den eskiyse bu tip hiç yoktur, kısıtlı dil kipinde (ConstrainedLanguage,
#    AppLocker/WDAC) statik üyeye erişilemez. İkisinde de elde boş kalır ve kurulumu
#    düşüren okuma buydu.
# 2. PROCESSOR_ARCHITEW6432 — yalnız WOW64 altında dolu ve işletim sisteminin mimarisini
#    söyler. Doluysa bir alttakinden daha doğrudur, o yüzden ondan önce.
# 3. PROCESSOR_ARCHITECTURE — sürecin mimarisi. WOW64 altında 'x86' der, yani tek başına
#    yanıltır; ancak yukarıdaki ikisi susunca kullanılıyor.
# 4. Hiçbiri ad vermezse geriye bit genişliği kalıyor. Bu bir okuma değil varsayım, ve
#    varsayıldığı kullanıcıya söyleniyor.
function Resolve-Architecture {
    $candidates = @()

    try {
        $runtime = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture
        if ($null -ne $runtime) { $candidates += [string]$runtime }
    }
    catch { }

    $candidates += $env:PROCESSOR_ARCHITEW6432
    $candidates += $env:PROCESSOR_ARCHITECTURE

    foreach ($candidate in $candidates) {
        $name = ConvertTo-ArchitectureName $candidate
        if ($name) {
            return @{ Outcome = 'Read'; Architecture = $name; Note = '' }
        }
    }

    # Bu okuma da engellenebilir. Engellenirse 64 bit varsayılıyor: 32 bit Windows artık
    # yok denecek kadar az ve K3'ün istediği, bilinmeyende durmak değil devam etmek.
    $is64Bit = $true
    try { $is64Bit = [Environment]::Is64BitOperatingSystem }
    catch { }

    if ($is64Bit) {
        return @{
            Outcome = 'Assumed'
            Architecture = 'x64'
            Note = 'Mimari okunamadı; işletim sistemi 64 bit olduğu için x64 varsayıldı.'
        }
    }

    return @{
        Outcome = 'Assumed'
        Architecture = 'x86'
        Note = 'Mimari okunamadı; işletim sistemi 32 bit olduğu için x86 kabul edildi.'
    }
}

# Windows tarafında iki hedef yayımlanıyor: win-x64 ve win-arm64. x86 için yayın yok. Yayının
# kendisi altı hedef taşıyor (osx-arm64, osx-x64, linux-x64, linux-arm64 da var) ama onlar bu
# betiğin işi değil. Desteklenmeyen bir mimari KESİN okunursa burada duruluyor:
# x64 arşivini oraya sessizce kurmak çalışan ama güncellenmeyen bir kurulum bırakır, çünkü
# güncelleyici kendi mimarisinin adını arar (UpdateCheck.Rid) ve o varlık yayında yoktur.
# Mimari okunamadıysa durulmuyor — bilinmeyen ile desteklenmeyen aynı şey değil.
function Get-RuntimeIdentifier {
    $decision = Resolve-Architecture

    if (@('x64', 'arm64') -notcontains $decision.Architecture) {
        if ($decision.Outcome -eq 'Read') {
            throw "Bu mimari için yayın yok: $($decision.Architecture). VidShrink Windows'ta şu an yalnız win-x64 ve win-arm64 için yayımlanıyor."
        }
        throw 'Mimari okunamadı ve işletim sistemi 32 bit görünüyor: win-x64 yayını bu makinede çalışmaz. VidShrink Windows''ta şu an yalnız win-x64 ve win-arm64 için yayımlanıyor.'
    }

    if ($decision.Note) { Write-Host $decision.Note -ForegroundColor Yellow }
    return "win-$($decision.Architecture)"
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

$libMpvUrl = 'https://github.com/Teknesyum/VidShrink/releases/download/deps-libmpv-20260903/mpv-dev-x86_64-20260903-git-69e63f425a.7z'
$libMpvFallbackUrl = 'https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20260903/mpv-dev-x86_64-20260903-git-69e63f425a.7z'
$libMpvArchiveSha256 = 'FAC135C68A35B7639E39D72C0C365104EDBAEBDEA39A0DFDD8C36E8C8E80FAEF'
$libMpvDllSha256 = '673E6397920AB64A9C5B3A618F7F16D38854EFE72B58665F1F84E4E873B763A4'
$libMpvFileName = 'libmpv-2.dll'

# arm64 kolunun pinleri. libmpv'nin aarch64 derlemesi aynı shinchiro sürümünden, kendi
# yayınımıza aynadan kopyalanmış hâliyle; ffmpeg ise BtbN'den, çünkü GyanD yalnız x86_64
# derliyor. BtbN'in kayan `latest` etiketi her gün üstüne yazıldığı için sabitleme olmaz;
# ay sonu autobuild etiketleri kalıcı (2024-10-31'den bugüne duruyor), gün içi olanlar
# budanıyor. Sayılar SetupModel.cs'deki FfmpegPin.Arm64 ve LibMpvPin.Arm64 ile aynı.
$libMpvArm64Url = 'https://github.com/Teknesyum/VidShrink/releases/download/deps-libmpv-20260903/mpv-dev-aarch64-20260903-git-69e63f425a.7z'
$libMpvArm64FallbackUrl = 'https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20260903/mpv-dev-aarch64-20260903-git-69e63f425a.7z'
$libMpvArm64ArchiveSha256 = '9D4E0CF7370FD1DD9A91A9D8139F24A88ECE9E58B00F5A9CA50B391D03114F2F'
$libMpvArm64DllSha256 = '3BFC5A042CC6EBE45ACE74992DBC135EE84E3E1B33AFAC070F8902A2D64A22E9'

$ffmpegArm64Url = 'https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-08-31-13-27/ffmpeg-n9.0.1-11-ge47273f4d9-winarm64-gpl-9.0.zip'
$ffmpegArm64Entries = @(
    @{ Entry = 'ffmpeg-n9.0.1-11-ge47273f4d9-winarm64-gpl-9.0/bin/ffmpeg.exe'; Name = 'ffmpeg.exe'; Sha256 = 'A169B9D26B2380BE66211022525C9AC16AFFC923BA05B561024E57C5ED4281F9' },
    @{ Entry = 'ffmpeg-n9.0.1-11-ge47273f4d9-winarm64-gpl-9.0/bin/ffprobe.exe'; Name = 'ffprobe.exe'; Sha256 = '6B0B738A2DF0186811F240A7036CD1279C0A08991981ADECCEEFE6A2C2B2236C' }
)

function Use-Arm64Pins {
    $script:libMpvUrl = $libMpvArm64Url
    $script:libMpvFallbackUrl = $libMpvArm64FallbackUrl
    $script:libMpvArchiveSha256 = $libMpvArm64ArchiveSha256
    $script:libMpvDllSha256 = $libMpvArm64DllSha256
}

function Get-FileSha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

# winget'in Gyan.FFmpeg paketi arm64 kurucusu sunmuyor; sunduğu x64 ikilisi emülasyonla
# koşar ve kodlama hızını düşürür. arm64'te ffmpeg pinli arşivden, sha256'sı dosya dosya
# doğrulanarak alınıyor.
function Install-FfmpegArm64([string]$WorkRoot, [string]$Destination) {
    New-Item -ItemType Directory -Path $Destination, $WorkRoot -Force | Out-Null
    $zip = Join-Path $WorkRoot 'ffmpeg-winarm64.zip'
    Write-Host 'FFmpeg ve FFprobe indiriliyor (winarm64)...' -ForegroundColor Cyan
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $ffmpegArm64Url -OutFile $zip
    }
    catch {
        throw "ffmpeg indirilemedi: $ffmpegArm64Url"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        foreach ($item in $ffmpegArm64Entries) {
            $entry = $archive.GetEntry($item.Entry)
            if (-not $entry) { throw "ffmpeg arşivinde $($item.Entry) yok." }
            $target = Join-Path $Destination $item.Name
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
            $expected = if ($DepsSha256Override) { $DepsSha256Override.ToUpperInvariant() } else { $item.Sha256 }
            $actual = Get-FileSha256 $target
            if ($actual -ne $expected) {
                throw "$($item.Name) sağlaması tutmuyor. Beklenen $expected, bulunan $actual. Kurulum durduruldu."
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    return @{
        ffmpeg  = (Join-Path $Destination 'ffmpeg.exe')
        ffprobe = (Join-Path $Destination 'ffprobe.exe')
    }
}

function Install-LibMpv([string]$WorkRoot, [string]$Destination, [string]$Existing) {
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    $target = Join-Path $Destination $libMpvFileName
    if ($Existing -and (Test-Path -LiteralPath $Existing) -and (Get-FileSha256 $Existing) -eq $libMpvDllSha256) {
        Copy-Item -LiteralPath $Existing -Destination $target -Force
        return 'reused'
    }

    $tar = Join-Path $env:SystemRoot 'System32\tar.exe'
    if (-not (Test-Path -LiteralPath $tar)) { throw "libmpv arşivini açmak için $tar gerekli; bu Windows'ta yok." }

    $archive = Join-Path $WorkRoot 'mpv-dev.7z'
    Write-Host 'libmpv indiriliyor...' -ForegroundColor Cyan
    $ProgressPreference = 'SilentlyContinue'
    $actual = $null
    $failures = @()
    foreach ($source in @($libMpvUrl, $libMpvFallbackUrl)) {
        try {
            Invoke-WebRequest -UseBasicParsing -Uri $source -OutFile $archive
        }
        catch {
            $failures += $source
            Write-Host "libmpv indirilemedi, yedek kaynak deneniyor: $source" -ForegroundColor Yellow
            continue
        }
        $actual = Get-FileSha256 $archive
        if ($actual -eq $libMpvArchiveSha256) { break }
        Write-Host "libmpv sağlaması tutmadı, yedek kaynak deneniyor: $source" -ForegroundColor Yellow
    }

    if ($null -eq $actual) {
        throw "libmpv indirilemedi: $($failures -join ', ')"
    }
    if ($actual -ne $libMpvArchiveSha256) {
        throw "libmpv arşivinin sağlaması tutmuyor. Beklenen $libMpvArchiveSha256, bulunan $actual. Kurulum durduruldu."
    }

    $extract = Join-Path $WorkRoot 'libmpv'
    New-Item -ItemType Directory -Path $extract -Force | Out-Null
    & $tar -xf $archive -C $extract $libMpvFileName
    if ($LASTEXITCODE -ne 0) { throw "libmpv arşivi açılamadı (tar çıkış kodu $LASTEXITCODE)." }

    $dll = Join-Path $extract $libMpvFileName
    if (-not (Test-Path -LiteralPath $dll)) { throw "libmpv arşivinde $libMpvFileName yok." }
    $dllActual = Get-FileSha256 $dll
    if ($dllActual -ne $libMpvDllSha256) {
        throw "$libMpvFileName sağlaması tutmuyor. Beklenen $libMpvDllSha256, bulunan $dllActual. Kurulum durduruldu."
    }

    Copy-Item -LiteralPath $dll -Destination $target -Force
    return 'downloaded'
}

$shellMenuKeyName = 'VidShrink'
$shellShrinkMenuKeyName = 'VidShrinkKucult'
$shellPackageName = 'Teknesyum.VidShrink.Shell'
$shellCommandClsid = '7B8B4A16-E3F5-4C4A-A8D2-26B2F895BE58'
$shellShrinkTargets = @(100, 250, 500, 1000, 2000)
$shellShrinkFlag = '--kucult'

$shellMenuExtensions = @(
    'mp4', 'mkv', 'mov', 'avi', 'webm', 'wmv', 'flv', 'm4v', 'mpg', 'mpeg', 'ts', 'm2ts',
    '3gp', 'ogv', 'vob', 'asf', 'rm', 'rmvb', 'divx', 'mxf', 'f4v', 'mts', 'dav', 'gif'
)

function Test-Windows11 {
    return [Environment]::OSVersion.Version.Build -ge 22000
}

function Test-DefaultRegistryRoot([string]$Root) {
    return $Root.TrimEnd('\') -eq 'HKCU:\Software\Classes'
}

function Remove-Windows11ShellMenu([string]$Root) {
    if (-not (Test-DefaultRegistryRoot $Root)) { return 0 }
    $packages = @(Get-AppxPackage -Name $shellPackageName -ErrorAction SilentlyContinue)
    foreach ($package in $packages) {
        Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop
    }
    return $packages.Count
}

function Write-Windows11ShellMenu([string]$Root, [string]$InstallDirectory) {
    if (-not (Test-Windows11)) { return $false }

    $InstallDirectory = [IO.Path]::GetFullPath($InstallDirectory)
    $shellRoot = Join-Path $InstallDirectory 'shell'
    $templatePath = Join-Path $shellRoot 'AppxManifest.template.xml'
    $extensionPath = Join-Path $shellRoot 'VidShrink.ShellExtension.dll'
    if (-not (Test-Path -LiteralPath $templatePath) -or -not (Test-Path -LiteralPath $extensionPath)) {
        Write-Host "Bu yayin Windows 11 kabuk paketini tasimiyor; klasik menu yazildi." -ForegroundColor Yellow
        return $false
    }

    if (-not (Test-DefaultRegistryRoot $Root)) { return $false }

    $verbs = foreach ($extension in $shellMenuExtensions) {
        "            <desktop5:ItemType Type=`".$extension`"><desktop5:Verb Id=`"VidShrink$extension`" Clsid=`"$shellCommandClsid`" /></desktop5:ItemType>"
    }
    $manifestPath = Join-Path $shellRoot 'AppxManifest.xml'
    $template = [IO.File]::ReadAllText($templatePath)
    [IO.File]::WriteAllText($manifestPath, $template.Replace('__ITEM_TYPES__', ($verbs -join [Environment]::NewLine)), [Text.UTF8Encoding]::new($false))

    Remove-Windows11ShellMenu $Root | Out-Null
    try {
        Add-AppxPackage -Register $manifestPath -ExternalLocation $InstallDirectory -ErrorAction Stop
    }
    catch {
        Write-Host "Windows 11 birincil sağ tık menüsü eklenemedi (imzasız paket için geliştirici modu gerekiyor); klasik menü 'Daha fazla seçenek göster' altında çalışır." -ForegroundColor Yellow
        return $false
    }
    return $true
}

function Resolve-ShellMenuLanguage([string]$Language, [string]$LocalesFolder) {
    $adaylar = @()
    if ($Language -and $Language -ne 'auto') { $adaylar += $Language }
    try { $adaylar += (Get-UICulture).Name } catch { }

    foreach ($aday in $adaylar) {
        $parcalar = @($aday -split '-' | Where-Object { $_ })
        for ($uzunluk = $parcalar.Count; $uzunluk -ge 1; $uzunluk--) {
            $etiket = ($parcalar[0..($uzunluk - 1)] -join '-')
            if ($LocalesFolder -and (Test-Path -LiteralPath $LocalesFolder)) {
                $klasor = Get-ChildItem -LiteralPath $LocalesFolder -Directory -ErrorAction SilentlyContinue |
                    Where-Object { $_.Name -eq $etiket } | Select-Object -First 1
                if ($klasor) { return $klasor.Name }
            }
            elseif ($etiket -eq 'tr' -or $etiket -eq 'en') { return $etiket.ToLowerInvariant() }
        }
    }
    return 'en'
}

function Get-LocalizedShellText([string]$Language, [string]$LocalesFolder, [string]$Key) {
    if (-not $LocalesFolder) { return $null }
    $dosya = Join-Path (Join-Path $LocalesFolder $Language) 'main.json'
    if (-not (Test-Path -LiteralPath $dosya)) { return $null }
    try {
        $tablo = Get-Content -LiteralPath $dosya -Raw -Encoding UTF8 | ConvertFrom-Json
        $metin = $tablo.$Key
        if ($metin) { return $metin }
    } catch { }
    return $null
}

function Get-ShellMenuLabel([string]$Language, [string]$LocalesFolder) {
    $metin = Get-LocalizedShellText $Language $LocalesFolder 'shell.menu.open'
    if ($metin) { return $metin }
    if ($Language -eq 'tr') { return 'Bu Videoyu VidShrink ile A' + [char]0x00E7 }
    return 'Open this video with VidShrink'
}

function Get-ShellMenuAssociationRoot([string]$Root) {
    return (Join-Path $Root 'SystemFileAssociations')
}

function Get-ShellShrinkMenuLabel([string]$Language, [string]$LocalesFolder) {
    $metin = Get-LocalizedShellText $Language $LocalesFolder 'shell.menu.shrink'
    if ($metin) { return $metin }
    if ($Language -eq 'tr') { return 'VidShrink ile K' + [char]0x00FC + [char]0x00E7 + [char]0x00FC + 'lt' }
    return 'Shrink with VidShrink'
}

function Get-QuickShrinkLabel([int]$Megabytes) {
    if ($Megabytes -ge 1000 -and ($Megabytes % 1000) -eq 0) { return "$($Megabytes / 1000) GB" }
    return "$Megabytes MB"
}

function Remove-ShellMenu([string]$Root) {
    $associations = Get-ShellMenuAssociationRoot $Root
    if (-not (Test-Path -LiteralPath $associations)) { return 0 }

    $removed = 0
    foreach ($association in Get-ChildItem -LiteralPath $associations) {
        $shell = Join-Path $association.PSPath 'shell'
        $touched = $false
        foreach ($menuKeyName in @($shellMenuKeyName, $shellShrinkMenuKeyName)) {
            $key = Join-Path $shell $menuKeyName
            if (-not (Test-Path -LiteralPath $key)) { continue }
            Remove-Item -LiteralPath $key -Recurse -Force
            $touched = $true
        }
        if ($touched) { $removed++ }

        foreach ($parent in $shell, $association.PSPath) {
            if (-not (Test-Path -LiteralPath $parent)) { break }
            $item = Get-Item -LiteralPath $parent
            if ($item.SubKeyCount -gt 0 -or $item.ValueCount -gt 0) { break }
            Remove-Item -LiteralPath $parent -Force
        }
    }
    return $removed
}

function Write-ShellMenu([string]$Root, [string]$Executable, [string]$Label) {
    foreach ($extension in $shellMenuExtensions) {
        $key = Join-Path (Get-ShellMenuAssociationRoot $Root) ".$extension\shell\$shellMenuKeyName"
        New-Item -Path $key -Force | Out-Null
        Set-ItemProperty -LiteralPath $key -Name 'MUIVerb' -Value $Label -Type String
        Set-ItemProperty -LiteralPath $key -Name 'Icon' -Value $Executable -Type String

        $command = Join-Path $key 'command'
        New-Item -Path $command -Force | Out-Null
        Set-Item -LiteralPath $command -Value ('"{0}" "%1"' -f $Executable)
    }
    return $shellMenuExtensions.Count
}

function Write-ShellShrinkMenu([string]$Root, [string]$Executable, [string]$Label) {
    $written = 0
    foreach ($extension in $shellMenuExtensions) {
        $verbKey = Join-Path (Get-ShellMenuAssociationRoot $Root) ".$extension\shell\$shellShrinkMenuKeyName"
        New-Item -Path $verbKey -Force | Out-Null
        Set-ItemProperty -LiteralPath $verbKey -Name 'MUIVerb' -Value $Label -Type String
        Set-ItemProperty -LiteralPath $verbKey -Name 'Icon' -Value $Executable -Type String
        Set-ItemProperty -LiteralPath $verbKey -Name 'SubCommands' -Value '' -Type String
        Set-ItemProperty -LiteralPath $verbKey -Name 'MultiSelectModel' -Value 'Player' -Type String

        foreach ($target in $shellShrinkTargets) {
            $targetKey = Join-Path $verbKey "shell\$target"
            New-Item -Path $targetKey -Force | Out-Null
            Set-ItemProperty -LiteralPath $targetKey -Name 'MUIVerb' -Value (Get-QuickShrinkLabel $target) -Type String
            Set-ItemProperty -LiteralPath $targetKey -Name 'MultiSelectModel' -Value 'Player' -Type String

            $command = Join-Path $targetKey 'command'
            New-Item -Path $command -Force | Out-Null
            Set-Item -LiteralPath $command -Value ('"{0}" {1} {2} "%1"' -f $Executable, $shellShrinkFlag, $target)
            $written++
        }
    }
    return $written
}

function Update-ShellMenu([string]$Root, [string]$Executable, [string]$Language) {
    Remove-ShellMenu $Root | Out-Null
    $locales = Join-Path (Join-Path (Split-Path -Parent $Executable) 'app') 'Locales'
    $dil = Resolve-ShellMenuLanguage $Language $locales
    $written = Write-ShellMenu $Root $Executable (Get-ShellMenuLabel $dil $locales)
    $shrinkWritten = Write-ShellShrinkMenu $Root $Executable (Get-ShellShrinkMenuLabel $dil $locales)
    $modern = Write-Windows11ShellMenu $Root (Split-Path -Parent $Executable)
    $path = if ($modern) { 'Windows 11 birincil ve klasik' } else { 'Windows 10 klasik' }
    Write-Host "Sağ tık menüsü $written uzantıya, küçültme alt menüsü $shrinkWritten girdiye yazıldı ($path menü)." -ForegroundColor Green
}

$fileAssociationProgId = 'Teknesyum.VidShrink.Video'
$fileAssociationName = 'VidShrink'
$fileAssociationCapabilities = 'Teknesyum\VidShrink\Capabilities'

function Get-CurrentUserSubKey([string]$Path) {
    $trimmed = $Path.TrimEnd('\')
    if ($trimmed -notmatch '^HKCU:\\(.+)$') { throw "Kayit koku HKCU:\ altinda olmali: $Path" }
    return $Matches[1]
}

function Get-AssociationSoftwareRoot([string]$Root) {
    if (Test-DefaultRegistryRoot $Root) { return 'Software' }
    return (Get-CurrentUserSubKey $Root)
}

function Open-CurrentUserKey([string]$SubKey) {
    return [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($SubKey)
}

function Set-RegistryString([string]$SubKey, [string]$Name, [string]$Value) {
    $key = Open-CurrentUserKey $SubKey
    try { $key.SetValue($Name, $Value, [Microsoft.Win32.RegistryValueKind]::String) }
    finally { $key.Close() }
}

function Remove-EmptyCurrentUserKey([string]$SubKey) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($SubKey)
    if ($null -eq $key) { return }
    $empty = $key.SubKeyCount -eq 0 -and $key.ValueCount -eq 0
    $key.Close()
    if ($empty) { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKey($SubKey, $false) }
}

function Send-AssociationChanged([string]$Root) {
    if (-not (Test-DefaultRegistryRoot $Root)) { return }
    try {
        Add-Type -Namespace VidShrinkKurulum -Name Kabuk -MemberDefinition '[DllImport("shell32.dll")] public static extern void SHChangeNotify(int eventId, uint flags, IntPtr first, IntPtr second);' -ErrorAction Stop
        [VidShrinkKurulum.Kabuk]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
    }
    catch { }
}

function Get-OpenCommandTarget([string]$Executable) {
    if ([IO.Path]::GetFileName($Executable) -ine 'VidShrink.exe') { return $Executable }
    return [IO.Path]::Combine([IO.Path]::GetDirectoryName($Executable), 'app\VidShrink.App.exe')
}

function Write-FileAssociation([string]$Root, [string]$Executable) {
    $classes = Get-CurrentUserSubKey $Root
    $software = Get-AssociationSoftwareRoot $Root
    $command = '"{0}" "%1"' -f (Get-OpenCommandTarget $Executable)
    $progId = "$classes\$fileAssociationProgId"
    $application = "$classes\Applications\$([IO.Path]::GetFileName($Executable))"
    $capabilities = "$software\$fileAssociationCapabilities"

    Set-RegistryString $progId '' $fileAssociationName
    Set-RegistryString $progId 'FriendlyTypeName' $fileAssociationName
    Set-RegistryString "$progId\DefaultIcon" '' "$Executable,0"
    Set-RegistryString "$progId\shell\open\command" '' $command

    Set-RegistryString $application 'FriendlyAppName' $fileAssociationName
    Set-RegistryString "$application\shell\open\command" '' $command

    Set-RegistryString $capabilities 'ApplicationName' $fileAssociationName
    Set-RegistryString $capabilities 'ApplicationDescription' $fileAssociationName

    foreach ($extension in $shellMenuExtensions) {
        Set-RegistryString "$application\SupportedTypes" ".$extension" ''
        Set-RegistryString "$capabilities\FileAssociations" ".$extension" $fileAssociationProgId

        $list = Open-CurrentUserKey "$classes\.$extension\OpenWithProgids"
        try { $list.SetValue($fileAssociationProgId, [byte[]]@(), [Microsoft.Win32.RegistryValueKind]::None) }
        finally { $list.Close() }
    }

    Set-RegistryString "$software\RegisteredApplications" $fileAssociationName $capabilities
    Send-AssociationChanged $Root
    return $shellMenuExtensions.Count
}

function Remove-FileAssociation([string]$Root) {
    $classes = Get-CurrentUserSubKey $Root
    $software = Get-AssociationSoftwareRoot $Root
    $user = [Microsoft.Win32.Registry]::CurrentUser
    $removed = 0

    foreach ($extension in $shellMenuExtensions) {
        $listPath = "$classes\.$extension\OpenWithProgids"
        $list = $user.OpenSubKey($listPath, $true)
        if ($null -eq $list) { continue }
        $had = @($list.GetValueNames()) -contains $fileAssociationProgId
        if ($had) { $list.DeleteValue($fileAssociationProgId, $false); $removed++ }
        $list.Close()
        Remove-EmptyCurrentUserKey $listPath
        Remove-EmptyCurrentUserKey "$classes\.$extension"
    }

    $user.DeleteSubKeyTree("$classes\$fileAssociationProgId", $false)
    $user.DeleteSubKeyTree("$classes\Applications\VidShrink.exe", $false)
    Remove-EmptyCurrentUserKey "$classes\Applications"
    $user.DeleteSubKeyTree("$software\$fileAssociationCapabilities", $false)
    Remove-EmptyCurrentUserKey "$software\Teknesyum\VidShrink"
    Remove-EmptyCurrentUserKey "$software\Teknesyum"

    $registered = $user.OpenSubKey("$software\RegisteredApplications", $true)
    if ($null -ne $registered) {
        $registered.DeleteValue($fileAssociationName, $false)
        $registered.Close()
        if (-not (Test-DefaultRegistryRoot $Root)) { Remove-EmptyCurrentUserKey "$software\RegisteredApplications" }
    }

    Send-AssociationChanged $Root
    return $removed
}

function Remove-Shortcut([string]$Path, [string]$Root) {
    if (-not (Test-Path -LiteralPath $Path)) { return $false }
    $shell = New-Object -ComObject WScript.Shell
    $target = $shell.CreateShortcut($Path).TargetPath
    if (-not $target -or -not $target.StartsWith($Root, [StringComparison]::OrdinalIgnoreCase)) { return $false }
    Remove-Item -LiteralPath $Path -Force
    return $true
}

function Test-ProgramsInstallRoot([string]$Root) {
    $programsRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
    return ($Root -ne $programsRoot -and
        $Root.StartsWith($programsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))
}

function Get-InstallRootHolder([string]$Root) {
    $holders = @()
    foreach ($processName in 'VidShrink.App', 'VidShrink') {
        $holders += @(Get-Process $processName -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -and $_.Path.StartsWith($Root, [StringComparison]::OrdinalIgnoreCase) })
    }
    return $holders
}

function Remove-InstallRoot([string]$Root) {
    if (-not (Test-Path -LiteralPath $Root)) { return }

    $delay = $script:RemoveFirstDelayMilliseconds
    $waited = 0
    $lastMessage = ''
    $holderRounds = 0

    for ($attempt = 1; $attempt -le $script:RemoveAttempts; $attempt++) {
        try {
            Remove-Item -LiteralPath $Root -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            $lastMessage = $_.Exception.Message
        }

        $holders = @(Get-InstallRootHolder $Root)
        if ($holders.Count -gt 0) { $holderRounds++ } else { $holderRounds = 0 }
        if ($holderRounds -ge 2) {
            $names = ($holders | ForEach-Object { "$($_.ProcessName) (PID $($_.Id))" }) -join ', '
            Write-Host "VidShrink kapanmayı bekliyor ($names); virüs taraması sürüyorsa en çok $script:RemoveHolderWaitSeconds sn beklenecek..." -ForegroundColor Yellow
            $holders | Stop-Process -Force -ErrorAction SilentlyContinue
            $holders | Wait-Process -Timeout $script:RemoveHolderWaitSeconds -ErrorAction SilentlyContinue
            $still = @(Get-InstallRootHolder $Root)
            if ($still.Count -gt 0) {
                $names = ($still | ForEach-Object { "$($_.ProcessName) (PID $($_.Id))" }) -join ', '
                throw ("Kurulum klasörü silinemedi: VidShrink $script:RemoveHolderWaitSeconds sn sonra hâlâ açık - $names. " +
                    "Virüs programı dosyayı tarıyorsa taramanın bitmesini bekleyip komutu yeniden çalıştırın. Klasör: $Root")
            }
            $holderRounds = 0
            try {
                Remove-Item -LiteralPath $Root -Recurse -Force -ErrorAction Stop
                return
            }
            catch {
                $lastMessage = $_.Exception.Message
            }
        }

        if ($attempt -lt $script:RemoveAttempts) {
            Write-Host "Kurulum klasörü kilitli, $delay ms sonra yeniden denenecek ($attempt/$script:RemoveAttempts)..." -ForegroundColor Yellow
            Start-Sleep -Milliseconds $delay
            $waited += $delay
            $delay = $delay * 2
        }
    }

    throw ("Kurulum klasörü $script:RemoveAttempts denemede ve $waited ms beklemede silinemedi: $Root. " +
        'Bir dosya başka bir süreçte açık - genellikle virüs taraması ya da Gezgin önizlemesi; ' +
        "birkaç saniye sonra komutu yeniden çalıştırın. Son hata: $lastMessage")
}

if ($Uninstall) {
    $resolvedUninstallRoot = [IO.Path]::GetFullPath($InstallRoot)
    foreach ($processName in 'VidShrink.App', 'VidShrink') {
        Get-Process $processName -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -and $_.Path.StartsWith($resolvedUninstallRoot, [StringComparison]::OrdinalIgnoreCase) } |
            Stop-Process -Force
    }

    $cleared = Remove-ShellMenu $RegistryRoot
    $packages = Remove-Windows11ShellMenu $RegistryRoot
    $unlinked = Remove-FileAssociation $RegistryRoot
    Write-Host "Sağ tık menüsü ($cleared uzantı, $packages paket) ve dosya ilişkilendirmesi ($unlinked uzantı) kaldırıldı." -ForegroundColor Green

    if (-not $SkipShortcuts) {
        Remove-Shortcut (Join-Path ([Environment]::GetFolderPath('Desktop')) 'VidShrink.lnk') $resolvedUninstallRoot | Out-Null
        $startMenuDirectory = Join-Path ([Environment]::GetFolderPath('Programs')) 'VidShrink'
        if (Remove-Shortcut (Join-Path $startMenuDirectory 'VidShrink.lnk') $resolvedUninstallRoot) {
            if (-not (Get-ChildItem -LiteralPath $startMenuDirectory -Force)) { Remove-Item -LiteralPath $startMenuDirectory -Force }
        }
    }

    if (Test-ProgramsInstallRoot $resolvedUninstallRoot) {
        Remove-InstallRoot $resolvedUninstallRoot
        Write-Host "VidShrink kaldırıldı: $resolvedUninstallRoot" -ForegroundColor Green
    }
    else {
        Write-Host "Kurulum klasörü LocalAppData\Programs altında değil, silinmedi: $resolvedUninstallRoot" -ForegroundColor Yellow
    }
    return
}

if ($RemoveShellMenu -or $RemoveFileAssociation) {
    if ($RemoveShellMenu) {
        $cleared = Remove-ShellMenu $RegistryRoot
        $packages = Remove-Windows11ShellMenu $RegistryRoot
        Write-Host "Sağ tık menüsü kaldırıldı: $cleared uzantı, $packages paket." -ForegroundColor Green
    }
    if ($RemoveFileAssociation) {
        $unlinked = Remove-FileAssociation $RegistryRoot
        Write-Host "Dosya ilişkilendirmesi kaldırıldı: $unlinked uzantı." -ForegroundColor Green
    }
    return
}

if ($ShellMenuOnly) {
    if ($SkipShortcuts) {
        Write-Host 'SkipShortcuts verildi; kabuğa dokunulmadı.' -ForegroundColor Yellow
        return
    }
    $shellMenuExecutable = Join-Path $InstallRoot 'VidShrink.exe'
    if (-not (Test-Path -LiteralPath $shellMenuExecutable)) {
        throw "Kurulu VidShrink.exe bulunamadı: $shellMenuExecutable. Önce kurulumu çalıştırın."
    }
    Update-ShellMenu $RegistryRoot $shellMenuExecutable $MenuLanguage
    $associated = Write-FileAssociation $RegistryRoot $shellMenuExecutable
    Write-Host "Dosya ilişkilendirmesi $associated uzantıya yazıldı." -ForegroundColor Green
    return
}

Write-Host 'VidShrink kurulumu hazırlanıyor...' -ForegroundColor Cyan

$runtimeIdentifier = Get-RuntimeIdentifier
$isArm64 = $runtimeIdentifier -eq 'win-arm64'
if ($isArm64) { Use-Arm64Pins }

if ($DepsOnly) {
    $depsRoot = [IO.Path]::GetFullPath($DepsOnly)
    $depsWork = Join-Path $depsRoot 'is'
    New-Item -ItemType Directory -Path $depsRoot, $depsWork -Force | Out-Null
    Write-Host "Bağımlılık kolu: $runtimeIdentifier" -ForegroundColor Cyan

    if ($isArm64) {
        $depsFfmpeg = Install-FfmpegArm64 $depsWork (Join-Path $depsRoot 'ffmpeg')
        Write-Host "ffmpeg hazır (sha256 doğrulandı): $($depsFfmpeg.ffmpeg)" -ForegroundColor Green
    }
    else {
        Write-Host 'x64 kolunda ffmpeg winget ile geliyor; bağımlılık kolu yalnız libmpv indirir.' -ForegroundColor Yellow
    }

    $depsLibMpv = Join-Path $depsRoot 'libmpv'
    Install-LibMpv $depsWork $depsLibMpv '' | Out-Null
    Write-Host "libmpv hazır (sha256 doğrulandı): $(Join-Path $depsLibMpv $libMpvFileName)" -ForegroundColor Green
    return
}

$ffmpeg = Find-Tool 'ffmpeg'
$ffprobe = Find-Tool 'ffprobe'
if (-not $ffmpeg -or -not $ffprobe) {
    if ($isArm64) {
        $ffmpegWork = Join-Path ([IO.Path]::GetTempPath()) ("vidshrink-ffmpeg-" + [Guid]::NewGuid().ToString('N'))
        $fetchedFfmpeg = Install-FfmpegArm64 $ffmpegWork (Join-Path $ffmpegWork 'bin')
        $ffmpeg = $fetchedFfmpeg.ffmpeg
        $ffprobe = $fetchedFfmpeg.ffprobe
    }
    else {
        Write-Host 'FFmpeg ve FFprobe yükleniyor...' -ForegroundColor Cyan
        Install-WinGetPackage 'Gyan.FFmpeg'
        $ffmpeg = Find-Tool 'ffmpeg'
        $ffprobe = Find-Tool 'ffprobe'
    }
}
if (-not $ffmpeg -or -not $ffprobe) { throw 'FFmpeg veya FFprobe kurulumdan sonra bulunamadı.' }

$resolvedInstallRoot = [IO.Path]::GetFullPath($InstallRoot)
if (-not (Test-ProgramsInstallRoot $resolvedInstallRoot)) {
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
    Set-Content -LiteralPath (Join-Path $stageRoot '.launcher-version') -Value $version -Encoding UTF8 -NoNewline

    $toolsRoot = Join-Path $stageRoot 'tools\ffmpeg'
    New-Item -ItemType Directory -Path $toolsRoot -Force | Out-Null
    Copy-Item -LiteralPath $ffmpeg -Destination (Join-Path $toolsRoot 'ffmpeg.exe') -Force
    Copy-Item -LiteralPath $ffprobe -Destination (Join-Path $toolsRoot 'ffprobe.exe') -Force

    $libMpvRoot = Join-Path $stageRoot 'tools\libmpv'
    $installedLibMpv = Join-Path $resolvedInstallRoot "tools\libmpv\$libMpvFileName"
    $libMpvSource = Install-LibMpv $workRoot $libMpvRoot $installedLibMpv
    Write-Host "libmpv hazır ($libMpvSource, sha256 doğrulandı)." -ForegroundColor Cyan

    foreach ($processName in 'VidShrink.App', 'VidShrink') {
        Get-Process $processName -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -and $_.Path.StartsWith($resolvedInstallRoot, [StringComparison]::OrdinalIgnoreCase) } |
            Stop-Process -Force
    }

    Remove-Windows11ShellMenu $RegistryRoot | Out-Null
    Remove-InstallRoot $resolvedInstallRoot
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

        Update-ShellMenu $RegistryRoot $installedExe $MenuLanguage
        $associated = Write-FileAssociation $RegistryRoot $installedExe
        Write-Host "Dosya ilişkilendirmesi $associated uzantıya yazıldı." -ForegroundColor Green
    }

    Write-Host "VidShrink $version kuruldu: $resolvedInstallRoot" -ForegroundColor Green
    if (-not $NoLaunch) { Start-Process -FilePath $installedExe }
}
finally {
    if (Test-Path -LiteralPath $workRoot) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
