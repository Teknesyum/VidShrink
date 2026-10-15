using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VidShrink.Core;

/// <summary>Denetim kanalının komut adları; boru üstünde bu yazımla gider (<c>docs/control-channel.md</c>).</summary>
public static class ControlCommandNames
{
    public const string Play = "play";
    public const string Pause = "pause";
    public const string Seek = "seek";
    public const string Open = "open";
    public const string RecordStart = "record-start";
    public const string RecordStop = "record-stop";
    public const string EnqueueShrink = "enqueue-shrink";
    public const string Status = "status";

    public static IReadOnlyList<string> All { get; } = new[] { Play, Pause, Seek, Open, RecordStart, RecordStop, EnqueueShrink, Status };
}

/// <summary>Kanalı açan başlatma bayrağı. Bayrak yoksa kanal kapalıdır ve her komut reddedilir.</summary>
public static class ControlSwitch
{
    public const string Flag = "--denetim-kanali";
    public const string EnglishFlag = "--control-channel";

    public static bool Requested(IReadOnlyList<string>? args)
        => args is not null && args.Any(a => string.Equals(a, Flag, StringComparison.Ordinal) || string.Equals(a, EnglishFlag, StringComparison.Ordinal));
}

/// <summary>Yanıttaki hata kodları; makine içindir, çevrilmez.</summary>
public static class ControlErrors
{
    public const string Disabled = "disabled";
    public const string Malformed = "malformed";
    public const string UnknownCommand = "unknown-command";
    public const string MissingArgument = "missing-argument";
    public const string BadArgument = "bad-argument";
    public const string NotReady = "not-ready";
    public const string Busy = "busy";
    public const string Failed = "failed";
    public const string Timeout = "timeout";
    public const string Unreachable = "unreachable";
    public const string Unsupported = "unsupported";
}

/// <summary>Tek satırlık komut: ad, isteğe bağlı kimlik ve komutun kendi alanları.</summary>
public sealed record ControlCommand(string Name)
{
    public string? Id { get; init; }
    public string? Path { get; init; }
    public double? Seconds { get; init; }
    public int? TargetMegabytes { get; init; }
}

/// <summary>Uygulamanın durum fotoğrafı: kaydedici, oynatıcı ve kodlama.</summary>
public sealed record ControlStatus(
    string Recorder,
    string? LastRecordingPath,
    string? PlayerPath,
    bool Playing,
    double PositionSeconds,
    bool Encoding);

/// <summary>Tek satırlık yanıt. <see cref="Ok"/> yanlışsa <see cref="Error"/> doludur.</summary>
public sealed record ControlReply(bool Ok, string? Error = null, string? Detail = null, ControlStatus? Status = null)
{
    public string? Id { get; init; }

    public static ControlReply Success(ControlStatus? status = null) => new(true, Status: status);

    public static ControlReply Failure(string error, string? detail = null) => new(false, error, detail);
}

/// <summary>
/// Satır başına bir JSON nesnesi. Eski tek örnek iletisi bir JSON dizisidir (yol listesi), komut ise
/// <c>{</c> ile başlar; aynı boru ikisini ilk karakterden ayırır.
/// </summary>
public static class ControlProtocol
{
    public const string RecorderIdle = "idle";
    public const string RecorderCountdown = "countdown";
    public const string RecorderRunning = "running";
    public const string RecorderPaused = "paused";

    public static bool IsCommandLine(string? line)
    {
        if (string.IsNullOrEmpty(line)) return false;
        foreach (var character in line)
        {
            if (char.IsWhiteSpace(character)) continue;
            return character == '{';
        }
        return false;
    }

    public static string Serialize(ControlCommand command)
    {
        var root = new JsonObject { ["command"] = command.Name };
        if (command.Id is not null) root["id"] = command.Id;
        if (command.Path is not null) root["path"] = command.Path;
        if (command.Seconds is { } seconds) root["seconds"] = seconds;
        if (command.TargetMegabytes is { } target) root["targetMb"] = target;
        return root.ToJsonString(Options);
    }

