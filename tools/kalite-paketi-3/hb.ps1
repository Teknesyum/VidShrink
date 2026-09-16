param(
    [Parameter(Mandatory)][ValidateSet('handbrake', 'dusuk', 'social', 'bantlasma', 'turbo', 'hdr', 'vt', 'ekranbant')][string]$Is,
    [Parameter(Mandatory)][string]$Cikti,
    [Parameter(Mandatory)][string]$Bench,
    [string]$Kesit = '',
    [string]$Kesitler = 'karanlik,parlak,hareketli,ekran',
    [string]$HandBrake = '',
    [string]$BenchE1 = '',
    [string]$BenchTurbo = '',
    [ValidateSet('bench', 'cli')][string]$UrunYolu = 'bench',
    [string]$Cli = '',
    [string]$CliSablon = '{girdi} {mb} --out {klasor} --speed quality --no-measure',
    [string]$Kbitler = '600,2000',
    [string]$DusukKbitler = '300,600',
    [string]$BantKbitler = '100,300,1200',
    [string]$HdrKbitler = '1000,3000',
    [string]$VtKbitler = '2000,5500',
    [string]$HdrKaynak = '',
    [string]$EkranUzun = '',
    [int]$UzunSure = 120,
    [string]$Hedefler = '10,25,50,100',
    [string]$Onayarlar = ''
)

$ErrorActionPreference = 'Stop'
if (-not $env:GITHUB_ACTIONS) {
    Write-Error 'Bu duzenek yalniz GitHub Actions kosucusunda calisir; kullanicinin makinesinde kodlama dongusu yasak.'
    exit 3
}

$Inv = [Globalization.CultureInfo]::InvariantCulture
New-Item -ItemType Directory -Force $Cikti | Out-Null
$Satirlar = [Collections.Generic.List[object]]::new()
$JsonAdi = if ($Kesit) { "$Is-$Kesit.json" } else { "$Is.json" }

function Ff([string[]]$Argumanlar) {
    & ffmpeg -hide_banner -nostdin -loglevel error -y @Argumanlar
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg basarisiz: $($Argumanlar -join ' ')" }
}

function Sayi([string]$s) { if ([string]::IsNullOrWhiteSpace($s) -or $s -eq 'N/A') { $null } else { [double]::Parse($s, $Inv) } }

function Probe([string]$Yol) {
    $j = & ffprobe -v error -select_streams v:0 -show_entries stream=codec_name,width,height,r_frame_rate,pix_fmt,color_transfer,color_primaries,color_space:format=duration -of json $Yol | ConvertFrom-Json
    $s = $j.streams[0]
    $p = $s.r_frame_rate.Split('/')
    [pscustomobject]@{
        W = [int]$s.width; H = [int]$s.height
        Fps = [double]::Parse($p[0], $Inv) / [double]::Parse($p[1], $Inv)
        FpsMetin = $s.r_frame_rate
        Sure = [double]::Parse($j.format.duration, $Inv)
        Pix = $s.pix_fmt; Kodek = $s.codec_name
        Transfer = $s.color_transfer; Primaries = $s.color_primaries; Matris = $s.color_space
    }
}

function Kbps([string]$Yol) {
    $b = Probe $Yol
    [math]::Round((Get-Item $Yol).Length * 8 / 1000 / $b.Sure, 1)
}

