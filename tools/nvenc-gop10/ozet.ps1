param([string]$Calisma = '.calisma/gop10', [string]$Json = 'gop.json')
$ErrorActionPreference = 'Stop'
$Inv = [Globalization.CultureInfo]::InvariantCulture
$D = (Resolve-Path $Calisma).Path
$hb = @{
    'hareketli:av1_nvenc:1000' = '-2.85:87.58:77.71'; 'hareketli:av1_nvenc:2000' = '0.33:97.09:91.16'
    'hareketli:h264_nvenc:1000' = '-8.73:69.31:50.03'; 'hareketli:h264_nvenc:2000' = '-0.88:89.78:79.00'
    'hareketli:hevc_nvenc:1000' = '-3.46:85.87:76.28'; 'hareketli:hevc_nvenc:2000' = '-0.40:96.15:89.29'
    'karanlik:av1_nvenc:1000' = '-0.22:86.89:81.23'; 'karanlik:av1_nvenc:2000' = '1.17:95.38:90.73'
    'karanlik:h264_nvenc:1000' = '-0.47:72.71:59.54'; 'karanlik:h264_nvenc:2000' = '5.59:89.28:81.34'
    'karanlik:hevc_nvenc:1000' = '1.99:85.18:77.95'; 'karanlik:hevc_nvenc:2000' = '2.03:94.11:89.44'
    'parlak:av1_nvenc:1000' = '1.84:86.03:70.44'; 'parlak:av1_nvenc:2000' = '-4.98:92.17:86.06'
    'parlak:h264_nvenc:1000' = '4.15:77.88:50.98'; 'parlak:h264_nvenc:2000' = '-7.68:86.44:68.22'
    'parlak:hevc_nvenc:1000' = '-12.38:81.11:65.94'; 'parlak:hevc_nvenc:2000' = '0.48:91.03:85.27'
    'orta:av1_nvenc:2000' = '0.12:95.96:93.35'; 'orta:h264_nvenc:2000' = '3.06:94.94:92.15'; 'orta:hevc_nvenc:2000' = '1.34:95.31:93.07'
}
$r = Get-Content (Join-Path $D $Json) -Raw | ConvertFrom-Json
function Kbit($x) { if ($x.hedef_kbps -gt 1500) { 2000 } else { 1000 } }
$sat = foreach ($a in ($r | Where-Object kol -eq 'g120')) {
    $b = $r | Where-Object { $_.kol -eq 'g240' -and $_.kesit -eq $a.kesit -and $_.kodek -eq $a.kodek -and $_.hedef_kbps -eq $a.hedef_kbps }
    $kb = Kbit $a
    $s, $ho, $hp = $hb["$($a.kesit):$($a.kodek):$kb"].Split(':') | ForEach-Object { [double]::Parse($_, $Inv) }
    [pscustomobject]@{ kesit = $a.kesit; kodek = $a.kodek -replace '_nvenc', ''; kbit = $kb; geo = $a.geometri; hedef = $a.hedef_kbps
        k120 = $a.kbps; k240 = $b.kbps; o120 = $a.vmafneg_ort; p120 = $a.vmafneg_p10; o240 = $b.vmafneg_ort; p240 = $b.vmafneg_p10
        dort = [math]::Round($b.vmafneg_ort - $a.vmafneg_ort, 2); dp10 = [math]::Round($b.vmafneg_p10 - $a.vmafneg_p10, 2)
        hbsap = $s; adil = [math]::Abs($s) -le 2
        hb120o = [math]::Round($a.vmafneg_ort - $ho, 2); hb120p = [math]::Round($a.vmafneg_p10 - $hp, 2)
        hb240o = [math]::Round($b.vmafneg_ort - $ho, 2); hb240p = [math]::Round($b.vmafneg_p10 - $hp, 2) }
}
$sat = $sat | Sort-Object kesit, kodek, kbit
'| Kesit | Kodek | kbit | Geo | Hedef kbps | g120 kbps / ort / p10 | g240 kbps / ort / p10 | g240−g120 ort / p10 | HB sapma % | g120−HB ort / p10 | g240−HB ort / p10 |'
'|---|---|---|---|---|---|---|---|---|---|---|'
foreach ($x in $sat) { "| $($x.kesit) | $($x.kodek) | $($x.kbit) | $($x.geo) | $($x.hedef) | $($x.k120) / $($x.o120) / $($x.p120) | $($x.k240) / $($x.o240) / $($x.p240) | $($x.dort) / $($x.dp10) | $($x.hbsap)$(if ($x.adil) { '' } else { ' (adil değil)' }) | $($x.hb120o) / $($x.hb120p) | $($x.hb240o) / $($x.hb240p) |" }
''
'| Hücre | g240−g120 ort | p10 | ort ≥ 0 | p10 ≥ −0,20 |'
'|---|---|---|---|---|'
$gec1 = 0; $gec2 = $true
foreach ($g in ($sat | Where-Object kesit -ne 'orta' | Group-Object kesit, kodek)) {
    $o = [math]::Round(($g.Group | Measure-Object dort -Average).Average, 3); $p = [math]::Round(($g.Group | Measure-Object dp10 -Average).Average, 3)
    if ($o -ge 0) { $gec1++ }; if ($p -lt -0.20) { $gec2 = $false }
    "| $($g.Name) | $o | $p | $(if ($o -ge 0) {'evet'} else {'hayır'}) | $(if ($p -ge -0.20) {'evet'} else {'hayır'}) |"
}
$ah = $sat | Where-Object { $_.kodek -eq 'hevc' -and $_.adil }
$a240o = [math]::Round(($ah | Measure-Object hb240o -Average).Average, 3); $a240p = [math]::Round(($ah | Measure-Object hb240p -Average).Average, 3)
$a120o = [math]::Round(($ah | Measure-Object hb120o -Average).Average, 3); $a120p = [math]::Round(($ah | Measure-Object hb120p -Average).Average, 3)
$tum = $sat; $to = [math]::Round(($tum | Measure-Object dort -Average).Average, 3); $tp = [math]::Round(($tum | Measure-Object dp10 -Average).Average, 3)
''
"Kural 1: ort ≥ 0 olan hücre $gec1/9 (≥7 gerekli) -> $(if ($gec1 -ge 7) {'GEÇTİ'} else {'KALDI'})"
"Kural 2: p10 < −0,20 hücre yok -> $(if ($gec2) {'GEÇTİ'} else {'KALDI'})"
"Kural 3: hevc adil ($($ah.Count) satır) g240−HB $a240o / $a240p (g120−HB $a120o / $a120p) -> $(if ($a240o -ge 0 -and $a240p -ge 0) {'GEÇTİ'} else {'KALDI'})"
"Tüm 21 çift ortalama g240−g120: $to / $tp"
