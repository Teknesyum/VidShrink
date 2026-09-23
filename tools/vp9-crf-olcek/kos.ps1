param(
    [Parameter(Mandatory)][ValidateSet('olc', 'ozet')][string]$Is,
    [string]$Kesit,
    [ValidateSet('x264', 'vp9a', 'vp9b')][string]$Grup,
    [Parameter(Mandatory)][string]$Cikti,
    [string]$Girdi,
    [string]$Bench = $env:BENCH,
    [double]$Saniye = 4
)

$ErrorActionPreference = 'Stop'
if (-not $env:GITHUB_ACTIONS) {
    Write-Error 'Bu duzenek yalniz GitHub Actions kosucusunda calisir; kullanicinin makinesinde kodlama dongusu yasak.'
    exit 3
}

$Inv = [Globalization.CultureInfo]::InvariantCulture
$Kesitler = @('karanlik', 'orta', 'hareketli')
$X264Crf = @(10, 16, 23, 30, 37, 45)
$Vp9Crf = @{ vp9a = @(10, 16, 20, 24, 28, 31, 34); vp9b = @(37, 40, 44, 48, 52, 56, 63) }
$X264Preset = 'slow'
$Vp9Cpu = 1

function Kodla([string[]]$ArgList) {
    & ffmpeg.exe -hide_banner -nostdin -loglevel error -y @ArgList
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg cikis $LASTEXITCODE : $($ArgList -join ' ')" }
}

function Olc([string]$Ref, [string]$Test, [string]$Json) {
    & dotnet $Bench measure-pair $Ref $Test --out $Json | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "measure-pair cikis $LASTEXITCODE : $Test" }
    Get-Content $Json -Raw | ConvertFrom-Json
}

function Satir($Kesit, $Kodek, $Crf, $Sure, $M, $Sha) {
    [pscustomobject]@{
        kesit = $Kesit; kodek = $Kodek; crf = $Crf
        sureSn = [math]::Round($Sure, 2); bayt = $M.Bayt; kbps = [math]::Round($M.Kbps, 1)
        vmafNeg = [math]::Round($M.VmafNegMean, 3); vmafNegP10 = [math]::Round($M.VmafNegP10, 3)
        refSha256 = $Sha
    }
}

function EsCrf($Noktalar, [double]$Hedef) {
    $s = @($Noktalar | Sort-Object crf)
    if ($Hedef -gt $s[0].vmafNeg) { return [pscustomobject]@{ crf = $null; kbps = $null; not = "< $($s[0].crf)" } }
    if ($Hedef -lt $s[-1].vmafNeg) { return [pscustomobject]@{ crf = $null; kbps = $null; not = "> $($s[-1].crf)" } }
    for ($i = 0; $i -lt $s.Count - 1; $i++) {
        $a = $s[$i]; $b = $s[$i + 1]
        $ust = [math]::Max($a.vmafNeg, $b.vmafNeg); $alt = [math]::Min($a.vmafNeg, $b.vmafNeg)
        if ($Hedef -le $ust -and $Hedef -ge $alt) {
            $t = if ($a.vmafNeg -eq $b.vmafNeg) { 0 } else { ($a.vmafNeg - $Hedef) / ($a.vmafNeg - $b.vmafNeg) }
            $crf = $a.crf + $t * ($b.crf - $a.crf)
            $kbps = [math]::Exp([math]::Log($a.kbps) + $t * ([math]::Log($b.kbps) - [math]::Log($a.kbps)))
            return [pscustomobject]@{ crf = [math]::Round($crf, 1); kbps = [math]::Round($kbps, 1); not = '' }
        }
    }
    [pscustomobject]@{ crf = $null; kbps = $null; not = 'bulunamadi' }
}

New-Item -ItemType Directory -Force -Path $Cikti | Out-Null
$isDizin = Join-Path $Cikti 'is'
New-Item -ItemType Directory -Force -Path $isDizin | Out-Null

