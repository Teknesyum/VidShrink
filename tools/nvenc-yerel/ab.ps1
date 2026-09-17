param(
    [string]$Calisma = '.calisma/nvenc', [string]$Json = 'ab.json', [string]$Hucreler = '', [string]$KolTanim = '')
$ErrorActionPreference = 'Stop'
$D = (Resolve-Path $Calisma).Path
$Bench = (Resolve-Path (Join-Path $PSScriptRoot '..\VidShrink.Bench\bin\Release\net8.0\VidShrink.Bench.dll')).Path
$Satir = [Collections.Generic.List[object]]::new()
$top = 0.0
foreach ($h in $Hucreler.Split(',')) {
    $p = $h.Split(':')
    $k = $p[0]; $c = $p[1]; $w = $p[2]; $hh = $p[3]; $b = [int]$p[4]; $pr = $p[5]
    $girdi = Join-Path $D "kesit-$k.mkv"
    $tepe = @('-maxrate', "$([int]($b * 1.1))k", '-bufsize', "$([int]($b * 1.2))k")
    $kl = [ordered]@{}
    foreach ($kd in $KolTanim.Split(';')) { $ad, $ek = $kd.Split('='); $kb = $b; $x = @(); foreach ($e in @($ek.Split(' ', [StringSplitOptions]::RemoveEmptyEntries))) { if ($e -eq 'TEPE') { $x += $tepe } elseif ($e -like 'BUF*') { $x += @('-maxrate', "$([int]($b * 1.1))k", '-bufsize', "$([int]($b * [double]$e.Substring(3)))k") } elseif ($e -like 'B*') { $kb = [int]$e.Substring(1) } else { $x += $e } }; $kl[$ad] = @{ B = $kb; X = $x } }
    foreach ($ad in $kl.Keys) {
        $cik = Join-Path $D "ab-$k-$c-$($ad.Replace('+','_')).mp4"
        $a = @('-hide_banner', '-nostdin', '-loglevel', 'error', '-y', '-i', $girdi, '-vf', "scale=${w}:${hh}:flags=lanczos", '-c:v', $c, '-preset', $pr, '-b:v', "$($kl[$ad].B)k") + $kl[$ad].X + @('-rc', 'vbr', '-pix_fmt', 'yuv420p', '-an', '-movflags', '+faststart', $cik)
        $sw = [Diagnostics.Stopwatch]::StartNew()
        & ffmpeg @a
        $sw.Stop(); $top += $sw.Elapsed.TotalSeconds
        $o = Join-Path $D "ab-$k-$c-$($ad.Replace('+','_')).olcu.json"
        & dotnet $Bench measure-pair $girdi $cik --fps 24/1 --out $o *>&1 | Out-Null
        $m = Get-Content $o -Raw | ConvertFrom-Json
        $Satir.Add([pscustomobject][ordered]@{ kesit = $k; kodek = $c; kol = $ad; geometri = "${w}x${hh}"; istenen_k = $kl[$ad].B; ek = ($kl[$ad].X -join ' '); kbps = [math]::Round($m.Kbps, 1); vmafneg_ort = [math]::Round($m.VmafNegMean, 2); vmafneg_harm = [math]::Round($m.VmafNegHarmonic, 2); vmafneg_p10 = [math]::Round($m.VmafNegP10, 2); xpsnr = [math]::Round($m.Xpsnr, 2); kodlama_sn = [math]::Round($sw.Elapsed.TotalSeconds, 1) })
        ConvertTo-Json -InputObject @($Satir) | Set-Content (Join-Path $D $Json)
        Remove-Item $cik
        Write-Host "$k $c $ad bitti"
    }
}
"toplam_kodlama_sn=$([math]::Round($top,1))" | Tee-Object -FilePath (Join-Path $D "$Json.sure.txt")




