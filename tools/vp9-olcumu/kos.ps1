param(
    [Parameter(Mandatory)][ValidateSet('cpu', 'crf', 'inis', 'ozet')][string]$Is,
    [string]$Kesit,
    [int]$HedefK,
    [string]$Cikti,
    [string]$Girdi,
    [string]$Bench = $env:BENCH
)

$ErrorActionPreference = 'Stop'
$Kesitler = @('gren', 'gradyan', 'hareket')
$Hedefler = @(1500, 4000)
$CpuAdimlari = 0..8
$CrfAdimlari = @(20, 25, 30, 35, 40, 45, 50)
$UrunCpu = 4

function Kodla([string[]]$ArgList) {
    & ffmpeg -hide_banner -nostdin -loglevel error -y @ArgList
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg cikis $LASTEXITCODE : $($ArgList -join ' ')" }
}

function KesitUret([string]$Ad, [string]$Dizin) {
    $kaynak = switch ($Ad) {
        'gren' { 'testsrc2=s=1920x1080:r=24:d=10,noise=alls=18:allf=t+u,format=yuv420p' }
        'gradyan' { 'gradients=s=1920x1080:r=24:d=10:c0=0x203040:c1=0xd8c8a8:nb_colors=2:speed=0.02:seed=1,format=yuv420p' }
        'hareket' { 'testsrc2=s=1920x1080:r=24:d=10,scroll=h=0.03:v=0.015,format=yuv420p' }
        default { throw "bilinmeyen kesit: $Ad" }
    }
    $yol = Join-Path $Dizin "$Ad.mkv"
    Kodla @('-f', 'lavfi', '-i', $kaynak, '-frames:v', '240', '-c:v', 'ffv1', '-level', '3', '-g', '1', '-slices', '4', '-slicecrc', '0', '-map_metadata', '-1', '-fflags', '+bitexact', '-flags:v', '+bitexact', $yol)
    [pscustomobject]@{ Yol = $yol; Sha256 = (Get-FileHash $yol -Algorithm SHA256).Hash; Bayt = (Get-Item $yol).Length }
}

function Olc([string]$Ref, [string]$Test, [string]$Json) {
    & dotnet $Bench measure-pair $Ref $Test --out $Json | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "measure-pair cikis $LASTEXITCODE : $Test" }
    Get-Content $Json -Raw | ConvertFrom-Json
}

function Vp9Temel([int]$Cpu) { @('-c:v', 'libvpx-vp9', '-deadline', 'good', '-cpu-used', "$Cpu", '-row-mt', '1', '-pix_fmt', 'yuv420p', '-an') }

function IkiGecis([string]$Ref, [string]$Out, [int]$Cpu, [int]$K, [string]$Dizin) {
    $log = Join-Path $Dizin ("pass-" + [guid]::NewGuid().ToString('N'))
    $sw = [Diagnostics.Stopwatch]::StartNew()
    Kodla (@('-i', $Ref) + (Vp9Temel $Cpu) + @('-b:v', "${K}k", '-pass', '1', '-passlogfile', $log, '-f', 'null', '/dev/null'))
    Kodla (@('-i', $Ref) + (Vp9Temel $Cpu) + @('-b:v', "${K}k", '-pass', '2', '-passlogfile', $log, $Out))
    $sw.Stop()
    Remove-Item "$log*" -ErrorAction SilentlyContinue
    $sw.Elapsed.TotalSeconds
}

function CrfKodla([string]$Ref, [string]$Out, [int]$Crf) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    Kodla (@('-i', $Ref) + (Vp9Temel $UrunCpu) + @('-crf', "$Crf", '-b:v', '0', $Out))
    $sw.Stop()
    $sw.Elapsed.TotalSeconds
}

function Satir($Kesit, $Is, $HedefK, $Cpu, $Crf, $Sure, $M, $Sha) {
    [pscustomobject]@{
        kesit = $Kesit; is = $Is; hedefK = $HedefK; cpuUsed = $Cpu; crf = $Crf
        sureSn = [math]::Round($Sure, 2); bayt = $M.Bayt; kbps = [math]::Round($M.Kbps, 1)
        sapma = if ($HedefK) { [math]::Round($M.Kbps / $HedefK - 1, 4) } else { $null }
        vmafNeg = [math]::Round($M.VmafNegMean, 3); vmafNegP10 = [math]::Round($M.VmafNegP10, 3)
        xpsnr = if ($null -ne $M.Xpsnr) { [math]::Round($M.Xpsnr, 3) } else { $null }
        refSha256 = $Sha
    }
}

