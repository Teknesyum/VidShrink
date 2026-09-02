param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 1000000)]
    [int]$MinimumTotal,

    [ValidateRange(0, 1000000)]
    [Nullable[int]]$MaximumSkipped,

    [string]$InputFile,
    [string]$OutputFile
)

$ErrorActionPreference = 'Stop'

try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

function Dur([int]$Code, [string]$Reason) {
    Write-Host "KOSUM KAPISI DUSTU: kod=$Code sart=$Reason"
    [Console]::Error.WriteLine($Reason)
    exit $Code
}

$interruptPattern = '(?im)kilitlendi|iptal edildi|durduruldu|\baborted\b|\bcancel(?:ed|led)\b' +
    '|Konak i\u015flemi[^\r\n]*beklenmedik' +
    '|beklenmedik \u015fekilde \u00e7\u0131k\u0131\u015f yap\u0131ld\u0131' +
    '|test\s*host process crashed|testhost process exited'

$text = $null
$trxPath = $null

if ($InputFile) {
    if ($InputFile -match '(?i)\.trx$') {
        $trxPath = $InputFile
    }
    else {
        $text = Get-Content -LiteralPath $InputFile -Raw -Encoding utf8
    }
    $commandExit = 0
}
else {
    $resultsDir = Join-Path ([System.IO.Path]::GetTempPath()) ('kosum-kapisi-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null
    $oncekiEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $lines = & dotnet test -c Release --no-restore --logger 'trx;LogFileName=kosum-kapisi.trx' --results-directory $resultsDir 2>&1 | ForEach-Object {
        $line = $_.ToString()
        Write-Host $line
        $line
    }
    $ErrorActionPreference = $oncekiEap
    $commandExit = $LASTEXITCODE
    $text = $lines -join [Environment]::NewLine
    if ($OutputFile) {
        $parent = Split-Path -Parent $OutputFile
        if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
        Set-Content -LiteralPath $OutputFile -Value $text -Encoding utf8
    }
    $foundTrx = Get-ChildItem -LiteralPath $resultsDir -Filter '*.trx' -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($foundTrx) {
        $trxPath = $foundTrx.FullName
    }
}

if ($text -and ($text -match $interruptPattern)) {
    Dur 65 'Kosum kesinti/iptal satiri iceriyor.'
}

$total = $null
$skipped = $null

if ($trxPath -and (Test-Path -LiteralPath $trxPath)) {
    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw -Encoding utf8
    $counters = $trx.TestRun.ResultSummary.Counters
    if (-not $counters -or [string]::IsNullOrEmpty($counters.total)) {
        Dur 66 'Basarisiz/Failed ozeti yok (trx Counters eksik).'
    }
    $failed = [int]$counters.failed
    if ($failed -ne 0) {
        Dur 66 "Basarisiz/Failed ozeti sifir degil: $failed."
    }
    $total = [int]$counters.total
    $passed = [int]$counters.passed
    $skipped = $total - $passed - $failed
}
elseif ($null -ne $text) {
    $failureMatches = [regex]::Matches($text, '(?im)(?:Ba\u015far\u0131s\u0131z|Failed)\s*:\s*(\d+)')
    if ($failureMatches.Count -eq 0) {
        Dur 66 'Basarisiz/Failed ozeti yok.'
    }
    foreach ($failureMatch in $failureMatches) {
        if ([int]$failureMatch.Groups[1].Value -ne 0) {
            Dur 66 "Basarisiz/Failed ozeti sifir degil: $($failureMatch.Value)."
        }
    }

    $totalMatches = [regex]::Matches($text, '(?im)(?:Toplam|Total(?: tests)?)\s*:\s*(\d+)')
    if ($totalMatches.Count -eq 0) {
        Dur 67 'Toplam/Total ozeti yok.'
    }
    $total = [int]$totalMatches[$totalMatches.Count - 1].Groups[1].Value

    if ($null -ne $MaximumSkipped) {
        $skippedMatches = [regex]::Matches($text, '(?im)(?:Atlanan|Skipped)\s*:\s*(\d+)')
        if ($skippedMatches.Count -eq 0) {
            Dur 69 'Atlanan/Skipped ozeti yok.'
        }
        $skipped = [int]$skippedMatches[$skippedMatches.Count - 1].Groups[1].Value
    }
}
else {
    Dur 66 'Basarisiz/Failed ozeti yok.'
}

if ($total -lt $MinimumTotal) {
    Dur 68 "Toplam test sayisi alt sinirin altinda: $total < $MinimumTotal."
}
if ($null -ne $MaximumSkipped) {
    if ($skipped -gt $MaximumSkipped) {
        Dur 69 "Atlanan sayisi ust sinirin ustunde: $skipped > $MaximumSkipped."
    }
}
if ($commandExit -ne 0) {
    Dur $commandExit "Komut sifirdan farkli cikti: $commandExit."
}

$skippedNote = if ($null -ne $MaximumSkipped) { " atlanan=$skipped ust-sinir=$MaximumSkipped" } else { "" }
Write-Host "KOŞUM KAPISI GEÇTİ: başarısız=0 toplam=$total alt-sınır=$MinimumTotal$skippedNote"
exit 0