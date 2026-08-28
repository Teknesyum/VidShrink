using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Performance;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// T64: Gelişmiş sekmesindeki başarım denetçisi paneli. T63 ölçüyor, panel gösteriyor.
///
/// Panel gözle görülmeden teslim ediliyor (ekran kapısı kapalı), bu yüzden gözün yerini
/// bu ölçümler tutuyor: panelin yeri ve kalıbı ağaçtan, ölçümün kendiliğinden koşmadığı
/// sahte sondanın çağrı sayacından, cümleler <see cref="PerformanceReportText"/>
/// çıktısından, yerleşim ise <c>PageAdvanced</c> taşıyıcısının uzam/görüş alanı farkından
/// okunuyor.
/// </summary>
public sealed class PerformanceCheckUiTests
{
    private static string WindowXaml() => File.ReadAllText(TipSources.WindowXamlPath);

    private static string ReportSource() => File.ReadAllText(
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Performance", "PerformanceReportText.cs"));

    /// <summary>
    /// Donanım yolunun çalıştığı, işlemciye bağlı olmadığı bir sonuç. Süreler sahte, ama
    /// T64 sözleşmesindeki ölçülmüş aralıktan türetildi; ölçümün kendisi bu dosyada değil,
    /// o koşumun ham günlüğünde durur.
    /// </summary>
    private static PerformanceCheckResult HardwareResult(double cpuAccountingFactor = 1.0) =>
        PerformanceCheck.Evaluate(
            new EncoderCost("libx264", true, 3100, 3200, 6000),
            new EncoderCost("h264_nvenc", true, 120, 375, 6000),
            new EncoderCost("h264_nvenc", true, 120, 360, 6000),
            logicalCores: 16,
            elapsedMs: 9_400,
            budgetMs: 20_000,
            hardwareEncoderPresent: true,
            cpuAccountingFactor: cpuAccountingFactor);

    /// <summary>
    /// T63'ün devrettiği borcun sahtesi: donanım kodlayıcısı çalışıyor ama iki donanım
    /// geçişi arasındaki fark bandı aştığı için <c>Evaluate</c> donanım dalını atlıyor ve
    /// <see cref="RecordingImpact"/> yazılım tarafına kayıyor. Bulgular yanıltmıyor,
    /// <c>Impact</c> yanıltıyor.
    /// </summary>
    private static PerformanceCheckResult DriftedImpactResult() =>
        PerformanceCheck.Evaluate(
            new EncoderCost("libx264", true, 3100, 3200, 6000),
            new EncoderCost("h264_nvenc", true, 120, 500, 6000),
            new EncoderCost("h264_nvenc", true, 120, 360, 6000),
            logicalCores: 16,
            elapsedMs: 11_800,
            budgetMs: 20_000,
            hardwareEncoderPresent: true,
            cpuAccountingFactor: 1.0);

    private static PerformanceCheckResult SoftwareOnlyResult() =>
        PerformanceCheck.Evaluate(
            new EncoderCost("libx264", true, 6000, 6100, 6000),
            null,
            null,
            logicalCores: 8,
            elapsedMs: 8_200,
            budgetMs: 20_000,
            hardwareEncoderPresent: false,
            cpuAccountingFactor: 1.0);

    private static IEnumerable<PerformanceCheckResult> AllResults()
    {
        yield return PerformanceCheckResult.NotMeasured;
        yield return HardwareResult();
        yield return HardwareResult(cpuAccountingFactor: 0);
        yield return DriftedImpactResult();
        yield return SoftwareOnlyResult();
    }

    // ---- Pencere ----

    /// <summary>
    /// <see cref="WindowLayoutTests"/> ile aynı yaklaşım: yerleşim pencereye değil
    /// pencerenin kök görsel çocuğuna veriliyor, çünkü pencere <c>Maximized</c> açılıyor
    /// ve kendi sınırlarını platform penceresinden alıyor.
    /// </summary>
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
    }

