#requires -Version 7
param([string]$Calisma = '.calisma/worktree-agent-a2cc7681a7aeb1609')
$ErrorActionPreference = 'Stop'
[Diagnostics.Process]::GetCurrentProcess().ProcessorAffinity = [IntPtr]0xF
New-Item -ItemType Directory -Force $Calisma | Out-Null
$D = (Resolve-Path $Calisma).Path
$kaynak = [ordered]@{
    gren    = 'testsrc2=s=1920x1080:r=24:d=10,noise=alls=20:allf=t+u:all_seed=6,format=yuv420p'
    gradyan = 'gradients=s=1920x1080:r=24:d=10:nb_colors=4:seed=6:speed=0.02,format=yuv420p'
    hayat   = 'life=s=480x270:r=24:ratio=0.3:mold=10:seed=6:life_color=#e0c070:death_color=#1a2b3c:mold_color=#6040a0,trim=duration=10,scale=1920:1080:flags=neighbor,format=yuv420p'
}
foreach ($k in $kaynak.Keys) {
    $out = Join-Path $D "kesit-$k.mkv"
    & ffmpeg -hide_banner -nostdin -loglevel error -threads 4 -y -f lavfi -i $kaynak[$k] -c:v ffv1 -level 3 -threads 4 $out
    if ($LASTEXITCODE -ne 0) { throw "$k uretilemedi" }
    $p = & ffprobe -v error -select_streams v:0 -count_frames -show_entries stream=width,height,r_frame_rate,pix_fmt,nb_read_frames -of csv=p=0 $out
    Write-Host "$k $p $((Get-Item $out).Length) bayt"
}
