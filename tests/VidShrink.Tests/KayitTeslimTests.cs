using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VidShrink.Core.Share;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kayıt bittikten sonraki teslim: dosyanın küçültme sekmesine ve oynatıcıya geçişi,
/// bir de paylaşımı. Ölçü kaynak metinden okunuyor — test projesi pencere açamıyor
/// (<c>Avalonia.Headless</c> bağlı değil), o yüzden kablonun varlığı biçimlemede ve
/// koddadır.
///
/// <para>Bir ölçü metin okumuyor: hedef tablosunun yayın paketine girdiği. O, tablonun
/// <see cref="ShareTargetTable.Locate"/> aramasıyla proje dosyasındaki taşıma bildirimi
/// arasındaki bağ — bildirim düşerse kurulu yapıda paylaş düğmesi hedef bulamaz ve bunu
/// yalnız kullanıcı fark eder.</para>
/// </summary>
public sealed class KayitTeslimTests
{
    private static string Read(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { TipSources.Root }.Concat(parts).ToArray()));

    private static string RecorderXaml() => Read("src", "VidShrink.App", "Recorder", "RecorderView.axaml");

    private static string RecorderCode() => Read("src", "VidShrink.App", "Recorder", "RecorderView.axaml.cs");

    private static string ShareCode() => Read("src", "VidShrink.App", "Recorder", "RecorderView.Paylas.cs");

    private static string WindowCode() => File.ReadAllText(TipSources.WindowCodePath);

    /// <summary>
    /// Sonuç panelinde dört kapı var: klasör, küçültme, oynatıcı, paylaşım. "Klasörü
    /// göster" tek başına teslim sayılmıyor.
    /// </summary>
    [Theory]
    [InlineData("BtnReveal")]
    [InlineData("BtnToShrink")]
    [InlineData("BtnToPlayer")]
    [InlineData("BtnRecShare")]
    public void SonucPanelindeDortKapiVar(string name)
    {
        var xaml = RecorderXaml();
        var start = xaml.IndexOf("x:Name=\"ResultPanel\"", System.StringComparison.Ordinal);
        Assert.True(start >= 0, "ResultPanel RecorderView.axaml içinde yok.");
        Assert.Contains($"x:Name=\"{name}\"", xaml[start..], System.StringComparison.Ordinal);
    }

    /// <summary>Teslim edilen yol ekrandaki metin kutusundan değil, kendi alanından okunuyor.</summary>
    [Fact]
    public void TeslimEdilenYolKutudanOkunmuyor()
    {
        var code = RecorderCode();
        Assert.Contains("private string? _lastRecording;", code, System.StringComparison.Ordinal);
        Assert.Contains("_lastRecording = result.OutputPath;", code, System.StringComparison.Ordinal);
        Assert.DoesNotContain("var path = TxtResultPath.Text;", code, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// İki kapı ana pencerede sekmeyi değiştirip dosyayı yüklüyor; kaydedici kendi
    /// çözümleyicisini ya da kendi oynatıcısını kurmuyor.
    /// </summary>
    [Fact]
    public void AnaPencereIkiKapiyiAciyor()
    {
        var window = WindowCode();
        Assert.Contains("RecorderPane.OpenInShrink = OpenInShrinkAsync;", window, System.StringComparison.Ordinal);
        Assert.Contains("RecorderPane.OpenInPlayer = OpenInPlayerAsync;", window, System.StringComparison.Ordinal);

        var shrink = Regex.Match(window, @"OpenInShrinkAsync\(string path\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline);
        Assert.True(shrink.Success, "OpenInShrinkAsync gövdesi okunamadı.");
        Assert.Contains("Tabs.SelectedIndex = ShrinkTabIndex;", shrink.Groups["body"].Value, System.StringComparison.Ordinal);
        Assert.Contains("await LoadAsync(path);", shrink.Groups["body"].Value, System.StringComparison.Ordinal);

        var player = Regex.Match(window, @"OpenInPlayerAsync\(string path\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline);
        Assert.True(player.Success, "OpenInPlayerAsync gövdesi okunamadı.");
        Assert.Contains("Tabs.SelectedIndex = PlayerTabIndex;", player.Groups["body"].Value, System.StringComparison.Ordinal);
        Assert.Contains("Player.OpenAsync(path)", player.Groups["body"].Value, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Kayıt paylaşımı ikinci bir yükleyici yazmıyor: iş <c>ShareFlow</c> ve
    /// <c>Core/Share</c> katmanından geçiyor.
    /// </summary>
    [Fact]
    public void KayitPaylasimiAyniKatmandanGeciyor()
    {
        var code = ShareCode();
        Assert.Contains("new ShareFlow(", code, System.StringComparison.Ordinal);
        Assert.Contains("CoreShare.ShareProviderFactory.Create(", code, System.StringComparison.Ordinal);
        Assert.Contains("flow.ShareAsync(target, path", code, System.StringComparison.Ordinal);
        Assert.DoesNotContain("HttpRequestMessage", code, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Hedef tablosu paketle taşınıyor. Taşınmazsa <see cref="ShareTargetTable.Locate"/>
    /// kurulu yapıda dosyayı bulamaz ve paylaşım hiç başlamaz.
    /// </summary>
    [Fact]
    public void HedefTablosuPaketeGiriyor()
    {
        var proj = Read("src", "VidShrink.App", "VidShrink.App.csproj");
        Assert.Contains(ShareTargetTable.FileName, proj, System.StringComparison.Ordinal);
        Assert.Contains("CopyToPublishDirectory=\"PreserveNewest\"", proj, System.StringComparison.Ordinal);

        var table = Path.Combine(TipSources.Root, ShareTargetTable.FileName);
        Assert.True(File.Exists(table), $"{ShareTargetTable.FileName} kaynak ağacının kökünde yok.");
        Assert.NotEmpty(ShareTargetTable.Parse(File.ReadAllText(table)).Targets);
    }

    /// <summary>İki yeni anahtar kırk iki dilin hepsinde var.</summary>
    [Theory]
    [InlineData("recorder.output.to-shrink")]
    [InlineData("recorder.output.to-player")]
    public void YeniAnahtarlarButunDillerde(string key)
    {
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            Assert.True(values.ContainsKey(key), $"{language} dilinde {key} yok.");
            Assert.False(string.IsNullOrWhiteSpace(values[key]), $"{language} dilinde {key} boş.");
        }
    }
}
