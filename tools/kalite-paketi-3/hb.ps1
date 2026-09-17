param(
    [Parameter(Mandatory)][ValidateSet('handbrake', 'dusuk', 'social', 'bantlasma', 'turbo', 'hdr', 'vt', 'ekranbant', 'svtara', 'turboilk', 'vtara', 'socialkodek', 'svtbekci')][string]$Is,
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
    [string]$RampaKbitler = '300,1200',
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

$script:FfSeviye = 'warning'

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
    $kollar = [ordered]@{ 'e0' = $Bench; 'e1' = $BenchE1; 'e0-duzen' = $Bench }
    foreach ($kbit in @($BantKbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
        $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
        foreach ($kol in $kollar.Keys) {
            Dene $Kesit $kol $kbit {
                $ek = if ($kol -eq 'e0-duzen') { @('--force-codec', 'libsvtav1', '--no-fps-drop') } else { @('--force-codec', 'libsvtav1', '--no-resolution-drop', '--no-fps-drop') }
                $u = Urun $girdi $mb "bant-$Kesit-$kbit-$kol" $ek $kollar[$kol]
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
    $script:vtOnbitKaldi = @()
    $neg = FfKos @('-f', 'lavfi', '-i', 'testsrc2=s=320x240:d=0.2', '-c:v', 'hevc_videotoolbox', '-foo', '1', '-f', 'null', $NullCikis)
    Ekle ([ordered]@{ is = 'vt'; kesit = ''; kol = 'negatif-vt-uydurma-bayrak'; cikis_kodu = $neg.Kod; uyari = (Uyarilar $neg.Metin); hukum = $(if ($neg.Kod -ne 0) { 'gecti: uydurma bayrak reddedildi' } else { 'kaldi: uydurma bayrak kabul edildi' }) }) $null $null
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
                $ok = IkiOkuma $girdi $dosya
                foreach ($x in $ok.Keys) { $ek[$x] = $ok[$x] }
                $pp = (& ffprobe -v error -select_streams v:0 -show_entries stream=pix_fmt,profile -of json $dosya | ConvertFrom-Json).streams[0]
                $ek['cikis_pix'] = $pp.pix_fmt
                $ek['cikis_profil'] = $pp.profile
                $ek['onbit_hukmu'] = if ($pp.pix_fmt -eq 'yuv420p10le' -and $pp.profile -eq 'Main 10' -and $yol -like 'bench shrink*') { 'gecti' } else { 'kaldi' }
                if ($ek['onbit_hukmu'] -ne 'gecti') { $script:vtOnbitKaldi += "$k $kbit $($pp.pix_fmt)/$($pp.profile)" }
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
                $hx = HbOrtak $h $hk
                $ok = IkiOkuma $girdi $c
                foreach ($x in $ok.Keys) { $hx[$x] = $ok[$x] }
                Ekle ([ordered]@{ is = 'vt'; kesit = $k; kol = 'handbrake-vt'; istenen_kbit = $kbit; kodlayici = 'HandBrakeCLI 1.11.2 H.265 Apple VideoToolbox 1080p' }) $o $hx
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
    if ($script:vtOnbitKaldi.Count -gt 0) { throw "VT urun ciktisi Main 10 degil: $($script:vtOnbitKaldi -join '; ')" }
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

$NullCikis = if ($IsWindows) { 'NUL' } else { '/dev/null' }

function FfKos([string[]]$A, [string]$Seviye = 'warning') {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $m = & ffmpeg -hide_banner -nostdin -loglevel $Seviye -y @A 2>&1 | Out-String
    $kod = $LASTEXITCODE
    $sw.Stop()
    [pscustomobject]@{ Kod = $kod; Sn = [math]::Round($sw.Elapsed.TotalSeconds, 1); Metin = $m }
}

function Uyarilar([string]$Metin) {
    ((($Metin -split "`n") | Where-Object { $_ -notmatch '^\s*Stream #' -and $_ -match 'unknown|Unknown|not used|not been used|different|first pass|Error parsing|Unrecognized|invalid|Invalid' } | ForEach-Object { $_.Trim() } | Select-Object -Unique -First 6) -join ' // ')
}

function AkisMd5([string]$Yol) {
    $m = & ffmpeg -hide_banner -nostdin -loglevel error -i $Yol -map 0:v:0 -c copy -f md5 - 2>$null | Out-String
    $m.Trim() -replace '^MD5=', ''
}

function SvtOnayar([string]$Metin) {
    $m = [regex]::Match($Metin, 'SVT \[config\]:[^\r\n]*preset[^:\r\n]*:\s*(-?\d+|Pass 1)')
    if (-not $m.Success) { $null } elseif ($m.Groups[1].Value -eq 'Pass 1') { 'Pass 1' } else { [int]$m.Groups[1].Value }
}

function Gecisli([string]$Girdi, [string]$Cikis, [string[]]$Kodek, [string[]]$Ilk, [string[]]$Iki) {
    $p = Join-Path $Cikti ('pass-' + [guid]::NewGuid().ToString('N'))
    $r1 = FfKos (@('-i', $Girdi, '-an') + $Kodek + $Ilk + @('-pass', '1', '-passlogfile', $p, '-f', 'null', $NullCikis)) $script:FfSeviye
    $istat = @(Get-ChildItem -Path $Cikti -Filter ((Split-Path $p -Leaf) + '*') -ErrorAction SilentlyContinue).Count
    if ($r1.Kod -ne 0) { throw "ilk gecis basarisiz ($($r1.Kod)): $(Uyarilar $r1.Metin) $($r1.Metin.Substring([math]::Max(0, $r1.Metin.Length - 300)))" }
    $r2 = FfKos (@('-i', $Girdi, '-an') + $Kodek + $Iki + @('-pass', '2', '-passlogfile', $p, $Cikis)) $script:FfSeviye
    Get-ChildItem -Path $Cikti -Filter ((Split-Path $p -Leaf) + '*') -ErrorAction SilentlyContinue | Remove-Item -ErrorAction SilentlyContinue
    if ($r2.Kod -ne 0) { throw "ikinci gecis basarisiz ($($r2.Kod)): $(Uyarilar $r2.Metin) $($r2.Metin.Substring([math]::Max(0, $r2.Metin.Length - 300)))" }
    [pscustomobject]@{ Sn1 = $r1.Sn; Sn2 = $r2.Sn; Sn = [math]::Round($r1.Sn + $r2.Sn, 1); IstatDosya = $istat; Uyari1 = (Uyarilar $r1.Metin); Uyari2 = (Uyarilar $r2.Metin); SvtOnayar1 = (SvtOnayar $r1.Metin); SvtOnayar2 = (SvtOnayar $r2.Metin) }
}

function HamEsBayt([string]$Cikis, [double]$HedefKbps, [int]$IlkB, [scriptblock]$Kodla) {
    $hB = $IlkB
    $hIz = @()
    $hMd5 = $null
    for ($hI = 1; $hI -le 3; $hI++) {
        $hR = & $Kodla $hB $Cikis
        $hK = Kbps $Cikis
        if ($hI -eq 1) { $hMd5 = AkisMd5 $Cikis }
        $hS = $hK / $HedefKbps - 1
        $hIz += "${hB}k->$hK"
        if ([math]::Abs($hS) -le 0.02 -or $hI -eq 3) {
            return [pscustomobject]@{ R = $hR; Kbps = $hK; B = $hB; Deneme = $hI; Sapma = [math]::Round($hS * 100, 2); Iz = ($hIz -join ' | '); Md5Ilk = $hMd5 }
        }
        $hB = [int][math]::Round($hB * $HedefKbps / $hK)
    }
}

function EsOrtak($h, [double]$HedefKbps) {
    $s = [ordered]@{ ham_b = $h.B; ham_deneme = $h.Deneme; ham_iz = $h.Iz; hedef_kbps = $HedefKbps; bayt_sapma_yuzde = $h.Sapma; es_bayt = ([math]::Abs($h.Sapma) -le 2); md5_ilk = $h.Md5Ilk }
    if ($h.R.PSObject.Properties['Sn1']) { $s.sn1 = $h.R.Sn1; $s.sn2 = $h.R.Sn2; $s.istat_dosya = $h.R.IstatDosya; $s.uyari1 = $h.R.Uyari1; $s.uyari2 = $h.R.Uyari2; $s.svt_onayar1 = $h.R.SvtOnayar1; $s.svt_onayar2 = $h.R.SvtOnayar2 }
    $s.kodlama_sn = $h.R.Sn
    $s
}

function IkiOkuma([string]$Ref, [string]$Test) {
    $rb = Probe $Ref
    $tb = Probe $Test
    $s = [ordered]@{ test_pix = $tb.Pix; yol_ii = 'zscale dither=none' }
    $cambi = "name=cambi\:enc_width=$($tb.W)\:enc_height=$($tb.H)"
    $zs = "zscale=w=$($rb.W):h=$($rb.H):filter=lanczos:dither=none:matrixin=709:matrix=709:transferin=709:transfer=709:primariesin=709:primaries=709:rangein=limited:range=limited,format=yuv420p"
    $okumalar = [ordered]@{
        i = @("scale=w=$($rb.W):h=$($rb.H):flags=lanczos,format=yuv420p10le", 'format=yuv420p10le')
        ii = @($zs, 'format=yuv420p')
    }
    foreach ($ad in $okumalar.Keys) {
        $t = $okumalar[$ad][0]
        $r = $okumalar[$ad][1]
        $s["cambi_$ad"] = $null; $s["vmafneg_ort_$ad"] = $null; $s["vmafneg_harm_$ad"] = $null; $s["xpsnr_$ad"] = $null; $s["hata_$ad"] = $null
        try {
            $log = Join-Path $Cikti ('vmaf-' + [guid]::NewGuid().ToString('N') + '.json')
            $g = "[0:v]$t,settb=AVTB,setpts=N[t];[1:v]$r,settb=AVTB,setpts=N[r];[t][r]libvmaf=model=version=vmaf_v0.6.1neg:feature='$cambi':log_fmt=json:log_path=$(Kacis $log):n_threads=4"
            $cikis = & ffmpeg -hide_banner -nostdin -i $Test -i $Ref -lavfi $g -f null - 2>&1 | Out-String
            if (-not (Test-Path $log) -and $ad -eq 'ii') {
                $t = "scale=w=$($rb.W):h=$($rb.H):flags=lanczos:sws_dither=none,format=yuv420p"
                $s.yol_ii = "swscale sws_dither=none (zscale kosmadi: $(Uyarilar $cikis))"
                $g = "[0:v]$t,settb=AVTB,setpts=N[t];[1:v]$r,settb=AVTB,setpts=N[r];[t][r]libvmaf=model=version=vmaf_v0.6.1neg:feature='$cambi':log_fmt=json:log_path=$(Kacis $log):n_threads=4"
                $cikis = & ffmpeg -hide_banner -nostdin -i $Test -i $Ref -lavfi $g -f null - 2>&1 | Out-String
            }
            if (-not (Test-Path $log)) { throw "libvmaf gunlugu yok: $($cikis.Substring([math]::Max(0, $cikis.Length - 400)))" }
            $p = (Get-Content $log -Raw | ConvertFrom-Json).pooled_metrics
            $c = $p.PSObject.Properties | Where-Object { $_.Name -like 'cambi*' } | Select-Object -First 1
            if ($c) { $s["cambi_$ad"] = [math]::Round($c.Value.mean, 4) }
            if ($p.vmaf) { $s["vmafneg_ort_$ad"] = [math]::Round($p.vmaf.mean, 4); $s["vmafneg_harm_$ad"] = [math]::Round($p.vmaf.harmonic_mean, 4) }
            Remove-Item $log -ErrorAction SilentlyContinue
            $gx = "[0:v]$t,settb=AVTB,setpts=N[t];[1:v]$r,settb=AVTB,setpts=N[r];[t][r]xpsnr"
            $cx = & ffmpeg -hide_banner -nostdin -i $Test -i $Ref -lavfi $gx -f null - 2>&1 | Out-String
            if ($cx -match 'XPSNR\s+y:\s*([\d.]+)\s*u:\s*([\d.]+)\s*v:\s*([\d.]+)') {
                $s["xpsnr_$ad"] = [math]::Round((4 * (Sayi $Matches[1]) + (Sayi $Matches[2]) + (Sayi $Matches[3])) / 6, 4)
            }
        } catch { $s["hata_$ad"] = $_.Exception.Message }
    }
    $s
}

function Md5Hukmu([string]$Kol, [string]$Md5, [string]$AtaMd5, [bool]$EsBekle) {
    if (-not $AtaMd5) { return 'ata yok' }
    $es = ($Md5 -eq $AtaMd5)
    if ($EsBekle) { if ($es) { 'gecti: uydurma anahtar bayt-es' } else { 'kaldi: uydurma anahtar ciktiyi degistirdi' } }
    else { if ($es) { 'kol yutuldu: ata ile bayt-es, olculmedi say' } else { 'gecti: ata ile bayt-farkli' } }
}

function SvtAra {
    if ($Kesit -eq 'rampa') { $girdi = Rampa; $kbitler = $RampaKbitler } else { $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"; $kbitler = $Kbitler }
    & ffmpeg -hide_banner -h encoder=libsvtav1 2>&1 | Out-String | Set-Content (Join-Path $Cikti 'ffmpeg-h-libsvtav1.txt')
    $sr = FfKos @('-f', 'lavfi', '-i', 'testsrc2=s=320x240:d=0.2', '-c:v', 'libsvtav1', '-f', 'null', $NullCikis) 'info'
    $sr.Metin | Set-Content (Join-Path $Cikti 'svt-surum.txt')
    $svtSurum = (($sr.Metin -split "`n") | Where-Object { $_ -match 'SVT' -and $_ -match 'v\d' } | ForEach-Object { $_.Trim() } | Select-Object -First 2) -join ' / '
    $b = Probe $girdi
    $k10 = Join-Path $Cikti "kesit-$Kesit-10bit.mkv"
    Ff @('-i', $girdi, '-c:v', 'ffv1', '-pix_fmt', 'yuv420p10le', $k10)
    Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'olcer-kaynak-kendisi'; svt_surum = $svtSurum }) $null (IkiOkuma $girdi $girdi)
    Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'olcer-kaynak-10bit' }) $null (IkiOkuma $girdi $k10)
    Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'olcer-kaynak-10bit-eski-olcer' }) $null (EkOlcu $girdi $k10 '')
    $g = [int][math]::Round($b.Fps * 5)
    $psy = 'tune=1:enable-variance-boost=0'
    $kollar = [ordered]@{
        'e0-8bit' = @{ Pix = 'yuv420p'; Psy = $psy; Ek = ''; Ata = ''; Es = $false }
        'negatif-uydurma-anahtar' = @{ Pix = 'yuv420p'; Psy = $psy; Ek = 'vidshrinkuydurma=1'; Ata = 'e0-8bit'; Es = $true }
        '10bit' = @{ Pix = 'yuv420p10le'; Psy = $psy; Ek = ''; Ata = 'e0-8bit'; Es = $false }
        '10bit-qm' = @{ Pix = 'yuv420p10le'; Psy = $psy; Ek = 'enable-qm=1:qm-min=0:qm-max=15'; Ata = '10bit'; Es = $false }
        '10bit-vb1' = @{ Pix = 'yuv420p10le'; Psy = 'tune=1:enable-variance-boost=1:variance-boost-strength=1'; Ek = ''; Ata = '10bit'; Es = $false }
        '10bit-tf0' = @{ Pix = 'yuv420p10le'; Psy = $psy; Ek = 'enable-tf=0'; Ata = '10bit'; Es = $false }
        '10bit-grain8' = @{ Pix = 'yuv420p10le'; Psy = $psy; Ek = 'film-grain=8:film-grain-denoise=0'; Ata = '10bit'; Es = $false }
    }
    foreach ($kbit in @($kbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
        $script:araMd5 = @{}
        $script:araTaban = $null
        foreach ($kol in $kollar.Keys) {
            Dene $Kesit $kol $kbit {
                $t = $kollar[$kol]
                $c = Join-Path $Cikti "svtara-$Kesit-$kbit-$kol.mkv"
                $prm = "keyint=${g}:scd=1:$($t.Psy)"
                if ($t.Ek) { $prm += ":$($t.Ek)" }
                $hedef = if ($script:araTaban) { $script:araTaban } else { [double]$kbit }
                $h = HamEsBayt $c $hedef $kbit { param($bb, $cc) Gecisli $girdi $cc @('-c:v', 'libsvtav1', '-preset', '6', '-b:v', "${bb}k", '-g', "$g", '-svtav1-params', $prm, '-pix_fmt', $t.Pix) @() @() }
                if ($kol -eq 'e0-8bit') { $script:araTaban = $h.Kbps }
                $script:araMd5[$kol] = $h.Md5Ilk
                $o = [ordered]@{ bayt = (Get-Item $c).Length; kbps = $h.Kbps }
                $ek = EsOrtak $h $hedef
                $ek.svtav1_params = $prm
                $ek.pix_fmt_istek = $t.Pix
                $ek.ata = $t.Ata
                $ek.md5_hukmu = Md5Hukmu $kol $h.Md5Ilk $script:araMd5[$t.Ata] $t.Es
                $ok = IkiOkuma $girdi $c
                foreach ($k in $ok.Keys) { $ek[$k] = $ok[$k] }
                $eski = EkOlcu $girdi $c ''
                $ek.eski_cambi = $eski.cambi
                Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = $kol; istenen_kbit = $kbit }) $o $ek
                Remove-Item $c
            }
        }
        if (-not $script:araTaban) { continue }
        Dene $Kesit 'handbrake-x265' $kbit {
            $c = Join-Path $Cikti "svtara-$Kesit-$kbit-handbrake.mkv"
            $h = HbEsBayt $girdi $c $script:araTaban (HbTemel $b)
            $ek = HbOrtak $h $script:araTaban
            $ok = IkiOkuma $girdi $c
            foreach ($k in $ok.Keys) { $ek[$k] = $ok[$k] }
            $ek.eski_cambi = (EkOlcu $girdi $c '').cambi
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'handbrake-x265'; istenen_kbit = $kbit; kodlayici = 'HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo' }) ([ordered]@{ bayt = (Get-Item $c).Length; kbps = $h.Kbps }) $ek
            Remove-Item $c
        }
    }
    Remove-Item $k10 -ErrorAction SilentlyContinue
}

