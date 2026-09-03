using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VidShrink.App.Localization;

namespace VidShrink.Tests;

public sealed class LocalizationTests : IDisposable
{
    private static readonly Regex KeyShape = new(@"^[a-z0-9]+([.\-][a-z0-9]+)*$", RegexOptions.Compiled);

    private readonly string sandbox;

    public LocalizationTests()
    {
        Strings.Reset();
        sandbox = Path.Combine(TestPaths.OutputRoot, "localization", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
    }

    public void Dispose()
    {
        Strings.Reset();
        try
        {
            Directory.Delete(sandbox, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void SevkiyattakiDillerinAnahtarKumesiIngilizceyleBirebirAyni()
    {
        var reference = new SortedSet<string>(Strings.KeysOf(Strings.FallbackLanguage), StringComparer.Ordinal);
        var complaints = new StringBuilder();

        foreach (var language in Strings.Languages)
        {
            if (string.Equals(language, Strings.FallbackLanguage, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var keys = new SortedSet<string>(Strings.KeysOf(language), StringComparer.Ordinal);

            foreach (var missing in reference.Except(keys, StringComparer.Ordinal))
            {
                complaints.AppendLine($"'{missing}' anahtarı '{language}' dilinde eksik.");
            }

            foreach (var extra in keys.Except(reference, StringComparer.Ordinal))
            {
                complaints.AppendLine($"'{extra}' anahtarı '{language}' dilinde var ama '{Strings.FallbackLanguage}' dilinde yok.");
            }
        }

        Assert.True(complaints.Length == 0, complaints.ToString());
    }

    [Fact]
    public void SevkiyatDosyalariDuzSozlukVeAnahtarlarNoktaAyrilmisKucukHarf()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Locales");
        Assert.True(Directory.Exists(root), $"Locales klasörü çıktıya kopyalanmamış: {root}");

        var files = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var texts = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
            Assert.NotNull(texts);

            foreach (var key in texts!.Keys)
            {
                Assert.True(KeyShape.IsMatch(key), $"'{key}' anahtarı ({file}) nokta.ayrilmis.kucuk-harf biçiminde değil.");
            }
        }
    }

    /// <summary>
    /// Aynı dilde iki alan dosyası aynı anahtarı taşırsa <see cref="Strings"/> onu sessizce
    /// üstüne yazar: bir metin hiç görünmeden kaybolur ve anahtar sayısı ölçümü de
    /// eşitliği koruduğu için yeşil kalır. Çakışma bu yüzden ayrıca aranıyor.
    /// </summary>
    [Fact]
    public void AyniDildeIkiAlanDosyasiAyniAnahtariTasimiyor()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Locales");
        var complaints = new StringBuilder();

        foreach (var folder in Directory.GetDirectories(root))
        {
            var owner = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var file in Directory.GetFiles(folder, "*.json").OrderBy(path => path, StringComparer.Ordinal))
            {
                var domain = Path.GetFileNameWithoutExtension(file);
                var texts = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
                Assert.NotNull(texts);

                foreach (var key in texts!.Keys)
                {
                    if (owner.TryGetValue(key, out var first))
                    {
                        complaints.AppendLine(
                            $"'{key}' hem {first}.json hem {domain}.json içinde ({Path.GetFileName(folder)}).");
                        continue;
                    }

                    owner[key] = domain;
                }
            }
        }

        Assert.True(complaints.Length == 0, "Aynı dilde anahtar çakışması:\n" + complaints);
    }

    [Fact]
    public void DortAlanDosyasiHerDilIcinCiktidaVar()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Locales");

        foreach (var language in new[] { "en", "tr" })
        {
            foreach (var domain in new[] { "main", "playback", "performance", "settings" })
            {
                var file = Path.Combine(root, language, domain + ".json");
                Assert.True(File.Exists(file), $"eksik: {file}");
            }
        }
    }

    [Fact]
    public void DillerKlasorlerdenOkunurSahteDilGorulur()
    {
        Assert.Contains("en", Strings.Languages, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("tr", Strings.Languages, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("zz", Strings.Languages, StringComparer.OrdinalIgnoreCase);

        Write("zz", "main", new Dictionary<string, string> { ["olcum.baslik"] = "Zz" });
        Strings.UseRoot(sandbox);

        Assert.Contains("zz", Strings.Languages, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void YururlukteDiliVarsaOnunMetniDoner()
    {
        Sandbox();
        Strings.Use("tr");

        Assert.Equal("Türkçe metin", Strings.Get("olcum.metin"));
    }

    [Fact]
    public void YururlukteDildeYoksaIngilizceDoner()
    {
        Sandbox();
        Strings.Use("tr");

        Assert.Equal("English only", Strings.Get("olcum.yalniz-ingilizce"));
    }

    [Fact]
    public void IkisindeDeYoksaAnahtarinKendisiDoner()
    {
        Sandbox();
        Strings.AssertOnMissingKey = false;
        Strings.Use("tr");

        Assert.Equal("olcum.hicbir-yerde", Strings.Get("olcum.hicbir-yerde"));
    }

    [Fact]
    public void BicimParametreleriUygulanir()
    {
        Sandbox();
        Strings.Use("tr");

        Assert.Equal("2 dosya, 5 saniye", Strings.Get("olcum.bicim", 2, 5));
    }

    [Fact]
    public void DilDegisinceChangedTetiklenir()
    {
        var count = 0;
        void Handler(object? sender, EventArgs e) => count++;

        Strings.Changed += Handler;
        try
        {
            Strings.Use("tr");
            Strings.Use("tr");
            Strings.Use("en");
        }
        finally
        {
            Strings.Changed -= Handler;
        }

        Assert.Equal(2, count);
        Assert.Equal("en", Strings.Language);
    }


    [Fact]
    public void KodunCagirdigiHerAnahtarIkiKatalogdaDaVar()
    {
        var scan = Measure();
        var english = Catalog(Strings.FallbackLanguage);
        var turkish = Catalog("tr");
        var complaints = new StringBuilder();

        foreach (var group in scan.Sites
                     .Where(site => site.Key is not null)
                     .GroupBy(site => site.Key!, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var missing = new List<string>();
            if (!english.Contains(group.Key)) missing.Add(Strings.FallbackLanguage);
            if (!turkish.Contains(group.Key)) missing.Add("tr");
            if (missing.Count == 0) continue;

            complaints.AppendLine(
                $"'{group.Key}' anahtarı {string.Join(" ve ", missing)} kataloğunda yok; " +
                $"{group.Count()} çağrı yeri, ilki {group.First().Caller} -> {group.First().Sink}.");
        }

        Assert.True(complaints.Length == 0, "Çağrılan ama olmayan anahtar:\n" + complaints);
    }

    [Fact]
    public void CagriYerineBaglanamayanAnahtarBicimliDizelerDeKatalogdaVar()
    {
        var scan = Measure();
        var attached = new HashSet<string>(
            scan.Sites.Where(site => site.Key is not null).Select(site => site.Key!),
            StringComparer.Ordinal);

        var english = Catalog(Strings.FallbackLanguage);
        var turkish = Catalog("tr");
        var domains = Domains(english);
        var complaints = new StringBuilder();

        foreach (var text in scan.Literals
                     .Where(text => LooksLikeKey(text, domains))
                     .Where(text => !attached.Contains(text))
                     .OrderBy(text => text, StringComparer.Ordinal))
        {
            var missing = new List<string>();
            if (!english.Contains(text)) missing.Add(Strings.FallbackLanguage);
            if (!turkish.Contains(text)) missing.Add("tr");
            if (missing.Count == 0) continue;

            complaints.AppendLine($"'{text}' dizesi {string.Join(" ve ", missing)} kataloğunda yok.");
        }

        Assert.True(complaints.Length == 0, "Anahtar biçimli ama karşılığı olmayan dize:\n" + complaints);
    }

    [Fact]
    public void KatalogdaBirikenOluCeviriListesiBuyumuyor()
    {
        var scan = Measure();
        var english = Catalog(Strings.FallbackLanguage);
        var domains = Domains(english);

        var seen = new HashSet<string>(
            scan.Literals.Where(text => LooksLikeKey(text, domains)),
            StringComparer.Ordinal);

        var dead = english.Where(key => !seen.Contains(key)).ToArray();
        var complaints = new StringBuilder();

        foreach (var key in dead.Except(KnownDead, StringComparer.Ordinal))
        {
            complaints.AppendLine($"'{key}' iki katalogda da var ama derlemenin hiçbir yerinde geçmiyor.");
        }

        foreach (var key in KnownDead.Except(dead, StringComparer.Ordinal))
        {
            complaints.AppendLine($"'{key}' artık ölü değil ya da katalogdan çıkmış; KnownDead listesinden düşür.");
        }

        Assert.True(complaints.Length == 0, "Ölü çeviri sayımı kaydıyla uyuşmuyor:\n" + complaints);
    }

    [Fact]
    public void AnahtarTuketenKapilarKaynaktakiBildirimlerleAyni()
    {
        var scan = Measure();
        var declared = 0;

        foreach (var file in Directory.GetFiles(
                     Path.Combine(TipSources.Root, "src", "VidShrink.App", "Localization"),
                     "*.cs"))
        {
            var source = File.ReadAllText(file);
            for (var at = source.IndexOf("string key", StringComparison.Ordinal);
                 at >= 0;
                 at = source.IndexOf("string key", at + 1, StringComparison.Ordinal))
            {
                declared++;
            }
        }

        Assert.Equal(declared, scan.SeedOverloads);
        Assert.Equal(
            new[]
            {
                "VidShrink.App.Localization.LocalizedText::.ctor",
                "VidShrink.App.Localization.LocalizedText::For",
                "VidShrink.App.Localization.Strings::Get",
                "VidShrink.App.Localization.Strings::GetIn",
                "VidShrink.App.Localization.TextExtension::.ctor"
            },
            scan.Seeds);
    }

    [Fact]
    public void OlcuKaynaktaGorunenCagriYerlerininTamaminiBuluyor()
    {
        var scan = Measure();
        var called = new HashSet<string>(
            scan.Sites.Where(site => site.Key is not null).Select(site => site.Key!),
            StringComparer.Ordinal);

        var fromSource = new SortedSet<string>(StringComparer.Ordinal);
        var markup = new Regex(@"\{loc:Text\s+([a-z0-9][a-z0-9.\-]*)\s*\}");
        var call = new Regex(@"Say\(""([a-z0-9][a-z0-9.\-]*)""");

        foreach (var file in Directory.GetFiles(
                     Path.Combine(TipSources.Root, "src", "VidShrink.App"), "*.axaml", SearchOption.AllDirectories))
        {
            foreach (Match found in markup.Matches(File.ReadAllText(file)))
            {
                fromSource.Add(found.Groups[1].Value);
            }
        }

        foreach (Match found in call.Matches(File.ReadAllText(TipSources.WindowCodePath)))
        {
            fromSource.Add(found.Groups[1].Value);
        }

        Assert.NotEmpty(fromSource);

        var lost = fromSource.Except(called, StringComparer.Ordinal).ToArray();
        Assert.True(
            lost.Length == 0,
            $"Kaynakta görünen {fromSource.Count} anahtarın {lost.Length} tanesi ölçünün çağrı yeri " +
            $"kümesinde yok; ölçü kör kalmış:\n  " + string.Join("\n  ", lost));
    }

    private static readonly string[] KnownDead = Array.Empty<string>();

    private static KeyScan Measure()
        => KeyCallSites.Scan(
            typeof(Strings).Assembly,
            text => LooksLikeKey(text, Domains(Catalog(Strings.FallbackLanguage))));

    private static SortedSet<string> Catalog(string language)
        => new(Strings.KeysOf(language), StringComparer.Ordinal);

    private static HashSet<string> Domains(IEnumerable<string> keys)
        => new(keys.Select(key => key.Split('.')[0]), StringComparer.Ordinal);

    private static bool LooksLikeKey(string text, HashSet<string> domains)
    {
        var cut = text.IndexOf('.');
        return cut > 0 && KeyShape.IsMatch(text) && domains.Contains(text[..cut]);
    }

    private void Sandbox()
    {
        Write("en", "main", new Dictionary<string, string>
        {
            ["olcum.metin"] = "English text",
            ["olcum.yalniz-ingilizce"] = "English only",
            ["olcum.bicim"] = "{0} files, {1} seconds",
        });

        Write("tr", "main", new Dictionary<string, string>
        {
            ["olcum.metin"] = "Türkçe metin",
            ["olcum.bicim"] = "{0} dosya, {1} saniye",
        });

        Strings.UseRoot(sandbox);
    }

    private void Write(string language, string domain, Dictionary<string, string> texts)
    {
        var directory = Path.Combine(sandbox, language);
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, domain + ".json"),
            JsonSerializer.Serialize(texts, new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal sealed record KeySite(string Caller, string Sink, string? Key);

internal sealed record KeyScan(
    IReadOnlyList<string> Seeds,
    int SeedOverloads,
    IReadOnlyList<string> Sinks,
    IReadOnlyList<string> Candidates,
    IReadOnlyList<KeySite> Sites,
    IReadOnlyCollection<string> Literals);

internal static class KeyCallSites
{
    internal const string LocalizationNamespace = "VidShrink.App.Localization";

    private const BindingFlags Everything =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
        BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly OpCode?[] Single = new OpCode?[256];
    private static readonly OpCode?[] Double = new OpCode?[256];

    static KeyCallSites()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode code) continue;
            var value = unchecked((ushort)code.Value);
            if (value < 0x100) Single[value] = code;
            else Double[value & 0xFF] = code;
        }
    }

    internal static KeyScan Scan(Assembly assembly, Func<string, bool> keyShape)
    {
        var seeds = new SortedSet<string>(StringComparer.Ordinal);
        var seedOverloads = 0;
        var candidates = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var type in Types(assembly))
        {
            if (type.FullName is not { } owner) continue;

            foreach (var member in type.GetMembers(Everything))
            {
                if (member is not MethodBase method) continue;

                var takesKey = method
                    .GetParameters()
                    .Any(p => p.ParameterType == typeof(string) &&
                              string.Equals(p.Name, "key", StringComparison.Ordinal));

                if (!takesKey) continue;

                var name = owner + "::" + method.Name;
                candidates.Add(name);

                if (string.Equals(type.Namespace, LocalizationNamespace, StringComparison.Ordinal))
                {
                    seeds.Add(name);
                    seedOverloads++;
                }
            }
        }

        var bodies = Bodies(assembly.Location, keyShape, out var literals);
        var sinks = Grow(seeds, candidates, bodies);
        var sites = new List<KeySite>();

        foreach (var body in bodies)
        {
            var pending = new List<string>();

            foreach (var step in body.Steps)
            {
                if (step.Literal is { } text)
                {
                    pending.Add(text);
                    continue;
                }

                if (step.Target is not { } target || !sinks.Contains(target)) continue;

                string? key = null;
                if (pending.Count > 0)
                {
                    key = pending[^1];
                    pending.RemoveAt(pending.Count - 1);
                }

                sites.Add(new KeySite(body.Name, target, key));
            }
        }

        return new KeyScan(
            seeds.ToArray(),
            seedOverloads,
            sinks.OrderBy(sink => sink, StringComparer.Ordinal).ToArray(),
            candidates.ToArray(),
            sites,
            literals);
    }

    private static HashSet<string> Grow(
        IEnumerable<string> seeds,
        IReadOnlyCollection<string> candidates,
        IReadOnlyList<Body> bodies)
    {
        var sinks = new HashSet<string>(seeds, StringComparer.Ordinal);
        var calls = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var body in bodies)
        {
            if (!calls.TryGetValue(body.Name, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                calls[body.Name] = set;
            }

            foreach (var step in body.Steps)
            {
                if (step.Target is { } target) set.Add(target);
            }
        }

        bool moved;
        do
        {
            moved = false;

            foreach (var candidate in candidates)
            {
                if (sinks.Contains(candidate)) continue;
                if (!calls.TryGetValue(candidate, out var called)) continue;
                if (!called.Any(sinks.Contains)) continue;

                sinks.Add(candidate);
                moved = true;
            }
        }
        while (moved);

        return sinks;
    }

    private sealed record Step(string? Literal, string? Target);

    private sealed record Body(string Name, IReadOnlyList<Step> Steps);

    private static IReadOnlyList<Body> Bodies(
        string assemblyPath,
        Func<string, bool> keyShape,
        out IReadOnlyCollection<string> literals)
    {
        var found = new List<Body>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        using var file = File.OpenRead(assemblyPath);
        using var pe = new PEReader(file);
        var reader = pe.GetMetadataReader();

        foreach (var handle in reader.MethodDefinitions)
        {
            var definition = reader.GetMethodDefinition(handle);
            if (definition.RelativeVirtualAddress == 0) continue;

            var il = pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes();
            if (il is null) continue;

            var steps = new List<Step>();
            var offset = 0;

            while (offset < il.Length)
            {
                var lead = il[offset++];
                OpCode? code;

                if (lead == 0xFE)
                {
                    if (offset >= il.Length) break;
                    code = Double[il[offset++]];
                }
                else
                {
                    code = Single[lead];
                }

                if (code is not { } op) break;

                var size = OperandSize(op.OperandType, il, offset);
                if (size < 0 || offset + size > il.Length) break;

                if (op.OperandType == OperandType.InlineString)
                {
                    var token = BitConverter.ToInt32(il, offset);
                    var text = reader.GetUserString(MetadataTokens.UserStringHandle(token));
                    seen.Add(text);
                    if (keyShape(text)) steps.Add(new Step(text, null));
                }
                else if (op.OperandType == OperandType.InlineMethod)
                {
                    var token = BitConverter.ToInt32(il, offset);
                    var target = TargetName(reader, MetadataTokens.EntityHandle(token));
                    if (target is not null) steps.Add(new Step(null, target));
                }

                offset += size;
            }

            found.Add(new Body(MethodName(reader, definition), steps));
        }

        literals = seen;
        return found;
    }

    private static IEnumerable<Type> Types(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException error)
        {
            return error.Types.Where(type => type is not null)!;
        }
    }

    private static int OperandSize(OperandType type, byte[] il, int offset) => type switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or
            OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or
            OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => offset + 4 <= il.Length
            ? 4 + (4 * BitConverter.ToInt32(il, offset))
            : -1,
        _ => -1
    };

    private static string? TargetName(MetadataReader reader, EntityHandle handle)
    {
        switch (handle.Kind)
        {
            case HandleKind.MethodDefinition:
                var definition = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                return MethodName(reader, definition);

            case HandleKind.MemberReference:
                var reference = reader.GetMemberReference((MemberReferenceHandle)handle);
                return reference.Parent.Kind == HandleKind.TypeDefinition
                    ? TypeName(reader, (TypeDefinitionHandle)reference.Parent) + "::" + reader.GetString(reference.Name)
                    : null;

            default:
                return null;
        }
    }

    private static string MethodName(MetadataReader reader, MethodDefinition definition)
        => TypeName(reader, definition.GetDeclaringType()) + "::" + reader.GetString(definition.Name);

    private static string TypeName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var type = reader.GetTypeDefinition(handle);
        var name = reader.GetString(type.Name);

        if (type.IsNested)
        {
            return TypeName(reader, type.GetDeclaringType()) + "+" + name;
        }

        var space = reader.GetString(type.Namespace);
        return space.Length == 0 ? name : space + "." + name;
    }
}
