using System.Text.Json;

namespace VidShrink.Core;

/// <summary>
/// Yerinde güncelleme: ayrı kurucu yok, bekleme yok. Windows koşan exe'nin ve yüklü
/// dll'in <em>adını</em> değiştirmeye izin verir, üstüne yazmaya izin vermez. Bu yüzden her
/// değişen dosya için hedef önce <c>&lt;ad&gt;.old</c> olur, sahnedeki doğrulanmış dosya aynı
/// birimde tek bir taşımayla hedefin adını alır. Değişmeyen dosyaya dokunulmaz. Koşan süreç
/// eski dosyaları eski adlarıyla açık tutmaya devam eder; yeni süreç yeni dosyaları açar.
/// <c>.old</c> dosyaları bir sonraki açılışta <see cref="SweepRetired"/> ile silinir.
///
/// <para>Her adım <see cref="JournalName"/> günlüğüne önceden yazılır. Taşımalardan biri
/// düşerse yapılanlar tersinden geri alınır: yerleşen dosya sahneye, <c>.old</c> hedefe
/// döner ve istisna çağırana gider; çağıran eski yola (başlatıcının <c>--update-now</c>'ı)
/// düşer. Süreç yarıda ölürse günlük kalır ve <see cref="Recover"/> aynı geri almayı yapar.</para>
/// </summary>
public static class InPlaceUpdate
{
    /// <summary>Yarıda kalan yer değiştirmenin günlüğü, uygulama klasöründe.</summary>
    public const string JournalName = ".update-inplace.json";

    /// <summary>Emekliye ayrılan dosyanın eki.</summary>
    public const string RetiredSuffix = ".old";

    /// <summary>Tek bir dosyanın yer değiştirmesi.</summary>
    public sealed record Step(string Target, string Retired, string Staged, bool Existed);

    /// <summary>Yarıda kalmış bir yer değiştirme duruyor mu.</summary>
    public static bool HasPending(string appDirectory) =>
        File.Exists(Path.Combine(appDirectory, JournalName));

    /// <summary>
    /// Sahnedeki uygulama dosyalarını yerine koyar. Önce hepsi doğrulanır; tutmayan tek
    /// dosya bile varsa hiçbir şeye dokunulmaz ve sahne atılmaz, eski yol aynı sahneyi
    /// kullanabilir. Bir taşıma düşerse yapılanlar geri alınır ve istisna yeniden atılır.
    /// </summary>
    public static void Swap(string stageDirectory, string appDirectory, IReadOnlyList<ManifestFile> files)
    {
        var mismatch = UpdateStage.FindMismatch(stageDirectory, files);
        if (mismatch is not null)
            throw new InvalidDataException($"İnen dosyanın özeti tutmadı: {mismatch.Path}");

        var steps = files.Select(file => Plan(stageDirectory, appDirectory, file)).ToList();
        WriteJournal(appDirectory, stageDirectory, steps);

        var done = new List<Step>();
        try
        {
            foreach (var step in steps)
            {
                var folder = Path.GetDirectoryName(step.Target);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                if (step.Existed) File.Move(step.Target, step.Retired);
                done.Add(step);
                File.Move(step.Staged, step.Target);
            }
        }
        catch
        {
            if (Undo(done)) ClearJournal(appDirectory);
            throw;
        }

        ClearJournal(appDirectory);
    }

    /// <summary>
    /// Bütün güncellemeyi uygular: uygulama dosyaları yerinde yer değiştirir, sonra kabuk
    /// dosyaları, sürüm işareti, uygulandı bildirimi ve başlatıcı gelir. Başlatıcı koşuyorsa
    /// geçişi burada yapılamaz; döndürülen değer geçişin hâlâ beklediğidir. Uygulama adımı
    /// düşerse istisna atılır ve kurulumun geri kalanına dokunulmaz.
    /// </summary>
    public static bool Apply(string baseDirectory, string appDirectory, StagedUpdate staged)
    {
        var stage = staged.Stage;
        var previous = UpdateCheck.ReadVersionMarker(appDirectory);
        var version = staged.Manifest.Version;

        Swap(stage, appDirectory, staged.App);
        ShellUpdate.Apply(stage, baseDirectory, staged.Shell);
        UpdateCheck.WriteVersionMarker(appDirectory, version);
        if (previous is not null && previous != version) AppliedUpdateNotice.Write(appDirectory, version);

        var launcherPending = false;
        if (staged.Launcher.Count > 0)
        {
            try
            {
                var moved = LauncherUpdate.Stage(stage, baseDirectory, staged.Launcher);
                if (moved.Count == staged.Launcher.Count && LauncherUpdate.Arm(baseDirectory, staged.Launcher, version))
                    launcherPending = !LauncherUpdate.Commit(baseDirectory, null, TimeSpan.Zero);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                launcherPending = LauncherUpdate.Pending(baseDirectory) is not null;
            }
        }
        else LauncherUpdate.MarkVerified(baseDirectory, staged.Manifest);

        UpdateStage.Discard(stage);
        return launcherPending;
    }

