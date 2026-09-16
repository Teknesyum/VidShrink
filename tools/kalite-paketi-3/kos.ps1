param(
    [Parameter(Mandatory)][ValidateSet('kesit', 'whatsapp', 'handbrake', 'ceza', 'av1', 'hedefbant', 'oran')][string]$Is,
    [string]$BenchE1 = '',
    [string]$Uzun = '',
    [string]$EkranUzun = '',
    [int]$UzunSure = 120,
    [string]$Hedefler = '50,100',
    [string]$OranKbitler = '400,800,1600,3200,6400',
    [Parameter(Mandatory)][string]$Kaynak,
    [Parameter(Mandatory)][string]$Cikti,
    [Parameter(Mandatory)][string]$Bench,
    [string]$HandBrake = '',
    [string]$Kesit = '',
    [string]$EkranKaynak = '',
    [string]$Kbitler = '600,2000',
    [string]$Izgara = '{"preset":[4,6,8,10],"crf":[28,36,44],"kbit":[],"filmgrain":[0,8],"tune":[0,1],"keyint":[240]}'
)

$ErrorActionPreference = 'Stop'
if (-not $env:GITHUB_ACTIONS) {
    Write-Error 'Bu duzenek yalniz GitHub Actions kosucusunda calisir; kullanicinin makinesinde kodlama dongusu yasak.'
    exit 3
}

$Inv = [Globalization.CultureInfo]::InvariantCulture
New-Item -ItemType Directory -Force $Cikti | Out-Null

function Ffmpeg([string[]]$Argumanlar) {
    & ffmpeg.exe -hide_banner -nostdin -loglevel error -y @Argumanlar
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg basarisiz: $($Argumanlar -join ' ')" }
}

function Probe([string]$Yol) {
    $j = & ffprobe.exe -v error -select_streams v:0 -show_entries stream=width,height,r_frame_rate:format=duration -of json $Yol | ConvertFrom-Json
    $p = $j.streams[0].r_frame_rate.Split('/')
    [pscustomobject]@{
        W = [int]$j.streams[0].width
        H = [int]$j.streams[0].height
        Fps = [double]::Parse($p[0], $Inv) / [double]::Parse($p[1], $Inv)
        FpsMetin = $j.streams[0].r_frame_rate
        Sure = [double]::Parse($j.format.duration, $Inv)
    }
}

function Cift([double]$x) { [int]([math]::Round($x / 2) * 2) }

function Olc([string]$Referans, [string]$Test, [string]$Json, [string]$Fps = '') {
    $a = @('measure-pair', $Referans, $Test, '--out', $Json)
    if ($Fps) { $a += @('--fps', $Fps) }
    & dotnet $Bench @a | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "olcum basarisiz: $Test" }
    Get-Content $Json -Raw | ConvertFrom-Json
}

function Satir($Etiket, $Kol, $Kbit, $Dosya, $Olcu, $Ek) {
    $s = [ordered]@{
        kesit = $Etiket; kol = $Kol; istenen_kbit = $Kbit
        kbps = [math]::Round($Olcu.Kbps, 1); mb = [math]::Round($Olcu.Mb, 4)
        vmafneg_ort = $Olcu.VmafNegMean; vmafneg_harm = $Olcu.VmafNegHarmonic; vmafneg_p10 = $Olcu.VmafNegP10
        xpsnr = $Olcu.Xpsnr; karanlik_psnr = $Olcu.Karanlik.KaranlikPsnr; karanlik_ton = $Olcu.Karanlik.TonOrani
        karanlik_kayma = $Olcu.Karanlik.OrtalamaKayma; karanlik_oran = $Olcu.Karanlik.KaranlikOran; kare = $Olcu.Karanlik.Kare
        dosya = (Split-Path $Dosya -Leaf)
    }
    if ($Ek) { foreach ($k in $Ek.Keys) { $s[$k] = $Ek[$k] } }
    [pscustomobject]$s
}

