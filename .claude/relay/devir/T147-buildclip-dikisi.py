import io
p = 'src/VidShrink.App/Playback/SegmentEncoder.cs'
s = io.open(p, encoding='utf-8-sig').read()

eski = """            var clip = new PreviewClip
            {
                SourcePath = sourcePath,
                EncodedPath = encodedPath,
                StartSeconds = start,
                DurationSeconds = segment.DurationSeconds,
                IsApproximate = segment.IsApproximate,
                Crf = segment.Plan.Crf,
                Elapsed = clock.Elapsed,
                DroppedOptions = DroppedAcross(runs)
            };"""
yeni = """            var clip = BuildClip(segment, sourcePath, encodedPath, start, runs, clock.Elapsed);"""
assert eski in s, 'parca kurulumu bulunamadi'
s = s.replace(eski, yeni)

capa = """    /// <summary>
    /// Parçayı üreten koşumlarda düşürülen ayarların birleşimi, sıra korunarak."""
yordam = """    /// <summary>
    /// Biten iki koşumdan parçayı kurar. Süreç başlatmaktan ayrı durur: ölçü, koşumların
    /// ürettiği parçayı ffmpeg koşturmadan pimler — özellikle düşürülen ayarların parçaya
    /// gerçekten bağlandığını.
    /// </summary>
    internal static PreviewClip BuildClip(
        PreviewSegment segment, string sourcePath, string encodedPath,
        double startSeconds, FfmpegRun[] runs, TimeSpan elapsed)
        => new()
        {
            SourcePath = sourcePath,
            EncodedPath = encodedPath,
            StartSeconds = startSeconds,
            DurationSeconds = segment.DurationSeconds,
            IsApproximate = segment.IsApproximate,
            Crf = segment.Plan.Crf,
            Elapsed = elapsed,
            DroppedOptions = DroppedAcross(runs)
        };

""" + capa
assert capa in s, 'DroppedAcross docstring capasi bulunamadi'
s = s.replace(capa, yordam, 1)

io.open(p, 'w', encoding='utf-8-sig', newline='').write(s)
print('BuildClip dikisi acildi')