function TurboIlk {
    $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    $b = Probe $girdi
    $g = [int][math]::Round($b.Fps * 5)
    $m = [int][math]::Round($b.Fps)
    $temel = "keyint=${g}:min-keyint=${m}:scenecut=40:psy-rd=2:psy-rdoq=1:aq-mode=2"
    $kollar = [ordered]@{
        'taban-slow' = @{ P1 = 'slow'; Ek = ''; Ata = ''; Es = $false }
        'negatif-slow-firstpass1' = @{ P1 = 'slow'; Ek = 'slow-firstpass=1'; Ata = 'taban-slow'; Es = $true }
        'ilk-veryfast' = @{ P1 = 'veryfast'; Ek = ''; Ata = 'taban-slow'; Es = $false }
        'ilk-faster' = @{ P1 = 'faster'; Ek = ''; Ata = 'taban-slow'; Es = $false }
        'ilk-fast' = @{ P1 = 'fast'; Ek = ''; Ata = 'taban-slow'; Es = $false }
        'ilk-medium' = @{ P1 = 'medium'; Ek = ''; Ata = 'taban-slow'; Es = $false }
        'slow-firstpass0' = @{ P1 = 'slow'; Ek = 'slow-firstpass=0'; Ata = 'taban-slow'; Es = $false }
    }
    foreach ($kbit in @($Kbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
        $script:araMd5 = @{}
        $script:araTaban = $null
        foreach ($kol in $kollar.Keys) {
            Dene $Kesit $kol $kbit {
                $t = $kollar[$kol]
                $c = Join-Path $Cikti "turboilk-$Kesit-$kbit-$kol.mkv"
                $prm = $temel
                if ($t.Ek) { $prm += ":$($t.Ek)" }
                $hedef = if ($script:araTaban) { $script:araTaban } else { [double]$kbit }
                $h = HamEsBayt $c $hedef $kbit { param($bb, $cc) Gecisli $girdi $cc @('-c:v', 'libx265', '-b:v', "${bb}k", '-g', "$g", '-x265-params', $prm, '-pix_fmt', 'yuv420p') @('-preset', $t.P1) @('-preset', 'slow') }
                if ($kol -eq 'taban-slow') { $script:araTaban = $h.Kbps }
                $script:araMd5[$kol] = $h.Md5Ilk
                $o = Olc $girdi $c $b.FpsMetin -EkYok
                $ek = EsOrtak $h $hedef
                $ek.ilk_gecis_preset = $t.P1
                $ek.x265_params = $prm
                $ek.ata = $t.Ata
                $ek.md5_hukmu = Md5Hukmu $kol $h.Md5Ilk $script:araMd5[$t.Ata] $t.Es
                Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = $kol; istenen_kbit = $kbit }) $o $ek
                Remove-Item $c
            }
        }
        Dene $Kesit 'negatif-uydurma-foo' $kbit {
            $c = Join-Path $Cikti "turboilk-$Kesit-$kbit-foo.mkv"
            $hukum = $null
            $iz = $null
            try {
                $r = Gecisli $girdi $c @('-c:v', 'libx265', '-b:v', "${kbit}k", '-g', "$g", '-x265-params', "${temel}:foo=1", '-pix_fmt', 'yuv420p') @('-preset', 'slow') @('-preset', 'slow')
                $iz = "$($r.Uyari1) // $($r.Uyari2)"
                $hukum = if ($iz -match 'foo') { 'gecti: kodlama bitti ama x265 foo anahtarini uyari ile reddetti' } else { 'kaldi: foo=1 ne dusurdu ne uyardi' }
                Remove-Item $c -ErrorAction SilentlyContinue
            } catch {
                $iz = $_.Exception.Message
                $hukum = 'gecti: foo=1 kodlamayi dusurdu'
            }
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'negatif-uydurma-foo'; istenen_kbit = $kbit; negatif_hukum = $hukum; negatif_iz = $iz }) $null $null
        }
    }
}

