using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using VidShrink.Core;

namespace VidShrink.App.Integration;

/// <summary>
/// Denetim kanalının uygulamadaki karşılığı. Ana pencere bunu uygular; ölçüler sahte bir konakla
/// komutun hangi kola düştüğünü pencere açmadan ve gerçek kayıt başlatmadan sınar.
/// </summary>
internal interface IControlHost
{
    ControlStatus Status();
    bool Play();
    bool Pause();
    bool Seek(double seconds);
    Task<ControlReply> OpenAsync(string path);
    Task<ControlReply> StartRecordingAsync();
    Task<ControlReply> StopRecordingAsync();
    bool SubmitShrink(ShrinkRequest request);
}

/// <summary>Komutun konakta yürütülmesi: argüman sınaması burada, işin kendisi konakta.</summary>
internal static class ControlCommands
{
    internal const string NoMedia = "no-media";

    internal static Func<string, bool> PathExists { get; set; } = path =>
    {
        try { return File.Exists(path); }
        catch (Exception) { return false; }
    };

    internal static async Task<ControlReply> RunAsync(IControlHost host, ControlCommand command)
    {
        switch (command.Name)
        {
            case ControlCommandNames.Status:
                return ControlReply.Success(host.Status());
            case ControlCommandNames.Play:
                return Done(host, host.Play(), NoMedia);
            case ControlCommandNames.Pause:
                return Done(host, host.Pause(), NoMedia);
            case ControlCommandNames.Seek:
                return Done(host, host.Seek(command.Seconds ?? 0), NoMedia);
            case ControlCommandNames.Open:
            {
                var path = command.Path!.Trim().Trim('"');
                if (!PathExists(path) && !MediaAddress.IsAddress(path))
                    return ControlReply.Failure(ControlErrors.BadArgument, nameof(ShrinkArgumentProblem.NoPath));
                return await host.OpenAsync(path);
            }
            case ControlCommandNames.RecordStart:
                return await host.StartRecordingAsync();
            case ControlCommandNames.RecordStop:
                return await host.StopRecordingAsync();
            case ControlCommandNames.EnqueueShrink:
            {
                var request = new ShrinkRequest(command.TargetMegabytes ?? 0, command.Path!.Trim().Trim('"'));
                if (ShrinkRequestResolver.Check(request, ShellIntegration.AcceptedShrinkTargetsMegabytes, PathExists) is { } problem)
                    return ControlReply.Failure(ControlErrors.BadArgument, problem.ToString());
                return host.SubmitShrink(request) ? ControlReply.Success(host.Status()) : ControlReply.Failure(ControlErrors.Failed);
            }
            default:
                return ControlReply.Failure(ControlErrors.UnknownCommand);
        }
    }

    private static ControlReply Done(IControlHost host, bool ok, string reason)
        => ok ? ControlReply.Success(host.Status()) : ControlReply.Failure(ControlErrors.NotReady, reason);
}

/// <summary>
/// Boru dinleyicisi pencere kurulmadan başlar; konak pencere açılınca bağlanır. Bağlanmadan gelen
/// komut <see cref="ControlErrors.NotReady"/> alır. Komut arayüz iş parçacığında yürür.
/// </summary>
internal sealed class ControlBridge
{
    internal static ControlBridge Shared { get; } = new();

    private readonly object _gate = new();
    private IControlHost? _host;

    internal void Attach(IControlHost host)
    {
        lock (_gate) _host = host;
    }

    internal void Detach(IControlHost host)
    {
        lock (_gate)
            if (ReferenceEquals(_host, host)) _host = null;
    }

    internal Task<ControlReply> ExecuteAsync(ControlCommand command)
    {
        IControlHost? host;
        lock (_gate) host = _host;
        if (host is null) return Task.FromResult(ControlReply.Failure(ControlErrors.NotReady));

        if (Dispatcher.UIThread.CheckAccess()) return ControlCommands.RunAsync(host, command);
        return Dispatcher.UIThread.InvokeAsync(() => ControlCommands.RunAsync(host, command));
    }
}
