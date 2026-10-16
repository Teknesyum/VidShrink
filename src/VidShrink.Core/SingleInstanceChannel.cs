using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("VidShrink.Tests")]

namespace VidShrink.Core;

public sealed class SingleInstanceChannel : IDisposable
{
    public const string Ack = "TAMAM";
    public const string Nack = "HATA";
    public const string ChannelVariable = "VIDSHRINK_INSTANCE_CHANNEL";

    private readonly Mutex? _ownerLock;
    private CancellationTokenSource? _cancel;
    private Task? _listenTask;
    private int _disposed;

    public SingleInstanceChannel(string channelName)
    {
        PipeName = PipeNameFor(channelName);
        var mutex = new Mutex(initiallyOwned: true, name: $"VidShrinkTekOrnekKilidi-{channelName}", out var createdNew);
        IsOwner = createdNew;
        if (IsOwner) _ownerLock = mutex;
        else mutex.Dispose();
    }

    public bool IsOwner { get; }

    public string PipeName { get; }

    public static string PipeNameFor(string channelName) => $"VidShrinkTekOrnek-{channelName}";

    public static string DefaultChannel()
    {
        var overridden = Environment.GetEnvironmentVariable(ChannelVariable);
        if (!string.IsNullOrWhiteSpace(overridden)) return Sanitize(overridden.Trim());
        return "ana-" + Sanitize(Environment.UserName);
    }

    internal static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
            builder.Append(char.IsAsciiLetterOrDigit(character) || character == '-' ? character : '_');
        return builder.Length == 0 ? "_" : builder.ToString();
    }

    /// <summary>
    /// Yol listesi iletisini <paramref name="handler"/>'a, <c>{</c> ile başlayan komut satırını
    /// <paramref name="commands"/>'a verir. <paramref name="commands"/> yoksa komut
    /// <see cref="ControlErrors.Disabled"/> ile reddedilir.
    /// </summary>
    public void StartListening(Func<IReadOnlyList<string>, bool> handler, ControlDispatcher? commands = null)
    {
        if (!IsOwner) throw new InvalidOperationException("Bu örnek kanalın sahibi değil.");
        if (_cancel is not null) throw new InvalidOperationException("Kanal zaten dinleniyor.");

        _cancel = new CancellationTokenSource();
        var token = _cancel.Token;
        _listenTask = Task.Run(() => Listen(handler, commands, token));
    }

    public bool Forward(IReadOnlyList<string> paths, TimeSpan connectTimeout, TimeSpan replyTimeout)
    {
        if (IsOwner) return false;

        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            client.Connect((int)connectTimeout.TotalMilliseconds);
            using var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, Encoding.UTF8, false, 1024, leaveOpen: true);
            writer.WriteLine(Encode(paths));
            var reply = reader.ReadLineAsync();
            if (!reply.Wait(replyTimeout)) return false;
            return string.Equals(reply.Result, Ack, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or ObjectDisposedException
                                       or UnauthorizedAccessException or AggregateException)
        {
            return false;
        }
    }

    internal static string Encode(IReadOnlyList<string> paths) => JsonSerializer.Serialize(paths, YolJson.Default.IReadOnlyListString);

    internal static IReadOnlyList<string>? Decode(string? line)
    {
        if (string.IsNullOrEmpty(line)) return null;
        try { return JsonSerializer.Deserialize(line, YolJson.Default.StringArray); }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// Bir yol listesi islenirken bir sonraki sunucu ornegi zaten kurulu durur. Unix'te boru
    /// ayni adli orneklerin paylastigi bir Unix soketi: tek ornek kapaninca dinleyici de
    /// kapaniyor ve o arada baglanmis ikinci gonderici sifirlanip kendi penceresini aciyordu
    /// (dosya yoneticisinden birden cok dosya acilinca). <see cref="ShrinkRequestQueue"/> ile ayni kalip.
    /// </summary>
    private void Listen(Func<IReadOnlyList<string>, bool> handler, ControlDispatcher? commands, CancellationToken token)
    {
        NamedPipeServerStream? next = null;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var server = next ?? TryOpen();
                next = null;
                if (server is null)
                {
                    if (token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(200))) return;
                    continue;
                }

                using (server)
                {
                    try
                    {
                        server.WaitForConnectionAsync(token).GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (IOException)
                    {
                        continue;
                    }

                    next = TryOpen();

                    try
                    {
                        using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
                        using var writer = new StreamWriter(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                        var line = reader.ReadLine();
                        if (ControlProtocol.IsCommandLine(line))
                            writer.WriteLine(commands is null ? ControlDispatcher.DisabledReply : commands.Handle(line!));
                        else
                        {
                            var paths = Decode(line);
                            writer.WriteLine(paths is not null && Deliver(handler, paths) ? Ack : Nack);
                        }
                        if (OperatingSystem.IsWindows()) server.WaitForPipeDrain();
                    }
                    catch (Exception ex) when (ex is IOException or ObjectDisposedException)
                    {
                    }
                }
            }
        }
        finally
        {
            next?.Dispose();
        }
    }

    private NamedPipeServerStream? TryOpen()
    {
        try
        {
            return new NamedPipeServerStream(PipeName, PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static bool Deliver(Func<IReadOnlyList<string>, bool> handler, IReadOnlyList<string> paths)
    {
        try { return handler(paths); }
        catch (Exception) { return false; }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        _cancel?.Cancel();
        try { _listenTask?.Wait(TimeSpan.FromSeconds(5)); }
        catch (AggregateException) { }

        try { _ownerLock?.ReleaseMutex(); }
        catch (Exception ex) when (ex is ApplicationException or ObjectDisposedException) { }
        _ownerLock?.Dispose();
        _cancel?.Dispose();
    }
}