function Kacis([string]$Yol) { "'" + $Yol.Replace('\', '/').Replace(':', '\:') + "'" }

function Yaz {
    ConvertTo-Json -Depth 6 -InputObject @($Satirlar) | Set-Content (Join-Path $Cikti $JsonAdi)
}

function EkOlcu([string]$Ref, [string]$Test, [string]$Fps, [switch]$Pq) {
    $rb = Probe $Ref
    $tb = Probe $Test
    $log = Join-Path $Cikti ('vmaf-' + [guid]::NewGuid().ToString('N') + '.json')
    $zincir = "scale=w=$($rb.W):h=$($rb.H):flags=lanczos,format=$($rb.Pix)"
    if ($Fps) { $zincir = "fps=$Fps,$zincir" }
    $cambi = "name=cambi\:enc_width=$($tb.W)\:enc_height=$($tb.H)"
    if ($Pq) { $cambi += '\:eotf=pq' }
    $sonuc = [ordered]@{ ssim = $null; cambi = $null; cambi_anahtar = $null; ek_vmafneg_ort = $null; ek_vmafneg_harm = $null; xpsnr_ek = $null; ek_hata = $null }
    try {
        $g = "[0:v]$zincir,settb=AVTB,setpts=N[t];[1:v]format=$($rb.Pix),settb=AVTB,setpts=N[r];[t][r]libvmaf=model=version=vmaf_v0.6.1neg:feature='name=float_ssim|$cambi':log_fmt=json:log_path=$(Kacis $log):n_threads=4"
        $cikis = & ffmpeg -hide_banner -nostdin -i $Test -i $Ref -lavfi $g -f null - 2>&1 | Out-String
        if (-not (Test-Path $log) -and $Pq) {
            $g = $g.Replace('\:eotf=pq', '')
            $cikis = & ffmpeg -hide_banner -nostdin -i $Test -i $Ref -lavfi $g -f null - 2>&1 | Out-String
            $sonuc.ek_hata = 'cambi eotf=pq kabul edilmedi, bt1886 ile olculdu'
        }
        if (-not (Test-Path $log)) { throw "libvmaf gunlugu yok: $($cikis.Substring([math]::Max(0, $cikis.Length - 600)))" }
        $p = (Get-Content $log -Raw | ConvertFrom-Json).pooled_metrics
        $c = $p.PSObject.Properties | Where-Object { $_.Name -like 'cambi*' } | Select-Object -First 1
        if ($c) { $sonuc.cambi = [math]::Round($c.Value.mean, 4); $sonuc.cambi_anahtar = $c.Name }
        if ($p.float_ssim) { $sonuc.ssim = [math]::Round($p.float_ssim.mean, 5) }
        if ($p.vmaf) { $sonuc.ek_vmafneg_ort = [math]::Round($p.vmaf.mean, 4); $sonuc.ek_vmafneg_harm = [math]::Round($p.vmaf.harmonic_mean, 4) }
        Remove-Item $log -ErrorAction SilentlyContinue
        $gx = "[0:v]$zincir,settb=AVTB,setpts=N[t];[1:v]format=$($rb.Pix),settb=AVTB,setpts=N[r];[t][r]xpsnr"
        $cx = & ffmpeg -hide_banner -nostdin -i $Test -i $Ref -lavfi $gx -f null - 2>&1 | Out-String
        if ($cx -match 'XPSNR\s+y:\s*([\d.]+)\s*u:\s*([\d.]+)\s*v:\s*([\d.]+)') {
            $sonuc.xpsnr_ek = [math]::Round((4 * (Sayi $Matches[1]) + (Sayi $Matches[2]) + (Sayi $Matches[3])) / 6, 4)
        }
    } catch {
        $sonuc.ek_hata = $_.Exception.Message
    }
    $sonuc
}

function Olc([string]$Ref, [string]$Test, [string]$Fps = '', [switch]$Pq, [switch]$EkYok) {
    $json = [IO.Path]::ChangeExtension($Test, '.olcu.json')
    $o = $null
    $hata = $null
    try {
        $a = @('measure-pair', $Ref, $Test, '--out', $json)
        if ($Fps) { $a += @('--fps', $Fps) }
        & dotnet $Bench @a *>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "measure-pair cikis $LASTEXITCODE" }
        $o = Get-Content $json -Raw | ConvertFrom-Json
    } catch { $hata = $_.Exception.Message }
    $s = [ordered]@{
        bayt = (Get-Item $Test).Length
        mb = [math]::Round((Get-Item $Test).Length / 1MB, 4)
        kbps = (Kbps $Test)
        geometri_cikti = "$((Probe $Test).W)x$((Probe $Test).H)"
        vmafneg_ort = $o.VmafNegMean; vmafneg_harm = $o.VmafNegHarmonic; vmafneg_p10 = $o.VmafNegP10
        xpsnr = $o.Xpsnr
        karanlik_psnr = $o.Karanlik.KaranlikPsnr; karanlik_ton = $o.Karanlik.TonOrani
        olcu_hata = $hata
    }
    if (-not $EkYok) {
        $e = EkOlcu $Ref $Test $Fps -Pq:$Pq
        foreach ($k in $e.Keys) { $s[$k] = $e[$k] }
        if ($null -eq $s.vmafneg_ort) { $s.vmafneg_ort = $e.ek_vmafneg_ort; $s.vmafneg_harm = $e.ek_vmafneg_harm }
        if ($null -eq $s.xpsnr) { $s.xpsnr = $e.xpsnr_ek }
    }
    $s
}

function Ekle($Ortak, $Olcu, $Ek) {
    $s = [ordered]@{}
    foreach ($k in $Ortak.Keys) { $s[$k] = $Ortak[$k] }
    if ($Olcu) { foreach ($k in $Olcu.Keys) { $s[$k] = $Olcu[$k] } }
    if ($Ek) { foreach ($k in $Ek.Keys) { $s[$k] = $Ek[$k] } }
    $Satirlar.Add([pscustomobject]$s)
    Yaz
}

function Urun([string]$Girdi, [double]$Mb, [string]$Ad, [string[]]$Ek = @(), [string]$BenchYolu = '') {
    if (-not $BenchYolu) { $BenchYolu = $Bench }
    $klasor = Join-Path $Cikti $Ad
    $log = Join-Path $Cikti "$Ad.log"
    $mbMetin = $Mb.ToString('0.####', $Inv)
    $sure = [Diagnostics.Stopwatch]::StartNew()
    if ($UrunYolu -eq 'cli' -and $BenchYolu -eq $Bench) {
        if (-not $Cli) { throw 'UrunYolu=cli icin -Cli gerekli.' }
        $cliArg = @($CliSablon.Split(' ', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Replace('{girdi}', $Girdi).Replace('{mb}', $mbMetin).Replace('{klasor}', $klasor) }) + $Ek
        if ($Cli.EndsWith('.dll')) { & dotnet $Cli @cliArg *>&1 | Out-File $log } else { & $Cli @cliArg *>&1 | Out-File $log }
    } else {
        & dotnet $BenchYolu shrink $Girdi $mbMetin --out $klasor --speed quality --no-measure @Ek *>&1 | Out-File $log
    }
    $sure.Stop()
    if ($LASTEXITCODE -ne 0) { throw "urun yolu basarisiz ($UrunYolu): $Ad" }
    $metin = Get-Content $log
    $komut = $metin | Where-Object { $_ -like 'komut:*' } | Select-Object -Last 1
    $denemeler = @(foreach ($l in $metin) {
        if ($l -match '^\s*deneme (\d+): (.+), (\S+), (\d+)k, hedeflenen ([\d.]+) MB, cikan ([\d.]+) MB') {
            [pscustomobject]@{ no = [int]$Matches[1]; dal = $Matches[2]; kbit = [int]$Matches[4]; hedeflenen_mb = (Sayi $Matches[5]); cikan_mb = (Sayi $Matches[6]) }
        }
    })
    $mp4 = Get-ChildItem $klasor -Filter '*.mp4' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $mp4) { throw "urun ciktisi yok: $Ad" }
    $r = $null
    $rj = Join-Path $klasor 'results.json'
    if (Test-Path $rj) { $r = @(Get-Content $rj -Raw | ConvertFrom-Json)[0] }
    [pscustomobject]@{
        Dosya = $mp4.FullName
        ToplamSn = [math]::Round($sure.Elapsed.TotalSeconds, 1)
        KodlamaSn = if ($r) { [math]::Round($r.EncodeSeconds, 1) } else { $null }
        Kodlayici = if ($r) { $r.Codec } else { $null }
        Geometri = if ($r) { "$($r.Width)x$($r.Height)@$($r.Fps)" } else { $null }
        Deneme = if ($r) { $r.Attempts } else { $null }
        Bantta = if ($r) { $r.InBand } else { $null }
        Tasma = if ($r) { $r.OverTarget } else { $null }
        Denemeler = $denemeler
        Komut = $komut
    }
}

