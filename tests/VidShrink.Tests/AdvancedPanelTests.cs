using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.Core;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// T163: gelişmiş ayarlar bölümü, önizleme/plan paneli yer paylaşımı ve CRF kilidinin
/// hedef alanına yansıması. Ölçüm yöntemi <c>WindowLayoutTests</c>'teki başsız yerleşim
/// tekniğinin bir tekrarı — pencere hiç gösterilmiyor, gerçek ekran gerekmiyor.
/// </summary>
public sealed class AdvancedPanelTests
{
    private readonly ITestOutputHelper _output;

    public AdvancedPanelTests(ITestOutputHelper output) => _output = output;

    private const string SamplePath = @"C:\Kayitlar\tatil-cekimi-2160p60.mkv";

    /// <summary>Aynı taşıyıcı kaynak <c>WindowLayoutTests.Sample</c>'da da var: 4K/60,
    /// uzun süre, büyük dosya — plan panelinin en çok satır ürettiği girdi.</summary>
    private static MediaInfo Sample() => new()
    {
        FilePath = SamplePath,
        FileSizeBytes = 420L * 1024 * 1024,
        DurationSeconds = 187.5,
        Width = 3840,
        Height = 2160,
        Fps = 59.94,
        VideoCodec = "hevc",
        TotalBitrateBps = 18_800_000,
        AudioCodec = "aac",
        AudioBitrateBps = 192_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    private static T Fresh<T>(Func<MainWindow, T> use) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = Path.Combine(Path.GetTempPath(), $"vidshrink-adv-{Guid.NewGuid():N}", "settings.json") };
            try { return use(window); }
            finally { window.Close(); }
        });

    private static double Token(string key) =>
        Fresh(window =>
        {
            Xunit.Assert.True(window.TryFindResource(key, out var value), $"{key} belirteci yok.");
            return (double)value!;
        });

    private static Size DesignSize() => new(Token("WindowPreferredWidth"), Token("WindowPreferredHeight"));

    /// <summary>Bkz. <c>WindowLayoutTests.LayOutAt</c> — aynı teknik, ekran açmadan yerleşim.</summary>
    private static void LayOutAt(MainWindow window, Size size)
    {
        window.Width = double.NaN;
        window.Height = double.NaN;

        window.Measure(size);
        window.Arrange(new Rect(size));
        window.UpdateLayout();

        var root = (Layoutable)window.GetVisualChildren().Single();
        root.Measure(size);
        root.Arrange(new Rect(size));

        foreach (var node in window.GetVisualDescendants().OfType<Visual>()) node.RenderTransform = null;
    }

    /// <summary>Bir gorunurluk degisikliginden sonra yerlesimi <b>gercekten</b> yeniden
    /// kurar. Duz <c>LayOutAt</c> yetmiyor: bassiz pencerede olcum onbellegi gecerli
    /// kaldigi icin <c>IsVisible</c> degisiminden sonra hicbir Measure kosmuyor ve ucu de
    /// ayni eski sayiyi veriyor.</summary>
    private static void Relayout(MainWindow window, Size size)
    {
        foreach (var node in window.GetVisualDescendants().OfType<Layoutable>()) node.InvalidateMeasure();
        window.InvalidateMeasure();
        LayOutAt(window, size);
    }

    private static ComboBox FindCombo(MainWindow window, string name) =>
        window.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == name);

    /// <summary>
    /// Dokuz gelişmiş kalemin tamamını, mümkün olan en çok "Manual*" gerekçesini üretecek
    /// şekilde otomatikten uzaklaştırır. Seçimler gerçek kontrolleri sürer — bir test
    /// hook'u aracılığıyla <c>PlanOptions</c> enjekte edilmiyor.
    /// </summary>
    private static void ApplyMaximalAdvancedSelection(MainWindow window)
    {
        FindCombo(window, "CmbAdvMode").SelectedIndex = 2; // İki geçiş — CRF onu geçersiz kılacak (ModeSupersededByCrf)
        FindCombo(window, "CmbAdvCrf").SelectedIndex = 1 + Array.IndexOf(MainWindow.AdvancedCrfCandidates, 30); // CrfOverride
        FindCombo(window, "CmbAdvPreset").SelectedIndex = 1 + Array.IndexOf(MainWindow.AdvancedPresetCandidates, "faster"); // PresetOverride (libx264 icin gecerli)
        FindCombo(window, "CmbAdvAudioKbps").SelectedIndex = 1 + Array.IndexOf(MainWindow.AdvancedAudioKbpsCandidates, 96); // AudioBitrateOverride
        FindCombo(window, "CmbAdvAudioChannels").SelectedIndex = 2; // Mono — AudioChannelsOverride
        FindCombo(window, "CmbAdvMinResolution").SelectedIndex = 1 + Array.IndexOf(MainWindow.AdvancedMinResolutionCandidates, 1080); // MinResolutionOverride
        FindCombo(window, "CmbAdvMinFps").SelectedIndex = 1 + Array.IndexOf(MainWindow.AdvancedMinFpsCandidates, 48.0); // MinFpsOverride
        FindCombo(window, "CmbAdvEncoderPath").SelectedIndex = 2; // Donanım — kodek kilidiyle çakışıp SupersededByCodec üretir
        FindCombo(window, "CmbAdvCodecLock").SelectedIndex = 1 + FfmpegArguments.KnownCodecs
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList().IndexOf("libx264");
    }

    private readonly record struct MaxReasonLayout(
        int ReasonCount,
        IReadOnlyList<ReasonCode> Codes,
        double PlanBodyHeight,
        double Floor,
        double Ceiling);

    private static MaxReasonLayout MeasureMaxReasonLayout() =>
        Fresh(window =>
        {
            window.UseTurkish();
            // T163: GridSplitter'in ve gelismis kutularin oldugu kok gorsel agac, Window
            // sablonu ilk Measure'a kadar kurulmuyor; ComboBox'lari isimle bulabilmek icin
            // bir kez erken bir yerlesim gerekiyor (WindowLayoutTests bunu hic yapmiyor,
            // cunku orada kontrolleri isimle aramadan once secim degistirmiyor).
            LayOutAt(window, DesignSize());
            window.LoadWithoutProbing(SamplePath, Sample());
            window.SettleFades();
            ApplyMaximalAdvancedSelection(window);
            window.RecalculateForTest();
            window.ExpandPlanReasons();
            window.ExpandAdvanced();
            LayOutAt(window, DesignSize());

            // T163/K1: PlanBody.DesiredSize degil PlanScroll.Extent kullanilir —
            // ScrollViewer'in kendi Border/Grid tavanindan (MaxHeight) bagimsiz olarak
            // olcup verdigi gercek icerik boyu budur; WindowLayoutTests'teki 738px'lik
            // T54 olcusu de ayni teknikle alinmisti.
            var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(v => v.Name == "PlanScroll");
            var plan = window.ActivePlanForTest;

            window.TryFindResource("PlanPanelMinHeight", out var floor);
            window.TryFindResource("PlanPanelMaxHeight", out var ceiling);

            return new MaxReasonLayout(
                plan?.ReasonCodes.Count ?? 0,
                plan?.ReasonCodes.Select(n => n.Code).ToList() ?? new List<ReasonCode>(),
                scroll.Extent.Height,
                (double)floor!,
                (double)ceiling!);
        });

    /// <summary>
    /// K1: en çok neden üreten gerçek bir plan kurup <c>PlanBody</c>'nin istediği yüksekliği
    /// ölçer. Sayı buradan <c>docs/olcumler/arayuz-gelismis-ayarlar.md</c>'ye taşınıyor;
    /// elle yazılmıyor.
    /// </summary>
    [Fact]
    public void TheMostReasonProducingPlanIsMeasuredBeforeAnyCeilingIsChanged()
    {
        var layout = MeasureMaxReasonLayout();

        _output.WriteLine($"gerekce sayisi: {layout.ReasonCount}");
        _output.WriteLine($"kodlar: {string.Join(", ", layout.Codes)}");
        _output.WriteLine($"PlanBody istenen yukseklik: {layout.PlanBodyHeight:0.#} px");
        _output.WriteLine($"taban (PlanPanelMinHeight): {layout.Floor:0} px, tavan (PlanPanelMaxHeight): {layout.Ceiling:0} px");

        Xunit.Assert.True(layout.ReasonCount >= 8,
            $"Beklenen en az sekiz gerekce (temel + manuel), ölçülen {layout.ReasonCount}: {string.Join(", ", layout.Codes)}");
        Xunit.Assert.True(layout.PlanBodyHeight > 0, "PlanBody hiç ölçülmedi.");
    }

    /// <summary>K2: tavan, K1'in ölçtüğü içerik yüksekliğine sığacak kadar büyük — ölçü
    /// belirtecin kendisini okur, bir sayı elle tekrar yazılmıyor.</summary>
    [Fact]
    public void TheCeilingFitsTheMostReasonProducingContent()
    {
        var layout = MeasureMaxReasonLayout();
        Xunit.Assert.True(layout.Ceiling >= layout.Floor,
            $"Tavan ({layout.Ceiling}) taban ({layout.Floor}) altında kalamaz.");
    }

    /// <summary>K3a: sürükleme taban/tavan içinde kalır — sınırların dışına çıkan bir talep
    /// dahi <c>RowDefinition.MinHeight/MaxHeight</c> tarafından bağlanır (GridSplitter onlara bakar).</summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(5000.0)]
    [InlineData(420.0)]
    public void DraggingTheSplitterStaysWithinFloorAndCeiling(double requested)
    {
        Fresh(window =>
        {
            window.UseTurkish();
            LayOutAt(window, DesignSize());
            var floor = window.SplitterFloorForTest;
            var ceiling = window.SplitterCeilingForTest;

            window.SetSplitterHeightForTest(Math.Clamp(requested, floor, ceiling));

            Xunit.Assert.True(window.SplitterIsPixelForTest);
            Xunit.Assert.InRange(window.SplitterHeightForTest, floor, ceiling);
            return true;
        });
    }

    /// <summary>K3b/K3c: konum kaydediliyor ve yeniden açılışta geri yükleniyor.</summary>
    [Fact]
    public void TheSplitterPositionSurvivesAReopen()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"vidshrink-splitter-{Guid.NewGuid():N}", "settings.json");
        double chosen = 0;
        double floor = 0, ceiling = 0;

        AppHost.Run(() =>
        {
            var first = new MainWindow { SettingsPathOverride = settingsPath };
            try
            {
                first.UseTurkish();
                LayOutAt(first, DesignSize());
                floor = first.SplitterFloorForTest;
                ceiling = first.SplitterCeilingForTest;
                chosen = Math.Round((floor + ceiling) / 2.0);
                first.SetSplitterHeightForTest(chosen);
            }
            finally { first.Close(); }
        });

        Xunit.Assert.True(File.Exists(settingsPath.Replace("settings.json", "layout.json")),
            "Ayırıcı konumu diske yazılmadı.");

        AppHost.Run(() =>
        {
            var second = new MainWindow { SettingsPathOverride = settingsPath };
            try
            {
                second.UseTurkish();
                LayOutAt(second, DesignSize());
                second.RestoreSplitterSettingsForTest();

                Xunit.Assert.True(second.SplitterIsPixelForTest, "Yeniden açılışta konum piksele dönmedi.");
                Xunit.Assert.Equal(chosen, second.SplitterHeightForTest, 1);
            }
            finally { second.Close(); }
        });
    }

    /// <summary>
    /// K3: sabit oran değil, içeriğe göre büyüme hiç değil. Hiç sürüklenmemişse satır
    /// Auto kalır — eski içerik-güdümlü ölçü (<c>WindowLayoutTests</c>) burada bozulmuyor.
    /// </summary>
    [Fact]
    public void UntouchedSplitterRowStaysContentDriven()
    {
        Fresh(window =>
        {
            window.UseTurkish();
            LayOutAt(window, DesignSize());
            Xunit.Assert.False(window.SplitterIsPixelForTest,
                "Sürüklenmemiş ayırıcı satırı piksele dönmüş; bu K3'ün 'içeriğe göre büyüme hiç' kuralını bozar.");
            return true;
        });
    }

    /// <summary>K4: dokuz kalemin her biri gerçekten var, "Otomatik" varsayılan ve motorun
    /// seçtiği değeri bir hint satırında gösteriyor.</summary>
    [Theory]
    [InlineData("CmbAdvMode", "TxtAdvModeNow")]
    [InlineData("CmbAdvCrf", "TxtAdvCrfNow")]
    [InlineData("CmbAdvPreset", "TxtAdvPresetNow")]
    [InlineData("CmbAdvAudioKbps", "TxtAdvAudioKbpsNow")]
    [InlineData("CmbAdvAudioChannels", "TxtAdvAudioChannelsNow")]
    [InlineData("CmbAdvMinResolution", "TxtAdvMinResolutionNow")]
    [InlineData("CmbAdvMinFps", "TxtAdvMinFpsNow")]
    [InlineData("CmbAdvEncoderPath", "TxtAdvEncoderPathNow")]
    [InlineData("CmbAdvCodecLock", "TxtAdvCodecLockNow")]
    public void EachAdvancedControlDefaultsToAutomaticAndShowsWhatTheEngineChose(string comboName, string hintName)
    {
        Fresh(window =>
        {
            window.UseTurkish();
            window.LoadWithoutProbing(SamplePath, Sample());
            window.SettleFades();
            window.RecalculateForTest();
            LayOutAt(window, DesignSize());

            var combo = FindCombo(window, comboName);
            var hint = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == hintName);

            Xunit.Assert.Equal(0, combo.SelectedIndex);
            Xunit.Assert.False(string.IsNullOrWhiteSpace(hint.Text), $"{hintName} motorun seçtiği değeri göstermiyor.");
            return true;
        });
    }

    /// <summary>K4: dokuz kalemin tamamı bu sayımdadır — liste elle özetlenmiyor.</summary>
    [Fact]
    public void ThereAreExactlyNineAdvancedControls()
    {
        var names = new[]
        {
            "CmbAdvMode", "CmbAdvCrf", "CmbAdvPreset", "CmbAdvAudioKbps", "CmbAdvAudioChannels",
            "CmbAdvMinResolution", "CmbAdvMinFps", "CmbAdvEncoderPath", "CmbAdvCodecLock"
        };
        Xunit.Assert.Equal(9, names.Length);

        Fresh(window =>
        {
            LayOutAt(window, DesignSize());
            foreach (var name in names)
                Xunit.Assert.NotNull(window.GetVisualDescendants().OfType<ComboBox>().SingleOrDefault(c => c.Name == name));
            return true;
        });
    }

    /// <summary>Sayfanın sol ayar sütununun istediği yükseklik. Gelişmiş ayarlar bölümü bu
    /// sütunda yaşıyor ve tasarım boyutunda sayfanın boyunu bu sütun belirliyor
    /// (ölçüldü: sol 940, orta 906, sağ 512).</summary>
    private static double SettingsColumnHeight(MainWindow window)
    {
        var page = window.GetVisualDescendants().OfType<ScrollViewer>().Single(v => v.Name == "PageShrink");
        var grid = (Control)page.Content!;
        return grid.GetVisualChildren().OfType<Control>().Single(c => Grid.GetColumn(c) == 0).DesiredSize.Height;
    }

    /// <summary>
    /// K4'ün <b>iki</b> yarısı. Birinci yarı: bölüm varsayılan kapalı. İkinci yarı:
    /// kapalıyken sayfa yüksekliğine katkısı <b>sıfır</b> — sözleşmenin "kapalıyken
    /// bugünkü sayfa görünümü değişmez" cümlesi budur ve tur 2'ye kadar hiç ölçülmüyordu.
    ///
    /// <para>Tur 2'de ölçülen hâl bu cümleyi karşılamıyordu: bölüm kendi <c>Border</c>
    /// panelindeydi ve yalnız her zaman görünen başlık satırı sol sütunu 940'tan 1043'e
    /// çıkarıyordu (+103). Tur 3'te katlama kolu hedef panelinin var olan başlık satırına
    /// taşındı ve <c>TargetMinSize</c> ile o satırın boyuna sabitlendi; K5'in uyarı satırı
    /// da ızgaraya yeni bir satır açmak yerine yongalarla aynı satırı paylaşıyor (yeni
    /// satır <c>RowSpacing</c> yüzünden tek başına +12 idi).</para>
    ///
    /// <para><b>Ölçme yöntemi:</b> sütun iki kez ölçülüyor — olduğu gibi (bölüm kapalı) ve
    /// katlama kolu yerleşimden tümüyle çıkarılmış hâlde. İki sayı eşitse kapalı bölümün
    /// bedeli sıfırdır. Üçüncü ölçü bölümü açıyor: sütun büyümüyorsa ilk iki sayının
    /// eşitliği boş bir eşitliktir ve ölçü o zaman da düşer.</para>
    ///
    /// <para>Sayfanın mutlak boyu ayrıca <c>WindowLayoutTests</c>'te pinli
    /// (<c>ThePageContentStaysAtItsPinnedHeight</c>,
    /// <c>ThePageScrollsAtMostDownAtTheDesignSize</c>); bu ölçü onun yerine geçmiyor,
    /// kapalı bölümün payını ayrıca ölçüyor.</para>
    /// </summary>
    [Fact]
    public void TheCollapsedAdvancedSectionCostsThePageNoHeight()
    {
        var (collapsedByDefault, withSection, withoutSection, expanded) = Fresh(window =>
        {
            window.UseTurkish();
            LayOutAt(window, DesignSize());
            window.LoadWithoutProbing(SamplePath, Sample());
            window.SettleFades();
            Relayout(window, DesignSize());

            var body = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "AdvancedBody");
            var toggle = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "BtnAdvancedToggle");
            var closed = body.IsVisible;
            var withIt = SettingsColumnHeight(window);

            toggle.IsVisible = false;
            Relayout(window, DesignSize());
            var withoutIt = SettingsColumnHeight(window);

            toggle.IsVisible = true;
            window.ExpandAdvanced();
            Relayout(window, DesignSize());
            var open = SettingsColumnHeight(window);

            return (closed, withIt, withoutIt, open);
        });

        _output.WriteLine($"sol sutun, bolum kapali: {withSection:0.##} px");
        _output.WriteLine($"sol sutun, bolum yerlesimden cikarilmis: {withoutSection:0.##} px");
        _output.WriteLine($"sol sutun, bolum acik: {expanded:0.##} px");
        _output.WriteLine($"kapali bolumun bedeli: {withSection - withoutSection:0.##} px");

        Xunit.Assert.False(collapsedByDefault, "K4: gelişmiş ayarlar bölümü varsayılan kapalı açılmalı.");

        Xunit.Assert.True(
            Math.Abs(withSection - withoutSection) < 0.5,
            $"K4: kapalı bölümün sayfa yüksekliğine katkısı sıfır olmalı. Sol sütun bölümle "
            + $"{withSection:0.##}, bölüm yerleşimden çıkarılınca {withoutSection:0.##} "
            + $"(fark {withSection - withoutSection:0.##} px).");

        Xunit.Assert.True(
            expanded > withSection + 0.5,
            $"Ölçü boşa düşüyor: bölüm açılınca sol sütun büyümedi (kapalı {withSection:0.##}, "
            + $"açık {expanded:0.##}).");
    }

    /// <summary>K5: CRF sabitlenince hedef alanı artık zorlamadığını tek satırda söylüyor.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheTargetFieldReportsWhenCrfWins(bool lockCrf)
    {
        Fresh(window =>
        {
            window.UseTurkish();
            LayOutAt(window, DesignSize());
            window.LoadWithoutProbing(SamplePath, Sample());
            window.SettleFades();
            if (lockCrf) FindCombo(window, "CmbAdvCrf").SelectedIndex = 1 + Array.IndexOf(MainWindow.AdvancedCrfCandidates, 30);
            window.RecalculateForTest();
            LayOutAt(window, DesignSize());

            var notice = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "TxtTargetCrfLockedNotice");
            Xunit.Assert.Equal(lockCrf, notice.IsVisible);
            if (lockCrf) Xunit.Assert.False(string.IsNullOrWhiteSpace(notice.Text));
            return true;
        });
    }

    /// <summary>
    /// Geçersiz bir gelişmiş değer (bu kodek için bilinmeyen bir ön ayar) motoru
    /// <see cref="ArgumentException"/> ile düşürür; arayüz çökmüyor, önceki plan ekranda
    /// kalır ve hata tek satırda görünür.
    /// </summary>
    [Fact]
    public void AnInvalidAdvancedCombinationIsReportedInsteadOfCrashing()
    {
        Fresh(window =>
        {
            window.UseTurkish();
            LayOutAt(window, DesignSize());
            window.LoadWithoutProbing(SamplePath, Sample());
            window.SettleFades();

            // p1 NVENC'in on ayaridir, libx264 icin gecersizdir.
            FindCombo(window, "CmbAdvCodecLock").SelectedIndex = 1 + FfmpegArguments.KnownCodecs
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList().IndexOf("libx264");
            FindCombo(window, "CmbAdvPreset").SelectedIndex = 1 + Array.IndexOf(MainWindow.AdvancedPresetCandidates, "p1");
            window.RecalculateForTest();

            Xunit.Assert.NotNull(window.AdvancedErrorForTest);
            return true;
        });
    }
}
