param([string]$Cihaz = "Mikrofon (Arctis Nova Pro Wireless)")
$d = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path ".calisma\kaydedici-ses-seviyesi"
New-Item -ItemType Directory -Force $d | Out-Null
$giris = "audio=$Cihaz"

function Kayit([string]$ad) {
    $wav = Join-Path $d "$ad.wav"
    Remove-Item $wav -ErrorAction SilentlyContinue
    return Start-Process ffmpeg -ArgumentList @("-hide_banner", "-nostdin", "-y", "-f", "dshow", "-i", "`"$giris`"", "-t", "6", "-c:a", "pcm_s16le", "`"$wav`"") `
        -NoNewWindow -PassThru -RedirectStandardError (Join-Path $d "$ad.kayit.err") -RedirectStandardOutput (Join-Path $d "$ad.kayit.out")
}

function Olcer([string]$ad, [int]$sn) {
    $raw = Join-Path $d "$ad.olcer.raw"
    Remove-Item $raw -ErrorAction SilentlyContinue
    return Start-Process ffmpeg -ArgumentList @("-hide_banner", "-nostdin", "-y", "-loglevel", "error", "-threads", "1", "-f", "dshow", "-audio_buffer_size", "50", "-i", "`"$giris`"", "-t", "$sn", "-ac", "1", "-ar", "8000", "-f", "s16le", "`"$raw`"") `
        -NoNewWindow -PassThru -RedirectStandardError (Join-Path $d "$ad.olcer.err") -RedirectStandardOutput (Join-Path $d "$ad.olcer.out")
}

function Rapor([string]$ad, $kayit, $saat, $olcer) {
    $wav = Join-Path $d "$ad.wav"
    $boy = if (Test-Path $wav) { (Get-Item $wav).Length } else { -1 }
    $sure = (ffprobe -v error -show_entries format=duration -of csv=p=0 "$wav" 2>&1) -join " "
    $akis = (ffprobe -v error -show_entries stream=sample_rate,channels -of csv=p=0 "$wav" 2>&1) -join " "
    $hata = (Get-Content (Join-Path $d "$ad.kayit.err") | Where-Object { $_ -match "error|buffer|drop|discontin|Non-monot|fail|busy|could not" -and $_ -notmatch "VCAMDS" }) -join " | "
    $satir = "$ad`tkayit_cikis=$($kayit.ExitCode)`tduvar_ms=$([int]$saat.ElapsedMilliseconds)`twav_bayt=$boy`tsure_sn=$sure`takis=$akis`tkayit_uyari=[$hata]"
    if ($olcer) {
        $raw = Join-Path $d "$ad.olcer.raw"
        $rboy = if (Test-Path $raw) { (Get-Item $raw).Length } else { -1 }
        $rhata = (Get-Content (Join-Path $d "$ad.olcer.err") | Where-Object { $_ -notmatch "VCAMDS" }) -join " | "
        $satir += "`tolcer_cikis=$($olcer.ExitCode)`tolcer_bayt=$rboy`tolcer_hata=[$rhata]"
    }
    $satir
}

$sonuc = @()

$s = [Diagnostics.Stopwatch]::StartNew(); $k = Kayit "A-yalniz"; $k.WaitForExit(); $s.Stop()
$sonuc += Rapor "A-yalniz" $k $s $null

$s = [Diagnostics.Stopwatch]::StartNew(); $k = Kayit "B-kayit-sonra-olcer"; Start-Sleep -Seconds 2; $o = Olcer "B-kayit-sonra-olcer" 2; $o.WaitForExit(); $k.WaitForExit(); $s.Stop()
$sonuc += Rapor "B-kayit-sonra-olcer" $k $s $o

$o = Olcer "C-olcer-sonra-kayit" 8; Start-Sleep -Seconds 2
$s = [Diagnostics.Stopwatch]::StartNew(); $k = Kayit "C-olcer-sonra-kayit"; $k.WaitForExit(); $s.Stop(); $o.WaitForExit()
$sonuc += Rapor "C-olcer-sonra-kayit" $k $s $o

$s = [Diagnostics.Stopwatch]::StartNew(); $k = Kayit "D-yalniz-tekrar"; $k.WaitForExit(); $s.Stop()
$sonuc += Rapor "D-yalniz-tekrar" $k $s $null

$sonuc | Tee-Object (Join-Path $d "olcum.txt")
Remove-Item (Join-Path $d "*.wav"), (Join-Path $d "*.raw") -ErrorAction SilentlyContinue