function UrunOrtak($u, [double]$HedefMb) {
    [ordered]@{
        urun_yolu = $UrunYolu; hedef_mb = $HedefMb; kodlayici = $u.Kodlayici; geometri = $u.Geometri; kodlama_sn = $u.KodlamaSn; toplam_sn = $u.ToplamSn
        deneme = $u.Deneme; bantta = $u.Bantta; tasma = $u.Tasma
        dallar = (($u.Denemeler | ForEach-Object { "$($_.no):$($_.dal):$($_.kbit)k:$($_.hedeflenen_mb)->$($_.cikan_mb)" }) -join ' | ')
        komut = $u.Komut
    }
}

function HbKodla([string]$Girdi, [string]$Cikis, [string[]]$Arglar) {
    $log = Join-Path $Cikti 'handbrake.log'
    "=== $Cikis :: $($Arglar -join ' ')" | Out-File -Append $log
    $sure = [Diagnostics.Stopwatch]::StartNew()
    & $HandBrake -i $Girdi -o $Cikis @Arglar *>&1 | Out-File -Append $log
    $sure.Stop()
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Cikis)) { throw "HandBrakeCLI basarisiz: $Cikis" }
    [math]::Round($sure.Elapsed.TotalSeconds, 1)
}

function HbEsBayt([string]$Girdi, [string]$Cikis, [double]$HedefKbps, [string[]]$Arglar) {
    $b = [int][math]::Round($HedefKbps)
    $izler = @()
    for ($i = 1; $i -le 3; $i++) {
        $sn = HbKodla $Girdi $Cikis ($Arglar + @('-b', "$b"))
        $kbps = Kbps $Cikis
        $sapma = $kbps / $HedefKbps - 1
        $izler += "${b}k->$kbps"
        if ([math]::Abs($sapma) -le 0.02 -or $i -eq 3) {
            return [pscustomobject]@{ Sn = $sn; Kbps = $kbps; B = $b; Deneme = $i; Sapma = [math]::Round($sapma * 100, 2); Iz = ($izler -join ' | ') }
        }
        $b = [int][math]::Round($b * $HedefKbps / $kbps)
    }
}

function HbOrtak($h, [double]$HedefKbps) {
    [ordered]@{ hb_b = $h.B; hb_deneme = $h.Deneme; hb_iz = $h.Iz; hedef_kbps = $HedefKbps; bayt_sapma_yuzde = $h.Sapma; es_bayt = ([math]::Abs($h.Sapma) -le 2); kodlama_sn = $h.Sn; toplam_sn = $h.Sn }
}

