param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 1000000)]
    [int]$MinimumTotal,

    [string]$InputFile,
    [string]$OutputFile
)

$ErrorActionPreference = 'Stop'

if ($InputFile) {
    $text = Get-Content -LiteralPath $InputFile -Raw -Encoding utf8
    $commandExit = 0
}
else {
    $lines = & dotnet test -c Release --no-restore 2>&1 | ForEach-Object {
        $line = $_.ToString()
        Write-Host $line
        $line
    }
    $commandExit = $LASTEXITCODE
    $text = $lines -join [Environment]::NewLine
    if ($OutputFile) {
        $parent = Split-Path -Parent $OutputFile
        if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
        Set-Content -LiteralPath $OutputFile -Value $text -Encoding utf8
    }
}

function Dur([int]$Code, [string]$Reason) {
    Write-Host "KOSUM KAPISI DUSTU: kod=$Code sart=$Reason"
    [Console]::Error.WriteLine($Reason)
    exit $Code
}

$interruptPattern = '(?im)kilitlendi|iptal edildi|durduruldu|\baborted\b|\bcancel(?:ed|led)\b' +
    '|Konak i\u015flemi[^\r\n]*beklenmedik' +
    '|beklenmedik \u015fekilde \u00e7\u0131k\u0131\u015f yap\u0131ld\u0131' +
    '|test\s*host process crashed|testhost process exited'
if ($text -match $interruptPattern) {
    Dur 65 'Kosum kesinti/iptal satiri iceriyor.'
}

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
if ($total -lt $MinimumTotal) {
    Dur 68 "Toplam test sayisi alt sinirin altinda: $total < $MinimumTotal."
}
if ($commandExit -ne 0) {
    Dur $commandExit "Komut sifirdan farkli cikti: $commandExit."
}

Write-Host "KOŞUM KAPISI GEÇTİ: başarısız=0 toplam=$total alt-sınır=$MinimumTotal"
exit 0
