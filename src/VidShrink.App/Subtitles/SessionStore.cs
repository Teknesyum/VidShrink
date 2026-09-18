using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using VidShrink.Core;
using VidShrink.Core.Subtitles;

namespace VidShrink.App.Subtitles;

/// <summary>
/// OpenSubtitles oturumunun saklandığı yer.
/// </summary>
/// <remarks>
/// Saklanan tek şey belirteçtir; parola hiçbir zaman diske yazılmaz. Dosya
/// <c>settings.json</c>ın yanındadır ve yolu ondan türer, dolayısıyla
/// <c>VIDSHRINK_SETTINGS_PATH</c> verildiğinde testler kullanıcının gerçek dosyasına
/// dokunmaz. Gövde Windows'ta DPAPI ile (<c>CurrentUser</c>) sarılır. Windows dışında
/// eşdeğer bir kasa yok: orada belirteç <b>diske hiç yazılmaz</b>, süreç ömrü kadar yaşar.
/// Taşınabilirlik uğruna korumasız yazmak, kullanıcıya olmayan bir güvence vermek olurdu.
/// </remarks>
public sealed class SessionStore : ISubtitleSessionStore
{
    private readonly string? _file;
    private SubtitleSession? _cached;

    public SessionStore(string? settingsPath = null)
        => _file = Supported ? PathFor(settingsPath) : null;

    /// <summary>Belirtecin korumalı saklanabildiği platform mu.</summary>
    public static bool Supported => OperatingSystem.IsWindows();

    /// <summary>Oturum dosyasının adı. "Tüm verileri sıfırla" listesi de bunu kullanır.</summary>
    public const string FileName = "opensubtitles-session.dat";

    /// <summary>Ayar dosyasının yanındaki oturum dosyası.</summary>
    public static string PathFor(string? settingsPath = null)
    {
        var settings = Path.GetFullPath(settingsPath ?? UpdateSettings.DefaultPath);
        return Path.Combine(Path.GetDirectoryName(settings) ?? ".", FileName);
    }

    public SubtitleSession? Read()
    {
        if (_file is null) return _cached;

        try
        {
            if (!File.Exists(_file)) return null;
            var plain = Unprotect(Convert.FromBase64String(File.ReadAllText(_file)));
            if (plain is null) return null;

            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(plain));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("token", out var token) || token.ValueKind != JsonValueKind.String) return null;
            if (!root.TryGetProperty("expires", out var expires) || !expires.TryGetInt64(out var seconds)) return null;

            var host = root.TryGetProperty("host", out var found) && found.ValueKind == JsonValueKind.String
                ? found.GetString() ?? SubtitleSession.DefaultHost
                : SubtitleSession.DefaultHost;

            return new SubtitleSession(token.GetString() ?? "", host, DateTimeOffset.FromUnixTimeSeconds(seconds));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public void Write(SubtitleSession session)
    {
        _cached = session;
        if (_file is null) return;

        try
        {
            var plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["token"] = session.Token,
                ["host"] = session.Host,
                ["expires"] = session.Expires.ToUnixTimeSeconds()
            }));

            var sealed_ = Protect(plain);
            if (sealed_ is null) return;

            Directory.CreateDirectory(Path.GetDirectoryName(_file) ?? ".");
            File.WriteAllText(_file, Convert.ToBase64String(sealed_));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void Clear()
    {
        _cached = null;
        if (_file is null) return;

        try
        {
            if (File.Exists(_file)) File.Delete(_file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Blob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);

    private static byte[]? Protect(byte[] plain) => Wrap(plain, true);

    private static byte[]? Unprotect(byte[] sealed_) => Wrap(sealed_, false);

    private static byte[]? Wrap(byte[] data, bool protect)
    {
        if (!Supported) return null;

        var input = new Blob { Size = data.Length, Data = Marshal.AllocHGlobal(data.Length) };
        var output = default(Blob);
        try
        {
            Marshal.Copy(data, 0, input.Data, data.Length);
            var ok = protect
                ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out output);
            if (!ok || output.Data == IntPtr.Zero) return null;

            var result = new byte[output.Size];
            Marshal.Copy(output.Data, result, 0, output.Size);
            return result;
        }
        catch (Exception exception) when (exception is OutOfMemoryException or ArgumentException)
        {
            return null;
        }
        finally
        {
            if (input.Data != IntPtr.Zero)
            {
                Array.Clear(data, 0, data.Length);
                Marshal.Copy(data, 0, input.Data, data.Length);
                Marshal.FreeHGlobal(input.Data);
            }

            if (output.Data != IntPtr.Zero) LocalFree(output.Data);
        }
    }
}