function Yaz($Satirlar, [string]$Yol) {
    ConvertTo-Json -InputObject @($Satirlar) -Depth 5 | Set-Content -Path $Yol -Encoding utf8
}

function CrfTahmin($Izgara, [double]$HedefK) {
    $noktalar = $Izgara | Sort-Object crf
    for ($i = 0; $i -lt $noktalar.Count - 1; $i++) {
        $a = $noktalar[$i]; $b = $noktalar[$i + 1]
        $la = [math]::Log($a.kbps); $lb = [math]::Log($b.kbps); $lh = [math]::Log($HedefK)
        if (($lh -le $la -and $lh -ge $lb) -or ($lh -ge $la -and $lh -le $lb)) {
            $t = if ($la -eq $lb) { 0 } else { ($lh - $la) / ($lb - $la) }
            return [pscustomobject]@{ crf = [int][math]::Round($a.crf + $t * ($b.crf - $a.crf)); sinirda = $false }
        }
    }
    $uc = if ($HedefK -gt ($noktalar | Measure-Object kbps -Maximum).Maximum) { $noktalar[0].crf } else { $noktalar[-1].crf }
    [pscustomobject]@{ crf = [int]$uc; sinirda = $true }
}

New-Item -ItemType Directory -Force -Path $Cikti | Out-Null
$isDizin = Join-Path $Cikti 'is'
New-Item -ItemType Directory -Force -Path $isDizin | Out-Null

if ($Is -in @('cpu', 'crf', 'inis')) {
    $k = KesitUret $Kesit $isDizin
    Write-Host "kesit $Kesit sha256=$($k.Sha256) bayt=$($k.Bayt)"
}