function Kesitler {
    $bilgi = Probe $Kaynak
    $log = Join-Path $Cikti 'yavg.txt'
    $logFfmpeg = $log.Replace('\', '/').Replace(':', '\:')
    Ffmpeg @('-i', $Kaynak, '-an', '-sn', '-vf', "fps=1,scale=160:-2,signalstats,metadata=print:file='$logFfmpeg'", '-f', 'null', '-')
    $degerler = @()
    $yayilim = @()
    $hareket = @()
    $low = 0
    foreach ($l in Get-Content $log) {
        if ($l -match 'signalstats\.YAVG=([\d.]+)') { $degerler += [double]::Parse($Matches[1], $Inv) }
        elseif ($l -match 'signalstats\.YDIF=([\d.]+)') { $hareket += [double]::Parse($Matches[1], $Inv) }
        elseif ($l -match 'signalstats\.YLOW=([\d.]+)') { $low = [double]::Parse($Matches[1], $Inv) }
        elseif ($l -match 'signalstats\.YHIGH=([\d.]+)') { $yayilim += [double]::Parse($Matches[1], $Inv) - $low }
    }
    $pencere = 10
    $bas = 30
    $son = [math]::Min($degerler.Count, [math]::Floor($bilgi.Sure)) - 180
    $adaylar = @()
    for ($t = $bas; $t + $pencere -le $son; $t += 5) {
        $dilim = $degerler[$t..($t + $pencere - 1)]
        $ort = ($dilim | Measure-Object -Average).Average
        $yay = ($yayilim[$t..($t + $pencere - 1)] | Measure-Object -Minimum).Minimum
        $hrk = ($hareket[$t..($t + $pencere - 1)] | Measure-Object -Average).Average
        $adaylar += [pscustomobject]@{ Baslangic = $t; YavgOrt = [math]::Round($ort, 2); YayilimMin = [math]::Round($yay, 2); YdifOrt = [math]::Round($hrk, 2) }
    }
    $karanlik = $adaylar | Where-Object { $_.YayilimMin -ge 20 } | Sort-Object YavgOrt | Select-Object -First 1
    $parlak = $adaylar | Sort-Object YavgOrt -Descending | Select-Object -First 1
    $ortaT = [math]::Floor(($bas + $son) / 2 / 5) * 5
    $orta = $adaylar | Sort-Object { [math]::Abs($_.Baslangic - $ortaT) } | Select-Object -First 1
    $hareketli = $adaylar | Where-Object { $_.Baslangic -notin @($karanlik.Baslangic, $parlak.Baslangic, $orta.Baslangic) } | Sort-Object YdifOrt -Descending | Select-Object -First 1
    $secim = [ordered]@{ karanlik = $karanlik; parlak = $parlak; orta = $orta; hareketli = $hareketli }
    foreach ($ad in $secim.Keys) {
        $hedef = Join-Path $Cikti "kesit-$ad.mkv"
        Ffmpeg @('-ss', $secim[$ad].Baslangic.ToString($Inv), '-i', $Kaynak, '-t', '10', '-an', '-sn', '-map', '0:v:0', '-c:v', 'ffv1', '-pix_fmt', 'yuv420p', $hedef)
    }
    if ($EkranKaynak) {
        $ekranBas = if ((Probe $EkranKaynak).Sure -ge 15) { 5 } else { 0 }
        Ffmpeg @('-ss', "$ekranBas", '-i', $EkranKaynak, '-t', '10', '-an', '-sn', '-map', '0:v:0', '-c:v', 'ffv1', '-pix_fmt', 'yuv420p', (Join-Path $Cikti 'kesit-ekran.mkv'))
        $secim['ekran'] = [pscustomobject]@{ Kaynak = (Split-Path $EkranKaynak -Leaf); Baslangic = $ekranBas }
    }
    [pscustomobject]@{ Kaynak = (Split-Path $Kaynak -Leaf); Genislik = $bilgi.W; Yukseklik = $bilgi.H; Fps = $bilgi.FpsMetin; Sure = $bilgi.Sure; Secim = $secim; Adaylar = $adaylar } |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $Cikti 'kesitler.json')
}

