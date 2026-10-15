param([string]$dir = (Join-Path (Get-Location) ".calisma\bas-konus-olcumu"))
New-Item -ItemType Directory -Force $dir | Out-Null
$out = Join-Path $dir "ptt2.wav"
Remove-Item $out -ErrorAction SilentlyContinue
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "ffmpeg"
$psi.Arguments = "-hide_banner -y -progress pipe:1 -nostats -stats_period 0.1 -threads 2 -re -f lavfi -i sine=frequency=440:sample_rate=48000:duration=6 -filter_complex `"[0:a]volume@ptt=0[aout]`" -map `"[aout]`" `"$out`""
$psi.RedirectStandardInput = $true
$psi.RedirectStandardError = $true
$psi.RedirectStandardOutput = $true
$psi.UseShellExecute = $false
$p = [System.Diagnostics.Process]::Start($psi)
$err = $p.StandardError.ReadToEndAsync()
$script:last = ""
$sw = [System.Diagnostics.Stopwatch]::StartNew()
function Pump($ms) {
  $until = $sw.ElapsedMilliseconds + $ms
  while ($sw.ElapsedMilliseconds -lt $until) {
    $t = $p.StandardOutput.ReadLineAsync()
    if (-not $t.Wait(200)) { continue }
    if ($t.Result -like "out_time_us=*") { $script:last = $t.Result }
  }
}
Pump 2000
"open at $script:last"
$p.StandardInput.Write("cvolume@ptt -1 volume 1`n"); $p.StandardInput.Flush()
Pump 2000
"close at $script:last"
$p.StandardInput.Write("cvolume@ptt -1 volume 0`n"); $p.StandardInput.Flush()
Pump 2500
$p.WaitForExit()
& ffmpeg -hide_banner -i $out -af silencedetect=n=-60dB:d=0.01 -f null - 2>&1 | Select-String "silence_(start|end)" | ForEach-Object { $_.Line }
