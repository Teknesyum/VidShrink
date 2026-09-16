param([string]$Etiket = 'tur', [string]$Kip = 'kurulum')
$cikti = Join-Path $PSScriptRoot '..\..\.calisma\kurulum-olcum'
New-Item -ItemType Directory -Force $cikti | Out-Null

$ErrorActionPreference = 'Stop'
$kok = Join-Path $cikti 'kok'
$local = Join-Path $kok 'local'
$tmp = Join-Path $kok 'tmp'
New-Item -ItemType Directory -Force $local, $tmp | Out-Null
$installRoot = Join-Path $local 'Programs\VidShrink'
$betik = (Resolve-Path (Join-Path $PSScriptRoot '..\..\Install-VidShrink.ps1')).Path

$argumanlar = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $betik, '-InstallRoot', $installRoot, '-NoLaunch')
if ($Kip -eq 'kurulum') { $argumanlar += '-SkipShortcuts' }
if ($Kip -eq 'menu') { $argumanlar += @('-ShellMenuOnly', '-RegistryRoot', 'HKCU:\Software\VidShrinkOlcum\Classes') }

$psi = New-Object Diagnostics.ProcessStartInfo
$psi.FileName = 'powershell.exe'
$psi.Arguments = ($argumanlar | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.EnvironmentVariables['LOCALAPPDATA'] = $local
$psi.EnvironmentVariables['TMP'] = $tmp
$psi.EnvironmentVariables['TEMP'] = $tmp
$psi.EnvironmentVariables.Remove('PSModulePath')
$psi.StandardOutputEncoding = [Text.Encoding]::GetEncoding(857)

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
$satirlar | Set-Content -Encoding UTF8 (Join-Path $cikti "betik-$Etiket.log")

