$ErrorActionPreference = 'Stop'
$gate = Join-Path $PSScriptRoot 'kosum-kapisi.ps1'
$fixtures = Join-Path $PSScriptRoot 'fixtures'

foreach ($name in @('gecerli-tr.txt', 'gecerli-en.txt')) {
    & $gate -MinimumTotal 974 -InputFile (Join-Path $fixtures $name)
    if ($LASTEXITCODE -ne 0) { throw "Geçerli örnek reddedildi: $name" }
}

$ErrorActionPreference = 'Continue'
foreach ($case in @(
    @{ Name = 'kesinti-tr.txt'; Exit = 65 },
    @{ Name = 'basarisiz-en.txt'; Exit = 66 },
    @{ Name = 'eksik-toplam-en.txt'; Exit = 68 }
)) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $gate -MinimumTotal 974 -InputFile (Join-Path $fixtures $case.Name) 2>$null
    if ($LASTEXITCODE -ne $case.Exit) {
        throw "Beklenen ret kodu gelmedi: $($case.Name), beklenen=$($case.Exit), gelen=$LASTEXITCODE"
    }
}
$ErrorActionPreference = 'Stop'

Write-Host 'kosum-kapisi fixture testleri geçti'
