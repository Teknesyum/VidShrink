$ErrorActionPreference = 'Stop'
$w = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$core = "$w\src\VidShrink.Core\Setup"
$mutasyonlar = @(
    @{ Ad = 'M1-kucult-MultiSelectModel'; Dosya = "$core\ShellRegistration.cs"; Eski = 'verb.SetValue("MultiSelectModel", "Player"'; Yeni = 'verb.SetValue("MultiSelectModel", "Document"' },
    @{ Ad = 'M2-tutan-tur-esigi'; Dosya = "$core\LockedFolder.cs"; Eski = 'if (holderRounds >= 2)'; Yeni = 'if (holderRounds >= 3)' },
    @{ Ad = 'M3-uygulama-saglamasi-atlandi'; Dosya = "$core\SetupRunner.cs"; Eski = 'SetupDownloads.AssertChecksum(checksums, archiveName, archiveTask.Result.Sha256);'; Yeni = '' },
    @{ Ad = 'M4-geri-koyma-yok'; Dosya = "$core\SetupRunner.cs"; Eski = 'Restore(root, aside);'; Yeni = '' },
    @{ Ad = 'M5-libmpv-dll-sabiti'; Dosya = "$core\SetupModel.cs"; Eski = '"673e6397920ab64a9c5b3a618f7f16d38854efe72b58665f1f84e4e873b763a4"'; Yeni = '"673e6397920ab64a9c5b3a618f7f16d38854efe72b58665f1f84e4e873b763a5"' }
)
$log = "$w\.calisma\kurulum-olcum\mutasyon.log"
Set-Content $log ''
foreach ($m in $mutasyonlar) {
    $asil = [IO.File]::ReadAllText($m.Dosya)
    if (-not $asil.Contains($m.Eski)) { Add-Content $log "$($m.Ad): ESLESME YOK"; continue }
    try {
        [IO.File]::WriteAllText($m.Dosya, $asil.Replace($m.Eski, $m.Yeni))
        $b = dotnet build "$w\tests\VidShrink.Tests\VidShrink.Tests.csproj" -c Release -m:2 2>&1 | Select-String 'Oluşturma başarılı|error' | Select-Object -First 1
        $t = dotnet test "$w\tests\VidShrink.Tests\VidShrink.Tests.csproj" -c Release --no-build --filter 'FullyQualifiedName~KurucuExeTests' 2>&1
        $kirmizi = ($t | Select-String '^\s+Başarısız VidShrink' | ForEach-Object { $_.Line.Trim() }) -join '; '
        $ozet = ($t | Select-String 'Toplam:' | Select-Object -First 1).Line.Trim()
        Add-Content $log "$($m.Ad): derleme=[$($b.Line.Trim())] $ozet :: $kirmizi"
    }
    finally {
        [IO.File]::WriteAllText($m.Dosya, $asil)
    }
}
$b = dotnet build "$w\tests\VidShrink.Tests\VidShrink.Tests.csproj" -c Release -m:2 2>&1 | Select-String 'Oluşturma başarılı|error' | Select-Object -First 1
$t = dotnet test "$w\tests\VidShrink.Tests\VidShrink.Tests.csproj" -c Release --no-build --filter 'FullyQualifiedName~KurucuExeTests' 2>&1
Add-Content $log "GERI-ALINDI: derleme=[$($b.Line.Trim())] $(($t | Select-String 'Toplam:' | Select-Object -First 1).Line.Trim())"
Get-Content $log
