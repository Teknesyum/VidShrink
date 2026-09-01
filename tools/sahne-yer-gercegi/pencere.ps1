$ErrorActionPreference = "Stop"
$dir = "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\agent-a5ce055537d0cccfe\.calisma\T101"
$src = "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\kaynak-1080p60-hdr-17dk.mp4"
$p = Join-Path $dir "pencere"
New-Item -ItemType Directory -Force -Path $p | Out-Null

$bas = "144.116667"
$sure = "189.183333"
$mkv = Join-Path $p "pencere-x264.mkv"

$a = @(
  "-hide_banner", "-loglevel", "error", "-nostats", "-y",
  "-ss", $bas, "-i", $src, "-t", $sure,
  "-an", "-sn", "-map", "0:v:0", "-pix_fmt", "yuv420p",
  "-c:v", "libx264", "-preset", "veryfast", "-crf", "23",
  "-g", "100000", "-keyint_min", "1",
  $mkv
)
$sw = [Diagnostics.Stopwatch]::StartNew()
$r = Start-Process -FilePath "ffmpeg" -ArgumentList $a -NoNewWindow -Wait -PassThru -RedirectStandardError (Join-Path $p "x264.log")
$sw.Stop()
"x264 scenecut kodlamasi exit=$($r.ExitCode) sure=$([math]::Round($sw.Elapsed.TotalSeconds,1))s"

$keys = & ffprobe -v error -select_streams v:0 -skip_frame nokey -show_entries frame=pts_time -of csv=p=0 $mkv
$keys | Set-Content -Path (Join-Path $p "x264-anahtar.txt") -Encoding utf8
"anahtar kare sayisi=$(($keys | Where-Object { $_ -match '\d' }).Count)"

$sheetDir = Join-Path $p "sayfa"
New-Item -ItemType Directory -Force -Path $sheetDir | Out-Null
$b = @(
  "-hide_banner", "-loglevel", "error", "-nostats", "-y",
  "-ss", $bas, "-i", $src, "-t", $sure,
  "-vf", "fps=1,scale=240:-2,drawtext=fontsize=18:fontcolor=yellow:box=1:boxcolor=black:x=2:y=2:text='%{eif\:trunc(t)+144\:d}',tile=8x4",
  "-an", "-sn",
  (Join-Path $sheetDir "sayfa-%02d.png")
)
$r2 = Start-Process -FilePath "ffmpeg" -ArgumentList $b -NoNewWindow -Wait -PassThru -RedirectStandardError (Join-Path $p "sayfa.log")
"kontakt sayfasi exit=$($r2.ExitCode)"
Get-ChildItem $sheetDir -Filter *.png | Select-Object Name, Length | Format-Table -AutoSize
"BITTI"
