using VidShrink.Ab;

if (args.Length == 0)
{
    Usage();
    return 1;
}

try
{
    return args[0] switch
    {
        "kos" => await RunAsync(args.Skip(1).ToList()),
        "parcala" => await CutAsync(args.Skip(1).ToList()),
        "sapma" => Deviation(args.Skip(1).ToList()),
        "denetle" => await InspectAsync(args.Skip(1).ToList()),
        _ => Unknown(args[0])
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static void Usage()
{
    Console.WriteLine("Kullanım:");
    Console.WriteLine("  ab kos --kaynak <yol> --hedef-mb <a[,b]> [--yarismaci handbrake,vidshrink] [--parca]");
    Console.WriteLine("         [--parca-dizin <klasör>] [--cikti <klasör>] [--gunluk <klasör>] [--json <yol>] [--tolerans 2]");
    Console.WriteLine("         [--esitleme-denemesi 4]");
    Console.WriteLine("  ab parcala --kaynak <yol> [--parca-dizin <klasör>]");
    Console.WriteLine("  ab sapma <tam.json> <parca.json>");
    Console.WriteLine("  ab denetle <referans> <aday>");
    Console.WriteLine("Yarışmacılar: " + string.Join(", ", AbSettings.KnownCompetitors));
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Bilinmeyen komut: {command}");
    Usage();
    return 1;
}

static async Task<int> RunAsync(IReadOnlyList<string> args)
{
    var settings = AbSettings.Parse(args, AbSettings.DefaultWorkRoot());
    Directory.CreateDirectory(settings.LogDirectory);
    var runLogPath = Path.Combine(settings.LogDirectory, "kosum-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".log");
    await using var runLog = new StreamWriter(runLogPath) { AutoFlush = true };
    using var log = new MirrorWriter(Console.Out, runLog);

    var report = await new AbRunner(log).RunAsync(settings, CancellationToken.None);

    Directory.CreateDirectory(Path.GetDirectoryName(settings.JsonPath)!);
    await File.WriteAllTextAsync(settings.JsonPath, Reporting.ToJson(report));

    var table = Reporting.Table(report);
    Console.WriteLine();
    Console.WriteLine(table);
    await File.WriteAllTextAsync(Path.ChangeExtension(settings.JsonPath, ".md"), table);
    Console.WriteLine($"json: {settings.JsonPath}");
    Console.WriteLine($"günlükler: {settings.LogDirectory}");

    var rejected = report.Measurements.Count(m => !m.Measured);
    if (rejected > 0)
    {
        Console.Error.WriteLine($"{rejected} ölçüm kapıda reddedildi; sayı basılmadı.");
        return 2;
    }
    return 0;
}

static async Task<int> CutAsync(IReadOnlyList<string> args)
{
    var settings = AbSettings.Parse(args.Concat(new[] { "--hedef-mb", "1" }).ToList(), AbSettings.DefaultWorkRoot());
    await ChunkCutter.EnsureAsync(settings.SourcePath, settings.ChunkDirectory, Console.Out, CancellationToken.None);
    return 0;
}

static async Task<int> InspectAsync(IReadOnlyList<string> args)
{
    if (args.Count < 2)
    {
        Console.Error.WriteLine("kullanım: ab denetle <referans> <aday>");
        return 1;
    }
    var (measured, text) = await AbRunner.InspectAsync(args[0], args[1], CancellationToken.None);
    Console.WriteLine(text);
    return measured ? 0 : 2;
}

static int Deviation(IReadOnlyList<string> args)
{
    if (args.Count < 2)
    {
        Console.Error.WriteLine("kullanım: ab sapma <tam.json> <parca.json>");
        return 1;
    }
    var full = Reporting.FromJson(File.ReadAllText(args[0]));
    var chunked = Reporting.FromJson(File.ReadAllText(args[1]));
    Console.WriteLine(Reporting.DeviationTable(Reporting.Deviation(full, chunked)));
    return 0;
}

internal sealed class MirrorWriter : TextWriter
{
    private readonly TextWriter _first;
    private readonly TextWriter _second;

    public MirrorWriter(TextWriter first, TextWriter second)
    {
        _first = first;
        _second = second;
    }

    public override System.Text.Encoding Encoding => _first.Encoding;

    public override void Write(char value)
    {
        _first.Write(value);
        _second.Write(value);
    }

    public override void WriteLine(string? value)
    {
        _first.WriteLine(value);
        _second.WriteLine(value);
    }
}
