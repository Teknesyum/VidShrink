using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using VidShrink.Core;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Bir tur uyesinin kaynaktaki tek gorunumu: hangi dosya, hangi satir, tuketici konumda mi
/// ve hangi kural bu karari verdi.
/// </summary>
internal readonly record struct MemberUse(string File, int Line, bool Consumer, string Rule, string Text);

/// <summary>Olcunun saydigi tur uyesi. <paramref name="Kind"/> "enum" ya da "alan".</summary>
internal readonly record struct TypeMember(string Type, string Name, string Kind)
{
    public override string ToString() => $"{Type}.{Name}";
}

internal sealed record MemberVerdict(
    TypeMember Member,
    IReadOnlyList<MemberUse> Uses,
    IReadOnlyList<string> Masked,
    int OutsideUses,
    bool SiblingConsumed)
{
    public int Producers => Uses.Count(u => !u.Consumer);
    public int Consumers => Uses.Count(u => u.Consumer);

    /// <summary>
    /// "Sifir uretim tuketicisi": uyeyi ureten en az bir satir var, uzerine dallanan hicbir
    /// satir yok. Uretimi de olmayan uye bu sinifa girmez — o baska bir sey, hic kullanilmayan
    /// uye.
    /// </summary>
    public bool ZeroConsumer => Producers > 0 && Consumers == 0;

    public bool Unused => Uses.Count == 0;

    public bool Flagged => ZeroConsumer || Unused;

    /// <summary>
    /// Bulgunun bicimi, olcunun kendi verisinden hesaplanir — yorum degil, sayidir.
    /// <c>varsayilan-kol</c>: ayni turun baska bir uyesi okunuyor, bu uye okuma tarafinda hic
    /// adlandirilmiyor. <c>hic-okunmayan-tur</c>: turun hicbir uyesi okunmuyor.
    /// <c>yalniz-disarida</c>: uretimde hic gorunmuyor, testlerde/araclarda goruluyor —
    /// bu sinifin yedi olayinda tekrar eden "tek tuketici test" cumlesi budur.
    /// <c>hic-gorunmeyen</c>: hicbir yerde gorunmuyor.
    /// </summary>
    public string Shape =>
        Uses.Count == 0
            ? OutsideUses > 0 ? "yalniz-disarida" : "hic-gorunmeyen"
            : Consumers > 0 ? "tuketiliyor"
            : SiblingConsumed ? "varsayilan-kol" : "hic-okunmayan-tur";
}

/// <summary>
/// Sifir uretim tuketicili tur uyelerini sayan duzenek.
///
/// Kume <b>turden</b> cikarilir: derlenmis <c>VidShrink.Core</c> uzerinde yansima ile
/// enum uyeleri ve <c>public static readonly</c> alanlar sayilir. Anahtar kelime listesi
/// yoktur; yeni bir uye eklendiginde kume kendiliginden buyur.
///
/// Konum karari <b>kaynaktan</b> okunur: <c>src/**</c> altindaki her <c>.cs</c> dosyasinin
/// yorumlari ve dizgi sabitleri once bosluga cevrilir (T150 oncesi bir tarama tam bunu
/// atlayip docstring metnini uye adi sanmisti), sonra <c>Tur.Uye</c> gorunumleri bulunup
/// tuketici mi uretici mi diye siniflanir.
/// </summary>
internal static class MemberScan
{
    private static readonly string SourceRoot = Path.Combine(TipSources.Root, "src");

    /// <summary>
    /// Tuketici konumlarin kapali listesi. Her kural bir C# dilbilgisi konumudur, bir
    /// anahtar kelime tahmini degil: uye ya bir orunt konumundadir (<c>case</c>, <c>is</c>,
    /// desen birlestiricileri, <c>switch</c> kolunun solu), ya bir esitlik karsilastirmasinin
    /// tarafidir, ya da bir tablo aramasinin anahtaridir.
    /// </summary>
    private static readonly (string Rule, Regex? Before, Regex? After)[] ConsumerRules =
    {
        ("case", new Regex(@"\bcase\s*$", RegexOptions.Compiled), null),
        ("esitlik-sol", null, new Regex(@"^\s*(==|!=)", RegexOptions.Compiled)),
        ("esitlik-sag", new Regex(@"(==|!=)\s*$", RegexOptions.Compiled), null),
        ("orunt-onek", new Regex(@"\b(is|not|or|and)\s+$", RegexOptions.Compiled), null),
        ("orunt-birlestirici", null, new Regex(@"^\s*(or|and)\b", RegexOptions.Compiled)),
        ("switch-kolu", null, new Regex(@"^\s*=>", RegexOptions.Compiled)),
        ("arama-cagrisi",
            new Regex(@"\b(ContainsKey|ContainsValue|Contains|TryGetValue|HasFlag|IndexOf)\s*\(\s*$", RegexOptions.Compiled),
            null),
        ("arama-dizini", new Regex(@"\[\s*$", RegexOptions.Compiled), new Regex(@"^\s*\](?!\s*=[^=])", RegexOptions.Compiled))
    };

