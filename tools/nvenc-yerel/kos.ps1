param(
    [string]$Calisma = '.calisma/nvenc',
    [string]$Kesitler = 'karanlik,parlak,hareketli',
    [string]$Kodekler = 'h264_nvenc,hevc_nvenc,av1_nvenc',
    [int[]]$Kbitler = @(2000),
    [string]$Negatif = 'hareketli',
    [string]$Json = 'sonuc.json'
)
$ErrorActionPreference = 'Stop'
$Inv = [Globalization.CultureInfo]::InvariantCulture
$D = (Resolve-Path $Calisma).Path
$env:VIDSHRINK_SETTINGS_PATH = Join-Path $D 'settings.json'
$Bench = (Resolve-Path (Join-Path $PSScriptRoot '..\VidShrink.Bench\bin\Release\net8.0\VidShrink.Bench.dll')).Path
$Hb = 'HandBrakeCLI'
$HbKod = @{ h264_nvenc = 'nvenc_h264'; hevc_nvenc = 'nvenc_h265'; av1_nvenc = 'nvenc_av1' }
$Satir = [Collections.Generic.List[object]]::new()
$script:KodlamaSn = 0.0

function Yaz { ConvertTo-Json -Depth 5 -InputObject @($Satir) | Set-Content (Join-Path $D $Json) }
function Probe([string]$Yol) {
    $j = & ffprobe -v error -select_streams v:0 -show_entries stream=width,height,r_frame_rate:format=duration -of json $Yol | ConvertFrom-Json
    [pscustomobject]@{ W = [int]$j.streams[0].width; H = [int]$j.streams[0].height; Fps = $j.streams[0].r_frame_rate; Sure = [double]::Parse($j.format.duration, $Inv) }
}
function Olc([string]$Ref, [string]$Test) {
    $o = Join-Path $D ([IO.Path]::GetFileNameWithoutExtension($Test) + '.olcu.json')
    & dotnet $Bench measure-pair $Ref $Test --fps (Probe $Ref).Fps --out $o *>&1 | Out-Null
    $m = Get-Content $o -Raw | ConvertFrom-Json
    [ordered]@{ bayt = $m.Bayt; kbps = [math]::Round($m.Kbps, 1); geometri = "$($m.Width)x$($m.Height)"; vmafneg_ort = [math]::Round($m.VmafNegMean, 2); vmafneg_harm = [math]::Round($m.VmafNegHarmonic, 2); vmafneg_p10 = [math]::Round($m.VmafNegP10, 2); xpsnr = [math]::Round($m.Xpsnr, 2) }
}
function Ekle { $s = [ordered]@{}; foreach ($x in $args) { if ($x) { foreach ($k in $x.Keys) { $s[$k] = $x[$k] } } }; $Satir.Add([pscustomobject]$s); Yaz }
function HbKodla([string]$Girdi, [string]$Cikis, [string[]]$Arg) {
    "=== $Cikis :: $($Arg -join ' ')" | Out-File -Append (Join-Path $D 'handbrake.log')
    $sw = [Diagnostics.Stopwatch]::StartNew()
    & $Hb -i $Girdi -o $Cikis @Arg *>&1 | Out-File -Append (Join-Path $D 'handbrake.log')
    $sw.Stop()
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Cikis)) { throw "HandBrakeCLI basarisiz: $Cikis" }
    $script:KodlamaSn += $sw.Elapsed.TotalSeconds
    [math]::Round($sw.Elapsed.TotalSeconds, 1)
}
function HbEsBayt([string]$Girdi, [string]$Cikis, [double]$HedefKbps, [string[]]$Arg) {
    $b = [int][math]::Round($HedefKbps); $iz = @(); $top = 0.0
    for ($i = 1; $i -le 3; $i++) {
        $sn = HbKodla $Girdi $Cikis ($Arg + @('-b', "$b")); $top += $sn
        $p = Probe $Cikis; $kbps = (Get-Item $Cikis).Length * 8 / 1000 / $p.Sure; $sap = $kbps / $HedefKbps - 1
        $iz += "${b}k->$([math]::Round($kbps,1))"
        if ([math]::Abs($sap) -le 0.02 -or $i -eq 3) { return [ordered]@{ hb_b = $b; hb_deneme = $i; hb_iz = ($iz -join ' | '); bayt_sapma_yuzde = [math]::Round($sap * 100, 2); kodlama_sn = $sn; toplam_sn = [math]::Round($top, 1) } }
        $b = [int][math]::Round($b * $HedefKbps / $kbps)
    }
}

