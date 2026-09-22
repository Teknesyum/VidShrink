#requires -Version 7
param([string]$Calisma = '.calisma/nvenc-5')
$ErrorActionPreference = 'Stop'
$Kok = (Get-Location).Path
New-Item -ItemType Directory -Force $Calisma | Out-Null
$D = (Resolve-Path $Calisma).Path
$fa = 'src/VidShrink.Core/FfmpegArguments.cs'
$yamalar = @{
    taban = @()
    uhq = @(, @('args.AddRange(new[] { "-rc-lookahead", "20", "-lookahead_level", "3" });',
                'args.AddRange(new[] { "-rc-lookahead", "20", "-lookahead_level", "3", "-tune", "uhq" });'))
    p7 = @(@('"h264_nvenc" or "hevc_nvenc" => "p4",', '"h264_nvenc" or "hevc_nvenc" => "p7",'),
           @('"av1_nvenc" => "p6",', '"av1_nvenc" => "p7",'))
}
$yamalar['uhqp7'] = @($yamalar.uhq) + @($yamalar.p7)

foreach ($kol in 'taban', 'uhq', 'p7', 'uhqp7') {
    $kaynak = Join-Path $D "src-$kol"
    if (Test-Path $kaynak) { Remove-Item -Recurse -Force $kaynak }
    foreach ($p in 'src/VidShrink.Core', 'src/VidShrink.Ffmpeg', 'tools/VidShrink.Bench') {
        $hedef = Join-Path $kaynak $p
        New-Item -ItemType Directory -Force $hedef | Out-Null
        & robocopy (Join-Path $Kok $p) $hedef /E /XD bin obj /NFL /NDL /NJH /NJS /NP | Out-Null
    }
    Copy-Item (Join-Path $Kok 'Directory.Build.props') $kaynak
    $dosya = Join-Path $kaynak $fa
    $metin = Get-Content $dosya -Raw
    foreach ($y in $yamalar[$kol]) {
        $n = ([regex]::Matches($metin, [regex]::Escape($y[0]))).Count
        if ($n -ne 1) { throw "$kol yamasi $n kez eslesti: $($y[0])" }
        $metin = $metin.Replace($y[0], $y[1])
    }
    Set-Content $dosya $metin -NoNewline
    & dotnet build (Join-Path $kaynak 'tools/VidShrink.Bench/VidShrink.Bench.csproj') -c Release -o (Join-Path $D "bin-$kol") -v q -nologo
    if ($LASTEXITCODE -ne 0) { throw "$kol derlenmedi" }
    Write-Host "$kol derlendi"
}