function VtProfil([string]$Yol) {
    $s = (& ffprobe -v error -select_streams v:0 -show_entries stream=profile,pix_fmt -of json $Yol | ConvertFrom-Json).streams[0]
    [ordered]@{ cikti_profil = $s.profile; cikti_pix = $s.pix_fmt; on_bit = ($s.pix_fmt -match '10' -and $s.profile -match '10') }
}

function VtAra {
    $yardim = & ffmpeg -hide_banner -h encoder=hevc_videotoolbox 2>&1 | Out-String
    $yardim | Set-Content (Join-Path $Cikti 'ffmpeg-h-hevc_videotoolbox.txt')
    $saq = $yardim -match 'spatial_aq'
    $script:JsonAdi = 'vtara.json'
    foreach ($k in @($Kesitler.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ })) {
        $script:Kesit = $k
        $girdi = Join-Path $Cikti "kesit-$k.mkv"
        if (-not (Test-Path $girdi)) { Write-Warning "kesit yok: $girdi"; continue }
        $b = Probe $girdi
        $g = [int][math]::Round($b.Fps * 5)
        $ortak = @('-c:v', 'hevc_videotoolbox', '-allow_sw', '0', '-g', "$g", '-tag:v', 'hvc1')
        Dene $k 'negatif-uydurma-foo' $null {
            $r = FfKos (@('-t', '2', '-i', $girdi, '-an') + $ortak + @('-b:v', '2000k', '-foo', '1', (Join-Path $Cikti "vtara-$k-foo.mp4")))
            $hukum = if ($r.Kod -ne 0) { "gecti: exit $($r.Kod)" } else { 'kaldi: -foo 1 kabul edildi' }
            Ekle ([ordered]@{ is = $Is; kesit = $k; kol = 'negatif-uydurma-foo'; negatif_hukum = $hukum; negatif_iz = (Uyarilar $r.Metin) }) $null $null
        }
        Dene $k 'negatif-preset-olu' $null {
            $r = FfKos (@('-t', '2', '-i', $girdi, '-an') + $ortak + @('-b:v', '2000k', '-preset', 'slow', '-pass', '1', (Join-Path $Cikti "vtara-$k-preset.mp4")))
            $bulundu = $r.Metin -match 'not (been )?used'
            $hukum = if ($bulundu) { 'gecti: -preset/-pass icin not used uyarisi var' } else { 'kaldi: not used uyarisi yok, preset olu iddiasi duser' }
            Ekle ([ordered]@{ is = $Is; kesit = $k; kol = 'negatif-preset-olu'; negatif_hukum = $hukum; negatif_iz = (Uyarilar $r.Metin); cikis_kodu = $r.Kod }) $null $null
        }
        foreach ($kbit in @($VtKbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
            $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
            $script:vtUrun = $null
            Dene $k 'urun-vt' $kbit {
                $u = Urun $girdi $mb "vtara-$k-$kbit-urun" @('--force-codec', 'hevc_videotoolbox', '--no-resolution-drop', '--no-fps-drop')
                $kb = Kbps $u.Dosya
                $bv = if ($u.Komut -match '-b:v (\d+)k') { [int]$Matches[1] } else { $null }
                $mr = if ($u.Komut -match '-maxrate (\d+)k') { [int]$Matches[1] } else { $null }
                $bs = if ($u.Komut -match '-bufsize (\d+)k') { [int]$Matches[1] } else { $null }
                $script:vtUrun = [pscustomobject]@{ Kbps = $kb; Tepe = $(if ($bv -and $mr) { $mr / $bv } else { 1.5 }); Tampon = $(if ($bv -and $bs) { $bs / $bv } else { 2.0 }) }
                $ek = UrunOrtak $u $mb
                $ek.tepe_orani = $script:vtUrun.Tepe
                $ek.tampon_orani = $script:vtUrun.Tampon
                $p = VtProfil $u.Dosya
                foreach ($x in $p.Keys) { $ek[$x] = $p[$x] }
                $ok = IkiOkuma $girdi $u.Dosya
                foreach ($x in $ok.Keys) { $ek[$x] = $ok[$x] }
                Ekle ([ordered]@{ is = $Is; kesit = $k; kol = 'urun-vt'; istenen_kbit = $kbit }) ([ordered]@{ bayt = (Get-Item $u.Dosya).Length; kbps = $kb }) $ek
                Remove-Item $u.Dosya
            }
            if (-not $script:vtUrun) { continue }
            $hk = $script:vtUrun.Kbps
            $kollar = [ordered]@{
                'vt-8bit-tek' = @{ Ek = @(); Sinir = $true }
                'vt-10bit-tek' = @{ Ek = @('-profile:v', 'main10', '-pix_fmt', 'p010le'); Sinir = $true }
                'vt-10bit-sinirsiz' = @{ Ek = @('-profile:v', 'main10', '-pix_fmt', 'p010le'); Sinir = $false }
                'vt-10bit-prio0' = @{ Ek = @('-profile:v', 'main10', '-pix_fmt', 'p010le', '-prio_speed', '0'); Sinir = $true }
            }
            if ($saq) { $kollar['vt-10bit-saq'] = @{ Ek = @('-profile:v', 'main10', '-pix_fmt', 'p010le', '-spatial_aq', '1'); Sinir = $true } }
            foreach ($kol in $kollar.Keys) {
                Dene $k $kol $kbit {
                    $t = $kollar[$kol]
                    $c = Join-Path $Cikti "vtara-$k-$kbit-$kol.mp4"
                    $h = HamEsBayt $c $hk ([int][math]::Round($hk)) {
                        param($bb, $cc)
                        $sinir = if ($t.Sinir) { @('-maxrate', "$([int]($bb * $script:vtUrun.Tepe))k", '-bufsize', "$([int]($bb * $script:vtUrun.Tampon))k") } else { @() }
                        $r = FfKos (@('-i', $girdi, '-an') + $ortak + @('-b:v', "${bb}k") + $sinir + $t.Ek + @($cc))
                        if ($r.Kod -ne 0) { throw "vt kolu basarisiz ($($r.Kod)): $(Uyarilar $r.Metin)" }
                        $r
                    }
                    $ek = EsOrtak $h $hk
                    $ek.vt_ek = ($t.Ek -join ' ')
                    $ek.sinirli = $t.Sinir
                    $p = VtProfil $c
                    foreach ($x in $p.Keys) { $ek[$x] = $p[$x] }
                    $ok = IkiOkuma $girdi $c
                    foreach ($x in $ok.Keys) { $ek[$x] = $ok[$x] }
                    Ekle ([ordered]@{ is = $Is; kesit = $k; kol = $kol; istenen_kbit = $kbit }) ([ordered]@{ bayt = (Get-Item $c).Length; kbps = $h.Kbps }) $ek
                    Remove-Item $c
                }
            }
            Dene $k 'handbrake-vt' $kbit {
                $hbArg = @('-Z', 'H.265 Apple VideoToolbox 1080p', '-a', 'none', '--crop-mode', 'none', '--width', "$($b.W)", '--height', "$($b.H)", '-f', 'av_mkv')
                $c = Join-Path $Cikti "vtara-$k-$kbit-handbrake.mkv"
                $h = HbEsBayt $girdi $c $hk $hbArg
                $ek = HbOrtak $h $hk
                $hl = Get-Content (Join-Path $Cikti 'handbrake.log') -Tail 400
                $ek.hb_kodlayici_satiri = (($hl | Where-Object { $_ -match 'vt_h26|VideoToolbox|10-bit|encoder' } | ForEach-Object { $_.Trim() } | Select-Object -Unique -Last 4) -join ' // ')
                $p = VtProfil $c
                foreach ($x in $p.Keys) { $ek[$x] = $p[$x] }
                $ok = IkiOkuma $girdi $c
                foreach ($x in $ok.Keys) { $ek[$x] = $ok[$x] }
                Ekle ([ordered]@{ is = $Is; kesit = $k; kol = 'handbrake-vt'; istenen_kbit = $kbit; kodlayici = 'HandBrakeCLI 1.11.2 H.265 Apple VideoToolbox 1080p' }) ([ordered]@{ bayt = (Get-Item $c).Length; kbps = $h.Kbps }) $ek
                Remove-Item $c
            }
        }
    }
}

