using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using Xunit;
using L = VidShrink.Tests.OynaticiListeTests;

namespace VidShrink.Tests;

/// <summary>
/// OYB-P3: liste penceresinde yazdıkça süzme, ada ve tarihe göre dizme, ters çevirme,
/// yukarı ve aşağı taşıma, satırda geçmişten gelen kalınan yer ve süre.
/// </summary>
public sealed class OynaticiListeAramaTests
{
    private static readonly string[] YeniAnahtarlar =
    {
        "player.list.search", "player.list.item.move-up", "player.list.item.move-down",
        "player.list.sort", "player.list.sort-name", "player.list.sort-date", "player.list.sort-reverse",
    };

    private static string Yol(string ad) => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "kuyruk", ad + ".mp4"));

    private static IReadOnlyList<string> Liste(params string[] adlar) => adlar.Select(Yol).ToArray();

    private static string? Birlestir(IReadOnlyList<string>? liste)
        => liste is null ? null : string.Join(",", liste.Select(Path.GetFileNameWithoutExtension));

    private static string[] Adlar(PlayerView view) => view.PlaylistFiles().Select(Path.GetFileName).ToArray()!;

    private static string[] Satirlar(PlayerView view)
        => view.Playlist.Items.OfType<ListBoxItem>().Select(i => Path.GetFileName((string)i.Tag!)).ToArray();

    [Theory]
    [InlineData("a3", "a1,a3,a2,a4")]
    [InlineData("a2", "a2,a1,a3,a4")]
    [InlineData("a1", null)]
    [InlineData("a9", null)]
    public void KuyrukYukariTasir(string dosya, string? beklenen)
        => Assert.Equal(beklenen, Birlestir(QueueEdit.MoveUp(Liste("a1", "a2", "a3", "a4"), Yol("a2"), Yol(dosya))));

    [Theory]
    [InlineData("a2", "a1,a3,a2,a4")]
    [InlineData("a1", "a2,a1,a3,a4")]
    [InlineData("a4", null)]
    [InlineData("a9", null)]
    public void KuyrukAsagiTasir(string dosya, string? beklenen)
        => Assert.Equal(beklenen, Birlestir(QueueEdit.MoveDown(Liste("a1", "a2", "a3", "a4"), Yol("a2"), Yol(dosya))));

    [Fact]
    public void KuyrukAdaGoreDogalSiraylaDizilir()
        => Assert.Equal("a2,a10,b1,B3", Birlestir(QueueEdit.SortByName(Liste("b1", "a10", "B3", "a2"), Yol("a2"), Yol("a2"))));

    [Fact]
    public void KuyrukTersCevrilir()
        => Assert.Equal("a4,a3,a2,a1", Birlestir(QueueEdit.Reverse(Liste("a1", "a2", "a3", "a4"), Yol("a2"), Yol("a2"))));

    [Fact]
    public void KuyrukTariheGoreYeniBastaDizilir()
    {
        var klasor = L.Klasor("eski.mp4", "orta.mp4", "yeni.mp4");
        try
        {
            var simdi = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(Path.Combine(klasor, "eski.mp4"), simdi.AddDays(-3));
            File.SetLastWriteTimeUtc(Path.Combine(klasor, "orta.mp4"), simdi.AddDays(-2));
            File.SetLastWriteTimeUtc(Path.Combine(klasor, "yeni.mp4"), simdi.AddDays(-1));
            var liste = new[] { "orta", "eski", "yok", "yeni" }.Select(a => Path.Combine(klasor, a + ".mp4")).ToArray();
            Assert.Equal("yeni,orta,eski,yok", Birlestir(QueueEdit.SortByDate(liste, liste[0], liste[0])));
        }
        finally
        {
            L.Sil(klasor);
        }
    }

    [Theory]
    [InlineData("", "Bolum 1,bölüm 2,Film")]
    [InlineData("  ", "Bolum 1,bölüm 2,Film")]
    [InlineData("BÖL", "bölüm 2")]
    [InlineData("bol", "Bolum 1")]
    [InlineData("fİLM", "Film")]
    [InlineData("xyz", "")]
    public void SuzgecAdaBakarHarfBuyuklugunuAyirmaz(string arama, string beklenen)
    {
        var liste = Liste("Bolum 1", "bölüm 2", "Film");
        var dil = VidShrink.App.Localization.Strings.Language;
        VidShrink.App.Localization.Strings.Use("tr");
        try
        {
            var sonuc = PlaylistFilter.Match(liste, arama, Path.GetFileNameWithoutExtension!);
            Assert.Equal(beklenen, string.Join(",", sonuc.Select(Path.GetFileNameWithoutExtension)));
        }
        finally
        {
            VidShrink.App.Localization.Strings.Use(dil);
        }
    }

    [Theory]
    [InlineData(65, 300, "01:05 / 05:00")]
    [InlineData(65, double.NaN, "01:05")]
    [InlineData(0, 300, "05:00")]
    [InlineData(0.5, 4000, "01:06:40")]
    [InlineData(0, double.NaN, null)]
    public void SatirIlerlemesiKalinanYerVeSure(double konum, double sure, string? beklenen)
        => Assert.Equal(beklenen, PlaylistRow.Progress(konum, sure));

    [Fact]
    public void GecmisKalinanYeriVeSureyiVerir()
    {
        var klasor = L.Klasor("a1.mp4", "a2.mp4");
        try
        {
            var gecmis = new PlaybackHistory();
            gecmis.Remember(Path.Combine(klasor, "a1.mp4"), 42, false, 120);
            Assert.Equal((42d, 120d), gecmis.Progress(Path.Combine(klasor, "a1.mp4")));
            Assert.Null(gecmis.Progress(Path.Combine(klasor, "a2.mp4")));
            gecmis.Clear();
            Assert.Null(gecmis.Progress(Path.Combine(klasor, "a1.mp4")));
        }
        finally
        {
            L.Sil(klasor);
        }
    }

    [Fact]
    public void YeniAnahtarlarCevrili()
        => Assert.All(YeniAnahtarlar, k => Assert.NotEqual(k, Strings.Get(k)));

    private static void Kosum(string[] adlar, string calan, Action<PlayerView, Window, string> is_)
    {
        var klasor = L.Klasor(adlar);
        try
        {
            AppHost.Run(() =>
            {
                var view = L.Ac(Path.Combine(klasor, calan), out var window);
                try
                {
                    is_(view, window, klasor);
                }
                finally
                {
                    L.Kapat(view, window);
                }
            });
        }
        finally
        {
            L.Sil(klasor);
        }
    }

    private static void MenuyuAc(PlayerView view, Window window)
    {
        L.SagTik(window, L.Merkez(window, view));
        DenetimSurucu.Pump(view, () => view.MenuOpen && view.PlaylistOpen, 5);
        DenetimSurucu.Wait(view, 0.3);
        Assert.True(view.MenuOpen, "menu acilmadi");
        Assert.True(view.PlaylistOpen, "liste acilmadi");
    }

    private static void SatiraSagTik(PlayerView view, int sira)
    {
        var satir = view.Playlist.ContainerFromIndex(sira)!;
        var kok = TopLevel.GetTopLevel(satir)!;
        var nokta = L.Merkez(kok, satir);
        L.Fareyle(kok, Avalonia.Input.Raw.RawPointerEventType.Move, nokta, Avalonia.Input.RawInputModifiers.None);
        L.Fareyle(kok, Avalonia.Input.Raw.RawPointerEventType.RightButtonDown, nokta, Avalonia.Input.RawInputModifiers.RightMouseButton);
        L.Fareyle(kok, Avalonia.Input.Raw.RawPointerEventType.RightButtonUp, nokta, Avalonia.Input.RawInputModifiers.None);
        DenetimSurucu.Pump(view, () => view.ItemMenuOpen, 5);
        DenetimSurucu.Wait(view, 0.2);
        Assert.True(view.ItemMenuOpen, "oge menusu acilmadi");
    }

    private static MenuItem Oge(PlayerView view, string etiket)
    {
        var ust = view.ItemMenu!.Items.OfType<MenuItem>().ToList();
        return ust.Concat(ust.SelectMany(i => i.Items.OfType<MenuItem>())).Single(i => (string?)i.Tag == etiket);
    }

    private static void Tikla(PlayerView view, string etiket)
    {
        var oge = Oge(view, etiket);
        Assert.True(oge.IsEnabled, etiket + " kapali");
        oge.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = oge });
        DenetimSurucu.Wait(view, 0.2);
    }

    [Fact]
    public void AramaKutusuYazdikcaSuzerVeTemizleninceDoner()
    {
        Kosum(new[] { "dizi-b01.mp4", "dizi-b02.mp4", "film.mp4", "klip.mp4" }, "dizi-b02.mp4", (view, window, _) =>
        {
            MenuyuAc(view, window);
            Assert.Equal("", view.PlaylistSearchBox.Text ?? "");
            Assert.Equal(4, view.Playlist.ItemCount);

            view.PlaylistSearchBox.Text = "B0";
            DenetimSurucu.Wait(view, 0.2);
            Assert.Equal(new[] { "dizi-b01.mp4", "dizi-b02.mp4" }, Satirlar(view));
            Assert.Equal(1, view.Playlist.SelectedIndex);
            Assert.True(view.MenuOpen, "arama menuyu kapatti");
            Assert.True(view.PlaylistOpen, "arama listeyi kapatti");

            view.PlaylistSearchBox.Text = "film";
            DenetimSurucu.Wait(view, 0.2);
            Assert.Equal(new[] { "film.mp4" }, Satirlar(view));
            Assert.Equal(-1, view.Playlist.SelectedIndex);
            Assert.Equal(4, view.PlaylistFiles().Count);

            view.PlaylistSearchBox.Text = "yok-boyle-bir-sey";
            DenetimSurucu.Wait(view, 0.2);
            Assert.Empty(Satirlar(view));
            Assert.True(view.PlaylistOpen, "bos sonuc listeyi kapatti");

            view.PlaylistSearchBox.Text = "";
            DenetimSurucu.Wait(view, 0.2);
            Assert.Equal(Adlar(view), Satirlar(view));
        });
    }

    [Fact]
    public void AramaYenidenAcilistaSifirlanir()
    {
        Kosum(new[] { "a1.mp4", "a2.mp4", "b1.mp4" }, "a1.mp4", (view, window, _) =>
        {
            MenuyuAc(view, window);
            view.PlaylistSearchBox.Text = "b";
            DenetimSurucu.Wait(view, 0.2);
            Assert.Single(Satirlar(view));

            L.SagTik(window, L.Merkez(window, view));
            DenetimSurucu.Wait(view, 0.4);
            Assert.False(view.MenuOpen);
            MenuyuAc(view, window);
            Assert.Equal("", view.PlaylistSearchBox.Text ?? "");
            Assert.Equal(3, Satirlar(view).Length);
        });
    }

    [Fact]
    public void AramaKutusunaTiklamakMenuyuKapatmaz()
    {
        Kosum(new[] { "a1.mp4", "a2.mp4" }, "a1.mp4", (view, window, _) =>
        {
            MenuyuAc(view, window);
            var kutu = view.PlaylistSearchBox;
            var kok = TopLevel.GetTopLevel(kutu)!;
            L.SolTik(kok, L.Merkez(kok, kutu), view);
            DenetimSurucu.Wait(view, 0.3);
            Assert.True(view.MenuOpen, "arama kutusuna tik menuyu kapatti");
            Assert.True(view.PlaylistOpen, "arama kutusuna tik listeyi kapatti");
        });
    }

    [Fact]
    public void YukariVeAsagiTasimaKuyruguDuzenler()
    {
        Kosum(new[] { "a1.mp4", "a2.mp4", "a3.mp4", "a4.mp4" }, "a1.mp4", (view, window, _) =>
        {
            MenuyuAc(view, window);
            SatiraSagTik(view, 2);
            Tikla(view, "move-up");
            Assert.Equal(new[] { "a1.mp4", "a3.mp4", "a2.mp4", "a4.mp4" }, Adlar(view));
            Assert.Equal(Adlar(view), Satirlar(view));
            Assert.True(view.PlaylistOpen, "tasima listeyi kapatti");

            SatiraSagTik(view, 0);
            Assert.False(Oge(view, "move-up").IsEnabled, "ilk satirda yukari tasi acik");
            Tikla(view, "move-down");
            Assert.Equal(new[] { "a3.mp4", "a1.mp4", "a2.mp4", "a4.mp4" }, Adlar(view));

            SatiraSagTik(view, 3);
            Assert.False(Oge(view, "move-down").IsEnabled, "son satirda asagi tasi acik");
        });
    }

    [Fact]
    public void ElleDizilenKuyrukKaristirmadanEtkilenmez()
    {
        Kosum(new[] { "a1.mp4", "a2.mp4", "a3.mp4", "a4.mp4", "a5.mp4" }, "a1.mp4", (view, window, _) =>
        {
            MenuyuAc(view, window);
            SatiraSagTik(view, 4);
            Tikla(view, "move-up");
            var dizilen = Adlar(view);
            Assert.Equal(new[] { "a1.mp4", "a2.mp4", "a3.mp4", "a5.mp4", "a4.mp4" }, dizilen);
            view.Settings.Shuffle = true;
            Assert.Equal(dizilen, Adlar(view));
        });
    }

    [Fact]
    public void SiralaAltMenusuAdaTariheVeTersineDizer()
    {
        Kosum(new[] { "c.mp4", "a.mp4", "b.mp4" }, "a.mp4", (view, window, klasor) =>
        {
            var simdi = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(Path.Combine(klasor, "a.mp4"), simdi.AddDays(-3));
            File.SetLastWriteTimeUtc(Path.Combine(klasor, "b.mp4"), simdi.AddDays(-1));
            File.SetLastWriteTimeUtc(Path.Combine(klasor, "c.mp4"), simdi.AddDays(-2));

            MenuyuAc(view, window);
            SatiraSagTik(view, 0);
            Assert.Equal(Strings.Get("player.list.sort"), (string)Oge(view, "sort").Header!);
            Tikla(view, "sort-date");
            Assert.Equal(new[] { "b.mp4", "c.mp4", "a.mp4" }, Adlar(view));
            Assert.Equal(Adlar(view), Satirlar(view));
            Assert.Equal(2, view.Playlist.SelectedIndex);

            SatiraSagTik(view, 0);
            Tikla(view, "sort-reverse");
            Assert.Equal(new[] { "a.mp4", "c.mp4", "b.mp4" }, Adlar(view));

            SatiraSagTik(view, 0);
            Tikla(view, "sort-name");
            Assert.Equal(new[] { "a.mp4", "b.mp4", "c.mp4" }, Adlar(view));
            Assert.Equal(Adlar(view), Satirlar(view));
        });
    }

    [Fact]
    public void SatirGecmistekiKalinanYeriVeSureyiGosterir()
    {
        Kosum(new[] { "a1.mp4", "a2.mp4", "a3.mp4" }, "a1.mp4", (view, window, klasor) =>
        {
            view.History.Remember(Path.Combine(klasor, "a2.mp4"), 65, false, 300);
            MenuyuAc(view, window);

            var satir = view.Playlist.Items.OfType<ListBoxItem>().Single(i => Path.GetFileName((string)i.Tag!) == "a2.mp4");
            var yazilar = ((Panel)satir.Content!).Children.OfType<TextBlock>().Select(t => t.Text).ToArray();
            Assert.Equal(new[] { "a2.mp4", "01:05 / 05:00" }, yazilar);

            var digeri = view.Playlist.Items.OfType<ListBoxItem>().Single(i => Path.GetFileName((string)i.Tag!) == "a3.mp4");
            Assert.IsAssignableFrom<TextBlock>(digeri.Content);
        });
    }
}
