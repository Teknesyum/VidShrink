param(
    [ValidateSet('ikisi', 'kurucu', 'tek-satir')][string]$Yol = 'ikisi',
    [string]$Etiket = '',
    [int]$AyaktaSaniye = 20,
    [int]$BekleDakika = 45,
    [switch]$AcikBirak
)

$ErrorActionPreference = 'Stop'
$duzenek = $PSScriptRoot
$kok = (Resolve-Path (Join-Path $duzenek '..\..')).Path
$sandbox = Join-Path $env:windir 'System32\WindowsSandbox.exe'
if (-not (Test-Path -LiteralPath $sandbox)) {
    Write-Host 'Windows Sandbox kapalı. Yönetici PowerShell''de açıp makineyi yeniden başlatın:' -ForegroundColor Yellow
    Write-Host 'Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -All'
    exit 2
}
$surecAdlari = 'WindowsSandbox', 'WindowsSandboxClient', 'WindowsSandboxRemoteSession', 'WindowsSandboxServer'
if (Get-Process -Name $surecAdlari -ErrorAction SilentlyContinue) {
    Write-Host 'Açık bir Windows Sandbox var; aynı anda tek Sandbox çalışır. Kapatıp yeniden deneyin.' -ForegroundColor Yellow
    exit 3
}

$kosu = Get-Date -Format 'yyyyMMdd-HHmmss'
$calisma = Join-Path $kok '.calisma\temiz-kurulum'
$sonuc = Join-Path $calisma $kosu
New-Item -ItemType Directory -Force -Path $sonuc | Out-Null

$argumanlar = "-Yol $Yol -AyaktaSaniye $AyaktaSaniye"
if ($Etiket) { $argumanlar += " -Etiket $Etiket" }
$sablon = [IO.File]::ReadAllText((Join-Path $duzenek 'temiz-kurulum.wsb'))
$wsb = $sablon.Replace('__DUZENEK__', [Security.SecurityElement]::Escape($duzenek)).
    Replace('__SONUC__', [Security.SecurityElement]::Escape($sonuc)).
    Replace('__ARGUMANLAR__', [Security.SecurityElement]::Escape($argumanlar))
$wsbYolu = Join-Path $calisma "$kosu.wsb"
[IO.File]::WriteAllText($wsbYolu, $wsb, (New-Object Text.UTF8Encoding $false))

Write-Host "Sandbox açılıyor; sonuçlar: $sonuc"
Start-Process -FilePath $sandbox -ArgumentList ('"' + $wsbYolu + '"') | Out-Null

$bitti = Join-Path $sonuc 'bitti.json'
$gunluk = Join-Path $sonuc 'gunluk.txt'
$okunan = 0
$son = (Get-Date).AddMinutes($BekleDakika)
while ((Get-Date) -lt $son -and -not (Test-Path -LiteralPath $bitti)) {
    if (Test-Path -LiteralPath $gunluk) {
        $satirlar = @(Get-Content -LiteralPath $gunluk -Encoding UTF8)
        if ($satirlar.Count -gt $okunan) { $satirlar[$okunan..($satirlar.Count - 1)] | ForEach-Object { Write-Host "  $_" }; $okunan = $satirlar.Count }
    }
    Start-Sleep -Seconds 5
}

if (-not (Test-Path -LiteralPath $bitti)) {
    Write-Host "$BekleDakika dakikada bitmedi; Sandbox açık bırakıldı. Günlük: $gunluk" -ForegroundColor Red
    exit 4
}

$ozet = Get-Content -LiteralPath $bitti -Raw -Encoding UTF8 | ConvertFrom-Json
Write-Host ''
Write-Host ("Ortam: {0} {1} ({2}), {3}, WebView2={4}, VC++={5}, vulkan-1(sistem)={6}" -f $ozet.ortam.urun, $ozet.ortam.surum, $ozet.ortam.derleme, $ozet.ortam.isletimMimarisi, $ozet.ortam.webView2, $ozet.ortam.vcRuntime140, $ozet.ortam.vulkanSistemde)
foreach ($ad in 'kurucu', 'tekSatir') {
    $y = $ozet.$ad
    if (-not $y) { continue }
    $cikis = if ($null -ne $y.cikisKodu) { $y.cikisKodu } else { $y.kabukCikisKodu }
    Write-Host ("{0,-9} gecti={1} cikis={2} sure={3}sn exe={4} ffmpeg={5} libmpv={6} ayakta={7} pencere={8}" -f $ad, $y.gecti, $cikis, $y.saniye,
        $y.kurulum.'VidShrink.exe', $y.kurulum.ffmpegKodlama, $y.kurulum.libmpvYuklendi, $y.acilis.ayakta, $y.acilis.pencere)
}
Write-Host "Ayrıntı: $bitti"

if (-not $AcikBirak) { Get-Process -Name $surecAdlari -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }
if ($ozet.gecti) { exit 0 } else { exit 1 }
