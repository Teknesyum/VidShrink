using Avalonia.Automation;

namespace VidShrink.App;

public partial class MainWindow
{
    private void YongalariAdlandir()
    {
        foreach (var (yonga, hedef) in QualityChips())
            if (hedef is not null && yonga.Content is string sayi)
                AutomationProperties.SetName(yonga, Say("main.unit.mb-value", sayi));
    }
}
