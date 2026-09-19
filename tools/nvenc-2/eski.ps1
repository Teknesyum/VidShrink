#requires -Version 7
param([string]$Calisma = '.calisma/nvenc2', [string]$Json = 'eski.json')
$ErrorActionPreference = 'Stop'
$Inv = [Globalization.CultureInfo]::InvariantCulture
$D = (Resolve-Path $Calisma).Path
$eski = @{
    'karanlik:h264_nvenc' = '652x278,960x408,1306x556'; 'karanlik:hevc_nvenc' = '806x344,1152x490,1574x670'; 'karanlik:av1_nvenc' = '922x392,1382x588,1882x802'
    'parlak:h264_nvenc' = '806x344,1152x490,1574x670'; 'parlak:hevc_nvenc' = '998x424,1420x604,1920x818'; 'parlak:av1_nvenc' = '1152x490,1728x736,1920x818'
    'hareketli:h264_nvenc' = '652x278,922x392,1266x540'; 'hareketli:hevc_nvenc' = '806x344,1152x490,1574x670'; 'hareketli:av1_nvenc' = '922x392,1382x588,1882x802'
    'orta:h264_nvenc' = '1036x442,1498x638,1920x818'; 'orta:hevc_nvenc' = '1266x540,1842x784,1920x818'; 'orta:av1_nvenc' = '1498x638,1920x818,1920x818'
}
$bv = @{ 1000 = 967; 2000 = 1945; 3500 = 3412 }
$yeni = Get-Content (Join-Path $D 'kos.json') -Raw | ConvertFrom-Json
foreach ($u in ($yeni | Where-Object { $_.kol -eq 'urun' -and $_.istenen_kbit -ne 600 })) {
    $geo = $eski["$($u.kesit):$($u.kodek)"].Split(',')[@(1000, 2000, 3500).IndexOf([int]$u.istenen_kbit)]
    $w, $h = $geo.Split('x')
    $b = $bv[[int]$u.istenen_kbit]
    $taban = [math]::Max(39, [math]::Ceiling([double]$w * [double]$h * (4.29e-3 * 24 + 0.0756) * 1.15 / 1000))
    $acik = ($b / $taban - 6.0) / (11.4 - 6.0)
    $tepe = [math]::Min(1.10, [math]::Max(1.02, 1.02 + 0.08 * $acik))
    $tampon = 1 + 2 * ($tepe - 1)
    $pr = if ($u.kodek -eq 'av1_nvenc') { 'p6' } else { 'p4' }
    $kol = "eski=-rc vbr -multipass fullres -g 120 -spatial-aq 1 -temporal-aq 1 PEAK=$($tepe.ToString('0.###', $Inv)) BUF=$($tampon.ToString('0.###', $Inv))"
    & (Join-Path $PSScriptRoot 'tara.ps1') -Calisma $Calisma -Json $Json -Hucreler "$($u.kesit):$($u.kodek):${w}:${h}:$($u.kbps.ToString($Inv)):$pr" -KolTanim $kol
}