    /// <summary>
    /// Sekme değiştikten sonraki geçiş. Yeni sekmenin içeriği ilk geçişte hiç ölçülmemiş
    /// oluyor; <see cref="TopLevel.UpdateLayout"/> onu pencerenin kendi <c>ClientSize</c>'ı
    /// ile (başsız koşumda sıfır) ölçüp temizliyor ve kök geçişi artık ona uğramıyor —
    /// sekme ağaçta duruyor ama sınırları sıfır kalıyor. Bu yüzden ikinci geçiş pencereyi
    /// hiç sürmez: ağacın tamamı geçersizleştirilip yalnız kök ölçülür.
    /// </summary>
    private static void RelayoutAt(MainWindow window, Size size)
    {
        foreach (var node in window.GetVisualDescendants().OfType<Layoutable>()) node.InvalidateMeasure();

        var root = (Layoutable)window.GetVisualChildren().Single();
        root.InvalidateMeasure();
        root.Measure(size);
        root.Arrange(new Rect(size));
    }

    /// <summary>
    /// Gelişmiş sekmesi seçili bir pencere kurar. Panel açılıyorsa <b>ilk ölçümden önce</b>
    /// açılır: gizliyken hiç ölçülmemiş bir alt ağaç sonradan açıldığında bu koşumda
    /// ölçülmüyor (T46'da ölçüldü), o yüzden istenen hâl daha kurulurken kuruluyor.
    /// </summary>
    private static T OnAdvancedTab<T>(Action<MainWindow> arrange, Func<MainWindow, T> read) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            arrange(window);

            var size = new Size(
                (double)Resource(window, "WindowPreferredWidth"),
                (double)Resource(window, "WindowPreferredHeight"));

            LayOutAt(window, size);

            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            var advanced = Enumerable.Range(0, tabs.ItemCount)
                .Single(index => (tabs.ContainerFromIndex(index) as TabItem)?.Header as string == "Advanced");

            tabs.SelectedIndex = advanced;
            RelayoutAt(window, size);

