using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia.Threading;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// SIK-P7: sıranın zamanlanması, bekleyen işin yerinde düzenlenmesi ve bitiş sesi. Zamanlanan sıra
/// saat gelene dek duraklatılmış kalır; geri sayım satırı kalan süreyi yazar.
/// </summary>
public partial class ShrinkJobWindow
{
    private static readonly CodecPreference[] EditCodecs = { CodecPreference.Auto, CodecPreference.Compatible, CodecPreference.MaxCompression };

    private DispatcherTimer? _scheduleTimer;
    private DateTimeOffset? _scheduleAt;
    private int _editIndex = -1;
    private bool _quitAsked;
    private readonly Dictionary<ShrinkRequest, (int Mb, CodecPreference Codec)> _duzenlenen = new(ReferenceEqualityComparer.Instance);

    /// <summary>Zamanlamanın saati; testler sahtesini verir.</summary>
    internal TimeProvider ZamanSaati { get; set; } = TimeProvider.System;

    /// <summary>Bitiş sesinin yüzü; test konağında sessiz olan gelir.</summary>
    internal IBitisSesi Ses { get; set; } = IsBittiBildirimi.VarsayilanSes();

    /// <summary>"İş bitince ses çal" ayarı.</summary>
    internal bool BitisSesi { get; set; }

    /// <summary>Bitişte çalacak ses dosyası; boşsa sistem sesi.</summary>
    internal string BitisSesiDosyasi { get; set; } = "";

    /// <summary>Zamanlanan başlangıca kalan süre; zamanlama yoksa <c>null</c>.</summary>
    internal TimeSpan? ScheduleLeft => _scheduleAt is { } at ? at - ZamanSaati.GetLocalNow() : null;

    /// <summary>Sırayı verilen saatte ya da süre sonra başlatır. Okunamayan girişte zamanlamaz ve hata satırını gösterir.</summary>
    internal bool Schedule(QueueStartMode mode, string? text)
    {
        var at = QueueSchedule.Resolve(mode, text, ZamanSaati.GetLocalNow());
        if (at is null)
        {
            TxtScheduleError.Text = Say("main.shrink-job.schedule.bad");
            TxtScheduleError.IsVisible = true;
            return false;
        }
        TxtScheduleError.IsVisible = false;
        CancelCountdown();
        _scheduleAt = at;
        SetPaused(true, Say("main.shrink-job.paused"));
        ShowSchedule();
        _scheduleTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _scheduleTimer.Tick -= OnScheduleTick;
        _scheduleTimer.Tick += OnScheduleTick;
        _scheduleTimer.Start();
        return true;
    }

    private void OnScheduleTick(object? sender, EventArgs e) => ScheduleTick();

    /// <summary>Zamanlamanın bir vuruşu: saat geldiyse sıra sürer, gelmediyse kalan süre yazılır.</summary>
    internal void ScheduleTick()
    {
        if (_scheduleAt is not { } at) return;
        if (ZamanSaati.GetLocalNow() >= at) SetPaused(false);
        else ShowSchedule();
    }

    private void ShowSchedule()
    {
        if (ScheduleLeft is not { } left) return;
        var ts = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling(left.TotalSeconds)));
        var text = $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}";
        TxtCountdown.Text = Core.Bicim.Satir.Bagla(Say("main.shrink-job.countdown.start", text));
        CountdownRow.IsVisible = true;
    }

    private void ClearSchedule()
    {
        _scheduleTimer?.Stop();
        _scheduleAt = null;
        if (_countdownLeft is null) CountdownRow.IsVisible = false;
    }

    private void CancelSchedule() => ClearSchedule();

    private void WireSchedule()
    {
        CmbStartAt.ItemsSource = new[] { Say("main.shrink-job.start.at"), Say("main.shrink-job.start.after") };
        CmbStartAt.SelectedIndex = 0;
        BtnSchedule.Click += (_, _) => Schedule((QueueStartMode)Math.Max(0, CmbStartAt.SelectedIndex), TxtStartAt.Text);
    }

    private void WireEdit()
    {
        CmbEditCodec.ItemsSource = new[] { Say("main.codec.automatic"), Say("main.codec.compatible"), Say("main.codec.smallest") };
        BtnEditSave.Click += (_, _) => SaveEdit();
        BtnEditCancel.Click += (_, _) => EditPanel.IsVisible = false;
    }

    /// <summary>Bekleyen işin hedefini ve kodeğini sıradan çıkarmadan değiştirir.</summary>
    internal bool EditPending(int index, int mb, CodecPreference codec)
    {
        if (index < 0 || index >= _pump.Pending.Count || mb <= 0) return false;
        var old = _pump.Pending[index];
        var neu = new ShrinkRequest(mb, old.Path);
        if (!_pump.ReplacePending(old, neu)) return false;
        _duzenlenen.Remove(old);
        _geriGelen.Remove(old);
        _duzenlenen[neu] = (mb, codec);
        RefreshPending();
        return true;
    }

    private PlanOptions EditedOptions(int mb, CodecPreference codec)
    {
        var options = _template is null
            ? new PlanOptions
            {
                TargetMb = mb,
                Priority = _appSettings.EncodePriority,
                EncoderThreads = _appSettings.EncoderThreads > 0 ? _appSettings.EncoderThreads : null
            }
            : PlanCalculator.WithTarget(_template, mb);
        if (options.Codec != codec)
        {
            options.Codec = codec;
            options.LockedCodec = null;
        }
        return options;
    }

    private void BeginEdit(int index)
    {
        if (index < 0 || index >= _pump.Pending.Count) return;
        var request = _pump.Pending[index];
        var (mb, codec) = _duzenlenen.TryGetValue(request, out var edited)
            ? edited
            : ((int)Math.Round(_template?.TargetMb ?? request.TargetMegabytes), _template?.Codec ?? CodecPreference.Compatible);
        var codecIndex = Array.IndexOf(EditCodecs, codec);
        _editIndex = index;
        TxtEditTitle.Text = Say("main.shrink-job.edit.title", Path.GetFileName(request.Path));
        TxtEditTarget.Text = mb.ToString(CultureInfo.InvariantCulture);
        CmbEditCodec.SelectedIndex = codecIndex < 0 ? 1 : codecIndex;
        EditPanel.IsVisible = true;
    }

    private void SaveEdit()
    {
        if (!int.TryParse(TxtEditTarget.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mb)) return;
        var codec = EditCodecs[Math.Clamp(CmbEditCodec.SelectedIndex, 0, EditCodecs.Length - 1)];
        if (EditPending(_editIndex, mb, codec)) EditPanel.IsVisible = false;
    }
}
