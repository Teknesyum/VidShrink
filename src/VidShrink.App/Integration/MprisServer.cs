using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Tmds.DBus.Protocol;
using VidShrink.Core;

namespace VidShrink.App.Integration;

/// <summary>
/// OYB-137: <c>/org/mpris/MediaPlayer2</c> nesnesini oturum veriyolunda yayınlar. Paket Avalonia'nın
/// Linux arka ucunun zaten getirdiği Tmds.DBus.Protocol; yeni bağımlılık yok. Eşlemenin kendisi
/// <see cref="Mpris"/>'te, komutlar <see cref="ControlBridge"/>'in yolundan geçer; yalnız oynatıcı
/// komutları açılır (kayıt ve küçültme MPRIS'te yok). Veriyolu yoksa ya da bağlantı düşerse sessizce susar.
/// </summary>
internal sealed class MprisServer : IPathMethodHandler, IDisposable
{
    private const string ErrorInvalidArgs = "org.freedesktop.DBus.Error.InvalidArgs";
    private const string ErrorUnknownProperty = "org.freedesktop.DBus.Error.UnknownProperty";
    private const string ErrorUnknownInterface = "org.freedesktop.DBus.Error.UnknownInterface";
    private const string ErrorReadOnly = "org.freedesktop.DBus.Error.PropertyReadOnly";

    private readonly Func<ControlCommand, Task<ControlReply>> _execute;
    private readonly Action? _raise;
    private readonly CancellationTokenSource _stop = new();
    private readonly object _gate = new();
    private DBusConnection? _connection;
    private ControlStatus? _status;
    private DateTime _statusAt = DateTime.UtcNow;

    internal MprisServer(Func<ControlCommand, Task<ControlReply>> execute, Action? raise = null)
    {
        _execute = execute;
        _raise = raise;
    }

    public string Path => Mpris.ObjectPath;

    public bool HandlesChildPaths => false;

    internal string? BusName { get; private set; }