    internal static IReadOnlyList<TypeMember> Members()
    {
        var assembly = typeof(CodecModel).Assembly;
        var members = new List<TypeMember>();

        foreach (var type in assembly.GetTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            if (type.Namespace is null || !type.Namespace.StartsWith("VidShrink.Core", StringComparison.Ordinal))
                continue;
            if (type.IsDefined(typeof(CompilerGeneratedAttribute), false))
                continue;

            if (type.IsEnum)
            {
                foreach (var name in Enum.GetNames(type))
                    members.Add(new TypeMember(type.Name, name, "enum"));
                continue;
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                if (field.IsInitOnly && !field.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    members.Add(new TypeMember(type.Name, field.Name, "alan"));
        }

        return members;
    }

    internal static IReadOnlyList<string> SourceFiles() => Files(SourceRoot);

    /// <summary>
    /// Uretim disindaki kullanicilar: testler ve olcum araclari. Bu sinifin yedi kayitli
    /// olayinda tekrar eden cumle "testler onu tek tuketici olarak ayakta tutuyor" idi;
    /// o cumlenin sayisi buradan cikar.
    /// </summary>
    internal static IReadOnlyList<string> OutsideFiles()
        => Files(Path.Combine(TipSources.Root, "tests"))
            .Concat(Files(Path.Combine(TipSources.Root, "tools")))
            .ToList();

    private static IReadOnlyList<string> Files(string root)
        => !Directory.Exists(root)
            ? Array.Empty<string>()
            : Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

    internal static IReadOnlyList<MemberVerdict> Scan()
    {
        var files = SourceFiles();
        var outside = OutsideFiles().ToDictionary(f => f, f => Strip(File.ReadAllText(f)), StringComparer.Ordinal);
        var stripped = files.ToDictionary(f => f, f => Strip(File.ReadAllText(f)), StringComparer.Ordinal);
        var raw = files.ToDictionary(f => f, f => File.ReadAllText(f), StringComparer.Ordinal);

        var verdicts = new List<MemberVerdict>();
        foreach (var member in Members())
        {
            var pattern = new Regex($@"\b{Regex.Escape(member.Type)}\s*\.\s*{Regex.Escape(member.Name)}\b", RegexOptions.Compiled);
            var uses = new List<MemberUse>();
            var masked = new List<string>();

            foreach (var file in files)
            {
                var text = stripped[file];
                foreach (Match match in pattern.Matches(text))
                {
                    var (consumer, rule) = Classify(member, text, match);
                    uses.Add(new MemberUse(Relative(file), LineOf(text, match.Index), consumer, rule, LineText(text, match.Index)));
                }

                var kept = pattern.Matches(text).Select(m => m.Index).ToHashSet();
                foreach (Match match in pattern.Matches(raw[file]))
                    if (!kept.Contains(match.Index))
                        masked.Add($"{Relative(file)}:{LineOf(raw[file], match.Index)}  {LineText(raw[file], match.Index)}");
            }

            var outsideUses = outside.Values.Sum(text => pattern.Matches(text).Count);
            verdicts.Add(new MemberVerdict(member, uses, masked, outsideUses, false));
        }

        var readTypes = verdicts
            .Where(v => v.Consumers > 0)
            .Select(v => v.Member.Type)
            .ToHashSet(StringComparer.Ordinal);

        return verdicts
            .Select(v => v with { SiblingConsumed = readTypes.Contains(v.Member.Type) })
            .ToList();
    }

    private static (bool Consumer, string Rule) Classify(TypeMember member, string text, Match match)
    {
        if (member.Kind == "alan")
            return (true, "alan-okumasi");

        var before = Unqualify(text[Math.Max(0, match.Index - 64)..match.Index]);
        var after = text[(match.Index + match.Length)..Math.Min(text.Length, match.Index + match.Length + 48)];

        foreach (var (rule, beforePattern, afterPattern) in ConsumerRules)
        {
            if (beforePattern is not null && !beforePattern.IsMatch(before)) continue;
            if (afterPattern is not null && !afterPattern.IsMatch(after)) continue;
            if (beforePattern is null && afterPattern is null) continue;
            return (true, rule);
        }

        return (false, "uretim");
    }

    /// <summary>
    /// Uyeden onceki ad nitelemesini duser. <c>result.Failure == CoreShare.ShareFailure.Cancelled</c>
    /// satirinda esitligi goren tek yol budur: ad alani takma adi araya girince kural
    /// <c>==</c> yerine <c>CoreShare.</c> goruyordu ve on gorunum yanlislikla uretim sayildi.
    /// </summary>
    private static readonly Regex Qualifier = new(@"[A-Za-z_][A-Za-z0-9_]*\s*\.\s*$", RegexOptions.Compiled);

    private static string Unqualify(string before)
    {
        var trimmed = before;
        while (true)
        {
            var match = Qualifier.Match(trimmed);
            if (!match.Success) return trimmed;
            trimmed = trimmed[..match.Index];
        }
    }

    internal static string Relative(string path)
        => Path.GetRelativePath(TipSources.Root, path).Replace('\\', '/');

    private static int LineOf(string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index; i++)
            if (text[i] == '\n') line++;
        return line;
    }

    private static string LineText(string text, int index)
    {
        var start = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
        var end = text.IndexOf('\n', index);
        if (end < 0) end = text.Length;
        return text[start..end].Trim();
    }

    /// <summary>
    /// Yorumlari ve dizgi sabitlerini bosluga cevirir; satir sonlarini korur, boylece
    /// satir numaralari kaymaz.
    /// </summary>
    internal static string Strip(string source)
    {
        var output = new StringBuilder(source.Length);
        var i = 0;

        void Blank(int from, int to)
        {
            for (var k = from; k < to && k < source.Length; k++)
                output.Append(source[k] == '\n' ? '\n' : source[k] == '\r' ? '\r' : ' ');
        }

        while (i < source.Length)
        {
            if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                var end = source.IndexOf('\n', i);
                if (end < 0) end = source.Length;
                Blank(i, end);
                i = end;
                continue;
            }

            if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
                Blank(i, end);
                i = end;
                continue;
            }

            if (source[i] == '"' && i + 2 < source.Length && source[i + 1] == '"' && source[i + 2] == '"')
            {
                var end = source.IndexOf("\"\"\"", i + 3, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 3;
                Blank(i, end);
                i = end;
                continue;
            }

            if (source[i] == '@' && i + 1 < source.Length && source[i + 1] == '"')
            {
                var k = i + 2;
                while (k < source.Length)
                {
                    if (source[k] == '"' && k + 1 < source.Length && source[k + 1] == '"') { k += 2; continue; }
                    if (source[k] == '"') { k++; break; }
                    k++;
                }
                Blank(i, k);
                i = k;
                continue;
            }

            if (source[i] == '"')
            {
                var k = i + 1;
                while (k < source.Length && source[k] != '\n')
                {
                    if (source[k] == '\\') { k += 2; continue; }
                    if (source[k] == '"') { k++; break; }
                    k++;
                }
                Blank(i, k);
                i = k;
                continue;
            }

            if (source[i] == '\'')
            {
                var k = i + 1;
                while (k < source.Length && source[k] != '\n')
                {
                    if (source[k] == '\\') { k += 2; continue; }
                    if (source[k] == '\'') { k++; break; }
                    k++;
                }
                Blank(i, k);
                i = k;
                continue;
            }

            output.Append(source[i]);
            i++;
        }

        return output.ToString();
    }
}

