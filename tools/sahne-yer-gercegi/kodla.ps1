$ErrorActionPreference = "Stop"
$dir = "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\agent-a5ce055537d0cccfe\.calisma\T101"
$src = "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\kaynak-1080p60-hdr-17dk.mp4"
$out = Join-Path $dir "kodlama"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$sahneler = @(
  @{ i = 17; s = 522.416; e = 534.632 },
  @{ i = 10; s = 215.951; e = 255.750 },
  @{ i = 12; s = 327.683; e = 333.300 },
  @{ i =  3; s = 144.117; e = 158.966 },
  @{ i = 21; s = 1004.048; e = 1011.549 },
  @{ i = 23; s = 1013.632; e = 1036.166 },
  @{ i = 16; s = 519.666; e = 522.416 },
  @{ i = 14; s = 477.933; e = 506.450 }
)

$kodlayicilar = @(
  @{ ad = "libx264";   crf = "23"; presetBayrak = "-preset"; preset = "veryfast" },
  @{ ad = "libx265";   crf = "28"; presetBayrak = "-preset"; preset = "veryfast" },
  @{ ad = "libsvtav1"; crf = "35"; presetBayrak = "-preset"; preset = "8" }
)

$csv = Join-Path $dir "kodlayici-olcum.csv"
"kodlayici;sahne;sure;bit;bit_sn;kodlama_sn" | Set-Content -Path $csv -Encoding utf8

foreach ($k in $kodlayicilar) {
  foreach ($sh in $sahneler) {
    $sure = $sh.e - $sh.s
    $hedef = Join-Path $out ("s{0:d2}-{1}.mkv" -f $sh.i, $k.ad)
    $errLog = Join-Path $out ("s{0:d2}-{1}.log" -f $sh.i, $k.ad)
    $a = @(
      "-hide_banner", "-loglevel", "error", "-nostats", "-y",
      "-ss", $sh.s.ToString("0.######", [Globalization.CultureInfo]::InvariantCulture),
      "-i", $src,
      "-t", $sure.ToString("0.######", [Globalization.CultureInfo]::InvariantCulture),
      "-an", "-sn", "-map", "0:v:0",
      "-pix_fmt", "yuv420p",
      "-c:v", $k.ad, $k.presetBayrak, $k.preset, "-crf", $k.crf,
      $hedef
    )
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath "ffmpeg" -ArgumentList $a -NoNewWindow -Wait -PassThru -RedirectStandardError $errLog
    $sw.Stop()
    if ($p.ExitCode -ne 0) { "HATA $($k.ad) s$($sh.i): $(Get-Content $errLog -Raw)"; continue }

    $pk = & ffprobe -v error -select_streams v:0 -show_entries packet=size -of csv=p=0 $hedef
    $bayt = 0L
    foreach ($line in $pk) { if ($line -match '^\d+') { $bayt += [int64]$Matches[0] } }
    $gercekSure = [double](& ffprobe -v error -select_streams v:0 -show_entries format=duration -of csv=p=0 $hedef)
    $bit = $bayt * 8
    $bps = $bit / $gercekSure
    $satir = "{0};{1};{2};{3};{4};{5}" -f $k.ad, $sh.i,
      $gercekSure.ToString("0.###", [Globalization.CultureInfo]::InvariantCulture),
      $bit,
      $bps.ToString("0", [Globalization.CultureInfo]::InvariantCulture),
      $sw.Elapsed.TotalSeconds.ToString("0.#", [Globalization.CultureInfo]::InvariantCulture)
    Add-Content -Path $csv -Value $satir -Encoding utf8
    $satir
    Remove-Item $hedef -Force
    Remove-Item $errLog -Force
  }
}
"BITTI"
