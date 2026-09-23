param(
    [Parameter(Mandatory)][string]$Girdi,
    [Parameter(Mandatory)][string]$Olcek,
    [Parameter(Mandatory)][string]$Kodek,
    [Parameter(Mandatory)][int]$Kbit,
    [Parameter(Mandatory)][double]$Tepe,
    [Parameter(Mandatory)][double]$Tampon,
    [string]$Onayar = 'p4',
    [string]$Calisma = '.calisma/t0-d1-oran',
    [string]$Cekirdek = '3'
)
$ErrorActionPreference = 'Stop'
$Inv = [Globalization.CultureInfo]::InvariantCulture
$kaynak = (Resolve-Path $Girdi).Path
$D = (Resolve-Path $Calisma).Path
$cikti = Join-Path $D 'kalite.mp4'
$gunluk = Join-Path $D 'kalite.json'
$m = [int]($Tepe * $Kbit); $b = [int]($Tampon * $Kbit)
$vf = if ($Olcek -eq '-') { '' } else { "-vf scale=$($Olcek):flags=lanczos" }
$argv = "/c start `"`" /affinity $Cekirdek /wait /b ffmpeg -hide_banner -loglevel error -y -i `"$kaynak`" $vf -c:v $Kodek -preset $Onayar -b:v $($Kbit)k -maxrate $($m)k -bufsize $($b)k -rc vbr -multipass fullres -g 120 -pix_fmt yuv420p -rc-lookahead 20 -lookahead_level 3 -map 0:0 -an -movflags +faststart `"$cikti`""
Start-Process cmd.exe -ArgumentList $argv -NoNewWindow -Wait | Out-Null
$mb = (Get-Item $cikti).Length / 1MB
$g = $gunluk.Replace('\', '/').Replace(':', '\:')
$lavfi = "[0:v]scale=w=1920:h=818:flags=lanczos,format=yuv420p[d];[1:v]format=yuv420p[r];[d][r]libvmaf=model=version=vmaf_v0.6.1neg:n_threads=2:log_fmt=json:log_path='$g'"
$argv = "/c start `"`" /affinity $Cekirdek /wait /b ffmpeg -hide_banner -loglevel error -nostdin -i `"$cikti`" -i `"$kaynak`" -lavfi `"$lavfi`" -f null -"
Start-Process cmd.exe -ArgumentList $argv -NoNewWindow -Wait | Out-Null
$v = (Get-Content $gunluk -Raw | ConvertFrom-Json).pooled_metrics.vmaf.mean
Remove-Item $cikti, $gunluk
Write-Output ("{0}`t{1}`t{2}" -f $Kbit, $mb.ToString('0.0000', $Inv), $v.ToString('0.000', $Inv))