            return read(window);
        });

    private static object Resource(MainWindow window, string key)
    {
        Assert.True(window.TryFindResource(key, out var value), $"{key} belirteci yok.");
        return value!;
    }

    private static T Named<T>(MainWindow window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    // ---- K1 ----

    /// <summary>
    /// K1: panel Gelişmiş sekmesinde ve mevcut kalıpta. Kalıbın kendisi kaynaktan
    /// okunuyor; panelin sekmenin içinde durduğu ise ağaçtan.
    /// </summary>
    [Fact]
    public void ThePanelSitsInTheAdvancedTabInTheSamePatternAsTheOthers()
    {
        var xaml = WindowXaml();
        var start = xaml.IndexOf("x:Name=\"PageAdvanced\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "PageAdvanced MainWindow.axaml içinde yok.");

        var page = xaml[start..xaml.IndexOf("</TabItem>", start, StringComparison.Ordinal)];

        Assert.Contains("x:Name=\"PerformancePanel\" Theme=\"{StaticResource Panel}\"", page, StringComparison.Ordinal);
        Assert.Contains("Text=\"Performance check\" Theme=\"{StaticResource H2}\"", page, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BtnPerformanceExpand\"", page, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PerformanceDetails\" IsVisible=\"False\"", page, StringComparison.Ordinal);

        // Açılır düğme diğer iki panelle aynı: ▾ içerikli GhostButton.
        var after = page[page.IndexOf("BtnPerformanceExpand", StringComparison.Ordinal)..];
        var toggle = after[..Math.Min(400, after.Length)];
        Assert.Contains("Content=\"▾\"", toggle, StringComparison.Ordinal);
        Assert.Contains("Theme=\"{StaticResource GhostButton}\"", toggle, StringComparison.Ordinal);

        // Sütun düzeni CommandPanel ile aynı.
        Assert.Equal(
            2,
            Regex.Matches(page, Regex.Escape("<Grid ColumnDefinitions=\"*,Auto\" ColumnSpacing=\"{StaticResource SpaceMd}\">")).Count);

        var (inPage, collapsed) = OnAdvancedTab(
            _ => { },
            window =>
            {
                var panel = Named<Border>(window, "PerformancePanel");
                var scroller = Named<ScrollViewer>(window, "PageAdvanced");
                return (panel.GetVisualAncestors().Contains(scroller),
                        Named<StackPanel>(window, "PerformanceDetails").IsVisible);
            });

        Assert.True(inPage, "Panel PageAdvanced altında değil.");
        Assert.False(collapsed, "Panel kapalı açılmıyor.");
    }

    // ---- K2 ----

    /// <summary>
    /// K2: ölçüm kendiliğinden koşmuyor. Pencere kurulur, yerleştirilir, sekme açılır —
    /// sahte sondanın sayacı sıfır. Düğme tıklanınca bir.
    /// </summary>
    [Fact]
    public void TheProbeRunsOnlyWhenTheButtonIsPressed()
    {
        var calls = 0;

        var afterSetup = OnAdvancedTab(
            window =>
            {
                window.PerformanceProbeRunner = _ =>
                {
                    calls++;
                    return Task.FromResult(HardwareResult());
                };
                window.ExpandPerformanceCheck();
            },
            window =>
            {
                var seen = calls;
                Named<Button>(window, "BtnPerformanceRun").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                return seen;
            });

        Assert.Equal(0, afterSetup);
        Assert.Equal(1, calls);
    }

    // ---- K3 ----

    /// <summary>
    /// K3: on iki bulgu kodunun her birinin ayrı, boş olmayan bir karşılığı var.
    /// </summary>
    [Fact]
    public void EveryFindingCodeHasItsOwnLine()
    {
        var codes = Enum.GetValues<PerformanceFindingCode>();
        Assert.Equal(12, codes.Length);

        var lines = codes
            .Select(code => (code, line: PerformanceReportText.Line(new PerformanceFinding(code))))
            .ToList();

        Assert.All(lines, entry => Assert.False(
            string.IsNullOrWhiteSpace(entry.line), $"{entry.code} için satır boş."));

        var repeated = lines
            .GroupBy(entry => entry.line, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" = ", group.Select(entry => entry.code)))
            .ToList();

        Assert.True(repeated.Count == 0, "Aynı satırı paylaşan kodlar var: " + string.Join(", ", repeated));
    }

    /// <summary>
    /// K3: eşlemede serbest geçiş yok. T63 tur 2'de kaldırıldı, geri gelmesin.
    /// </summary>
    [Fact]
    public void TheMappingHasNoFallThrough()
    {
        var source = ReportSource();

        Assert.DoesNotContain("_ =>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("default:", source, StringComparison.Ordinal);
    }

    // ---- K4 ----

    /// <summary>
    /// K4: ölçülmeyen şey ekranda "ölçülmedi" diye duruyor. Donanımın işlemci maliyeti ve
    /// güvenilmez işlemci sayacı sessizce atlanamaz — kullanıcı ikisini de "bedava" diye
    /// okumamalı. Metin üretiminde <b>ve</b> ekrandaki satırlarda aranıyor.
    /// </summary>
    [Fact]
    public void WhatWasNotMeasuredIsSaidOnScreen()
    {
        var result = HardwareResult(cpuAccountingFactor: 0);

        Assert.Contains(PerformanceFindingCode.HardwareCpuCostNotMeasured, result.Findings.Select(f => f.Code));
        Assert.Contains(PerformanceFindingCode.CpuAccountingUnreliable, result.Findings.Select(f => f.Code));

        var text = PerformanceReportText.Describe(result);

        Assert.Contains(text, line => line.Contains("processor cost of the hardware pass was not measured", StringComparison.Ordinal));
        Assert.Contains(text, line => line.Contains("processor time counter on this machine is not dependable", StringComparison.Ordinal));

        var onScreen = OnAdvancedTab(
            window =>
            {
                window.ExpandPerformanceCheck();
                window.ShowPerformanceResult(result);
            },
            window => Named<StackPanel>(window, "PerformanceLines").Children
                .OfType<TextBlock>()
                .Select(block => block.Text ?? string.Empty)
                .ToList());

        Assert.Equal(text.Count, onScreen.Count);
        Assert.Contains(onScreen, line => line.Contains("Was Not Measured", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(onScreen, line => line.Contains("Not Dependable", StringComparison.OrdinalIgnoreCase));
    }

    // ---- K5 ----

    /// <summary>
    /// Kayıt aracı hakkında iddia kuran kalıplar. VidShrink kayıt yapmaz ve o ölçüm hiç
    /// yapılmadı; panelin bu cümleleri kurma hakkı yok.
    /// </summary>
    private static readonly Regex ForbiddenClaim = new(
        @"\b(obs|shadowplay|game ?bar|nvidia|fps|frames? per second|frame rate|record(s|ed|ing)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void NoLineMakesAClaimAboutTheCaptureTool()
    {
        var offenders = new List<string>();

        foreach (var code in Enum.GetValues<PerformanceFindingCode>())
        {
            var line = PerformanceReportText.Line(new PerformanceFinding(code, Codec: "h264_nvenc"));
            var hit = ForbiddenClaim.Match(line);
            if (hit.Success) offenders.Add($"{code}: {hit.Value}");
        }

        foreach (var result in AllResults())
            foreach (var line in PerformanceReportText.Describe(result).Concat(
                         PerformanceReportText.Facts(result).Select(fact => $"{fact.Label} {fact.Value}")))
            {
                var hit = ForbiddenClaim.Match(line);
                if (hit.Success) offenders.Add($"{line} -> {hit.Value}");
            }

        Assert.True(offenders.Count == 0,
            "Kayıt aracı hakkında iddia kuran satır var:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// K5: sınır cümlesi her sonuçta duruyor — ölçüm hiç koşmamışken de. Panel açılışta
    /// <see cref="PerformanceCheckResult.NotMeasured"/> ile kuruluyor, yani cümle ekranda
    /// kalıcı.
    /// </summary>
    [Fact]
    public void TheBoundarySentenceIsInEveryResultAndOnScreenBeforeAnyMeasurement()
    {
        Assert.Contains("does not capture video", PerformanceReportText.Boundary, StringComparison.Ordinal);
        Assert.Contains("on this machine", PerformanceReportText.Boundary, StringComparison.Ordinal);

        foreach (var result in AllResults())
            Assert.Equal(PerformanceReportText.Boundary, PerformanceReportText.Describe(result).Last());

        var onScreen = OnAdvancedTab(
            window => window.ExpandPerformanceCheck(),
            window => Named<StackPanel>(window, "PerformanceLines").Children
                .OfType<TextBlock>()
                .Select(block => block.Text ?? string.Empty)
                .ToList());

        Assert.Contains(onScreen, line => line.Contains("Does Not Capture Video", StringComparison.OrdinalIgnoreCase));
    }

    // ---- K6 ----

    /// <summary>
    /// K6: birimler karışmıyor. Çekirdek talebi "cores", boru hattı hızı "× realtime"
    /// diye görünür; hız oranı çekirdek diye etiketlenemez.
    /// </summary>
    [Fact]
    public void EachNumberCarriesTheUnitItWasMeasuredIn()
    {
        var result = HardwareResult();
        var facts = PerformanceReportText.Facts(result);

        var cores = facts.Single(fact => fact.Label == "Software cost");
        var pipeline = facts.Single(fact => fact.Label == "Hardware pipeline");

        Assert.EndsWith(" cores", cores.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("realtime", cores.Value, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(
            result.SoftwareRealtimeCores.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            cores.Value,
            StringComparison.Ordinal);

        Assert.EndsWith("× realtime", pipeline.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("core", pipeline.Value, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(
            result.HardwarePipelineRealtimeFactor.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
            pipeline.Value,
            StringComparison.Ordinal);

        // Ekranda da ölçüldüğü yazımla duruyor. Satır koşulardan kuruluyor, çünkü sözcük
        // kuralından geçseydi "libx264" -> "Libx264", "ms" -> "Ms" olurdu.
        var rows = OnAdvancedTab(
            window =>
            {
                window.ExpandPerformanceCheck();
                window.ShowPerformanceResult(result);
            },
            window => Named<StackPanel>(window, "PerformanceFacts").Children
                .OfType<TextBlock>()
                .Select(block => block.Inlines is { } inlines
                    ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
                    : block.Text ?? string.Empty)
                .ToList());

        Assert.Contains(rows, row => row.EndsWith(": 0.53 cores", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.EndsWith(": 16× realtime", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.EndsWith(": h264_nvenc", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.EndsWith(": libx264", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.EndsWith(" ms", StringComparison.Ordinal));
    }

    // ---- K7 ----

    /// <summary>
    /// K7: panel açıkken Gelişmiş sekmesi kendi kaydırıcısında kalıyor. Yana hiç kaymıyor
    /// ve sayfanın kendisinden başka hiçbir taşıyıcı taşmıyor.
    /// </summary>
    [Fact]
    public void TheOpenPanelStaysInsideItsOwnScroller()
    {
        // Ölçümün gerçekten koştuğunu görmek için: sayfanın görüş alanı ve panelin boyu
        // sıfırdan büyük olmalı, yoksa aşağıdaki taşma taraması boş yere yeşil verir.
        var (viewport, panel, lines) = OnAdvancedTab(
            window =>
            {
                window.ExpandPerformanceCheck();
                window.ShowPerformanceResult(HardwareResult());
            },
            window => (
                Named<ScrollViewer>(window, "PageAdvanced").Viewport,
                Named<Border>(window, "PerformancePanel").Bounds,
                Named<StackPanel>(window, "PerformanceLines").Children.Count));

        Assert.True(viewport.Height > 0 && viewport.Width > 0, "PageAdvanced görüş alanı sıfır; yerleşim koşmamış.");
        Assert.True(panel.Height > 0, "Panel yerleştirilmemiş.");
        Assert.True(lines > 0, "Panelde satır yok.");
        Assert.True(
            panel.Width <= viewport.Width + 0.5,
            $"Panel görüş alanından geniş: {panel.Width:0.#} > {viewport.Width:0.#}");

        var overflowing = OnAdvancedTab(
            window =>
            {
                window.ExpandPerformanceCheck();
                window.ShowPerformanceResult(HardwareResult());
            },
            window => Named<ScrollViewer>(window, "PageAdvanced")
                .GetSelfAndVisualDescendants()
                .OfType<ScrollViewer>()
                .Where(viewer => viewer.IsEffectivelyVisible && viewer.FindAncestorOfType<TextBox>() is null)
                .Select(viewer => (
                    Name: string.IsNullOrEmpty(viewer.Name) ? viewer.GetType().Name : viewer.Name!,
                    Vertical: viewer.Extent.Height - viewer.Viewport.Height,
                    Horizontal: viewer.Extent.Width - viewer.Viewport.Width))
                .Where(entry => entry.Vertical > 0.5 || entry.Horizontal > 0.5)
                .ToList());

        Assert.True(
            overflowing.All(entry => entry.Name == "PageAdvanced"),
            "Panel açıkken sayfanın kendi kaydırıcısı dışında taşan taşıyıcı var: "
            + string.Join(", ", overflowing.Select(entry => $"{entry.Name} dikey +{entry.Vertical:0.#}, yatay +{entry.Horizontal:0.#}")));

        Assert.All(overflowing, entry => Assert.True(
            entry.Horizontal <= 0.5, $"{entry.Name} yana kayıyor: +{entry.Horizontal:0.#}"));
    }

    // ---- K9 ----

    /// <summary>
    /// K9: <c>Impact</c> sınırda yanlış cevap verebiliyor; manşet bulgulardan kuruluyor.
    /// Donanım kodlayıcısı çalışırken <c>Impact</c> yazılım tarafına kaymış bir sonuçta
    /// panel "bu makinede donanım kodlayıcısı yok" demiyor.
    /// </summary>
    [Fact]
    public void TheHeadlineFollowsTheFindingsNotTheImpactField()
    {
        var result = DriftedImpactResult();

        Assert.Equal(RecordingImpact.SoftwareLightLoad, result.Impact);
        Assert.Contains(PerformanceFindingCode.HardwarePathWorks, result.Findings.Select(f => f.Code));

        var headline = PerformanceReportText.Headline(result);

        Assert.Contains("has a working hardware encoder", headline, StringComparison.Ordinal);
        Assert.DoesNotContain("No working hardware encoder", headline, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no hardware", string.Join(" ", PerformanceReportText.Describe(result)), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hardware encoding works on this machine.", PerformanceReportText.Describe(result));

        // Kodlayıcının adı sayı bloğunda duruyor: bulgu ile çelişen bir manşetin yanında
        // hangi kodlayıcının ölçüldüğü de görünür.
        Assert.Equal("h264_nvenc", PerformanceReportText.Facts(result).Single(fact => fact.Label == "Hardware encoder").Value);
    }
}
