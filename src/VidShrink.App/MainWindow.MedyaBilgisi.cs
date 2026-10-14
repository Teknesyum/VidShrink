using Avalonia.Interactivity;

namespace VidShrink.App;

/// <summary>
/// ARC-01: Küçült'ün kaynak kartındaki "tüm ayrıntılar" düğmesi. Döküm başlık seçiminden önceki
/// tam yoklamadan yazılır; seçili başlığa daraltılmış bilgi diğer izleri saklardı.
/// </summary>
public partial class MainWindow
{
    internal MediaInfoWindow? ShowMediaDetails()
    {
        if ((_probed ?? _info) is not { } info) return null;
        var window = new MediaInfoWindow(info.FilePath, info);
        window.Show(this);
        return window;
    }

    private void OnMediaDetails(object? sender, RoutedEventArgs e) => ShowMediaDetails();
}
