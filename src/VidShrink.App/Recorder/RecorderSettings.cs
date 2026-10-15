using System;
using System.Globalization;
using System.IO;
using System.Linq;
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

    /// <summary>
    /// Kullanıcı kodlama kolunu kendi mi yazıyor. Varsayılan <c>false</c>: program
    /// <see cref="RecorderAutoPlan"/> ile kendi seçiyor. Kaydedilen şey seçimin kendisi
    /// değil "elle istendi" bilgisi, çünkü makine iki açılış arasında değişebiliyor.
    /// </summary>
    internal bool ManualMode { get; set; }

    internal bool AdvancedMode { get; set; }

    internal RecorderWizardContent? WizardContent { get; set; }

    internal RecorderWizardAudio? WizardAudio { get; set; }

    internal RecorderWizardDestination? WizardDestination { get; set; }

    internal static readonly int[] CountdownChoices = { 0, 3, 5, 10 };

    internal int CountdownSeconds { get; set; }

    internal RecorderRepeat ScheduleRepeat { get; set; } = RecorderRepeat.Off;

    /// <summary>Saatli kaydın ilk oluşumu, yerel saatle; yineleme bu çapadan sayılır.</summary>
    internal DateTime ScheduleAt { get; set; }

    /// <summary>Kullanıcının istediği tahmini süre; verilmediyse <c>null</c>.</summary>
    internal int? TargetSeconds { get; set; }

    /// <summary>Kullanıcının istediği tahmini dosya boyutu; verilmediyse <c>null</c>.</summary>
    internal double? TargetMegabytes { get; set; }

    internal int Fps { get; set; } = RecorderArguments.DefaultFps;

    internal string Codec { get; set; } = RecorderArguments.DefaultVideoCodec;

    internal string Preset { get; set; } = RecorderArguments.DefaultPreset;

    internal double Quality { get; set; } = RecorderArguments.DefaultQuality;

    internal bool ShowCursor { get; set; } = true;

    internal bool OpenFolderWhenDone { get; set; }

    internal bool ShowResultCard { get; set; } = true;

    internal bool PushToTalk { get; set; }

    internal RecorderFinishAction FinishAction { get; set; } = RecorderFinishAction.Nothing;

    internal bool ShowClicks { get; set; }

    internal bool ClickSound { get; set; }

    internal bool ShowKeys { get; set; }

    /// <summary>Varsayılan açık: tuş gösterimi yalnız kısayolları yazar, yazılan parola kayda girmez.</summary>
    internal bool KeysShortcutsOnly { get; set; } = true;

    internal KeyCaptionStyle KeyCaption { get; set; } = KeyCaptionStyle.Default;

    internal bool ShowMagnifier { get; set; }

    internal bool CursorHighlight { get; set; }

    internal bool Spotlight { get; set; }

    internal PointerRingSize PointerSize { get; set; } = PointerStyle.Default.Size;

    internal PointerColor HighlightColor { get; set; } = PointerStyle.Default.HighlightColor;

    internal PointerColor LeftClickColor { get; set; } = PointerStyle.Default.LeftColor;

    internal PointerColor RightClickColor { get; set; } = PointerStyle.Default.RightColor;

    internal bool LivePreview { get; set; }

    internal int ReplaySeconds { get; set; } = ReplayBuffer.DefaultSeconds;

    /// <summary>
    /// Genel kısayolların ataması, <see cref="RecorderHotkeys.Write"/> yazımıyla. Anahtarı
    /// taşımayan eski dosya ve okunamayan yazım varsayılan F7–F11 ile açılır.
    /// </summary>
    internal string Hotkeys { get; set; } = RecorderHotkeys.Write(RecorderHotkeys.All);

    internal RecorderTargetKind Target { get; set; } = RecorderTargetKind.Screen;

    internal string? WindowTitle { get; set; }

    internal int RegionX { get; set; }

    internal int RegionY { get; set; }

    internal int RegionWidth { get; set; } = DefaultRegionWidth;

    internal int RegionHeight { get; set; } = DefaultRegionHeight;

    internal string RegionAspect { get; set; } = RegionDraw.Free;

    /// <summary>
    /// Seçilen mikrofonun adı. Indeks değil ad saklanıyor: cihaz listesi iki açılış
    /// arasında sıra değiştirdiğinde indeks başka cihazı gösterirdi. Cihaz artık yoksa
    /// kutular sessizde açılıyor.
    /// </summary>
    internal string? MicrophoneName { get; set; }

    /// <summary>Seçilen sistem sesi cihazının adı.</summary>
    internal string? SystemAudioName { get; set; }

    internal string? WebcamName { get; set; }

    internal WebcamShape WebcamShape { get; set; } = WebcamShape.Rectangle;

    internal bool WebcamMirror { get; set; }

    /// <summary>Kamera kutusunun yeri ve boyu, <see cref="WebcamPlacement"/> oranlarıyla; varsayılan sağ alt.</summary>
    internal double WebcamX { get; set; } = 1;

    internal double WebcamY { get; set; } = 1;

    internal double WebcamSize { get; set; } = 0.2;

    internal WebcamBackground WebcamBackground { get; set; } = WebcamBackground.Keep;

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

    /// <summary>Kare hızı kipi; Otomatik'te <c>-fps_mode</c> yazılmıyor.</summary>
    internal RecorderFrameRateMode FrameRateMode { get; set; } = RecorderFrameRateMode.Auto;

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

    /// <summary>HDR ekranda kaydı SDR'ye çevir; varsayılan kapalı, kayıt HDR kalır.</summary>
    internal bool HdrToSdr { get; set; }

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

    /// <summary>Dinamik aralık sıkıştırıcısı (<c>acompressor</c>) açık mı.</summary>
    internal bool AudioCompressor { get; set; }

    /// <summary>Tepe sınırlayıcı (<c>alimiter</c>) açık mı.</summary>
    internal bool AudioLimiter { get; set; }

    /// <summary>Ses kodeği; <see cref="RecorderArguments.AudioCodecsFor"/> kümesinden.</summary>
    internal string AudioCodec { get; set; } = RecorderArguments.DefaultAudioCodec;

    /// <summary>Gizlilik maskeleri, masaüstü pikselinde; kayda bulanık yakılır.</summary>
    internal List<RecorderRegion> PrivacyMasks { get; set; } = new();

    /// <summary>
    /// Kullanıcının seçtiği ses filtreleri. Hiçbiri açık değilse
    /// <see cref="AudioFilterOptions.None"/> dönüyor ve grafik hiç kurulmuyor.
    /// </summary>
    internal AudioFilterOptions AudioFilters =>
        new(AudioGainDb, AudioNoiseGate, AudioNoiseSuppression, AudioCompressor, AudioLimiter);

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
    /// Ayarların durduğu klasör: programın ana ayar dosyasının klasörü. Yol
    /// <see cref="UpdateSettings.DefaultPath"/>'ten okunuyor, böylece
    /// <c>VIDSHRINK_SETTINGS_PATH</c> ana ayarı nereye alıyorsa kaydedicinin ayarı da oraya
    /// gidiyor ve test ya da ölçüm gerçek AppData'ya yazmıyor.
    /// </summary>
    internal static string? Folder
    {
        get
        {
            var main = UpdateSettings.DefaultPath;
            var folder = Path.GetDirectoryName(main);
            return string.IsNullOrEmpty(folder) || !Path.IsPathRooted(main) ? null : folder;
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
            settings.ManualMode = (bool?)root["manualMode"] ?? !((bool?)root["autoMode"] ?? true);
            settings.AdvancedMode = (bool?)root["advancedMode"] ?? settings.ManualMode;
            if ((int?)root["countdownSeconds"] is { } countdown && Array.IndexOf(CountdownChoices, countdown) >= 0) settings.CountdownSeconds = countdown;
            if (Enum.TryParse<RecorderRepeat>((string?)root["scheduleRepeat"], true, out var repeat) && Enum.IsDefined(repeat)) settings.ScheduleRepeat = repeat;
            if (DateTime.TryParseExact((string?)root["scheduleAt"], "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)) settings.ScheduleAt = at;
            if (settings.ScheduleAt == default) settings.ScheduleRepeat = RecorderRepeat.Off;
            if ((int?)root["targetSeconds"] is { } targetSeconds && targetSeconds > 0) settings.TargetSeconds = targetSeconds;
            if ((double?)root["targetMegabytes"] is { } targetMegabytes && targetMegabytes > 0) settings.TargetMegabytes = targetMegabytes;
            if ((int?)root["fps"] is { } fps && fps > 0) settings.Fps = fps;
            if ((string?)root["codec"] is { Length: > 0 } codec) settings.Codec = codec;
            if ((string?)root["preset"] is { Length: > 0 } preset) settings.Preset = preset;
            if ((double?)root["quality"] is { } quality) settings.Quality = quality;
            settings.ShowCursor = (bool?)root["showCursor"] ?? true;
            settings.OpenFolderWhenDone = (bool?)root["openFolderWhenDone"] ?? false;
            settings.ShowResultCard = (bool?)root["showResultCard"] ?? true;
            settings.PushToTalk = (bool?)root["pushToTalk"] ?? false;
            if (Enum.TryParse<RecorderFinishAction>((string?)root["finishAction"], true, out var finish) && Enum.IsDefined(finish)) settings.FinishAction = finish;
            settings.ShowClicks = (bool?)root["showClicks"] ?? false;
            settings.ClickSound = (bool?)root["clickSound"] ?? false;
            settings.ShowKeys = (bool?)root["showKeys"] ?? false;
            settings.KeysShortcutsOnly = (bool?)root["keysShortcutsOnly"] ?? true;
            var caption = KeyCaptionStyle.Default;
            if (Enum.TryParse<KeyCaptionPlace>((string?)root["keyCaptionPlace"], true, out var captionPlace) && Enum.IsDefined(captionPlace)) caption = caption with { Place = captionPlace };
            if (Enum.TryParse<KeyCaptionSize>((string?)root["keyCaptionSize"], true, out var captionSize) && Enum.IsDefined(captionSize)) caption = caption with { Size = captionSize };
            if (Enum.TryParse<KeyCaptionHold>((string?)root["keyCaptionHold"], true, out var captionHold) && Enum.IsDefined(captionHold)) caption = caption with { Hold = captionHold };
            settings.KeyCaption = caption;
            settings.ShowMagnifier = (bool?)root["showMagnifier"] ?? false;
            settings.CursorHighlight = (bool?)root["cursorHighlight"] ?? false;
            settings.Spotlight = (bool?)root["spotlight"] ?? false;
            if (Enum.TryParse<PointerRingSize>((string?)root["pointerSize"], true, out var pointerSize) && Enum.IsDefined(pointerSize)) settings.PointerSize = pointerSize;
            if (Enum.TryParse<PointerColor>((string?)root["highlightColor"], true, out var highlightColor) && Enum.IsDefined(highlightColor)) settings.HighlightColor = highlightColor;
            if (Enum.TryParse<PointerColor>((string?)root["leftClickColor"], true, out var leftClickColor) && Enum.IsDefined(leftClickColor)) settings.LeftClickColor = leftClickColor;
            if (Enum.TryParse<PointerColor>((string?)root["rightClickColor"], true, out var rightClickColor) && Enum.IsDefined(rightClickColor)) settings.RightClickColor = rightClickColor;
            settings.LivePreview = (bool?)root["livePreview"] ?? false;
            if ((int?)root["replaySeconds"] is { } replay && Array.IndexOf(ReplayBuffer.SecondsChoices, replay) >= 0) settings.ReplaySeconds = replay;
            if (RecorderHotkeys.Read((string?)root["hotkeys"]) is { } hotkeys) settings.Hotkeys = RecorderHotkeys.Write(hotkeys);
            if (Enum.TryParse<RecorderTargetKind>((string?)root["target"], true, out var target)) settings.Target = target;
            settings.WindowTitle = (string?)root["windowTitle"];
            settings.RegionX = (int?)root["regionX"] ?? 0;
            settings.RegionY = (int?)root["regionY"] ?? 0;
            if ((int?)root["regionWidth"] is { } width && width > 0) settings.RegionWidth = width;
            if ((int?)root["regionHeight"] is { } height && height > 0) settings.RegionHeight = height;
            if ((string?)root["regionAspect"] is { } aspect && Array.IndexOf(RegionDraw.Aspects, aspect) >= 0) settings.RegionAspect = aspect;
            settings.MicrophoneName = (string?)root["microphoneName"];
            settings.SystemAudioName = (string?)root["systemAudioName"];
            settings.WebcamName = (string?)root["webcamName"];
            if (Enum.TryParse<WebcamCorner>((string?)root["webcamCorner"], true, out var corner) && Enum.IsDefined(corner))
            {
                settings.WebcamX = corner is WebcamCorner.TopLeft or WebcamCorner.BottomLeft ? 0 : 1;
                settings.WebcamY = corner is WebcamCorner.TopLeft or WebcamCorner.TopRight ? 0 : 1;
            }
            if (Enum.TryParse<WebcamShape>((string?)root["webcamShape"], true, out var shape) && Enum.IsDefined(shape)) settings.WebcamShape = shape;
            if ((bool?)root["webcamMirror"] is { } mirror) settings.WebcamMirror = mirror;
            if ((double?)root["webcamX"] is { } camX && camX is >= 0 and <= 1) settings.WebcamX = camX;
            if ((double?)root["webcamY"] is { } camY && camY is >= 0 and <= 1) settings.WebcamY = camY;
            if ((double?)root["webcamSize"] is { } camSize && camSize is >= RecorderArguments.MinWebcamSize and <= RecorderArguments.MaxWebcamSize)
                settings.WebcamSize = camSize;
            if (Enum.TryParse<WebcamBackground>((string?)root["webcamBackground"], true, out var background) && Enum.IsDefined(background)) settings.WebcamBackground = background;
            if (Enum.TryParse<RecorderWizardContent>((string?)root["wizardContent"], true, out var wizardContent) && Enum.IsDefined(wizardContent)) settings.WizardContent = wizardContent;
            if (Enum.TryParse<RecorderWizardAudio>((string?)root["wizardAudio"], true, out var wizardAudio) && Enum.IsDefined(wizardAudio)) settings.WizardAudio = wizardAudio;
            if (Enum.TryParse<RecorderWizardDestination>((string?)root["wizardDestination"], true, out var wizardDestination) && Enum.IsDefined(wizardDestination)) settings.WizardDestination = wizardDestination;
            if (Enum.TryParse<RecorderContainer>((string?)root["containerFormat"], true, out var format) && Enum.IsDefined(format)) settings.Container = format;
            else if (Enum.TryParse<RecorderContainer>((string?)root["containerChoice"], true, out var choice) && Enum.IsDefined(choice))
                settings.Container = choice == RecorderContainer.Mkv ? RecorderContainer.Mp4 : choice;
            else if (Enum.TryParse<RecorderContainer>((string?)root["container"], true, out var container) && Enum.IsDefined(container))
                settings.Container = container;
            settings.ScreenIndex = (int?)root["screenIndex"] ?? 0;
            settings.ScaleWidth = (int?)root["scaleWidth"] ?? 0;
            settings.ScaleHeight = (int?)root["scaleHeight"] ?? 0;
            settings.KeyframeSeconds = (int?)root["keyframeSeconds"] ?? RecorderArguments.DefaultKeyframeSeconds;
            settings.Profile = (string?)root["profile"];
            settings.Tune = (string?)root["tune"];
            if (Enum.TryParse<RecorderRateControl>((string?)root["rateControl"], true, out var rate)) settings.RateControl = rate;
            if (Enum.TryParse<RecorderFrameRateMode>((string?)root["frameRateMode"], true, out var fpsMode) && Enum.IsDefined(fpsMode)) settings.FrameRateMode = fpsMode;
            settings.BitrateKbps = (int?)root["bitrateKbps"] ?? 0;
            settings.MaxBitrateKbps = (int?)root["maxBitrateKbps"] ?? 0;
            settings.BufferKbits = (int?)root["bufferKbits"] ?? 0;
            if ((string?)root["pixelFormat"] is { Length: > 0 } pixelFormat) settings.PixelFormat = RecorderArguments.StoredPixelFormat(pixelFormat);
            settings.ColorSpace = (string?)root["colorSpace"];
            settings.ColorRange = (string?)root["colorRange"];
            settings.HdrToSdr = (bool?)root["hdrToSdr"] ?? false;
            settings.MaxDurationSeconds = (double?)root["maxDurationSeconds"] ?? 0;
            settings.SplitSeconds = (double?)root["splitSeconds"] ?? 0;
            settings.SplitMegabytes = (double?)root["splitMegabytes"] ?? 0;
            if (Enum.TryParse<AudioTrackLayout>((string?)root["audioLayout"], true, out var layout)) settings.AudioLayout = layout;
            settings.AudioGainDb = (double?)root["audioGainDb"] ?? 0;
            settings.AudioNoiseGate = (bool?)root["audioNoiseGate"] ?? false;
            settings.AudioNoiseSuppression = (bool?)root["audioNoiseSuppression"] ?? false;
            settings.AudioCompressor = (bool?)root["audioCompressor"] ?? false;
            settings.AudioLimiter = (bool?)root["audioLimiter"] ?? false;
            settings.AudioCodec = (string?)root["audioCodec"] == RecorderArguments.OpusAudioCodec
                ? RecorderArguments.OpusAudioCodec
                : RecorderArguments.DefaultAudioCodec;
            if (root["privacyMasks"] is JsonArray masks)
                foreach (var mask in masks.OfType<JsonObject>())
                    if ((int?)mask["x"] is { } mx && (int?)mask["y"] is { } my
                        && (int?)mask["width"] is { } mw && mw > 0 && (int?)mask["height"] is { } mh && mh > 0)
                        settings.PrivacyMasks.Add(new RecorderRegion(mx, my, mw, mh));
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
            File.WriteAllBytes(temp, ToJson());
            File.Move(temp, file, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal byte[] ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            {
                writer.WriteStartObject();
                if (OutputFolder is null) writer.WriteNull("outputFolder");
                else writer.WriteString("outputFolder", OutputFolder);
                writer.WriteBoolean("manualMode", ManualMode);
                writer.WriteBoolean("advancedMode", AdvancedMode);
                writer.WriteNumber("countdownSeconds", CountdownSeconds);
                writer.WriteString("scheduleRepeat", ScheduleRepeat.ToString());
                writer.WriteString("scheduleAt", ScheduleAt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
                if (TargetSeconds is { } ts) writer.WriteNumber("targetSeconds", ts);
                else writer.WriteNull("targetSeconds");
                if (TargetMegabytes is { } tm) writer.WriteNumber("targetMegabytes", tm);
                else writer.WriteNull("targetMegabytes");
                writer.WriteNumber("fps", Fps);
                writer.WriteString("codec", Codec);
                writer.WriteString("preset", Preset);
                writer.WriteNumber("quality", Quality);
                writer.WriteBoolean("showCursor", ShowCursor);
                writer.WriteBoolean("openFolderWhenDone", OpenFolderWhenDone);
                writer.WriteBoolean("showResultCard", ShowResultCard);
                writer.WriteBoolean("pushToTalk", PushToTalk);
                writer.WriteString("finishAction", FinishAction.ToString());
                writer.WriteBoolean("showClicks", ShowClicks);
                writer.WriteBoolean("clickSound", ClickSound);
                writer.WriteBoolean("showKeys", ShowKeys);
                writer.WriteBoolean("keysShortcutsOnly", KeysShortcutsOnly);
                writer.WriteString("keyCaptionPlace", KeyCaption.Place.ToString());
                writer.WriteString("keyCaptionSize", KeyCaption.Size.ToString());
                writer.WriteString("keyCaptionHold", KeyCaption.Hold.ToString());
                writer.WriteBoolean("showMagnifier", ShowMagnifier);
                writer.WriteBoolean("cursorHighlight", CursorHighlight);
                writer.WriteBoolean("spotlight", Spotlight);
                writer.WriteString("pointerSize", PointerSize.ToString());
                writer.WriteString("highlightColor", HighlightColor.ToString());
                writer.WriteString("leftClickColor", LeftClickColor.ToString());
                writer.WriteString("rightClickColor", RightClickColor.ToString());
                writer.WriteBoolean("livePreview", LivePreview);
                writer.WriteNumber("replaySeconds", ReplaySeconds);
                writer.WriteString("hotkeys", Hotkeys);
                writer.WriteString("target", Target.ToString());
                if (WindowTitle is null) writer.WriteNull("windowTitle");
                else writer.WriteString("windowTitle", WindowTitle);
                writer.WriteNumber("regionX", RegionX);
                writer.WriteNumber("regionY", RegionY);
                writer.WriteNumber("regionWidth", RegionWidth);
                writer.WriteNumber("regionHeight", RegionHeight);
                writer.WriteString("regionAspect", RegionAspect);
                if (MicrophoneName is null) writer.WriteNull("microphoneName");
                else writer.WriteString("microphoneName", MicrophoneName);
                if (SystemAudioName is null) writer.WriteNull("systemAudioName");
                else writer.WriteString("systemAudioName", SystemAudioName);
                if (WebcamName is null) writer.WriteNull("webcamName");
                else writer.WriteString("webcamName", WebcamName);
                writer.WriteString("webcamShape", WebcamShape.ToString());
                writer.WriteBoolean("webcamMirror", WebcamMirror);
                writer.WriteNumber("webcamX", WebcamX);
                writer.WriteNumber("webcamY", WebcamY);
                writer.WriteNumber("webcamSize", WebcamSize);
                writer.WriteString("webcamBackground", WebcamBackground.ToString());
                if (WizardContent is { } wizardContent) writer.WriteString("wizardContent", wizardContent.ToString());
                else writer.WriteNull("wizardContent");
                if (WizardAudio is { } wizardAudio) writer.WriteString("wizardAudio", wizardAudio.ToString());
                else writer.WriteNull("wizardAudio");
                if (WizardDestination is { } wizardDestination) writer.WriteString("wizardDestination", wizardDestination.ToString());
                else writer.WriteNull("wizardDestination");
                writer.WriteString("containerFormat", Container.ToString());
                writer.WriteNumber("screenIndex", ScreenIndex);
                writer.WriteNumber("scaleWidth", ScaleWidth);
                writer.WriteNumber("scaleHeight", ScaleHeight);
                writer.WriteNumber("keyframeSeconds", KeyframeSeconds);
                if (Profile is null) writer.WriteNull("profile");
                else writer.WriteString("profile", Profile);
                if (Tune is null) writer.WriteNull("tune");
                else writer.WriteString("tune", Tune);
                writer.WriteString("rateControl", RateControl.ToString());
                writer.WriteString("frameRateMode", FrameRateMode.ToString());
                writer.WriteNumber("bitrateKbps", BitrateKbps);
                writer.WriteNumber("maxBitrateKbps", MaxBitrateKbps);
                writer.WriteNumber("bufferKbits", BufferKbits);
                writer.WriteString("pixelFormat", PixelFormat);
                if (ColorSpace is null) writer.WriteNull("colorSpace");
                else writer.WriteString("colorSpace", ColorSpace);
                if (ColorRange is null) writer.WriteNull("colorRange");
                else writer.WriteString("colorRange", ColorRange);
                writer.WriteBoolean("hdrToSdr", HdrToSdr);
                writer.WriteNumber("maxDurationSeconds", MaxDurationSeconds);
                writer.WriteNumber("splitSeconds", SplitSeconds);
                writer.WriteNumber("splitMegabytes", SplitMegabytes);
                writer.WriteString("audioLayout", AudioLayout.ToString());
                writer.WriteNumber("audioGainDb", AudioGainDb);
                writer.WriteBoolean("audioNoiseGate", AudioNoiseGate);
                writer.WriteStartArray("privacyMasks");
                foreach (var mask in PrivacyMasks)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("x", mask.X);
                    writer.WriteNumber("y", mask.Y);
                    writer.WriteNumber("width", mask.Width);
                    writer.WriteNumber("height", mask.Height);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteBoolean("audioNoiseSuppression", AudioNoiseSuppression);
                writer.WriteBoolean("audioCompressor", AudioCompressor);
                writer.WriteBoolean("audioLimiter", AudioLimiter);
                writer.WriteString("audioCodec", AudioCodec);
                writer.WriteEndObject();
            }
        }

        return stream.ToArray();
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
        var stem = "kayit_" + Bicim.DosyaDamgasi(now);
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
        var stem = "kare_" + Bicim.DosyaDamgasi(now);
        var candidate = Path.Combine(folder, stem + ".png");
        for (var index = 2; File.Exists(candidate); index++)
            candidate = Path.Combine(folder, stem + "_" + index.ToString(CultureInfo.InvariantCulture) + ".png");
        return candidate;
    }
}