    /// <summary>
    /// Yarıda kalmış yer değiştirmeyi geri alır: <c>.old</c> duran her hedef eski dosyasına
    /// döner, yeni dosya sahneye geri taşınır. Döndürdüğü değer bir iş yapılıp
    /// yapılmadığıdır. Geri alma tam olursa günlük silinir; olmazsa kalır ve bir sonraki
    /// açılış yeniden dener.
    /// </summary>
    public static bool Recover(string appDirectory)
    {
        var journal = Path.Combine(appDirectory, JournalName);
        if (!File.Exists(journal)) return false;

        List<Step> steps;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(journal));
            steps = new List<Step>();
            foreach (var element in document.RootElement.GetProperty("steps").EnumerateArray())
            {
                steps.Add(new Step(
                    element.GetProperty("target").GetString()!,
                    element.GetProperty("retired").GetString()!,
                    element.GetProperty("staged").GetString()!,
                    element.GetProperty("existed").GetBoolean()));
            }
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or IOException
            or InvalidOperationException or UnauthorizedAccessException)
        {
            ClearJournal(appDirectory);
            return false;
        }

        if (Undo(steps)) ClearJournal(appDirectory);
        return true;
    }

    /// <summary>
    /// Önceki güncellemeden kalan <c>.old</c> dosyalarını sessizce siler. Yarıda kalmış bir
    /// yer değiştirme varsa hiçbirine dokunmaz: geri alma onlara muhtaç. Hâlâ açık olan
    /// dosya kalır, sonraki açılış yeniden dener.
    /// </summary>
    public static int SweepRetired(string appDirectory)
    {
        if (HasPending(appDirectory) || !Directory.Exists(appDirectory)) return 0;
        var removed = 0;
        try
        {
            foreach (var retired in Directory.EnumerateFiles(appDirectory, "*" + RetiredSuffix, SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(retired);
                    removed++;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        return removed;
    }

    private static Step Plan(string stageDirectory, string appDirectory, ManifestFile file)
    {
        var target = UpdateCheck.LocalPath(appDirectory, file.Path);
        var staged = UpdateCheck.LocalPath(stageDirectory, file.Path);
        var existed = File.Exists(target);
        return new Step(target, existed ? RetiredName(target) : target + RetiredSuffix, staged, existed);
    }

    private static string RetiredName(string target)
    {
        var retired = target + RetiredSuffix;
        if (!File.Exists(retired)) return retired;
        try
        {
            File.Delete(retired);
            return retired;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return target + "." + Guid.NewGuid().ToString("N")[..8] + RetiredSuffix;
        }
    }

    private static bool Undo(IReadOnlyList<Step> steps)
    {
        var clean = true;
        for (var i = steps.Count - 1; i >= 0; i--)
        {
            var step = steps[i];
            try
            {
                if (step.Existed)
                {
                    if (!File.Exists(step.Retired)) continue;
                    if (File.Exists(step.Target))
                    {
                        if (File.Exists(step.Staged)) File.Delete(step.Target);
                        else File.Move(step.Target, step.Staged);
                    }
                    File.Move(step.Retired, step.Target);
                }
                else if (File.Exists(step.Target) && !File.Exists(step.Staged))
                {
                    File.Move(step.Target, step.Staged);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                clean = false;
            }
        }
        return clean;
    }

    private static void WriteJournal(string appDirectory, string stageDirectory, IReadOnlyList<Step> steps)
    {
        Directory.CreateDirectory(appDirectory);
        using var stream = new FileStream(Path.Combine(appDirectory, JournalName), FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("stage", stageDirectory);
        writer.WriteStartArray("steps");
        foreach (var step in steps)
        {
            writer.WriteStartObject();
            writer.WriteString("target", step.Target);
            writer.WriteString("retired", step.Retired);
            writer.WriteString("staged", step.Staged);
            writer.WriteBoolean("existed", step.Existed);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static void ClearJournal(string appDirectory)
    {
        try { File.Delete(Path.Combine(appDirectory, JournalName)); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }
}
