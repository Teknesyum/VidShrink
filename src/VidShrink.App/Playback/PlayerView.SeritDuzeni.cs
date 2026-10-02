using Avalonia.Controls;
using Avalonia.Layout;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    /// <summary>Alt şeridin iki satıra dizili olup olmadığı.</summary>
    internal bool SeritIkiSatir => StripRow.RowDefinitions.Count == 2;

    /// <summary>
    /// Alt şerit üç öbeği simetrik <c>*,Auto,*</c> ızgarada tutar; küçük kipte (738 px pencere)
    /// yan öbekler 225 px'e iniyor, ses çipi ve tam ekran düğmesi satırın dışına taşıyor, iki
    /// kaydırıcı sıfır genişlikte kalıyordu. Satır <c>CompactBreakpointWidth</c>'ten darsa
    /// oynatma öbeği üstte ortalı, ses ve hız öbekleri altta iki eşit sütunda durur; hiçbir
    /// denetim gizlenmez.
    /// </summary>
    private void SeritDuzeni(double genislik)
    {
        if (StripRow.Children.Count != 3 || genislik <= 0) return;
        var esik = this.TryFindResource("CompactBreakpointWidth", out var deger) && deger is double d ? d : 0;
        var dar = genislik < esik;
        if (dar == SeritIkiSatir) return;

        var sol = StripRow.Children[0];
        var orta = StripRow.Children[1];
        var sag = StripRow.Children[2];
        if (dar)
        {
            StripRow.ColumnDefinitions = new ColumnDefinitions("*,*");
            StripRow.RowDefinitions = new RowDefinitions("Auto,Auto");
            StripRow.RowSpacing = StripRow.ColumnSpacing;
            Yerlestir(orta, 0, 0, 2);
            orta.HorizontalAlignment = HorizontalAlignment.Center;
            Yerlestir(sol, 1, 0, 1);
            Yerlestir(sag, 1, 1, 1);
            return;
        }

        StripRow.RowDefinitions = new RowDefinitions();
        StripRow.ColumnDefinitions = new ColumnDefinitions("*,Auto,*");
        StripRow.RowSpacing = 0;
        Yerlestir(sol, 0, 0, 1);
        Yerlestir(orta, 0, 1, 1);
        orta.HorizontalAlignment = HorizontalAlignment.Stretch;
        Yerlestir(sag, 0, 2, 1);
    }

    private static void Yerlestir(Control parca, int satir, int sutun, int yayilma)
    {
        Grid.SetRow(parca, satir);
        Grid.SetColumn(parca, sutun);
        Grid.SetColumnSpan(parca, yayilma);
    }
}
