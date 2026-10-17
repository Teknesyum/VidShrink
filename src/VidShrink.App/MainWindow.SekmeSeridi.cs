using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace VidShrink.App;

public partial class MainWindow
{
    private void SekmeSeridiOdagi() =>
        Tabs.AddHandler(GotFocusEvent, (_, _) => SekmeSeridiniSabitle(), RoutingStrategies.Bubble, handledEventsToo: true);

    private void SekmeSeridiniSabitle()
    {
        if (Tabs.ItemsPanelRoot is not { } serit) return;
        if (Tabs.ContainerFromIndex(Tabs.SelectedIndex) is not TabItem secili) return;
        if (ReferenceEquals(KeyboardNavigation.GetTabOnceActiveElement(serit), secili)) return;
        KeyboardNavigation.SetTabOnceActiveElement(serit, secili);
    }
}
