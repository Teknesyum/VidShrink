[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$Klip,
    [Parameter(Mandatory = $true)][string]$Cikti,
    [ValidateSet('sicak', 'soguk')][string]$Kip = 'sicak',
    [ValidateRange(1, 200)][int]$Tekrar = 12,
    [ValidateRange(1, 600)][int]$ZamanAsimiSn = 60,
    [string]$Libmpv = '',
    [string]$Etiket = ''
)

$ErrorActionPreference = 'Stop'

function Yuzdelik([double[]]$Degerler, [double]$Oran) {
    if ($Degerler.Count -eq 0) { return [double]::NaN }
    $sirali = $Degerler | Sort-Object
    $sira = [Math]::Ceiling($Oran * $sirali.Count)
    if ($sira -lt 1) { $sira = 1 }
    return [double]$sirali[$sira - 1]
}

function Ortanca([double[]]$Degerler) {
    if ($Degerler.Count -eq 0) { return [double]::NaN }
    $sirali = $Degerler | Sort-Object
    $orta = [int][Math]::Floor($sirali.Count / 2)
    if ($sirali.Count % 2 -eq 1) { return [double]$sirali[$orta] }
    return ([double]$sirali[$orta - 1] + [double]$sirali[$orta]) / 2
}

if (-not (Test-Path -LiteralPath $Exe)) { throw "Uygulama bulunamadi: $Exe" }
if (-not (Test-Path -LiteralPath $Klip)) { throw "Klip bulunamadi: $Klip" }

$Exe = (Resolve-Path -LiteralPath $Exe).Path
$Klip = (Resolve-Path -LiteralPath $Klip).Path
$yayinKok = Split-Path -Parent $Exe
$exeAdi = Split-Path -Leaf $Exe

New-Item -ItemType Directory -Force -Path $Cikti | Out-Null
$Cikti = (Resolve-Path -LiteralPath $Cikti).Path
if ($Libmpv -ne '') { $Libmpv = (Resolve-Path -LiteralPath $Libmpv).Path }

$soguklukKok = Join-Path $Cikti ('soguk-' + [Guid]::NewGuid().ToString('n').Substring(0, 8))

$kosumlar = New-Object System.Collections.ArrayList
$adimlar = @('main', 'tek-ornek', 'cerceve', 'gecici-temizlik', 'ayar-okundu', 'palet',
             'pencere-yapici', 'xaml', 'yapici-bitti', 'pencere-kuruldu',
             'pencere-yuklendi', 'ayarlar', 'varsayilan-oneri', 'giris-canlandirmasi',
             'sekme', 'kare-kaynagi', 'ilk-kare', 'motor-acildi', 'kucultme-yuklendi')