function SocialKodek {
    $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    $b = Probe $girdi
    $ad = 'Social 25 MB 30 Seconds 1080p60'
    $script:skHb = $null
    Dene $Kesit "hb:$ad" $null {
        $c = Join-Path $Cikti "socialkodek-$Kesit-hb.mp4"
        $sn = HbKodla $girdi $c @('-Z', $ad, '-a', 'none')
        $hb = Probe $c
        $o = Olc $girdi $c $b.FpsMetin
        $script:skHb = [pscustomobject]@{ Kbps = $o.kbps; Mb = [math]::Round((Get-Item $c).Length / 1MB, 4); W = $hb.W; H = $hb.H; Fps = $hb.Fps }
        Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'handbrake'; onayar = $ad; kodlama_sn = $sn; hb_fps = $hb.Fps }) $o $null
        Remove-Item $c
    }
    if (-not $script:skHb) { return }
    $hk = $script:skHb.Kbps
    foreach ($uKol in @('urun-otomatik', 'urun-sosyal')) {
        Dene $Kesit $uKol $null {
            $uEk = if ($uKol -eq 'urun-sosyal') { @('--intent', 'socialmedia') } else { @() }
            $u = Urun $girdi $script:skHb.Mb "socialkodek-$Kesit-$uKol" $uEk
            $o = Olc $girdi $u.Dosya $b.FpsMetin
            $uo = UrunOrtak $u $script:skHb.Mb
            $uo.komut_onayar = if ($u.Komut -match '-preset (\S+)') { $Matches[1] } else { $null }
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = $uKol; onayar = $ad }) $o $uo
            Remove-Item $u.Dosya
        }
    }
    $vf = @()
    $f = @()
    if ($script:skHb.W -ne $b.W -or $script:skHb.H -ne $b.H) { $f += "scale=$($script:skHb.W):$($script:skHb.H):flags=lanczos" }
    if ($script:skHb.Fps -lt $b.Fps - 0.01) { $f += "fps=$($script:skHb.Fps.ToString('0.###', $Inv))" }
    if ($f.Count -gt 0) { $vf = @('-vf', ($f -join ',')) }
    $fpsKod = [math]::Min($b.Fps, $script:skHb.Fps)
    $g = [int][math]::Round($fpsKod * 5)
    $m = [int][math]::Round($fpsKod)
    $kollar = [ordered]@{
        'svt-p6' = @{ K = @('-c:v', 'libsvtav1', '-preset', '6', '-svtav1-params', "keyint=${g}:scd=1:tune=1:enable-variance-boost=0", '-pix_fmt', 'yuv420p'); Ata = ''; Es = $false; Yarim = $false; Onayar = 6 }
        'negatif-svt-p6-uydurma' = @{ K = @('-c:v', 'libsvtav1', '-preset', '6', '-svtav1-params', "keyint=${g}:scd=1:tune=1:enable-variance-boost=0:vidshrinkuydurma=1", '-pix_fmt', 'yuv420p'); Ata = 'svt-p6'; Es = $true; Yarim = $false; Onayar = 6 }
        'svt-p4' = @{ K = @('-c:v', 'libsvtav1', '-preset', '4', '-svtav1-params', "keyint=${g}:scd=1:tune=1:enable-variance-boost=0", '-pix_fmt', 'yuv420p'); Ata = 'svt-p6'; Es = $false; Yarim = $false; Onayar = 4 }
        'x265-slow' = @{ K = @('-c:v', 'libx265', '-preset', 'slow', '-x265-params', "keyint=${g}:min-keyint=${m}:scenecut=40:psy-rd=2:psy-rdoq=1:aq-mode=2", '-pix_fmt', 'yuv420p'); Ata = ''; Es = $false; Yarim = $false }
        'negatif-svt-p6-yarim-bit' = @{ K = @('-c:v', 'libsvtav1', '-preset', '6', '-svtav1-params', "keyint=${g}:scd=1:tune=1:enable-variance-boost=0", '-pix_fmt', 'yuv420p'); Ata = ''; Es = $false; Yarim = $true; Onayar = 6 }
    }
    $script:araMd5 = @{}
    $script:FfSeviye = 'info'
    foreach ($kol in $kollar.Keys) {
        Dene $Kesit $kol $null {
            $t = $kollar[$kol]
            $c = Join-Path $Cikti "socialkodek-$Kesit-$kol.mkv"
            $hedef = if ($t.Yarim) { $hk / 2 } else { $hk }
            $h = HamEsBayt $c $hedef ([int][math]::Round($hk)) { param($bb, $cc) Gecisli $girdi $cc (@('-b:v', "${bb}k") + $vf + $t.K + @('-g', "$g")) @() @() }
            $script:araMd5[$kol] = $h.Md5Ilk
            $o = Olc $girdi $c $b.FpsMetin
            $ek = EsOrtak $h $hedef
            $ek.komut_kodek = ($vf + $t.K) -join ' '
            $ek.geometri = "$($script:skHb.W)x$($script:skHb.H)@$fpsKod"
            $ek.md5_hukmu = Md5Hukmu $kol $h.Md5Ilk $script:araMd5[$t.Ata] $t.Es
            if ($t.Onayar) { $ek.onayar_hukmu = if ($ek.svt_onayar1 -eq 'Pass 1' -and $ek.svt_onayar2 -eq $t.Onayar) { "gecti: ilk gecis SVT'nin kendi Pass 1 on ayari, ikinci geciste preset $($t.Onayar)" } else { "kaldi: beklenen $($t.Onayar), gecis1=$($ek.svt_onayar1) gecis2=$($ek.svt_onayar2)" } }
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = $kol; onayar = $ad }) $o $ek
            Remove-Item $c
        }
    }
}

