param(
    [Parameter(Mandatory)][ValidateSet('handbrake', 'dusuk', 'social', 'bantlasma', 'turbo', 'hdr', 'vt', 'ekranbant', 'svtara', 'turboilk', 'vtara', 'socialkodek', 'svtbekci', 'tavanbekci', 'svtbant', 'yavg', 'karanlikgecis', 'vthizli', 'handbrakecli', 'handbrakecli-svt', 'filtre', 'butceilk')][string]$Is,
    [Parameter(Mandatory)][string]$Cikti,
    [Parameter(Mandatory)][string]$Bench,
    [string]$Kesit = '',
    [string]$Kesitler = 'karanlik,parlak,hareketli,ekran',
    [string]$HandBrake = '',
    [string]$BenchE1 = '',
    [string]$BenchTurbo = '',
    [string]$BenchNeg = '',
    [string]$BekciKbitler = '1200,300,100',
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
    [string]$Onayarlar = '',
    [double]$DengeliOran = 3.0
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
        TavanAsildi = if ($r) { $r.CeilingExceeded } else { $null }
        Doygun = if ($r) { $r.Saturated } else { $null }
        Bekci = [bool]@($denemeler | Where-Object { $_.dal -like 'ceiling guard*' }).Count
        Yanitsiz = [bool]@($metin | Where-Object { $_ -like '*did not answer*' }).Count
        TabanAdimi = [bool]@($denemeler | Where-Object { $_.dal -like 'encoder floor*' }).Count
        Denemeler = $denemeler
        Komut = $komut
    }
}

function UrunOrtak($u, [double]$HedefMb) {
    [ordered]@{
        urun_yolu = $UrunYolu; hedef_mb = $HedefMb; kodlayici = $u.Kodlayici; geometri = $u.Geometri; kodlama_sn = $u.KodlamaSn; toplam_sn = $u.ToplamSn
        deneme = $u.Deneme; bantta = $u.Bantta; tasma = $u.Tasma; tavan_asildi = $u.TavanAsildi; doygun = $u.Doygun; bekci = $u.Bekci; yanitsiz = $u.Yanitsiz; taban_adimi = $u.TabanAdimi
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
            $tekrarlar = if ($Kesit -eq 'rampa' -and $kol -eq 'e0-duzen' -and $kbit -eq 1200) { 3 } else { 1 }
            for ($tekrar = 1; $tekrar -le $tekrarlar; $tekrar++) {
                Dene $Kesit $kol $kbit {
                    $ek = if ($kol -eq 'e0-duzen') { @('--force-codec', 'libsvtav1', '--no-fps-drop') } else { @('--force-codec', 'libsvtav1', '--no-resolution-drop', '--no-fps-drop') }
                    $u = Urun $girdi $mb "bant-$Kesit-$kbit-$kol-$tekrar" $ek $kollar[$kol]
                    $o = Olc $girdi $u.Dosya $b.FpsMetin
                    $ortak = UrunOrtak $u $mb
                    $ortak.tekrar = $tekrar
                    $ortak.kabul = BantKabul $kol $kbit $mb $u $o
                    Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = $kol; istenen_kbit = $kbit }) $o $ortak
                    Remove-Item $u.Dosya
                }
            }
        }
    }
}

function BantKabul([string]$Kol, [int]$Kbit, [double]$TavanMb, $u, $o) {
    $yukseklik = [int](($o.geometri_cikti -split 'x')[1])
    $neden = @()
    if ($Kesit -eq 'rampa' -and $Kol -eq 'e0-duzen' -and $Kbit -eq 1200) {
        $ikiTasma = @($u.Denemeler | Where-Object { $_.no -le 2 -and $_.dal -eq 'over ceiling' }).Count -ge 2
        if ($o.mb -lt 0.703) { $neden += "mb $($o.mb) < 0,703" }
        if ($u.Deneme -gt 3) { $neden += "deneme $($u.Deneme) > 3" }
        if ($ikiTasma -and -not $u.Bekci) { $neden += 'iki tavan ustu denemeden sonra izde ceiling guard yok' }
        if ($u.Doygun) { $neden += 'doygun' }
        if ($u.Yanitsiz) { $neden += 'did not answer' }
        if ($neden.Count) { return 'kaldi: ' + ($neden -join '; ') }
        if ($o.mb -gt $TavanMb) { return "olasiliksal: tavan ustu $($o.mb) > $TavanMb, en kucuk teslim" }
        return 'gecti'
    }
    if ($Kesit -eq 'karanlik' -and $Kbit -eq 100 -and $Kol -eq 'e0-duzen') {
        if ($o.mb -lt 0.1123 -or $o.mb -gt 0.1221) { $neden += "mb $($o.mb) 0,1123-0,1221 disinda" }
        if ($u.Deneme -ne 1) { $neden += "deneme $($u.Deneme) != 1" }
        if ($yukseklik -ge 818) { $neden += "yukseklik $yukseklik >= 818" }
    } elseif ($Kesit -eq 'karanlik' -and $Kbit -eq 100 -and $Kol -eq 'e0') {
        if ($o.mb -gt 0.140) { $neden += "mb $($o.mb) > 0,140" }
        if (-not $u.Tasma) { $neden += 'OverTarget yok' }
        if (-not $u.TavanAsildi) { $neden += 'CeilingExceeded yok' }
        if ($u.Deneme -ne 3) { $neden += "deneme $($u.Deneme) != 3" }
        if ($u.TabanAdimi) { $neden += 'encoder floor satiri var' }
    } else {
        return $null
    }
    if ($neden.Count) { 'kaldi: ' + ($neden -join '; ') } else { 'gecti' }
}

