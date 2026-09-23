param(
    [Parameter(Mandatory)][string]$Girdi,
    [Parameter(Mandatory)][string]$Olcek,
    [Parameter(Mandatory)][string]$Kodek,
    [Parameter(Mandatory)][string]$Kbitler,
    [Parameter(Mandatory)][double]$HedefMb,
    [string]$Onayar = 'p4',
    [string]$Calisma = '.calisma/t0-d1-tavan',
    [string]$Cekirdek = '3'
)
$ErrorActionPreference = 'Stop'
$Inv = [Globalization.CultureInfo]::InvariantCulture
$kaynak = (Resolve-Path $Girdi).Path
$cikti = Join-Path (Resolve-Path $Calisma).Path 'egri.mp4'
foreach ($k in $Kbitler.Split(',') | ForEach-Object { [int]$_ }) {
    $m = 2 * $k
    $argv = "/c start `"`" /affinity $Cekirdek /wait /b ffmpeg -hide_banner -loglevel error -y -i `"$kaynak`" -vf scale=$($Olcek):flags=lanczos -c:v $Kodek -preset $Onayar -b:v $($k)k -maxrate $($m)k -bufsize $($m)k -rc vbr -multipass fullres -g 120 -pix_fmt yuv420p -rc-lookahead 20 -lookahead_level 3 -map 0:0 -an -movflags +faststart `"$cikti`""
    Start-Process cmd.exe -ArgumentList $argv -NoNewWindow -Wait | Out-Null
    $mb = (Get-Item $cikti).Length / 1MB
    Write-Output ("{0}`t{1}`t{2}" -f $k, $mb.ToString('0.0000', $Inv), ($mb / $HedefMb).ToString('0.0000', $Inv))
}
Remove-Item $cikti