/// <summary>
/// Olcunun pimi. Her satir bir bulguyu tasir: hangi uye, hangi bicim, karar ne ve <b>neden</b>.
/// <para>
/// <c>Verdict</c> iki degerden birini alir. <c>mesru</c>: uyenin okuma tarafinda
/// adlandirilmamasi kasitli, gerekcesi kodda ya da olcumde yazili. <c>borc</c>: T150 bu uyeyi
/// siniflamadi — mesru oldugu <b>gosterilmedi</b>, kaza oldugu da gosterilmedi; dusurmek
/// uretim davranisini degistirdigi icin karar ayri bir sozlesmenin isi.
/// </para>
/// <para>
/// Gerekcesiz satir yoktur: <c>mesru</c> satirlari bir gerekce cumlesi tasimak zorundadir,
/// <c>borc</c> satirlari da neyin olculmedigini soyler.
/// </para>
/// </summary>
internal readonly record struct PinnedFinding(string Member, string Shape, string Verdict, string Reason);

public sealed class OluUyeTests
{
    private const string Legitimate = "mesru";
    private const string Debt = "borc";

    /// <summary>
    /// Bugunku kume. Bu liste olcunun <b>girdisi</b> degil <b>beklentisidir</b>: kume
    /// yansimayla turden cikar, bu satirlar yalniz cikanla karsilastirilir. Yeni bir sifir
    /// tuketicili uye acilirsa ya da var olan birine gercek bir tuketici gelirse burasi kirmizi
    /// olur.
    /// <para>
    /// Kume bugun 37 satir: 32 sifir uretim tuketicili uye + 5 hic kullanilmayan uye
    /// (<c>Flagged = ZeroConsumer || Unused</c>). T163 kumeyi 51 satirdan 37'ye indirdi ve
    /// kalan bir satirin bicimini degistirdi. Dusen 19 satir T165'in <c>ReasonCode.Manual*</c>
    /// kodlariydi: T165 onlari uretmis ama okuyamamisti, cunku okuma tarafi
    /// <c>src/VidShrink.App/**</c> ve orasi T163'un alaniydi; T163
    /// <c>MainWindow.axaml.cs:2625-2661</c>'e on dokuz kolun hepsini yazdi, kodlar artik
    /// tuketiliyor ve pimde isleri kalmadi. Bicimi degisen satir
    /// <c>EncoderPathOverride.Software</c>: T163 (<c>64125dc</c>) <c>MainWindow.axaml.cs:1005</c>
    /// ile uyeye uretimde ilk ureticiyi yazdi, uye
    /// <c>yalniz-disarida</c>'dan <c>varsayilan-kol</c>'a gecti. Pime T170 bes yeni satir
    /// getirdi: <c>ShrinkArgumentProblem</c>'in bes uyesi de uretiliyor, hicbiri okunmuyor.
    /// T165 turunda kume 31'den 51'e cikmisti. Bundan onceki degisim T150 tur 2'deydi: sifir
    /// tuketici 27'den 26'ya, kume 32 satirdan 31'e inmisti. O turda cikan uye
    /// <c>EncoderProbeState.NotWorking</c>:
    /// T139 <c>src/VidShrink.Ffmpeg/PerformanceProbe.cs:97</c> satirini yazdi
    /// (<c>2caff96</c> ekledi, <c>cf009f0</c> cagriyi <c>KnownState</c> olarak yeniden adlandirdi)
    /// ve satir <c>main</c>e <c>5df0a98</c> birlesmesiyle geldi. Satir
    /// <c>if (availability.KnownState(candidate) != EncoderProbeState.NotWorking) return candidate;</c>
    /// olcunun <c>esitlik-sag</c> kuralina dusuyor: uye artik dokuz yerde uretilip bir yerde
    /// tuketiliyor, sifir uretim tuketicisi degil. Pimden dusuruldu, uye dusurulmedi.
    /// </para>
    /// </summary>
    private static readonly PinnedFinding[] Pinned =
    {
        new("ArchitectureOutcome.Assumed", "hic-okunmayan-tur", Debt,
            "Iki uyeli turun hicbir uyesi uretimde okunmuyor: ArchitectureDecision.Outcome yaziliyor, kimse sormuyor. Turun docstring'i (UpdateCheck.cs:34) 'Kullaniciya ne soylenecegini bu ayiriyor' diyor; ayiran kol yok."),
        new("ArchitectureOutcome.Read", "hic-okunmayan-tur", Debt,
            "Ayni turun oteki uyesi, ayni bulgu. Turun tamami okunmadigi icin bu bir 'olumsuz kol' degil; docstring ile kod arasindaki fark olculmedi."),
        new("ComparisonSourceState.Duraklatildi", "varsayilan-kol", Debt,
            "Karsilastirma kaynaginin duraklatilmis durumu uretiliyor, hicbir kol duraklatilmisi ayirmiyor. Ayirmanin gerekip gerekmedigi olculmedi."),
        new("ConversionQualityMode.Bitrate", "varsayilan-kol", Legitimate,
            "Iki degerli kipin olumsuz kolu. Tek okuyan ConversionArguments.cs:86 'QualityMode == ConversionQualityMode.Crf' diye soruyor; Bitrate o kosulun else'i, ayrica adlandirilmasi ayni dali ikiye bolerdi."),
        new("EncoderVendor.Software", "varsayilan-kol", Legitimate,
            "Vendor()'in son satiri; IsHardware olculmus uc saticiyi adiyla sayip '_ => false' diyor, QualityArgs son kolda -crf veriyor. Software'i ayrica adlandirmak ayni davranisi iki yere yazardi."),
        new("FillPolicy.QualityCeiling", "varsayilan-kol", Legitimate,
            "Iki degerli siyasetin olumsuz kolu. Uc okuyan da (PlanCalculator.cs:342, MainWindow.axaml.cs:2284, EncodeRunner.cs:140) 'fillPolicy == FillPolicy.FillTarget' soruyor; tavan kolu o kosulun else'i."),
        new("HardwareVerdictReason.BitrateFloorTooHigh", "varsayilan-kol", Debt,
            "Alti gerekceden biri; uretiliyor, hicbir kol bu gerekceyi ayirmiyor. Digerlerinin okunup bunun okunmamasi kasitli mi olculmedi."),
        new("PreviewQuality.Desteklenmiyor", "varsayilan-kol", Debt,
            "PreviewSegment.cs:103 modellenmemis kodek icin uretiyor; arayuz rozeti bu uyeyi adiyla sormuyor. Rozet kosulunun hangi uyeye baktigi olculmedi."),
        new("PreviewQuality.Yaklasik", "varsayilan-kol", Debt,
            "Bitrate'ten cevrilen kalite degerinin isareti; uretiliyor, okuyan kol yok. Ayni olcum borcu Desteklenmiyor ile birlikte durur."),
        new("PreviewState.Olculemedi", "varsayilan-kol", Debt,
            "Onizleme zaman cizgisinin uc durumu uretiliyor, uzerlerine dallanilmiyor. Durumlarin arayuzde ayrilmasi gerekip gerekmedigi olculmedi."),
        new("PreviewState.OrnekKodlaniyor", "varsayilan-kol", Debt,
            "Ayni tur, ayni bulgu: uretiliyor, okuma tarafinda adi gecmiyor."),
        new("PreviewState.TamKodlama", "varsayilan-kol", Debt,
            "Ayni tur, ayni bulgu: uretiliyor, okuma tarafinda adi gecmiyor."),
        new("QualityTargetBound.Matched", "varsayilan-kol", Legitimate,
            "Hedefe varildi demek; arayuz yalniz sapmalari yaziyor (MainWindow.axaml.cs:2517-2519: BelowFloor, AboveSourceCeiling, '_ => \"\"'). Varildiginda gosterilecek bir cumle yok, o yuzden okuyan da yok."),
        new("RecordingImpact.HardwareOffload", "varsayilan-kol", Legitimate,
            "PerformanceReportText.cs:22-26 mansetin Impact uzerinden kurulmadigini olcumle yaziyor: makine mesgulken Impact yazilim dalina kayiyor, dogru bilgi bulgularda. Alan raporda tasiniyor, karar vermiyor."),
        new("RecordingImpact.SoftwareHeavyLoad", "varsayilan-kol", Legitimate,
            "Ayni olcum: Impact karar alani degil, rapor alani. PerformanceReportText mansetini PerformanceFindingCode uzerinden kuruyor."),
        new("RecordingImpact.SoftwareLightLoad", "varsayilan-kol", Legitimate,
            "Ayni olcum: Impact karar alani degil, rapor alani. PerformanceReportText mansetini PerformanceFindingCode uzerinden kuruyor."),
        new("ShareFailure.FileTooLarge", "varsayilan-kol", Debt,
            "ShareErrorClassifier on bir hatanin hepsini uretiyor, sekizini hicbir kol ayirmiyor. Okunan dort uye: Cancelled ve TokenExpired arayuzde (MainWindow.axaml.cs:1033, :3415), None ShareResult.cs:103'te, Unknown siniflandiricinin kendi sayacinda (ShareErrorClassifier.cs:227). Ayrimin kullaniciya ulasip ulasmadigi olculmedi."),
        new("ShareFailure.FileUnreadable", "varsayilan-kol", Debt,
            "Ayni bulgu, uzerine bir tane daha: uretim disinda da hic gorunmuyor (disarida=0), yani onu ayakta tutan bir test bile yok."),
        new("ShareFailure.LocalDiskFull", "varsayilan-kol", Debt,
            "Ayni bulgu, uzerine bir tane daha: uretim disinda da hic gorunmuyor (disarida=0), yani onu ayakta tutan bir test bile yok."),
        new("ShareFailure.NetworkFailure", "varsayilan-kol", Debt,
            "Ayni bulgu: dort yerde uretiliyor, hicbir kol ag hatasini ayirmiyor."),
        new("ShareFailure.NotAuthorized", "varsayilan-kol", Debt,
            "Ayni bulgu: uc yerde uretiliyor, yetkisizligi ayiran hicbir kol yok; arayuz onu genel hata cumlesine dokuyor."),
        new("ShareFailure.QuotaExceeded", "varsayilan-kol", Debt,
            "Ayni bulgu: uretiliyor, kota asimini ayiran kol yok; kullanici genel hata cumlesini goruyor."),
        new("ShareFailure.RateLimited", "varsayilan-kol", Debt,
            "Ayni bulgu: uretiliyor, hiz sinirini ayiran kol yok; yeniden deneme onerisi hicbir yerde kurulmuyor."),
        new("ShareFailure.ServiceError", "varsayilan-kol", Debt,
            "Ayni bulgu: uc yerde uretiliyor, servis hatasini ayiran kol yok; kullanici genel hata cumlesini goruyor."),
        new("SpeedMode.Quality", "varsayilan-kol", Legitimate,
            "Iki degerli kipin olumsuz kolu ve varsayilani. On okuma yerinin hepsi 'speed == SpeedMode.Fast' kalibinda soruyor (dokuzu ==, CalibrationProbe.cs:148 !=); Quality o kosulun else'i."),
        new("WindowBiasSource.None", "varsayilan-kol", Legitimate,
            "Pencere sapmasinin 'kaynak yok' hali. ComplexityProfile.cs:128-132 Scan ve Packets'i adlandirip '_ => MeasuredBand' diyor; None olculmemis bandin ta kendisi, ayri bir kol ayni degeri verirdi."),
        new("FfmpegArguments.SceneMapRuleOfRecord", "yalniz-disarida", Debt,
            "Uretimde sifir gorunum, testlerde ve araclarda bes. Bu sinifin en saf hali: alani ayakta tutan tek taraf olcum tarafi. Dusurmek olcum duzenegini kirar, karar ayri sozlesme."),
        new("Intent.SocialMedia", "hic-gorunmeyen", Debt,
            "Hicbir yerde gorunmuyor — ne uretim, ne test, ne arac. T0'in ikinci desen denemesinin bulabildigi tek uye buydu; olcu onu ayni yerde buluyor ama tek basina degil."),
        new("LauncherUpdate.CommitWindow", "hic-gorunmeyen", Debt,
            "public static readonly, hicbir yerde okunmuyor. Dusurulmesi UpdateCheck.cs'i degistirir, o dosya bu sozlesmenin owns listesinde yok."),
        new("MacUpdate.DownloadTimeout", "hic-gorunmeyen", Debt,
            "public static readonly, hicbir yerde okunmuyor. Ayni dosya, ayni sinir."),
        new("UpdateCheck.ManifestTimeout", "yalniz-disarida", Debt,
            "Uretimde sifir, testlerde bir gorunum. Ayni dosya, ayni sinir."),
        new("EncoderPathOverride.Software", "varsayilan-kol", Legitimate,
            "Uc degerli turun orta uyesi; motor yolu 'Auto mu degil mi' ve 'Hardware mi' diye iki adimda soruyor (PlanCalculator.cs:271 kapiyi acar, :274 wantsHardware = EncoderPath == Hardware). Software ikinci sorunun else'i, o yuzden okuma tarafinda ada gerek kalmiyor; ayrica adlandirmak ayni dali ikiye bolerdi. T163 (64125dc) uretim tarafina tek uretici ekledi: MainWindow.axaml.cs:1005, gelismis ayarlar acilir kutusunun ikinci satiri kullanicinin secimini bu uyeye ceviriyor. Bicim o yuzden yalniz-disarida'dan varsayilan-kol'a dondu: uye artik uretimde uretiliyor ama hala hicbir kol onu adiyla tuketmiyor. Islevsel olarak ulasildigi asagidaki TheSoftwareEncoderPathIsReachedWithoutBeingNamed olcusuyle gosteriliyor: ayni girdide Auto donanim, Software yazilim, Hardware donanim kodegi veriyor ve uc sonuc da birbirinden farkli."),
        new("ShrinkArgumentProblem.NoPath", "hic-okunmayan-tur", Debt, ShrinkProblemDebt),
        new("ShrinkArgumentProblem.NoTarget", "hic-okunmayan-tur", Debt, ShrinkProblemDebt),
        new("ShrinkArgumentProblem.TargetNotANumber", "hic-okunmayan-tur", Debt, ShrinkProblemDebt),
        new("ShrinkArgumentProblem.TargetNotInQuickList", "hic-okunmayan-tur", Debt, ShrinkProblemDebt),
        new("ShrinkArgumentProblem.TargetNotPositive", "hic-okunmayan-tur", Debt, ShrinkProblemDebt)
    };

