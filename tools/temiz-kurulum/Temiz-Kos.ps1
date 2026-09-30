param(
    [Parameter(Mandatory = $true)][string]$Sonuc,
    [ValidateSet('ikisi', 'kurucu', 'tek-satir')][string]$Yol = 'ikisi',
    [string]$Etiket = '',
    [int]$AyaktaSaniye = 20,
    [int]$KurulumDakika = 20,
    [string[]]$KurucuEk = @()
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'
New-Item -ItemType Directory -Force -Path $Sonuc | Out-Null
$gunluk = Join-Path $Sonuc 'gunluk.txt'
$depo = 'Teknesyum/VidShrink'
$tekSatirKomutu = '[Net.ServicePointManager]::SecurityProtocol=3072; Set-Variable ProgressPreference SilentlyContinue; Set-Location ([IO.Path]::GetTempPath()); irm https://github.com/Teknesyum/VidShrink/releases/latest/download/VidShrink-Setup.exe -OutFile VidShrink-Setup.exe; .\VidShrink-Setup.exe'

function Yaz([string]$Metin) {
    $satir = '{0:HH:mm:ss} {1}' -f (Get-Date), $Metin
    Write-Host $satir
    Add-Content -LiteralPath $gunluk -Value $satir -Encoding UTF8
}

Add-Type -Namespace TemizKurulum -Name Yerel -MemberDefinition @'
[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
public static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);
[DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
public static extern IntPtr GetProcAddress(IntPtr module, string name);
[DllImport("kernel32.dll")]
public static extern bool FreeLibrary(IntPtr module);
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
public static extern IntPtr FindWindowW(string className, string title);
[DllImport("user32.dll")]
public static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
[DllImport("user32.dll")]
public static extern bool IsWindow(IntPtr window);
'@

function Kayit([string]$Yol, [string]$Ad) {
    try { return (Get-ItemProperty -LiteralPath $Yol -Name $Ad -ErrorAction Stop).$Ad } catch { return $null }
}

function Ortam {
    $os = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
    $webview = $null
    foreach ($anahtar in 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
                        'HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
                        'HKCU:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') {
        $pv = Kayit $anahtar 'pv'
        if ($pv -and $pv -ne '0.0.0.0') { $webview = $pv; break }
    }
    $sistem = Join-Path $env:windir 'System32'
    try { $isletim = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() } catch { $isletim = $env:PROCESSOR_ARCHITEW6432 }
    return [ordered]@{
        urun               = $os.ProductName
        surum              = $os.DisplayVersion
        derleme            = '{0}.{1}' -f $os.CurrentBuild, $os.UBR
        windows11          = ([int]$os.CurrentBuild -ge 22000)
        islemciMimarisi    = $env:PROCESSOR_ARCHITECTURE
        isletimMimarisi    = $isletim
        powershell         = $PSVersionTable.PSVersion.ToString()
        tlsVarsayilan      = [Net.ServicePointManager]::SecurityProtocol.ToString()
        yurutmeIlkesi      = (Get-ExecutionPolicy).ToString()
        winget             = [bool](Get-Command winget.exe -ErrorAction SilentlyContinue)
        webView2           = $webview
        vcRuntime140       = (Test-Path -LiteralPath (Join-Path $sistem 'vcruntime140.dll'))
        vulkanSistemde     = (Test-Path -LiteralPath (Join-Path $sistem 'vulkan-1.dll'))
        dotnetKurulu       = (Test-Path -LiteralPath (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'))
        akilliUygulama     = Kayit 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy' 'VerifiedAndReputablePolicyState'
        kullanici          = $env:USERNAME
        temp               = [IO.Path]::GetTempPath()
    }
}

function Indir([string]$Adres, [string]$Hedef) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    for ($deneme = 1; $deneme -le 5; $deneme++) {
        try {
            Invoke-WebRequest -UseBasicParsing -Uri $Adres -OutFile $Hedef -ErrorAction Stop
            return $true
        }
        catch {
            Yaz "indirme denemesi $deneme düştü: $($_.Exception.Message)"
            Start-Sleep -Seconds 10
        }
    }
    return $false
}

function Surec([string]$Dosya, [string[]]$Argumanlar, [string]$Ad, [int]$Dakika) {
    $cikti = Join-Path $Sonuc "$Ad-stdout.txt"
    $hata = Join-Path $Sonuc "$Ad-stderr.txt"
    $p = Start-Process -FilePath $Dosya -ArgumentList $Argumanlar -RedirectStandardOutput $cikti -RedirectStandardError $hata -NoNewWindow -PassThru
    $null = $p.Handle
    if (-not $p.WaitForExit($Dakika * 60000)) {
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        return 'zaman-asimi'
    }
    return $p.ExitCode
}

function Ekran([string]$Ad) {
    try {
        Add-Type -AssemblyName System.Windows.Forms, System.Drawing
        $alan = [Windows.Forms.SystemInformation]::VirtualScreen
        $resim = New-Object Drawing.Bitmap $alan.Width, $alan.Height
        $cizim = [Drawing.Graphics]::FromImage($resim)
        $cizim.CopyFromScreen($alan.Location, [Drawing.Point]::Empty, $alan.Size)
        $resim.Save((Join-Path $Sonuc "$Ad.png"), [Drawing.Imaging.ImageFormat]::Png)
        $cizim.Dispose(); $resim.Dispose()
    }
    catch { Yaz "ekran görüntüsü alınamadı: $($_.Exception.Message)" }
}

function Denetle-Kurulum([string]$Kok, [string]$Ad) {
    $d = [ordered]@{ kok = $Kok }
    foreach ($parca in 'VidShrink.exe', 'app\VidShrink.App.exe', 'tools\ffmpeg\ffmpeg.exe', 'tools\ffmpeg\ffprobe.exe', 'tools\libmpv\libmpv-2.dll', 'tools\libmpv\vulkan-1.dll') {
        $d[$parca] = Test-Path -LiteralPath (Join-Path $Kok $parca)
    }

    $ffmpeg = Join-Path $Kok 'tools\ffmpeg\ffmpeg.exe'
    $d.ffmpegKodlama = $false
    if (Test-Path -LiteralPath $ffmpeg) {
        $deneme = Join-Path ([IO.Path]::GetTempPath()) ("temiz-kurulum-" + [Guid]::NewGuid().ToString('N') + '.mp4')
        & $ffmpeg -hide_banner -loglevel error -y -f lavfi -i 'testsrc=duration=1:size=320x240:rate=10' -threads 1 -c:v libx264 -preset ultrafast $deneme 2>&1 |
            Out-File -LiteralPath (Join-Path $Sonuc "$Ad-ffmpeg.txt") -Encoding UTF8
        $d.ffmpegKodlama = ($LASTEXITCODE -eq 0) -and (Test-Path -LiteralPath $deneme) -and ((Get-Item -LiteralPath $deneme).Length -gt 0)
        Remove-Item -LiteralPath $deneme -Force -ErrorAction SilentlyContinue
    }

    $libmpv = Join-Path $Kok 'tools\libmpv\libmpv-2.dll'
    $d.libmpvYuklendi = $false
    $d.libmpvHata = $null
    if (Test-Path -LiteralPath $libmpv) {
        $modul = [TemizKurulum.Yerel]::LoadLibraryExW($libmpv, [IntPtr]::Zero, 0x1100)
        if ($modul -eq [IntPtr]::Zero) {
            $d.libmpvHata = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        }
        else {
            $d.libmpvYuklendi = [TemizKurulum.Yerel]::GetProcAddress($modul, 'mpv_create') -ne [IntPtr]::Zero
            [void][TemizKurulum.Yerel]::FreeLibrary($modul)
        }
    }
    return $d
}

function Kapat-Uygulama {
    Get-Process -Name 'VidShrink.App', 'VidShrink' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
}

function Denetle-Acilis([string]$Ad, [datetime]$Once) {
    Start-Sleep -Seconds $AyaktaSaniye
    $r = [ordered]@{ bekleme = $AyaktaSaniye }
    $uygulama = Get-Process -Name 'VidShrink.App' -ErrorAction SilentlyContinue | Select-Object -First 1
    $r.ayakta = [bool]$uygulama
    $r.pencere = $false
    if ($uygulama) {
        $uygulama.Refresh()
        $r.pencere = $uygulama.MainWindowHandle -ne [IntPtr]::Zero
        $r.baslik = $uygulama.MainWindowTitle
        $r.yanitVeriyor = $uygulama.Responding
        $r.bellekMB = [math]::Round($uygulama.WorkingSet64 / 1MB)
    }
    Ekran "$Ad-uygulama"
    $r.olaylar = @(Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = $Once; Level = 1, 2 } -ErrorAction SilentlyContinue |
        Where-Object { $_.Message -match 'VidShrink' } | Select-Object -First 5 |
        ForEach-Object { '{0}: {1}' -f $_.ProviderName, (($_.Message -split "`r?`n" | Select-Object -First 4) -join ' | ') })
    Kapat-Uygulama
    return $r
}

function Gecti($Kurulum, $Acilis) {
    if (-not $Kurulum -or -not $Acilis) { return $false }
    foreach ($anahtar in @($Kurulum.Keys)) {
        if ($anahtar -in 'kok', 'libmpvHata') { continue }
        if ($Kurulum[$anahtar] -ne $true) { return $false }
    }
    return ($Acilis.ayakta -and $Acilis.pencere)
}

function Yol-Kurucu {
    $r = [ordered]@{ yol = 'kurucu' }
    $klasor = Join-Path $env:USERPROFILE 'Downloads'
    New-Item -ItemType Directory -Force -Path $klasor | Out-Null
    $kurucu = Join-Path $klasor 'VidShrink-Setup.exe'
    if ($Etiket) { $adres = "https://github.com/$depo/releases/download/$Etiket/VidShrink-Setup.exe" }
    else { $adres = "https://github.com/$depo/releases/latest/download/VidShrink-Setup.exe" }
    Yaz "kurucu indiriliyor: $adres"
    $r.indirildi = Indir $adres $kurucu
    if (-not $r.indirildi) { $r.gecti = $false; return $r }
    $r.imza = (Get-AuthenticodeSignature -LiteralPath $kurucu).Status.ToString()

    $argumanlar = @('--console', '--no-launch') + $KurucuEk
    if ($Etiket) { $argumanlar += @('--tag', $Etiket) }
    Yaz "kurucu sessiz kipte: $($argumanlar -join ' ')"
    $saat = [Diagnostics.Stopwatch]::StartNew()
    $r.cikisKodu = Surec $kurucu $argumanlar 'kurucu' $KurulumDakika
    $r.saniye = [math]::Round($saat.Elapsed.TotalSeconds)
    Yaz "kurucu çıkış kodu: $($r.cikisKodu) ($($r.saniye) sn)"

    $kok = Join-Path $env:LOCALAPPDATA 'Programs\VidShrink'
    $r.kurulum = Denetle-Kurulum $kok 'kurucu'
    $r.kisayollar = [ordered]@{
        masaustu = Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'VidShrink.lnk')
        baslat   = Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('Programs')) 'VidShrink\VidShrink.lnk')
    }
    $once = Get-Date
    if (Test-Path -LiteralPath (Join-Path $kok 'VidShrink.exe')) {
        Start-Process -FilePath (Join-Path $kok 'VidShrink.exe') -WorkingDirectory $kok | Out-Null
        $r.acilis = Denetle-Acilis 'kurucu' $once
    }
    $r.gecti = ($r.cikisKodu -eq 0) -and (Gecti $r.kurulum $r.acilis)

    Yaz 'kaldırma: --console --uninstall'
    $r.kaldirmaCikisKodu = Surec $kurucu @('--console', '--uninstall') 'kaldirma' 5
    $r.kaldirmaSonrasiKokVar = Test-Path -LiteralPath $kok
    return $r
}