switch ($Is) {
    'cpu' {
        $satirlar = @()
        foreach ($c in $CpuAdimlari) {
            $out = Join-Path $isDizin "cpu$c.webm"
            $sure = IkiGecis $k.Yol $out $c $HedefK $isDizin
            $m = Olc $k.Yol $out (Join-Path $isDizin "cpu$c.json")
            $s = Satir $Kesit 'cpu' $HedefK $c $null $sure $m $k.Sha256
            Write-Host ($s | ConvertTo-Json -Compress)
            $satirlar += $s
            Remove-Item $out
        }
        Yaz $satirlar (Join-Path $Cikti "sonuc-cpu-$Kesit-$HedefK.json")
    }
    'crf' {
        $satirlar = @()
        foreach ($c in $CrfAdimlari) {
            $out = Join-Path $isDizin "crf$c.webm"
            $sure = CrfKodla $k.Yol $out $c
            $m = Olc $k.Yol $out (Join-Path $isDizin "crf$c.json")
            $s = Satir $Kesit 'crf' $null $UrunCpu $c $sure $m $k.Sha256
            Write-Host ($s | ConvertTo-Json -Compress)
            $satirlar += $s
            Remove-Item $out
        }
        Yaz $satirlar (Join-Path $Cikti "sonuc-crf-$Kesit.json")
    }
    'inis' {
        $digerleri = Get-ChildItem $Girdi -Recurse -Filter 'sonuc-crf-*.json' | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json } | Where-Object { $_.kesit -ne $Kesit }
        $kesitSayisi = @($digerleri | Select-Object -ExpandProperty kesit -Unique).Count
        if ($kesitSayisi -ne 2) { throw "olcek icin iki kesit gerekir, bulunan: $kesitSayisi" }
        $olcek = $CrfAdimlari | ForEach-Object {
            $c = $_
            $v = @($digerleri | Where-Object { $_.crf -eq $c })
            $log = ($v | ForEach-Object { [math]::Log($_.kbps) } | Measure-Object -Average).Average
            [pscustomobject]@{ crf = $c; kbps = [math]::Exp($log) }
        }
        $satirlar = @()
        foreach ($h in $Hedefler) {
            $tahmin = CrfTahmin $olcek $h
            $out = Join-Path $isDizin "inis$h.webm"
            $sure = CrfKodla $k.Yol $out $tahmin.crf
            $m = Olc $k.Yol $out (Join-Path $isDizin "inis$h.json")
            $s = Satir $Kesit 'inis' $h $UrunCpu $tahmin.crf $sure $m $k.Sha256
            $s | Add-Member -NotePropertyName olcekSinirda -NotePropertyValue $tahmin.sinirda
            $s | Add-Member -NotePropertyName olcekKesitleri -NotePropertyValue (@($digerleri | Select-Object -ExpandProperty kesit -Unique) -join ',')
            Write-Host ($s | ConvertTo-Json -Compress)
            $satirlar += $s
            Remove-Item $out
        }
        Yaz $satirlar (Join-Path $Cikti "sonuc-inis-$Kesit.json")
    }
    'ozet' {
        $hepsi = @(Get-ChildItem $Girdi -Recurse -Filter 'sonuc-*.json' | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json })
        $md = [Collections.Generic.List[string]]::new()
        $md.Add('# VP9 Olcumu Ozeti')
        $md.Add('')
        $md.Add("ffmpeg: $((& ffmpeg -hide_banner -version | Select-Object -First 1))")
        $md.Add("paketler: $((& dpkg-query -W -f '${Package} ${Version}; ' 'libvpx*' 'libvmaf*' 2>$null) -join '')")
        $md.Add("cekirdek: $([Environment]::ProcessorCount)")
        $md.Add('')
        $md.Add('## Kesit Sha256')
        foreach ($g in ($hepsi | Group-Object kesit)) {
            $shalar = @($g.Group | Select-Object -ExpandProperty refSha256 -Unique)
            $durum = if ($shalar.Count -eq 1) { 'tutuyor' } else { 'UYUSMUYOR' }
            $md.Add("- $($g.Name): $($shalar -join ' / ') ($durum)")
        }
        $hucreler = @()
        $md.Add('')
        $md.Add('## cpu-used')
        $md.Add('')
        $md.Add('| kesit | hedef | cpu | sure sn | kbps | sapma | VMAF-NEG | fark | kabul |')
        $md.Add('|---|---|---|---|---|---|---|---|---|')
        foreach ($kes in $Kesitler) {
            foreach ($h in $Hedefler) {
                $hucre = @($hepsi | Where-Object { $_.is -eq 'cpu' -and $_.kesit -eq $kes -and $_.hedefK -eq $h } | Sort-Object cpuUsed)
                if ($hucre.Count -eq 0) { $md.Add("| $kes | $h | eksik | | | | | | |"); continue }
                $taban = ($hucre | Where-Object { $_.cpuUsed -eq 0 }).vmafNeg
                $kmax = ($hucre | Measure-Object kbps -Maximum).Maximum
                $kmin = ($hucre | Measure-Object kbps -Minimum).Minimum
                $baytTuttu = ($kmax / $kmin - 1) -le 0.03
                $hucreler += [pscustomobject]@{ kesit = $kes; hedef = $h; baytTuttu = $baytTuttu; kbpsYayilim = [math]::Round($kmax / $kmin - 1, 4) }
                foreach ($r in $hucre) {
                    $fark = [math]::Round($r.vmafNeg - $taban, 3)
                    $kabul = if ($fark -ge -0.3) { 'evet' } else { 'hayir' }
                    $md.Add("| $kes | $h | $($r.cpuUsed) | $($r.sureSn) | $($r.kbps) | $([math]::Round($r.sapma * 100, 2))% | $($r.vmafNeg) | $fark | $kabul |")
                }
                if (-not $baytTuttu) { $md.Add("| $kes | $h | bayt tutmadi (yayilim $([math]::Round(($kmax / $kmin - 1) * 100, 2))%) | | | | | | |") }
            }
        }
        $md.Add('')
        $md.Add('### Adim Ozeti (yalniz bayti tutan hucreler)')
        $md.Add('')
        $md.Add('| cpu | toplam sure sn | en kotu fark | kabul hucre | en kotu sapma |')
        $md.Add('|---|---|---|---|---|')
        $adimOzeti = foreach ($c in $CpuAdimlari) {
            $gecerli = @($hucreler | Where-Object baytTuttu | ForEach-Object { "$($_.kesit)|$($_.hedef)" })
            $satir = @($hepsi | Where-Object { $_.is -eq 'cpu' -and $_.cpuUsed -eq $c -and $gecerli -contains "$($_.kesit)|$($_.hedefK)" })
            $farklar = foreach ($r in $satir) {
                $t = ($hepsi | Where-Object { $_.is -eq 'cpu' -and $_.kesit -eq $r.kesit -and $_.hedefK -eq $r.hedefK -and $_.cpuUsed -eq 0 }).vmafNeg
                $r.vmafNeg - $t
            }
            $o = [pscustomobject]@{
                cpuUsed = $c
                toplamSureSn = [math]::Round(($satir | Measure-Object sureSn -Sum).Sum, 1)
                enKotuFark = [math]::Round(($farklar | Measure-Object -Minimum).Minimum, 3)
                kabulHucre = @($farklar | Where-Object { $_ -ge -0.3 }).Count
                hucre = $satir.Count
                enKotuSapma = [math]::Round(($satir | ForEach-Object { [math]::Abs($_.sapma) } | Measure-Object -Maximum).Maximum, 4)
            }
            $md.Add("| $c | $($o.toplamSureSn) | $($o.enKotuFark) | $($o.kabulHucre)/$($o.hucre) | $([math]::Round($o.enKotuSapma * 100, 2))% |")
            $o
        }
        $gecenler = @($adimOzeti | Where-Object { $_.hucre -gt 0 -and $_.kabulHucre -eq $_.hucre })
        $urun = $gecenler | Sort-Object @{ Expression = 'toplamSureSn' }, @{ Expression = 'cpuUsed'; Descending = $true } | Select-Object -First 1
        $md.Add('')
        $md.Add("Olcute gore urun degeri: $(if ($urun) { $urun.cpuUsed } else { 'yok' }) (mevcut $UrunCpu)")
        $md.Add('')
        $md.Add('## CRF Egrisi')
        $md.Add('')
        $md.Add('| kesit | crf | kbps | MB | VMAF-NEG | sure sn |')
        $md.Add('|---|---|---|---|---|---|')
        foreach ($r in ($hepsi | Where-Object { $_.is -eq 'crf' } | Sort-Object kesit, crf)) {
            $md.Add("| $($r.kesit) | $($r.crf) | $($r.kbps) | $([math]::Round($r.bayt / 1MB, 3)) | $($r.vmafNeg) | $($r.sureSn) |")
        }
        $md.Add('')
        $md.Add('## CRF Inis Ve Iki Gecis Inis')
        $md.Add('')
        $md.Add('| kesit | hedef | CRF (olcek) | CRF kbps | CRF hata | iki gecis kbps | iki gecis hata |')
        $md.Add('|---|---|---|---|---|---|---|')
        $crfEnKotu = 0.0; $ikiEnKotu = 0.0; $inisSayisi = 0
        foreach ($kes in $Kesitler) {
            foreach ($h in $Hedefler) {
                $i = $hepsi | Where-Object { $_.is -eq 'inis' -and $_.kesit -eq $kes -and $_.hedefK -eq $h } | Select-Object -First 1
                $v = $hepsi | Where-Object { $_.is -eq 'cpu' -and $_.kesit -eq $kes -and $_.hedefK -eq $h -and $_.cpuUsed -eq $UrunCpu } | Select-Object -First 1
                if (-not $i -or -not $v) { $md.Add("| $kes | $h | eksik | | | | |"); continue }
                $inisSayisi++
                $crfEnKotu = [math]::Max($crfEnKotu, [math]::Abs($i.sapma))
                $ikiEnKotu = [math]::Max($ikiEnKotu, [math]::Abs($v.sapma))
                $sinir = if ($i.olcekSinirda) { ' (olcek disi)' } else { '' }
                $md.Add("| $kes | $h | $($i.crf)$sinir | $($i.kbps) | $([math]::Round($i.sapma * 100, 2))% | $($v.kbps) | $([math]::Round($v.sapma * 100, 2))% |")
            }
        }
        $crfAcilir = $inisSayisi -eq 6 -and $crfEnKotu -lt $ikiEnKotu
        $md.Add('')
        $md.Add("En kotu hata: CRF $([math]::Round($crfEnKotu * 100, 2))%, iki gecis $([math]::Round($ikiEnKotu * 100, 2))% ($inisSayisi/6 hucre)")
        $md.Add("Olcute gore CRF: $(if ($crfAcilir) { 'acilir' } else { 'acilmaz' })")
        $md -join "`n" | Set-Content -Path (Join-Path $Cikti 'ozet.md') -Encoding utf8
        $ham = [ordered]@{
            ffmpeg = (& ffmpeg -hide_banner -version | Select-Object -First 1)
            satirlar = $hepsi
            hucreler = $hucreler
            adimOzeti = $adimOzeti
            urunCpuUsed = if ($urun) { $urun.cpuUsed } else { $null }
            crfEnKotuHata = $crfEnKotu
            ikiGecisEnKotuHata = $ikiEnKotu
            crfAcilir = $crfAcilir
        }
        ConvertTo-Json -InputObject $ham -Depth 6 | Set-Content -Path (Join-Path $Cikti 'vp9-olcumu-ham.json') -Encoding utf8
        Get-Content (Join-Path $Cikti 'ozet.md')
    }
}

if (Test-Path $isDizin) { Remove-Item $isDizin -Recurse -Force }