function X264([string]$Girdi, [string]$Cikis, [int]$W, [int]$H, [int]$Kbit, [int]$Gop, [string[]]$Psy) {
    $pass = [IO.Path]::ChangeExtension($Cikis, '.pass')
    $ortak = @('-i', $Girdi, '-an', '-vf', "scale=${W}:${H}:flags=lanczos", '-c:v', 'libx264', '-preset', 'slow', '-b:v', "${Kbit}k", '-g', "$Gop", '-pix_fmt', 'yuv420p') + $Psy
    Ffmpeg ($ortak + @('-pass', '1', '-passlogfile', $pass, '-f', 'null', 'NUL'))
    Ffmpeg ($ortak + @('-pass', '2', '-passlogfile', $pass, '-movflags', '+faststart', $Cikis))
}

function WhatsApp {
    $urunPsy = @(& dotnet $Bench psy-args libx264 | ConvertFrom-Json)
    $kollar = [ordered]@{
        'urun' = $urunPsy
        'urun-tekrar' = $urunPsy
        'aq3' = @('-x264-params', 'aq-mode=3')
        'aq3-s08' = @('-x264-params', 'aq-mode=3:aq-strength=0.8')
        'negatif-aq0' = @('-x264-params', 'aq-mode=0')
    }
    $satirlar = @()
    foreach ($ad in @('karanlik', 'orta')) {
        $girdi = Join-Path $Cikti "kesit-$ad.mkv"
        $b = Probe $girdi
        $w = 1280; $h = Cift ($b.H * 1280 / $b.W)
        $gop = [int][math]::Round($b.Fps * 2)
        foreach ($kbit in @(500, 1000)) {
            foreach ($kol in $kollar.Keys) {
                $cikis = Join-Path $Cikti "wa-$ad-$kbit-$kol.mp4"
                X264 $girdi $cikis $w $h $kbit $gop $kollar[$kol]
                $o = Olc $girdi $cikis ([IO.Path]::ChangeExtension($cikis, '.json'))
                $satirlar += Satir $ad $kol $kbit $cikis $o @{ psy = ($kollar[$kol] -join ' '); geometri = "${w}x${h}" }
            }
        }
    }
    $satirlar | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $Cikti 'whatsapp.json')
}

function HandBrakeKodla([string]$Girdi, [string]$Cikis, [int]$Kbit, $Bilgi) {
    & $HandBrake -i $Girdi -o $Cikis -Z 'H.265 MKV 1080p30' -e x265 --encoder-preset slow -b $Kbit --multi-pass --turbo -a none --crop-mode none --width $Bilgi.W --height $Bilgi.H -r $Bilgi.Fps.ToString('0.###', $Inv) --cfr 2>&1 |
        Out-File -Append (Join-Path $Cikti 'handbrake.log')
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Cikis)) { throw "HandBrakeCLI basarisiz: $Cikis" }
}