function TavanBekci {
    if (-not $BenchNeg) { throw 'tavanbekci icin -BenchNeg gerekli.' }
    $girdi = if ($Kesit -eq 'rampa') { Rampa } else { Join-Path $Cikti "kesit-$Kesit.mkv" }
    $b = Probe $girdi
    foreach ($kbit in @($BekciKbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
        $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
        $script:bekciMd5 = $null
        $script:bekciKostu = $false
        foreach ($kol in @('x264-bekci', 'negatif-x264-tepe-serbest')) {
            Dene $Kesit $kol $kbit {
                $by = if ($kol -eq 'x264-bekci') { $Bench } else { $BenchNeg }
                $u = Urun $girdi $mb "tavanbekci-$Kesit-$kbit-$kol" @('--force-codec', 'libx264', '--no-fps-drop') $by
                $md5 = AkisMd5 $u.Dosya
                $dosyaMb = [math]::Round((Get-Item $u.Dosya).Length / 1MB, 4)
                $vk = & ffprobe -v error -select_streams v:0 -show_entries stream=bit_rate -of csv=p=0 $u.Dosya
                $videoKbps = if ($vk -match '^\d+') { [math]::Round([double]$Matches[0] / 1000, 1) } else { $null }
                $bekciK = @($u.Denemeler | Where-Object { $_.dal -like 'ceiling guard*' } | Select-Object -Last 1 | ForEach-Object { $_.kbit })
                $son = $u.Denemeler | Select-Object -Last 1
                $ortak = UrunOrtak $u $mb
                $ortak.mb = $dosyaMb
                $ortak.tavan_alti = ($dosyaMb -le $mb)
                $ortak.md5 = $md5
                $ortak.video_kbps_ffprobe = $videoKbps
                $ortak.bekci_k = if ($bekciK.Count) { $bekciK[0] } else { $null }
                $ortak.teslim_bekci_denemesi = ($u.Bekci -and $son -and [math]::Abs($son.cikan_mb - $dosyaMb) -lt 0.002)
                if ($kol -eq 'x264-bekci') {
                    $script:bekciMd5 = $md5
                    $script:bekciKostu = $u.Bekci
                    $ortak.kabul = if (-not $u.Bekci) { 'bekci kosmadi' }
                        elseif (-not $ortak.teslim_bekci_denemesi) { "kaldi: teslim bekci denemesi degil (tavan ustu en kucuk $dosyaMb MB)" }
                        elseif ($videoKbps -le $ortak.bekci_k -and $ortak.tavan_alti) { 'gecti' }
                        else { "kaldi: video $videoKbps kbps, K $($ortak.bekci_k), dosya $dosyaMb / $mb MB" }
                } else {
                    $ortak.kabul = if (-not $script:bekciKostu -or -not $u.Bekci) { 'bekci kosmadi' }
                        elseif ($md5 -eq $script:bekciMd5) { 'kaldi: tepe bayragi etkisiz, md5 ayni' }
                        else { "gecti: md5 farkli, tavan_alti=$($ortak.tavan_alti)" }
                }
                Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = $kol; istenen_kbit = $kbit }) $null $ortak
                Remove-Item $u.Dosya
            }
        }
        $sinirMb = [math]::Round($kbit * 1000 * ($b.Sure + 1) / 8 / 1MB, 4)
        $ortaMb = [math]::Round($kbit * 1000 * $b.Sure / 8 / 1MB, 4)
        foreach ($kodek in @('libx264', 'libx265')) {
            foreach ($tepe in @($true, $false)) {
                $kol = if ($tepe) { "ham-$kodek-tepe-esit" } else { "negatif-ham-$kodek-tepe-serbest" }
                Dene $Kesit $kol $kbit {
                    $c = Join-Path $Cikti "tavanbekci-$Kesit-$kbit-$kol.mp4"
                    $a = @('-c:v', $kodek, '-preset', 'slow', '-b:v', "${kbit}k", '-pix_fmt', 'yuv420p')
                    if ($tepe) { $a += @('-maxrate', "${kbit}k", '-bufsize', "${kbit}k") }
                    if ($kodek -eq 'libx265') { $a += @('-x265-params', 'log-level=error') }
                    $r = Gecisli $girdi $c $a @() @('-movflags', '+faststart')
                    $mbD = [math]::Round((Get-Item $c).Length / 1MB, 4)
                    $vk = & ffprobe -v error -select_streams v:0 -show_entries stream=bit_rate -of csv=p=0 $c
                    $vKbps = if ($vk -match '^\d+') { [math]::Round([double]$Matches[0] / 1000, 1) } else { $null }
                    $s = [ordered]@{ kodek = $kodek; tepe_esit = $tepe; mb = $mbD; ortalama_mb = $ortaMb; sinir_mb = $sinirMb; video_kbps_ffprobe = $vKbps; oran = [math]::Round($mbD / $ortaMb, 4); kodlama_sn = $r.Sn; uyari = $r.Uyari2 }
                    $s.kabul = if ($tepe) { if ($mbD -le $sinirMb) { 'gecti: K x (T+1) icinde' } else { "kaldi: $mbD > $sinirMb" } } else { if ($mbD -gt $sinirMb) { "negatif gosterdi: $mbD > $sinirMb" } else { 'negatif sinirda kaldi: bu icerikte tepe serbest de tasmiyor' } }
                    Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = $kol; istenen_kbit = $kbit }) $null $s
                    Remove-Item $c
                }
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

function VtHizli {
    $enc = & ffmpeg -hide_banner -encoders 2>&1 | Out-String
    $enc | Set-Content (Join-Path $Cikti 'ffmpeg-encoders.txt')
    $script:JsonAdi = 'vthizli.json'
    $hucreler = [Collections.Generic.List[object]]::new()
    $neg = FfKos @('-f', 'lavfi', '-i', 'testsrc2=s=320x240:d=0.2', '-c:v', 'hevc_videotoolbox', '-foo', '1', '-f', 'null', $NullCikis)
    $n1 = ($neg.Kod -ne 0)
    Ekle ([ordered]@{ is = 'vthizli'; kesit = ''; kol = 'negatif-vt-uydurma-bayrak'; cikis_kodu = $neg.Kod; uyari = (Uyarilar $neg.Metin); hukum = $(if ($n1) { 'gecti: uydurma bayrak reddedildi' } else { 'kaldi: uydurma bayrak kabul edildi' }) }) $null $null
    $drop = @('--no-resolution-drop', '--no-fps-drop')
    foreach ($k in @($Kesitler.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ })) {
        $script:Kesit = $k
        $girdi = Join-Path $Cikti "kesit-$k.mkv"
        if (-not (Test-Path $girdi)) { Write-Warning "kesit yok: $girdi"; continue }
        $b = Probe $girdi
        foreach ($kbit in @($VtKbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
            $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
            $h = [ordered]@{ kesit = $k; kbit = $kbit; hedef_mb = $mb; vt_kodlayici = $null; vt_vmaf = $null; vt_xpsnr = $null; vt_sn = $null; vt_kbps = $null; vt_bantta = $null; vt_tavan = $null; yz_kodlayici = $null; yz_vmaf = $null; yz_xpsnr = $null; hb_vmaf = $null; hb_xpsnr = $null; hb_sn = $null; hb_es_bayt = $null }
            Dene $k 'vt-hizli' $kbit {
                $u = Urun $girdi $mb "vth-$k-$kbit-hizli" (@('--speed', 'fast') + $drop)
                $o = Olc $girdi $u.Dosya $b.FpsMetin
                $ek = UrunOrtak $u $mb
                $h.vt_kodlayici = $u.Kodlayici; $h.vt_vmaf = $o.vmafneg_ort; $h.vt_xpsnr = $o.xpsnr; $h.vt_sn = $u.KodlamaSn; $h.vt_kbps = $o.kbps; $h.vt_bantta = $u.Bantta; $h.vt_tavan = $u.TavanAsildi
                Ekle ([ordered]@{ is = 'vthizli'; kesit = $k; kol = 'vt-hizli'; istenen_kbit = $kbit }) $o $ek
                Remove-Item $u.Dosya
            }
            Dene $k 'yazilim' $kbit {
                $u = Urun $girdi $mb "vth-$k-$kbit-yazilim" $drop
                $o = Olc $girdi $u.Dosya $b.FpsMetin
                $ek = UrunOrtak $u $mb
                $h.yz_kodlayici = $u.Kodlayici; $h.yz_vmaf = $o.vmafneg_ort; $h.yz_xpsnr = $o.xpsnr
                Ekle ([ordered]@{ is = 'vthizli'; kesit = $k; kol = 'yazilim'; istenen_kbit = $kbit }) $o $ek
                Remove-Item $u.Dosya
            }
            if ($h.vt_kbps) {
                $hbArg = @('-Z', 'H.265 Apple VideoToolbox 1080p', '-a', 'none', '--crop-mode', 'none', '--width', "$($b.W)", '--height', "$($b.H)", '-f', 'av_mkv')
                Dene $k 'handbrake-vt' $kbit {
                    $c = Join-Path $Cikti "vth-$k-$kbit-handbrake.mkv"
                    $hb = HbEsBayt $girdi $c $h.vt_kbps $hbArg
                    $o = Olc $girdi $c $b.FpsMetin
                    $hx = HbOrtak $hb $h.vt_kbps
                    $h.hb_vmaf = $o.vmafneg_ort; $h.hb_xpsnr = $o.xpsnr; $h.hb_sn = $hb.Sn; $h.hb_es_bayt = $hx.es_bayt
                    Ekle ([ordered]@{ is = 'vthizli'; kesit = $k; kol = 'handbrake-vt'; istenen_kbit = $kbit; kodlayici = 'HandBrakeCLI 1.11.2 H.265 Apple VideoToolbox 1080p' }) $o $hx
                    Remove-Item $c
                }
            }
            $h['k2_ham'] = if ($null -ne $h.vt_vmaf -and $null -ne $h.yz_vmaf) { $h.vt_vmaf - $h.yz_vmaf } else { $null }
            $h['k3_ham'] = if ($h.vt_sn -and $h.hb_sn) { $h.vt_sn / $h.hb_sn } else { $null }
            $h['k5_ham'] = if ($null -ne $h.vt_vmaf -and $null -ne $h.hb_vmaf) { $h.vt_vmaf - $h.hb_vmaf } else { $null }
            $h['k2_fark'] = if ($null -ne $h.k2_ham) { [math]::Round($h.k2_ham, 6) } else { $null }
            $h['k3_oran'] = if ($null -ne $h.k3_ham) { [math]::Round($h.k3_ham, 6) } else { $null }
            $h['k5_fark'] = if ($null -ne $h.k5_ham) { [math]::Round($h.k5_ham, 6) } else { $null }
            $h['xpsnr_fark_yazilim'] = if ($null -ne $h.vt_xpsnr -and $null -ne $h.yz_xpsnr) { [math]::Round($h.vt_xpsnr - $h.yz_xpsnr, 3) } else { $null }
            $h['xpsnr_fark_hb'] = if ($null -ne $h.vt_xpsnr -and $null -ne $h.hb_xpsnr) { [math]::Round($h.vt_xpsnr - $h.hb_xpsnr, 3) } else { $null }
            $hucreler.Add([pscustomobject]$h)
        }
    }
    $beklenen = @($Kesitler.Split(',') | Where-Object { $_.Trim() }).Count * @($VtKbitler.Split(',') | Where-Object { $_.Trim() }).Count
    $tam = ($hucreler.Count -eq $beklenen)
    $k1 = $tam -and @($hucreler | Where-Object { $_.vt_kodlayici -ne 'hevc_videotoolbox' }).Count -eq 0
    $k2 = $tam -and @($hucreler | Where-Object { $null -eq $_.k2_ham -or $_.k2_ham -lt -0.3 }).Count -eq 0
    $k3 = $tam -and @($hucreler | Where-Object { $null -eq $_.k3_ham -or $_.k3_ham -gt 1.5 }).Count -eq 0
    $k4 = $tam -and @($hucreler | Where-Object { $_.vt_bantta -ne $true -or $_.vt_tavan -eq $true }).Count -eq 0
    $k5 = $tam -and @($hucreler | Where-Object { $null -eq $_.k5_ham -or $_.k5_ham -lt -0.3 }).Count -eq 0
    $hukum = [ordered]@{
        hucre = $hucreler.Count; beklenen = $beklenen
        K1_kodlayici = $k1; K2_yazilima_gore = $k2; K3_hiz = $k3; K4_bant_tavan = $k4; K5_hb_vt_ye_gore = $k5; N1_uydurma_bayrak = $n1
        gecti = ($k1 -and $k2 -and $k3 -and $k4 -and $k5 -and $n1)
        hucreler = @($hucreler)
    }
    ConvertTo-Json -Depth 6 -InputObject $hukum | Set-Content (Join-Path $Cikti 'vthizli-hukum.json')
    Write-Host (ConvertTo-Json -Depth 6 -InputObject $hukum)
    if (-not $hukum.gecti) {
        throw "VT hizli kapisi kaldi: hucre $($hucreler.Count)/$beklenen, K1=$k1 K2=$k2 K3=$k3 K4=$k4 K5=$k5 N1=$n1 (vthizli-hukum.json)"
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
    [pscustomobject]@{ Sn1 = $r1.Sn; Sn2 = $r2.Sn; Sn = [math]::Round($r1.Sn + $r2.Sn, 1); IstatDosya = $istat; Uyari1 = (Uyarilar $r1.Metin); Uyari2 = (Uyarilar $r2.Metin); SvtOnayar1 = (SvtOnayar $r1.Metin); SvtOnayar2 = (SvtOnayar $r2.Metin); Metin = ($r1.Metin + "`n" + $r2.Metin) }
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

function IkiOkuma([string]$Ref, [string]$Test, [switch]$Ucuncu, [string[]]$TestGirdiArg = @()) {
    $rb = Probe $Ref
    $tb = Probe $Test
    $s = [ordered]@{ test_pix = $tb.Pix; yol_ii = 'zscale dither=none' }
    $cambi = "name=cambi\:enc_width=$($tb.W)\:enc_height=$($tb.H)"
    $zs = "zscale=w=$($rb.W):h=$($rb.H):filter=lanczos:dither=none:matrixin=709:matrix=709:transferin=709:transfer=709:primariesin=709:primaries=709:rangein=limited:range=limited,format=yuv420p"
    $okumalar = [ordered]@{
        i = @("scale=w=$($rb.W):h=$($rb.H):flags=lanczos,format=yuv420p10le", 'format=yuv420p10le')
        ii = @($zs, 'format=yuv420p')
    }
    if ($Ucuncu) { $okumalar.iii = @($zs.Replace('dither=none', 'dither=ordered'), 'format=yuv420p') }
    foreach ($ad in $okumalar.Keys) {
        $t = $okumalar[$ad][0]
        $r = $okumalar[$ad][1]
        $s["cambi_$ad"] = $null; $s["vmafneg_ort_$ad"] = $null; $s["vmafneg_harm_$ad"] = $null; $s["xpsnr_$ad"] = $null; $s["hata_$ad"] = $null
        try {
            $log = Join-Path $Cikti ('vmaf-' + [guid]::NewGuid().ToString('N') + '.json')
            $g = "[0:v]$t,settb=AVTB,setpts=N[t];[1:v]$r,settb=AVTB,setpts=N[r];[t][r]libvmaf=model=version=vmaf_v0.6.1neg:feature='$cambi':log_fmt=json:log_path=$(Kacis $log):n_threads=4"
            $cikis = & ffmpeg -hide_banner -nostdin @TestGirdiArg -i $Test -i $Ref -lavfi $g -f null - 2>&1 | Out-String
            if (-not (Test-Path $log) -and $ad -eq 'ii') {
                $t = "scale=w=$($rb.W):h=$($rb.H):flags=lanczos:sws_dither=none,format=yuv420p"
                $s.yol_ii = "swscale sws_dither=none (zscale kosmadi: $(Uyarilar $cikis))"
                $g = "[0:v]$t,settb=AVTB,setpts=N[t];[1:v]$r,settb=AVTB,setpts=N[r];[t][r]libvmaf=model=version=vmaf_v0.6.1neg:feature='$cambi':log_fmt=json:log_path=$(Kacis $log):n_threads=4"
                $cikis = & ffmpeg -hide_banner -nostdin @TestGirdiArg -i $Test -i $Ref -lavfi $g -f null - 2>&1 | Out-String
            }
            if (-not (Test-Path $log)) { throw "libvmaf gunlugu yok: $($cikis.Substring([math]::Max(0, $cikis.Length - 400)))" }
            $p = (Get-Content $log -Raw | ConvertFrom-Json).pooled_metrics
            $c = $p.PSObject.Properties | Where-Object { $_.Name -like 'cambi*' } | Select-Object -First 1
            if ($c) { $s["cambi_$ad"] = [math]::Round($c.Value.mean, 4) }
            if ($p.vmaf) { $s["vmafneg_ort_$ad"] = [math]::Round($p.vmaf.mean, 4); $s["vmafneg_harm_$ad"] = [math]::Round($p.vmaf.harmonic_mean, 4) }
            Remove-Item $log -ErrorAction SilentlyContinue
            $gx = "[0:v]$t,settb=AVTB,setpts=N[t];[1:v]$r,settb=AVTB,setpts=N[r];[t][r]xpsnr"
            $cx = & ffmpeg -hide_banner -nostdin @TestGirdiArg -i $Test -i $Ref -lavfi $gx -f null - 2>&1 | Out-String
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

function SvtAnahtarSatirlari([string]$Metin) {
    ((($Metin -split "`n") | Where-Object { $_ -match '(?i)luminance|variance|compress|tune|grain|Error parsing' } | ForEach-Object { ($_ -replace '^\s*\[[^\]]*\]\s*', '').Trim() } | Select-Object -Unique -First 12) -join ' // ')
}

function SvtBant {
    $script:FfSeviye = 'info'
    $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    $b = Probe $girdi
    $kbitler = if ($Kesit -eq 'karanlik') { @($Kbitler.Split(',') | ForEach-Object { [int]$_.Trim() }) } else { @([int]($Kbitler.Split(',')[-1].Trim())) }
    $sr = FfKos @('-f', 'lavfi', '-i', 'testsrc2=s=320x240:d=0.2', '-c:v', 'libsvtav1', '-f', 'null', $NullCikis) 'info'
    $svtSurum = (($sr.Metin -split "`n") | Where-Object { $_ -match 'SVT' -and $_ -match 'v\d' } | ForEach-Object { $_.Trim() } | Select-Object -First 2) -join ' / '
    Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'olcer-kaynak-kendisi'; svt_surum = $svtSurum }) $null (IkiOkuma $girdi $girdi -Ucuncu)
    if ($Kesit -eq 'karanlik') {
        $k10 = Join-Path $Cikti "kesit-$Kesit-10bit.mkv"
        Ff @('-i', $girdi, '-c:v', 'ffv1', '-pix_fmt', 'yuv420p10le', $k10)
        Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'olcer-iii-kaynak-10bit' }) $null (IkiOkuma $girdi $k10 -Ucuncu)
        Remove-Item $k10
        $alti = Join-Path $Cikti "kesit-$Kesit-6bit.mkv"
        Ff @('-i', $girdi, '-vf', 'lutyuv=y=bitand(val\,252)', '-c:v', 'ffv1', '-pix_fmt', 'yuv420p', $alti)
        Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'olcer-iii-kaynak-6bit' }) $null (IkiOkuma $girdi $alti -Ucuncu)
        Remove-Item $alti
        $gr = Join-Path $Cikti 'olcer-gradients-10bit.mkv'
        Ff @('-f', 'lavfi', '-i', "gradients=s=$($b.W)x$($b.H):c0=0x000000:c1=0x303030:x0=0:y0=0:x1=$($b.W - 1):y1=0:speed=0:r=24:d=2", '-vf', 'format=yuv420p10le', '-c:v', 'ffv1', '-pix_fmt', 'yuv420p10le', $gr)
        $gr8 = Join-Path $Cikti 'olcer-gradients-8bit-ref.mkv'
        Ff @('-i', $gr, '-vf', 'format=yuv420p', '-c:v', 'ffv1', '-pix_fmt', 'yuv420p', $gr8)
        Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'olcer-iii-gradients-10bit-gurultusuz' }) $null (IkiOkuma $gr8 $gr -Ucuncu)
        Remove-Item $gr, $gr8
    }
    $g = [int][math]::Round($b.Fps * 5)
    $psy = 'tune=1:enable-variance-boost=0'
    $kollar = [ordered]@{
        'e0-8bit' = @{ Psy = $psy; Ek = '' }
        'negatif-uydurma-anahtar' = @{ Psy = $psy; Ek = 'vidshrinkuydurma=1' }
        'lqb20' = @{ Psy = $psy; Ek = 'luminance-qp-bias=20' }
        'lqb40' = @{ Psy = $psy; Ek = 'luminance-qp-bias=40' }
        'lqb60' = @{ Psy = $psy; Ek = 'luminance-qp-bias=60' }
        'vb3' = @{ Psy = 'tune=1:enable-variance-boost=1:variance-boost-strength=3'; Ek = '' }
        'qsc2' = @{ Psy = $psy; Ek = 'qp-scale-compress-strength=2' }
        'tune0' = @{ Psy = 'tune=0:enable-variance-boost=0'; Ek = '' }
        'ortu-fg4' = @{ Psy = $psy; Ek = 'film-grain=4:film-grain-denoise=0' }
    }
    foreach ($kbit in $kbitler) {
        $script:bantTaban = $null
        foreach ($kol in $kollar.Keys) {
            Dene $Kesit $kol $kbit {
                $t = $kollar[$kol]
                $c = Join-Path $Cikti "svtbant-$Kesit-$kbit-$kol.mkv"
                $prm = "keyint=${g}:scd=1:$($t.Psy)"
                if ($t.Ek) { $prm += ":$($t.Ek)" }
                $hedef = if ($script:bantTaban) { $script:bantTaban } else { [double]$kbit }
                $h = HamEsBayt $c $hedef $kbit { param($bb, $cc) Gecisli $girdi $cc @('-c:v', 'libsvtav1', '-preset', '6', '-b:v', "${bb}k", '-g', "$g", '-svtav1-params', $prm, '-pix_fmt', 'yuv420p') @() @() }
                if ($kol -eq 'e0-8bit') { $script:bantTaban = $h.Kbps }
                $o = [ordered]@{ bayt = (Get-Item $c).Length; kbps = $h.Kbps }
                $ek = EsOrtak $h $hedef
                $ek.svtav1_params = $prm
                $ek.anahtar_satirlari = SvtAnahtarSatirlari $h.R.Metin
                $ek.anahtar_reddedildi = ($h.R.Metin -match 'Error parsing')
                $ok = IkiOkuma $girdi $c -Ucuncu
                foreach ($k in $ok.Keys) { $ek[$k] = $ok[$k] }
                if ($kol -like 'ortu-*') {
                    $gs = IkiOkuma $girdi $c -TestGirdiArg @('-export_side_data', 'film_grain')
                    $ek.grainsiz_cambi_ii = $gs.cambi_ii
                    $ek.grainsiz_hata_ii = $gs.hata_ii
                }
                Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = $kol; istenen_kbit = $kbit }) $o $ek
                Remove-Item $c
            }
        }
        if (-not $script:bantTaban) { continue }
        Dene $Kesit 'urun-x265' $kbit {
            $mb = [math]::Round($script:bantTaban * $b.Sure / 8 / 1024, 4)
            $u = Urun $girdi $mb "svtbant-$Kesit-$kbit-urun-x265" @('--force-codec', 'libx265', '--no-resolution-drop', '--no-fps-drop')
            $ek = UrunOrtak $u $mb
            $ok = IkiOkuma $girdi $u.Dosya -Ucuncu
            foreach ($k in $ok.Keys) { $ek[$k] = $ok[$k] }
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'urun-x265'; istenen_kbit = $kbit }) ([ordered]@{ bayt = (Get-Item $u.Dosya).Length; kbps = (Kbps $u.Dosya) }) $ek
            Remove-Item $u.Dosya
        }
        if ($Kesit -ne 'karanlik') { continue }
        Dene $Kesit 'handbrake-x265' $kbit {
            $c = Join-Path $Cikti "svtbant-$Kesit-$kbit-handbrake.mkv"
            $h = HbEsBayt $girdi $c $script:bantTaban (HbTemel $b)
            $ek = HbOrtak $h $script:bantTaban
            $ok = IkiOkuma $girdi $c -Ucuncu
            foreach ($k in $ok.Keys) { $ek[$k] = $ok[$k] }
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'handbrake-x265'; istenen_kbit = $kbit; kodlayici = 'HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo' }) ([ordered]@{ bayt = (Get-Item $c).Length; kbps = $h.Kbps }) $ek
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