foreach ($k in $Kesitler.Split(',')) {
    $girdi = Join-Path $D "kesit-$k.mkv"
    $kb = Probe $girdi
    foreach ($kbit in $Kbitler) {
        $mb = $kbit * $kb.Sure / 8 / 1024
        foreach ($c in $Kodekler.Split(',')) {
            $ortak = [ordered]@{ kesit = $k; istenen_kbit = $kbit; hedef_mb = [math]::Round($mb, 4); kodek = $c }
            try {
                $kl = Join-Path $D "urun-$k-$kbit-$c"
                $log = "$kl.log"
                $sw = [Diagnostics.Stopwatch]::StartNew()
                & dotnet $Bench shrink $girdi $mb.ToString('0.####', $Inv) --out $kl --speed quality --no-measure --lock-codec $c *>&1 | Out-File $log
                $sw.Stop(); $script:KodlamaSn += $sw.Elapsed.TotalSeconds
                $r = @(Get-Content (Join-Path $kl 'results.json') -Raw | ConvertFrom-Json)[0]
                $mp4 = Get-ChildItem $kl -Filter *.mp4 | Select-Object -First 1
                $txt = Get-Content $log
                $o = Olc $girdi $mp4.FullName
                Ekle $ortak ([ordered]@{ kol = 'urun'; plan_kodek = $r.Codec; plan_geometri = "$($r.Width)x$($r.Height)"; cikan_mb = [math]::Round($r.ActualMb, 4); doluluk = [math]::Round($r.FillPercent, 2); bantta = $r.InBand; tasma = $r.OverTarget; deneme = $r.Attempts; kodlama_sn = [math]::Round($r.EncodeSeconds, 1); toplam_sn = [math]::Round($sw.Elapsed.TotalSeconds, 1); dallar = (($txt | Where-Object { $_ -match '^\s*deneme' } | ForEach-Object { $_.Trim() }) -join ' | '); komut = ($txt | Where-Object { $_ -like 'komut:*' } | Select-Object -Last 1) }) $o
                $hk = $o.kbps
                $pg = Probe $mp4.FullName
                Remove-Item $mp4.FullName
                $arg = @('-Z', 'H.265 NVENC 1080p', '-e', $HbKod[$c], '-a', 'none', '--crop-mode', 'none', '-f', 'av_mkv')
                $cik = Join-Path $D "hb-$k-$kbit-$c.mkv"
                $h = HbEsBayt $girdi $cik $hk ($arg + @('--width', "$($kb.W)", '--height', "$($kb.H)"))
                Ekle $ortak $h ([ordered]@{ kol = 'handbrake'; hedef_kbps = $hk }) (Olc $girdi $cik)
                Remove-Item $cik
                $cik = Join-Path $D "hbg-$k-$kbit-$c.mkv"
                $h = HbEsBayt $girdi $cik $hk ($arg + @('--width', "$($pg.W)", '--height', "$($pg.H)"))
                Ekle $ortak $h ([ordered]@{ kol = 'handbrake-urun-geometri'; hedef_kbps = $hk }) (Olc $girdi $cik)
                Remove-Item $cik
                if ($k -eq $Negatif) {
                    $cik = Join-Path $D "neg-$k-$kbit-$c.mkv"
                    $sn = HbKodla $girdi $cik ($arg + @('--width', "$($kb.W)", '--height', "$($kb.H)", '-b', "$([int][math]::Round($hk / 2))"))
                    Ekle $ortak ([ordered]@{ kol = 'negatif-hb-yarim-bit'; kodlama_sn = $sn }) (Olc $girdi $cik)
                    Remove-Item $cik
                }
            } catch {
                Ekle $ortak ([ordered]@{ kol = 'hata'; hata = $_.Exception.Message })
            }
            Write-Host "$k $kbit $c bitti; toplam kodlama sn=$([math]::Round($script:KodlamaSn,1))"
        }
    }
}
"toplam_kodlama_sn=$([math]::Round($script:KodlamaSn,1))" | Tee-Object -FilePath (Join-Path $D "$Json.sure.txt")


