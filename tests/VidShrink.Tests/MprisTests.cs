using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Tmds.DBus.Protocol;
using VidShrink.App.Integration;
using VidShrink.Core;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// CI'nın <c>mpris-linux</c> işi koşturur: Linux, oturum veriyolu (<c>dbus-run-session</c>) ve
/// <c>VIDSHRINK_MPRIS_CANLI=1</c>. Değişken verilmişse veriyolu yokluğu atlama değil kırmızıdır.
/// </summary>
public sealed class MprisCanliFactAttribute : FactAttribute
{
    public MprisCanliFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("VIDSHRINK_MPRIS_CANLI") == "1") return;
        if (!OperatingSystem.IsLinux())
            Skip = "MPRIS oturum veriyolunda ölçülür; bu makine Linux değil (CI'nın mpris-linux işi koşturur).";
        else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS")))
            Skip = "DBUS_SESSION_BUS_ADDRESS yok, oturum veriyolu olmadan MPRIS ölçülemez.";
    }
}

/// <summary>libmpv dosyası <c>VIDSHRINK_LIBMPV</c> ile verilmişse koşar (yerelde ve CI'da verilir).</summary>
public sealed class LibMpvDosyasiFactAttribute : FactAttribute
{
    public LibMpvDosyasiFactAttribute()
    {
        var yol = Environment.GetEnvironmentVariable(LibMpvLocator.EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(yol) || !File.Exists(yol)) Skip = "VIDSHRINK_LIBMPV verilmedi ya da dosya yok.";
    }
}

public sealed class MprisTests
{
    private const string Dosya = "/medya/klip.mp4";

    private static ControlStatus Durum(string? yol = Dosya, bool oynuyor = false, double konum = 10, double? sure = 60, string? baslik = null)
        => new(ControlProtocol.RecorderIdle, null, yol, oynuyor, konum, false) { DurationSeconds = sure, Title = baslik };

    [Theory]
    [InlineData(false, null, false)]
    [InlineData(true, null, true)]
    [InlineData(true, "1", true)]
    [InlineData(true, "0", false)]
    [InlineData(true, " 0 ", false)]
    public void Yayin_yalniz_Linuxta_ve_0_verilmedikce_acik(bool linux, string? deger, bool beklenen)
        => Assert.Equal(beklenen, Mpris.Enabled(linux, deger));

    [Fact]
    public void Ilk_ornek_kisa_adi_ikincisi_ornek_ekini_alir()
    {
        Assert.Equal("org.mpris.MediaPlayer2.vidshrinkplayer", Mpris.BusName(null));
        Assert.Equal("org.mpris.MediaPlayer2.vidshrinkplayer.instance42", Mpris.BusName(42));
    }

    [Fact]
    public void Oynatma_durumu_yukluluk_ve_oynamadan_cikar()
    {
        Assert.Equal(Mpris.Stopped, Mpris.PlaybackStatus(null));
        Assert.Equal(Mpris.Stopped, Mpris.PlaybackStatus(Durum(yol: null, oynuyor: true)));
        Assert.Equal(Mpris.Playing, Mpris.PlaybackStatus(Durum(oynuyor: true)));
        Assert.Equal(Mpris.Paused, Mpris.PlaybackStatus(Durum()));
    }

    [Fact]
    public void Mikrosaniye_cevrimi_gecersizi_sifirlar()
    {
        Assert.Equal(1_500_000, Mpris.Microseconds(1.5));
        Assert.Equal(0, Mpris.Microseconds(null));
        Assert.Equal(0, Mpris.Microseconds(double.NaN));
        Assert.Equal(0, Mpris.Microseconds(-3));
        Assert.Equal(2.25, Mpris.Seconds(2_250_000));
    }

    [Fact]
    public void Iz_kimligi_yola_bagli_ve_gecerli_nesne_yolu()
    {
        Assert.Equal(Mpris.NoTrack, Mpris.TrackId(null));
        var bir = Mpris.TrackId(Dosya);
        Assert.Equal(bir, Mpris.TrackId(Dosya));
        Assert.NotEqual(bir, Mpris.TrackId("/medya/baska.mp4"));
        Assert.Matches(new Regex("^/org/mpris/MediaPlayer2/vidshrinkplayer/track/t[0-9a-f]{16}$"), bir);
    }