function LumaPencereleri([double]$Sure) {
    if ($Sure -le 3) { return @(0.0) }
    $kullanilir = [math]::Max(0.0, $Sure - 2)
    $adet = if ($Sure -lt 12) { 2 } else { 3 }
    @(for ($i = 0; $i -lt $adet; $i++) { $kullanilir * ($i + 0.5) / $adet })
}

function LumaOku([string]$Girdi, [double]$Bas, [double]$Uzunluk) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $a = @('-hide_banner', '-nostdin', '-ss', $Bas.ToString('0.###', $Inv), '-t', $Uzunluk.ToString('0.###', $Inv), '-i', $Girdi, '-an', '-sn', '-dn', '-vf', 'fps=4,scale=160:-2,format=yuv420p,signalstats,metadata=print:key=lavfi.signalstats.YAVG', '-f', 'null', $NullCikis)
    $m = & ffmpeg @a 2>&1 | Out-String
    $sw.Stop()
    if ($LASTEXITCODE -ne 0) { throw "signalstats basarisiz: $Girdi" }
    $d = @([regex]::Matches($m, 'lavfi\.signalstats\.YAVG=([\d.]+)') | ForEach-Object { [double]::Parse($_.Groups[1].Value, $Inv) })
    [pscustomobject]@{ Kare = $d.Count; Ort = if ($d.Count) { [math]::Round(($d | Measure-Object -Average).Average, 2) } else { $null }; Ms = $sw.ElapsedMilliseconds; Komut = ($a -join ' ') }
}

