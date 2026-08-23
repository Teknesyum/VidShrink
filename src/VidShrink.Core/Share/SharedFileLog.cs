using System.Text.Json;
using System.Text.Json.Serialization;

namespace VidShrink.Core.Share;

/// <summary>
/// Uygulamanın Drive'a yüklediği bir dosya. Yayın kapatılınca kayıt silinmez, yalnız
/// <see cref="Shared"/> alanı düşer — dosya Drive'da durmaya ve yer kaplamaya devam eder.
/// </summary>
public sealed record UploadedFile(
    string FileId,
    string FileName,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    bool Shared,
    string? PermissionId = null,
    string? WebViewLink = null)
{
    /// <summary>Yüklemenin üstünden geçen süre.</summary>
    public TimeSpan Age(DateTimeOffset now) => now - UploadedAt;
}

/// <summary>Tek bir silme denemesinin sonucu.</summary>
/// <remarks>
/// Dosya zaten yoksa bu hata değildir: amaç gerçekleşmiştir. <see cref="AlreadyGone"/> bunu
/// ayırır ki arayüz "silindi" yerine "zaten yoktu" diyebilsin.
/// </remarks>
public sealed record DeleteOutcome(
    string FileId,
    bool Ok,
    ShareFailure Failure,
    string Detail,
    bool AlreadyGone)
{
    public static DeleteOutcome Deleted(string fileId) =>
        new(fileId, true, ShareFailure.None, string.Empty, false);

    /// <summary>Dosya Drive'da bulunamadı — elle silinmiş olabilir. Başarı sayılır.</summary>
    public static DeleteOutcome Missing(string fileId) =>
        new(fileId, true, ShareFailure.None, "Dosya Drive'da zaten yok.", true);

    public static DeleteOutcome Failed(string fileId, ShareFailure failure, string detail) =>
        new(fileId, false, failure, detail, false);
}

/// <summary>
/// Uygulamanın yüklediği dosyaların kaydı: kimlik, ad, boyut, yükleme zamanı, paylaşım durumu.
/// </summary>
/// <remarks>
/// Bu kayıt <b>tek doğruluk kaynağı değildir</b>; doğruluk kaynağı Drive'ın kendisidir. Kayıt
/// bozulur veya silinirse <c>drive.file</c> kapsamıyla listelenip yeniden kurulur
/// (<see cref="DriveMaintenance.RefreshAsync"/>). Bu yüzden bozuk dosya hata değil, boş liste
/// sayılır. Jeton burada durmaz, gizli bilgi taşımaz; düz JSON yeterli.
/// </remarks>
public sealed class SharedFileLog
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _path;

    public SharedFileLog(string? path = null) => _path = path ?? DefaultPath;

    /// <summary>Ayarın yanında, <c>%APPDATA%</c> altında. Exe'nin yanında değil.</summary>
    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VidShrink",
            "yuklemeler.json");

    /// <summary>Kaydın diskteki yeri.</summary>
    public string FilePath => _path;

    public IReadOnlyList<UploadedFile> Load()
    {
        try
        {
            if (!File.Exists(_path)) return Array.Empty<UploadedFile>();
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<UploadedFile>>(json, Options) ?? new List<UploadedFile>();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            // Kayıt bozuksa boş sayılır; doğrusu Drive'dan yeniden kurulur.
            return Array.Empty<UploadedFile>();
        }
    }

    /// <summary>Toplam boyut. Kullanıcı Drive'ının ne kadarını VidShrink'in doldurduğunu görür.</summary>
    public long TotalBytes() => Load().Sum(x => x.SizeBytes);

    public void Record(UploadedFile file)
    {
        var all = Load().Where(x => x.FileId != file.FileId).ToList();
        all.Add(file);
        Write(all);
    }

    /// <summary>Yayın kapandı: bağlantı öldü ama <b>dosya Drive'da kaldı</b>, kayıt da kalır.</summary>
    public void MarkUnshared(string fileId)
    {
        var all = Load().ToList();
        var index = all.FindIndex(x => x.FileId == fileId);
        if (index < 0) return;
        all[index] = all[index] with { Shared = false, PermissionId = null };
        Write(all);
    }

    /// <summary>Dosya Drive'dan silindi: kayıt da gider.</summary>
    public void Remove(string fileId)
    {
        var all = Load().ToList();
        if (all.RemoveAll(x => x.FileId == fileId) == 0) return;
        Write(all);
    }

    /// <summary>Drive'dan gelen listeyle kaydı baştan kurar. Bozuk kayıt böyle onarılır.</summary>
    public void ReplaceAll(IEnumerable<UploadedFile> files) => Write(files.ToList());

    private void Write(IReadOnlyList<UploadedFile> files)
    {
        var folder = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        File.WriteAllText(_path, JsonSerializer.Serialize(files, Options));
    }
}
