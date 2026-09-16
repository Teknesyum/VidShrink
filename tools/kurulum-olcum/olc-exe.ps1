param([string]$Etiket = 'tur', [string]$Kip = 'kurulum', [string]$Exe)
$cikti = Join-Path $PSScriptRoot '..\..\.calisma\kurulum-olcum'
New-Item -ItemType Directory -Force $cikti | Out-Null

$ErrorActionPreference = 'Stop'
$kok = Join-Path $cikti 'kok'
$local = Join-Path $kok 'local'
$tmp = Join-Path $kok 'tmp'
$kisayol = Join-Path $kok 'kisayol'
New-Item -ItemType Directory -Force $local, $tmp, $kisayol | Out-Null
$installRoot = Join-Path $local 'Programs\VidShrink'

$argumanlar = @('--install-root', $installRoot, '--no-launch', '--timings', '--registry-root', 'HKCU:\Software\VidShrinkOlcum\Classes')
if ($Kip -eq 'kurulum') { $argumanlar += '--skip-shortcuts' }
if ($Kip -eq 'menu') { $argumanlar += @('--shortcut-dir', $kisayol, '--menu-language', 'tr') }

$psi = New-Object Diagnostics.ProcessStartInfo
$psi.FileName = $Exe
foreach ($a in $argumanlar) { $psi.ArgumentList.Add($a) }
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.EnvironmentVariables['LOCALAPPDATA'] = $local
$psi.EnvironmentVariables['TMP'] = $tmp
$psi.EnvironmentVariables['TEMP'] = $tmp
$psi.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)

$saat = [Diagnostics.Stopwatch]::StartNew()
$p = [Diagnostics.Process]::Start($psi)
$satirlar = New-Object Collections.Generic.List[string]
while ($null -ne ($line = $p.StandardOutput.ReadLine())) {
    $s = '{0,8:N0} ms  {1}' -f $saat.Elapsed.TotalMilliseconds, $line
    $satirlar.Add($s); Write-Host $s
}
$err = $p.StandardError.ReadToEnd()
$p.WaitForExit()
$toplam = '{0,8:N0} ms  BITTI cikis={1}' -f $saat.Elapsed.TotalMilliseconds, $p.ExitCode
$satirlar.Add($toplam); Write-Host $toplam
if ($err) { $satirlar.Add("STDERR: $err"); Write-Host "STDERR: $err" }
$satirlar | Set-Content -Encoding UTF8 (Join-Path $cikti "exe-$Etiket.log")