function Yavg {
    $secim = $null
    $kj = Join-Path $Cikti 'kesitler.json'
    if (Test-Path $kj) { $secim = (Get-Content $kj -Raw | ConvertFrom-Json).Secim }
    foreach ($k in @('siyah', 'beyaz')) {
        Dene $k 'olcer-sentetik' $null {
            $renk = if ($k -eq 'siyah') { 'black' } else { 'white' }
            $c = Join-Path $Cikti "yavg-$k.mkv"
            Ff @('-f', 'lavfi', '-i', "color=c=${renk}:s=1920x1080:r=24:d=4", '-c:v', 'ffv1', '-pix_fmt', 'yuv420p', $c)
            $p = LumaOku $c 1 2
            Ekle ([ordered]@{ is = $Is; kesit = $k; kol = 'olcer-sentetik' }) $null ([ordered]@{ yavg_pencere_ort = $p.Ort; kare = $p.Kare })
            Remove-Item $c
        }
    }
    foreach ($k in @($Kesitler.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ })) {
        Dene $k 'yavg' $null {
            $girdi = Join-Path $Cikti "kesit-$k.mkv"
            $b = Probe $girdi
            $pencereler = @(LumaPencereleri $b.Sure)
            $okumalar = @($pencereler | ForEach-Object { LumaOku $girdi $_ 2 })
            $tam = LumaOku $girdi 0 $b.Sure
            $ek = [ordered]@{
                sure = $b.Sure; geometri = "$($b.W)x$($b.H)"
                pencere_baslari = (($pencereler | ForEach-Object { $_.ToString('0.###', $Inv) }) -join ',')
                pencere_yavg = (($okumalar | ForEach-Object { $_.Ort }) -join ',')
                yavg_pencere_ort = [math]::Round(($okumalar | Measure-Object -Property Ort -Average).Average, 2)
                yavg_tam_kesit = $tam.Ort
                kesit_secimi_yavg = if ($secim -and $secim.$k -and $secim.$k.PSObject.Properties['YavgOrt']) { $secim.$k.YavgOrt } else { $null }
                pencere_ms = (($okumalar | ForEach-Object { $_.Ms }) -join ',')
                komut = $okumalar[0].Komut
            }
            Ekle ([ordered]@{ is = $Is; kesit = $k; kol = 'yavg' }) $null $ek
        }
    }
}

function KaynakBasligi([string]$Log) {
    $s = Get-Content $Log | Where-Object { $_ -like 'kaynak *' } | Select-Object -First 1
    $o = [ordered]@{ prob_ms = $null; luma = $null }
    if ($s -match 'prob-ms (\d+)') { $o.prob_ms = [int]$Matches[1] }
    if ($s -match '\| luma ([\d.]+) \|') { $o.luma = Sayi $Matches[1] }
    $o
}