    [Fact]
    public void Ust_veri_basligi_dosya_adina_duser_ve_uzunlugu_tasir()
    {
        var bos = Mpris.Metadata(null);
        Assert.Equal(new MprisMetadata(Mpris.NoTrack, null, null, 0), bos);

        var adli = Mpris.Metadata(Durum());
        Assert.Equal("klip.mp4", adli.Title);
        Assert.Equal(60_000_000, adli.LengthMicroseconds);
        Assert.Equal(Mpris.TrackId(Dosya), adli.TrackId);

        Assert.Equal("Asıl Başlık", Mpris.Metadata(Durum(baslik: "  Asıl Başlık ")).Title);
        Assert.Equal(0, Mpris.Metadata(Durum(sure: null)).LengthMicroseconds);
    }

    [Fact]
    public void Adres_kendisi_yerel_yol_dosya_uri_si_olur_ve_geri_doner()
    {
        const string adres = "https://ornek.test/akis.m3u8";
        Assert.Equal(adres, Mpris.Url(adres));
        Assert.Equal(adres, Mpris.PathFromUri(adres));

        var yerel = Path.Combine(TestPaths.OutputRoot, "mpris", "a b.mp4");
        var uri = Mpris.Url(yerel);
        Assert.StartsWith("file:///", uri);
        Assert.Contains("a%20b.mp4", uri);
        Assert.Equal(Path.GetFullPath(yerel), Mpris.PathFromUri(uri));

        Assert.Null(Mpris.Url(" "));
        Assert.Null(Mpris.PathFromUri("mailto:biri@ornek.test"));
        Assert.Null(Mpris.PathFromUri("yol değil"));
        Assert.Null(Mpris.PathFromUri(null));
    }

    [Fact]
    public void Yontemler_yuklu_degilken_bir_sey_yapmaz()
    {
        foreach (var uye in new[] { Mpris.MethodPlay, Mpris.MethodPause, Mpris.MethodPlayPause, Mpris.MethodStop, Mpris.MethodSeek, Mpris.MethodSetPosition })
            Assert.Null(Mpris.Command(uye, Durum(yol: null), 1_000_000, Mpris.NoTrack));
        Assert.Null(Mpris.Command(Mpris.MethodPlay, null));
    }

    [Fact]
    public void Oynat_duraklat_ve_durdur_denetim_komutuna_esler()
    {
        Assert.Equal(ControlCommandNames.Play, Mpris.Command(Mpris.MethodPlay, Durum())!.Name);
        Assert.Equal(ControlCommandNames.Pause, Mpris.Command(Mpris.MethodPause, Durum(oynuyor: true))!.Name);
        Assert.Equal(ControlCommandNames.Pause, Mpris.Command(Mpris.MethodStop, Durum(oynuyor: true))!.Name);
        Assert.Equal(ControlCommandNames.Pause, Mpris.Command(Mpris.MethodPlayPause, Durum(oynuyor: true))!.Name);
        Assert.Equal(ControlCommandNames.Play, Mpris.Command(Mpris.MethodPlayPause, Durum(oynuyor: false))!.Name);
        Assert.Null(Mpris.Command(Mpris.MethodNext, Durum()));
        Assert.Null(Mpris.Command(Mpris.MethodPrevious, Durum()));
    }

    [Theory]
    [InlineData(5_000_000, 15)]
    [InlineData(-4_000_000, 6)]
    [InlineData(-30_000_000, 0)]
    [InlineData(90_000_000, 60)]
    public void Seek_goreli_ve_sinira_kirpilir(long mikro, double beklenen)
    {
        var komut = Mpris.Command(Mpris.MethodSeek, Durum(), mikro)!;
        Assert.Equal(ControlCommandNames.Seek, komut.Name);
        Assert.Equal(beklenen, komut.Seconds!.Value, 6);
    }

