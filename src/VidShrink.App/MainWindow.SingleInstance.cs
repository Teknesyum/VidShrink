using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.App.Integration;

namespace VidShrink.App;

public partial class MainWindow
{
    internal const string InstanceTraceVariable = "VIDSHRINK_INSTANCE_TRACE";

    /// <summary>
    /// Gerçek denetim <see cref="OperatingSystem.IsMacOS"/>; ölçüler macOS'un
    /// <c>IActivatableLifetime.Activated</c> olayını tetiklemeden aynı kuyruk çağrısını
    /// (<see cref="AcceptForwarded"/>) başka bir platformda da macOS koluna sokabilsin diye
    /// değiştirilebilir. Üretimde hiç dokunulmaz.
    /// </summary>
    internal static Func<bool> IsMacOSPlatformForTest = OperatingSystem.IsMacOS;

    /// <summary>
    /// Kabuk çoklu seçimde dosya başına ayrı bir süreç doğurur; her biri yolunu ayrı iletir.
    /// Son kabuk açılışından bu süre geçmeden gelen yol yeni açılış sayılmaz, öncekilerle
    /// birlikte tek kuyruk olur.
    /// </summary>
    internal static TimeSpan ShellBatchWindow { get; set; } = TimeSpan.FromSeconds(1.5);

    private WindowState _restoreState = WindowState.Maximized;
    private ForwardedRequest? _pendingMacRequest;
    private readonly List<string> _shellBatch = new();
    private DateTime _shellBatchAt = DateTime.MinValue;
    private Task _shellTail = Task.CompletedTask;

    internal void AcceptForwardedFiles(ForwardedFiles files)
    {
        PropertyChanged += (_, change) =>
        {
            if (change.Property == WindowStateProperty && WindowState != WindowState.Minimized)
                _restoreState = WindowState;
        };
        Opened += (_, _) => files.Attach(AcceptForwarded);
    }

    /// <summary>
    /// Windows/Linux'ta bir başka süreç aynı yolu ilettiğinde ve macOS'ta işletim sistemi
    /// <c>Activated</c> ile dosya açtığında tek giriş noktası. Kodlama sürüyorsa Windows'ta
    /// hâlâ <c>false</c> döner — çağıran taraf (<see cref="SingleInstanceChannel"/> üstünden
    /// gelen ikinci süreç) bunu HATA sayıp kendi penceresini açar. macOS'ta ise ayrı bir süreç
    /// yok; ret, dosyanın sessizce düşmesi olurdu. Orada yol bekletilip kodlama bitince
    /// (iptal ya da tamamlanma) <see cref="FlushPendingMacFile"/> ile yüklenir.
    /// </summary>
    internal bool AcceptForwarded(ForwardedRequest request)
    {
        if (_cts is not null)
        {
            if (!IsMacOSPlatformForTest()) return false;

            _pendingMacRequest = request;
            ReportSourceError(Say("main.instance.waiting", request.First is null ? "-" : Path.GetFileName(request.First)));
            return true;
        }

        if (!request.Enqueue || Player.LoadedPath is null)
        {
            if (WindowState == WindowState.Minimized) WindowState = _restoreState;
            Activate();
        }

        if (request.Paths.Count == 0) return true;
        if (request.Enqueue)
        {
            ForwardedLoad = AfterShellWork(() => EnqueueForwardedAsync(request.Paths));
            return true;
        }

        if (JoinShellBatch(request.Paths) is { } batch)
        {
            ForwardedLoad = AfterShellWork(() => OpenShellBatchAsync(batch));
            return true;
        }

        var path = request.Paths[0];
        ForwardedLoad = AfterShellWork(() => LoadAndTraceAsync(path));
        return true;
    }

    /// <summary>
    /// Yolları kabuk partisine yazar. Parti tek yolda kalıyorsa <c>null</c> döner ve yol
    /// eskisi gibi tek dosya açılır; birden çok yol biriktiyse partinin tamamı döner.
    /// </summary>
    private IReadOnlyList<string>? JoinShellBatch(IReadOnlyList<string> paths)
    {
        var now = DateTime.UtcNow;
        if (_shellBatch.Count == 0 || now - _shellBatchAt >= ShellBatchWindow) _shellBatch.Clear();
        _shellBatchAt = now;
        _shellBatch.AddRange(paths);
        return _shellBatch.Count > 1 ? _shellBatch.ToArray() : null;
    }

    private Task StartShellOpen(string path, Func<Task> load)
    {
        _shellBatch.Clear();
        _shellBatch.Add(path);
        _shellBatchAt = DateTime.UtcNow;
        return AfterShellWork(load);
    }

    private Task AfterShellWork(Func<Task> work)
    {
        _shellTail = RunAfter(_shellTail, work);
        return _shellTail;
    }

    private static async Task RunAfter(Task previous, Func<Task> work)
    {
        try { await previous; }
        catch (Exception) { }
        await work();
    }

    private async Task OpenShellBatchAsync(IReadOnlyList<string> batch)
    {
        Tabs.SelectedIndex = PlayerTabIndex;
        if (Player.OpenShellBatch(batch)) await Player.Navigation;
    }

    private async Task EnqueueForwardedAsync(IReadOnlyList<string> paths)
    {
        var idle = Player.LoadedPath is null;
        if (idle) Tabs.SelectedIndex = PlayerTabIndex;
        if (Player.EnqueueItems(paths) && idle) await Player.Navigation;
    }

    /// <summary>
    /// Kodlama bittiğinde (iptal ya da tamamlanma) <c>OnStart</c>/<c>OnConvert</c>'in
    /// <c>finally</c> kolundan çağrılır. Bekleyen bir macOS dosyası yoksa hiçbir şey yapmaz.
    /// </summary>
    private void FlushPendingMacFile()
    {
        if (_pendingMacRequest is not { } request) return;
        _pendingMacRequest = null;
        AcceptForwarded(request);
    }

    internal void BeginEncodingForTest() => _cts ??= new CancellationTokenSource();

    internal void EndEncodingForTest()
    {
        var cts = _cts;
        _cts = null;
        cts?.Dispose();
        FlushPendingMacFile();
    }

    internal Task? ForwardedLoad { get; private set; }

    private async Task LoadAndTraceAsync(string path)
    {
        try
        {
            await LoadStartupFileAsync(path);
        }
        catch (Exception ex)
        {
            ReportSourceError($"{Say("main.error.startup")}: {ex.Message}");
        }

        WriteInstanceTrace(path);
    }

    private void WriteInstanceTrace(string path)
    {
        var trace = Environment.GetEnvironmentVariable(InstanceTraceVariable);
        if (string.IsNullOrWhiteSpace(trace)) return;

        try
        {
            File.AppendAllText(trace, $"{path}\t{TxtFileName.Text}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
