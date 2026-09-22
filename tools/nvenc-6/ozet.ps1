#requires -Version 7
param([string]$Ham = 'docs/olcumler/nvenc-6-ham.json')
$ErrorActionPreference = 'Stop'
$h = Get-Content $Ham -Raw | ConvertFrom-Json
$r = @($h.kalite)
function Anahtar($x) { "$($x.kesit)-$($x.istenen_kbit)" }
$taban = @{}; foreach ($x in $r | Where-Object kol -eq 'taban') { $taban[(Anahtar $x)] = $x }
$p7 = @($r | Where-Object kol -eq 'p7')
$f = $p7 | ForEach-Object { [math]::Round($_.vmafneg_ort - $taban[(Anahtar $_)].vmafneg_ort, 2) }
$iyi = @($f | Where-Object { $_ -ge 0.10 }).Count
$kotu = @($f | Where-Object { $_ -lt -0.10 }).Count
$asim = @($r | Where-Object hedefi_asti).Count
$oranlar = foreach ($g in $h.hiz | Group-Object kesit) {
    ($g.Group | Where-Object kol -eq 'p7').sn_ortanca / ($g.Group | Where-Object kol -eq 'taban').sn_ortanca
}
$enbuyuk = ($oranlar | Measure-Object -Maximum).Maximum
"1. >= taban+0,10: $iyi / 9 (esik 7) -> $(if ($iyi -ge 7) { 'GECTI' } else { 'KALDI' })"
"2. taban-0,10'dan kotu: $kotu -> $(if ($kotu -eq 0) { 'GECTI' } else { 'KALDI' })"
"3. tavan asimi (iki kol): $asim -> $(if (@($p7 | Where-Object hedefi_asti).Count -eq 0) { 'GECTI' } else { 'KALDI' })"
"4. saf sure orani en buyuk: {0:0.00} -> $(if ($enbuyuk -le 2.0) { 'GECTI' } else { 'KALDI' })" -f $enbuyuk
"en kotu fark {0:+0.00;-0.00}, ort fark {1:+0.000;-0.000}" -f ($f | Measure-Object -Minimum).Minimum, ($f | Measure-Object -Average).Average
"urun suresi orani (toplam) {0:0.00}" -f (($p7 | Measure-Object kodlama_sn -Sum).Sum / (($r | Where-Object kol -eq 'taban') | Measure-Object kodlama_sn -Sum).Sum)
""
"| Kesit | kbit | taban | p7 | fark | teslim taban / p7 | deneme taban / p7 | p10 taban / p7 |"
"|---|---|---|---|---|---|---|---|"
foreach ($y in $p7) {
    $x = $taban[(Anahtar $y)]
    "| $($y.kesit) | $($y.istenen_kbit) | {0:0.00} | {1:0.00} | {2:+0.00;-0.00} | {3:0.000} / {4:0.000} | $($x.deneme) / $($y.deneme) | {5:0.00} / {6:0.00} |" -f $x.vmafneg_ort, $y.vmafneg_ort, [math]::Round($y.vmafneg_ort - $x.vmafneg_ort, 2), $x.teslim_orani, $y.teslim_orani, $x.vmafneg_p10, $y.vmafneg_p10
}
""
"| Kesit | taban (p4) | p7 | oran |"
"|---|---|---|---|"
foreach ($g in $h.hiz | Group-Object kesit) {
    $t = ($g.Group | Where-Object kol -eq 'taban').sn_ortanca; $s = ($g.Group | Where-Object kol -eq 'p7').sn_ortanca
    "| $($g.Name) | {0:0.000} sn | {1:0.000} sn | ×{2:0.00} |" -f $t, $s, ($s / $t)
}
