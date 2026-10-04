param(
    [Parameter(Mandatory = $true)][string]$KesitDizini,
    [Parameter(Mandatory = $true)][string]$Calisma,
    [int]$Tekrar = 21
)
$ErrorActionPreference = 'Stop'
$K = (Resolve-Path $KesitDizini).Path
New-Item -ItemType Directory -Force $Calisma | Out-Null
$D = (Resolve-Path $Calisma).Path

function Kos([string[]]$A) {
    & ffmpeg -y -hide_banner -v error -threads 2 @A
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg dustu: $LASTEXITCODE" }
}

$uc = 'parlak', 'hareketli', 'karanlik' | ForEach-Object { "file '" + (Join-Path $K "kesit-$_.mkv").Replace('\', '/') + "'" }
$ucListe = Join-Path $D 'uc.txt'
Set-Content $ucListe $uc -Encoding ascii

$parca = Join-Path $D 'sintel-1080p-h264-30sn.mp4'
Kos @('-f', 'concat', '-safe', '0', '-i', $ucListe, '-vf', 'pad=1920:1080:0:131:black', '-pix_fmt', 'yuv420p', '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '16', '-g', '240', '-keyint_min', '240', '-sc_threshold', '0', '-threads', '2', $parca)

$h264Liste = Join-Path $D 'h264.txt'
Set-Content $h264Liste (1..$Tekrar | ForEach-Object { "file 'sintel-1080p-h264-30sn.mp4'" }) -Encoding ascii
$h264 = Join-Path $D 'sintel-1080p-h264-10dk.mp4'
Kos @('-f', 'concat', '-safe', '0', '-i', $h264Liste, '-c', 'copy', $h264)

$ffv1Liste = Join-Path $D 'ffv1.txt'
Set-Content $ffv1Liste (1..$Tekrar | ForEach-Object { $uc }) -Encoding ascii
$ffv1 = Join-Path $D 'sintel-818p-ffv1-10dk.mkv'
Kos @('-f', 'concat', '-safe', '0', '-i', $ffv1Liste, '-c', 'copy', $ffv1)

foreach ($yol in $parca, $h264, $ffv1) {
    $ozet = & ffprobe -v error -select_streams v:0 -show_entries 'stream=codec_name,width,height,r_frame_rate:format=duration,size,bit_rate' -of compact $yol
    Write-Output ("{0} {1}" -f (Split-Path $yol -Leaf), ($ozet -join ' '))
}