function HandBrakeKiyas {
    if (-not $HandBrake) { throw 'HandBrake yolu verilmedi.' }
    $satirlar = @()
    $adlar = if ($Kesit) { @($Kesit) } else { @('karanlik', 'parlak', 'orta') }
    foreach ($ad in $adlar) {
        $girdi = Join-Path $Cikti "kesit-$ad.mkv"
        $b = Probe $girdi
        foreach ($kbit in @($Kbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
            $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
            $mbMetin = $mb.ToString($Inv)
            $kollar = [ordered]@{
                'urun-x265' = @('--force-codec', 'libx265', '--no-resolution-drop', '--no-fps-drop')
                'urun-svtav1' = @('--force-codec', 'libsvtav1', '--no-resolution-drop', '--no-fps-drop')
                'urun-otomatik' = @()
            }
            $x265Kbps = $null
            foreach ($kol in $kollar.Keys) {
                $klasor = Join-Path $Cikti "hb-$ad-$kbit-$kol"
                & dotnet $Bench shrink $girdi $mbMetin --out $klasor --speed quality --no-measure @($kollar[$kol]) | Out-File (Join-Path $Cikti "hb-$ad-$kbit-$kol.log")
                if ($LASTEXITCODE -ne 0) { throw "bench shrink basarisiz: $kol" }
                $cikis = Get-ChildItem $klasor -Filter '*.mp4' | Select-Object -First 1
                $sonuc = Get-Content (Join-Path $klasor 'results.json') -Raw | ConvertFrom-Json
                $o = Olc $girdi $cikis.FullName (Join-Path $klasor 'olcu.json') $b.FpsMetin
                $satirlar += Satir $ad $kol $kbit $cikis.FullName $o @{ kodlayici = $sonuc[0].Codec; geometri = "$($sonuc[0].Width)x$($sonuc[0].Height)@$($sonuc[0].Fps)"; hedef_mb = $mb; doluluk = $sonuc[0].FillPercent }
                if ($kol -eq 'urun-x265') { $x265Kbps = $o.Kbps }
            }
            $hbKbit = [int][math]::Round($x265Kbps)
            $cikis = Join-Path $Cikti "hb-$ad-$kbit-handbrake.mkv"
            HandBrakeKodla $girdi $cikis $hbKbit $b
            $o = Olc $girdi $cikis ([IO.Path]::ChangeExtension($cikis, '.json')) $b.FpsMetin
            $satirlar += Satir $ad 'handbrake' $kbit $cikis $o @{ kodlayici = 'HandBrakeCLI x265 slow'; hb_kbit = $hbKbit; hedef_mb = $mb }
            $cikis = Join-Path $Cikti "hb-$ad-$kbit-negatif-yarim.mkv"
            HandBrakeKodla $girdi $cikis ([int][math]::Round($hbKbit / 2)) $b
            $o = Olc $girdi $cikis ([IO.Path]::ChangeExtension($cikis, '.json')) $b.FpsMetin
            $satirlar += Satir $ad 'negatif-handbrake-yarim-bit' $kbit $cikis $o @{ kodlayici = 'HandBrakeCLI x265 slow'; hb_kbit = [int][math]::Round($hbKbit / 2) }
        }
    }
    $satirlar | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $Cikti "handbrake-$(if ($Kesit) { $Kesit } else { 'hepsi' }).json")
}

function Ceza {
    if (-not $Kesit) { throw 'Ceza izgarasi icin -Kesit gerekli.' }
    $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    $b = Probe $girdi
    $psy = @(& dotnet $Bench psy-args libsvtav1 | ConvertFrom-Json)
    $satirlar = @()
    foreach ($olcek in @(1.0, 0.6667, 0.5, 0.3333)) {
        $w = Cift ($b.W * $olcek); $h = Cift ($b.H * $olcek)
        foreach ($fps in @(24, 16, 12)) {
            foreach ($kbit in @(300, 1200)) {
                $cikis = Join-Path $Cikti "ceza-$Kesit-${w}x${h}-$fps-$kbit.mkv"
                $pass = [IO.Path]::ChangeExtension($cikis, '.pass')
                $ortak = @('-i', $girdi, '-an', '-vf', "scale=${w}:${h}:flags=lanczos,fps=$fps", '-c:v', 'libsvtav1', '-preset', '8', '-b:v', "${kbit}k", '-g', "$($fps * 2)", '-pix_fmt', 'yuv420p') + $psy
                Ffmpeg ($ortak + @('-pass', '1', '-passlogfile', $pass, '-f', 'null', 'NUL'))
                Ffmpeg ($ortak + @('-pass', '2', '-passlogfile', $pass, $cikis))
                $o = Olc $girdi $cikis ([IO.Path]::ChangeExtension($cikis, '.json')) $b.FpsMetin
                $satirlar += Satir $Kesit "${w}x${h}@$fps" $kbit $cikis $o @{ olcek = $olcek; fps = $fps; genislik = $w; yukseklik = $h }
            }
        }
    }
    $satirlar | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $Cikti "ceza-$Kesit.json")
}