function Dene([string]$DKesit, [string]$DKol, $DKbit, [scriptblock]$Blok) {
    try { & $Blok } catch {
        Write-Warning "$DKesit $DKol $DKbit : $($_.Exception.Message)"
        Ekle ([ordered]@{ is = $script:Is; kesit = $DKesit; kol = $DKol; istenen_kbit = $DKbit; hata = $_.Exception.Message }) $null $null
    }
}

function HbTemel($b) {
    @('-Z', 'H.265 MKV 1080p30', '-e', 'x265', '--encoder-preset', 'slow', '--multi-pass', '--turbo', '-a', 'none', '--crop-mode', 'none', '--width', "$($b.W)", '--height', "$($b.H)", '-r', $b.Fps.ToString('0.###', $Inv), '--cfr')
}

function KaynakSatiri([string]$Girdi, [string]$Etiket, [switch]$Pq) {
    $e = EkOlcu $Girdi $Girdi '' -Pq:$Pq
    Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = $Etiket; istenen_kbit = $null }) $null $e
}

function Kiyas {
    $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    $b = Probe $girdi
    KaynakSatiri $girdi 'negatif-kaynak-kendisi'
    foreach ($kbit in @($Kbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
        $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
        $urunKbps = $null
        Dene $Kesit 'urun-otomatik' $kbit {
            $u = Urun $girdi $mb "hb-$Kesit-$kbit-urun"
            $o = Olc $girdi $u.Dosya $b.FpsMetin
            $script:urunKbps = $o.kbps
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'urun-otomatik'; istenen_kbit = $kbit }) $o (UrunOrtak $u $mb)
            Remove-Item $u.Dosya
        }
        if (-not $script:urunKbps) { continue }
        $hk = $script:urunKbps
        Dene $Kesit 'handbrake' $kbit {
            $c = Join-Path $Cikti "hb-$Kesit-$kbit-handbrake.mkv"
            $h = HbEsBayt $girdi $c $hk (HbTemel $b)
            $o = Olc $girdi $c $b.FpsMetin
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'handbrake'; istenen_kbit = $kbit; kodlayici = 'HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo' }) $o (HbOrtak $h $hk)
            Remove-Item $c
        }
        Dene $Kesit 'negatif-handbrake-yarim-bit' $kbit {
            $c = Join-Path $Cikti "hb-$Kesit-$kbit-negatif.mkv"
            $sn = HbKodla $girdi $c ((HbTemel $b) + @('-b', "$([int][math]::Round($hk / 2))"))
            $o = Olc $girdi $c $b.FpsMetin
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'negatif-handbrake-yarim-bit'; istenen_kbit = $kbit; kodlayici = 'HandBrakeCLI 1.11.2 x265 slow'; kodlama_sn = $sn }) $o $null
            Remove-Item $c
        }
        $script:urunKbps = $null
    }
}

function Dusuk {
    $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    $b = Probe $girdi
    foreach ($kbit in @($DusukKbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
        $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
        $script:urunKbps = $null
        foreach ($kol in @('urun-otomatik', 'urun-dusurme-kapali')) {
            Dene $Kesit $kol $kbit {
                $ek = if ($kol -eq 'urun-dusurme-kapali') { @('--no-resolution-drop', '--no-fps-drop') } else { @() }
                $u = Urun $girdi $mb "dusuk-$Kesit-$kbit-$kol" $ek
                $o = Olc $girdi $u.Dosya $b.FpsMetin
                if ($kol -eq 'urun-otomatik') { $script:urunKbps = $o.kbps }
                Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = $kol; istenen_kbit = $kbit }) $o (UrunOrtak $u $mb)
                Remove-Item $u.Dosya
            }
        }
        if (-not $script:urunKbps) { continue }
        $hk = $script:urunKbps
        Dene $Kesit 'handbrake-1080p' $kbit {
            $c = Join-Path $Cikti "dusuk-$Kesit-$kbit-handbrake.mkv"
            $h = HbEsBayt $girdi $c $hk (HbTemel $b)
            $o = Olc $girdi $c $b.FpsMetin
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'handbrake-1080p'; istenen_kbit = $kbit; kodlayici = 'HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo'; geometri = "$($b.W)x$($b.H)" }) $o (HbOrtak $h $hk)
            Remove-Item $c
        }
        Dene $Kesit 'negatif-handbrake-yarim-bit' $kbit {
            $c = Join-Path $Cikti "dusuk-$Kesit-$kbit-negatif.mkv"
            $sn = HbKodla $girdi $c ((HbTemel $b) + @('-b', "$([int][math]::Round($hk / 2))"))
            $o = Olc $girdi $c $b.FpsMetin
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'negatif-handbrake-yarim-bit'; istenen_kbit = $kbit; kodlama_sn = $sn }) $o $null
            Remove-Item $c
        }
    }
}