    [Fact]
    public void SetPosition_iz_kimligi_tutmazsa_ya_da_sinir_disiysa_yok_sayilir()
    {
        var iz = Mpris.TrackId(Dosya);
        var komut = Mpris.Command(Mpris.MethodSetPosition, Durum(), 42_000_000, iz)!;
        Assert.Equal(ControlCommandNames.Seek, komut.Name);
        Assert.Equal(42, komut.Seconds!.Value, 6);

        Assert.Null(Mpris.Command(Mpris.MethodSetPosition, Durum(), 42_000_000, Mpris.TrackId("/medya/baska.mp4")));
        Assert.Null(Mpris.Command(Mpris.MethodSetPosition, Durum(), 42_000_000, null));
        Assert.Null(Mpris.Command(Mpris.MethodSetPosition, Durum(), -1, iz));
        Assert.Null(Mpris.Command(Mpris.MethodSetPosition, Durum(), 61_000_000, iz));
    }

    [Fact]
    public void OpenUri_yolu_ya_da_adresi_acar()
    {
        var yerel = Path.Combine(TestPaths.OutputRoot, "mpris", "ac.mkv");
        var komut = Mpris.Command(Mpris.MethodOpenUri, null, argument: new Uri(Path.GetFullPath(yerel)).AbsoluteUri)!;
        Assert.Equal(ControlCommandNames.Open, komut.Name);
        Assert.Equal(Path.GetFullPath(yerel), komut.Path);

        Assert.Equal("https://ornek.test/v.mp4", Mpris.Command(Mpris.MethodOpenUri, null, argument: "https://ornek.test/v.mp4")!.Path);
        Assert.Null(Mpris.Command(Mpris.MethodOpenUri, Durum(), argument: "mailto:biri@ornek.test"));
    }

    [Fact]
    public void Player_yontem_kumesi_kapali()
    {
        foreach (var uye in new[] { "Play", "Pause", "PlayPause", "Stop", "Seek", "SetPosition", "OpenUri", "Next", "Previous" })
            Assert.True(Mpris.IsPlayerMethod(uye), uye);
        Assert.False(Mpris.IsPlayerMethod("Raise"));
        Assert.False(Mpris.IsPlayerMethod("play"));
    }

    [Fact]
    public void Degisen_ozellikler_konumu_saymaz()
    {
        Assert.Empty(Mpris.Changed(Durum(), Durum(konum: 30)));
        Assert.Equal(new[] { Mpris.PropertyPlaybackStatus }, Mpris.Changed(Durum(), Durum(oynuyor: true)));
        Assert.Equal(new[] { Mpris.PropertyMetadata }, Mpris.Changed(Durum(), Durum(yol: "/medya/baska.mp4")));
        Assert.Equal(new[] { Mpris.PropertyPlaybackStatus, Mpris.PropertyMetadata }, Mpris.Changed(null, Durum(oynuyor: true)));
        Assert.Empty(Mpris.Changed(null, null));
    }

    [Fact]
    public void Atlama_beklenen_konumdan_sapmayla_anlasilir()
    {
        var bir = TimeSpan.FromSeconds(1);
        Assert.False(Mpris.Seeked(Durum(oynuyor: true, konum: 10), Durum(oynuyor: true, konum: 11), bir));
        Assert.True(Mpris.Seeked(Durum(oynuyor: true, konum: 10), Durum(oynuyor: true, konum: 25), bir));
        Assert.True(Mpris.Seeked(Durum(konum: 10), Durum(konum: 8), bir));
        Assert.False(Mpris.Seeked(Durum(konum: 10), Durum(konum: 10), bir));
        Assert.False(Mpris.Seeked(Durum(konum: 10), Durum(yol: "/medya/baska.mp4", konum: 50), bir));
        Assert.False(Mpris.Seeked(null, Durum(), bir));
    }

    [Fact]
    public void Desteklenen_semalar_dosya_ve_adres_semalari()
    {
        Assert.Equal("file", Mpris.UriSchemes[0]);
        foreach (var sema in MediaAddress.Schemes) Assert.Contains(sema, Mpris.UriSchemes);
        Assert.Contains("video/mp4", Mpris.MimeTypes);
    }

