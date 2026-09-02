$ErrorActionPreference = "Stop"
$dir = "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\agent-a5ce055537d0cccfe\.calisma\T101"
$src = "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\kaynak-1080p60-hdr-17dk.mp4"
$p = Join-Path $dir "av1kontrol"
New-Item -ItemType Directory -Force -Path $p | Out-Null

foreach ($crf in @(20, 35, 50)) {
  $h = Join-Path $p "s16-crf$crf.mkv"
  $a = @(
    "-hide_banner", "-loglevel", "error", "-nostats", "-y",
    "-ss", "519.666", "-i", $src, "-t", "2.75",
    "-an", "-sn", "-map", "0:v:0", "-pix_fmt", "yuv420p",
    "-c:v", "libsvtav1", "-preset", "8", "-crf", "$crf",
    $h
  )
  $r = Start-Process -FilePath "ffmpeg" -ArgumentList $a -NoNewWindow -Wait -PassThru -RedirectStandardError (Join-Path $p "crf$crf.log")
  "crf=$crf exit=$($r.ExitCode) bayt=$((Get-Item $h).Length)"
}
Get-Content (Join-Path $p "crf35.log") -Raw
