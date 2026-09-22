#requires -Version 7
param(
    [string[]]$Kollar = @('taban', 'k097', 'k0985', 'k100'),
    [string]$Kesitler = 'karanlik,parlak,hareketli',
    [string]$Kodekler = 'hevc_nvenc,av1_nvenc',
    [int[]]$Kbitler = @(1000, 2000, 3500),
    [string]$KesitDizini = '.calisma/nvenc-2',
    [string]$Ham = 'docs/olcumler/nvenc-3-ham.json',
    [string]$Calisma = '.calisma/nvenc-4'
)
$ErrorActionPreference = 'Stop'
$Inv = [Globalization.CultureInfo]::InvariantCulture
$D = (Resolve-Path $Calisma).Path
$env:VIDSHRINK_SETTINGS_PATH = Join-Path $D 'settings.json'
$yol = Join-Path $D 'teslim.json'
$Satir = [Collections.Generic.List[object]]::new()
if (Test-Path $yol) { foreach ($x in @(Get-Content $yol -Raw | ConvertFrom-Json)) { $Satir.Add($x) } }
$eski = @(Get-Content $Ham -Raw | ConvertFrom-Json | Where-Object kol -eq 'urun')
function Yaz { ConvertTo-Json -Depth 5 -InputObject @($Satir) | Set-Content $yol }
function Olc([string]$Bench, [string]$Ref, [string]$Test) {
    $o = Join-Path $D ([IO.Path]::GetFileNameWithoutExtension($Test) + '.olcu.json')
    & dotnet $Bench measure-pair $Ref $Test --fps 24/1 --out $o *>&1 | Out-Null
    $m = Get-Content $o -Raw | ConvertFrom-Json
    Remove-Item $o
    @{ ort = [math]::Round($m.VmafNegMean, 2); p10 = [math]::Round($m.VmafNegP10, 2) }
}

foreach ($kol in $Kollar) {
    $Bench = Join-Path $D "bin-$kol/VidShrink.Bench.dll"
    foreach ($k in $Kesitler.Split(',')) {
        $girdi = Join-Path $KesitDizini "kesit-$k.mkv"
        foreach ($c in $Kodekler.Split(',')) {
            if ($Satir | Where-Object { $_.kol -eq $kol -and $_.kesit -eq $k -and $_.kodek -eq $c }) { continue }
            $mbler = $Kbitler | ForEach-Object { ($_ * 10.0 / 8 / 1024).ToString('0.####', $Inv) }
            $kl = Join-Path $D "$kol-$k-$c"
            $txt = & dotnet $Bench shrink $girdi ($mbler -join ',') --out $kl --speed quality --no-measure --lock-codec $c 2>&1
            $txt | Out-File "$kl.log"
            $rs = @(Get-Content (Join-Path $kl 'results.json') -Raw | ConvertFrom-Json)
            for ($j = 0; $j -lt $Kbitler.Count; $j++) {
                $kbit = $Kbitler[$j]; $r = $rs[$j]
                $mp4 = Join-Path $kl ("kesit-${k}_" + $r.TargetMb.ToString('0.#', $Inv) + 'mb.mp4')
                $bayt = (Get-Item $mp4).Length
                $vm = $null; $kaynak = ''
                $tb = $Satir | Where-Object { $_.kol -eq 'taban' -and $_.kesit -eq $k -and $_.kodek -eq $c -and $_.istenen_kbit -eq $kbit }
                $h = $eski | Where-Object { $_.kesit -eq $k -and $_.kodek -eq $c -and $_.istenen_kbit -eq $kbit }
                if ($tb -and $tb.bayt -eq $bayt) { $vm = @{ ort = $tb.vmafneg_ort; p10 = $tb.vmafneg_p10 }; $kaynak = 'taban ile ayni bayt' }
                elseif ($h -and $h.bayt -eq $bayt) { $vm = @{ ort = $h.vmafneg_ort; p10 = $h.vmafneg_p10 }; $kaynak = 'nvenc-3 ile ayni bayt' }
                else { $vm = Olc $Bench $girdi $mp4; $kaynak = 'olculdu' }
                $Satir.Add([pscustomobject][ordered]@{
                    kol = $kol; kesit = $k; kodek = $c; istenen_kbit = $kbit; hedef_mb = $r.TargetMb
                    bayt = $bayt; teslim_orani = [math]::Round($r.ActualMb / $r.TargetMb, 4)
                    hedefi_asti = ($r.ActualMb -gt $r.TargetMb); deneme = $r.Attempts
                    vmafneg_ort = $vm.ort; vmafneg_p10 = $vm.p10; vmaf_kaynak = $kaynak
                })
                Yaz
                Remove-Item $mp4
                Write-Host "$kol $k $c $kbit oran=$([math]::Round($r.ActualMb / $r.TargetMb, 4)) deneme=$($r.Attempts) vmaf=$($vm.ort) ($kaynak)"
            }
        }
    }
}
