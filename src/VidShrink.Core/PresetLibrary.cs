using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace VidShrink.Core;

public enum PresetKind { General, Platform, Device, User }

public enum PresetSourceStatus { Code, Official }

public sealed record PresetSource
{
    public PresetSourceStatus Status { get; init; } = PresetSourceStatus.Code;
    public string? Url { get; init; }
    public string? Checked { get; init; }
}

public sealed record PresetProfile
{
    public string Id { get; init; } = string.Empty;
    public string? Name { get; init; }
    public PresetKind Kind { get; init; } = PresetKind.User;
    public string? Chip { get; init; }
    public double? TargetMb { get; init; }
    public bool SizeCapped { get; init; } = true;
    public Intent Intent { get; init; } = Intent.Sharing;
    public CodecPreference Codec { get; init; } = CodecPreference.Compatible;
    public FillPolicy Fill { get; init; } = FillPolicy.FillTarget;
    public int? MaxShortEdge { get; init; }
    public int? AudioKbps { get; init; }
    public OutputContainer? Container { get; init; }

    /// <summary>
    /// Profilin kilitledigi kodlayici (<see cref="PlanOptions.LockedCodec"/>). Bugun yalniz
    /// HandBrake'in VP9/WebM on ayarindan <c>libvpx-vp9</c> gelir; bos kalirsa motor secer.
    /// </summary>
    public string? LockedCodec { get; init; }
    public PresetSource Source { get; init; } = new();

    public PlanOptions ToPlanOptions(double fallbackTargetMb = 25) => new()
    {
        TargetMb = TargetMb ?? fallbackTargetMb,
        Intent = Intent,
        Codec = Codec,
        FillPolicy = Fill,
        FixedResolution = MaxShortEdge,
        LockedAudioKbps = AudioKbps,
        LockedCodec = LockedCodec
    };
}

public enum PresetFileError
{
    NotJson,
    NotPresetFile,
    HandBrakeFile,
    SchemaMissing,
    SchemaTooNew,
    InvalidValue,
    ReservedId,
    NoHandBrakePreset
}

public sealed class PresetFileException : Exception
{
    public PresetFileException(PresetFileError error, string detail, Exception? inner = null)
        : base($"{error}: {detail}", inner)
    {
        Error = error;
        Detail = detail;
    }

    public PresetFileError Error { get; }
    public string Detail { get; }
    public string LocaleKey => PresetLibrary.ErrorKey(Error);
}

public sealed class PresetLibrary
{
    public const int SchemaVersion = 1;
    public const string FileKind = "vidshrink-presets";
    public const string UserFileName = "presets.json";
    internal const string ResourceName = "VidShrink.Core.Presets.platformlar.json";

    private static readonly Lazy<PresetLibrary> Embedded = new(LoadEmbedded);

    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public PresetLibrary(IReadOnlyList<PresetProfile> profiles) => Profiles = profiles;

    public IReadOnlyList<PresetProfile> Profiles { get; }

    public static PresetLibrary BuiltIn => Embedded.Value;

