[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$Klip,
    [Parameter(Mandatory = $true)][string]$Cikti,
    [ValidateSet('sicak', 'soguk')][string]$Kip = 'sicak',
    [ValidateRange(1, 200)][int]$Tekrar = 12,
    [ValidateRange(1, 600)][int]$ZamanAsimiSn = 60,
    [string]$Libmpv = '',
    [string]$Etiket = 'a',
    [string]$ExeB = '',
    [string]$EtiketB = 'b'
)

$ErrorActionPreference = 'Stop'

$Adimlar = @('main', 'tek-ornek', 'libmpv-hazir', 'cerceve', 'gecici-temizlik', 'ayar-okundu',
             'palet', 'pencere-yapici', 'xaml', 'yapici-bitti', 'pencere-kuruldu',
             'pencere-yuklendi', 'ayarlar', 'varsayilan-oneri', 'giris-canlandirmasi',
             'sekme', 'kare-kaynagi', 'ilk-kare', 'motor-acildi', 'kucultme-yuklendi')

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

function Kosum([string]$KosanExe, [string]$Iz, [string]$Etiketi, [int]$Sira, [string]$Klibi,
               [string]$Kipi, [int]$Sinir) {
    $kanal = 'olcum-' + [Guid]::NewGuid().ToString('n')
    if (Test-Path -LiteralPath $Iz) { Remove-Item -LiteralPath $Iz -Force }

    $env:VIDSHRINK_ACILIS_IZI = $Iz
    $env:VIDSHRINK_INSTANCE_CHANNEL = $kanal

    $surec = Start-Process -FilePath $KosanExe -ArgumentList "`"$Klibi`"" -PassThru
    $bekleme = [Diagnostics.Stopwatch]::StartNew()
    $geldi = $false
    while ($bekleme.Elapsed.TotalSeconds -lt $Sinir) {
        if (Test-Path -LiteralPath $Iz) {
            $metin = Get-Content -LiteralPath $Iz -Raw -ErrorAction SilentlyContinue
            if ($metin -and $metin -match '(?m)^ilk-kare\t') { $geldi = $true; break }
        }
        if ($surec.HasExited) { break }
        Start-Sleep -Milliseconds 20
    }

    if ($geldi) { Start-Sleep -Milliseconds 1500 }

    try { Stop-Process -Id $surec.Id -Force -ErrorAction Stop } catch {}
    try { $surec.WaitForExit(10000) | Out-Null } catch {}
    if (-not $surec.HasExited) { throw "Surec kapanmadi: pid $($surec.Id)" }

    $satirlar = @{}
    if (Test-Path -LiteralPath $Iz) {
        foreach ($satir in Get-Content -LiteralPath $Iz) {
            $parca = $satir -split "`t"
            if ($parca.Count -ne 2) { continue }
            if ($satirlar.ContainsKey($parca[0])) { continue }
            $satirlar[$parca[0]] = [double]::Parse($parca[1], [Globalization.CultureInfo]::InvariantCulture)
        }
    }

    $kayit = [ordered]@{ tekrar = $Sira; etiket = $Etiketi; kip = $Kipi; geldi = $geldi }
    foreach ($adim in $Adimlar) {
        $kayit[$adim] = if ($satirlar.ContainsKey($adim)) { $satirlar[$adim] } else { $null }
    }
    Write-Host ("{0,3}. {1,-26} ilk-kare={2} ms" -f $Sira, $Etiketi, $kayit['ilk-kare'])
    return [pscustomobject]$kayit
}