function KaranlikGecis {
    if (-not $BenchNeg) { throw 'karanlikgecis icin -BenchNeg (main 460ecc89 bench) gerekli.' }
    $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    $b = Probe $girdi
    $karanlik = ($Kesit -eq 'karanlik')
    $kbitler = if ($karanlik) { @($Kbitler.Split(',') | ForEach-Object { [int]$_.Trim() }) } else { @([int]($Kbitler.Split(',')[-1].Trim())) }
    $beklenen = if ($karanlik) { 'libx265' } else { 'libsvtav1' }
    foreach ($kbit in $kbitler) {
        $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
        $script:kgUrun = $null
        $script:kgAna = $null
        Dene $Kesit 'urun-otomatik' $kbit {
            $ad = "kg-$Kesit-$kbit-urun"
            $u = Urun $girdi $mb $ad
            $ek = UrunOrtak $u $mb
            foreach ($p in (KaynakBasligi (Join-Path $Cikti "$ad.log")).GetEnumerator()) { $ek[$p.Key] = $p.Value }
            $ek.beklenen_kodek = $beklenen
            $ek.kodek_hukmu = if ($u.Kodlayici -eq $beklenen -and $u.Komut -like "*$beklenen*") { 'gecti' } else { 'kaldi' }
            $ek.md5 = AkisMd5 $u.Dosya
            $ok = IkiOkuma $girdi $u.Dosya
            foreach ($k in $ok.Keys) { $ek[$k] = $ok[$k] }
            if ($karanlik) { $ek.cambi_hukmu = if ($null -ne $ek.cambi_ii -and $ek.cambi_ii -le 7.5) { 'gecti' } else { 'kaldi' } }
            $script:kgUrun = [pscustomobject]@{ Kbps = (Kbps $u.Dosya); Sn = $u.KodlamaSn; Toplam = $u.ToplamSn; Md5 = $ek.md5; Vmaf = $ek.vmafneg_ort_ii; Xpsnr = $ek.xpsnr_ii; Cambi = $ek.cambi_ii; Kodlayici = $u.Kodlayici }
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'urun-otomatik'; istenen_kbit = $kbit }) ([ordered]@{ bayt = (Get-Item $u.Dosya).Length; kbps = $script:kgUrun.Kbps }) $ek
            Remove-Item $u.Dosya
        }
        Dene $Kesit 'e0-main-460ecc89' $kbit {
            $ad = "kg-$Kesit-$kbit-e0"
            $u = Urun $girdi $mb $ad -BenchYolu $BenchNeg
            $ek = UrunOrtak $u $mb
            foreach ($p in (KaynakBasligi (Join-Path $Cikti "$ad.log")).GetEnumerator()) { $ek[$p.Key] = $p.Value }
            $ek.md5 = AkisMd5 $u.Dosya
            $ok = IkiOkuma $girdi $u.Dosya
            foreach ($k in $ok.Keys) { $ek[$k] = $ok[$k] }
            if ($script:kgUrun) {
                $ek.urun_md5_es = ($script:kgUrun.Md5 -eq $ek.md5)
                if ($null -ne $script:kgUrun.Vmaf -and $null -ne $ek.vmafneg_ort_ii) { $ek.urun_eksi_e0_vmafneg = [math]::Round($script:kgUrun.Vmaf - $ek.vmafneg_ort_ii, 4) }
                if ($null -ne $script:kgUrun.Xpsnr -and $null -ne $ek.xpsnr_ii) { $ek.urun_eksi_e0_xpsnr = [math]::Round($script:kgUrun.Xpsnr - $ek.xpsnr_ii, 4) }
                if (-not $karanlik) { $ek.negatif_hukmu = if ($ek.urun_md5_es -or ($ek.Contains('urun_eksi_e0_vmafneg') -and $ek.Contains('urun_eksi_e0_xpsnr') -and [math]::Abs($ek.urun_eksi_e0_vmafneg) -le 0.05 -and [math]::Abs($ek.urun_eksi_e0_xpsnr) -le 0.02)) { 'gecti' } else { 'kaldi' } }
                if ($karanlik -and $u.KodlamaSn) { $ek.urun_bolu_e0_sure = [math]::Round($script:kgUrun.Sn / $u.KodlamaSn, 3) }
            }
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'e0-main-460ecc89'; istenen_kbit = $kbit }) ([ordered]@{ bayt = (Get-Item $u.Dosya).Length; kbps = (Kbps $u.Dosya) }) $ek
            Remove-Item $u.Dosya
        }
        if (-not $karanlik -or -not $script:kgUrun) { continue }
        Dene $Kesit 'handbrake-x265' $kbit {
            $c = Join-Path $Cikti "kg-$Kesit-$kbit-handbrake.mkv"
            $h = HbEsBayt $girdi $c $script:kgUrun.Kbps (HbTemel $b)
            $ek = HbOrtak $h $script:kgUrun.Kbps
            $ok = IkiOkuma $girdi $c
            foreach ($k in $ok.Keys) { $ek[$k] = $ok[$k] }
            $ek.urun_kodlama_sn = $script:kgUrun.Sn
            $ek.urun_toplam_sn = $script:kgUrun.Toplam
            $sureOrani = $script:kgUrun.Sn / $h.Sn
            $ek.urun_bolu_hb_sure = [math]::Round($sureOrani, 3)
            $ek.urun_toplam_bolu_hb_sure = [math]::Round($script:kgUrun.Toplam / $h.Sn, 3)
            $ek.sure_hukmu = if ($sureOrani -le 1.5) { 'gecti' } else { 'kaldi' }
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'handbrake-x265'; istenen_kbit = $kbit; kodlayici = 'HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo' }) ([ordered]@{ bayt = (Get-Item $c).Length; kbps = $h.Kbps }) $ek
            Remove-Item $c
        }
        Dene $Kesit 'urun-dengeli' $kbit {
            $ad = "kg-$Kesit-$kbit-dengeli"
            $kaynakMb = [math]::Round($mb * $DengeliOran, 4)
            $u = Urun $girdi $mb $ad -Ek @('--source-mb', $kaynakMb.ToString('0.####', $Inv))
            $ek = UrunOrtak $u $mb
            foreach ($p in (KaynakBasligi (Join-Path $Cikti "$ad.log")).GetEnumerator()) { $ek[$p.Key] = $p.Value }
            $ek.kaynak_mb = $kaynakMb
            $ek.rejim_orani = $DengeliOran
            $ek.beklenen_rejim = 'Balanced'
            $ek.beklenen_kodek = 'libx264'
            $ek.kodek_hukmu = if ($u.Kodlayici -eq 'libx264' -and $u.Komut -like '*libx264*') { 'gecti' } else { 'kaldi' }
            $ek.md5 = AkisMd5 $u.Dosya
            $ok = IkiOkuma $girdi $u.Dosya
            foreach ($k in $ok.Keys) { $ek[$k] = $ok[$k] }
            $ek.cambi_tavan = 7.5
            $ek.genisleme_kapisi = if ($null -eq $ek.cambi_ii) { 'olculemedi' } elseif ($ek.cambi_ii -gt 7.5) { 'genislet' } else { 'kalsin' }
            if ($script:kgUrun) {
                $ek.x265_kodlayici = $script:kgUrun.Kodlayici
                if ($null -ne $script:kgUrun.Cambi -and $null -ne $ek.cambi_ii) { $ek.x265_eksi_dengeli_cambi = [math]::Round($script:kgUrun.Cambi - $ek.cambi_ii, 4) }
                if ($null -ne $script:kgUrun.Vmaf -and $null -ne $ek.vmafneg_ort_ii) { $ek.x265_eksi_dengeli_vmafneg = [math]::Round($script:kgUrun.Vmaf - $ek.vmafneg_ort_ii, 4) }
                if ($null -ne $script:kgUrun.Xpsnr -and $null -ne $ek.xpsnr_ii) { $ek.x265_eksi_dengeli_xpsnr = [math]::Round($script:kgUrun.Xpsnr - $ek.xpsnr_ii, 4) }
                if ($u.KodlamaSn) {
                    $ek.x265_bolu_dengeli_sure = [math]::Round($script:kgUrun.Sn / $u.KodlamaSn, 3)
                    $ek.genisleme_sure_hukmu = if ($ek.x265_bolu_dengeli_sure -le 2.0) { 'gecti' } else { 'kaldi' }
                }
            }
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'urun-dengeli'; istenen_kbit = $kbit }) ([ordered]@{ bayt = (Get-Item $u.Dosya).Length; kbps = (Kbps $u.Dosya) }) $ek
            Remove-Item $u.Dosya
        }
    }
}

$script:CliKapi = [ordered]@{ vmafneg_bant = -0.3; xpsnr_bant = -0.2; karanlik_psnr_taban = -0.5; cambi_tavan = 1.0; bayt_sapma_yuzde = 2.0; hiz_orani_tavan = 1.0 }

function UrunCli([string]$Girdi, [double]$Mb, [string]$Ad, [string[]]$Ek = @()) {
    if (-not $Cli) { throw 'handbrakecli icin -Cli gerekli.' }
    $klasor = Join-Path $Cikti $Ad
    New-Item -ItemType Directory -Force $klasor | Out-Null
    $cikis = Join-Path $klasor 'cikti.mp4'
    $json = Join-Path $Cikti "$Ad.json.txt"
    $log = Join-Path $Cikti "$Ad.log"
    $a = @('kucult', $Girdi, '--hedef', $Mb.ToString('0.####', $Inv), '--cikti', $cikis, '--json') + $Ek
    "cli: $Cli $($a -join ' ')" | Out-File $log
    $sure = [Diagnostics.Stopwatch]::StartNew()
    if ($Cli.EndsWith('.dll')) { & dotnet $Cli @a 2>> $log > $json } else { & $Cli @a 2>> $log > $json }
    $cikisKodu = $LASTEXITCODE
    $sure.Stop()
    if ($cikisKodu -notin @(0, 2, 3)) { throw "cli cikis $cikisKodu : $Ad" }
    $j = Get-Content $json -Raw | ConvertFrom-Json
    if (-not $j.result.success -or -not (Test-Path $j.result.output)) { throw "cli ciktisi yok (cikis $cikisKodu): $Ad" }
    $args2 = @($j.arguments)
    $preset = $null; $pix = $null
    for ($i = 0; $i -lt $args2.Count - 1; $i++) {
        if ($args2[$i] -eq '-preset') { $preset = $args2[$i + 1] }
        if ($args2[$i] -eq '-pix_fmt') { $pix = $args2[$i + 1] }
    }
    $iz = IzSureleri $j.result.trace ([double]$j.result.elapsedSeconds)
    [pscustomobject]@{
        Dosya = $j.result.output; CikisKodu = $cikisKodu
        ToplamSn = [math]::Round($sure.Elapsed.TotalSeconds, 1); KodlamaSn = [math]::Round([double]$j.result.elapsedSeconds, 1)
        Kodlayici = $j.plan.codec; Preset = $preset; Pix = $pix; Mod = $j.plan.mode
        Geometri = "$($j.plan.width)x$($j.plan.height)@$($j.plan.fps)"
        Deneme = $j.result.attempts; AltBant = $j.result.underBand; TavanAsildi = $j.result.ceilingExceeded; Tasma = $j.result.overTarget
        Olculdu = $j.measured
        Dallar = ((@($j.result.trace) | ForEach-Object { "$($_.number):$($_.branch):$($_.videoBitrateK)k:$($_.aimMb)->$($_.actualMb)" }) -join ' | ')
        IlkDenemeSn = $iz.Ilk; DenemeSnToplami = $iz.Toplam; OlcumDisiSn = $iz.Disi; DenemeSureleri = $iz.Metin; DenemeSnEksik = $iz.Eksik
        Komut = $j.commandLine
    }
}

