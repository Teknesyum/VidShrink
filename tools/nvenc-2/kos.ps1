param(
    [string]$Calisma = '.calisma/nvenc2',
    [string]$Kesitler = 'karanlik,parlak,hareketli,orta',
    [string]$Kodekler = 'h264_nvenc,hevc_nvenc,av1_nvenc',
    [int[]]$Kbitler = @(1000, 2000, 3500),
    [string]$EkKesit = 'orta',
    [string]$EkKodekler = 'hevc_nvenc,av1_nvenc',
    [int]$EkKbit = 600,
    [string]$Negatif = 'hareketli',
    [string]$Json = 'kos.json'
)
$ErrorActionPreference = 'Stop'
$Inv = [Globalization.CultureInfo]::InvariantCulture
$D = (Resolve-Path $Calisma).Path
$env:VIDSHRINK_SETTINGS_PATH = Join-Path $D 'settings.json'
$Bench = (Resolve-Path (Join-Path $PSScriptRoot '..\VidShrink.Bench\bin\Release\net8.0\VidShrink.Bench.dll')).Path
$HbKod = @{ h264_nvenc = 'nvenc_h264'; hevc_nvenc = 'nvenc_h265'; av1_nvenc = 'nvenc_av1' }
$Satir = [Collections.Generic.List[object]]::new()
$yol = Join-Path $D $Json
if (Test-Path $yol) { foreach ($x in @(Get-Content $yol -Raw | ConvertFrom-Json)) { $Satir.Add($x) } }
$surePath = Join-Path $D 'kodlama-sn.txt'
$script:Top = [double]::Parse((Get-Content $surePath -Raw).Trim(), $Inv)

function Sure([double]$sn) { $script:Top += $sn; Set-Content $surePath $script:Top.ToString($Inv) }
function Yaz { ConvertTo-Json -Depth 5 -InputObject @($Satir) | Set-Content $yol }
function Olc([string]$Ref, [string]$Test) {
    $o = Join-Path $D ([IO.Path]::GetFileNameWithoutExtension($Test) + '.olcu.json')
    & dotnet $Bench measure-pair $Ref $Test --fps 24/1 --out $o *>&1 | Out-Null
    $m = Get-Content $o -Raw | ConvertFrom-Json
    Remove-Item $o
    [ordered]@{ bayt = $m.Bayt; kbps = [math]::Round($m.Kbps, 1); vmafneg_ort = [math]::Round($m.VmafNegMean, 2); vmafneg_p10 = [math]::Round($m.VmafNegP10, 2) }
}
function Ekle { $s = [ordered]@{}; foreach ($x in $args) { if ($x) { foreach ($k in $x.Keys) { $s[$k] = $x[$k] } } }; $Satir.Add([pscustomobject]$s); Yaz }
function HbKodla([string]$Girdi, [string]$Cikis, [string[]]$Arg) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    & HandBrakeCLI -i $Girdi -o $Cikis @Arg *>&1 | Out-File -Append (Join-Path $D 'handbrake.log')
    $sw.Stop(); Sure $sw.Elapsed.TotalSeconds
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Cikis)) { throw "HandBrakeCLI basarisiz: $Cikis" }
}
function HbEsBayt([string]$Girdi, [string]$Cikis, [double]$HedefKbps, [string[]]$Arg) {
    $b = [int][math]::Round($HedefKbps); $iz = @()
    for ($i = 1; $i -le 3; $i++) {
        HbKodla $Girdi $Cikis ($Arg + @('-b', "$b"))
        $kbps = (Get-Item $Cikis).Length * 8 / 1000 / 10.0; $sap = $kbps / $HedefKbps - 1
        $iz += "${b}k->$([math]::Round($kbps,1))"
        if ([math]::Abs($sap) -le 0.02 -or $i -eq 3) { return [ordered]@{ hb_deneme = $i; hb_iz = ($iz -join ' | ') } }
        $b = [int][math]::Round($b * $HedefKbps / $kbps)
    }
}

foreach ($k in $Kesitler.Split(',')) {
    $girdi = Join-Path $D "kesit-$k.mkv"
    foreach ($c in $Kodekler.Split(',')) {
        $bitler = @($Kbitler)
        if ($k -eq $EkKesit -and $EkKodekler.Split(',') -contains $c) { $bitler = @($EkKbit) + $bitler }
        $bitler = @($bitler | Where-Object { $kb = $_; -not ($Satir | Where-Object { $_.kesit -eq $k -and $_.kodek -eq $c -and $_.istenen_kbit -eq $kb }) })
        if ($bitler.Count -eq 0) { continue }
        $mbler = $bitler | ForEach-Object { ($_ * 10.0 / 8 / 1024).ToString('0.####', $Inv) }
        $kl = Join-Path $D "urun-$k-$c"
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $txt = & dotnet $Bench shrink $girdi ($mbler -join ',') --out $kl --speed quality --no-measure --lock-codec $c 2>&1
        $sw.Stop(); Sure $sw.Elapsed.TotalSeconds
        $txt | Out-File "$kl.log"
        $rs = @(Get-Content (Join-Path $kl 'results.json') -Raw | ConvertFrom-Json)
        $komutlar = @($txt | Where-Object { "$_" -like 'komut:*' })
        for ($j = 0; $j -lt $bitler.Count; $j++) {
            $kbit = $bitler[$j]; $r = $rs[$j]
            $ortak = [ordered]@{ kesit = $k; kodek = $c; istenen_kbit = $kbit; hedef_mb = $r.TargetMb }
            try {
                $mp4 = Join-Path $kl ("kesit-${k}_" + $r.TargetMb.ToString('0.#', $Inv) + 'mb.mp4')
                $o = Olc $girdi $mp4
                Ekle $ortak ([ordered]@{ kol = 'urun'; geometri = "$($r.Width)x$($r.Height)"; bayt_sapma_yuzde = [math]::Round(($r.ActualMb / $r.TargetMb - 1) * 100, 2); bantta = $r.InBand; tasma = $r.OverTarget; deneme = $r.Attempts; komut = "$($komutlar[$j])" }) $o
                Remove-Item $mp4
                $arg = @('-Z', 'H.265 NVENC 1080p', '-e', $HbKod[$c], '-a', 'none', '--crop-mode', 'none', '-f', 'av_mkv', '--width', '1920', '--height', '818')
                $cik = Join-Path $D "hb-$k-$kbit-$c.mkv"
                $h = HbEsBayt $girdi $cik $o.kbps $arg
                $ho = Olc $girdi $cik
                Ekle $ortak $h ([ordered]@{ kol = 'handbrake'; geometri = '1920x818'; bayt_sapma_yuzde = [math]::Round(($ho.bayt / $o.bayt - 1) * 100, 2) }) $ho
                Remove-Item $cik
                if ($k -eq $Negatif) {
                    $cik = Join-Path $D "neg-$k-$kbit-$c.mkv"
                    HbKodla $girdi $cik ($arg + @('-b', "$([int][math]::Round($o.kbps / 2))"))
                    $no = Olc $girdi $cik
                    Ekle $ortak ([ordered]@{ kol = 'negatif-hb-yarim-bit'; geometri = '1920x818'; bayt_sapma_yuzde = [math]::Round(($no.bayt / $o.bayt - 1) * 100, 2) }) $no
                    Remove-Item $cik
                }
            } catch {
                Ekle $ortak ([ordered]@{ kol = 'hata'; hata = $_.Exception.Message })
            }
            Write-Host "$k $c $kbit bitti; toplam kodlama sn=$([math]::Round($script:Top,1))"
        }
    }
}