    /// <summary>
    /// T170'in <c>--kucult</c> arguman cozumunun urettigi bes ret gerekcesi. <c>ShrinkRequest.cs</c>
    /// hepsini <c>ShrinkArgumentResult.Failure</c> ile uretiyor, ama <c>src/**</c> altinda
    /// <c>Problem</c> alanini okuyan tek bir satir yok — turun hicbir uyesi tuketilmedigi icin
    /// bicim <c>hic-okunmayan-tur</c>. Yani kabuk menusunden gelen bozuk arguman adiyla
    /// reddediliyor ama kullanici o adi hicbir yerde gormuyor.
    /// </summary>
    private const string ShrinkProblemDebt =
        "T170'in --kucult arguman cozumunun urettigi ret gerekcesi. ShrinkRequest.cs onu ShrinkArgumentResult.Failure ile uretiyor ve olcusu ShrinkRequestTests'te var, ama src/** altinda ShrinkArgumentResult.Problem alanini okuyan hicbir satir yok; turun hicbir uyesi tuketilmiyor. Bozuk arguman adlandirilmis bir gerekceyle reddediliyor, kullaniciya gerekce gosterilmiyor. Gerekcenin kullaniciya ulasmasi gerekip gerekmedigi olculmedi.";

    private readonly ITestOutputHelper _output;