    [LibMpvDosyasiFact]
    public void Paketteki_libmpv_MPRIS_adi_tasimiyor()
    {
        var yol = Environment.GetEnvironmentVariable(LibMpvLocator.EnvironmentVariable)!;
        Assert.False(Iceriyor(yol, Encoding.ASCII.GetBytes("org.mpris.MediaPlayer2")), $"{yol} MPRIS adı taşıyor; libmpv'nin kendi yayını yeniden ölçülmeli.");
        Assert.True(Iceriyor(yol, Encoding.ASCII.GetBytes("mpv_create")), $"{yol} libmpv değil; tarama yanlış dosyayı okuyor.");
        if (OperatingSystem.IsWindows())
            Assert.True(Iceriyor(yol, Encoding.ASCII.GetBytes("media-controls")), $"{yol} media-controls seçeneğini taşımıyor; sürüm beklenenden eski.");
    }

    private static bool Iceriyor(string yol, byte[] aranan)
    {
        var tampon = new byte[1 << 20];
        var kalan = 0;
        using var akis = File.OpenRead(yol);
        while (true)
        {
            var okunan = akis.Read(tampon, kalan, tampon.Length - kalan);
            if (okunan == 0) return false;
            var dolu = kalan + okunan;
            if (tampon.AsSpan(0, dolu).IndexOf(aranan) >= 0) return true;
            kalan = Math.Min(aranan.Length - 1, dolu);
            Array.Copy(tampon, dolu - kalan, tampon, 0, kalan);
        }
    }

    private const int SecenekYok = -5;

    [MprisCanliFact]
    public async Task Linux_libmpv_media_controls_acikken_veriyolunda_MPRIS_adi_belirmiyor()
    {
        LibMpvLocator.EnsureLoaded();
        using var istemci = await Baglan();
        var once = await istemci.ListServicesAsync();
        var mpv = Native.mpv_create();
        Assert.NotEqual(IntPtr.Zero, mpv);
        try
        {
            Assert.Equal(0, Native.mpv_set_option_string(mpv, "vo", "null"));
            Assert.Equal(0, Native.mpv_set_option_string(mpv, "ao", "null"));
            Assert.Equal(0, Native.mpv_set_option_string(mpv, "load-scripts", "no"));
            Assert.Contains(Native.mpv_set_option_string(mpv, "media-controls", "yes"), new[] { 0, SecenekYok });
            Assert.Equal(0, Native.mpv_initialize(mpv));
            var mpris = Array.Empty<string>();
            for (var tur = 0; tur < 15; tur++)
            {
                await Task.Delay(200);
                mpris = (await istemci.ListServicesAsync()).Where(ad => ad.StartsWith("org.mpris.", StringComparison.Ordinal)).Except(once).ToArray();
                if (mpris.Length > 0) break;
            }
            Assert.Empty(mpris);
        }
        finally
        {
            Native.mpv_terminate_destroy(mpv);
        }
    }