function Av1 {
    if (-not $Kesit) { throw 'AV1 izgarasi icin -Kesit gerekli.' }
    $g = $Izgara | ConvertFrom-Json
    $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    $b = Probe $girdi
    $kbitler = if ($g.kbit -is [pscustomobject]) { @($g.kbit.$Kesit) } else { @($g.kbit) }
    $kontroller = @($g.crf | Where-Object { $null -ne $_ } | ForEach-Object { [pscustomobject]@{ Tur = 'crf'; Deger = [int]$_ } }) + @($kbitler | Where-Object { $null -ne $_ } | ForEach-Object { [pscustomobject]@{ Tur = 'kbit'; Deger = [int]$_ } })
    $ekler = if ($g.PSObject.Properties['ek']) { @($g.ek) } else { @($null) }
    $bicimler = if ($g.PSObject.Properties['pixfmt']) { @($g.pixfmt) } else { @($null) }
    $satirlar = @()
    foreach ($preset in $g.preset) {
        foreach ($k in $kontroller) {
            foreach ($grain in $g.filmgrain) {
                foreach ($tune in $g.tune) {
                    foreach ($keyint in $g.keyint) {
                    foreach ($ek in $ekler) {
                    foreach ($bicim in $bicimler) {
                        $etiket = "p$preset-$($k.Tur)$($k.Deger)-fg$grain-t$tune-k$keyint"
                        if ($null -ne $ek) { $etiket += "-e$([array]::IndexOf($ekler, $ek))" }
                        if ($null -ne $bicim) { $etiket += "-$bicim" }
                        $pix = if ($bicim) { $bicim } else { 'yuv420p10le' }
                        $parametre = "tune=${tune}:film-grain=${grain}"
                        if ($ek) { $parametre += ":$ek" }
                        $cikis = Join-Path $Cikti "av1-$Kesit-$etiket.mkv"
                        $ortak = @('-i', $girdi, '-an', '-c:v', 'libsvtav1', '-preset', "$preset", '-g', "$keyint", '-pix_fmt', $pix, '-svtav1-params', $parametre, '-fflags', '+bitexact', '-flags:v', '+bitexact')
                        $sure = [Diagnostics.Stopwatch]::StartNew()
                        if ($k.Tur -eq 'crf') {
                            Ffmpeg ($ortak + @('-crf', "$($k.Deger)", $cikis))
                        } else {
                            $pass = [IO.Path]::ChangeExtension($cikis, '.pass')
                            Ffmpeg ($ortak + @('-b:v', "$($k.Deger)k", '-pass', '1', '-passlogfile', $pass, '-f', 'null', 'NUL'))
                            Ffmpeg ($ortak + @('-b:v', "$($k.Deger)k", '-pass', '2', '-passlogfile', $pass, $cikis))
                        }
                        $sure.Stop()
                        $o = Olc $girdi $cikis ([IO.Path]::ChangeExtension($cikis, '.json')) $b.FpsMetin
                        $satirlar += Satir $Kesit $etiket $(if ($k.Tur -eq 'kbit') { $k.Deger } else { $null }) $cikis $o @{ preset = $preset; kontrol = $k.Tur; deger = $k.Deger; filmgrain = $grain; tune = $tune; keyint = $keyint; ek = $(if ($ek) { $ek } else { '' }); pixfmt = $pix; bayt = (Get-Item $cikis).Length; sha256 = (Get-FileHash $cikis -Algorithm SHA256).Hash; kodlama_sn = [math]::Round($sure.Elapsed.TotalSeconds, 1) }
                        Remove-Item $cikis -ErrorAction SilentlyContinue
                        ConvertTo-Json -Depth 4 -InputObject @($satirlar) | Set-Content (Join-Path $Cikti "av1-$Kesit.json")
                    }
                    }
                    }
                }
            }
        }
    }
    ConvertTo-Json -Depth 4 -InputObject @($satirlar) | Set-Content (Join-Path $Cikti "av1-$Kesit.json")
}

function Kollar {
    if (-not $BenchE1) { throw 'e1 kolu icin -BenchE1 gerekli.' }
    [ordered]@{ 'e0' = $Bench; 'e1' = $BenchE1 }
}