    internal TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    internal static void RaiseMainWindow()
        => Dispatcher.UIThread.Post(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }) window.Activate();
        });

    /// <summary>Bağlanır, adı alır ve durum yoklamasını başlatır; başaramazsa <c>false</c>.</summary>
    internal async Task<bool> StartAsync(string? address, bool secondary)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;
        DBusConnection? connection = null;
        try
        {
            connection = new DBusConnection(address);
            await connection.ConnectAsync();
            connection.AddMethodHandler(this);
            var name = Mpris.BusName(null);
            if (secondary || !await connection.TryRequestNameAsync(name, RequestNameOptions.None))
            {
                name = Mpris.BusName(Environment.ProcessId);
                await connection.RequestNameAsync(name, RequestNameOptions.None);
            }

            BusName = name;
            _connection = connection;
            await RefreshAsync();
            _ = PollAsync(_stop.Token);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            connection?.Dispose();
            return false;
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _connection?.Dispose();
        _connection = null;
    }

    private async Task PollAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(PollInterval);
            while (await timer.WaitForNextTickAsync(token)) await RefreshAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<ControlStatus?> StatusAsync()
    {
        try
        {
            var reply = await _execute(new ControlCommand(ControlCommandNames.Status));
            return reply.Ok ? reply.Status : null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>Durumu tazeler, değişeni <c>PropertiesChanged</c> ile, atlamayı <c>Seeked</c> ile duyurur.</summary>
    internal async Task<ControlStatus?> RefreshAsync(bool seeked = false)
    {
        var now = await StatusAsync();
        ControlStatus? before;
        DateTime at;
        lock (_gate)
        {
            before = _status;
            at = _statusAt;
            _status = now;
            _statusAt = DateTime.UtcNow;
        }

        var changed = Mpris.Changed(before, now);
        if (changed.Count > 0 || !SameCapabilities(Capabilities(before), Capabilities(now)))
        {
            var values = new Dictionary<string, VariantValue>(StringComparer.Ordinal);
            foreach (var property in changed) values[property] = PlayerProperty(property, now);
            foreach (var (property, value) in Capabilities(now)) values[property] = value;
            SendPropertiesChanged(values);
        }

        if (now?.PlayerPath is not null && (seeked || Mpris.Seeked(before, now, DateTime.UtcNow - at)))
            SendSeeked(Mpris.Microseconds(now.PositionSeconds));
        return now;
    }

    private static (string, bool)[] Capabilities(ControlStatus? status)
    {
        var loaded = status?.PlayerPath is not null;
        return new[]
        {
            ("CanPlay", loaded),
            ("CanPause", loaded),
            ("CanSeek", loaded && status!.DurationSeconds is > 0)
        };
    }

    private static bool SameCapabilities((string, bool)[] left, (string, bool)[] right)
        => ((ReadOnlySpan<(string, bool)>)left).SequenceEqual(right);

    private void SendPropertiesChanged(Dictionary<string, VariantValue> values)
    {
        if (_connection is not { } connection) return;
        try
        {
            using var writer = connection.GetMessageWriter();
            writer.WriteSignalHeader(null, Mpris.ObjectPath, Mpris.PropertiesInterface, "PropertiesChanged", "sa{sv}as");
            writer.WriteString(Mpris.PlayerInterface);
            writer.WriteDictionary(values);
            writer.WriteArray(Array.Empty<string>());
            connection.TrySendMessage(writer.CreateMessage());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
        }
    }

    private void SendSeeked(long microseconds)
    {
        if (_connection is not { } connection) return;
        try
        {
            using var writer = connection.GetMessageWriter();
            writer.WriteSignalHeader(null, Mpris.ObjectPath, Mpris.PlayerInterface, "Seeked", "x");
            writer.WriteInt64(microseconds);
            connection.TrySendMessage(writer.CreateMessage());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
        }
    }

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        if (context.IsDBusIntrospectRequest)
        {
            context.ReplyIntrospectXml(new ReadOnlyMemory<byte>[] { RootXml, PlayerXml }, (IList<string>)Array.Empty<string>());
            return ValueTask.CompletedTask;
        }

        var request = context.Request;
        var face = request.InterfaceAsString;
        var member = request.MemberAsString ?? "";
        if (face == Mpris.PropertiesInterface) return HandleProperties(context, member);
        if (face == Mpris.RootInterface) return HandleRoot(context, member);
        if (face == Mpris.PlayerInterface && Mpris.IsPlayerMethod(member)) return HandlePlayer(context, member);
        context.ReplyUnknownMethodError();
        return ValueTask.CompletedTask;
    }

    private ValueTask HandleRoot(MethodContext context, string member)
    {
        switch (member)
        {
            case "Raise":
                _raise?.Invoke();
                ReplyEmpty(context);
                break;
            case "Quit":
                ReplyEmpty(context);
                break;
            default:
                context.ReplyUnknownMethodError();
                break;
        }

        return ValueTask.CompletedTask;
    }

    private ValueTask HandlePlayer(MethodContext context, string member)
    {
        long microseconds = 0;
        string? argument = null;
        var reader = context.Request.GetBodyReader();
        try
        {
            switch (member)
            {
                case Mpris.MethodSeek:
                    microseconds = reader.ReadInt64();
                    break;
                case Mpris.MethodSetPosition:
                    argument = reader.ReadObjectPathAsString();
                    microseconds = reader.ReadInt64();
                    break;
                case Mpris.MethodOpenUri:
                    argument = reader.ReadString();
                    break;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            context.ReplyError(ErrorInvalidArgs, member);
            return ValueTask.CompletedTask;
        }

        return RunPlayerAsync(context, member, microseconds, argument);
    }

    private async ValueTask RunPlayerAsync(MethodContext context, string member, long microseconds, string? argument)
    {
        var status = await StatusAsync();
        var command = Mpris.Command(member, status, microseconds, argument);
        ControlReply? reply = null;
        if (command is not null)
        {
            try { reply = await _execute(command); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { reply = ControlReply.Failure(ControlErrors.Failed); }
        }

        if (member == Mpris.MethodOpenUri && reply is not { Ok: true })
        {
            context.ReplyError(ErrorInvalidArgs, reply?.Error ?? Mpris.MethodOpenUri);
            return;
        }

        ReplyEmpty(context);
        if (command is not null) await RefreshAsync(seeked: command.Name == ControlCommandNames.Seek);
    }

    private ValueTask HandleProperties(MethodContext context, string member)
    {
        string face, property = "";
        var reader = context.Request.GetBodyReader();
        try
        {
            face = reader.ReadString();
            if (member is "Get" or "Set") property = reader.ReadString();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            context.ReplyError(ErrorInvalidArgs, member);
            return ValueTask.CompletedTask;
        }

        switch (member)
        {
            case "Get":
                return GetAsync(context, face, property);
            case "GetAll":
                return GetAllAsync(context, face);
            case "Set":
                if (face == Mpris.PlayerInterface && property is "Volume" or "Rate") ReplyEmpty(context);
                else if (face is Mpris.PlayerInterface or Mpris.RootInterface) context.ReplyError(ErrorReadOnly, property);
                else context.ReplyError(ErrorUnknownInterface, face);
                return ValueTask.CompletedTask;
            default:
                context.ReplyUnknownMethodError();
                return ValueTask.CompletedTask;
        }
    }

    private async ValueTask GetAsync(MethodContext context, string face, string property)
    {
        var all = await PropertiesAsync(face);
        if (all is null)
        {
            context.ReplyError(ErrorUnknownInterface, face);
            return;
        }

        if (!all.TryGetValue(property, out var value))
        {
            context.ReplyError(ErrorUnknownProperty, property);
            return;
        }

        ReplyVariant(context, value);
    }

    private static void ReplyVariant(MethodContext context, VariantValue value)
    {
        using var writer = context.CreateReplyWriter("v");
        writer.WriteVariant(value);
        context.Reply(writer.CreateMessage());
    }

    private async ValueTask GetAllAsync(MethodContext context, string face)
    {
        var all = await PropertiesAsync(face);
        if (all is null)
        {
            context.ReplyError(ErrorUnknownInterface, face);
            return;
        }

        ReplyDictionary(context, all);
    }

    private static void ReplyDictionary(MethodContext context, Dictionary<string, VariantValue> values)
    {
        using var writer = context.CreateReplyWriter("a{sv}");
        writer.WriteDictionary(values);
        context.Reply(writer.CreateMessage());
    }

    private async Task<Dictionary<string, VariantValue>?> PropertiesAsync(string face)
    {
        if (face == Mpris.RootInterface) return RootProperties();
        if (face != Mpris.PlayerInterface) return null;
        var status = await StatusAsync();
        var values = new Dictionary<string, VariantValue>(StringComparer.Ordinal);
        foreach (var name in PlayerPropertyNames) values[name] = PlayerProperty(name, status);
        return values;
    }

    private static Dictionary<string, VariantValue> RootProperties() => new(StringComparer.Ordinal)
    {
        ["CanQuit"] = false,
        ["CanRaise"] = true,
        ["HasTrackList"] = false,
        ["Identity"] = Mpris.Identity,
        ["DesktopEntry"] = Mpris.DesktopEntry,
        ["SupportedUriSchemes"] = VariantValue.Array(ToArray(Mpris.UriSchemes)),
        ["SupportedMimeTypes"] = VariantValue.Array(ToArray(Mpris.MimeTypes))
    };

    private static string[] ToArray(IReadOnlyList<string> list)
    {
        var array = new string[list.Count];
        for (var index = 0; index < array.Length; index++) array[index] = list[index];
        return array;
    }

    private static readonly string[] PlayerPropertyNames =
    {
        Mpris.PropertyPlaybackStatus, "Rate", Mpris.PropertyMetadata, "Volume", Mpris.PropertyPosition,
        "MinimumRate", "MaximumRate", "CanGoNext", "CanGoPrevious", "CanPlay", "CanPause", "CanSeek", "CanControl"
    };

    private static VariantValue PlayerProperty(string name, ControlStatus? status)
    {
        var loaded = status?.PlayerPath is not null;
        return name switch
        {
            Mpris.PropertyPlaybackStatus => Mpris.PlaybackStatus(status),
            Mpris.PropertyMetadata => MetadataValue(Mpris.Metadata(status)),
            Mpris.PropertyPosition => Mpris.Microseconds(status?.PositionSeconds),
            "Rate" or "Volume" or "MinimumRate" or "MaximumRate" => 1.0,
            "CanGoNext" or "CanGoPrevious" => false,
            "CanPlay" or "CanPause" => loaded,
            "CanSeek" => loaded && status!.DurationSeconds is > 0,
            "CanControl" => true,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
    }

    private static VariantValue MetadataValue(MprisMetadata metadata)
    {
        var dict = new Dict<string, VariantValue> { ["mpris:trackid"] = new ObjectPath(metadata.TrackId) };
        if (metadata.LengthMicroseconds > 0) dict["mpris:length"] = metadata.LengthMicroseconds;
        if (metadata.Title is { } title) dict["xesam:title"] = title;
        if (metadata.Url is { } url) dict["xesam:url"] = url;
        return dict;
    }

    private static void ReplyEmpty(MethodContext context)
    {
        if (context.NoReplyExpected) return;
        using var writer = context.CreateReplyWriter(null!);
        context.Reply(writer.CreateMessage());
    }

    private static readonly ReadOnlyMemory<byte> RootXml = Encoding.UTF8.GetBytes(
        """
        <interface name="org.mpris.MediaPlayer2">
          <method name="Raise"/>
          <method name="Quit"/>
          <property name="CanQuit" type="b" access="read"/>
          <property name="CanRaise" type="b" access="read"/>
          <property name="HasTrackList" type="b" access="read"/>
          <property name="Identity" type="s" access="read"/>
          <property name="DesktopEntry" type="s" access="read"/>
          <property name="SupportedUriSchemes" type="as" access="read"/>
          <property name="SupportedMimeTypes" type="as" access="read"/>
        </interface>

        """);

    private static readonly ReadOnlyMemory<byte> PlayerXml = Encoding.UTF8.GetBytes(
        """
        <interface name="org.mpris.MediaPlayer2.Player">
          <method name="Next"/>
          <method name="Previous"/>
          <method name="Pause"/>
          <method name="PlayPause"/>
          <method name="Stop"/>
          <method name="Play"/>
          <method name="Seek"><arg name="Offset" type="x" direction="in"/></method>
          <method name="SetPosition"><arg name="TrackId" type="o" direction="in"/><arg name="Position" type="x" direction="in"/></method>
          <method name="OpenUri"><arg name="Uri" type="s" direction="in"/></method>
          <signal name="Seeked"><arg name="Position" type="x"/></signal>
          <property name="PlaybackStatus" type="s" access="read"/>
          <property name="Rate" type="d" access="readwrite"/>
          <property name="Metadata" type="a{sv}" access="read"/>
          <property name="Volume" type="d" access="readwrite"/>
          <property name="Position" type="x" access="read"/>
          <property name="MinimumRate" type="d" access="read"/>
          <property name="MaximumRate" type="d" access="read"/>
          <property name="CanGoNext" type="b" access="read"/>
          <property name="CanGoPrevious" type="b" access="read"/>
          <property name="CanPlay" type="b" access="read"/>
          <property name="CanPause" type="b" access="read"/>
          <property name="CanSeek" type="b" access="read"/>
          <property name="CanControl" type="b" access="read"/>
        </interface>

        """);
}
