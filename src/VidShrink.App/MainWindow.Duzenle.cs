using System.Threading.Tasks;

namespace VidShrink.App;

public partial class MainWindow
{
    internal async Task OpenInEditorAsync(string path, double seconds = 0)
    {
        _media.Focus(path);
        if (Player.IsPlaying) Player.TogglePlay();
        var edit = EditorPane.EditAsync(path, seconds);
        Tabs.SelectedIndex = EditorTabIndex;
        await edit.ConfigureAwait(true);
    }
}
