param(
    [Parameter(Mandatory)][ValidateSet('crf', 'cpu', 'ozet')][string]$Is,
    [string]$Kesit,
    [Parameter(Mandatory)][string]$Cikti,
    [string]$Girdi,
    [string]$Bench = $env:BENCH
)

$ErrorActionPreference = 'Stop'
if (-not $env:GITHUB_ACTIONS) {
    Write-Error 'Bu duzenek yalniz GitHub Actions kosucusunda calisir; kullanicinin makinesinde kodlama dongusu yasak.'
    exit 3
}

$Kesitler = @('karanlik', 'orta', 'hareketli')
$CrfAdimlari = @(20, 25, 30, 35, 40, 45, 50)
$HedefCrf = 30
$CpuAdimlari = @(1, 2, 4)
$UrunCpu = 4

function Kodla([string[]]$ArgList) {
    & ffmpeg.exe -hide_banner -nostdin -loglevel error -y @ArgList
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg cikis $LASTEXITCODE : $($ArgList -join ' ')" }
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
    Kodla (@('-i', $Ref) + (Vp9Temel $Cpu) + @('-b:v', "${K}k", '-pass', '1', '-passlogfile', $log, '-f', 'null', 'NUL'))
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

New-Item -ItemType Directory -Force -Path $Cikti | Out-Null
$isDizin = Join-Path $Cikti 'is'
New-Item -ItemType Directory -Force -Path $isDizin | Out-Null

switch ($Is) {
    'crf' {
        $girdiDosya = Get-ChildItem $Girdi -Recurse -Filter "kesit-$Kesit.mkv" | Select-Object -First 1
        if (-not $girdiDosya) { throw "kesit dosyasi yok: kesit-$Kesit.mkv" }
        $sha = (Get-FileHash $girdiDosya.FullName -Algorithm SHA256).Hash
        Write-Host "kesit $Kesit sha256=$sha kaynak=Sintel.2010.1080p.mkv"
        $satirlar = @()
        foreach ($c in $CrfAdimlari) {
            $out = Join-Path $isDizin "crf$c.webm"
            $sure = CrfKodla $girdiDosya.FullName $out $c
            $m = Olc $girdiDosya.FullName $out (Join-Path $isDizin "crf$c.json")
            $s = Satir $Kesit 'crf' $null $UrunCpu $c $sure $m $sha
            Write-Host ($s | ConvertTo-Json -Compress)
            $satirlar += $s
            Remove-Item $out
        }
        Yaz $satirlar (Join-Path $Cikti "sonuc-crf-$Kesit.json")
    }
    'cpu' {
        $girdiDosya = Get-ChildItem $Girdi -Recurse -Filter "kesit-$Kesit.mkv" | Select-Object -First 1
        if (-not $girdiDosya) { throw "kesit dosyasi yok: kesit-$Kesit.mkv" }
        $sha = (Get-FileHash $girdiDosya.FullName -Algorithm SHA256).Hash
        $crfSatirlari = Get-ChildItem $Girdi -Recurse -Filter "sonuc-crf-$Kesit.json" | Select-Object -First 1
        if (-not $crfSatirlari) { throw "sonuc-crf-$Kesit.json yok, crf isi once kosmali" }
        $crfVeri = Get-Content $crfSatirlari.FullName -Raw | ConvertFrom-Json
        $hedefSatir = $crfVeri | Where-Object { $_.crf -eq $HedefCrf } | Select-Object -First 1
        if (-not $hedefSatir) { throw "CRF $HedefCrf satiri yok: $Kesit" }
        $hedefK = [int][math]::Round($hedefSatir.kbps)
        Write-Host "kesit $Kesit hedefK=$hedefK (CRF $HedefCrf kbps=$($hedefSatir.kbps))"
        $satirlar = @()
        foreach ($c in $CpuAdimlari) {
            $out = Join-Path $isDizin "cpu$c.webm"
            $sure = IkiGecis $girdiDosya.FullName $out $c $hedefK $isDizin
            $m = Olc $girdiDosya.FullName $out (Join-Path $isDizin "cpu$c.json")
            $s = Satir $Kesit 'cpu' $hedefK $c $null $sure $m $sha
            Write-Host ($s | ConvertTo-Json -Compress)
            $satirlar += $s
            Remove-Item $out
        }
        Yaz $satirlar (Join-Path $Cikti "sonuc-cpu-$Kesit.json")
    }
    'ozet' {
        $hepsi = @(Get-ChildItem $Girdi -Recurse -Filter 'sonuc-*.json' | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json })
        $md = [Collections.Generic.List[string]]::new()
        $md.Add('# VP9 Gercek Kesitli Cpu-Used Ozeti')
        $md.Add('')
        $md.Add("ffmpeg: $((& ffmpeg.exe -hide_banner -version | Select-Object -First 1))")
        $md.Add("cekirdek: $([Environment]::ProcessorCount)")
        $md.Add('')
        $md.Add('## Kesit Sha256 (uc isin uzerinde)')
        foreach ($g in ($hepsi | Group-Object kesit)) {
            $shalar = @($g.Group | Select-Object -ExpandProperty refSha256 -Unique)
            $durum = if ($shalar.Count -eq 1) { 'tutuyor' } else { 'UYUSMUYOR' }
            $md.Add("- $($g.Name): $($shalar -join ' / ') ($durum)")
        }
        $md.Add('')
        $md.Add('## CRF Egrisi (cpu-used 4)')
        $md.Add('')
        $md.Add('| kesit | crf | kbps | MB | VMAF-NEG | sure sn |')
        $md.Add('|---|---|---|---|---|---|')
        foreach ($r in ($hepsi | Where-Object { $_.is -eq 'crf' } | Sort-Object kesit, crf)) {
            $md.Add("| $($r.kesit) | $($r.crf) | $($r.kbps) | $([math]::Round($r.bayt / 1MB, 3)) | $($r.vmafNeg) | $($r.sureSn) |")
        }
        $md.Add('')
        $md.Add('## cpu-used (hedef = kesitin CRF 30 kbps''i)')
        $md.Add('')
        $md.Add('| kesit | hedef kbps | cpu | sure sn | kbps | sapma | VMAF-NEG | fark (cpu1''e) | kabul |')
        $md.Add('|---|---|---|---|---|---|---|---|---|')
        $hucreler = @()
        foreach ($kes in $Kesitler) {
            $hucre = @($hepsi | Where-Object { $_.is -eq 'cpu' -and $_.kesit -eq $kes } | Sort-Object cpuUsed)
            if ($hucre.Count -eq 0) { $md.Add("| $kes | eksik | | | | | | | |"); continue }
            $taban = ($hucre | Where-Object { $_.cpuUsed -eq 1 }).vmafNeg
            $kmax = ($hucre | Measure-Object kbps -Maximum).Maximum
            $kmin = ($hucre | Measure-Object kbps -Minimum).Minimum
            $baytTuttu = ($kmax / $kmin - 1) -le 0.03
            $hucreler += [pscustomobject]@{ kesit = $kes; baytTuttu = $baytTuttu; kbpsYayilim = [math]::Round($kmax / $kmin - 1, 4) }
            foreach ($r in $hucre) {
                $fark = [math]::Round($r.vmafNeg - $taban, 3)
                $kabul = if ($fark -ge -0.3) { 'evet' } else { 'hayir' }
                $md.Add("| $kes | $($r.hedefK) | $($r.cpuUsed) | $($r.sureSn) | $($r.kbps) | $([math]::Round($r.sapma * 100, 2))% | $($r.vmafNeg) | $fark | $kabul |")
            }
            if (-not $baytTuttu) { $md.Add("| $kes | bayt tutmadi (yayilim $([math]::Round(($kmax / $kmin - 1) * 100, 2))%) | | | | | | | |") }
        }
        $gecerliKesitler = @($hucreler | Where-Object baytTuttu | Select-Object -ExpandProperty kesit)
        $cpu2Kabul = $true
        foreach ($kes in $gecerliKesitler) {
            $hucre = @($hepsi | Where-Object { $_.is -eq 'cpu' -and $_.kesit -eq $kes })
            $taban = ($hucre | Where-Object { $_.cpuUsed -eq 1 }).vmafNeg
            $c2 = ($hucre | Where-Object { $_.cpuUsed -eq 2 }).vmafNeg
            if (($c2 - $taban) -lt -0.3) { $cpu2Kabul = $false }
        }
        $hukumGecerli = $gecerliKesitler.Count -eq $Kesitler.Count
        $md.Add('')
        if (-not $hukumGecerli) {
            $md.Add("Hukum: gecersiz — yalniz $($gecerliKesitler.Count)/$($Kesitler.Count) kesit bayt tuttu, olcut butun kesitlerin bayt tutmasini sartliyor.")
        } elseif ($cpu2Kabul) {
            $md.Add('Olcute gore hukum: **onerilir — cpu-used 2**.')
        } else {
            $md.Add('Olcute gore hukum: **onerilmez — 1 kalir**.')
        }
        $md -join "`n" | Set-Content -Path (Join-Path $Cikti 'ozet.md') -Encoding utf8
        $ham = [ordered]@{
            ffmpeg = (& ffmpeg.exe -hide_banner -version | Select-Object -First 1)
            satirlar = $hepsi
            hucreler = $hucreler
            gecerliKesitler = $gecerliKesitler
            hukumGecerli = $hukumGecerli
            cpu2Kabul = $cpu2Kabul
        }
        ConvertTo-Json -InputObject $ham -Depth 6 | Set-Content -Path (Join-Path $Cikti 'vp9-gercek-kesit-ham.json') -Encoding utf8
        Get-Content (Join-Path $Cikti 'ozet.md')
    }
}

if (Test-Path $isDizin) { Remove-Item $isDizin -Recurse -Force }