    public PresetProfile? Find(string id) =>
        Profiles.FirstOrDefault(profile => string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<PresetProfile> OfKind(PresetKind kind) => Profiles.Where(profile => profile.Kind == kind);

    public IEnumerable<PresetProfile> Chips() => Profiles.Where(profile => profile.Chip is not null);

    public static string ErrorKey(PresetFileError error) => error switch
    {
        PresetFileError.NotJson => "main.preset.error.not-json",
        PresetFileError.NotPresetFile => "main.preset.error.not-preset-file",
        PresetFileError.HandBrakeFile => "main.preset.error.hand-brake-file",
        PresetFileError.SchemaMissing => "main.preset.error.schema-missing",
        PresetFileError.SchemaTooNew => "main.preset.error.schema-too-new",
        PresetFileError.InvalidValue => "main.preset.error.invalid-value",
        PresetFileError.ReservedId => "main.preset.error.reserved-id",
        PresetFileError.NoHandBrakePreset => "main.preset.error.no-hand-brake-preset",
        _ => throw new ArgumentOutOfRangeException(nameof(error))
    };

    public static string DefaultUserPath
    {
        get
        {
            var settings = UpdateSettings.DefaultPath;
            var folder = Path.GetDirectoryName(settings);
            return Path.Combine(string.IsNullOrEmpty(folder) ? "." : folder, UserFileName);
        }
    }

    public static IReadOnlyList<PresetProfile> Parse(string json)
    {
        var documentOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        JsonNode? root;
        try
        {
            using (var document = JsonDocument.Parse(json, documentOptions))
            {
                if (HandBrakePresetImport.LooksLikeHandBrake(document.RootElement))
                    throw new PresetFileException(PresetFileError.HandBrakeFile, "PresetList");
            }

            root = JsonNode.Parse(json, documentOptions: documentOptions);
            if (root is JsonObject probe) _ = probe.Count;
        }
        catch (JsonException ex)
        {
            throw new PresetFileException(PresetFileError.NotJson, $"line {ex.LineNumber + 1}", ex);
        }
        catch (ArgumentException ex)
        {
            throw new PresetFileException(PresetFileError.NotJson, "duplicate key", ex);
        }

        if (root is not JsonObject obj || obj["presets"] is not JsonArray)
            throw new PresetFileException(PresetFileError.NotPresetFile, "presets");

        if (obj["kind"] is JsonValue kindValue && kindValue.TryGetValue<string>(out var kind) &&
            !string.Equals(kind, FileKind, StringComparison.Ordinal))
            throw new PresetFileException(PresetFileError.NotPresetFile, kind);

        if (obj["schema"] is not JsonValue schemaValue || !schemaValue.TryGetValue<int>(out var schema))
            throw new PresetFileException(PresetFileError.SchemaMissing, "schema");

        if (schema > SchemaVersion)
            throw new PresetFileException(PresetFileError.SchemaTooNew, schema.ToString(CultureInfo.InvariantCulture));

        var list = new List<PresetProfile>();
        var items = (JsonArray)obj["presets"]!;
        for (var i = 0; i < items.Count; i++)
        {
            PresetProfile? profile;
            try
            {
                profile = items[i].Deserialize<PresetProfile>(Options);
            }
            catch (JsonException ex)
            {
                throw new PresetFileException(PresetFileError.InvalidValue, $"presets[{i}]{ex.Path?.TrimStart('$')}", ex);
            }
            catch (InvalidOperationException ex)
            {
                throw new PresetFileException(PresetFileError.InvalidValue, $"presets[{i}]", ex);
            }

            if (profile is null)
                throw new PresetFileException(PresetFileError.InvalidValue, $"presets[{i}]");

            Validate(profile, i);
            list.Add(profile);
        }

        return list;
    }

    public static string Serialize(IEnumerable<PresetProfile> profiles)
    {
        var root = new JsonObject
        {
            ["schema"] = SchemaVersion,
            ["kind"] = FileKind,
            ["presets"] = JsonSerializer.SerializeToNode(profiles.ToList(), Options)
        };
        return root.ToJsonString(Options);
    }

    public static IReadOnlyList<PresetProfile> Import(string path)
    {
        var profiles = Parse(File.ReadAllText(path));
        foreach (var profile in profiles)
            RejectReserved(profile);
        return profiles.Select(profile => profile with { Kind = PresetKind.User, Chip = null }).ToList();
    }

    public static void Export(IEnumerable<PresetProfile> profiles, string path)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        File.WriteAllText(path, Serialize(profiles), new UTF8Encoding(false));
    }

    public static IReadOnlyList<PresetProfile> LoadUser(string? path = null)
    {
        path ??= DefaultUserPath;
        return File.Exists(path) ? Parse(File.ReadAllText(path)) : Array.Empty<PresetProfile>();
    }

