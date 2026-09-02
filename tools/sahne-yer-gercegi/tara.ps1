param([string]$Base = "0.01")

$ErrorActionPreference = "Stop"
$dir = "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\agent-a5ce055537d0cccfe\.calisma\T101"
$src = "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\kaynak-1080p60-hdr-17dk.mp4"
$vstats = Join-Path $dir "vstats-tam.log"
$log = Join-Path $dir "scan-tam-b001.log"

$graph = "[0:v]split=2[a][b];[a]select='gte(scene,$Base)',metadata=print[sc];[b]scale=640:-2[enc]"
$args = @(
  "-hide_banner", "-loglevel", "info", "-nostats",
  "-i", $src,
  "-filter_complex", $graph,
  "-map", "[sc]", "-f", "null", "-",
  "-map", "[enc]", "-an",
  "-c:v", "libx264", "-preset", "ultrafast", "-crf", "23",
  "-vstats_file", $vstats,
  "-f", "null", "-"
)

$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath "ffmpeg" -ArgumentList $args -NoNewWindow -Wait -PassThru -RedirectStandardError $log -RedirectStandardOutput (Join-Path $dir "scan-stdout.log")
$sw.Stop()
"exit=$($p.ExitCode) sure_sn=$([math]::Round($sw.Elapsed.TotalSeconds,1))"
