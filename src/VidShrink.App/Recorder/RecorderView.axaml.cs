using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

/// <summary>
/// Kaydedici sekmesinin gövdesi. Kalıp <c>Playback/PlayerView</c> ile aynı: görünüm bir
/// <see cref="UserControl"/>, konular kısmi sınıflara bölünmüş — şerit
/// <c>RecorderView.Serit.cs</c>'de, hedef seçimi <c>RecorderView.Hedef.cs</c>'de, kalıcı
/// ayar <c>RecorderSettings.cs</c>'de.
///
/// <para>Görünüm ana pencereye bağlanmıyor: çıkış klasörünü ve ayar yolunu kendisi
/// çözüyor. Böylece <c>MainWindow</c> tarafında sekmeyi açan indeks alanından başka bir
/// kablo gerekmiyor.</para>
/// </summary>
internal partial class RecorderView : UserControl
{
    private readonly RecorderSettings _settings;

    private string? _lastRecording;

    /// <summary>Biten kaydı küçültme sekmesine taşıyan kapı; ana pencere kuruyor.</summary>
    internal Func<string, Task>? OpenInShrink { get; set; }

    /// <summary>Biten kaydı oynatıcı sekmesinde açan kapı; ana pencere kuruyor.</summary>
    internal Func<string, Task>? OpenInPlayer { get; set; }

    public RecorderView()
    {
        InitializeComponent();
        _settings = RecorderSettings.Load(RecorderSettings.FilePath);
        InitHedef();
        InitOtomatik();
        InitSes();
        InitSerit();
        InitGeriSayim();
        InitMini();
        RefreshSerit();
    }

    /// <summary>Sayfadaki hata satırı. Ölçüm kendi gördüğünü okuyabilsin diye açık.</summary>
    internal string ErrorText => TxtError.IsVisible ? TxtError.Text ?? string.Empty : string.Empty;