function Social {
    $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    $b = Probe $girdi
    $liste = & $HandBrake --preset-list 2>&1 | Out-String
    $liste | Set-Content (Join-Path $Cikti 'handbrake-preset-list.txt')
    $hbOnayar = [ordered]@{
        'Social 25 MB 30 Seconds 1080p60' = 30; 'Social 25 MB 1 Minute 720p60' = 60; 'Social 25 MB 2 Minutes 540p60' = 120; 'Social 25 MB 5 Minutes 360p60' = 300
        'Social 10 MB 30 Seconds 720p60' = 30; 'Social 10 MB 1 Minute 540p60' = 60; 'Social 10 MB 2 Minutes 360p60' = 120
    }
    foreach ($ad in @($hbOnayar.Keys | Where-Object { -not $Onayarlar -or ($Onayarlar.Split(',') -contains $_) })) {
        $kisa = ($ad -replace '[^A-Za-z0-9]+', '-').ToLowerInvariant()
        $script:hbMb = $null
        Dene $Kesit "hb:$ad" $null {
            if ($liste -notmatch [regex]::Escape($ad)) { throw "on ayar listede yok: $ad" }
            $c = Join-Path $Cikti "social-$Kesit-$kisa.mp4"
            $sn = HbKodla $girdi $c @('-Z', $ad, '-a', 'none')
            $o = Olc $girdi $c $b.FpsMetin
            $script:hbMb = (Get-Item $c).Length / 1MB
            $pb = [int]([regex]::Match($ad, '(\d+) MB').Groups[1].Value)
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'handbrake'; onayar = $ad; istenen_kbit = $null; kodlama_sn = $sn; toplam_sn = $sn; onayar_mb = $pb; onayar_sn = $hbOnayar[$ad] }) $o $null
            Remove-Item $c
        }
        if (-not $script:hbMb) { continue }
        $hedef = [math]::Round($script:hbMb, 4)
        foreach ($kol in @('urun-otomatik', 'urun-dusurme-kapali')) {
            Dene $Kesit $kol $null {
                $ek = if ($kol -eq 'urun-dusurme-kapali') { @('--no-resolution-drop', '--no-fps-drop') } else { @() }
                $u = Urun $girdi $hedef "social-$Kesit-$kisa-$kol" $ek
                $o = Olc $girdi $u.Dosya $b.FpsMetin
                Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = $kol; onayar = $ad; istenen_kbit = $null }) $o (UrunOrtak $u $hedef)
                Remove-Item $u.Dosya
            }
        }
    }
}

function Rampa {
    $temiz = Join-Path $Cikti 'rampa-temiz.mkv'
    $girdi = Join-Path $Cikti 'kesit-rampa.mkv'
    $kaynak = 'gradients=s=1920x1080:c0=0x000000:c1=0x383838:x0=0:y0=540:x1=1919:y1=540:speed=0:r=24:d=10,format=yuv420p'
    Ff @('-f', 'lavfi', '-i', $kaynak, '-c:v', 'ffv1', '-pix_fmt', 'yuv420p', $temiz)
    Ff @('-i', $temiz, '-vf', 'noise=alls=2:allf=t', '-c:v', 'ffv1', '-pix_fmt', 'yuv420p', $girdi)
    Ff @('-i', $temiz, '-vf', 'lutyuv=y=bitand(val\,252)', '-c:v', 'ffv1', '-pix_fmt', 'yuv420p', (Join-Path $Cikti 'rampa-temiz-6bit.mkv'))
    $girdi
}

function Bantlasma {
    if (-not $BenchE1) { throw 'bantlasma icin -BenchE1 gerekli.' }
    if ($Kesit -eq 'rampa') {
        $girdi = Rampa
        $temiz = Join-Path $Cikti 'rampa-temiz.mkv'
        Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'negatif-rampa-temiz-kendisi' }) $null (EkOlcu $temiz $temiz '')
        Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'negatif-rampa-temiz-6bit' }) $null (EkOlcu $temiz (Join-Path $Cikti 'rampa-temiz-6bit.mkv') '')
    } else {
        $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    }
    $b = Probe $girdi
    KaynakSatiri $girdi 'negatif-kaynak-kendisi'
    $alti = Join-Path $Cikti "kesit-$Kesit-6bit.mkv"
    Ff @('-i', $girdi, '-vf', 'lutyuv=y=bitand(val\,252)', '-c:v', 'ffv1', '-pix_fmt', 'yuv420p', $alti)
    Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'negatif-kaynak-6bit' }) $null (EkOlcu $girdi $alti '')
    $kollar = [ordered]@{ 'e0' = $Bench; 'e1' = $BenchE1 }
    foreach ($kbit in @($BantKbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
        $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
        foreach ($kol in $kollar.Keys) {
            Dene $Kesit $kol $kbit {
                $u = Urun $girdi $mb "bant-$Kesit-$kbit-$kol" @('--force-codec', 'libsvtav1', '--no-resolution-drop', '--no-fps-drop') $kollar[$kol]
                $o = Olc $girdi $u.Dosya $b.FpsMetin
                Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = $kol; istenen_kbit = $kbit }) $o (UrunOrtak $u $mb)
                Remove-Item $u.Dosya
            }
        }
    }
}

