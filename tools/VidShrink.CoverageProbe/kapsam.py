import io, subprocess, sys

PH = 'src/VidShrink.App/Playback/PanelHost.cs'

SONDALAR = ('116', '121', '709', '743', '750', '766', '772', '773', '778')


def geri():
    subprocess.check_call(['git', 'checkout', '--', PH])
    print('sonda geri alindi: ' + PH)


def uygula(which):
    s = io.open(PH, encoding='utf-8-sig', newline='').read().replace('\r\n', '\n')

    def sub(old, new):
        assert s.count(old) == 1, 'anchor count %d: %s' % (s.count(old), old[:50])
        return s.replace(old, new)

    def firlat(satir):
        return 'throw new InvalidOperationException("KAPSAM %s");' % satir

    if which == '116':
        s = sub("        _settle.Tick += (_, _) => SettleElapsed();",
                '        _settle.Tick += (_, _) => ' + firlat('116'))
    elif which == '121':
        s = sub("        _segmentDelay.Tick += (_, _) => { _ = SegmentDelayElapsed(); };",
                '        _segmentDelay.Tick += (_, _) => ' + firlat('121'))
    elif which == '709':
        s = sub("    private void Drain()\n    {\n        var source = _source;",
                "    private void Drain()\n    {\n        " + firlat('709')
                + "\n#pragma warning disable CS0162\n        var source = _source;")
        s = sub("        SampleRate();\n    }",
                "        SampleRate();\n#pragma warning restore CS0162\n    }")
    elif which == '743':
        s = sub("        _submitted++;",
                '        if (_submitted >= 0) ' + firlat('743') + '\n        _submitted++;')
    elif which == '750':
        s = sub("        if (clip is not null) Follow(clip, position);",
                '        if (_submitted >= 0) ' + firlat('750'))
    elif which == '766':
        s = sub("        if (!_panel.Controls.IsPlaying) return;\n        var played = position.TotalSeconds;",
                '        ' + firlat('766')
                + '\n#pragma warning disable CS0162\n        if (!_panel.Controls.IsPlaying) return;'
                + '\n        var played = position.TotalSeconds;')
        s = sub("        AdvanceClip();\n    }",
                "        AdvanceClip();\n#pragma warning restore CS0162\n    }")
    elif which == '772':
        s = sub("        if (played >= clip.DurationSeconds * PrefetchAtFraction) _ = PrepareAheadAsync();",
                '        if (played >= clip.DurationSeconds * PrefetchAtFraction) ' + firlat('772'))
    elif which == '773':
        s = sub("        if (played >= clip.DurationSeconds - HandoverLeadSeconds) BeginHandover();",
                '        if (played >= clip.DurationSeconds - HandoverLeadSeconds) ' + firlat('773'))
    elif which == '778':
        s = sub("        if (_standby is { } standby && standby.Status.ProducedFrames > 0) { SwapToStandby(); return; }\n        AdvanceClip();",
                '        ' + firlat('778'))
    else:
        raise SystemExit('bilinmeyen sonda: ' + which)

    io.open(PH, 'w', encoding='utf-8-sig', newline='').write(s.replace('\n', '\r\n'))
    print('kapsam sondasi %s uygulandi' % which)


if len(sys.argv) != 2:
    raise SystemExit('kullanim: kapsam.py <%s | --geri | --liste>' % ' | '.join(SONDALAR))
arg = sys.argv[1]
if arg == '--liste':
    print(' '.join(SONDALAR))
elif arg == '--geri':
    geri()
else:
    uygula(arg)