    /// <summary>Komutu çözer; çözülemezse <paramref name="error"/> doludur ve <c>null</c> döner.</summary>
    public static ControlCommand? Parse(string? line, out string? error)
    {
        error = ControlErrors.Malformed;
        if (!IsCommandLine(line)) return null;

        JsonObject? root;
        try { root = JsonNode.Parse(line!) as JsonObject; }
        catch (JsonException) { return null; }
        if (root is null) return null;

        try
        {
            var name = Text(root["command"]);
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (!ControlCommandNames.All.Contains(name, StringComparer.Ordinal))
            {
                error = ControlErrors.UnknownCommand;
                return null;
            }

            var command = new ControlCommand(name)
            {
                Id = Text(root["id"]),
                Path = Text(root["path"]),
                Seconds = Number(root["seconds"]),
                TargetMegabytes = Number(root["targetMb"]) is { } mb && mb == Math.Floor(mb) && mb is > 0 and <= int.MaxValue ? (int)mb : null
            };

            if (Required(command) is { } missing)
            {
                error = missing;
                return null;
            }

            error = null;
            return command;
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static string? Required(ControlCommand command) => command.Name switch
    {
        ControlCommandNames.Seek when command.Seconds is null => ControlErrors.MissingArgument,
        ControlCommandNames.Seek when command.Seconds is { } s && (double.IsNaN(s) || double.IsInfinity(s) || s < 0) => ControlErrors.BadArgument,
        ControlCommandNames.Open when string.IsNullOrWhiteSpace(command.Path) => ControlErrors.MissingArgument,
        ControlCommandNames.EnqueueShrink when string.IsNullOrWhiteSpace(command.Path) || command.TargetMegabytes is null => ControlErrors.MissingArgument,
        _ => null
    };

    private static string? Text(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static double? Number(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<double>(out var number)) return number;
        if (value.TryGetValue<string>(out var text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return null;
    }

    public static string SerializeReply(ControlReply reply)
    {
        var root = new JsonObject { ["ok"] = reply.Ok };
        if (reply.Id is not null) root["id"] = reply.Id;
        if (reply.Error is not null) root["error"] = reply.Error;
        if (reply.Detail is not null) root["detail"] = reply.Detail;
        if (reply.Status is { } status)
            root["status"] = new JsonObject
            {
                ["recorder"] = status.Recorder,
                ["lastRecordingPath"] = status.LastRecordingPath,
                ["playerPath"] = status.PlayerPath,
                ["playing"] = status.Playing,
                ["positionSeconds"] = Math.Round(status.PositionSeconds, 3),
                ["encoding"] = status.Encoding
            };
        return root.ToJsonString(Options);
    }

    /// <summary>Yanıtı çözer; eski sürümün <c>HATA</c> satırı <see cref="ControlErrors.Unsupported"/> olur.</summary>
    public static ControlReply ParseReply(string? line)
    {
        if (line is null) return ControlReply.Failure(ControlErrors.Unreachable);
        if (string.Equals(line, SingleInstanceChannel.Nack, StringComparison.Ordinal)) return ControlReply.Failure(ControlErrors.Unsupported);
        try
        {
            if (JsonNode.Parse(line) is not JsonObject root || root["ok"] is not JsonValue okValue || !okValue.TryGetValue<bool>(out var ok))
                return ControlReply.Failure(ControlErrors.Malformed);

            ControlStatus? status = null;
            if (root["status"] is JsonObject s)
                status = new ControlStatus(
                    Text(s["recorder"]) ?? RecorderIdle,
                    Text(s["lastRecordingPath"]),
                    Text(s["playerPath"]),
                    s["playing"] is JsonValue p && p.TryGetValue<bool>(out var playing) && playing,
                    Number(s["positionSeconds"]) ?? 0,
                    s["encoding"] is JsonValue e && e.TryGetValue<bool>(out var encoding) && encoding);

            return new ControlReply(ok, Text(root["error"]), Text(root["detail"]), status) { Id = Text(root["id"]) };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return ControlReply.Failure(ControlErrors.Malformed);
        }
    }

    private static readonly JsonSerializerOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
}

/// <summary>
/// Komut satırını yanıt satırına çevirir. Kanal kapalıyken (<see cref="Enabled"/> yanlış) hiçbir komut
/// çözülmez ya da yürütülmez; yanıt <see cref="ControlErrors.Disabled"/> olur. Yürütme
/// <see cref="Timeout"/> içinde bitmezse yanıt <see cref="ControlErrors.Timeout"/> olur.
/// </summary>
public sealed class ControlDispatcher
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly Func<bool> _enabled;
    private readonly Func<ControlCommand, Task<ControlReply>> _execute;

    public ControlDispatcher(Func<bool> enabled, Func<ControlCommand, Task<ControlReply>> execute, TimeSpan? timeout = null)
    {
        _enabled = enabled;
        _execute = execute;
        Timeout = timeout ?? DefaultTimeout;
    }

    public TimeSpan Timeout { get; }

    public bool Enabled => _enabled();

    public static string DisabledReply { get; } = ControlProtocol.SerializeReply(ControlReply.Failure(ControlErrors.Disabled));

    public string Handle(string line)
    {
        if (!_enabled()) return DisabledReply;

        var command = ControlProtocol.Parse(line, out var error);
        if (command is null) return ControlProtocol.SerializeReply(ControlReply.Failure(error ?? ControlErrors.Malformed));

        ControlReply reply;
        try
        {
            var task = _execute(command);
            reply = task.Wait(Timeout) ? task.Result : ControlReply.Failure(ControlErrors.Timeout);
        }
        catch (AggregateException ex)
        {
            reply = ControlReply.Failure(ControlErrors.Failed, ex.InnerException?.Message);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException or UnauthorizedAccessException)
        {
            reply = ControlReply.Failure(ControlErrors.Failed, ex.Message);
        }

        return ControlProtocol.SerializeReply(reply with { Id = command.Id });
    }
}

/// <summary>Kanalın istemcisi: komutu sahibin borusuna yazar, tek satır yanıt okur. Ağ yok; boru yalnız aynı kullanıcıya açık.</summary>
public static class ControlClient
{
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(20);

    public static ControlReply Send(string channelName, ControlCommand command, TimeSpan? connectTimeout = null, TimeSpan? replyTimeout = null)
    {
        var line = Exchange(SingleInstanceChannel.PipeNameFor(channelName), ControlProtocol.Serialize(command),
            connectTimeout ?? ConnectTimeout, replyTimeout ?? ReplyTimeout, out var timedOut);
        if (timedOut) return ControlReply.Failure(ControlErrors.Timeout);
        return ControlProtocol.ParseReply(line);
    }

    internal static string? Exchange(string pipeName, string line, TimeSpan connectTimeout, TimeSpan replyTimeout, out bool timedOut)
    {
        timedOut = false;
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            client.Connect((int)connectTimeout.TotalMilliseconds);
            using var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, Encoding.UTF8, false, 1024, leaveOpen: true);
            writer.WriteLine(line);
            var reply = reader.ReadLineAsync();
            if (!reply.Wait(replyTimeout))
            {
                timedOut = true;
                return null;
            }
            return reply.Result;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or ObjectDisposedException
                                       or UnauthorizedAccessException or AggregateException)
        {
            return null;
        }
    }
}
