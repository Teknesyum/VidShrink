$ErrorActionPreference = "Stop"
$p = "C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\agent-a5ce055537d0cccfe\.calisma\T101\klip"
Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $p | Out-Null
$klip = Join-Path $p "kademeli.mp4"

$graf = "[1:v][2:v]concat=n=2:v=1:a=0[sag];[0:v][sag]hstack=inputs=2[parca];" +
        "[4:v][5:v]hstack=inputs=2[serit];[parca][3:v][serit]concat=n=3:v=1:a=0"
$a = @("-hide_banner","-loglevel","error","-nostats","-y",
  "-f","lavfi","-i","testsrc2=duration=4:size=854x720:rate=30",
  "-f","lavfi","-i","smptehdbars=duration=2:size=426x720:rate=30",
  "-f","lavfi","-i","color=c=navy:duration=2:size=426x720:rate=30",
  "-f","lavfi","-i","smptehdbars=duration=2:size=1280x720:rate=30",
  "-f","lavfi","-i","smptehdbars=duration=2:size=1240x720:rate=30",
  "-f","lavfi","-i","color=c=navy:duration=2:size=40x720:rate=30",
  "-filter_complex",$graf,
  "-c:v","libx264","-preset","ultrafast","-crf","18","-pix_fmt","yuv420p",$klip)
$r = Start-Process -FilePath "ffmpeg" -ArgumentList $a -NoNewWindow -Wait -PassThru -RedirectStandardError (Join-Path $p "yap.log")
if ($r.ExitCode -ne 0) { Get-Content (Join-Path $p "yap.log") -Raw; exit 1 }

$log = Join-Path $p "skor.log"
$b = @("-hide_banner","-loglevel","info","-nostats","-i",$klip,"-vf","select='gte(scene,0)',metadata=print","-f","null","-")
Start-Process -FilePath "ffmpeg" -ArgumentList $b -NoNewWindow -Wait -RedirectStandardError $log | Out-Null
$skorlar = Select-String -Path $log -Pattern "lavfi.scene_score=([0-9.]+)" -AllMatches |
  ForEach-Object { $_.Matches } | ForEach-Object { [double]$_.Groups[1].Value }
"toplam={0}" -f $skorlar.Count
foreach ($e in @(0.01,0.02,0.05,0.10,0.15,0.20,0.35,0.50)) {
  "  >={0}: {1}" -f $e, ($skorlar | Where-Object { $_ -ge $e }).Count
}
"en yuksek 6: " + (($skorlar | Sort-Object -Descending | Select-Object -First 6) -join ", ")