    [MprisCanliFact]
    public async Task Sunucu_veriyolunda_ozellikleri_verir_ve_yontemleri_denetime_iletir()
    {
        var sahte = new SahteOynatici(Durum(konum: 10));
        using var sunucu = new MprisServer(sahte.Yurut) { PollInterval = TimeSpan.FromMilliseconds(200) };
        using var ikinci = new MprisServer(sahte.Yurut);
        Assert.True(await sunucu.StartAsync(DBusAddress.Session, secondary: false));
        Assert.True(await ikinci.StartAsync(DBusAddress.Session, secondary: false));
        Assert.StartsWith(Mpris.BusName(null), sunucu.BusName);
        Assert.Equal(Mpris.BusName(Environment.ProcessId), ikinci.BusName);
        var hedef = sunucu.BusName!;

        using var istemci = await Baglan();
        Assert.Contains(hedef, await istemci.ListServicesAsync());

        var xml = await istemci.CallMethodAsync(Mesaj(istemci, hedef, Mpris.IntrospectableInterface, "Introspect", null), (m, _) => m.GetBodyReader().ReadString(), null);
        Assert.Contains("org.mpris.MediaPlayer2.Player", xml);

        Assert.Equal(Mpris.Paused, (await Al(istemci, hedef, Mpris.PlayerInterface, Mpris.PropertyPlaybackStatus)).GetString());
        Assert.Equal(10_000_000, (await Al(istemci, hedef, Mpris.PlayerInterface, Mpris.PropertyPosition)).GetInt64());
        Assert.Equal(Mpris.Identity, (await Al(istemci, hedef, Mpris.RootInterface, "Identity")).GetString());

        var hepsi = await istemci.CallMethodAsync(Mesaj(istemci, hedef, Mpris.PropertiesInterface, "GetAll", "s", Mpris.PlayerInterface),
            (m, _) => m.GetBodyReader().ReadDictionaryOfStringToVariantValue(), null);
        Assert.True(hepsi["CanSeek"].GetBool());
        Assert.False(hepsi["CanGoNext"].GetBool());
        var ust = hepsi[Mpris.PropertyMetadata].GetDictionary<string, VariantValue>();
        Assert.Equal(Mpris.TrackId(Dosya), ust["mpris:trackid"].GetObjectPathAsString());
        Assert.Equal(60_000_000, ust["mpris:length"].GetInt64());
        Assert.Equal("klip.mp4", ust["xesam:title"].GetString());

        var hata = await Assert.ThrowsAsync<DBusErrorReplyException>(() => Al(istemci, hedef, Mpris.PlayerInterface, "Yok"));
        Assert.Equal("org.freedesktop.DBus.Error.UnknownProperty", hata.ErrorName);

        var degisen = new ConcurrentQueue<string>();
        var atlanan = new ConcurrentQueue<long>();
        using var izle = await istemci.WatchSignalAsync(hedef, Mpris.ObjectPath, Mpris.PropertiesInterface, "PropertiesChanged",
            (m, _) =>
            {
                var okuyucu = m.GetBodyReader();
                okuyucu.ReadString();
                var degerler = okuyucu.ReadDictionaryOfStringToVariantValue();
                return degerler.TryGetValue(Mpris.PropertyPlaybackStatus, out var deger) ? deger.GetString() : "";
            },
            (Notification<string> bildirim) => { if (bildirim.HasValue) degisen.Enqueue(bildirim.Value); }, ObserverFlags.None);
        using var izleAtlama = await istemci.WatchSignalAsync(hedef, Mpris.ObjectPath, Mpris.PlayerInterface, "Seeked",
            (m, _) => m.GetBodyReader().ReadInt64(), (Notification<long> bildirim) => { if (bildirim.HasValue) atlanan.Enqueue(bildirim.Value); }, ObserverFlags.None);

        await istemci.CallMethodAsync(Mesaj(istemci, hedef, Mpris.PlayerInterface, Mpris.MethodPlayPause, null));
        Assert.Equal(ControlCommandNames.Play, sahte.SonKomut()!.Name);
        Assert.True(await Bekle(() => degisen.Contains(Mpris.Playing)), "PropertiesChanged Playing duyurmadı.");

        await istemci.CallMethodAsync(Mesaj(istemci, hedef, Mpris.PlayerInterface, Mpris.MethodSeek, "x", 5_000_000L));
        var atlama = sahte.SonKomut()!;
        Assert.Equal(ControlCommandNames.Seek, atlama.Name);
        Assert.InRange(atlama.Seconds!.Value, 14.9, 17);
        Assert.True(await Bekle(() => !atlanan.IsEmpty), "Seeked sinyali gelmedi.");

        var sayi = sahte.KomutSayisi;
        await istemci.CallMethodAsync(Mesaj(istemci, hedef, Mpris.PlayerInterface, Mpris.MethodSetPosition, "ox", new ObjectPath(Mpris.TrackId("/medya/baska.mp4")), 1_000_000L));
        Assert.Equal(sayi, sahte.KomutSayisi);

        var yerel = Path.GetFullPath(Path.Combine(TestPaths.OutputRoot, "mpris", "ac.mkv"));
        await istemci.CallMethodAsync(Mesaj(istemci, hedef, Mpris.PlayerInterface, Mpris.MethodOpenUri, "s", new Uri(yerel).AbsoluteUri));
        Assert.Equal(ControlCommandNames.Open, sahte.SonKomut()!.Name);
        Assert.Equal(yerel, sahte.SonKomut()!.Path);

        var acilmaz = await Assert.ThrowsAsync<DBusErrorReplyException>(() => istemci.CallMethodAsync(Mesaj(istemci, hedef, Mpris.PlayerInterface, Mpris.MethodOpenUri, "s", "mailto:biri@ornek.test")));
        Assert.Equal("org.freedesktop.DBus.Error.InvalidArgs", acilmaz.ErrorName);

        var yazilamaz = await Assert.ThrowsAsync<DBusErrorReplyException>(() => istemci.CallMethodAsync(Mesaj(istemci, hedef, Mpris.PropertiesInterface, "Set", "ss", Mpris.PlayerInterface, Mpris.PropertyPlaybackStatus)));
        Assert.Equal("org.freedesktop.DBus.Error.PropertyReadOnly", yazilamaz.ErrorName);
    }