    public static IReadOnlyList<PresetProfile> SaveUser(PresetProfile profile, string? path = null)
    {
        path ??= DefaultUserPath;
        RejectReserved(profile);
        Validate(profile, 0);
        var stored = profile with { Kind = PresetKind.User, Chip = null };
        var list = LoadUser(path)
            .Where(existing => !string.Equals(existing.Id, stored.Id, StringComparison.OrdinalIgnoreCase))
            .Append(stored)
            .ToList();
        Export(list, path);
        return list;
    }

    private static void RejectReserved(PresetProfile profile)
    {
        if (BuiltIn.Find(profile.Id) is not null)
            throw new PresetFileException(PresetFileError.ReservedId, profile.Id);
    }

    private static void Validate(PresetProfile profile, int index)
    {
        if (string.IsNullOrWhiteSpace(profile.Id))
            throw new PresetFileException(PresetFileError.InvalidValue, $"presets[{index}].id");
        if (profile.TargetMb is { } mb && !(mb > 0 && double.IsFinite(mb)))
            throw new PresetFileException(PresetFileError.InvalidValue, $"presets[{index}].targetMb");
        if (profile.MaxShortEdge is { } edge && (edge < 2 || edge % 2 != 0))
            throw new PresetFileException(PresetFileError.InvalidValue, $"presets[{index}].maxShortEdge");
        if (profile.AudioKbps is { } kbps && kbps <= 0)
            throw new PresetFileException(PresetFileError.InvalidValue, $"presets[{index}].audioKbps");
        if (profile.LockedCodec is not null && !PlanCalculator.IsLockableCodec(profile.LockedCodec))
            throw new PresetFileException(PresetFileError.InvalidValue, $"presets[{index}].lockedCodec");
        if (profile.Source.Status == PresetSourceStatus.Official && !Uri.TryCreate(profile.Source.Url, UriKind.Absolute, out _))
            throw new PresetFileException(PresetFileError.InvalidValue, $"presets[{index}].source.url");
    }

    private static PresetLibrary LoadEmbedded()
    {
        using var stream = typeof(PresetLibrary).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(ResourceName + " is not embedded.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return new PresetLibrary(Parse(reader.ReadToEnd()));
    }

    /// <summary>
    /// Profilin kabından çıktı uzantısı. Kodlama kabı uzantıdan okuduğu için
    /// (<see cref="StreamMapping.ForOutput"/>) kabı uygulamak uzantıyı seçmek demek. WebM için
    /// <c>null</c> döner ve plan kendi kabını seçer: WebM'i kap değil kodek taşır, libvpx-vp9
    /// kilitli plan <see cref="StreamMapping.ContainerFor(StreamRequest, string?)"/> ile webm'e
    /// gider, kilitsiz profil ise mp4'te kalır. Karar: <c>docs/danisma/010-fable-onayar-kap.md</c>.
    /// </summary>
    public static string? DeliveredExtension(OutputContainer? container) =>
        DeliveredContainer(container) is { } kap ? StreamMapping.ExtensionOf(kap) : null;

    /// <summary>
    /// Profilin teslim ettiği kap; <see cref="DeliveredExtension"/> ile aynı kural. Plan bunu
    /// <see cref="PlanOptions.DeliveredContainer"/> olarak alır, yoksa akış kararları ve yan bütçe
    /// planın kendi kabına göre kurulur ve kodlama anında sessizce yeniden hesaplanırdı.
    /// </summary>
    public static OutputContainer? DeliveredContainer(OutputContainer? container) =>
        container is { } kap and not OutputContainer.WebM ? kap : null;
}

public enum PresetNoteOutcome { Carried, Approximated, Dropped }

public enum PresetNoteReason
{
    None,
    SizeFromName,
    EngineDecides,
    NoEquivalent,
    UnsupportedCodec,
    Structural
}

public sealed record PresetFieldNote(string Field, PresetNoteOutcome Outcome, PresetNoteReason Reason, string? Value)
{
    public string LocaleKey => Outcome switch
    {
        PresetNoteOutcome.Carried => "main.preset.handbrake.carried.none",
        PresetNoteOutcome.Approximated => Reason switch
        {
            PresetNoteReason.SizeFromName => "main.preset.handbrake.approximated.size-from-name",
            PresetNoteReason.None => "main.preset.handbrake.approximated.none",
            _ => "main.preset.handbrake.approximated.none"
        },
        PresetNoteOutcome.Dropped => Reason switch
        {
            PresetNoteReason.EngineDecides => "main.preset.handbrake.dropped.engine-decides",
            PresetNoteReason.UnsupportedCodec => "main.preset.handbrake.dropped.unsupported-codec",
            PresetNoteReason.Structural => "main.preset.handbrake.dropped.structural",
            PresetNoteReason.NoEquivalent => "main.preset.handbrake.dropped.no-equivalent",
            _ => "main.preset.handbrake.dropped.no-equivalent"
        },
        _ => throw new ArgumentOutOfRangeException(nameof(Outcome))
    };
}

public sealed record HandBrakeTranslation(string PresetName, PresetProfile Profile, IReadOnlyList<PresetFieldNote> Notes)
{
    public IEnumerable<PresetFieldNote> With(PresetNoteOutcome outcome) => Notes.Where(note => note.Outcome == outcome);

