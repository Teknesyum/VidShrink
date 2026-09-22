#requires -Version 7
param(
    [string]$Ham = 'docs/olcumler/nvenc-5-ham.json',
    [string]$HandBrake = 'docs/olcumler/nvenc-3-lookahead-handbrake.md'
)
$ErrorActionPreference = 'Stop'
$h = Get-Content $Ham -Raw | ConvertFrom-Json
$r = @($h.kalite)
$hb = @{}
foreach ($l in Get-Content $HandBrake) {
    if ($l -match '^\| (karanlik|parlak|hareketli) \| (hevc|av1) \| (\d+) \|') {
        $c = $l.Split('|') | ForEach-Object Trim
        $hb["$($c[1])-$($c[2])_nvenc-$($c[3])"] = [double]($c[7].Split('/')[0].Trim())
    }
}
function Anahtar($x) { "$($x.kesit)-$($x.kodek)-$($x.istenen_kbit)" }
$taban = @{}; foreach ($x in $r | Where-Object kol -eq 'taban') { $taban[(Anahtar $x)] = $x }
$ayni = @($r | Where-Object kol -eq 'taban' | Where-Object { $_.bayt -eq $_.nvenc4_k097_bayt }).Count
"taban baytı nvenc-4 k097 ile aynı: $ayni / 18"
"| Kol | ≥ taban+0,10 | taban−0,30'dan kötü | En kötü fark | Ort. fark | Süre oranı (toplam) | Süre oranı (deneme başına) | Tavan aşımı | HB önünde (ort) |"
"|---|---|---|---|---|---|---|---|---|"
foreach ($kol in 'taban', 'uhq', 'p7', 'uhqp7') {
    $s = @($r | Where-Object kol -eq $kol)
    $f = $s | ForEach-Object { $_.vmafneg_ort - $taban[(Anahtar $_)].vmafneg_ort }
    $iyi = @($f | Where-Object { $_ -ge 0.0999 }).Count
    $kotu = @($f | Where-Object { $_ -lt -0.3001 }).Count
    $sure = ($s | Measure-Object kodlama_sn -Sum).Sum / (($r | Where-Object kol -eq 'taban') | Measure-Object kodlama_sn -Sum).Sum
    $dk = ($s | ForEach-Object { $_.kodlama_sn / $_.deneme } | Measure-Object -Sum).Sum / (($r | Where-Object kol -eq 'taban') | ForEach-Object { $_.kodlama_sn / $_.deneme } | Measure-Object -Sum).Sum
    $asim = @($s | Where-Object hedefi_asti).Count
    $hbon = @($s | Where-Object { $_.vmafneg_ort -gt $hb["$($_.kesit)-$($_.kodek)-$($_.istenen_kbit)"] }).Count
    "| $kol | $iyi / 18 | $kotu | {0:+0.00;-0.00} | {1:+0.000;-0.000} | {2:0.00} | {3:0.00} | $asim | $hbon / 18 |" -f ($f | Measure-Object -Minimum).Minimum, ($f | Measure-Object -Average).Average, $sure, $dk
}
""
"| Kesit | Kodek | kbit | HB | taban | uhq | p7 | uhq+p7 |"
"|---|---|---|---|---|---|---|---|"
foreach ($x in $r | Where-Object kol -eq 'taban') {
    $a = Anahtar $x
    $hucre = foreach ($kol in 'uhq', 'p7', 'uhqp7') {
        $y = $r | Where-Object { $_.kol -eq $kol -and (Anahtar $_) -eq $a }
        "{0:0.00} ({1:+0.00;-0.00}) / {2:0.000}" -f $y.vmafneg_ort, ($y.vmafneg_ort - $x.vmafneg_ort), $y.teslim_orani
    }
    "| $($x.kesit) | $($x.kodek -replace '_nvenc') | $($x.istenen_kbit) | $($hb[$a]) | {0:0.00} / {1:0.000} | $($hucre -join ' | ') |" -f $x.vmafneg_ort, $x.teslim_orani
}
""
"| Kesit | Kodek | taban | uhq | p7 | uhq+p7 |"
"|---|---|---|---|---|---|"
foreach ($g in $h.hiz | Group-Object kesit, kodek) {
    $t = ($g.Group | Where-Object kol -eq 'taban').sn_ortanca
    $v = foreach ($kol in 'taban', 'uhq', 'p7', 'uhqp7') { $s = ($g.Group | Where-Object kol -eq $kol).sn_ortanca; "{0:0.000} sn ×{1:0.00}" -f $s, ($s / $t) }
    "| $($g.Group[0].kesit) | $($g.Group[0].kodek -replace '_nvenc') | $($v -join ' | ') |"
}
