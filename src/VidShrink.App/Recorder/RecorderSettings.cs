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

    /// <summary>Kaydın yazıldığı kap; çıktı uzantısı bundan geliyor.</summary>
    internal RecorderContainer Container { get; set; } = RecorderContainer.Mp4;

    /// <summary>
    /// Seçilen monitörün indeksi. Windows'ta sıfırdan farklı indeks monitör sınırlarından
    /// bölge ofsetine çevriliyor; sınırlar okunamazsa motor seçimi reddediyor.
    /// </summary>
    internal int ScreenIndex { get; set; }

    /// <summary>Çıktı ölçeklemesinin genişliği; sıfır ölçekleme yok demek.</summary>
    internal int ScaleWidth { get; set; }

    /// <summary>Çıktı ölçeklemesinin yüksekliği; sıfır ölçekleme yok demek.</summary>
    internal int ScaleHeight { get; set; }

    /// <summary>Anahtar kare aralığı, saniye.</summary>
    internal int KeyframeSeconds { get; set; } = RecorderArguments.DefaultKeyframeSeconds;

    /// <summary>Kodlayıcı profili; boş bırakılırsa <c>-profile:v</c> yazılmıyor.</summary>
    internal string? Profile { get; set; }

    /// <summary>Kodlayıcı ayarı; boş bırakılırsa <c>-tune</c> yazılmıyor.</summary>
    internal string? Tune { get; set; }

    /// <summary>Hangi hız kontrolü kolunun koşacağı.</summary>
    internal RecorderRateControl RateControl { get; set; } = RecorderRateControl.Quality;

    /// <summary>Hedef görüntü bit hızı, kbit/s. Sıfır "verilmedi" demek.</summary>
    internal int BitrateKbps { get; set; }

    /// <summary>Tavan bit hızı, kbit/s. Sıfır "verilmedi" demek.</summary>
    internal int MaxBitrateKbps { get; set; }

    /// <summary>Tampon boyu, kbit. Sıfır "verilmedi" demek.</summary>
    internal int BufferKbits { get; set; }

    /// <summary>Piksel biçimi.</summary>
    internal string PixelFormat { get; set; } = RecorderArguments.DefaultPixelFormat;

    /// <summary>Renk uzayı; boş bırakılırsa yazılmıyor.</summary>
    internal string? ColorSpace { get; set; }

    /// <summary>Renk aralığı; boş bırakılırsa yazılmıyor.</summary>
    internal string? ColorRange { get; set; }

    /// <summary>Kaydın kendiliğinden duracağı süre, saniye. Sıfır sınır yok demek.</summary>
    internal double MaxDurationSeconds { get; set; }

    /// <summary>Kendiliğinden bölme süresi, saniye. Sıfır ölçüt yok demek.</summary>
    internal double SplitSeconds { get; set; }

    /// <summary>Kendiliğinden bölme boyutu, MB. Sıfır ölçüt yok demek.</summary>
    internal double SplitMegabytes { get; set; }

    /// <summary>İki ses girdisi seçildiğinde tek ize karıştırılsın mı, ayrı izlere mi gitsin.</summary>
    internal AudioTrackLayout AudioLayout { get; set; } = AudioTrackLayout.MixedSingleTrack;

    /// <summary>Ses kazancı, desibel.</summary>
    internal double AudioGainDb { get; set; }

    /// <summary>Gürültü kapısı (<c>agate</c>) açık mı.</summary>
    internal bool AudioNoiseGate { get; set; }

    /// <summary>Gürültü bastırma (<c>afftdn</c>) açık mı.</summary>
    internal bool AudioNoiseSuppression { get; set; }

    /// <summary>
    /// Kullanıcının seçtiği ses filtreleri. Hiçbiri açık değilse
    /// <see cref="AudioFilterOptions.None"/> dönüyor ve grafik hiç kurulmuyor.
    /// </summary>
    internal AudioFilterOptions AudioFilters =>
        new(AudioGainDb, AudioNoiseGate, AudioNoiseSuppression);

    /// <summary>Ölçekleme seçilmişse boyut, seçilmemişse <c>null</c>.</summary>
    internal RecorderScale? Scale =>
        ScaleWidth > 0 && ScaleHeight > 0 ? new RecorderScale(ScaleWidth, ScaleHeight) : null;

    /// <summary>Bölme ölçütü verilmişse ölçüt, verilmemişse <c>null</c>.</summary>
    internal RecorderSplit? Split
    {
        get
        {
            var duration = SplitSeconds > 0 ? TimeSpan.FromSeconds(SplitSeconds) : (TimeSpan?)null;
            var megabytes = SplitMegabytes > 0 ? SplitMegabytes : (double?)null;
            return duration is null && megabytes is null ? null : new RecorderSplit(duration, megabytes);
        }
    }

    /// <summary>Süre sınırı verilmişse süre, verilmemişse <c>null</c>.</summary>
    internal TimeSpan? MaxDuration =>
        MaxDurationSeconds > 0 ? TimeSpan.FromSeconds(MaxDurationSeconds) : null;

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
            if (Enum.TryParse<RecorderContainer>((string?)root["container"], true, out var container)) settings.Container = container;
            settings.ScreenIndex = (int?)root["screenIndex"] ?? 0;
            settings.ScaleWidth = (int?)root["scaleWidth"] ?? 0;
            settings.ScaleHeight = (int?)root["scaleHeight"] ?? 0;
            settings.KeyframeSeconds = (int?)root["keyframeSeconds"] ?? RecorderArguments.DefaultKeyframeSeconds;
            settings.Profile = (string?)root["profile"];
            settings.Tune = (string?)root["tune"];
            if (Enum.TryParse<RecorderRateControl>((string?)root["rateControl"], true, out var rate)) settings.RateControl = rate;
            settings.BitrateKbps = (int?)root["bitrateKbps"] ?? 0;
            settings.MaxBitrateKbps = (int?)root["maxBitrateKbps"] ?? 0;
            settings.BufferKbits = (int?)root["bufferKbits"] ?? 0;
            if ((string?)root["pixelFormat"] is { Length: > 0 } pixelFormat) settings.PixelFormat = pixelFormat;
            settings.ColorSpace = (string?)root["colorSpace"];
            settings.ColorRange = (string?)root["colorRange"];
            settings.MaxDurationSeconds = (double?)root["maxDurationSeconds"] ?? 0;
            settings.SplitSeconds = (double?)root["splitSeconds"] ?? 0;
            settings.SplitMegabytes = (double?)root["splitMegabytes"] ?? 0;
            if (Enum.TryParse<AudioTrackLayout>((string?)root["audioLayout"], true, out var layout)) settings.AudioLayout = layout;
            settings.AudioGainDb = (double?)root["audioGainDb"] ?? 0;
            settings.AudioNoiseGate = (bool?)root["audioNoiseGate"] ?? false;
            settings.AudioNoiseSuppression = (bool?)root["audioNoiseSuppression"] ?? false;
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
                writer.WriteString("container", Container.ToString());
                writer.WriteNumber("screenIndex", ScreenIndex);
                writer.WriteNumber("scaleWidth", ScaleWidth);
                writer.WriteNumber("scaleHeight", ScaleHeight);
                writer.WriteNumber("keyframeSeconds", KeyframeSeconds);
                if (Profile is null) writer.WriteNull("profile");
                else writer.WriteString("profile", Profile);
                if (Tune is null) writer.WriteNull("tune");
                else writer.WriteString("tune", Tune);
                writer.WriteString("rateControl", RateControl.ToString());
                writer.WriteNumber("bitrateKbps", BitrateKbps);
                writer.WriteNumber("maxBitrateKbps", MaxBitrateKbps);
                writer.WriteNumber("bufferKbits", BufferKbits);
                writer.WriteString("pixelFormat", PixelFormat);
                if (ColorSpace is null) writer.WriteNull("colorSpace");
                else writer.WriteString("colorSpace", ColorSpace);
                if (ColorRange is null) writer.WriteNull("colorRange");
                else writer.WriteString("colorRange", ColorRange);
                writer.WriteNumber("maxDurationSeconds", MaxDurationSeconds);
                writer.WriteNumber("splitSeconds", SplitSeconds);
                writer.WriteNumber("splitMegabytes", SplitMegabytes);
                writer.WriteString("audioLayout", AudioLayout.ToString());
                writer.WriteNumber("audioGainDb", AudioGainDb);
                writer.WriteBoolean("audioNoiseGate", AudioNoiseGate);
                writer.WriteBoolean("audioNoiseSuppression", AudioNoiseSuppression);
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
    /// başlarsa sona sayı ekleniyor, var olan dosyanın üstüne yazılmıyor. Uzantı
    /// <see cref="Container"/>'dan okunuyor — motorun doğrulaması kapla uzantının
    /// ayrışmasını reddediyor, o yüzden burada sabit bir uzantı yazılamaz.
    /// </summary>
    internal string OutputPath(DateTime now)
    {
        var folder = ResolveFolder();
        var extension = "." + RecorderArguments.Extension(Container);
        var stem = "kayit_" + now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        var candidate = Path.Combine(folder, stem + extension);
        for (var index = 2; File.Exists(candidate); index++)
            candidate = Path.Combine(folder, stem + "_" + index.ToString(CultureInfo.InvariantCulture) + extension);
        return candidate;
    }

    /// <summary>
    /// Kayıt sürerken alınan ekran görüntüsünün dosya yolu. Kaydın adının yanına
    /// numaralanıyor; var olan dosyanın üstüne yazılmıyor.
    /// </summary>
    internal string SnapshotPath(DateTime now)
    {
        var folder = ResolveFolder();
        var stem = "kare_" + now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        var candidate = Path.Combine(folder, stem + ".png");
        for (var index = 2; File.Exists(candidate); index++)
            candidate = Path.Combine(folder, stem + "_" + index.ToString(CultureInfo.InvariantCulture) + ".png");
        return candidate;
    }
}