    public PresetFieldNote? Note(string field) =>
        Notes.FirstOrDefault(note => string.Equals(note.Field, field, StringComparison.Ordinal));
}

public static class HandBrakePresetImport
{
    private static readonly Regex SizeInName = new(@"(?<mb>\d+(?:\.\d+)?)\s*MB\b", RegexOptions.CultureInvariant);

    private static readonly HashSet<string> StructuralFields = new(StringComparer.Ordinal)
    {
        "ChildrenArray", "Default", "Folder", "FolderOpen", "Type"
    };

    internal static bool LooksLikeHandBrake(JsonElement root) => root.ValueKind switch
    {
        JsonValueKind.Object => root.TryGetProperty("PresetList", out _) || root.TryGetProperty("PresetName", out _),
        JsonValueKind.Array => root.EnumerateArray().Any(item =>
            item.ValueKind == JsonValueKind.Object && item.TryGetProperty("PresetName", out _)),
        _ => false
    };

    public static IReadOnlyList<HandBrakeTranslation> TranslateFile(string path) => Translate(File.ReadAllText(path));

    public static IReadOnlyList<HandBrakeTranslation> Translate(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        }
        catch (JsonException ex)
        {
            throw new PresetFileException(PresetFileError.NotJson, $"line {ex.LineNumber + 1}", ex);
        }

        using (document)
        {
            var presets = new List<JsonElement>();
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("PresetList", out var list))
                Collect(list, presets);
            else
                Collect(root, presets);

            if (presets.Count == 0)
                throw new PresetFileException(PresetFileError.NoHandBrakePreset, "PresetName");