function SvtSatirlari([string]$Metin) {
    ((($Metin -split "`n") | Where-Object { $_ -match 'BRC mode|pred struct|Svt\[warn\]|Svt\[error\]|Error parsing' } | ForEach-Object { $_.Trim() } | Select-Object -Unique -First 8) -join ' // ')
}

function SvtBekci {
    if ($Kesit -eq 'rampa') { $girdi = Rampa; $r = 1174; $tavanKbit = 1200 } else { $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"; $r = 580; $tavanKbit = 600 }
    $b = Probe $girdi
    $tavanMb = [math]::Round($tavanKbit * $b.Sure / 8 / 1024, 4)
    $g = [int][math]::Round($b.Fps * 5)
    $psy = "keyint=${g}:scd=1:tune=1:enable-variance-boost=0"
    $t = [int][math]::Round($r * 0.9)
    $kollar = [ordered]@{
        'vbr2' = @{ Iki = $true; K = @('-b:v', "${r}k"); P = '' }
        'negatif-uydurma' = @{ Iki = $true; K = @('-b:v', "${r}k"); P = 'vidshrinkuydurma=1' }
        'vbr1' = @{ Iki = $false; K = @('-b:v', "${r}k"); P = '' }
        'cbr1' = @{ Iki = $false; K = @('-b:v', "${r}k", '-maxrate', "${r}k", '-bufsize', "${r}k"); P = '' }
        'cbr2' = @{ Iki = $true; K = @('-b:v', "${r}k", '-maxrate', "${r}k", '-bufsize', "${r}k"); P = '' }
        'vbr2-tepe' = @{ Iki = $true; K = @('-b:v', "${t}k", '-maxrate', "${r}k", '-bufsize', "${r}k"); P = '' }
        'capcrf' = @{ Iki = $false; K = @('-crf', '40', '-maxrate', "${r}k", '-bufsize', "$(2 * $r)k"); P = '' }
        'vbr2-gop' = @{ Iki = $true; K = @('-b:v', "${r}k"); P = 'gop-constraint-rc=1' }
        'vbr2-os0' = @{ Iki = $true; K = @('-b:v', "${r}k"); P = 'overshoot-pct=0' }
        'vbr2-recode' = @{ Iki = $true; K = @('-b:v', "${r}k"); P = 'recode-loop=4' }
    }
    foreach ($kol in $kollar.Keys) {
        for ($tekrar = 1; $tekrar -le 3; $tekrar++) {
            Dene $Kesit $kol $r {
                $k = $kollar[$kol]
                $prm = $psy
                if ($k.P) { $prm += ":$($k.P)" }
                $kodek = @('-c:v', 'libsvtav1', '-preset', '6') + $k.K + @('-g', "$g", '-svtav1-params', $prm, '-pix_fmt', 'yuv420p')
                $c = Join-Path $Cikti "bekci-$Kesit-$kol-$tekrar.mp4"
                if ($k.Iki) {
                    $p = Join-Path $Cikti ('pass-' + [guid]::NewGuid().ToString('N'))
                    $r1 = FfKos (@('-i', $girdi, '-an') + $kodek + @('-pass', '1', '-passlogfile', $p, '-f', 'null', $NullCikis)) 'info'
                    $r2 = FfKos (@('-i', $girdi, '-an') + $kodek + @('-pass', '2', '-passlogfile', $p, '-movflags', '+faststart', $c)) 'info'
                    Get-ChildItem -Path $Cikti -Filter ((Split-Path $p -Leaf) + '*') -ErrorAction SilentlyContinue | Remove-Item -ErrorAction SilentlyContinue
                    $kod = [math]::Max($r1.Kod, $r2.Kod); $sn = [math]::Round($r1.Sn + $r2.Sn, 1); $metin = $r2.Metin
                } else {
                    $r2 = FfKos (@('-i', $girdi, '-an') + $kodek + @('-movflags', '+faststart', $c)) 'info'
                    $kod = $r2.Kod; $sn = $r2.Sn; $metin = $r2.Metin
                }
                $satir = [ordered]@{ is = $Is; kesit = $Kesit; kol = $kol; tekrar = $tekrar; istek_kbit = $r; tavan_mb = $tavanMb; cikis_kodu = $kod; kodlama_sn = $sn; komut_kodek = ($kodek -join ' '); svt = (SvtSatirlari $metin); uyari = (Uyarilar $metin) }
                if ($kod -eq 0 -and (Test-Path $c)) {
                    $mb = (Get-Item $c).Length / 1MB
                    $satir.mb = [math]::Round($mb, 4)
                    $satir.tavan_orani = [math]::Round($mb / $tavanMb, 4)
                    $satir.tavan_alti = ($mb -le $tavanMb)
                    $satir.md5 = AkisMd5 $c
                    Remove-Item $c
                }
                Ekle $satir $null $null
            }
        }
    }
}

switch ($Is) {
    'svtbekci' { SvtBekci }
    'svtara' { SvtAra }
    'turboilk' { TurboIlk }
    'vtara' { VtAra }
    'socialkodek' { SocialKodek }
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
