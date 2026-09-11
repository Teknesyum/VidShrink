using System;
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

    private WindowState _restoreState = WindowState.Maximized;
    private string? _pendingMacPath;
    private bool _pendingMacFile;

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
    internal bool AcceptForwarded(string? path)
    {
        if (_cts is not null)
        {
            if (!IsMacOSPlatformForTest()) return false;

            _pendingMacPath = path;
            _pendingMacFile = true;
            ReportSourceError(Say("main.instance.waiting", path is null ? "-" : Path.GetFileName(path)));
            return true;
        }

        if (WindowState == WindowState.Minimized) WindowState = _restoreState;
        Activate();

        if (path is not null) _ = LoadForwardedAsync(path);
        return true;
    }

    /// <summary>
    /// Kodlama bittiğinde (iptal ya da tamamlanma) <c>OnStart</c>/<c>OnConvert</c>'in
    /// <c>finally</c> kolundan çağrılır. Bekleyen bir macOS dosyası yoksa hiçbir şey yapmaz.
    /// </summary>
    private void FlushPendingMacFile()
    {
        if (!_pendingMacFile) return;
        _pendingMacFile = false;
        var path = _pendingMacPath;
        _pendingMacPath = null;
        AcceptForwarded(path);
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

    private Task LoadForwardedAsync(string path)
    {
        ForwardedLoad = LoadAndTraceAsync(path);
        return ForwardedLoad;
    }

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