function Turbo {
    if (-not $BenchTurbo) { throw 'turbo icin -BenchTurbo gerekli.' }
    $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    $b = Probe $girdi
    foreach ($kbit in @($Kbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
        $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
        $script:urunKbps = $null
        foreach ($kol in @('urun-x265', 'urun-x265-turbo')) {
            Dene $Kesit $kol $kbit {
                $by = if ($kol -eq 'urun-x265-turbo') { $BenchTurbo } else { $Bench }
                $u = Urun $girdi $mb "turbo-$Kesit-$kbit-$kol" @('--force-codec', 'libx265', '--no-resolution-drop', '--no-fps-drop') $by
                $o = Olc $girdi $u.Dosya $b.FpsMetin -EkYok
                if ($kol -eq 'urun-x265') { $script:urunKbps = $o.kbps }
                Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = $kol; istenen_kbit = $kbit }) $o (UrunOrtak $u $mb)
                Remove-Item $u.Dosya
            }
        }
        if (-not $script:urunKbps) { continue }
        $hk = $script:urunKbps
        foreach ($kol in @('handbrake-x265', 'handbrake-x265-turbo')) {
            Dene $Kesit $kol $kbit {
                $a = @(HbTemel $b | Where-Object { $_ -ne '--turbo' })
                if ($kol -eq 'handbrake-x265-turbo') { $a += '--turbo' }
                $c = Join-Path $Cikti "turbo-$Kesit-$kbit-$kol.mkv"
                $h = HbEsBayt $girdi $c $hk $a
                $o = Olc $girdi $c $b.FpsMetin -EkYok
                Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = $kol; istenen_kbit = $kbit; kodlayici = 'HandBrakeCLI 1.11.2 x265 slow 2 gecis' }) $o (HbOrtak $h $hk)
                Remove-Item $c
            }
        }
    }
}

function YanVeri([string]$Yol) {
    $s = (& ffprobe -v error -select_streams v:0 -show_streams -of json $Yol | ConvertFrom-Json).streams[0]
    $f = (& ffprobe -v error -select_streams v:0 -show_frames -read_intervals '%+#1' -of json $Yol | ConvertFrom-Json).frames | Select-Object -First 1
    $liste = @()
    if ($s.side_data_list) { $liste += $s.side_data_list }
    if ($f.side_data_list) { $liste += $f.side_data_list }
    $md = $liste | Where-Object { $_.side_data_type -eq 'Mastering display metadata' } | Select-Object -First 1
    $cll = $liste | Where-Object { $_.side_data_type -eq 'Content light level metadata' } | Select-Object -First 1
    $mdMetin = if ($md) { "R($($md.red_x),$($md.red_y)) G($($md.green_x),$($md.green_y)) B($($md.blue_x),$($md.blue_y)) WP($($md.white_point_x),$($md.white_point_y)) L($($md.max_luminance),$($md.min_luminance))" } else { '' }
    $cllMetin = if ($cll) { "$($cll.max_content),$($cll.max_average)" } else { '' }
    [ordered]@{
        pix_fmt = $s.pix_fmt; color_transfer = $s.color_transfer; color_primaries = $s.color_primaries; color_space = $s.color_space
        mastering = $mdMetin; content_light = $cllMetin
        yan_veri_turleri = (($liste | ForEach-Object { $_.side_data_type }) -join '; ')
    }
}