    /// <summary>
    /// Dil değişimine abonelik. Şeridin durum etiketi ve hedef kutusunun öğeleri XAML'dan
    /// değil kodla yazılıyor; abonelik olmadan bu iki yer eski dilde kalıyor ve
    /// <c>LanguageTests.DilDegisinceEkrandaOtekiDilinMetniKalmiyor</c> kırmızı veriyor.
    /// Kalıp <c>Playback/PlayerAdvancedPanel</c> ile aynı.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Strings.Changed -= OnLanguageChanged;
        Strings.Changed += OnLanguageChanged;
        RefreshLanguage();
        ActivateTray();
        ActivateHotkeys();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Strings.Changed -= OnLanguageChanged;
        DeactivateTray();
        DeactivateHotkeys();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess()) RefreshLanguage();
        else Dispatcher.UIThread.Post(RefreshLanguage);
    }

    /// <summary>Kodla yazılan bütün metinleri yeniden üretir; seçimler korunuyor.</summary>
    private void RefreshLanguage()
    {
        RefreshTargetLabels();
        RefreshAudioBoxes();
        RefreshCountdownLabels();
        RefreshSerit();
    }

    internal RecorderSettings Settings => _settings;

    private static string Say(string key) => LanguageCatalog.Display(Strings.Get(key));

    private static string Say(string key, params object?[] args)
        => string.Format(Strings.Culture, LanguageCatalog.Display(Strings.Get(key)), args);

    private void ClearMessages()
    {
        TxtError.IsVisible = false;
        TxtError.Text = string.Empty;
        TxtNotice.IsVisible = false;
        TxtNotice.Text = string.Empty;
        ResultPanel.IsVisible = false;
        TxtWarning.IsVisible = false;
        _lastRecording = null;
        ResetShare();
    }

    private void ShowError(string message)
    {
        TxtError.Text = message;
        TxtError.IsVisible = true;
    }

    internal string NoticeText => TxtNotice.IsVisible ? TxtNotice.Text ?? string.Empty : string.Empty;

    private void ShowNotice(string message)
    {
        TxtNotice.Text = message;
        TxtNotice.IsVisible = true;
    }

    /// <summary>
    /// Biten kaydın teslimi. Yol her koşulda görünür oluyor — yarım dosyada bile — çünkü
    /// kullanıcının aradığı ilk şey dosyanın nereye yazıldığı.
    /// </summary>
    internal void ShowResult(RecordResult result)
    {
        ResultPanel.IsVisible = true;
        _lastRecording = result.OutputPath;
        TxtResultPath.Text = result.OutputPath;
        BtnToMp4.IsVisible = VidShrink.Core.RecorderArguments.ContainerOf(result.OutputPath) == VidShrink.Core.RecorderContainer.Mkv;
        ResetShare();
        TxtResult.Text = Say(
            "recorder.output.done",
            result.OutputMb.ToString("0.0", Strings.Culture),
            result.Segments.ToString(CultureInfo.InvariantCulture));

        if (result.Partial)
        {
            TxtWarning.Text = Say("recorder.output.partial");
            TxtWarning.IsVisible = true;
        }
        else if (!result.Ok)
        {
            TxtWarning.Text = Say("recorder.output.failed", result.ExitCode.ToString(CultureInfo.InvariantCulture));
            TxtWarning.IsVisible = true;
        }
    }

    private void OnReveal(object? sender, RoutedEventArgs e)
    {
        if (Delivered() is { } path) VidShrink.App.Platform.Reveal(path);
    }

    /// <summary>
    /// Teslim edilmiş dosya. Yol kutudan değil alandan okunuyor: kutu salt okunur olsa da
    /// bir metin kutusudur, sonraki işin girdisi ekrandaki metne bağlanmaz.
    /// </summary>
    private string? Delivered()
        => !string.IsNullOrWhiteSpace(_lastRecording) && File.Exists(_lastRecording) ? _lastRecording : null;

    private async void OnToShrink(object? sender, RoutedEventArgs e)
    {
        if (Delivered() is { } path && OpenInShrink is { } gate) await gate(path);
    }

    private async void OnToPlayer(object? sender, RoutedEventArgs e)
    {
        if (Delivered() is { } path && OpenInPlayer is { } gate) await gate(path);
    }

    internal bool Mp4Visible => BtnToMp4.IsVisible;

    private async void OnToMp4(object? sender, RoutedEventArgs e)
        => await SaveAsMp4Async(async args => (await FfmpegRunner.RunAsync(args)).Ok);

    internal async Task<string?> SaveAsMp4Async(Func<System.Collections.Generic.IReadOnlyList<string>, Task<bool>> run)
    {
        if (Delivered() is not { } source) return null;

        var target = VidShrink.Core.RecorderArguments.RemuxTarget(source, File.Exists);
        bool ok;
        try { ok = await run(VidShrink.Core.RecorderArguments.BuildRemuxToMp4(source, target)); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            ok = false;
        }

        if (ok)
        {
            TxtError.IsVisible = false;
            ShowNotice(Say("recorder.output.mp4-saved", target));
            return target;
        }

        TxtNotice.IsVisible = false;
        ShowError(Say("recorder.output.mp4-failed"));
        return null;
    }

    /// <summary>
    /// Çıkış klasörünü seçer. Kalıp <c>Playback/PlayerView.Window.cs</c>'deki klasör
    /// seçiciyle aynı: seçici yoksa iş sessizce bırakılıyor, kutunun içindeki yol duruyor.
    /// </summary>
    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanPickFolder: true } storage) return;

        try
        {
            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Say("recorder.output.browse"),
                AllowMultiple = false
            });

            if (folders.Count == 0) return;
            var picked = folders[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(picked)) return;

            TxtOutputFolder.Text = picked;
            _settings.OutputFolder = picked;
            _settings.Save(RecorderSettings.FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowError(Say("recorder.error.folder", ex.Message));
        }
    }
}
