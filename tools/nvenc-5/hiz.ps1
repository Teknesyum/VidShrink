#requires -Version 7
param(
    [string]$KesitDizini = '.calisma/nvenc-2',
    [string]$Calisma = '.calisma/nvenc-5',
    [int]$Tekrar = 3
)
$ErrorActionPreference = 'Stop'
[Diagnostics.Process]::GetCurrentProcess().ProcessorAffinity = [IntPtr]0xF
$D = (Resolve-Path $Calisma).Path
$urun = '-b:v 2000k -maxrate 4000k -bufsize 4000k -rc vbr -multipass fullres -g 240 -pix_fmt yuv420p -rc-lookahead 20 -lookahead_level 3'.Split(' ')
$kollar = [ordered]@{ taban = @(); uhq = @('-tune', 'uhq'); p7 = @(); uhqp7 = @('-tune', 'uhq') }
$Satir = [Collections.Generic.List[object]]::new()
foreach ($k in 'karanlik', 'parlak', 'hareketli') {
    $y4m = Join-Path $D "$k.y4m"
    & ffmpeg -hide_banner -nostdin -loglevel error -threads 4 -y -i (Join-Path $KesitDizini "kesit-$k.mkv") -an $y4m
    foreach ($c in 'hevc_nvenc', 'av1_nvenc') {
        $p = if ($c -eq 'av1_nvenc') { 'p6' } else { 'p4' }
        foreach ($kol in $kollar.Keys) {
            $pr = if ($kol -in 'p7', 'uhqp7') { 'p7' } else { $p }
            $sn = for ($i = 0; $i -lt $Tekrar; $i++) {
                $w = [Diagnostics.Stopwatch]::StartNew()
                & ffmpeg -hide_banner -nostdin -loglevel error -threads 4 -y -i $y4m -c:v $c -preset $pr @urun @($kollar[$kol]) -f null NUL
                if ($LASTEXITCODE -ne 0) { throw "$kol $k $c kodlanmadi" }
                $w.Elapsed.TotalSeconds
            }
            $ort = ($sn | Sort-Object)[[int][math]::Floor($Tekrar / 2)]
            $Satir.Add([pscustomobject][ordered]@{ kesit = $k; kodek = $c; kol = $kol; preset = $pr; sn_ortanca = [math]::Round($ort, 3); fps = [math]::Round(240 / $ort, 1) })
            Write-Host "$k $c $kol $pr $([math]::Round($ort, 3)) sn"
        }
    }
    Remove-Item $y4m
}
ConvertTo-Json -InputObject @($Satir) | Set-Content (Join-Path $D 'hiz.json')
