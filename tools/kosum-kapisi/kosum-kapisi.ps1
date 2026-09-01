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

$interruptPattern = '(?im)kilitlendi|iptal edildi|durduruldu|\baborted\b|\bcancel(?:ed|led)\b'
if ($text -match $interruptPattern) {
    [Console]::Error.WriteLine('Koşum kesinti/iptal satırı içeriyor.')
    exit 65
}

$failureMatches = [regex]::Matches($text, '(?im)(?:Ba\u015far\u0131s\u0131z|Failed)\s*:\s*(\d+)')
if ($failureMatches.Count -eq 0 -or [int]$failureMatches[$failureMatches.Count - 1].Groups[1].Value -ne 0) {
    [Console]::Error.WriteLine('Başarısız/Failed özeti yok veya sıfır değil.')
    exit 66
}

$totalMatches = [regex]::Matches($text, '(?im)(?:Toplam|Total(?: tests)?)\s*:\s*(\d+)')
if ($totalMatches.Count -eq 0) {
    [Console]::Error.WriteLine('Toplam/Total özeti yok.')
    exit 67
}
$total = [int]$totalMatches[$totalMatches.Count - 1].Groups[1].Value
if ($total -lt $MinimumTotal) {
    [Console]::Error.WriteLine("Toplam test sayısı alt sınırın altında: $total < $MinimumTotal.")
    exit 68
}
if ($commandExit -ne 0) {
    [Console]::Error.WriteLine("Komut sıfırdan farklı çıktı: $commandExit.")
    exit $commandExit
}

Write-Host "KOŞUM KAPISI GEÇTİ: başarısız=0 toplam=$total alt-sınır=$MinimumTotal"
exit 0