    private static async Task<DBusConnection> Baglan()
    {
        var adres = DBusAddress.Session;
        Assert.False(string.IsNullOrWhiteSpace(adres), "oturum veriyolu adresi yok");
        var baglanti = new DBusConnection(adres!);
        await baglanti.ConnectAsync();
        return baglanti;
    }

    private static Task<VariantValue> Al(DBusConnection istemci, string hedef, string arayuz, string ozellik)
        => istemci.CallMethodAsync(Mesaj(istemci, hedef, Mpris.PropertiesInterface, "Get", "ss", arayuz, ozellik), (m, _) => m.GetBodyReader().ReadVariantValue(), null);

    private static MessageBuffer Mesaj(DBusConnection baglanti, string hedef, string arayuz, string uye, string? imza, params object[] degerler)
    {
        using var yazici = baglanti.GetMessageWriter();
        yazici.WriteMethodCallHeader(hedef, Mpris.ObjectPath, arayuz, uye, imza, MessageFlags.None);
        foreach (var deger in degerler)
            switch (deger)
            {
                case string metin: yazici.WriteString(metin); break;
                case long sayi: yazici.WriteInt64(sayi); break;
                case ObjectPath yol: yazici.WriteObjectPath(yol); break;
                default: throw new ArgumentOutOfRangeException(nameof(degerler), deger, null);
            }
        return yazici.CreateMessage();
    }

    private static async Task<bool> Bekle(Func<bool> kosul)
    {
        for (var tur = 0; tur < 50; tur++)
        {
            if (kosul()) return true;
            await Task.Delay(100);
        }
        return kosul();
    }

    private sealed class SahteOynatici
    {
        private readonly object _kilit = new();
        private readonly List<ControlCommand> _komutlar = new();
        private ControlStatus _durum;

        public SahteOynatici(ControlStatus durum) => _durum = durum;

        public int KomutSayisi
        {
            get { lock (_kilit) return _komutlar.Count; }
        }

        public ControlCommand? SonKomut()
        {
            lock (_kilit) return _komutlar.Count == 0 ? null : _komutlar[^1];
        }

        public Task<ControlReply> Yurut(ControlCommand komut)
        {
            lock (_kilit)
            {
                if (komut.Name == ControlCommandNames.Status) return Task.FromResult(ControlReply.Success(_durum));
                _komutlar.Add(komut);
                _durum = komut.Name switch
                {
                    ControlCommandNames.Play => _durum with { Playing = true },
                    ControlCommandNames.Pause => _durum with { Playing = false },
                    ControlCommandNames.Seek => _durum with { PositionSeconds = komut.Seconds ?? _durum.PositionSeconds },
                    ControlCommandNames.Open => _durum with { PlayerPath = komut.Path, PositionSeconds = 0 },
                    _ => _durum
                };
                return Task.FromResult(ControlReply.Success(_durum));
            }
        }
    }
}
