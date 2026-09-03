#!/usr/bin/env bash
set -u
EC=src/VidShrink.Ffmpeg/EncoderCapabilities.cs
SE=src/VidShrink.App/Playback/SegmentEncoder.cs
OUT=.calisma/T147/K5-izgara.txt
: > "$OUT"

run_case () {
  echo "=================== $1 ===================" >> "$OUT"
  if ! dotnet build tests/VidShrink.Tests/VidShrink.Tests.csproj -c Release --no-incremental > .calisma/T147/b.log 2>&1; then
    echo "DERLEME HATASI" >> "$OUT"; grep -E "error CS" .calisma/T147/b.log | head -3 >> "$OUT"; echo >> "$OUT"; return
  fi
  dotnet test tests/VidShrink.Tests/VidShrink.Tests.csproj -c Release \
    --filter "SessizDusurmeTests|EncoderCapabilitiesTests" > .calisma/T147/t.log 2>&1
  grep -E "^\s+Başarısız Vid|Başarısız!|Başarılı!" .calisma/T147/t.log | sed 's/\[.*//' >> "$OUT"
  echo >> "$OUT"
}

git checkout -- "$EC" "$SE"; run_case "M0 TABAN"

git checkout -- "$EC" "$SE"
sed -i 's|=> exitCode == 0 \&\& FfmpegDiagnostics.DroppedOptionLines(diagnostic).Count == 0;|=> exitCode == 0;|' "$EC"
run_case "M1 yoklama sozlugu hic okumuyor, yalniz cikis koduna bakiyor"

git checkout -- "$EC" "$SE"
sed -i 's|=> exitCode == 0 \&\& FfmpegDiagnostics.DroppedOptionLines(diagnostic).Count == 0;|=> exitCode == 0 \&\& !diagnostic.Contains("Unknown", StringComparison.OrdinalIgnoreCase);|' "$EC"
run_case "M2 sozluk yerine genis desen: Unknown"

git checkout -- "$EC" "$SE"
sed -i 's|=> exitCode == 0 \&\& FfmpegDiagnostics.DroppedOptionLines(diagnostic).Count == 0;|=> FfmpegDiagnostics.DroppedOptionLines(diagnostic).Count == 0;|' "$EC"
run_case "M3 cikis kodu kapisi kaldirildi"

git checkout -- "$EC" "$SE"
sed -i 's|            DroppedOptions = DroppedAcross(runs)|            DroppedOptions = Array.Empty<string>()|' "$SE"
run_case "M4 onizleme tasimayi okumuyor"

git checkout -- "$EC" "$SE"
sed -i 's|        => runs.SelectMany(run => run.DroppedOptions ?? Array.Empty<string>()).ToArray();|        => runs.Take(1).SelectMany(run => run.DroppedOptions ?? Array.Empty<string>()).ToArray();|' "$SE"
run_case "M5 birlesim yalniz ilk kosumu okuyor"

git checkout -- "$EC" "$SE"
sed -i 's|            "-hide_banner", "-loglevel", "error", "-nostdin", "-y",|            "-hide_banner", "-loglevel", "info", "-nostdin", "-y",|' "$SE"
run_case "M6 kaynak parca seviyesi sessizce info'ya yukseltildi"

git checkout -- "$EC" "$SE"
echo "=== calisma agaci geri alindi ===" >> "$OUT"
git status --short >> "$OUT"