for ($i = 1; $i -le $Tekrar; $i++) {
    $kanal = 'olcum-' + [Guid]::NewGuid().ToString('n')
    $iz = Join-Path $Cikti ("iz-$Kip-$i.txt")
    if (Test-Path -LiteralPath $iz) { Remove-Item -LiteralPath $iz -Force }

    $kosanExe = $Exe
    if ($Kip -eq 'soguk') {
        # Soguk kosum: yayin klasoru her tekrar icin yeni bir yola kopyalanir. Dosya
        # yollari yeni oldugu icin .NET derleme onbellegi ve imaj esleme sifirdan kurulur.
        # Isletim sisteminin sayfa onbellegi kopyalama sirasinda isinir; bu olcunun
        # "soguk"lugu disk degil, surec/yol sogukluguydur.
        $hedef = Join-Path $soguklukKok "k$i"
        Copy-Item -LiteralPath $yayinKok -Destination $hedef -Recurse -Force
        $kosanExe = Join-Path $hedef $exeAdi
    }

    $env:VIDSHRINK_ACILIS_IZI = $iz
    $env:VIDSHRINK_INSTANCE_CHANNEL = $kanal
    if ($Libmpv -ne '') { $env:VIDSHRINK_LIBMPV = $Libmpv }

    $surec = Start-Process -FilePath $kosanExe -ArgumentList "`"$Klip`"" -PassThru
    $bekleme = [Diagnostics.Stopwatch]::StartNew()
    $geldi = $false
    while ($bekleme.Elapsed.TotalSeconds -lt $ZamanAsimiSn) {
        if (Test-Path -LiteralPath $iz) {
            $metin = Get-Content -LiteralPath $iz -Raw -ErrorAction SilentlyContinue
            if ($metin -and $metin -match '(?m)^ilk-kare\t') { $geldi = $true; break }
        }
        if ($surec.HasExited) { break }
        Start-Sleep -Milliseconds 20
    }

    # Kucultme tarafi da olculsun diye ilk kareden sonra kisa bir kuyruk birakilir.
    if ($geldi) { Start-Sleep -Milliseconds 1500 }

    try { Stop-Process -Id $surec.Id -Force -ErrorAction Stop } catch {}
    try { $surec.WaitForExit(10000) | Out-Null } catch {}
    if (-not $surec.HasExited) { throw "Surec kapanmadi: pid $($surec.Id)" }

    $satirlar = @{}
    if (Test-Path -LiteralPath $iz) {
        foreach ($satir in Get-Content -LiteralPath $iz) {
            $parca = $satir -split "`t"
            if ($parca.Count -ne 2) { continue }
            if ($satirlar.ContainsKey($parca[0])) { continue }
            $satirlar[$parca[0]] = [double]::Parse($parca[1], [Globalization.CultureInfo]::InvariantCulture)
        }
    }

    $kayit = [ordered]@{ tekrar = $i; kip = $Kip; geldi = $geldi }
    foreach ($adim in $adimlar) {
        $kayit[$adim] = if ($satirlar.ContainsKey($adim)) { $satirlar[$adim] } else { $null }
    }
    [void]$kosumlar.Add([pscustomobject]$kayit)
    Write-Host ("{0,3}. {1}  ilk-kare={2} ms" -f $i, $Kip, $kayit['ilk-kare'])
}

if ($Kip -eq 'soguk' -and (Test-Path -LiteralPath $soguklukKok)) {
    Remove-Item -LiteralPath $soguklukKok -Recurse -Force -ErrorAction SilentlyContinue
}

$ozet = New-Object System.Collections.ArrayList
foreach ($adim in $adimlar) {
    $degerler = @($kosumlar | Where-Object { $_.geldi -and $null -ne $_.$adim } | ForEach-Object { [double]$_.$adim })
    if ($degerler.Count -eq 0) { continue }
    [void]$ozet.Add([pscustomobject]@{
        adim    = $adim
        n       = $degerler.Count
        en_az   = [Math]::Round(($degerler | Measure-Object -Minimum).Minimum, 1)
        ortanca = [Math]::Round((Ortanca $degerler), 1)
        p95     = [Math]::Round((Yuzdelik $degerler 0.95), 1)
        en_cok  = [Math]::Round(($degerler | Measure-Object -Maximum).Maximum, 1)
    })
}

$ad = if ($Etiket -ne '') { $Etiket } else { $Kip }
$hamYol = Join-Path $Cikti "ham-$ad.csv"
$ozetYol = Join-Path $Cikti "ozet-$ad.txt"

$kosumlar | Export-Csv -LiteralPath $hamYol -NoTypeInformation -Encoding UTF8

$basliklar = @(
    "etiket   : $ad",
    "kip      : $Kip",
    "exe      : $Exe",
    "klip     : $Klip ($([Math]::Round((Get-Item -LiteralPath $Klip).Length / 1MB, 1)) MB)",
    "tekrar   : $Tekrar (ilk kare gelen: $(($kosumlar | Where-Object { $_.geldi }).Count))",
    "makine   : $env:COMPUTERNAME / $([Environment]::OSVersion.VersionString)",
    "an       : $((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))",
    "birim    : ms, surec yaratilmasindan (Process.StartTime) itibaren",
    "p95      : en yakin sira yontemi, ceil(0.95*n)",
    ""
)
$basliklar + ($ozet | Format-Table -AutoSize | Out-String) | Set-Content -LiteralPath $ozetYol -Encoding UTF8

Write-Host ""
Get-Content -LiteralPath $ozetYol
Write-Host "ham: $hamYol"
Write-Host "ozet: $ozetYol"
