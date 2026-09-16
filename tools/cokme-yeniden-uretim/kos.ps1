param(
    [int]$Esszamanli = 2,
    [int]$Tur = 1,
    [string]$Filtre = '',
    [string]$Cikti = '.calisma/cokme'
)

$ErrorActionPreference = 'Stop'
if (-not $env:GITHUB_ACTIONS) {
    Write-Error 'Bu duzenek esszamanli tam suit kosturur; yalniz GitHub Actions kosucusunda calisir.'
    exit 3
}

New-Item -ItemType Directory -Force $Cikti | Out-Null
$ozet = @()
for ($t = 1; $t -le $Tur; $t++) {
    $surecler = @()
    for ($s = 1; $s -le $Esszamanli; $s++) {
        $dizin = Join-Path $Cikti "tur$t-surec$s"
        New-Item -ItemType Directory -Force $dizin | Out-Null
        $a = @('test', 'tests/VidShrink.Tests', '-c', 'Release', '--no-build', '--blame-crash', '--blame-crash-dump-type', 'mini', '--blame-hang', '--blame-hang-timeout', '20m', '--results-directory', $dizin, '--logger', 'trx', '--logger', 'console;verbosity=normal')
        if ($Filtre) { $a += @('--filter', $Filtre) }
        $env:VIDSHRINK_SETTINGS_PATH = (Join-Path (Resolve-Path $dizin) 'settings.json')
        $p = Start-Process dotnet -ArgumentList $a -NoNewWindow -PassThru -RedirectStandardOutput (Join-Path $dizin 'cikti.txt') -RedirectStandardError (Join-Path $dizin 'hata.txt')
        $surecler += [pscustomobject]@{ Surec = $s; P = $p; Dizin = $dizin; Baslangic = Get-Date }
    }

    $bellek = @()
    while ($surecler | Where-Object { -not $_.P.HasExited }) {
        $os = Get-CimInstance Win32_OperatingSystem
        $testhost = Get-Process testhost -ErrorAction SilentlyContinue
        $bellek += [pscustomobject]@{
            Zaman = (Get-Date).ToString('HH:mm:ss')
            AyrilanGb = [math]::Round(((Get-Counter '\Memory\Committed Bytes').CounterSamples[0].CookedValue) / 1GB, 2)
            TavanGb = [math]::Round(((Get-Counter '\Memory\Commit Limit').CounterSamples[0].CookedValue) / 1GB, 2)
            BosFizikselGb = [math]::Round($os.FreePhysicalMemory * 1KB / 1GB, 2)
            TesthostSayi = @($testhost).Count
            TesthostOzelGb = [math]::Round((($testhost | Measure-Object PrivateMemorySize64 -Sum).Sum) / 1GB, 2)
        }
        Start-Sleep -Seconds 15
    }
    $bellek | ConvertTo-Json | Set-Content (Join-Path $Cikti "tur$t-bellek.json")

    foreach ($s in $surecler) {
        $s.P.WaitForExit()
        $metin = Get-Content (Join-Path $s.Dizin 'cikti.txt') -Raw
        $satir = (Get-Content (Join-Path $s.Dizin 'cikti.txt') | Where-Object { $_ -match '^Test Run (Successful|Failed|Aborted)' } | Select-Object -Last 1)
        $toplam = if ($metin -match 'Total tests:\s*(\d+)') { [int]$Matches[1] } else { $null }
        $ozet += [pscustomobject]@{
            Tur = $t; Surec = $s.Surec; Esszamanli = $Esszamanli
            Cikis = $s.P.ExitCode
            Dakika = [math]::Round(((Get-Date) - $s.Baslangic).TotalMinutes, 1)
            SonucSatiri = $satir
            Toplam = $toplam
            HostCokmesi = $metin -match 'Test host process crashed|The active test run was aborted'
            Asilma = $metin -match 'blame-hang|hang dump'
            Dokum = @(Get-ChildItem $s.Dizin -Recurse -Filter '*.dmp').Count
            TepeAyrilanGb = ($bellek | Measure-Object AyrilanGb -Maximum).Maximum
            TavanGb = ($bellek | Select-Object -First 1).TavanGb
        }
    }
}
$ozet | ConvertTo-Json | Set-Content (Join-Path $Cikti 'ozet.json')
$ozet | Format-Table -AutoSize | Out-String -Width 400 | Write-Host
