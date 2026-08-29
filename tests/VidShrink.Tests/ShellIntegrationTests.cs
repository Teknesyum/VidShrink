using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// T67: kabuğun verdiği dosya yolu açılışta yükleniyor mu.
///
/// İki katman ölçülüyor. Argüman çözümü saf bir işlev, doğrudan çağrılıyor. Pencere
/// tarafı için <c>Loaded</c> olayı başsız koşuda ateşlenmediğinden açılış yüklemesi
/// elle başlatılıyor; yükleyicinin arayüz iş parçacığına dönen kuyruğu
/// <see cref="Dispatcher.UIThread"/> elle boşaltılarak yürütülüyor.
/// </summary>
public sealed class ShellIntegrationTests : IDisposable
{
    private readonly string _root =
        Path.Combine(TestPaths.OutputRoot, "shell-integration", Guid.NewGuid().ToString("n"));

    public ShellIntegrationTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private string Write(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, "veri");
        return path;
    }

    [Fact]
    public void Media_extension_list_is_the_twenty_four_the_application_opens()
    {
        Assert.Equal(
            new[]
            {
                "mp4", "mkv", "mov", "avi", "webm", "wmv", "flv", "m4v", "mpg", "mpeg", "ts", "m2ts",
                "3gp", "ogv", "vob", "asf", "rm", "rmvb", "divx", "mxf", "f4v", "mts", "dav", "gif"
            },
            ShellIntegration.MediaExtensions);
    }

    [Fact]
    public void Single_existing_path_resolves()
    {
        var file = Write("kayit.mp4");
        Assert.Equal(file, ShellIntegration.ResolveStartupPath(new[] { file }));
    }

    [Fact]
    public void Quoted_path_loses_its_quotes()
    {
        var file = Write("kayit.mp4");
        Assert.Equal(file, ShellIntegration.ResolveStartupPath(new[] { "\"" + file + "\"" }));
    }

    [Fact]
    public void Path_broken_into_pieces_on_its_spaces_resolves()
    {
        var file = Write("tatil cekimi 2160p.mp4");
        var pieces = file.Split(' ');

        Assert.True(pieces.Length > 1, "Ölçüm boşluklu yolu parçalayamadı.");
        Assert.Equal(file, ShellIntegration.ResolveStartupPath(pieces));
    }

    [Fact]
    public void Missing_path_returns_null()
        => Assert.Null(ShellIntegration.ResolveStartupPath(new[] { Path.Combine(_root, "yok.mp4") }));

    [Fact]
    public void Folder_returns_null()
        => Assert.Null(ShellIntegration.ResolveStartupPath(new[] { _root }));

    [Fact]
    public void Empty_and_blank_arguments_return_null()
    {
        Assert.Null(ShellIntegration.ResolveStartupPath(Array.Empty<string>()));
        Assert.Null(ShellIntegration.ResolveStartupPath(new[] { "", "   " }));
        Assert.Null(ShellIntegration.ResolveStartupPath(null));
    }

    [Fact]
    public void Startup_file_goes_through_the_loader_drag_and_drop_uses()
    {
        var file = Write("belge.txt");

        var (fileName, status, visible) = AppHost.Run(() =>
        {
            var window = new MainWindow(file);
            Drain(window.LoadStartupFileAsync());
            return (Text(window, "TxtFileName"), Text(window, "TxtSourceStatus"), Shown(window, "TxtSourceStatus"));
        });

        Assert.Equal("belge.txt", fileName);
        Assert.True(visible, "Medya olmayan dosyada kaynak hatası satırı görünmedi.");
        Assert.False(string.IsNullOrWhiteSpace(status));
    }

    [Fact]
    public void Window_without_an_argument_loads_nothing()
    {
        var (completed, fileName, visible) = AppHost.Run(() =>
        {
            var window = new MainWindow(ShellIntegration.ResolveStartupPath(Array.Empty<string>()));
            var loading = window.LoadStartupFileAsync();
            return (loading.IsCompletedSuccessfully, Text(window, "TxtFileName"), Shown(window, "TxtSourceStatus"));
        });

        Assert.True(completed, "Argümansız pencere açılış yüklemesinde bekledi.");
        Assert.False(visible);
        Assert.NotEqual("", fileName);
    }

    private static string? Text(MainWindow window, string name)
        => window.FindControl<TextBlock>(name)?.Text;

    private static bool Shown(MainWindow window, string name)
        => window.FindControl<TextBlock>(name)?.IsVisible ?? false;

    private static void Drain(Task work)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!work.IsCompleted && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Assert.True(work.IsCompleted, "Açılış yüklemesi süresinde bitmedi.");
        work.GetAwaiter().GetResult();
    }
}
