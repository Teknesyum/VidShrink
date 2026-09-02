$ErrorActionPreference = 'Stop'
$gate = Join-Path $PSScriptRoot 'kosum-kapisi.ps1'
$fixtures = Join-Path $PSScriptRoot 'fixtures'

foreach ($case in @(
    @{ Name = 'gecerli-tr.txt'; Minimum = 974 },
    @{ Name = 'gecerli-en.txt'; Minimum = 974 },
    @{ Name = 'gercek-kosum-tr.txt'; Minimum = 80 },
    @{ Name = 'gecerli-en.txt'; Minimum = 974; MaximumSkipped = 30 }
)) {
    $params = @{ MinimumTotal = $case.Minimum; InputFile = (Join-Path $fixtures $case.Name) }
    if ($case.ContainsKey('MaximumSkipped')) { $params.MaximumSkipped = $case.MaximumSkipped }
    & $gate @params
    if ($LASTEXITCODE -ne 0) { throw "Gecerli ornek reddedildi: $($case.Name)" }
}

$ErrorActionPreference = 'Continue'
foreach ($case in @(
    @{ Name = 'kesinti-tr.txt'; Minimum = 974; Exit = 65 },
    @{ Name = 'konak-cokmesi-tr.txt'; Minimum = 974; Exit = 65 },
    @{ Name = 'basarisiz-en.txt'; Minimum = 974; Exit = 66 },
    @{ Name = 'ikili-ozet-tr.txt'; Minimum = 974; Exit = 66 },
    @{ Name = 'eksik-toplam-en.txt'; Minimum = 974; Exit = 68 },
    @{ Name = 'korluk-geri-en.txt'; Minimum = 1134; MaximumSkipped = 30; Exit = 69 }
)) {
    $params = @{ MinimumTotal = $case.Minimum; InputFile = (Join-Path $fixtures $case.Name) }
    if ($case.ContainsKey('MaximumSkipped')) { $params.MaximumSkipped = $case.MaximumSkipped }
    & powershell -NoProfile -ExecutionPolicy Bypass -File $gate @params 2>$null
    if ($LASTEXITCODE -ne $case.Exit) {
        throw "Beklenen ret kodu gelmedi: $($case.Name), beklenen=$($case.Exit), gelen=$LASTEXITCODE"
    }
}
$ErrorActionPreference = 'Stop'

Write-Host 'kosum-kapisi fixture testleri gecti'