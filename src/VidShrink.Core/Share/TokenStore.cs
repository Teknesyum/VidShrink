using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VidShrink.Core.Share;

/// <summary>
/// Bir Drive oturumunun jetonları. <see cref="RefreshToken"/> uzun ömürlüdür ve asıl sır odur;
/// erişim jetonu bir saat içinde ölür.
/// </summary>
public sealed record DriveTokens(
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("access_expires_at")] DateTimeOffset AccessExpiresAt)
{
    /// <summary>Erişim jetonu bu andan sonra yenilenmeli. 60 saniyelik pay bırakılır.</summary>
    public bool AccessUsableAt(DateTimeOffset now) =>
        !string.IsNullOrEmpty(AccessToken) && AccessExpiresAt - now > TimeSpan.FromSeconds(60);
}

/// <summary>
/// Jeton saklama yeri. <see cref="Persists"/> yanlışsa jeton diske hiç yazılmaz ve kullanıcıdan
/// her oturumda yeniden izin istenir.
/// </summary>
public interface ITokenStore
{
    /// <summary>Jeton uygulama kapandıktan sonra da duruyor mu.</summary>
    bool Persists { get; }

    /// <summary>Bu saklama yolunun insana anlatılabilir adı. Rapor ve arayüz metni için.</summary>
    string Description { get; }

    DriveTokens? Load();

    void Save(DriveTokens tokens);

    void Clear();
}

/// <summary>
/// Çalışılan platforma uygun jeton saklama yolunu seçer.
/// </summary>
/// <remarks>
/// Windows'ta işletim sisteminin kendi koruma yolu (DPAPI, kullanıcı kapsamı) kullanılır: dosya
/// şifreli durur ve yalnız aynı Windows kullanıcısı çözebilir. macOS ve Linux'ta bu katmanın
/// karşılığı çekirdek kütüphanede yok (Keychain ve Secret Service dışarıdan bağımlılık ister),
/// bu yüzden jeton <b>hiç saklanmaz</b>: her oturumda tarayıcı akışı yeniden açılır. Kötü
/// saklamaktansa saklamamak yeğdir.
/// </remarks>
public static class TokenStore
{
    public static ITokenStore ForCurrentPlatform(string? path = null) =>
        OperatingSystem.IsWindows()
            ? new DpapiTokenStore(path ?? DefaultPath)
            : new EphemeralTokenStore();

    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VidShrink",
            "drive-jeton.bin");
}

/// <summary>
/// Jetonu bellekte tutar, diske yazmaz. Windows dışı platformların yolu budur; testlerde de
/// kullanılır.
/// </summary>
public sealed class EphemeralTokenStore : ITokenStore
{
    private DriveTokens? _tokens;

    public bool Persists => false;

    public string Description => "saklanmıyor — her oturumda yeniden izin istenir";

    public DriveTokens? Load() => _tokens;

    public void Save(DriveTokens tokens) => _tokens = tokens;

    public void Clear() => _tokens = null;
}

/// <summary>
/// Jetonu Windows DPAPI ile kullanıcı kapsamında şifreleyip dosyaya yazar. Dosyada düz metin
/// bulunmaz; başka bir Windows kullanıcısı ya da başka bir makine çözemez.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiTokenStore : ITokenStore
{
    /// <summary>Şifrelemeye karıştırılan sabit ek. Sır değil; başka uygulamanın blobunu ayırır.</summary>
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("VidShrink.Drive.v1");

    private readonly string _path;

    public DpapiTokenStore(string path) => _path = path;

    public bool Persists => true;

    public string Description => "Windows DPAPI (kullanıcı kapsamı), şifreli dosya";

    public string FilePath => _path;

    public DriveTokens? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var plain = Unprotect(File.ReadAllBytes(_path));
            return JsonSerializer.Deserialize<DriveTokens>(Encoding.UTF8.GetString(plain));
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    public void Save(DriveTokens tokens)
    {
        var folder = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        var plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tokens));
        File.WriteAllBytes(_path, Protect(plain));
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
        catch (IOException)
        {
            // Silinemediyse jeton yine de sunucu tarafında iptal edilir; burada susmak yeterli.
        }
    }

    internal static byte[] Protect(byte[] plain) => Transform(plain, protect: true);

    internal static byte[] Unprotect(byte[] cipher) => Transform(cipher, protect: false);

    private static byte[] Transform(byte[] input, bool protect)
    {
        var inBlob = default(DataBlob);
        var entropyBlob = default(DataBlob);
        var outBlob = default(DataBlob);
        try
        {
            inBlob = Allocate(input);
            entropyBlob = Allocate(Entropy);
            var ok = protect
                ? CryptProtectData(ref inBlob, null, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, 0, out outBlob)
                : CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, 0, out outBlob);
            if (!ok)
            {
                throw new InvalidOperationException(
                    $"DPAPI çağrısı başarısız (hata {Marshal.GetLastWin32Error()}).");
            }
            var result = new byte[outBlob.cbData];
            Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
            return result;
        }
        finally
        {
            Release(ref inBlob);
            Release(ref entropyBlob);
            if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
        }
    }

    private static DataBlob Allocate(byte[] data)
    {
        var blob = new DataBlob { cbData = data.Length, pbData = Marshal.AllocHGlobal(Math.Max(data.Length, 1)) };
        Marshal.Copy(data, 0, blob.pbData, data.Length);
        return blob;
    }

    private static void Release(ref DataBlob blob)
    {
        if (blob.pbData == IntPtr.Zero) return;
        // Düz metin bellekte kalmasın: serbest bırakmadan önce üstüne sıfır yazılır.
        for (var i = 0; i < blob.cbData; i++) Marshal.WriteByte(blob.pbData, i, 0);
        Marshal.FreeHGlobal(blob.pbData);
        blob.pbData = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DataBlob
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn,
        string? szDataDescr,
        ref DataBlob pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn,
        IntPtr ppszDataDescr,
        ref DataBlob pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DataBlob pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