function IzSureleri($Trace, [double]$KodlamaSn) {
    $ler = @($Trace)
    $sn = @($ler | ForEach-Object { if ($null -eq $_.seconds) { $null } else { [double]$_.seconds } })
    $eksik = ($ler.Count -eq 0) -or (@($sn | Where-Object { $null -eq $_ }).Count -gt 0)
    if ($eksik) { return [pscustomobject]@{ Ilk = $null; Toplam = $null; Disi = $null; Metin = $null; Eksik = $true } }
    $toplam = ($sn | Measure-Object -Sum).Sum
    [pscustomobject]@{
        Ilk = [math]::Round($sn[0], 1)
        Toplam = [math]::Round($toplam, 1)
        Disi = [math]::Round($KodlamaSn - $toplam, 1)
        Metin = (($ler | ForEach-Object { "$($_.number):$([math]::Round([double]$_.seconds, 1))sn" }) -join ' | ')
        Eksik = $false
    }
}

$script:HbSvtPresetEslemesi = @{
    veryslow = '4'; slower = '5'; slow = '6'; medium = '8'
    fast = '9'; faster = '10'; veryfast = '11'; ultrafast = '12'
}

function HbSvtPresetNo([string]$Preset) {
    if (-not $Preset) { return '8' }
    if ($Preset -match '^\d+$') { return $Preset }
    $ad = $Preset.ToLowerInvariant()
    if ($script:HbSvtPresetEslemesi.ContainsKey($ad)) { return $script:HbSvtPresetEslemesi[$ad] }
    return '8'
}

function HbSvtArg($b, [string]$Preset, [string]$Pix) {
    $e = if ($Pix -and $Pix -like '*10*') { 'svt_av1_10bit' } else { 'svt_av1' }
    $p = HbSvtPresetNo $Preset
    @('-Z', 'H.265 MKV 1080p30', '-e', $e, '--encoder-preset', $p, '-a', 'none', '--crop-mode', 'none', '--width', "$($b.W)", '--height', "$($b.H)", '-r', $b.Fps.ToString('0.###', $Inv), '--cfr')
}

function Fark($a, $b) { if ($null -eq $a -or $null -eq $b) { $null } else { [math]::Round([double]$a - [double]$b, 4) } }

$script:ButceKodekleri = [ordered]@{ x265 = 'libx265'; h264 = 'libx264' }

function ButceIlk {
    $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    $b = Probe $girdi
    foreach ($kbit in @($Kbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
        $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
        $script:bu = @{}
        foreach ($ad in $script:ButceKodekleri.Keys) {
            $beklenen = $script:ButceKodekleri[$ad]
            Dene $Kesit "urun-$ad" $kbit {
                $u = UrunCli $girdi $mb "butce-$Kesit-$kbit-$ad" @('--kodek', $ad)
                $kbps = Kbps $u.Dosya
                $script:bu[$ad] = [pscustomobject]@{ U = $u; Kbps = $kbps }
                Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = "urun-$ad"; istenen_kbit = $kbit }) $null ([ordered]@{
                    urun_yolu = 'cli'; hedef_mb = $mb; cli_cikis = $u.CikisKodu; zorlanan_kodek = $ad; beklenen_kodek = $beklenen
                    kodlayici = $u.Kodlayici; kodek_tuttu = ($u.Kodlayici -eq $beklenen); preset = $u.Preset; mod = $u.Mod
                    geometri = $u.Geometri; cikti_kbps = $kbps; kodlama_sn = $u.KodlamaSn; toplam_sn = $u.ToplamSn
                    deneme = $u.Deneme; deneme_sureleri = $u.DenemeSureleri; deneme_sn_eksik = $u.DenemeSnEksik
                    ilk_deneme_sn = $u.IlkDenemeSn; deneme_sn_toplami = $u.DenemeSnToplami; olcum_disi_sn = $u.OlcumDisiSn
                    alt_bant = $u.AltBant; tavan_asildi = $u.TavanAsildi; tasma = $u.Tasma; olculdu = $u.Olculdu
                    dallar = $u.Dallar; komut = $u.Komut })
                Remove-Item $u.Dosya
            }
        }
        if (-not $script:bu['x265']) { continue }
        $hk = $script:bu['x265'].Kbps
        $script:bhx = $null
        Dene $Kesit 'handbrake' $kbit {
            $c = Join-Path $Cikti "butce-$Kesit-$kbit-handbrake.mkv"
            $h = HbEsBayt $girdi $c $hk (HbTemel $b)
            $script:bhx = $h
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'handbrake'; istenen_kbit = $kbit; kodlayici = 'HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo' }) $null (HbOrtak $h $hk)
            Remove-Item $c
        }
        if (-not $script:bhx) { continue }
        $k = $script:CliKapi
        $s = [ordered]@{ is = $Is; kesit = $Kesit; kol = 'kapi'; istenen_kbit = $kbit; kapi = "hiz_orani_tavan=$($k.hiz_orani_tavan) bayt_sapma_yuzde=$($k.bayt_sapma_yuzde)" }
        $s.hb_sn = $script:bhx.Sn
        $s.hb_bayt_sapma_yuzde = $script:bhx.Sapma
        $s.es_bayt = [math]::Abs($script:bhx.Sapma) -le $k.bayt_sapma_yuzde
        foreach ($ad in $script:ButceKodekleri.Keys) {
            $r = $script:bu[$ad]
            if (-not $r) { continue }
            $u = $r.U
            $s["${ad}_kodlayici"] = $u.Kodlayici
            $s["${ad}_deneme"] = $u.Deneme
            $s["${ad}_deneme_sureleri"] = $u.DenemeSureleri
            $s["${ad}_kodlama_sn"] = $u.KodlamaSn
            $s["${ad}_toplam_sn"] = $u.ToplamSn
            $s["${ad}_ilk_deneme_sn"] = $u.IlkDenemeSn
            $s["${ad}_deneme_sn_toplami"] = $u.DenemeSnToplami
            $s["${ad}_olcum_disi_sn"] = $u.OlcumDisiSn
            $s["${ad}_oran_toplam"] = [math]::Round($u.ToplamSn / $script:bhx.Sn, 3)
            $s["${ad}_oran_kodlama"] = [math]::Round($u.KodlamaSn / $script:bhx.Sn, 3)
            $s["${ad}_b5_toplam"] = $s["${ad}_oran_toplam"] -le $k.hiz_orani_tavan
            if ($null -ne $u.IlkDenemeSn) {
                $s["${ad}_oran_ilk_deneme"] = [math]::Round($u.IlkDenemeSn / $script:bhx.Sn, 3)
                $s["${ad}_b5_ilk_deneme"] = $s["${ad}_oran_ilk_deneme"] -le $k.hiz_orani_tavan
                if ($u.Deneme -gt 1 -and $u.DenemeSnToplami -gt 0) {
                    $s["${ad}_ek_deneme_sn"] = [math]::Round($u.DenemeSnToplami - $u.IlkDenemeSn, 1)
                    $s["${ad}_ek_deneme_payi_yuzde"] = [math]::Round(($u.DenemeSnToplami - $u.IlkDenemeSn) / $u.ToplamSn * 100, 1)
                } else {
                    $s["${ad}_ek_deneme_sn"] = 0.0
                    $s["${ad}_ek_deneme_payi_yuzde"] = 0.0
                }
            }
        }
        Ekle $s $null $null
    }
}

