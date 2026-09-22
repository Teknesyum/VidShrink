#requires -Version 7
param(
    [string[]]$Kollar = @('taban', 'p7'),
    [string]$Kesitler = 'gren,gradyan,hayat',
    [string]$Kodek = 'hevc_nvenc',
    [int[]]$Kbitler = @(1000, 2000, 3500),
    [string]$Calisma = '.calisma/worktree-agent-a2cc7681a7aeb1609'
)
$ErrorActionPreference = 'Stop'
[Diagnostics.Process]::GetCurrentProcess().ProcessorAffinity = [IntPtr]0xF
$Inv = [Globalization.CultureInfo]::InvariantCulture
$D = (Resolve-Path $Calisma).Path
$env:VIDSHRINK_SETTINGS_PATH = Join-Path $D 'settings.json'
$yol = Join-Path $D 'kalite.json'
$Satir = [Collections.Generic.List[object]]::new()
if (Test-Path $yol) { foreach ($x in @(Get-Content $yol -Raw | ConvertFrom-Json)) { $Satir.Add($x) } }
$preset = @{ taban = '-preset p4'; p7 = '-preset p7' }
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
        $girdi = Join-Path $D "kesit-$k.mkv"
        if ($Satir | Where-Object { $_.kol -eq $kol -and $_.kesit -eq $k }) { continue }
        $mbler = $Kbitler | ForEach-Object { ($_ * 10.0 / 8 / 1024).ToString('0.####', $Inv) }
        $kl = Join-Path $D "$kol-$k"
        $txt = & dotnet $Bench shrink $girdi ($mbler -join ',') --out $kl --speed quality --no-measure --lock-codec $Kodek 2>&1
        $txt | Out-File "$kl.log"
        $komut = @($txt | Where-Object { "$_" -match "-c:v $Kodek" })
        if ($komut.Count -eq 0) { throw "$kol $k komut satiri gunlukte yok" }
        foreach ($s in $komut) {
            if ("$s" -notmatch [regex]::Escape($preset[$kol])) { throw "$kol komutunda '$($preset[$kol])' yok: $s" }
        }
        $rs = @(Get-Content (Join-Path $kl 'results.json') -Raw | ConvertFrom-Json)
        for ($j = 0; $j -lt $Kbitler.Count; $j++) {
            $kbit = $Kbitler[$j]; $r = $rs[$j]
            $mp4 = Join-Path $kl ("kesit-${k}_" + $r.TargetMb.ToString('0.#', $Inv) + 'mb.mp4')
            $bayt = (Get-Item $mp4).Length
            $vm = Olc $Bench $girdi $mp4
            $Satir.Add([pscustomobject][ordered]@{
                kol = $kol; kesit = $k; kodek = $Kodek; istenen_kbit = $kbit; hedef_mb = $r.TargetMb
                bayt = $bayt; teslim_orani = [math]::Round($r.ActualMb / $r.TargetMb, 4)
                hedefi_asti = ($r.ActualMb -gt $r.TargetMb); deneme = $r.Attempts
                kodlama_sn = [math]::Round($r.EncodeSeconds, 3)
                vmafneg_ort = $vm.ort; vmafneg_p10 = $vm.p10
            })
            Yaz
            Remove-Item $mp4
            Write-Host "$kol $k $kbit oran=$([math]::Round($r.ActualMb / $r.TargetMb, 4)) deneme=$($r.Attempts) sn=$([math]::Round($r.EncodeSeconds, 2)) vmaf=$($vm.ort)"
        }
    }
}
