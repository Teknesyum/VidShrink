$ErrorActionPreference = "Stop"
$dir = "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\agent-a5ce055537d0cccfe\.calisma\T101"
$src = "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\kaynak-1080p60-hdr-17dk.mp4"
$vstats = Join-Path $dir "vstats-atma.log"

$kosumlar = @(
  @{ ad = "yalniz-cozme-2"; a = @("-i", $src, "-an", "-f", "null", "-") },
  @{ ad = "sonda-kare-atlatmali"; a = @("-i", $src, "-an", "-vf", "select='not(mod(n\,2))',scale=640:-2", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "23", "-fps_mode", "passthrough", "-f", "null", "-") }
)

foreach ($k in $kosumlar) {
  $ffa = @("-hide_banner", "-loglevel", "error", "-nostats") + $k.a
  $log = Join-Path $dir ("maliyet-" + $k.ad + ".log")
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $p = Start-Process -FilePath "ffmpeg" -ArgumentList $ffa -NoNewWindow -Wait -PassThru -RedirectStandardError $log
  $sw.Stop()
  "{0};{1};{2}" -f $k.ad, $p.ExitCode, $sw.Elapsed.TotalSeconds.ToString("0.#", [Globalization.CultureInfo]::InvariantCulture)
}
"BITTI"