function Ozetle($Kosumlar, [string]$Etiketi) {
    $ozet = New-Object System.Collections.ArrayList
    $secili = @($Kosumlar | Where-Object { $_.etiket -eq $Etiketi -and $_.geldi })
    foreach ($adim in $Adimlar) {
        $degerler = @($secili | Where-Object { $null -ne $_.$adim } | ForEach-Object { [double]$_.$adim })
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
    return $ozet
}

if (-not (Test-Path -LiteralPath $Exe)) { throw "Uygulama bulunamadi: $Exe" }
if (-not (Test-Path -LiteralPath $Klip)) { throw "Klip bulunamadi: $Klip" }

$Exe = (Resolve-Path -LiteralPath $Exe).Path
$Klip = (Resolve-Path -LiteralPath $Klip).Path
if ($ExeB -ne '') {
    if (-not (Test-Path -LiteralPath $ExeB)) { throw "Ikinci uygulama bulunamadi: $ExeB" }
    $ExeB = (Resolve-Path -LiteralPath $ExeB).Path
}

New-Item -ItemType Directory -Force -Path $Cikti | Out-Null
$Cikti = (Resolve-Path -LiteralPath $Cikti).Path
if ($Libmpv -ne '') {
    $Libmpv = (Resolve-Path -LiteralPath $Libmpv).Path
    $env:VIDSHRINK_LIBMPV = $Libmpv
}

$soguklukKok = Join-Path $Cikti ('soguk-' + [Guid]::NewGuid().ToString('n').Substring(0, 8))
$kosumlar = New-Object System.Collections.ArrayList

for ($i = 1; $i -le $Tekrar; $i++) {
    $isler = New-Object System.Collections.ArrayList
    [void]$isler.Add(@{ exe = $Exe; etiket = $Etiket })
    if ($ExeB -ne '') { [void]$isler.Add(@{ exe = $ExeB; etiket = $EtiketB }) }
    if ($ExeB -ne '' -and $i % 2 -eq 0) { $isler.Reverse() }

    foreach ($is in $isler) {
        $kosanExe = $is.exe
        $hedef = $null
        if ($Kip -eq 'soguk') {
            $hedef = Join-Path $soguklukKok ("k$i-" + $is.etiket)
            Copy-Item -LiteralPath (Split-Path -Parent $kosanExe) -Destination $hedef -Recurse -Force
            $kosanExe = Join-Path $hedef (Split-Path -Leaf $is.exe)
        }

        $iz = Join-Path $Cikti ("iz-{0}-{1}-{2}.txt" -f $Kip, $is.etiket, $i)
        [void]$kosumlar.Add((Kosum $kosanExe $iz $is.etiket $i $Klip $Kip $ZamanAsimiSn))

        if ($hedef -ne $null) { Remove-Item -LiteralPath $hedef -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

if (Test-Path -LiteralPath $soguklukKok) {
    Remove-Item -LiteralPath $soguklukKok -Recurse -Force -ErrorAction SilentlyContinue
}

$ad = if ($ExeB -ne '') { "$Etiket-vs-$EtiketB-$Kip" } else { "$Etiket-$Kip" }
$hamYol = Join-Path $Cikti "ham-$ad.csv"
$ozetYol = Join-Path $Cikti "ozet-$ad.txt"
$kosumlar | Export-Csv -LiteralPath $hamYol -NoTypeInformation -Encoding UTF8

$govde = New-Object System.Collections.ArrayList
[void]$govde.Add("kip      : $Kip")
[void]$govde.Add("klip     : $Klip ($([Math]::Round((Get-Item -LiteralPath $Klip).Length / 1MB, 1)) MB)")
[void]$govde.Add("tekrar   : $Tekrar")
[void]$govde.Add("makine   : $env:COMPUTERNAME / $([Environment]::OSVersion.VersionString)")
[void]$govde.Add("an       : $((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))")
[void]$govde.Add("birim    : ms, surec yaratilmasindan (Process.StartTime) itibaren")
[void]$govde.Add("p95      : en yakin sira yontemi, ceil(0.95*n)")
if ($ExeB -ne '') { [void]$govde.Add("eslesik  : her tekrarda iki yapi, sira tekrardan tekrara donuyor") }
[void]$govde.Add("")

$etiketler = @($Etiket)
if ($ExeB -ne '') { $etiketler += $EtiketB }
foreach ($e in $etiketler) {
    $exeYolu = if ($e -eq $Etiket) { $Exe } else { $ExeB }
    $gelen = @($kosumlar | Where-Object { $_.etiket -eq $e -and $_.geldi }).Count
    [void]$govde.Add("== $e ==  exe: $exeYolu  (ilk kare gelen: $gelen)")
    [void]$govde.Add(((Ozetle $kosumlar $e) | Format-Table -AutoSize | Out-String).TrimEnd())
    [void]$govde.Add("")
}

if ($ExeB -ne '') {
    $farklar = New-Object System.Collections.ArrayList
    foreach ($adim in $Adimlar) {
        $ciftler = New-Object System.Collections.ArrayList
        for ($i = 1; $i -le $Tekrar; $i++) {
            $a = $kosumlar | Where-Object { $_.tekrar -eq $i -and $_.etiket -eq $Etiket -and $_.geldi }
            $b = $kosumlar | Where-Object { $_.tekrar -eq $i -and $_.etiket -eq $EtiketB -and $_.geldi }
            if ($null -eq $a -or $null -eq $b) { continue }
            if ($null -eq $a.$adim -or $null -eq $b.$adim) { continue }
            [void]$ciftler.Add([double]$b.$adim - [double]$a.$adim)
        }
        if ($ciftler.Count -eq 0) { continue }
        $dizi = [double[]]$ciftler.ToArray()
        [void]$farklar.Add([pscustomobject]@{
            adim          = $adim
            cift          = $dizi.Count
            fark_ortanca  = [Math]::Round((Ortanca $dizi), 1)
            fark_en_az    = [Math]::Round(($dizi | Measure-Object -Minimum).Minimum, 1)
            fark_en_cok   = [Math]::Round(($dizi | Measure-Object -Maximum).Maximum, 1)
            b_lehine_cift = @($dizi | Where-Object { $_ -lt 0 }).Count
        })
    }
    [void]$govde.Add("== eslesik fark ($EtiketB eksi $Etiket, eksi deger $EtiketB lehine) ==")
    [void]$govde.Add(($farklar | Format-Table -AutoSize | Out-String).TrimEnd())
}

$govde | Set-Content -LiteralPath $ozetYol -Encoding UTF8

Write-Host ""
Get-Content -LiteralPath $ozetYol
Write-Host "ham: $hamYol"
Write-Host "ozet: $ozetYol"
