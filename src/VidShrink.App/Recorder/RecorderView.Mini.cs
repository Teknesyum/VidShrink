using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

/// <summary>
/// Mini kip: kaydedici sekmesi TinyTask ölçüsünde bir şeride küçülüyor.
///
/// <para><b>Geçiş şeritten, ayardan değil.</b> ScreenToGif'in kompakt kipi Ayarlar
/// altındaki bir onay kutusu ve kullanıcı bulamazsa hiç kullanmıyor. Burada geçiş
/// şeridin kendi düğmesinde; tıkla-geç, tıkla-dön.</para>
///
/// <para><b>Sıcak tuşlar F7 ve F8.</b> ScreenToGif ve TinyTask'ın varsayılanı. Windows'ta
/// Oyun Çubuğu'nun <c>Win+Alt+R</c>'siyle çakışmıyor. Tanım büyük pencerede duruyor,
/// tuş mini kipte de çalışıyor: iki pencere de aynı <see cref="OnHotkey"/>'e bağlı.</para>
/// </summary>
internal partial class RecorderView
{
    private RecorderMini? _mini;

    /// <summary>Mini kip açık mı. Ölçüm kendi gördüğünü okuyabilsin diye açık.</summary>
    internal bool MiniOpen => _mini is not null;

    /// <summary>Kadrajın içine düşeceği bildirilen kayıt var mı; tam ekran kaydında doğru.</summary>
    internal bool MiniInFrame { get; private set; }

    private void InitMini()
    {
        BtnMini.Click += OnShrinkToMini;
        AddHandler(KeyDownEvent, OnHotkey, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// F7 başlat/duraklat, F8 durdur. Tuş ancak kaydedici görünürken dinleniyor: başka
    /// sekmedeyken F7'ye basan kullanıcı kayıt başlatmak istemiyor.
    /// </summary>
    private async void OnHotkey(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None) return;

        switch (e.Key)
        {
            case Key.F7:
                e.Handled = true;
                await ToggleAsync();
                break;

            case Key.F8 when HasSession:
                e.Handled = true;
                await StopAsync();
                break;
        }
    }

    /// <summary>
    /// Tek düğmeye indirilmiş başlat/duraklat/sürdür. Saha taramasındaki 22 kompakt
    /// yüzeyin hepsinde bu üçü tek sütunu paylaşıyor: yarısı hep boş duran ikinci bir
    /// 40 piksel ayırmanın karşılığı yok.
    /// </summary>
    internal async System.Threading.Tasks.Task ToggleAsync()
    {
        switch (State)
        {
            case RecorderState.Running when HasSession: await PauseAsync(); break;
            case RecorderState.Paused when HasSession: await ResumeAsync(); break;
            default: await StartAsync(); break;
        }
    }

    private void OnShrinkToMini(object? sender, RoutedEventArgs e) => ShrinkToMini();

    /// <summary>
    /// Mini kipi açar ve ana pencereyi gizler. Şerit kadrajın dışına konumlanıyor;
    /// bölge tam ekransa dışarısı yok ve kullanıcıya şeridin kayda gireceği söyleniyor.
    /// </summary>
    internal void ShrinkToMini()
    {
        if (_mini is not null) return;

        var owner = TopLevel.GetTopLevel(this) as Window;

        _mini = new RecorderMini();
        _mini.ToggleRequested += async (_, _) => await ToggleAsync();
        _mini.StopRequested += async (_, _) => await StopAsync();
        _mini.ExpandRequested += (_, _) => ExpandFromMini();
        _mini.Closed += (_, _) => ExpandFromMini();
        _mini.AddHandler(KeyDownEvent, OnHotkey, RoutingStrategies.Tunnel);

        _mini.Show();

        var region = FrameRegion();
        MiniInFrame = region is null;
        if (MiniInFrame) ShowError(Say("recorder.mini.in-frame"));

        _mini.PlaceOutside(region);
        RefreshMini();

        owner?.Hide();
    }

    /// <summary>Mini kipi kapatır ve ana pencereyi geri getirir.</summary>
    internal void ExpandFromMini()
    {
        var mini = _mini;
        if (mini is null) return;

        _mini = null;
        MiniInFrame = false;

        mini.RemoveHandler(KeyDownEvent, OnHotkey);
        mini.Close();

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            owner.Show();
            owner.Activate();
        }
    }

    /// <summary>
    /// Şeridin dışına konacağı kadraj. Bölge seçilmemişse — yani tam ekran ya da pencere
    /// kaydı — dışarısı yok ve <c>null</c> dönüyor.
    /// </summary>
    private PixelRect? FrameRegion()
        => BuildRequest(applyAuto: false) is { Region: { } r }
            ? new PixelRect(r.X, r.Y, r.Width, r.Height)
            : null;

    private void RefreshMini() => _mini?.Follow(State, TxtElapsed.Text ?? string.Empty);
}
