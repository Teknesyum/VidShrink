using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

/// <summary>
/// Denetim şeridi: başlat, duraklat, sürdür, durdur. Dört düğme de motora
/// <see cref="RecorderSession"/> üzerinden ulaşıyor; şerit kendi kaydını tutmuyor,
/// oturumun halini okuyor.
///
/// <para><b>Şeridin okuduğu sayılar.</b> Kayıt sürerken yalnız geçen süre ve kare sayısı
/// akıyor. Yazılan boyut akmıyor: <c>-progress</c> akışında <c>total_size</c> gdigrab
/// yakalamasında on bir ilerleme bloğunun onunda sıfır kalıyor ve gerçek değer ancak son
/// blokta geliyor (8a ölçümü). Bu yüzden şeritte canlı boyut göstergesi <b>yok</b>; boyut
/// kayıt bitince <see cref="RecordResult.OutputMb"/>'den, yani dosyanın kendisinden
/// okunuyor.</para>
/// </summary>
internal partial class RecorderView
{
    private RecorderSession? _session;

    /// <summary>Oturum yoksa hal <see cref="RecorderState.Stopped"/>: şerit boşta okunur.</summary>
    internal RecorderState State => _session?.State ?? RecorderState.Stopped;

    internal bool HasSession => _session is not null;

    internal string ElapsedText => TxtElapsed.Text ?? string.Empty;

    internal string FramesText => TxtFrames.Text ?? string.Empty;

    internal string DroppedText => TxtDropped.Text ?? string.Empty;

    internal string ResultPathText => TxtResultPath.Text ?? string.Empty;

    /// <summary>
    /// Geçen sürenin yazımı. Sayılar değişmez biçimde yazılıyor, yani dil değişince
    /// kaymıyor; bir saati geçen kayıt <c>01:02:12</c> biçimine geçer.
    /// </summary>
    internal static string Clock(TimeSpan at)
        => at.TotalHours >= 1
            ? at.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : at.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

    private void InitSerit()
        => ShowProgress(new RecordProgress(TimeSpan.Zero, TimeSpan.Zero, 0, 0, 0));

    private async void OnStart(object? sender, RoutedEventArgs e) => await StartAsync();

    private async void OnPause(object? sender, RoutedEventArgs e) => await PauseAsync();

    private async void OnResume(object? sender, RoutedEventArgs e) => await ResumeAsync();

    private async void OnStop(object? sender, RoutedEventArgs e) => await StopAsync();

    /// <summary>
    /// Kaydı başlatır. Başlamayan kaydın sebebi yutulmuyor: ffmpeg eksikse, istek
    /// doğrulamadan geçmiyorsa ya da ilk ilerleme bloğu gelmeden süreç ölüyorsa ekranda
    /// kendi satırı çıkıyor.
    /// </summary>
    internal async Task StartAsync()
    {
        if (_session is not null) return;

        ClearMessages();

        if (!ToolLocator.IsAvailable(out var missing))
        {
            ShowError(Say("recorder.error.no-ffmpeg", missing ?? string.Empty));
            return;
        }

        if (BuildRequest() is not { } request) return;

        StoreChoices();
        var path = _settings.OutputPath(DateTime.Now);

        var errors = RecorderArguments.Validate(request, path);
        if (errors.Count > 0)
        {
            ShowError(Say("recorder.error.invalid", string.Join(" ", errors)));
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _settings.ResolveFolder());
            _session = await RecorderSession.StartAsync(
                request, path, new Progress<RecordProgress>(ShowProgress));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            _session = null;
            ShowError(Say("recorder.error.start", ex.Message));
        }

        RefreshSerit();
    }

    /// <summary>O anki parçayı nazikçe kapatır; dosya kapalı ve oynatılabilir kalır.</summary>
    internal async Task PauseAsync()
    {
        if (_session is null) return;
        try { await _session.PauseAsync(); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException) { ShowError(Say("recorder.error.start", ex.Message)); }
        RefreshSerit();
    }

    /// <summary>Duraklatılmış kayda yeni bir parça açar.</summary>
    internal async Task ResumeAsync()
    {
        if (_session is null) return;
        try { await _session.ResumeAsync(); }
        catch (Exception ex) when (ex is InvalidOperationException or IOException) { ShowError(Say("recorder.error.start", ex.Message)); }
        RefreshSerit();
    }

    /// <summary>
    /// Kaydı bitirir ve teslim edilen dosyayı gösterir. Yarım dosya da gösteriliyor:
    /// <see cref="RecordResult.Partial"/> doğruyken yol yine görünür, yanına da oynatılabilir
    /// sayılmadığı yazılır.
    /// </summary>
    internal async Task StopAsync()
    {
        if (_session is null) return;

        var session = _session;
        try
        {
            var result = await session.StopAsync();
            ShowResult(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            ShowError(Say("recorder.error.start", ex.Message));
        }
        finally
        {
            _session = null;
            RefreshSerit();
        }
    }

    private void ShowProgress(RecordProgress progress)
    {
        TxtElapsed.Text = Clock(progress.Elapsed);
        TxtFrames.Text = progress.Frames.ToString("N0", Strings.Culture);
        TxtDropped.Text = progress.DroppedFrames.ToString("N0", Strings.Culture);
    }

    /// <summary>
    /// Şeridin yüzü. Düğmeler duruma göre görünür ya da görünmez oluyor; devre dışı düğme
    /// bırakılmıyor, çünkü basılamayan bir düğme kendi başına bir açıklama borcu doğurur.
    /// </summary>
    private void RefreshSerit()
    {
        var state = State;
        var running = _session is not null && state == RecorderState.Running;
        var paused = _session is not null && state == RecorderState.Paused;

        BtnStart.IsVisible = _session is null;
        BtnPause.IsVisible = running;
        BtnResume.IsVisible = paused;
        BtnStop.IsVisible = running || paused;

        LiveDot.IsVisible = running;
        TxtState.Text = running ? Say("recorder.strip.live")
            : paused ? Say("recorder.strip.paused")
            : Say("recorder.strip.idle");
    }
}