            return presets.Select(TranslateOne).ToList();
        }
    }

    private static void Collect(JsonElement element, List<JsonElement> sink)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) Collect(item, sink);
                break;
            case JsonValueKind.Object:
                var isFolder = element.TryGetProperty("Folder", out var folder) && folder.ValueKind == JsonValueKind.True;
                if (element.TryGetProperty("ChildrenArray", out var children) && children.ValueKind == JsonValueKind.Array)
                    Collect(children, sink);
                if (!isFolder && element.TryGetProperty("PresetName", out var name) && name.ValueKind == JsonValueKind.String)
                    sink.Add(element.Clone());
                break;
        }
    }

    private static HandBrakeTranslation TranslateOne(JsonElement preset)
    {
        var notes = new List<PresetFieldNote>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var name = preset.GetProperty("PresetName").GetString() ?? string.Empty;

        double? targetMb = null;
        var sizeMatch = SizeInName.Match(name);
        if (sizeMatch.Success &&
            double.TryParse(sizeMatch.Groups["mb"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mb) && mb > 0)
            targetMb = mb;

        var codec = CodecPreference.Auto;
        int? shortEdge = null;
        int? audioKbps = null;
        OutputContainer? container = null;
        string? lockedCodec = null;
        var encoderNamed = false;
        int? webmNote = null;
        var qualityMode = false;

        foreach (var property in preset.EnumerateObject())
        {
            if (!seen.Add(property.Name)) continue;
            var value = property.Value;
            var raw = value.ValueKind is JsonValueKind.Object or JsonValueKind.Array ? null : value.ToString();

            switch (property.Name)
            {
                case "PresetName":
                    notes.Add(new(property.Name, PresetNoteOutcome.Carried, PresetNoteReason.None, raw));
                    if (targetMb is { } size)
                        notes.Add(new("PresetName:MB", PresetNoteOutcome.Approximated, PresetNoteReason.SizeFromName,
                            size.ToString(CultureInfo.InvariantCulture)));
                    break;
                case "VideoEncoder":
                    var family = EncoderFamily(raw);
                    if (IsVp9Encoder(raw))
                    {
                        lockedCodec = "libvpx-vp9";
                        encoderNamed = true;
                        notes.Add(new(property.Name,
                            raw!.Equals("vp9", StringComparison.OrdinalIgnoreCase) ? PresetNoteOutcome.Carried : PresetNoteOutcome.Approximated,
                            PresetNoteReason.None, raw));
                    }
                    else if (family is { } known)
                    {
                        codec = known;
                        encoderNamed = true;
                        notes.Add(new(property.Name, PresetNoteOutcome.Approximated, PresetNoteReason.None, raw));
                    }
                    else
                    {
                        notes.Add(new(property.Name, PresetNoteOutcome.Dropped, PresetNoteReason.UnsupportedCodec, raw));
                    }
                    break;
                case "PictureWidth":
                case "PictureHeight":
                    break;
                case "FileFormat":
                    container = raw?.ToLowerInvariant() switch
                    {
                        "mp4" or "av_mp4" => OutputContainer.Mp4,
                        "mkv" or "av_mkv" => OutputContainer.Mkv,
                        "webm" or "av_webm" => OutputContainer.WebM,
                        "mov" or "av_mov" => OutputContainer.Mov,
                        _ => null
                    };
                    if (container == OutputContainer.WebM) webmNote = notes.Count;
                    notes.Add(container is null
                        ? new(property.Name, PresetNoteOutcome.Dropped, PresetNoteReason.NoEquivalent, raw)
                        : new(property.Name, PresetNoteOutcome.Carried, PresetNoteReason.None, raw));
                    break;
                case "AudioList":
                    var first = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().FirstOrDefault() : default;
                    if (first.ValueKind == JsonValueKind.Object &&
                        first.TryGetProperty("AudioBitrate", out var bitrate) &&
                        bitrate.TryGetInt32(out var kbps) && kbps > 0)
                    {
                        audioKbps = kbps;
                        notes.Add(new(property.Name, PresetNoteOutcome.Approximated, PresetNoteReason.None,
                            kbps.ToString(CultureInfo.InvariantCulture)));
                    }
                    else
                    {
                        notes.Add(new(property.Name, PresetNoteOutcome.Dropped, PresetNoteReason.NoEquivalent, null));
                    }
                    break;
                case "VideoQualityType":
                    qualityMode = value.TryGetInt32(out var type) && type == 2;
                    notes.Add(new(property.Name, PresetNoteOutcome.Approximated, PresetNoteReason.None, raw));
                    break;
                case "VideoAvgBitrate":
                case "VideoQualitySlider":
                case "VideoMultiPass":
                case "VideoTurboMultiPass":
                case "VideoPreset":
                case "VideoFramerate":
                case "VideoFramerateMode":
                    notes.Add(new(property.Name, PresetNoteOutcome.Dropped, PresetNoteReason.EngineDecides, raw));
                    break;
                default:
                    notes.Add(new(property.Name,
                        PresetNoteOutcome.Dropped,
                        StructuralFields.Contains(property.Name) ? PresetNoteReason.Structural : PresetNoteReason.NoEquivalent,
                        raw));
                    break;
            }
        }

        var width = IntOf(preset, "PictureWidth");
        var height = IntOf(preset, "PictureHeight");
        if (width is > 0 && height is > 0)
        {
            var edge = Math.Min(width.Value, height.Value);
            shortEdge = edge - edge % 2;
            notes.Add(new("PictureWidth", PresetNoteOutcome.Carried, PresetNoteReason.None, width.Value.ToString(CultureInfo.InvariantCulture)));
            notes.Add(new("PictureHeight", PresetNoteOutcome.Carried, PresetNoteReason.None, height.Value.ToString(CultureInfo.InvariantCulture)));
        }
        else
        {
            if (preset.TryGetProperty("PictureWidth", out _))
                notes.Add(new("PictureWidth", PresetNoteOutcome.Dropped, PresetNoteReason.NoEquivalent, width?.ToString(CultureInfo.InvariantCulture)));
            if (preset.TryGetProperty("PictureHeight", out _))
                notes.Add(new("PictureHeight", PresetNoteOutcome.Dropped, PresetNoteReason.NoEquivalent, height?.ToString(CultureInfo.InvariantCulture)));
        }

        if (webmNote is { } at)
        {
            if (lockedCodec is null && !encoderNamed) lockedCodec = "libvpx-vp9";
            if (lockedCodec is null)
                notes[at] = notes[at] with { Outcome = PresetNoteOutcome.Approximated, Reason = PresetNoteReason.NoEquivalent };
        }

        var sizeCapped = targetMb is not null && !qualityMode;
        var profile = new PresetProfile
        {
            Id = "hb-" + Slug(name),
            Name = name,
            Kind = PresetKind.User,
            TargetMb = sizeCapped ? targetMb : null,
            SizeCapped = sizeCapped,
            Intent = Intent.Sharing,
            Codec = codec,
            Fill = sizeCapped ? FillPolicy.FillTarget : FillPolicy.QualityCeiling,
            MaxShortEdge = shortEdge,
            AudioKbps = audioKbps,
            Container = container,
            LockedCodec = lockedCodec
        };

        return new HandBrakeTranslation(name, profile, notes);
    }

    private static int? IntOf(JsonElement preset, string field) =>
        preset.TryGetProperty(field, out var value) && value.TryGetInt32(out var number) ? number : null;

    /// <summary>
    /// HandBrake'in VP9 kodlayicilari: <c>vp9</c> birebir tasinir, <c>vp9_10bit</c> gibi
    /// turevler 8 bit libvpx-vp9'a yaklasir.
    /// </summary>
    private static bool IsVp9Encoder(string? encoder)
        => encoder is not null && encoder.Contains("vp9", StringComparison.OrdinalIgnoreCase);

    private static CodecPreference? EncoderFamily(string? encoder)
    {
        var name = encoder?.ToLowerInvariant() ?? string.Empty;
        if (name.Contains("264")) return CodecPreference.Compatible;
        if (name.Contains("265") || name.Contains("hevc")) return CodecPreference.Auto;
        if (name.Contains("av1")) return CodecPreference.MaxCompression;
        return null;
    }

    private static string Slug(string name)
    {
        var builder = new StringBuilder();
        foreach (var ch in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch)) builder.Append(ch);
            else if (builder.Length > 0 && builder[^1] != '-') builder.Append('-');
        }
        return builder.ToString().Trim('-');
    }
}
