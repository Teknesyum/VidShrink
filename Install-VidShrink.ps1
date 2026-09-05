param(
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\VidShrink'),
    [switch]$NoLaunch,
    [switch]$SkipShortcuts,
    [switch]$ShellMenuOnly,
    [switch]$RemoveShellMenu,
    [ValidateSet('auto', 'tr', 'en')]
    [string]$MenuLanguage = 'auto',
    [string]$RegistryRoot = 'HKCU:\Software\Classes'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$repository = 'Teknesyum/VidShrink'

$script:RemoveAttempts = 6
$script:RemoveFirstDelayMilliseconds = 200

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

# Windows tarafında yayımlanan tek hedef win-x64; arm64 ve x86 için Windows yayını yok. Yayının
# kendisi dört hedef taşıyor (osx-arm64, osx-x64, linux-x64 de var) ama onlar bu betiğin
# işi değil. arm64 ya da x86 olduğu KESİN anlaşılırsa burada duruluyor:
# x64 arşivini oraya sessizce kurmak çalışan ama güncellenmeyen bir kurulum bırakır, çünkü
# güncelleyici kendi mimarisinin adını arar (UpdateCheck.Rid) ve o varlık yayında yoktur.
# Mimari okunamadıysa durulmuyor — bilinmeyen ile desteklenmeyen aynı şey değil.
function Get-RuntimeIdentifier {
    $decision = Resolve-Architecture

    if ($decision.Architecture -ne 'x64') {
        if ($decision.Outcome -eq 'Read') {
            throw "Bu mimari için yayın yok: $($decision.Architecture). VidShrink Windows'ta şu an yalnız win-x64 için yayımlanıyor."
        }
        throw 'Mimari okunamadı ve işletim sistemi 32 bit görünüyor: win-x64 yayını bu makinede çalışmaz. VidShrink Windows''ta şu an yalnız win-x64 için yayımlanıyor.'
    }

    if ($decision.Note) { Write-Host $decision.Note -ForegroundColor Yellow }
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

$shellMenuKeyName = 'VidShrink'
$shellShrinkMenuKeyName = 'VidShrinkKucult'
$shellPackageName = 'Teknesyum.VidShrink.Shell'
$shellCommandClsid = '7B8B4A16-E3F5-4C4A-A8D2-26B2F895BE58'
$shellShrinkTargets = @(100, 250, 500, 1024, 2048)
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
    Add-AppxPackage -Register $manifestPath -ExternalLocation $InstallDirectory -ErrorAction Stop
    return $true
}

function Get-ShellMenuLabel([string]$Language) {
    $choice = $Language
    if ($choice -eq 'auto') {
        $interface = ''
        try { $interface = (Get-UICulture).TwoLetterISOLanguageName } catch { }
        if ($interface -eq 'tr') { $choice = 'tr' } else { $choice = 'en' }
    }
    if ($choice -eq 'tr') { return 'Bu Videoyu VidShrink ile A' + [char]0x00E7 }
    return 'Open this video with VidShrink'
}

function Get-ShellMenuAssociationRoot([string]$Root) {
    return (Join-Path $Root 'SystemFileAssociations')
}

function Get-ShellShrinkMenuLabel([string]$Language) {
    $choice = $Language
    if ($choice -eq 'auto') {
        $interface = ''
        try { $interface = (Get-UICulture).TwoLetterISOLanguageName } catch { }
        if ($interface -eq 'tr') { $choice = 'tr' } else { $choice = 'en' }
    }
    if ($choice -eq 'tr') { return 'VidShrink ile K' + [char]0x00FC + [char]0x00E7 + [char]0x00FC + 'lt' }
    return 'Shrink with VidShrink'
}

function Get-QuickShrinkLabel([int]$Megabytes) {
    if ($Megabytes -ge 1024 -and ($Megabytes % 1024) -eq 0) { return "$($Megabytes / 1024) GB" }
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
    $written = Write-ShellMenu $Root $Executable (Get-ShellMenuLabel $Language)
    $shrinkWritten = Write-ShellShrinkMenu $Root $Executable (Get-ShellShrinkMenuLabel $Language)
    $modern = Write-Windows11ShellMenu $Root (Split-Path -Parent $Executable)
    $path = if ($modern) { 'Windows 11 birincil ve klasik' } else { 'Windows 10 klasik' }
    Write-Host "Sağ tık menüsü $written uzantıya, küçültme alt menüsü $shrinkWritten girdiye yazıldı ($path menü)." -ForegroundColor Green
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
            throw "Kurulum klasörü silinemedi: VidShrink hâlâ açık - $names. Programı kapatıp komutu yeniden çalıştırın. Klasör: $Root"
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

if ($RemoveShellMenu) {
    $cleared = Remove-ShellMenu $RegistryRoot
    $packages = Remove-Windows11ShellMenu $RegistryRoot
    Write-Host "Sağ tık menüsü kaldırıldı: $cleared uzantı, $packages paket." -ForegroundColor Green
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
    return
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
    Set-Content -LiteralPath (Join-Path $stageRoot '.launcher-version') -Value $version -Encoding UTF8 -NoNewline

    $toolsRoot = Join-Path $stageRoot 'tools\ffmpeg'
    New-Item -ItemType Directory -Path $toolsRoot -Force | Out-Null
    Copy-Item -LiteralPath $ffmpeg -Destination (Join-Path $toolsRoot 'ffmpeg.exe') -Force
    Copy-Item -LiteralPath $ffprobe -Destination (Join-Path $toolsRoot 'ffprobe.exe') -Force

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
    }

    Write-Host "VidShrink $version kuruldu: $resolvedInstallRoot" -ForegroundColor Green
    if (-not $NoLaunch) { Start-Process -FilePath $installedExe }
}
finally {
    if (Test-Path -LiteralPath $workRoot) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
