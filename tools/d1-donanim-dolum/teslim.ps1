param(
    [string[]]$Kollar = @('taban'),
    [string]$Kesitler = 'karanlik,parlak,hareketli',
    [string]$Kodekler = 'hevc_nvenc,av1_nvenc',
    [int[]]$Kbitler = @(1000, 2000, 3500),
    [string]$KesitDizini = '../../../.calisma/nvenc-2',
    [string]$Calisma = '.calisma/t0-d1-donanim-dolum',
    [string]$Cekirdek = '3'
)
$ErrorActionPreference = 'Stop'
$Inv = [Globalization.CultureInfo]::InvariantCulture
$D = (Resolve-Path $Calisma).Path
$Kaynak = (Resolve-Path $KesitDizini).Path
$env:VIDSHRINK_SETTINGS_PATH = Join-Path $D 'settings.json'
$yol = Join-Path $D 'teslim.json'
$Satir = [Collections.Generic.List[object]]::new()
if (Test-Path $yol) { foreach ($x in @(Get-Content $yol -Raw | ConvertFrom-Json)) { $Satir.Add($x) } }
function Yaz { ConvertTo-Json -Depth 6 -InputObject @($Satir) | Set-Content $yol }

foreach ($kol in $Kollar) {
    $Bench = Join-Path $D "bin-$kol/VidShrink.Bench.dll"
    foreach ($k in $Kesitler.Split(',')) {
        $girdi = Join-Path $Kaynak "kesit-$k.mkv"
        foreach ($c in $Kodekler.Split(',')) {
            if ($Satir | Where-Object { $_.kol -eq $kol -and $_.kesit -eq $k -and $_.kodek -eq $c }) { continue }
            $mbler = $Kbitler | ForEach-Object { ($_ * 10.0 / 8 / 1024).ToString('0.####', $Inv) }
            $kl = Join-Path $D "$kol-$k-$c"
            $log = "$kl.log"
            $saat = [Diagnostics.Stopwatch]::StartNew()
            $argv = "/c start `"`" /affinity $Cekirdek /wait /b dotnet `"$Bench`" shrink `"$girdi`" $($mbler -join ',') --out `"$kl`" --speed quality --no-measure --lock-codec $c > `"$log`" 2>&1"
            $p = Start-Process cmd.exe -ArgumentList $argv -NoNewWindow -Wait -PassThru
            $saat.Stop()
            $rs = @(Get-Content (Join-Path $kl 'results.json') -Raw | ConvertFrom-Json)
            $izler = [Collections.Generic.List[string]]::new()
            $acik = [Collections.Generic.List[string]]::new()
            foreach ($l in Get-Content $log) {
                if ($l -match '^\s+deneme (\d+): (.+)$') { $acik.Add($Matches[1] + ':' + $Matches[2]) }
                elseif ($l -match ' MB -> ') { $izler.Add(($acik -join ' / ')); $acik.Clear() }
            }
            for ($j = 0; $j -lt $Kbitler.Count; $j++) {
                $r = $rs[$j]
                $iz = $izler[$j]
                $Satir.Add([pscustomobject][ordered]@{
                    kol = $kol; kesit = $k; kodek = $c; istenen_kbit = $Kbitler[$j]; hedef_mb = $r.TargetMb
                    cikan_mb = $r.ActualMb; teslim_orani = [math]::Round($r.ActualMb / $r.TargetMb, 4)
                    hedefi_asti = ($r.ActualMb -gt $r.TargetMb); deneme = $r.Attempts; iz = $iz
                    sure_sn = [math]::Round($saat.Elapsed.TotalSeconds / $Kbitler.Count, 1)
                })
                Write-Host "$kol $k $c $($Kbitler[$j]) oran=$([math]::Round($r.ActualMb / $r.TargetMb, 4)) deneme=$($r.Attempts) | $iz"
            }
            Yaz
            Get-ChildItem $kl -Filter *.mp4 | Remove-Item
        }
    }
}
