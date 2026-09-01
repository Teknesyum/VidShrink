$ErrorActionPreference = "Stop"
$dir = "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\agent-a5ce055537d0cccfe\.calisma\T101"
$src = "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\kaynak\kaynak-1080p60-hdr-17dk.mp4"
$p = Join-Path $dir "cift"
Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $p | Out-Null

$adaylar = @(183.034, 190.051, 213.901, 226.700, 247.734, 255.551,
             258.634, 264.901, 269.216, 272.983, 279.217, 287.484,
             294.650, 298.267, 303.484, 306.300, 309.766, 315.483,
             317.551, 319.533, 321.866, 327.583, 327.701)

$n = 0
foreach ($t in $adaylar) {
  foreach ($off in @(-0.050, 0.010)) {
    $zaman = ($t + $off).ToString("0.######", [Globalization.CultureInfo]::InvariantCulture)
    $hedef = Join-Path $p ("k{0:d3}.png" -f $n)
    $a = @("-hide_banner", "-loglevel", "error", "-nostats", "-y",
           "-ss", $zaman, "-i", $src, "-frames:v", "1",
           "-vf", "scale=320:-2", $hedef)
    $r = Start-Process -FilePath "ffmpeg" -ArgumentList $a -NoNewWindow -Wait -PassThru -RedirectStandardError (Join-Path $p "err.log")
    if ($r.ExitCode -ne 0) { "HATA t=$zaman"; }
    $n++
  }
}
"kare=$n"

$b = @("-hide_banner", "-loglevel", "error", "-nostats", "-y",
       "-framerate", "1", "-i", (Join-Path $p "k%03d.png"),
       "-vf", "tile=2x6:margin=3:padding=3:color=red",
       (Join-Path $p "cift-%02d.png"))
$r2 = Start-Process -FilePath "ffmpeg" -ArgumentList $b -NoNewWindow -Wait -PassThru -RedirectStandardError (Join-Path $p "tile.log")
"tile exit=$($r2.ExitCode)"
Get-ChildItem $p -Filter "cift-*.png" | Select-Object Name, Length | Format-Table -AutoSize
