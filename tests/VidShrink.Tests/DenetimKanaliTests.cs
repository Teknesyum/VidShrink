using VidShrink.App;
using VidShrink.App.Integration;
using VidShrink.Cli;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Yerel denetim borusu (later 43): tek örnek borusuna satır başına bir JSON komutu. Borular gerçek,
/// konak sahte: kayıt başlatma ve durdurma sahte konağa düşer, ekran yakalanmaz.
/// </summary>
public sealed class DenetimKanaliTests : IDisposable
{
    private static readonly TimeSpan Short = TimeSpan.FromSeconds(5);

    private readonly string _work = Path.Combine(TestPaths.OutputRoot, "denetim-kanali", Guid.NewGuid().ToString("n"));

    public DenetimKanaliTests() => Directory.CreateDirectory(_work);

    public void Dispose()
    {
        try { Directory.Delete(_work, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string NewChannel() => "test-denetim-" + Guid.NewGuid().ToString("n");

    private sealed class SahteKonak : IControlHost
    {
        internal readonly List<string> Calls = new();
        internal bool Loaded { get; set; } = true;
        internal bool Recording { get; private set; }
        internal double? SoughtTo { get; private set; }

        public ControlStatus Status()
            => new(Recording ? ControlProtocol.RecorderRunning : ControlProtocol.RecorderIdle, null, Loaded ? "a.mp4" : null, false, SoughtTo ?? 0, false);

        public bool Play() { Calls.Add("play"); return Loaded; }

        public bool Pause() { Calls.Add("pause"); return Loaded; }

        public bool Seek(double seconds) { Calls.Add("seek"); SoughtTo = seconds; return Loaded; }

        public Task<ControlReply> OpenAsync(string path) { Calls.Add("open:" + path); return Task.FromResult(ControlReply.Success(Status())); }

        public Task<ControlReply> StartRecordingAsync()
        {
            Calls.Add("record-start");
            if (Recording) return Task.FromResult(ControlReply.Failure(ControlErrors.Busy));
            Recording = true;
            return Task.FromResult(ControlReply.Success(Status()));
        }

        public Task<ControlReply> StopRecordingAsync()
        {
            Calls.Add("record-stop");
            if (!Recording) return Task.FromResult(ControlReply.Failure(ControlErrors.NotReady));
            Recording = false;
            return Task.FromResult(ControlReply.Success(Status()));
        }

        public bool SubmitShrink(ShrinkRequest request) { Calls.Add($"shrink:{request.TargetMegabytes}:{request.Path}"); return true; }
    }

    private static ControlDispatcher Acik(IControlHost host) => new(() => true, c => ControlCommands.RunAsync(host, c));

    [Fact]
    public void Komut_satiri_gidip_geliyor_ve_dizi_komut_sayilmiyor()
    {
        var giden = new ControlCommand(ControlCommandNames.EnqueueShrink) { Id = "7", Path = "C:\\v\\a b.mp4", TargetMegabytes = 25 };
        var cozulen = ControlProtocol.Parse(ControlProtocol.Serialize(giden), out var hata);

        Assert.Null(hata);
        Assert.Equal(giden, cozulen);
        Assert.False(ControlProtocol.IsCommandLine("[\"C:\\\\a.mp4\"]"));
        Assert.True(ControlProtocol.IsCommandLine("  {\"command\":\"status\"}"));
    }

    [Theory]
    [InlineData("{\"command\":\"ucur\"}", ControlErrors.UnknownCommand)]
    [InlineData("{\"command\":\"seek\"}", ControlErrors.MissingArgument)]
    [InlineData("{\"command\":\"seek\",\"seconds\":-1}", ControlErrors.BadArgument)]
    [InlineData("{\"command\":\"open\"}", ControlErrors.MissingArgument)]
    [InlineData("{\"command\":\"enqueue-shrink\",\"path\":\"a.mp4\"}", ControlErrors.MissingArgument)]
    [InlineData("{\"command\":", ControlErrors.Malformed)]
    [InlineData("{}", ControlErrors.Malformed)]
    public void Bozuk_komut_gerekcesiyle_reddediliyor(string satir, string beklenen)
    {
        Assert.Null(ControlProtocol.Parse(satir, out var hata));
        Assert.Equal(beklenen, hata);
    }

    [Fact]
    public void Eski_surumun_HATA_satiri_desteklenmiyor_okunuyor()
        => Assert.Equal(ControlErrors.Unsupported, ControlProtocol.ParseReply(SingleInstanceChannel.Nack).Error);

    [Fact]
    public void Durum_sure_ve_basligi_tasir_bossa_null_yazar()
    {
        var dolu = new ControlStatus(ControlProtocol.RecorderIdle, null, "/a.mp4", true, 3, false) { DurationSeconds = 61.23456, Title = "Başlık" };
        var satir = ControlProtocol.SerializeReply(ControlReply.Success(dolu));
        Assert.Contains("\"durationSeconds\":61.235", satir);
        var geri = ControlProtocol.ParseReply(satir).Status!;
        Assert.Equal(61.235, geri.DurationSeconds!.Value, 6);
        Assert.Equal("Başlık", geri.Title);

        var bos = ControlProtocol.SerializeReply(ControlReply.Success(dolu with { DurationSeconds = null, Title = null }));
        Assert.Contains("\"durationSeconds\":null", bos);
        Assert.Contains("\"title\":null", bos);
        var bosGeri = ControlProtocol.ParseReply(bos).Status!;
        Assert.Null(bosGeri.DurationSeconds);
        Assert.Null(bosGeri.Title);
    }

    [Fact]
    public void Kanal_acikken_boru_ustunden_komut_ve_yanit_geliyor()
    {
        var channel = NewChannel();
        var konak = new SahteKonak();
        using var owner = new SingleInstanceChannel(channel);
        owner.StartListening(_ => true, Acik(konak));

        var durum = ControlClient.Send(channel, new ControlCommand(ControlCommandNames.Status) { Id = "d1" }, Short, Short);
        var konum = ControlClient.Send(channel, new ControlCommand(ControlCommandNames.Seek) { Seconds = 12.5 }, Short, Short);

        Assert.True(durum.Ok);
        Assert.Equal("d1", durum.Id);
        Assert.Equal("a.mp4", durum.Status!.PlayerPath);
        Assert.True(konum.Ok);
        Assert.Equal(12.5, konak.SoughtTo);
        Assert.Equal(12.5, konum.Status!.PositionSeconds);
    }

    [Fact]
    public void Kanal_kapaliyken_komut_reddediliyor_yol_iletimi_suruyor()
    {
        var channel = NewChannel();
        var gelen = new List<string>();
        using var owner = new SingleInstanceChannel(channel);
        using var second = new SingleInstanceChannel(channel);
        owner.StartListening(paths => { lock (gelen) gelen.AddRange(paths); return true; });

        var yanit = ControlClient.Send(channel, new ControlCommand(ControlCommandNames.RecordStart), Short, Short);

        Assert.False(yanit.Ok);
        Assert.Equal(ControlErrors.Disabled, yanit.Error);
        Assert.True(second.Forward(new[] { "C:\\yok\\a.mp4" }, Short, Short));
        lock (gelen) Assert.Equal(new[] { "C:\\yok\\a.mp4" }, gelen);
    }

    [Fact]
    public void Kapali_dagitici_konaga_hic_ulasmiyor()
    {
        var cagrildi = false;
        var dagitici = new ControlDispatcher(() => false, _ => { cagrildi = true; return Task.FromResult(ControlReply.Success()); });

        var yanit = ControlProtocol.ParseReply(dagitici.Handle("{\"command\":\"record-start\"}"));

        Assert.Equal(ControlErrors.Disabled, yanit.Error);
        Assert.False(cagrildi);
    }

    [Fact]
    public void Bitmeyen_komut_zaman_asimi_aliyor()
    {
        var dagitici = new ControlDispatcher(() => true, _ => new TaskCompletionSource<ControlReply>().Task, TimeSpan.FromMilliseconds(100));

        Assert.Equal(ControlErrors.Timeout, ControlProtocol.ParseReply(dagitici.Handle("{\"command\":\"status\"}")).Error);
    }

    [Fact]
    public void Sahipsiz_kanal_ulasilamaz_donuyor()
        => Assert.Equal(ControlErrors.Unreachable,
            ControlClient.Send(NewChannel(), new ControlCommand(ControlCommandNames.Status), TimeSpan.FromMilliseconds(200), Short).Error);

    [Fact]
    public void Baslatma_bayragi_yoksa_kanal_kapali()
    {
        Assert.Null(Program.ControlFor(new[] { "C:\\a.mp4" }, new ControlBridge()));
        Assert.NotNull(Program.ControlFor(new[] { ControlSwitch.Flag }, new ControlBridge()));
        Assert.NotNull(Program.ControlFor(new[] { ControlSwitch.EnglishFlag }, new ControlBridge()));
    }

    [Fact]
    public async Task Pencere_baglanmadan_gelen_komut_hazir_degil_aliyor()
        => Assert.Equal(ControlErrors.NotReady, (await new ControlBridge().ExecuteAsync(new ControlCommand(ControlCommandNames.Status))).Error);

    [Fact]
    public async Task Oynatici_komutlari_bos_oynaticida_hazir_degil()
    {
        var konak = new SahteKonak { Loaded = false };

        var yanit = await ControlCommands.RunAsync(konak, new ControlCommand(ControlCommandNames.Play));

        Assert.Equal(ControlErrors.NotReady, yanit.Error);
        Assert.Equal(new[] { "play" }, konak.Calls);
    }

    [Fact]
    public async Task Kucultme_istegi_kabuk_kurallariyla_siniyor()
    {
        var dosya = Path.Combine(_work, "a.mp4");
        File.WriteAllBytes(dosya, new byte[] { 0 });
        var hedef = ShellIntegration.AcceptedShrinkTargetsMegabytes[0];
        var konak = new SahteKonak();

        var listeDisi = await ControlCommands.RunAsync(konak, new ControlCommand(ControlCommandNames.EnqueueShrink) { Path = dosya, TargetMegabytes = 3 });
        var yolsuz = await ControlCommands.RunAsync(konak, new ControlCommand(ControlCommandNames.EnqueueShrink) { Path = dosya + ".yok", TargetMegabytes = hedef });
        var gecerli = await ControlCommands.RunAsync(konak, new ControlCommand(ControlCommandNames.EnqueueShrink) { Path = dosya, TargetMegabytes = hedef });

        Assert.Equal(nameof(ShrinkArgumentProblem.TargetNotInQuickList), listeDisi.Detail);
        Assert.Equal(nameof(ShrinkArgumentProblem.NoPath), yolsuz.Detail);
        Assert.True(gecerli.Ok);
        Assert.Equal(new[] { $"shrink:{hedef}:{dosya}" }, konak.Calls);
        Assert.Equal(new[] { ShellIntegration.ShrinkFlag, hedef.ToString(System.Globalization.CultureInfo.InvariantCulture), dosya },
            ShrinkRequestResolver.ShellArguments(new ShrinkRequest(hedef, dosya)));
    }

    [Fact]
    public async Task Olmayan_dosya_acilmiyor()
    {
        var konak = new SahteKonak();

        var yanit = await ControlCommands.RunAsync(konak, new ControlCommand(ControlCommandNames.Open) { Path = Path.Combine(_work, "yok.mp4") });

        Assert.Equal(ControlErrors.BadArgument, yanit.Error);
        Assert.Empty(konak.Calls);
    }

    [Fact]
    public async Task Komut_satiri_boru_ustunden_kaydi_baslatip_durduruyor()
    {
        var channel = NewChannel();
        var konak = new SahteKonak();
        using var owner = new SingleInstanceChannel(channel);
        owner.StartListening(_ => true, Acik(konak));
        var hizmet = new CliServices { Control = c => ControlClient.Send(channel, c, Short, Short) };

        async Task<(int Exit, string Out, string Err)> Kos(params string[] args)
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exit = await CliApp.RunAsync(args, stdout, stderr, CliText.ForLanguage("tr"), hizmet, CancellationToken.None);
            return (exit, stdout.ToString(), stderr.ToString());
        }

        var basla = await Kos("denetim", "kayit-baslat");
        var durum = await Kos("denetim", "durum", "--json");
        var ikinci = await Kos("control", "record-start");
        var dur = await Kos("denetim", "kayit-durdur");

        Assert.Equal(ExitCodes.InBand, basla.Exit);
        Assert.Contains(ControlProtocol.RecorderRunning, basla.Out);
        Assert.Equal(ExitCodes.InBand, durum.Exit);
        Assert.True(ControlProtocol.ParseReply(durum.Out.Trim()).Status!.Recorder == ControlProtocol.RecorderRunning);
        Assert.Equal(ExitCodes.Error, ikinci.Exit);
        Assert.Contains(CliText.ForLanguage("tr").Format("control.busy", ControlErrors.Busy), ikinci.Err);
        Assert.Equal(ExitCodes.InBand, dur.Exit);
        Assert.False(konak.Recording);
        Assert.Equal(new[] { "record-start", "record-start", "record-stop" }, konak.Calls);
    }

    [Fact]
    public async Task Kapali_pencereye_komut_satiri_nedenini_soyluyor()
    {
        var channel = NewChannel();
        using var owner = new SingleInstanceChannel(channel);
        owner.StartListening(_ => true);
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(new[] { "denetim", "kayit-baslat" }, new StringWriter(), stderr, CliText.ForLanguage("tr"),
            new CliServices { Control = c => ControlClient.Send(channel, c, Short, Short) }, CancellationToken.None);

        Assert.Equal(ExitCodes.Error, exit);
        Assert.Contains(CliText.ForLanguage("tr")["control.disabled"], stderr.ToString());
    }

    [Theory]
    [InlineData("denetim")]
    [InlineData("denetim", "ucur")]
    [InlineData("denetim", "durum", "kayit-baslat")]
    public async Task Eksik_ya_da_bilinmeyen_is_kullanim_hatasi(params string[] args)
    {
        var gonderildi = false;
        var exit = await CliApp.RunAsync(args, new StringWriter(), new StringWriter(), CliText.ForLanguage("tr"),
            new CliServices { Control = _ => { gonderildi = true; return ControlReply.Success(); } }, CancellationToken.None);

        Assert.Equal(ExitCodes.Usage, exit);
        Assert.False(gonderildi);
    }
}