function Yol-TekSatir {
    $r = [ordered]@{ yol = 'tek-satir'; komut = $tekSatirKomutu }
    $tuhaf = Join-Path $env:USERPROFILE 'Kullanıcı Şğü Öç'
    $temp = Join-Path $tuhaf 'Temp'
    $yerel = Join-Path $tuhaf 'Local'
    New-Item -ItemType Directory -Force -Path $temp, $yerel | Out-Null
    $r.temp = $temp
    $r.localAppData = $yerel

    $eski = @{ TEMP = $env:TEMP; TMP = $env:TMP; LOCALAPPDATA = $env:LOCALAPPDATA }
    $env:TEMP = $temp; $env:TMP = $temp; $env:LOCALAPPDATA = $yerel
    Yaz "tek satır koşuyor (TEMP ve LOCALAPPDATA boşluklu/Türkçe): $tuhaf"
    $kabuk = Start-Process -FilePath (Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe') -ArgumentList @('-NoProfile', '-Command', ('"' + $tekSatirKomutu + '"')) -PassThru
    $null = $kabuk.Handle
    $env:TEMP = $eski.TEMP; $env:TMP = $eski.TMP; $env:LOCALAPPDATA = $eski.LOCALAPPDATA

    $pencere = [IntPtr]::Zero
    $son = (Get-Date).AddMinutes(10)
    while ((Get-Date) -lt $son -and -not $kabuk.HasExited) {
        $pencere = [TemizKurulum.Yerel]::FindWindowW('VidShrinkSetupPanel', $null)
        if ($pencere -ne [IntPtr]::Zero) { break }
        Start-Sleep -Seconds 2
    }
    $r.panelAcildi = $pencere -ne [IntPtr]::Zero
    $r.indirilenKurucu = Test-Path -LiteralPath (Join-Path $temp 'VidShrink-Setup.exe')
    if (-not $r.panelAcildi) {
        if ($kabuk.HasExited) { $r.kabukCikisKodu = $kabuk.ExitCode }
        Ekran 'tek-satir-panel-yok'
        $r.gecti = $false
        return $r
    }

    Start-Sleep -Seconds 2
    Ekran 'tek-satir-panel'
    Yaz 'panel açıldı; Enter ile "Kur"'
    [void][TemizKurulum.Yerel]::PostMessageW($pencere, 0x0100, [IntPtr]0x0D, [IntPtr]::Zero)

    $kok = Join-Path $yerel 'Programs\VidShrink'
    $isGorulen = $false
    $bitti = $false
    $saat = [Diagnostics.Stopwatch]::StartNew()
    $son = (Get-Date).AddMinutes($KurulumDakika)
    while ((Get-Date) -lt $son) {
        $is = @(Get-ChildItem -LiteralPath $temp -Directory -Filter 'vidshrink-setup-*' -ErrorAction SilentlyContinue)
        if ($is.Count -gt 0) { $isGorulen = $true }
        elseif ($isGorulen) { $bitti = $true; break }
        if (-not [TemizKurulum.Yerel]::IsWindow($pencere)) { break }
        Start-Sleep -Seconds 2
    }
    $r.saniye = [math]::Round($saat.Elapsed.TotalSeconds)
    $r.kurulumBitti = $bitti
    Start-Sleep -Seconds 2
    Ekran 'tek-satir-bitti'
    $gunlukDosyasi = Join-Path $yerel 'VidShrink\kurulum.log'
    if (Test-Path -LiteralPath $gunlukDosyasi) { Copy-Item -LiteralPath $gunlukDosyasi -Destination (Join-Path $Sonuc 'tek-satir-kurulum.log') -Force }

    $r.kurulum = Denetle-Kurulum $kok 'tek-satir'
    $once = Get-Date
    if ([TemizKurulum.Yerel]::IsWindow($pencere)) {
        Yaz 'Enter ile "Programı Aç"'
        [void][TemizKurulum.Yerel]::PostMessageW($pencere, 0x0100, [IntPtr]0x0D, [IntPtr]::Zero)
        $r.acilis = Denetle-Acilis 'tek-satir' $once
    }
    if ([TemizKurulum.Yerel]::IsWindow($pencere)) {
        [void][TemizKurulum.Yerel]::PostMessageW($pencere, 0x0100, [IntPtr]0x1B, [IntPtr]::Zero)
    }
    if ($kabuk.WaitForExit(60000)) { $r.kabukCikisKodu = $kabuk.ExitCode }
    else { $r.kabukCikisKodu = 'kapanmadi'; Stop-Process -Id $kabuk.Id -Force -ErrorAction SilentlyContinue }
    Get-Process -Name 'VidShrink-Setup' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    $r.gecti = $bitti -and ($r.kabukCikisKodu -eq 0) -and (Gecti $r.kurulum $r.acilis)
    return $r
}

$ozet = [ordered]@{ baslangic = (Get-Date).ToString('s'); ortam = Ortam }
Yaz ("ortam: " + ($ozet.ortam | ConvertTo-Json -Compress))
if ($Yol -in 'ikisi', 'kurucu') { $ozet.kurucu = Yol-Kurucu }
if ($Yol -in 'ikisi', 'tek-satir') { $ozet.tekSatir = Yol-TekSatir }
$gecenler = @()
foreach ($anahtar in 'kurucu', 'tekSatir') { if ($ozet.Contains($anahtar)) { $gecenler += [bool]$ozet[$anahtar].gecti } }
$ozet.gecti = ($gecenler.Count -gt 0) -and -not ($gecenler -contains $false)
$ozet.bitis = (Get-Date).ToString('s')
$json = $ozet | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText((Join-Path $Sonuc 'bitti.json'), $json, (New-Object Text.UTF8Encoding $false))
Yaz "bitti: gecti=$($ozet.gecti)"
if ($ozet.gecti) { exit 0 } else { exit 1 }
