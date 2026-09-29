using System.Globalization;
using System.Text.Json;

namespace VidShrink.Core;

public sealed class UpdateHealth
{
    public const string FileName = "update-health.json";

    public const int FailureLimit = 3;

    public DateTimeOffset? LastSuccess { get; private set; }

    public DateTimeOffset? FailingSince { get; private set; }

    public int Failures { get; private set; }

    public string LastError { get; private set; } = "";

    public bool Stuck => Failures >= FailureLimit;

    public static string PathFor(string? settingsPath) =>
        settingsPath is null
            ? Path.Combine(Path.GetDirectoryName(UpdateSettings.DefaultPath) ?? "", FileName)
            : settingsPath + "." + FileName;

    public static UpdateHealth Load(string? settingsPath = null)
    {
        var health = new UpdateHealth();
        try
        {
            var file = PathFor(settingsPath);
            if (!File.Exists(file)) return health;
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return health;
            health.LastSuccess = ReadTime(root, "lastSuccess");
            health.FailingSince = ReadTime(root, "failingSince");
            if (root.TryGetProperty("failures", out var failures) && failures.ValueKind == JsonValueKind.Number && failures.TryGetInt32(out var count))
                health.Failures = Math.Max(0, count);
            if (root.TryGetProperty("lastError", out var error) && error.ValueKind == JsonValueKind.String)
                health.LastError = error.GetString() ?? "";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return health;
    }

    public static UpdateHealth RecordSuccess(string? settingsPath, DateTimeOffset now)
    {
        var health = new UpdateHealth { LastSuccess = now };
        health.TrySave(settingsPath);
        return health;
    }

    public static UpdateHealth RecordFailure(string? settingsPath, DateTimeOffset now, string reason)
    {
        var health = Load(settingsPath);
        health.Failures++;
        health.FailingSince ??= now;
        health.LastError = reason;
        health.TrySave(settingsPath);
        return health;
    }

    private void TrySave(string? settingsPath)
    {
        try
        {
            var file = PathFor(settingsPath);
            var folder = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            using var stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            if (LastSuccess is { } success) writer.WriteString("lastSuccess", success.ToString("O", CultureInfo.InvariantCulture));
            if (FailingSince is { } since) writer.WriteString("failingSince", since.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteNumber("failures", Failures);
            if (LastError.Length > 0) writer.WriteString("lastError", LastError);
            writer.WriteEndObject();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static DateTimeOffset? ReadTime(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time)
            ? time
            : null;
}