switch ($Is) {
    'olc' {
        $kaynak = Get-ChildItem $Girdi -Recurse -Filter "kesit-$Kesit.mkv" | Select-Object -First 1
        if (-not $kaynak) { throw "kesit dosyasi yok: kesit-$Kesit.mkv" }
        $ref = Join-Path $isDizin "ref-$Kesit.mkv"
        Kodla @('-i', $kaynak.FullName, '-t', $Saniye.ToString($Inv), '-an', '-sn', '-map', '0:v:0', '-c:v', 'ffv1', '-pix_fmt', 'yuv420p', $ref)
        $sha = (Get-FileHash $kaynak.FullName -Algorithm SHA256).Hash
        Write-Host "kesit $Kesit sha256=$sha sure=${Saniye}sn grup=$Grup"
        $satirlar = @()
        $crfler = if ($Grup -eq 'x264') { $X264Crf } else { $Vp9Crf[$Grup] }
        foreach ($c in $crfler) {
            if ($Grup -eq 'x264') {
                $out = Join-Path $isDizin "x264-$c.mkv"
                $arg = @('-i', $ref, '-c:v', 'libx264', '-preset', $X264Preset, '-crf', "$c", '-pix_fmt', 'yuv420p', '-an', $out)
                $kodek = 'libx264'
            } else {
                $out = Join-Path $isDizin "vp9-$c.webm"
                $arg = @('-i', $ref, '-c:v', 'libvpx-vp9', '-deadline', 'good', '-cpu-used', "$Vp9Cpu", '-row-mt', '1', '-crf', "$c", '-b:v', '0', '-pix_fmt', 'yuv420p', '-an', $out)
                $kodek = 'libvpx-vp9'
            }
            $sw = [Diagnostics.Stopwatch]::StartNew()
            Kodla $arg
            $sw.Stop()
            $m = Olc $ref $out (Join-Path $isDizin "$Grup-$c.json")
            $s = Satir $Kesit $kodek $c $sw.Elapsed.TotalSeconds $m $sha
            Write-Host ($s | ConvertTo-Json -Compress)
            $satirlar += $s
            Remove-Item $out
        }
        ConvertTo-Json -InputObject @($satirlar) -Depth 5 | Set-Content -Path (Join-Path $Cikti "sonuc-$Kesit-$Grup.json") -Encoding utf8
    }
    'ozet' {
        $hepsi = @(Get-ChildItem $Girdi -Recurse -Filter 'sonuc-*.json' | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json })
        $md = [Collections.Generic.List[string]]::new()
        $md.Add('# VP9 CRF Olcegi')
        $md.Add('')
        $md.Add("ffmpeg: $((& ffmpeg.exe -hide_banner -version | Select-Object -First 1))")
        $md.Add("cekirdek: $([Environment]::ProcessorCount); kesit suresi: ${Saniye} sn; libx264 -preset $X264Preset; libvpx-vp9 -deadline good -cpu-used $Vp9Cpu -row-mt 1 -b:v 0")
        $md.Add('')
        $md.Add('## Esit VMAF-NEG Noktasi')
        $md.Add('')
        $md.Add('| kesit | x264 crf | x264 VMAF-NEG | x264 kbps | VP9 esdeger crf | VP9 kbps (ara) | VP9/x264 bayt |')
        $md.Add('|---|---|---|---|---|---|---|')
        $esler = @()
        foreach ($kes in $Kesitler) {
            $x = @($hepsi | Where-Object { $_.kesit -eq $kes -and $_.kodek -eq 'libx264' } | Sort-Object crf)
            $v = @($hepsi | Where-Object { $_.kesit -eq $kes -and $_.kodek -eq 'libvpx-vp9' } | Sort-Object crf)
            if ($x.Count -eq 0 -or $v.Count -lt 2) { $md.Add("| $kes | eksik (x264 $($x.Count), vp9 $($v.Count)) | | | | | |"); continue }
            foreach ($r in $x) {
                $e = EsCrf $v $r.vmafNeg
                $oran = if ($e.kbps) { [math]::Round($e.kbps / $r.kbps, 3) } else { '' }
                $crfMetin = if ($null -ne $e.crf) { "$($e.crf)" } else { $e.not }
                $md.Add("| $kes | $($r.crf) | $($r.vmafNeg) | $($r.kbps) | $crfMetin | $($e.kbps) | $oran |")
                $esler += [pscustomobject]@{ kesit = $kes; x264Crf = $r.crf; vmafNeg = $r.vmafNeg; x264Kbps = $r.kbps; vp9Crf = $e.crf; vp9Kbps = $e.kbps; not = $e.not }
            }
        }
        $md.Add('')
        $md.Add('## Kesit Ortalamasi')
        $md.Add('')
        $md.Add('| x264 crf | VP9 esdeger crf (kesitler) | ortalama |')
        $md.Add('|---|---|---|')
        foreach ($c in $X264Crf) {
            $g = @($esler | Where-Object { $_.x264Crf -eq $c })
            $sayilar = @($g | Where-Object { $null -ne $_.vp9Crf } | Select-Object -ExpandProperty vp9Crf)
            $ort = if ($sayilar.Count) { [math]::Round(($sayilar | Measure-Object -Average).Average, 1) } else { '' }
            $liste = ($g | ForEach-Object { if ($null -ne $_.vp9Crf) { "$($_.kesit) $($_.vp9Crf)" } else { "$($_.kesit) $($_.not)" } }) -join ', '
            $md.Add("| $c | $liste | $ort |")
        }
        $md.Add('')
        $md.Add('## Ham Egri')
        $md.Add('')
        $md.Add('| kesit | kodek | crf | kbps | MB | VMAF-NEG | P10 | sure sn |')
        $md.Add('|---|---|---|---|---|---|---|---|')
        foreach ($r in ($hepsi | Sort-Object kesit, kodek, crf)) {
            $md.Add("| $($r.kesit) | $($r.kodek) | $($r.crf) | $($r.kbps) | $([math]::Round($r.bayt / 1MB, 3)) | $($r.vmafNeg) | $($r.vmafNegP10) | $($r.sureSn) |")
        }
        $md.Add('')
        $md.Add('## Kesit Sha256')
        foreach ($g in ($hepsi | Group-Object kesit)) {
            $shalar = @($g.Group | Select-Object -ExpandProperty refSha256 -Unique)
            $durum = if ($shalar.Count -eq 1) { 'tutuyor' } else { 'UYUSMUYOR' }
            $md.Add("- $($g.Name): $($shalar -join ' / ') ($durum)")
        }
        $md -join "`n" | Set-Content -Path (Join-Path $Cikti 'ozet.md') -Encoding utf8
        ConvertTo-Json -InputObject ([ordered]@{ satirlar = $hepsi; esler = $esler }) -Depth 6 | Set-Content -Path (Join-Path $Cikti 'vp9-crf-olcek-ham.json') -Encoding utf8
        Get-Content (Join-Path $Cikti 'ozet.md')
    }
}

if (Test-Path $isDizin) { Remove-Item $isDizin -Recurse -Force }
