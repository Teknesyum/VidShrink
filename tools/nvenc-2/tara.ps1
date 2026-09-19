#requires -Version 7
param(
    [string]$Calisma = '.calisma/nvenc2', [string]$Json = 'tara.json', [string]$Hucreler = '', [string]$KolTanim = '',
    [double]$Tolerans = 0.015, [int]$EnCokDeneme = 3)
$ErrorActionPreference = 'Stop'
$Inv = [Globalization.CultureInfo]::InvariantCulture
$D = (Resolve-Path $Calisma).Path
$Bench = (Resolve-Path (Join-Path $PSScriptRoot '..\VidShrink.Bench\bin\Release\net8.0\VidShrink.Bench.dll')).Path
$yol = Join-Path $D $Json
$Satir = [Collections.Generic.List[object]]::new()
if (Test-Path $yol) { foreach ($x in @(Get-Content $yol -Raw | ConvertFrom-Json)) { $Satir.Add($x) } }
$surePath = Join-Path $D 'kodlama-sn.txt'
$top = if (Test-Path $surePath) { [double]::Parse((Get-Content $surePath -Raw).Trim(), $Inv) } else { 0.0 }

function Genislet([string]$ek, [int]$b) {
    $x = @()
    foreach ($e in @($ek.Split(' ', [StringSplitOptions]::RemoveEmptyEntries))) {
        if ($e -like 'PEAK=*') { $x += @('-maxrate', "$([int]($b * [double]::Parse($e.Substring(5), $Inv)))k") }
        elseif ($e -like 'BUF=*') { $x += @('-bufsize', "$([int]($b * [double]::Parse($e.Substring(4), $Inv)))k") }
        else { $x += $e }
    }
    $x
}

foreach ($h in $Hucreler.Split(',')) {
    $p = $h.Split(':')
    $k = $p[0]; $c = $p[1]; $w = $p[2]; $hh = $p[3]; $hedef = [double]$p[4]; $pr = $p[5]
    $girdi = Join-Path $D "kesit-$k.mkv"
    foreach ($kd in $KolTanim.Split(';')) {
        $ad, $ek = $kd.Split('=', 2)
        if ($Satir | Where-Object { $_.kesit -eq $k -and $_.kodek -eq $c -and $_.kol -eq $ad -and $_.geometri -eq "${w}x${hh}" -and $_.hedef_kbps -eq $hedef }) { continue }
        $cik = Join-Path $D "t-$k-$c-$ad.mp4"
        $b = [int]$hedef; $iz = @()
        for ($i = 1; $i -le $EnCokDeneme; $i++) {
            $vf = if ($w -eq '1920' -and $hh -eq '818') { @() } else { @('-vf', "scale=${w}:${hh}:flags=lanczos") }
            $a = @('-hide_banner', '-nostdin', '-loglevel', 'error', '-y', '-i', $girdi) + $vf + @('-c:v', $c, '-preset', $pr, '-b:v', "${b}k") + (Genislet $ek $b) + @('-pix_fmt', 'yuv420p', '-an', '-movflags', '+faststart', $cik)
            $sw = [Diagnostics.Stopwatch]::StartNew()
            & ffmpeg @a
            $sw.Stop(); $top += $sw.Elapsed.TotalSeconds
            Set-Content $surePath $top.ToString($Inv)
            if ($LASTEXITCODE -ne 0) { throw "ffmpeg basarisiz: $k $c $ad" }
            $kbps = (Get-Item $cik).Length * 8 / 1000 / 10.0
            $iz += "${b}k->$([math]::Round($kbps,1))"
            if ([math]::Abs($kbps / $hedef - 1) -le $Tolerans -or $i -eq $EnCokDeneme) { break }
            $b = [int][math]::Round($b * $hedef / $kbps)
        }
        $o = Join-Path $D "t-$k-$c-$ad.olcu.json"
        & dotnet $Bench measure-pair $girdi $cik --fps 24/1 --out $o *>&1 | Out-Null
        $m = Get-Content $o -Raw | ConvertFrom-Json
        $Satir.Add([pscustomobject][ordered]@{ kesit = $k; kodek = $c; kol = $ad; geometri = "${w}x${hh}"; hedef_kbps = $hedef; preset = $pr; ek = $ek; iz = ($iz -join ' | '); kbps = [math]::Round($m.Kbps, 1); sapma_yuzde = [math]::Round(($m.Kbps / $hedef - 1) * 100, 2); vmafneg_ort = [math]::Round($m.VmafNegMean, 2); vmafneg_p10 = [math]::Round($m.VmafNegP10, 2); xpsnr = [math]::Round($m.Xpsnr, 2) })
        ConvertTo-Json -InputObject @($Satir) | Set-Content $yol
        Remove-Item $cik, $o
        Write-Host "$k $c ${w}x$hh $hedef $ad -> $([math]::Round($m.Kbps,1)) kbps ort $([math]::Round($m.VmafNegMean,2)) p10 $([math]::Round($m.VmafNegP10,2)) | kodlama top $([math]::Round($top,1)) sn"
    }
}
