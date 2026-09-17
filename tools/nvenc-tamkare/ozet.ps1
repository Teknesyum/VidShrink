param([string]$Calisma = '.calisma/tamkare', [string]$Json = 'tamkare.json', [switch]$TamKare)
$ErrorActionPreference = 'Stop'
$Inv = [Globalization.CultureInfo]::InvariantCulture
$r = @(Get-Content (Join-Path (Resolve-Path $Calisma).Path $Json) -Raw | ConvertFrom-Json)
function F([double]$x) { $x.ToString('0.00', $Inv) }
$a = @(
    @{ k = 'hareketli'; c = 'hevc_nvenc'; b = 1899.5; o = 95.84; p = 88.05; ho = 96.15 },
    @{ k = 'parlak'; c = 'hevc_nvenc'; b = 1980.4; o = 90.53; p = 85.18; ho = 91.03 },
    @{ k = 'orta'; c = 'hevc_nvenc'; b = 1905.3; o = 94.80; p = 92.23; ho = 95.31 },
    @{ k = 'karanlik'; c = 'hevc_nvenc'; b = 1933.2; o = 93.69; p = 88.66; ho = 94.11 },
    @{ k = 'hareketli'; c = 'av1_nvenc'; b = 2020; o = 97.00; p = 90.25; ho = $null },
    @{ k = 'karanlik'; c = 'av1_nvenc'; b = 1912.6; o = 95.11; p = 90.55; ho = $null })
'| Hücre | Hedef kbps | 1882 ort / p10 | Tam kbps (sapma %) / ort / p10 | tam−1882 ort / p10 | tam−HB ort |'
'|---|---|---|---|---|---|'
$g1 = 0; $g3 = $true; $hb = @()
foreach ($h in $a) {
    $t = $r | Where-Object { $_.kesit -eq $h.k -and $_.kodek -eq $h.c -and $_.kol -eq 'temel' -and $_.geometri -eq '1920x818' -and $_.hedef_kbps -eq $h.b }
    $do = $t.vmafneg_ort - $h.o; $dp = $t.vmafneg_p10 - $h.p
    if ($do -ge -1e-9) { $g1++ }; if ($dp -lt -0.20 - 1e-9) { $g3 = $false }
    $hbs = if ($null -ne $h.ho) { $x = $t.vmafneg_ort - $h.ho; $hb += $x; F $x } else { '—' }
    "| $($h.k) $($h.c -replace '_nvenc','') | $($h.b) | $(F $h.o) / $(F $h.p) | $($t.kbps) ($($t.sapma_yuzde)) / $(F $t.vmafneg_ort) / $(F $t.vmafneg_p10) | $(F $do) / $(F $dp) | $hbs |"
}
$hbo = ($hb | Measure-Object -Average).Average
''
"A kural 1: tam−1882 ort ≥ 0 hücre $g1/6 (≥5) -> $(if ($g1 -ge 5) {'GEÇTİ'} else {'KALDI'})"
"A kural 2: hevc dörtlüsü ort(tam−HB) $(F $hbo) (≥ 0) -> $(if ($hbo -ge 0) {'GEÇTİ'} else {'KALDI'})"
"A kural 3: p10 < −0,20 hücre $(if ($g3) {'yok -> GEÇTİ'} else {'var -> KALDI'})"
''
$geo = if ($TamKare) { '1920x818' } else { '1882x802' }
$b = @(@('hareketli', $geo, 1899.5), @('parlak', $geo, 1980.4), @('orta', $geo, 1905.3), @('karanlik', '1650x702', 983.6),
    @('karanlik', '1920x818', 3458.2), @('parlak', '1920x818', 3498.3), @('hareketli', '1920x818', 3447.5))
'| Çift | Geo | Hedef kbps | temel kbps (sapma %) / ort / p10 | la20 kbps (sapma %) / ort / p10 | la20−temel ort / p10 |'
'|---|---|---|---|---|---|'
$n = 0; $fark = @(); $bant = $true
foreach ($x in $b) {
    $s = $r | Where-Object { $_.kesit -eq $x[0] -and $_.kodek -eq 'hevc_nvenc' -and $_.geometri -eq $x[1] -and $_.hedef_kbps -eq $x[2] }
    $t = $s | Where-Object kol -eq 'temel'; $l = $s | Where-Object kol -eq 'la20'
    $do = $l.vmafneg_ort - $t.vmafneg_ort; $fark += $do; if ($do -ge -1e-9) { $n++ }
    foreach ($y in $t, $l) { if ([math]::Abs($y.sapma_yuzde) -gt 1.5) { $bant = $false } }
    "| $($x[0]) | $($x[1]) | $($x[2]) | $($t.kbps) ($($t.sapma_yuzde)) / $(F $t.vmafneg_ort) / $(F $t.vmafneg_p10) | $($l.kbps) ($($l.sapma_yuzde)) / $(F $l.vmafneg_ort) / $(F $l.vmafneg_p10) | $(F $do) / $(F ($l.vmafneg_p10 - $t.vmafneg_p10)) |"
}
$ort = ($fark | Measure-Object -Average).Average
''
"B kural 1: la20−temel ort ≥ 0 çift $n/7 (≥5) -> $(if ($n -ge 5) {'GEÇTİ'} else {'KALDI'})"
"B kural 2: ortalama $(F $ort) (≥ +0,15) -> $(if ($ort -ge 0.15) {'GEÇTİ'} else {'KALDI'})"
"B kural 3: 14 kodlama ±%1,5 bantta -> $(if ($bant) {'GEÇTİ'} else {'KALDI'})"