    public OluUyeTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// K3'un pimi. Kume yansimayla turden cikiyor, bu test cikani <see cref="Pinned"/> ile
    /// karsilastiriyor. Uretimde yeni bir sifir tuketicili uye acilirsa fazla satir,
    /// var olan birine gercek bir tuketici gelirse eksik satir cikar; iki mutasyon da kirmizi.
    /// </summary>
    [Fact]
    public void TheZeroConsumerSetIsThePinnedSet()
    {
        var found = MemberScan.Scan()
            .Where(v => v.Flagged)
            .Select(v => $"{v.Member}  {v.Shape}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        var expected = Pinned
            .Select(p => $"{p.Member}  {p.Shape}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        foreach (var line in found) _output.WriteLine(line);

        Assert.Equal(expected, found);
    }

    /// <summary>
    /// K4: beyaz liste gerekcesiz satir kabul etmez. <c>mesru</c> satiri neden mesru oldugunu
    /// soyler, <c>borc</c> satiri neyin olculmedigini soyler.
    /// </summary>
    [Fact]
    public void EveryPinnedFindingCarriesAReason()
    {
        foreach (var finding in Pinned)
        {
            Assert.True(finding.Verdict is Legitimate or Debt, $"{finding.Member}: taninmayan karar '{finding.Verdict}'");
            Assert.True(finding.Reason.Length >= 60, $"{finding.Member}: gerekce yok ya da tek kelimelik");
        }

        _output.WriteLine($"mesru: {Pinned.Count(p => p.Verdict == Legitimate)}  borc: {Pinned.Count(p => p.Verdict == Debt)}");
    }

    /// <summary>
    /// K2: kume anahtar kelime listesinden degil turden cikiyor. Kanit, olcunun bu dosyada
    /// adi hic gecmeyen uyeleri de bulmasi — pimlenen 37 satir 163 uyelik kumenin bir parcasi,
    /// kumenin kendisi degil.
    /// </summary>
    [Fact]
    public void TheMemberSetComesFromTheAssemblyNotFromThisFile()
    {
        var testSource = File.ReadAllText(Path.Combine(TipSources.Root, "tests", "VidShrink.Tests", "OluUyeTests.cs"));
        var members = MemberScan.Members();
        var unnamed = members.Count(m => !testSource.Contains(m.ToString(), StringComparison.Ordinal));

        _output.WriteLine($"uye: {members.Count}  bu dosyada adi gecmeyen: {unnamed}  pimlenen: {Pinned.Length}");

        Assert.True(unnamed >= 90, $"olcu kumeyi kendi dosyasindan sayiyor olabilir: adi gecmeyen yalniz {unnamed}");
        Assert.True(members.Count > Pinned.Length * 3, "kume pim listesinden buyuk degil");
    }

    /// <summary>
    /// T150 oncesi bir tarama enum govdesini yorumlariyla birlikte ayristirip 63 olu uye
    /// bildirmisti; adlarin yarisi docstring metniydi. Bu test kirpicinin yalnizca yorum ve
    /// dizgi sakladigini pimler: saklanan her gorunum bir docstring satirindan gelmeli.
    /// </summary>
    [Fact]
    public void TheStripperHidesOnlyCommentsAndStringLiterals()
    {
        var masked = MemberScan.Scan().SelectMany(v => v.Masked).ToList();

        foreach (var line in masked) _output.WriteLine(line);
        _output.WriteLine($"saklanan gorunum: {masked.Count}");

        Assert.NotEmpty(masked);
        Assert.All(masked, line =>
        {
            var text = line[(line.IndexOf("  ", StringComparison.Ordinal) + 2)..].TrimStart();
            Assert.StartsWith("///", text, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// K5: <c>QualityArgs</c>'in son kolu <c>-crf</c> uretir ve VideoToolbox <c>-crf</c> kabul
    /// etmez. Kol artik sessizce gecersiz bayrak uretmiyor, acikca patliyor.
    /// </summary>
    [Theory]
    [InlineData("h264_videotoolbox")]
    [InlineData("hevc_videotoolbox")]
    public void VideoToolboxDoesNotFallIntoTheCrfArm(string codec)
    {
        var error = Assert.Throws<NotSupportedException>(() => CodecModel.QualityArgs(codec, 23));

        Assert.Contains(codec, error.Message, StringComparison.Ordinal);
        Assert.Contains("-q:v", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Ayni kol yazilim kodlayicisini eskisi gibi <c>-crf</c>'e goturuyor.</summary>
    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    [InlineData("libsvtav1")]
    public void TheSoftwareArmStillProducesCrf(string codec)
    {
        Assert.Equal(new[] { "-crf", "23" }, CodecModel.QualityArgs(codec, 23));
    }

    /// <summary>
    /// K5'in siniri: kol yazildi, kapi acilmadi. Kapinin kendi olcusu
    /// <c>PlanParserTests.ParserStillRejectsVideoToolboxEncoders</c>; burada yalniz kapinin
    /// listesi okunuyor, cunku K5'in gerekcesi "buraya uretimden ulasan yok" cumlesine dayaniyor.
    /// </summary>
    [Fact]
    public void TheGateStaysClosed()
    {
        var parser = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.Core", "PlanParser.cs"));
        var preview = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.Core", "PreviewSegment.cs"));

        Assert.DoesNotContain("videotoolbox", parser, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("videotoolbox", preview, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeAvailability : IEncoderAvailability
    {
        private readonly Dictionary<string, EncoderProbeState> _states;

        public FakeAvailability(params (string Codec, EncoderProbeState State)[] states)
            => _states = states.ToDictionary(s => s.Codec, s => s.State, StringComparer.OrdinalIgnoreCase);

        public bool HasEncoder(string name) => _states.ContainsKey(name);
        public bool WorksAsEncoder(string codec) => _states.TryGetValue(codec, out var s) && s == EncoderProbeState.Working;
        public EncoderProbeState EncoderState(string codec) => _states.TryGetValue(codec, out var s) ? s : EncoderProbeState.NotWorking;
    }

    private static FakeAvailability AllWorking() => new(
        ("libx264", EncoderProbeState.Working),
        ("libx265", EncoderProbeState.Working),
        ("libsvtav1", EncoderProbeState.Working),
        ("h264_nvenc", EncoderProbeState.Working),
        ("hevc_nvenc", EncoderProbeState.Working),
        ("av1_nvenc", EncoderProbeState.Working));

    private static MediaInfo Kaynak() => new()
    {
        FilePath = "sample.mp4",
        FileSizeBytes = 500L * 1024 * 1024,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 35_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    private static MediaInfo HedefinAltindaKaynak() => new()
    {
        FilePath = "zaten-kucuk.mp4",
        FileSizeBytes = 10L * 1024 * 1024,
        DurationSeconds = 60,
        Width = 1280,
        Height = 720,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1_400_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    /// <summary>
    /// <c>EncoderPathOverride.Software</c>'in pimi neden <c>mesru</c>: uye uretimde ada gore
    /// hic gecmiyor ama islevsel olarak ulasiliyor. Motor yolu iki adimda soruyor —
    /// <c>PlanCalculator.cs:271</c> kapiyi <c>!= Auto</c> ile acar, <c>:274</c>
    /// <c>wantsHardware = EncoderPath == Hardware</c> der. <c>Software</c> ikinci sorunun
    /// else'idir. Uc degerin ucu de ayni girdide farkli plan uretirse uye canlidir.
    /// </summary>
    [Fact]
    public void TheSoftwareEncoderPathIsReachedWithoutBeingNamed()
    {
        var info = Kaynak();
        PlanResult Kur(EncoderPathOverride yol) => PlanCalculator.BuildDetailed(
            info,
            new PlanOptions { TargetMb = 25, Codec = CodecPreference.Fast, SpeedMode = SpeedMode.Fast, EncoderPath = yol },
            null,
            AllWorking());

        var auto = Kur(EncoderPathOverride.Auto);
        var software = Kur(EncoderPathOverride.Software);
        var hardware = Kur(EncoderPathOverride.Hardware);

        _output.WriteLine($"Auto     -> {auto.Plan.Codec} (donanim={CodecModel.IsHardware(auto.Plan.Codec)})");
        _output.WriteLine($"Software -> {software.Plan.Codec} (donanim={CodecModel.IsHardware(software.Plan.Codec)})");
        _output.WriteLine($"Hardware -> {hardware.Plan.Codec} (donanim={CodecModel.IsHardware(hardware.Plan.Codec)})");

        Assert.True(CodecModel.IsHardware(auto.Plan.Codec),
            $"motor kendiliginden donanim secmezse bu olcu bir sey kanitlamaz (bulunan {auto.Plan.Codec})");
        Assert.False(CodecModel.IsHardware(software.Plan.Codec));
        Assert.True(CodecModel.IsHardware(hardware.Plan.Codec));
        Assert.NotEqual(auto.Plan.Codec, software.Plan.Codec);

        var not = Assert.Single(software.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualEncoderPathOverride);
        Assert.Equal("Software", not.ManualOverrideValue);
        Assert.DoesNotContain(auto.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualEncoderPathOverride);
    }

    /// <summary>
    /// Ayni uyenin oteki iki uretim kapisi: <c>PlanCalculator.cs:263-264</c> (kodek kilidiyle
    /// celisme) ve <c>:799</c> (kopyalama yolunda dusme). Ikisinde de <c>Software</c> degeri
    /// nota <b>adiyla</b> giriyor — yani ada gore gorunmemesi taramanin siniri, uyenin degil.
    /// </summary>
    [Fact]
    public void TheSoftwarePathValueReachesBothOtherProductionGates()
    {
        var kilit = PlanCalculator.BuildDetailed(
            Kaynak(),
            new PlanOptions { TargetMb = 25, Codec = CodecPreference.Compatible, LockedCodec = "hevc_nvenc", EncoderPath = EncoderPathOverride.Software },
            null,
            AllWorking());

        var kopya = PlanCalculator.BuildDetailed(
            HedefinAltindaKaynak(),
            new PlanOptions { TargetMb = 25, Codec = CodecPreference.Auto, EncoderPath = EncoderPathOverride.Software },
            null,
            AllWorking());

        _output.WriteLine($"kilit: codec={kilit.Plan.Codec} gerekce={kilit.Plan.Reason}");
        _output.WriteLine($"kopya: mode={kopya.Plan.Mode} gerekce={kopya.Plan.Reason}");

        var kilitNotu = Assert.Single(kilit.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualEncoderPathSupersededByCodec);
        Assert.Equal("Software", kilitNotu.ManualOverrideValue);
        Assert.Equal("hevc_nvenc", kilit.Plan.Codec);

        var kopyaNotu = Assert.Single(kopya.Plan.ReasonCodes, n => n.Code == ReasonCode.ManualOverrideDroppedOnPassThrough);
        Assert.Equal("passthrough", kopya.Plan.Mode);
        Assert.Equal("kodlayici yolu=Software", kopyaNotu.ManualOverrideValue);
    }

    [Fact]
    public void TheScanDumpsEveryMemberItFound()
    {
        var verdicts = MemberScan.Scan();

        _output.WriteLine($"dosya: {MemberScan.SourceFiles().Count}  uye: {verdicts.Count}");
        _output.WriteLine($"sifir tuketici: {verdicts.Count(v => v.ZeroConsumer)}  hic kullanilmayan: {verdicts.Count(v => v.Unused)}");
        _output.WriteLine($"dizgi/yorum icinde kalan gorunum: {verdicts.Sum(v => v.Masked.Count)}");
        _output.WriteLine("");

        foreach (var verdict in verdicts.OrderBy(v => v.Member.ToString(), StringComparer.Ordinal))
        {
            var state = verdict.Unused ? "KULLANILMIYOR" : verdict.ZeroConsumer ? "SIFIR-TUKETICI" : "tuketiliyor";
            _output.WriteLine($"{verdict.Member} [{verdict.Member.Kind}] {state} uretim={verdict.Producers} tuketim={verdict.Consumers} disarida={verdict.OutsideUses} maskeli={verdict.Masked.Count}");
            foreach (var use in verdict.Uses)
                _output.WriteLine($"    {(use.Consumer ? "T" : "U")} {use.Rule,-20} {use.File}:{use.Line}  {use.Text}");
            foreach (var line in verdict.Masked)
                _output.WriteLine($"    M maskeli              {line}");
        }

        Assert.NotEmpty(verdicts);
    }
}
