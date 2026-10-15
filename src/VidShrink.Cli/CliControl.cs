using System.Globalization;
using VidShrink.Core;

namespace VidShrink.Cli;

/// <summary>
/// <c>denetim</c> komutu: açık pencerenin denetim kanalının istemcisi (<c>docs/control-channel.md</c>).
/// Kaydı pencere başlatır ve durdurur; komut satırı yalnız komutu iletir ve yanıtı yazar. Pencere
/// <c>--denetim-kanali</c> ile açılmadıysa yanıt <c>disabled</c> olur.
/// </summary>
public static class CliControl
{
    public static readonly IReadOnlyList<string> Heads = new[] { "denetim", "control" };

    public static readonly IReadOnlyList<(string Turkish, string English, string Command)> Actions = new[]
    {
        ("kayit-baslat", "record-start", ControlCommandNames.RecordStart),
        ("kayit-durdur", "record-stop", ControlCommandNames.RecordStop),
        ("durum", "status", ControlCommandNames.Status)
    };

    public static bool Matches(IReadOnlyList<string> args) => args.Count > 0 && Heads.Contains(args[0], StringComparer.Ordinal);

    /// <summary>Kanalın varsayılan istemcisi: bu kullanıcının tek örnek borusu.</summary>
    public static ControlReply DefaultSend(ControlCommand command)
        => ControlClient.Send(SingleInstanceChannel.DefaultChannel(), command);

    public static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr, CliText text, Func<ControlCommand, ControlReply> send)
    {
        string? action = null;
        var json = false;
        for (var i = 1; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg is "-h" or "--yardim" or "--help")
            {
                stdout.WriteLine(text.Format("help", CliText.Version));
                return ExitCodes.InBand;
            }
            if (arg == "--json")
            {
                json = true;
                continue;
            }
            var found = Actions.FirstOrDefault(a => a.Turkish == arg || a.English == arg);
            if (found.Command is null || action is not null)
            {
                stderr.WriteLine(text.Format(arg.StartsWith('-') ? "error.unknown-option" : "error.control-action", arg));
                stderr.WriteLine(text["usage.hint"]);
                return ExitCodes.Usage;
            }
            action = found.Command;
        }

        if (action is null)
        {
            stderr.WriteLine(text.Format("error.control-action", "-"));
            stderr.WriteLine(text["usage.hint"]);
            return ExitCodes.Usage;
        }

        var reply = send(new ControlCommand(action));
        if (json)
        {
            stdout.WriteLine(ControlProtocol.SerializeReply(reply));
            return reply.Ok ? ExitCodes.InBand : ExitCodes.Error;
        }

        if (!reply.Ok)
        {
            stderr.WriteLine(text.Format(ErrorKey(reply.Error), reply.Detail ?? reply.Error ?? "-"));
            return ExitCodes.Error;
        }

        if (reply.Status is { } status) WriteStatus(stdout, text, status);
        return ExitCodes.InBand;
    }

    internal static string ErrorKey(string? error) => error switch
    {
        ControlErrors.Disabled => "control.disabled",
        ControlErrors.Unreachable => "control.unreachable",
        ControlErrors.Unsupported => "control.unsupported",
        ControlErrors.Busy => "control.busy",
        _ => "control.failed"
    };

    private static void WriteStatus(TextWriter stdout, CliText text, ControlStatus status)
    {
        stdout.WriteLine(text.Format("control.recorder", status.Recorder));
        if (status.LastRecordingPath is { } last) stdout.WriteLine(text.Format("control.last-recording", last));
        if (status.PlayerPath is { } media)
            stdout.WriteLine(text.Format("control.player", media, status.PositionSeconds.ToString("0.0", CultureInfo.InvariantCulture),
                status.Playing ? "playing" : "paused"));
        if (status.Encoding) stdout.WriteLine(text["control.encoding"]);
    }
}