function Hdr {
    if (-not $HdrKaynak) { throw 'hdr icin -HdrKaynak gerekli.' }
    $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    $x265 = 'lossless=1:hdr10=1:repeat-headers=1:colorprim=bt2020:transfer=smpte2084:colormatrix=bt2020nc:range=limited:master-display=G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,50):max-cll=1000,400'
    $ham = Join-Path $Cikti "kesit-$Kesit-ham.mkv"
    Ff @('-i', $HdrKaynak, '-an', '-c:v', 'libx265', '-preset', 'ultrafast', '-x265-params', $x265, '-pix_fmt', 'yuv420p10le', '-color_primaries', 'bt2020', '-color_trc', 'smpte2084', '-colorspace', 'bt2020nc', '-color_range', 'tv', $ham)
    Ff @('-i', $ham, '-c', 'copy', '-color_primaries', 'bt2020', '-color_trc', 'smpte2084', '-colorspace', 'bt2020nc', '-color_range', 'tv', $girdi)
    Remove-Item $ham
    $b = Probe $girdi
    $kaynakYan = YanVeri $girdi
    Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'kaynak-ara'; x265_params = $x265 }) $kaynakYan (EkOlcu $girdi $girdi '' -Pq)
    foreach ($kbit in @($HdrKbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
        $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
        $script:urunKbps = $null
        Dene $Kesit 'urun-otomatik' $kbit {
            $u = Urun $girdi $mb "hdr-$Kesit-$kbit-urun"
            $y = YanVeri $u.Dosya
            $o = Olc $girdi $u.Dosya $b.FpsMetin -Pq
            $script:urunKbps = $o.kbps
            $ek = UrunOrtak $u $mb
            foreach ($k in $y.Keys) { $ek[$k] = $y[$k] }
            $ek['yan_veri_es'] = ($y.mastering -eq $kaynakYan.mastering -and $y.content_light -eq $kaynakYan.content_light)
            $ek['pq_korundu'] = ($y.color_transfer -eq 'smpte2084')
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'urun-otomatik'; istenen_kbit = $kbit }) $o $ek
            Remove-Item $u.Dosya
        }
        if (-not $script:urunKbps) { continue }
        $hk = $script:urunKbps
        $hbArg = @('-e', 'x265_10bit', '--encoder-preset', 'slow', '--multi-pass', '--turbo', '-a', 'none', '--crop-mode', 'none', '--width', "$($b.W)", '--height', "$($b.H)", '-r', $b.Fps.ToString('0.###', $Inv), '--cfr', '-f', 'av_mkv')
        Dene $Kesit 'handbrake' $kbit {
            $c = Join-Path $Cikti "hdr-$Kesit-$kbit-handbrake.mkv"
            $h = HbEsBayt $girdi $c $hk $hbArg
            $y = YanVeri $c
            $o = Olc $girdi $c $b.FpsMetin -Pq
            $ek = HbOrtak $h $hk
            foreach ($k in $y.Keys) { $ek[$k] = $y[$k] }
            $ek['yan_veri_es'] = ($y.mastering -eq $kaynakYan.mastering -and $y.content_light -eq $kaynakYan.content_light)
            $ek['pq_korundu'] = ($y.color_transfer -eq 'smpte2084')
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'handbrake'; istenen_kbit = $kbit; kodlayici = 'HandBrakeCLI 1.11.2 x265_10bit slow 2 gecis turbo' }) $o $ek
            Remove-Item $c
        }
        Dene $Kesit 'negatif-handbrake-yarim-bit' $kbit {
            $c = Join-Path $Cikti "hdr-$Kesit-$kbit-negatif.mkv"
            $sn = HbKodla $girdi $c ($hbArg + @('-b', "$([int][math]::Round($hk / 2))"))
            $o = Olc $girdi $c $b.FpsMetin -Pq
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'negatif-handbrake-yarim-bit'; istenen_kbit = $kbit; kodlama_sn = $sn }) $o $null
            Remove-Item $c
        }
    }
}

