#requires -Version 7
param(
    [string]$Calisma = '.calisma/worktree-agent-a2cc7681a7aeb1609',
    [int]$Tekrar = 3
)
$ErrorActionPreference = 'Stop'
[Diagnostics.Process]::GetCurrentProcess().ProcessorAffinity = [IntPtr]0xF
$D = (Resolve-Path $Calisma).Path
$urun = '-b:v 2000k -maxrate 4000k -bufsize 4000k -rc vbr -multipass fullres -g 240 -pix_fmt yuv420p -rc-lookahead 20 -lookahead_level 3'.Split(' ')
$kollar = [ordered]@{ taban = 'p4'; p7 = 'p7' }
$Satir = [Collections.Generic.List[object]]::new()
foreach ($k in 'gren', 'gradyan', 'hayat') {
    $y4m = Join-Path $D "$k.y4m"
    & ffmpeg -hide_banner -nostdin -loglevel error -threads 4 -y -i (Join-Path $D "kesit-$k.mkv") -an $y4m
    foreach ($kol in $kollar.Keys) {
        $pr = $kollar[$kol]
        $sn = for ($i = 0; $i -lt $Tekrar; $i++) {
            $w = [Diagnostics.Stopwatch]::StartNew()
            & ffmpeg -hide_banner -nostdin -loglevel error -threads 4 -y -i $y4m -c:v hevc_nvenc -preset $pr @urun -f null NUL
            if ($LASTEXITCODE -ne 0) { throw "$kol $k kodlanmadi" }
            $w.Elapsed.TotalSeconds
        }
        $ort = ($sn | Sort-Object)[[int][math]::Floor($Tekrar / 2)]
        $Satir.Add([pscustomobject][ordered]@{ kesit = $k; kodek = 'hevc_nvenc'; kol = $kol; preset = $pr; sn_tekrarlar = @($sn | ForEach-Object { [math]::Round($_, 3) }); sn_ortanca = [math]::Round($ort, 3); fps = [math]::Round(240 / $ort, 1) })
        Write-Host "$k $kol $pr $([math]::Round($ort, 3)) sn"
    }
    Remove-Item $y4m
}
ConvertTo-Json -Depth 4 -InputObject @($Satir) | Set-Content (Join-Path $D 'hiz.json')
