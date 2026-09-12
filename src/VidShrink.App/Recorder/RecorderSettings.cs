using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using VidShrink.Core;

namespace VidShrink.App.Recorder;

/// <summary>
/// Kaydedicinin kalıcı ayarları. Kalıp <c>Playback/PlayerSettings.cs</c> ile aynı: okuma
/// bozuk dosyada varsayılana düşer, yazma geçici dosya üzerinden taşınır.
///
/// <para>Varsayılan sayıların hiçbiri burada uydurulmuyor — kare hızı, kodlayıcı, ön ayar
/// ve kalite motorun kendi sabitlerinden (<see cref="RecorderArguments"/>) geliyor. Böylece
/// arayüzün açılışta gösterdiği değer motorun varsayılanıyla aynı kalıyor.</para>
/// </summary>
internal sealed class RecorderSettings
{
    internal const string FileName = "recorder-settings.json";

    /// <summary>Bölge kaydının açılışta gösterdiği dikdörtgen. <c>yuv420p</c> için iki ölçü de çift.</summary>
    internal const int DefaultRegionWidth = 1280;

    /// <summary>Bölge kaydının açılışta gösterdiği yükseklik.</summary>
    internal const int DefaultRegionHeight = 720;

    internal string? OutputFolder { get; set; }

    internal int Fps { get; set; } = RecorderArguments.DefaultFps;

    internal string Codec { get; set; } = RecorderArguments.DefaultVideoCodec;

    internal string Preset { get; set; } = RecorderArguments.DefaultPreset;

    internal double Quality { get; set; } = RecorderArguments.DefaultQuality;

    internal bool ShowCursor { get; set; } = true;

    internal RecorderTargetKind Target { get; set; } = RecorderTargetKind.Screen;

    internal string? WindowTitle { get; set; }

    internal int RegionX { get; set; }

    internal int RegionY { get; set; }

    internal int RegionWidth { get; set; } = DefaultRegionWidth;

    internal int RegionHeight { get; set; } = DefaultRegionHeight;

    /// <summary>
    /// Seçilen mikrofonun adı. Indeks değil ad saklanıyor: cihaz listesi iki açılış
    /// arasında sıra değiştirdiğinde indeks başka cihazı gösterirdi. Cihaz artık yoksa
    /// kutular sessizde açılıyor.
    /// </summary>
    internal string? MicrophoneName { get; set; }

    /// <summary>Seçilen sistem sesi cihazının adı.</summary>
    internal string? SystemAudioName { get; set; }

    /// <summary>
    /// Ayarların durduğu klasör. Kaydedici ana pencereye bağlanmadığı için yolu kendisi
    /// çözüyor; program başına tek yer.
    /// </summary>
    internal static string? Folder
    {
        get
        {
            var data = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return string.IsNullOrEmpty(data) ? null : Path.Combine(data, "VidShrink");
        }
    }

    internal static string? FilePath => Folder is { } folder ? Path.Combine(folder, FileName) : null;

    internal static RecorderSettings Load(string? file)
    {
        var settings = new RecorderSettings();
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return settings;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject root) return settings;
            settings.OutputFolder = (string?)root["outputFolder"];
            if ((int?)root["fps"] is { } fps && fps > 0) settings.Fps = fps;
            if ((string?)root["codec"] is { Length: > 0 } codec) settings.Codec = codec;
            if ((string?)root["preset"] is { Length: > 0 } preset) settings.Preset = preset;
            if ((double?)root["quality"] is { } quality) settings.Quality = quality;
            settings.ShowCursor = (bool?)root["showCursor"] ?? true;
            if (Enum.TryParse<RecorderTargetKind>((string?)root["target"], true, out var target)) settings.Target = target;
            settings.WindowTitle = (string?)root["windowTitle"];
            settings.RegionX = (int?)root["regionX"] ?? 0;
            settings.RegionY = (int?)root["regionY"] ?? 0;
            if ((int?)root["regionWidth"] is { } width && width > 0) settings.RegionWidth = width;
            if ((int?)root["regionHeight"] is { } height && height > 0) settings.RegionHeight = height;
            settings.MicrophoneName = (string?)root["microphoneName"];
            settings.SystemAudioName = (string?)root["systemAudioName"];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return new RecorderSettings();
        }

        return settings;
    }

    internal void Save(string? file)
    {
        if (string.IsNullOrEmpty(file)) return;
        try
        {
            var folder = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            var temp = file + ".tmp";
            using (var stream = File.Create(temp))
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                if (OutputFolder is null) writer.WriteNull("outputFolder");
                else writer.WriteString("outputFolder", OutputFolder);
                writer.WriteNumber("fps", Fps);
                writer.WriteString("codec", Codec);
                writer.WriteString("preset", Preset);
                writer.WriteNumber("quality", Quality);
                writer.WriteBoolean("showCursor", ShowCursor);
                writer.WriteString("target", Target.ToString());
                if (WindowTitle is null) writer.WriteNull("windowTitle");
                else writer.WriteString("windowTitle", WindowTitle);
                writer.WriteNumber("regionX", RegionX);
                writer.WriteNumber("regionY", RegionY);
                writer.WriteNumber("regionWidth", RegionWidth);
                writer.WriteNumber("regionHeight", RegionHeight);
                if (MicrophoneName is null) writer.WriteNull("microphoneName");
                else writer.WriteString("microphoneName", MicrophoneName);
                if (SystemAudioName is null) writer.WriteNull("systemAudioName");
                else writer.WriteString("systemAudioName", SystemAudioName);
                writer.WriteEndObject();
            }
            File.Move(temp, file, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Kaydın yazılacağı klasör. Ayarda bir klasör yoksa işletim sisteminin video klasörü,
    /// o da yoksa çalışma klasörü.
    /// </summary>
    internal string ResolveFolder()
    {
        if (!string.IsNullOrWhiteSpace(OutputFolder)) return OutputFolder!;
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (!string.IsNullOrEmpty(videos)) return Path.Combine(videos, "VidShrink");
        return Environment.CurrentDirectory;
    }

    /// <summary>
    /// Yeni kaydın dosya yolu. Ad zaman damgasından geliyor; aynı saniyede ikinci kayıt
    /// başlarsa sona sayı ekleniyor, var olan dosyanın üstüne yazılmıyor.
    /// </summary>
    internal string OutputPath(DateTime now)
    {
        var folder = ResolveFolder();
        var stem = "kayit_" + now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        var candidate = Path.Combine(folder, stem + ".mp4");
        for (var index = 2; File.Exists(candidate); index++)
            candidate = Path.Combine(folder, stem + "_" + index.ToString(CultureInfo.InvariantCulture) + ".mp4");
        return candidate;
    }
}