function HandbrakeCli {
    $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    $b = Probe $girdi
    KaynakSatiri $girdi 'negatif-kaynak-kendisi'
    foreach ($kbit in @($Kbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
        $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
        $script:cu = $null; $script:co = $null; $script:hx = $null; $script:hxo = $null; $script:hs = $null; $script:no = $null
        Dene $Kesit 'urun-cli' $kbit {
            $u = UrunCli $girdi $mb "hbcli-$Kesit-$kbit-urun"
            $o = Olc $girdi $u.Dosya $b.FpsMetin
            $script:cu = $u; $script:co = $o
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'urun-cli'; istenen_kbit = $kbit }) $o ([ordered]@{
                urun_yolu = 'cli'; hedef_mb = $mb; cli_cikis = $u.CikisKodu; kodlayici = $u.Kodlayici; preset = $u.Preset; pix_fmt = $u.Pix; mod = $u.Mod
                geometri = $u.Geometri; kodlama_sn = $u.KodlamaSn; toplam_sn = $u.ToplamSn; deneme = $u.Deneme; alt_bant = $u.AltBant
                tavan_asildi = $u.TavanAsildi; tasma = $u.Tasma; olculdu = $u.Olculdu; dallar = $u.Dallar; komut = $u.Komut })
            Remove-Item $u.Dosya
        }
        if (-not $script:co) { continue }
        $hk = $script:co.kbps
        Dene $Kesit 'handbrake' $kbit {
            $c = Join-Path $Cikti "hbcli-$Kesit-$kbit-handbrake.mkv"
            $h = HbEsBayt $girdi $c $hk (HbTemel $b)
            $o = Olc $girdi $c $b.FpsMetin
            $script:hx = $h; $script:hxo = $o
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'handbrake'; istenen_kbit = $kbit; kodlayici = 'HandBrakeCLI 1.11.2 x265 slow 2 gecis turbo' }) $o (HbOrtak $h $hk)
            Remove-Item $c
        }
        Dene $Kesit 'handbrake-svtav1' $kbit {
            $c = Join-Path $Cikti "hbcli-$Kesit-$kbit-svtav1.mkv"
            $arg = HbSvtArg $b $script:cu.Preset $script:cu.Pix
            $h = HbEsBayt $girdi $c $hk $arg
            $o = Olc $girdi $c $b.FpsMetin
            $script:hs = [pscustomobject]@{ H = $h; O = $o }
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'handbrake-svtav1'; istenen_kbit = $kbit; kodlayici = "HandBrakeCLI 1.11.2 $($arg[3]) preset $($arg[5]) tek gecis" }) $o (HbOrtak $h $hk)
            Remove-Item $c
        }
        Dene $Kesit 'negatif-handbrake-yarim-bit' $kbit {
            $c = Join-Path $Cikti "hbcli-$Kesit-$kbit-negatif.mkv"
            $sn = HbKodla $girdi $c ((HbTemel $b) + @('-b', "$([int][math]::Round($hk / 2))"))
            $o = Olc $girdi $c $b.FpsMetin
            $script:no = $o
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'negatif-handbrake-yarim-bit'; istenen_kbit = $kbit; kodlayici = 'HandBrakeCLI 1.11.2 x265 slow'; kodlama_sn = $sn }) $o $null
            Remove-Item $c
        }
        $k = $script:CliKapi
        $s = [ordered]@{ is = $Is; kesit = $Kesit; kol = 'kapi'; istenen_kbit = $kbit; kapi = (($k.Keys | ForEach-Object { "$_=$($k[$_])" }) -join ' ') }
        if ($script:hxo) {
            $s.d_vmafneg = Fark $script:co.vmafneg_ort $script:hxo.vmafneg_ort
            $s.d_xpsnr = Fark $script:co.xpsnr $script:hxo.xpsnr
            $s.d_karanlik_psnr = Fark $script:co.karanlik_psnr $script:hxo.karanlik_psnr
            $s.d_cambi = Fark $script:co.cambi $script:hxo.cambi
            $s.bayt_sapma_yuzde = $script:hx.Sapma
            $s.es_bayt = [math]::Abs($script:hx.Sapma) -le $k.bayt_sapma_yuzde
            $s.b1_vmafneg = $null -ne $s.d_vmafneg -and $s.d_vmafneg -ge $k.vmafneg_bant
            $s.b1_xpsnr = $null -ne $s.d_xpsnr -and $s.d_xpsnr -ge $k.xpsnr_bant
            $s.b1_karanlik_psnr = $null -ne $s.d_karanlik_psnr -and $s.d_karanlik_psnr -ge $k.karanlik_psnr_taban
            $s.b1_cambi = $null -ne $s.d_cambi -and $s.d_cambi -le $k.cambi_tavan
            $s.b1 = $s.es_bayt -and $s.b1_vmafneg -and $s.b1_xpsnr -and $s.b1_karanlik_psnr -and $s.b1_cambi
            $s.hiz_orani_x265_toplam = [math]::Round($script:cu.ToplamSn / $script:hx.Sn, 3)
            $s.hiz_orani_x265_kodlama = [math]::Round($script:cu.KodlamaSn / $script:hx.Sn, 3)
            $s.b5_x265 = $s.hiz_orani_x265_toplam -le $k.hiz_orani_tavan
            $s.deneme = $script:cu.Deneme
            $s.deneme_sureleri = $script:cu.DenemeSureleri
            $s.ilk_deneme_sn = $script:cu.IlkDenemeSn
            $s.deneme_sn_toplami = $script:cu.DenemeSnToplami
            $s.olcum_disi_sn = $script:cu.OlcumDisiSn
            if ($null -ne $script:cu.IlkDenemeSn) {
                $s.hiz_orani_x265_ilk_deneme = [math]::Round($script:cu.IlkDenemeSn / $script:hx.Sn, 3)
                $s.b5_x265_ilk_deneme = $s.hiz_orani_x265_ilk_deneme -le $k.hiz_orani_tavan
            }
            if ($script:no) { $s.negatif_ayirdi = ($script:hxo.vmafneg_ort - $script:no.vmafneg_ort) -gt [math]::Abs($k.vmafneg_bant) }
        }
        if ($script:hs) {
            $s.svt_bayt_sapma_yuzde = $script:hs.H.Sapma
            $s.hiz_orani_svt_toplam = [math]::Round($script:cu.ToplamSn / $script:hs.H.Sn, 3)
            $s.hiz_orani_svt_kodlama = [math]::Round($script:cu.KodlamaSn / $script:hs.H.Sn, 3)
            $s.b5_svt = $s.hiz_orani_svt_toplam -le $k.hiz_orani_tavan
            $s.d_vmafneg_svt = Fark $script:co.vmafneg_ort $script:hs.O.vmafneg_ort
            $s.d_xpsnr_svt = Fark $script:co.xpsnr $script:hs.O.xpsnr
        }
        Ekle $s $null $null
    }
}

function HandbrakeCliSvt {
    $girdi = Join-Path $Cikti "kesit-$Kesit.mkv"
    $b = Probe $girdi
    foreach ($kbit in @($Kbitler.Split(',') | ForEach-Object { [int]$_.Trim() })) {
        $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
        $script:cu = $null; $script:co = $null; $script:hs = $null
        Dene $Kesit 'urun-cli' $kbit {
            $u = UrunCli $girdi $mb "hbsvt-$Kesit-$kbit-urun"
            $o = Olc $girdi $u.Dosya $b.FpsMetin
            $script:cu = $u; $script:co = $o
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'urun-cli'; istenen_kbit = $kbit }) $o ([ordered]@{
                urun_yolu = 'cli'; hedef_mb = $mb; cli_cikis = $u.CikisKodu; kodlayici = $u.Kodlayici; preset = $u.Preset; pix_fmt = $u.Pix; mod = $u.Mod
                geometri = $u.Geometri; kodlama_sn = $u.KodlamaSn; toplam_sn = $u.ToplamSn; deneme = $u.Deneme; alt_bant = $u.AltBant
                tavan_asildi = $u.TavanAsildi; tasma = $u.Tasma; olculdu = $u.Olculdu; dallar = $u.Dallar; komut = $u.Komut })
            Remove-Item $u.Dosya
        }
        if (-not $script:co) { continue }
        $hk = $script:co.kbps
        Dene $Kesit 'handbrake-svtav1' $kbit {
            $c = Join-Path $Cikti "hbsvt-$Kesit-$kbit-svtav1.mkv"
            $arg = HbSvtArg $b $script:cu.Preset $script:cu.Pix
            $h = HbEsBayt $girdi $c $hk $arg
            $o = Olc $girdi $c $b.FpsMetin
            $script:hs = [pscustomobject]@{ H = $h; O = $o }
            Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = 'handbrake-svtav1'; istenen_kbit = $kbit
                kodlayici = "HandBrakeCLI 1.11.2 $($arg[3]) preset $($arg[5]) tek gecis"
                urun_preset = $script:cu.Preset; hb_encoder_preset = $arg[5] }) $o (HbOrtak $h $hk)
            Remove-Item $c
        }
        $k = $script:CliKapi
        $s = [ordered]@{ is = $Is; kesit = $Kesit; kol = 'kapi'; istenen_kbit = $kbit; kapi = (($k.Keys | ForEach-Object { "$_=$($k[$_])" }) -join ' ') }
        if ($script:hs) {
            $s.svt_bayt_sapma_yuzde = $script:hs.H.Sapma
            $s.svt_es_bayt = [math]::Abs($script:hs.H.Sapma) -le $k.bayt_sapma_yuzde
            $s.hiz_orani_svt_toplam = [math]::Round($script:cu.ToplamSn / $script:hs.H.Sn, 3)
            $s.hiz_orani_svt_kodlama = [math]::Round($script:cu.KodlamaSn / $script:hs.H.Sn, 3)
            $s.b5_svt = $s.hiz_orani_svt_toplam -le $k.hiz_orani_tavan
            $s.d_vmafneg_svt = Fark $script:co.vmafneg_ort $script:hs.O.vmafneg_ort
            $s.d_xpsnr_svt = Fark $script:co.xpsnr $script:hs.O.xpsnr
        }
        Ekle $s $null $null
    }
}

function FiltreSatir([string]$Kol, [int]$Tekrar, [string]$Girdi, [string]$Ref, [double]$Mb, [int]$Kbit, [string[]]$Ek) {
    $ad = "filtre-$Kesit-$Kol-$Tekrar"
    $u = Urun $Girdi $Mb $ad (@('--no-resolution-drop', '--no-fps-drop') + $Ek)
    $rb = Probe $Ref
    $o = Olc $Ref $u.Dosya $rb.FpsMetin -EkYok
    $e = UrunOrtak $u $Mb
    $e.tekrar = $Tekrar
    $e.filtre = @(Get-Content (Join-Path $Cikti "$ad.log") | Where-Object { $_ -like 'filtre:*' } | Select-Object -Last 1) -join ''
    $e.yoklama = @(Get-Content (Join-Path $Cikti "$ad.log") | Where-Object { $_ -like 'yoklama:*' } | Select-Object -Last 1) -join ''
    $e.bwdif = [bool]($u.Komut -like '*bwdif*')
    Ekle ([ordered]@{ is = $Is; kesit = $Kesit; kol = $Kol; istenen_kbit = $Kbit }) $o $e
    Remove-Item $u.Dosya
}