function Vt {
    $enc = & ffmpeg -hide_banner -encoders 2>&1 | Out-String
    $enc | Set-Content (Join-Path $Cikti 'ffmpeg-encoders.txt')
    & $HandBrake --help 2>&1 | Out-String | Set-Content (Join-Path $Cikti 'handbrake-help.txt')
    $script:JsonAdi = 'vt.json'
    foreach ($k in @($Kesitler.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ })) {
        $script:Kesit = $k
        $girdi = Join-Path $Cikti "kesit-$k.mkv"
        if (-not (Test-Path $girdi)) { Write-Warning "kesit yok: $girdi"; continue }
        $b = Probe $girdi
        foreach ($kbit in @($VtKbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
            $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
            $script:urunKbps = $null
            Dene $k 'urun-vt' $kbit {
                $yol = 'bench shrink --force-codec hevc_videotoolbox'
                $sn = $null
                try {
                    $u = Urun $girdi $mb "vt-$k-$kbit-urun" @('--force-codec', 'hevc_videotoolbox', '--no-resolution-drop', '--no-fps-drop')
                    $dosya = $u.Dosya
                    $ek = UrunOrtak $u $mb
                } catch {
                    $yol = "ffmpeg + bench psy-args hevc_videotoolbox (bench shrink hatasi: $($_.Exception.Message))"
                    $psy = @(& dotnet $Bench psy-args hevc_videotoolbox | ConvertFrom-Json)
                    $dosya = Join-Path $Cikti "vt-$k-$kbit-urun-ham.mp4"
                    $sw = [Diagnostics.Stopwatch]::StartNew()
                    Ff (@('-i', $girdi, '-an', '-c:v', 'hevc_videotoolbox', '-b:v', "${kbit}k") + $psy + @('-tag:v', 'hvc1', '-movflags', '+faststart', $dosya))
                    $sw.Stop()
                    $ek = [ordered]@{ hedef_mb = $mb; kodlayici = 'hevc_videotoolbox'; kodlama_sn = [math]::Round($sw.Elapsed.TotalSeconds, 1); toplam_sn = [math]::Round($sw.Elapsed.TotalSeconds, 1); komut = "psy=$($psy -join ' ')" }
                }
                $o = Olc $girdi $dosya $b.FpsMetin
                $script:urunKbps = $o.kbps
                $ek['urun_yolu_vt'] = $yol
                Ekle ([ordered]@{ is = 'vt'; kesit = $k; kol = 'urun-vt'; istenen_kbit = $kbit }) $o $ek
                Remove-Item $dosya
            }
            if (-not $script:urunKbps) { continue }
            $hk = $script:urunKbps
            $hbArg = @('-Z', 'H.265 Apple VideoToolbox 1080p', '-a', 'none', '--crop-mode', 'none', '--width', "$($b.W)", '--height', "$($b.H)", '-f', 'av_mkv')
            Dene $k 'handbrake-vt' $kbit {
                $c = Join-Path $Cikti "vt-$k-$kbit-handbrake.mkv"
                $h = HbEsBayt $girdi $c $hk $hbArg
                $o = Olc $girdi $c $b.FpsMetin
                Ekle ([ordered]@{ is = 'vt'; kesit = $k; kol = 'handbrake-vt'; istenen_kbit = $kbit; kodlayici = 'HandBrakeCLI 1.11.2 H.265 Apple VideoToolbox 1080p' }) $o (HbOrtak $h $hk)
                Remove-Item $c
            }
            Dene $k 'negatif-handbrake-yarim-bit' $kbit {
                $c = Join-Path $Cikti "vt-$k-$kbit-negatif.mkv"
                $sn = HbKodla $girdi $c ($hbArg + @('-b', "$([int][math]::Round($hk / 2))"))
                $o = Olc $girdi $c $b.FpsMetin
                Ekle ([ordered]@{ is = 'vt'; kesit = $k; kol = 'negatif-handbrake-yarim-bit'; istenen_kbit = $kbit; kodlama_sn = $sn }) $o $null
                Remove-Item $c
            }
        }
    }
}

function EkranBant {
    if (-not $EkranUzun) { throw 'ekranbant icin -EkranUzun gerekli.' }
    $kb = Probe $EkranUzun
    $girdi = Join-Path $Cikti 'uzun-ekran.mkv'
    $on = if ($kb.Sure -lt $UzunSure) { @('-stream_loop', '-1') } else { @() }
    Ff ($on + @('-i', $EkranUzun, '-t', "$UzunSure", '-an', '-sn', '-map', '0:v:0', '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '4', '-pix_fmt', 'yuv420p', $girdi))
    $b = Probe $girdi
    foreach ($hedef in @($Hedefler.Split(',') | ForEach-Object { [double]::Parse($_.Trim(), $Inv) })) {
        Dene 'ekran' 'urun-otomatik' $null {
            $u = Urun $girdi $hedef "ekranbant-$($hedef.ToString($Inv))"
            $o = Olc $girdi $u.Dosya $b.FpsMetin -EkYok
            $ilk = $u.Denemeler | Where-Object { $_.no -eq 1 } | Select-Object -First 1
            $ek = UrunOrtak $u $hedef
            $ek['kaynak_sure'] = $b.Sure
            $ek['kaynak_dongu'] = ($on.Count -gt 0)
            $ek['ilk_kbit'] = $ilk.kbit
            $ek['ilk_istenen_kbps'] = if ($ilk) { [math]::Round($ilk.hedeflenen_mb * 8 * 1024 / $b.Sure, 1) } else { $null }
            $ek['ilk_cikan_mb'] = $ilk.cikan_mb
            $ek['ilk_verim'] = if ($ilk -and $ilk.hedeflenen_mb) { [math]::Round($ilk.cikan_mb / $ilk.hedeflenen_mb, 3) } else { $null }
            $ek['teslim_doluluk'] = [math]::Round($o.mb / $hedef * 100, 2)
            $ek['teslim_tasma'] = ($o.mb -gt $hedef)
            Ekle ([ordered]@{ is = $Is; kesit = 'ekran'; kol = 'urun-otomatik'; istenen_kbit = $null }) $o $ek
            Remove-Item $u.Dosya
        }
    }
    Remove-Item $girdi -ErrorAction SilentlyContinue
}

switch ($Is) {
    'handbrake' { Kiyas }
    'dusuk' { Dusuk }
    'social' { Social }
    'bantlasma' { Bantlasma }
    'turbo' { Turbo }
    'hdr' { Hdr }
    'vt' { Vt }
    'ekranbant' { EkranBant }
}
Yaz
$hatali = @($Satirlar | Where-Object { $_.PSObject.Properties['hata'] -and $_.hata })
Write-Host "satir=$($Satirlar.Count) hatali=$($hatali.Count)"
if ($Satirlar.Count -eq 0 -or $hatali.Count -eq $Satirlar.Count) { exit 1 }
