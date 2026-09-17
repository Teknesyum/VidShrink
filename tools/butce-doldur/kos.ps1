param(
    [Parameter(Mandatory)][string]$Calisma,
    [Parameter(Mandatory)][string]$BenchOnce,
    [Parameter(Mandatory)][string]$BenchSonra,
    [string]$Gruplar = 'hareketli:av1_nvenc:1000;hareketli:h264_nvenc:1000,2000,3500;hareketli:hevc_nvenc:1000,2000,3500;karanlik:av1_nvenc:1000,2000,3500;karanlik:h264_nvenc:1000,3500;karanlik:hevc_nvenc:2000,3500;orta:av1_nvenc:600,1000;orta:h264_nvenc:2000,3500;orta:hevc_nvenc:1000,2000,3500;parlak:av1_nvenc:1000;parlak:h264_nvenc:1000,3500;parlak:hevc_nvenc:1000,2000;karanlik:libx264:1000;parlak:libx264:1000;hareketli:libx264:2000;karanlik:libx265:1000;parlak:libx265:1000;hareketli:libx265:2000',
    [string]$Kollar = 'once,sonra',
    [string]$Json = 'butce.json',
    [string]$EkArg = ''
)
$ErrorActionPreference = 'Stop'
$Inv = [Globalization.CultureInfo]::InvariantCulture
$D = (Resolve-Path $Calisma).Path
$env:VIDSHRINK_SETTINGS_PATH = Join-Path $D 'settings.json'
$yol = Join-Path $D $Json
$Satir = [Collections.Generic.List[object]]::new()
if (Test-Path $yol) { foreach ($x in @(Get-Content $yol -Raw | ConvertFrom-Json)) { $Satir.Add($x) } }
$surePath = Join-Path $D 'kodlama-sn.txt'
if (-not (Test-Path $surePath)) { Set-Content $surePath '0' }
$script:Top = [double]::Parse((Get-Content $surePath -Raw).Trim(), $Inv)

function Sure([double]$sn) { $script:Top += $sn; Set-Content $surePath $script:Top.ToString($Inv) }
function Bosta {
    while (Get-Process -Name ffmpeg, HandBrakeCLI -ErrorAction SilentlyContinue) { Start-Sleep -Seconds 5 }
}
function Olc([string]$Bench, [string]$Ref, [string]$Test) {
    $o = Join-Path $D ([IO.Path]::GetFileNameWithoutExtension($Test) + '.olcu.json')
    Bosta
    & dotnet $Bench measure-pair $Ref $Test --fps 24/1 --out $o *>&1 | Out-Null
    $m = Get-Content $o -Raw | ConvertFrom-Json
    Remove-Item $o
    [ordered]@{ bayt = $m.Bayt; vmafneg_ort = [math]::Round($m.VmafNegMean, 2); vmafneg_p10 = [math]::Round($m.VmafNegP10, 2) }
}

foreach ($g in $Gruplar.Split(';')) {
    $k, $c, $kb = $g.Split(':')
    $bitler = @($kb.Split(',') | ForEach-Object { [int]$_ })
    $girdi = Join-Path $D "kesit-$k.mkv"
    foreach ($kol in $Kollar.Split(',')) {
        $eksik = @($bitler | Where-Object { $b = $_; -not ($Satir | Where-Object { $_.kesit -eq $k -and $_.kodek -eq $c -and $_.kbit -eq $b -and $_.kol -eq $kol }) })
        if ($eksik.Count -eq 0) { continue }
        $bench = if ($kol -eq 'once') { $BenchOnce } else { $BenchSonra }
        $mbler = $eksik | ForEach-Object { ($_ * 10.0 / 8 / 1024).ToString('0.####', $Inv) }
        $kl = Join-Path $D "$kol-$k-$c"
        $ek = @(); if ($EkArg) { $ek = $EkArg.Split(' ') }
        Bosta
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $txt = & dotnet $bench shrink $girdi ($mbler -join ',') --out $kl --speed quality --no-measure --lock-codec $c @ek 2>&1
        $sw.Stop(); Sure $sw.Elapsed.TotalSeconds
        $txt | Out-File "$kl.log"
        $rs = @(Get-Content (Join-Path $kl 'results.json') -Raw | ConvertFrom-Json)
        $izler = @(); $iz = @()
        foreach ($s in $txt) { $t = "$s"; if ($t -like 'komut:*') { if ($iz.Count) { $izler += ,($iz -join ' | ') }; $iz = @() } elseif ($t -like '  deneme *') { $iz += $t.Trim() } }
        $izler += ,($iz -join ' | ')
        for ($j = 0; $j -lt $eksik.Count; $j++) {
            $r = $rs[$j]
            $mp4 = Join-Path $kl ("kesit-${k}_" + $r.TargetMb.ToString('0.#', $Inv) + 'mb.mp4')
            $o = Olc $bench $girdi $mp4
            $Satir.Add([pscustomobject][ordered]@{
                kesit = $k; kodek = $c; kbit = $eksik[$j]; kol = $kol; hedef_mb = $r.TargetMb; cikan_mb = $r.ActualMb
                bayt_sapma_yuzde = [math]::Round(($r.ActualMb / $r.TargetMb - 1) * 100, 2); deneme = $r.Attempts
                geometri = "$($r.Width)x$($r.Height)"; vmafneg_ort = $o.vmafneg_ort; vmafneg_p10 = $o.vmafneg_p10; iz = $izler[$j]
            })
            ConvertTo-Json -Depth 4 -InputObject @($Satir) | Set-Content $yol
            Remove-Item $mp4
            Write-Host "$kol $k $c $($eksik[$j]) sapma=$([math]::Round(($r.ActualMb / $r.TargetMb - 1) * 100, 2)) deneme=$($r.Attempts) vmaf=$($o.vmafneg_ort) toplam-sn=$([math]::Round($script:Top,1))"
        }
    }
}