function Filtre {
    $ffv1 = Join-Path $Cikti "kesit-$Kesit.mkv"
    $kbit = 2000
    $ara = Join-Path $Cikti "filtre-$Kesit-h264.mkv"
    Ff @('-i', $ffv1, '-an', '-sn', '-map', '0:v:0', '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '4', '-pix_fmt', 'yuv420p', $ara)
    $b = Probe $ara
    $mb = [math]::Round($kbit * $b.Sure / 8 / 1024, 4)
    $kollar = @{ kapali = @('--filters', 'deinterlace=off'); otomatik = @(); acik = @('--filters', 'deinterlace=on') }
    $sira = @(@(1, @('kapali', 'otomatik', 'acik')), @(2, @('acik', 'otomatik', 'kapali')))
    foreach ($s in $sira) {
        foreach ($kol in $s[1]) {
            Dene $Kesit $kol $kbit { FiltreSatir $kol $s[0] $ara $ara $mb $kbit $kollar[$kol] }
        }
    }
    $ozet = [ordered]@{ is = $Is; kesit = $Kesit; kol = 'hukum-progressive'; istenen_kbit = $kbit }
    $satir = @($Satirlar | Where-Object { $_.kesit -eq $Kesit -and $_.kol -in @('kapali', 'otomatik', 'acik') -and -not $_.PSObject.Properties['hata'] })
    $k = @($satir | Where-Object { $_.kol -eq 'kapali' })
    $ozet.kapali_deneme = (($k | ForEach-Object { $_.deneme }) -join '+')
    $kb = ($k | ForEach-Object { $_.kodlama_sn / $_.deneme } | Measure-Object -Minimum).Minimum
    foreach ($kol in @('otomatik', 'acik')) {
        $a = @($satir | Where-Object { $_.kol -eq $kol })
        $ozet["${kol}_deneme"] = (($a | ForEach-Object { $_.deneme }) -join '+')
        if ($k.Count -lt 2 -or $a.Count -lt 2) { $ozet["${kol}_hukum"] = 'eksik'; continue }
        $dv = [math]::Round((($a | Measure-Object vmafneg_ort -Average).Average) - (($k | Measure-Object vmafneg_ort -Average).Average), 4)
        $ab = ($a | ForEach-Object { $_.kodlama_sn / $_.deneme } | Measure-Object -Minimum).Minimum
        $ds = [math]::Round(($ab - $kb) / $kb * 100, 2)
        $ozet["${kol}_kodlama_sn_deneme_basi"] = [math]::Round($ab, 2)
        $ozet["${kol}_sure_yuzde"] = $ds
        $ozet["${kol}_hukum"] = if ([math]::Abs($dv) -lt 0.1 -and $ds -lt 5) { 'gecti' } else { 'kaldi' }
        $ozet["${kol}_delta_vmafneg"] = $dv
    }
    $ozet.kapali_kodlama_sn_deneme_basi = [math]::Round($kb, 2)
    $ozet.sure_olcutu = 'kodlama_sn/deneme, deneme sayisindan bagimsiz'
    Ekle $ozet $null $null
    Remove-Item $ara -ErrorAction SilentlyContinue

    if ($Kesit -ne 'hareketli') { return }
    $tar = Join-Path $Cikti 'filtre-taramali.mkv'
    $ref = Join-Path $Cikti 'filtre-taramali-ref.mkv'
    Ff @('-i', $ffv1, '-an', '-sn', '-map', '0:v:0', '-vf', 'crop=iw:trunc(ih/4)*4:0:0,tinterlace=mode=interleave_top,setfield=tff', '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '4', '-flags', '+ilme+ildct', '-pix_fmt', 'yuv420p', $tar)
    Ff @('-i', $ffv1, '-an', '-sn', '-map', '0:v:0', '-vf', "crop=iw:trunc(ih/4)*4:0:0,select=not(mod(n\,2)),fps=$((Probe $tar).FpsMetin)", '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '4', '-pix_fmt', 'yuv420p', $ref)
    $alan = (& ffprobe -v error -select_streams v:0 -show_entries stream=field_order -of csv=p=0 $tar | Out-String).Trim()
    $tb = Probe $tar
    $tmb = [math]::Round($kbit * $tb.Sure / 8 / 1024, 4)
    foreach ($kol in @('kapali', 'otomatik')) {
        Dene $Kesit "taramali-$kol" $kbit { FiltreSatir "taramali-$kol" 1 $tar $ref $tmb $kbit $kollar[$kol] }
    }
    $n = [ordered]@{ is = $Is; kesit = $Kesit; kol = 'hukum-taramali'; istenen_kbit = $kbit; field_order = $alan }
    $n.alan_progressive_degil = ($alan -and $alan -notlike 'progressive*')
    $tk = @($Satirlar | Where-Object { $_.kol -eq 'taramali-kapali' -and -not $_.PSObject.Properties['hata'] }) | Select-Object -First 1
    $ta = @($Satirlar | Where-Object { $_.kol -eq 'taramali-otomatik' -and -not $_.PSObject.Properties['hata'] }) | Select-Object -First 1
    if ($tk -and $ta) {
        $n.yoklama = $ta.yoklama
        $n.delta_vmafneg = [math]::Round($ta.vmafneg_ort - $tk.vmafneg_ort, 4)
        $n.hukum = if ($n.alan_progressive_degil -and $ta.bwdif -and -not $tk.bwdif -and $n.delta_vmafneg -ge 1.0) { 'gecti' } else { 'kaldi' }
    } else { $n.hukum = 'eksik' }
    Ekle $n $null $null
    Remove-Item $tar, $ref -ErrorAction SilentlyContinue

    $dv = Join-Path $Cikti 'filtre-belirsiz.dv'
    $dref = Join-Path $Cikti 'filtre-belirsiz-ref.mkv'
    Ff @('-i', $ffv1, '-an', '-sn', '-map', '0:v:0', '-vf', 'scale=720:576,setsar=1,fps=50,tinterlace=mode=interleave_top,setfield=tff,format=yuv420p', '-c:v', 'dvvideo', '-pix_fmt', 'yuv420p', $dv)
    $dalan = (& ffprobe -v error -select_streams v:0 -show_entries stream=field_order -of csv=p=0 $dv | Out-String).Trim()
    $db = Probe $dv
    Ff @('-i', $ffv1, '-an', '-sn', '-map', '0:v:0', '-vf', 'scale=720:576,setsar=1,fps=50,select=not(mod(n\,2)),fps=25', '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '4', '-pix_fmt', 'yuv420p', $dref)
    $dmb = [math]::Round($kbit * $db.Sure / 8 / 1024, 4)
    foreach ($kol in @('kapali', 'otomatik')) {
        Dene $Kesit "belirsiz-$kol" $kbit { FiltreSatir "belirsiz-$kol" 1 $dv $dref $dmb $kbit $kollar[$kol] }
    }
    $y = [ordered]@{ is = $Is; kesit = $Kesit; kol = 'hukum-belirsiz-alan'; istenen_kbit = $kbit; field_order = $dalan }
    $y.alan_belirsiz = ($dalan -eq '' -or $dalan -eq 'unknown')
    $bk = @($Satirlar | Where-Object { $_.kol -eq 'belirsiz-kapali' -and -not $_.PSObject.Properties['hata'] }) | Select-Object -First 1
    $ba = @($Satirlar | Where-Object { $_.kol -eq 'belirsiz-otomatik' -and -not $_.PSObject.Properties['hata'] }) | Select-Object -First 1
    if ($bk -and $ba) {
        $y.yoklama_kapali = $bk.yoklama
        $y.yoklama_otomatik = $ba.yoklama
        $y.idet_kostu = [bool]($ba.yoklama -like '*idet=kostu*')
        $y.delta_vmafneg = [math]::Round($ba.vmafneg_ort - $bk.vmafneg_ort, 4)
        $y.hukum = if ($y.alan_belirsiz -and $y.idet_kostu -and $ba.bwdif -and -not $bk.bwdif -and $y.delta_vmafneg -ge 1.0) { 'gecti' } else { 'kaldi' }
    } else { $y.hukum = 'eksik' }
    Ekle $y $null $null
    Remove-Item $dv, $dref -ErrorAction SilentlyContinue
}

switch ($Is) {
    'butceilk' { ButceIlk }
    'handbrakecli' { HandbrakeCli }
    'handbrakecli-svt' { HandbrakeCliSvt }
    'filtre' { Filtre }
    'karanlikgecis' { KaranlikGecis }
    'yavg' { Yavg }
    'svtbekci' { SvtBekci }
    'tavanbekci' { TavanBekci }
    'svtbant' { SvtBant }
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
    'vthizli' { VtHizli }
    'ekranbant' { EkranBant }
}
Yaz
$hatali = @($Satirlar | Where-Object { $_.PSObject.Properties['hata'] -and $_.hata })
Write-Host "satir=$($Satirlar.Count) hatali=$($hatali.Count)"
if ($Satirlar.Count -eq 0 -or $hatali.Count -eq $Satirlar.Count) { exit 1 }
