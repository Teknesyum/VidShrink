using System;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

/// <summary>
/// Seçili ses girişinin canlı seviyesi. Her kutunun altındaki çubuk, o kutuda cihaz
/// seçiliyken ve kaynak açılabildiyse görünür; kaynak kayıt sürecinden ayrı bir okuyucudur
/// (<see cref="AudioLevelSource"/>), çubuğun değeri <see cref="AudioLevel"/> kararlarından gelir.
///
/// <para>Okuyucu yalnız sekme görsel ağaçtayken yaşar. Kayıt başlarken kapatılır ve kayıt
/// süreci cihazı aldıktan sonra yeniden açılır: cihazı önce kayıt alır, paylaşamayan bir
/// sürücüde düşen taraf gösterge olur.</para>
/// </summary>
internal partial class RecorderView
{
    private sealed class LevelSlot
    {
        public LevelSlot(AudioSourceRole role, ProgressBar bar)
        {
            Role = role;
            Bar = bar;
        }

        public AudioSourceRole Role { get; }

        public ProgressBar Bar { get; }

        public AudioCaptureDevice? Device { get; set; }

        public IAudioLevelSource? Source { get; set; }

        public AudioLevelHold Hold { get; set; } = AudioLevelHold.Silent;
    }

    private LevelSlot[] _levels = Array.Empty<LevelSlot>();

    private bool _levelsLive;

    private bool _levelsPaused;

    private Window? _levelWindow;

    /// <summary>Okuyucuyu açan kapı; ölçüm sahte kaynak verir.</summary>
    internal Func<AudioCaptureDevice, IAudioLevelSource?> OpenLevelSource { get; set; } = AudioLevelSource.Open;

    private void InitSesSeviyesi()
    {
        _levels = new[]
        {
            new LevelSlot(AudioSourceRole.Microphone, BarMicrophoneLevel),
            new LevelSlot(AudioSourceRole.SystemAudio, BarSystemAudioLevel)
        };
        CmbMicrophone.SelectionChanged += (_, _) => RefreshLevels();
        CmbSystemAudio.SelectionChanged += (_, _) => RefreshLevels();
    }

    private void ActivateLevels()
    {
        _levelsLive = true;
        _levelWindow = TopLevel.GetTopLevel(this) as Window;
        if (_levelWindow is not null) _levelWindow.Closed += OnLevelWindowClosed;
        RefreshLevels();
    }

    private void DeactivateLevels()
    {
        _levelsLive = false;
        if (_levelWindow is not null) _levelWindow.Closed -= OnLevelWindowClosed;
        _levelWindow = null;
        RefreshLevels();
    }

    private void OnLevelWindowClosed(object? sender, EventArgs e) => DeactivateLevels();

    internal void PauseLevels()
    {
        _levelsPaused = true;
        RefreshLevels();
    }

    internal void ResumeLevels()
    {
        _levelsPaused = false;
        RefreshLevels();
    }

    /// <summary>Açık okuyucuları kapatıp seçime göre yeniden açar; biten kayıt ve liste yenileme çağırır.</summary>
    internal void RestartLevels()
    {
        foreach (var slot in _levels) CloseLevel(slot);
        RefreshLevels();
    }

    /// <summary>
    /// Her kutunun okuyucusunu o anki seçime eşitler. Seçim değişmediyse açık okuyucuya
    /// dokunulmaz; dil değişimi kutuları yeniden doldururken süreç yeniden açılmaz. Açılamayan
    /// ya da kendiliğinden biten okuyucu aynı cihaz için yeniden denenmez.
    /// </summary>
    private void RefreshLevels()
    {
        if (_fillingAudio) return;

        foreach (var slot in _levels)
        {
            var wanted = _levelsLive && !_levelsPaused ? Chosen(slot.Role) : null;
            if (wanted == slot.Device) continue;

            CloseLevel(slot);
            if (wanted is null) continue;

            slot.Device = wanted;
            var source = OpenLevelSource(wanted);
            if (source is null) continue;

            slot.Source = source;
            source.Level += db => Dispatcher.UIThread.Post(() => ShowLevel(slot, source, db));
            source.Ended += () => Dispatcher.UIThread.Post(() => EndLevel(slot, source));
            slot.Bar.IsVisible = true;
        }
    }

    private static void CloseLevel(LevelSlot slot)
    {
        var source = slot.Source;
        slot.Source = null;
        slot.Device = null;
        slot.Hold = AudioLevelHold.Silent;
        slot.Bar.Value = 0;
        slot.Bar.IsVisible = false;
        source?.Dispose();
    }

    private static void ShowLevel(LevelSlot slot, IAudioLevelSource source, double db)
    {
        if (!ReferenceEquals(slot.Source, source)) return;
        slot.Hold = AudioLevel.Advance(slot.Hold, db, AudioLevel.WindowSeconds);
        slot.Bar.Value = AudioLevel.Fraction(slot.Hold.Db);
    }

    private static void EndLevel(LevelSlot slot, IAudioLevelSource source)
    {
        if (!ReferenceEquals(slot.Source, source)) return;
        var device = slot.Device;
        CloseLevel(slot);
        slot.Device = device;
    }

    internal bool LevelVisible(AudioSourceRole role) => LevelSlotOf(role).Bar.IsVisible;

    internal double LevelValue(AudioSourceRole role) => LevelSlotOf(role).Bar.Value;

    internal IAudioLevelSource? LevelSource(AudioSourceRole role) => LevelSlotOf(role).Source;

    private LevelSlot LevelSlotOf(AudioSourceRole role) => Array.Find(_levels, slot => slot.Role == role)!;
}