function HedefBant {
    if (-not $Kesit) { throw 'Hedef bant olcumu icin -Kesit gerekli.' }
    $secim = (Get-Content (Join-Path $Cikti 'kesitler.json') -Raw | ConvertFrom-Json).Secim.$Kesit
    $kaynakUzun = if ($Kesit -eq 'ekran') { $EkranUzun } else { $Uzun }
    if (-not $kaynakUzun) { throw "uzun kaynak verilmedi: $Kesit" }
    $kb = Probe $kaynakUzun
    $bas = [int]$secim.Baslangic
    if (($bas + $UzunSure) -gt $kb.Sure) { $bas = [int][math]::Max(0, [math]::Floor($kb.Sure - 180 - $UzunSure)) }
    $dongu = ($bas + $UzunSure) -gt $kb.Sure
    if ($dongu) { $bas = 0 }
    $girdi = Join-Path $Cikti "uzun-$Kesit.mkv"
    $on = if ($dongu) { @('-stream_loop', '-1') } else { @() }
    Ffmpeg ($on + @('-ss', "$bas", '-i', $kaynakUzun, '-t', "$UzunSure", '-an', '-sn', '-map', '0:v:0', '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '4', '-pix_fmt', 'yuv420p', $girdi))
    $gb = Probe $girdi
    $kaynakMb = [math]::Round((Get-Item $girdi).Length / 1MB, 3)
    $satirlar = @()
    $kollar = Kollar
    foreach ($hedef in @($Hedefler.Split(',') | ForEach-Object { [double]::Parse($_.Trim(), $Inv) })) {
        foreach ($kol in $kollar.Keys) {
            $ad = "hbant-$Kesit-$kol-$($hedef.ToString($Inv))"
            $klasor = Join-Path $Cikti $ad
            $log = Join-Path $Cikti "$ad.log"
            $sure = [Diagnostics.Stopwatch]::StartNew()
            & dotnet $kollar[$kol] shrink $girdi $hedef.ToString($Inv) --out $klasor --speed quality --no-measure --force-codec libsvtav1 --no-resolution-drop --no-fps-drop *>&1 | Out-File $log
            $sure.Stop()
            if ($LASTEXITCODE -ne 0) { throw "bench shrink basarisiz: $ad" }
            $metin = Get-Content $log
            $komut = $metin | Where-Object { $_ -like 'komut:*' } | Select-Object -First 1
            $params = if ($komut -match '-svtav1-params\s+"?([^\s"]+)') { $Matches[1] } else { '' }
            $denemeler = @(foreach ($l in $metin) {
                if ($l -match '^\s*deneme (\d+): (.+), (\S+), (\d+)k, hedeflenen ([\d.]+) MB, cikan ([\d.]+) MB') {
                    [pscustomobject]@{ no = [int]$Matches[1]; dal = $Matches[2]; mod = $Matches[3]; kbit = [int]$Matches[4]; hedeflenen_mb = [double]::Parse($Matches[5], $Inv); cikan_mb = [double]::Parse($Matches[6], $Inv) }
                }
            })
            $sonuc = @(Get-Content (Join-Path $klasor 'results.json') -Raw | ConvertFrom-Json)[0]
            $mp4 = Get-ChildItem $klasor -Filter '*.mp4' -ErrorAction SilentlyContinue | Select-Object -First 1
            $teslimBayt = if ($mp4) { $mp4.Length } else { 0 }
            $alt = $hedef * 0.972
            $ilk = $denemeler | Where-Object { $_.no -eq 1 } | Select-Object -First 1
            $satirlar += [pscustomobject][ordered]@{
                kesit = $Kesit; kol = $kol; svtav1_params = $params; hedef_mb = $hedef; bant_alt_mb = [math]::Round($alt, 3)
                kaynak_bas = $bas; kaynak_sure = $gb.Sure; kaynak_dongu = $dongu; kaynak_mb = $kaynakMb; geometri = "$($sonuc.Width)x$($sonuc.Height)@$($sonuc.Fps)"
                ilk_kbit = $ilk.kbit; ilk_hedeflenen_mb = $ilk.hedeflenen_mb; ilk_cikan_mb = $ilk.cikan_mb
                ilk_doluluk = [math]::Round($ilk.cikan_mb / $hedef * 100, 2); ilk_bantta = ($ilk.cikan_mb -ge $alt -and $ilk.cikan_mb -le $hedef); ilk_dal = $ilk.dal
                deneme = $sonuc.Attempts; dallar = (($denemeler | ForEach-Object { "$($_.no):$($_.dal):$($_.kbit)k:$($_.cikan_mb)" }) -join ' | ')
                teslim = [bool]$mp4; teslim_bayt = $teslimBayt; teslim_mb = [math]::Round($teslimBayt / 1MB, 4); son_mb = $sonuc.ActualMb
                teslim_tasma = ($teslimBayt / 1MB -gt $hedef); bantta = $sonuc.InBand; kodlama_sn = [math]::Round($sure.Elapsed.TotalSeconds, 1)
                komut = $komut
            }
            if ($mp4) { Remove-Item $mp4.FullName }
            ConvertTo-Json -Depth 4 -InputObject @($satirlar) | Set-Content (Join-Path $Cikti "hedefbant-$Kesit.json")
        }
    }
    Remove-Item $girdi -ErrorAction SilentlyContinue
    ConvertTo-Json -Depth 4 -InputObject @($satirlar) | Set-Content (Join-Path $Cikti "hedefbant-$Kesit.json")
}

function Oran {
    if (-not $Kesit) { throw 'Oran olcumu icin -Kesit gerekli.' }
    $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    $b = Probe $girdi
    $e = Kollar
    $kollar = [ordered]@{
        'libx264' = @{ Kodlayici = 'libx264'; Preset = 'slow'; Psy = @(& dotnet $Bench psy-args libx264 | ConvertFrom-Json) }
        'libx265' = @{ Kodlayici = 'libx265'; Preset = 'slow'; Psy = @(& dotnet $Bench psy-args libx265 | ConvertFrom-Json) }
        'svtav1-e0' = @{ Kodlayici = 'libsvtav1'; Preset = '6'; Psy = @(& dotnet $e['e0'] psy-args libsvtav1 | ConvertFrom-Json) }
        'svtav1-e1' = @{ Kodlayici = 'libsvtav1'; Preset = '6'; Psy = @(& dotnet $e['e1'] psy-args libsvtav1 | ConvertFrom-Json) }
    }
    $satirlar = @()
    foreach ($kbit in @($OranKbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
        foreach ($kol in $kollar.Keys) {
            $k = $kollar[$kol]
            $cikis = Join-Path $Cikti "oran-$Kesit-$kol-$kbit.mkv"
            $pass = [IO.Path]::ChangeExtension($cikis, '.pass')
            $ortak = @('-i', $girdi, '-an', '-c:v', $k.Kodlayici, '-preset', $k.Preset, '-g', '240', '-pix_fmt', 'yuv420p') + $k.Psy + @('-b:v', "${kbit}k")
            $sure = [Diagnostics.Stopwatch]::StartNew()
            Ffmpeg ($ortak + @('-pass', '1', '-passlogfile', $pass, '-f', 'null', 'NUL'))
            Ffmpeg ($ortak + @('-pass', '2', '-passlogfile', $pass, $cikis))
            $sure.Stop()
            $o = Olc $girdi $cikis ([IO.Path]::ChangeExtension($cikis, '.json')) $b.FpsMetin
            $satirlar += Satir $Kesit $kol $kbit $cikis $o @{ kodlayici = $k.Kodlayici; preset = $k.Preset; psy = ($k.Psy -join ' '); bayt = (Get-Item $cikis).Length; kodlama_sn = [math]::Round($sure.Elapsed.TotalSeconds, 1) }
            Remove-Item $cikis -ErrorAction SilentlyContinue
            ConvertTo-Json -Depth 4 -InputObject @($satirlar) | Set-Content (Join-Path $Cikti "oran-$Kesit.json")
        }
    }
    ConvertTo-Json -Depth 4 -InputObject @($satirlar) | Set-Content (Join-Path $Cikti "oran-$Kesit.json")
}

switch ($Is) {
    'hedefbant' { HedefBant }
    'oran' { Oran }
    'av1' { Av1 }
    'kesit' { Kesitler }
    'whatsapp' { WhatsApp }
    'handbrake' { HandBrakeKiyas }
    'ceza' { Ceza }
}
