using Avalonia.Controls;
using Avalonia.LogicalTree;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Ayarlar sekmesinde iki seçenekli açılır liste kalmadı: çıktı klasörü ve ffmpeg yolu radyo
/// şeridi. Seçim kaydedilir, geri yüklenir, seçici satırını açıp kapatır.
/// </summary>
public sealed class AyarRadyoSeridiTests
{
    private static string SettingsFile()
    {
        var folder = Path.Combine(TestPaths.OutputRoot, "ayar-radyo");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "settings-" + Guid.NewGuid().ToString("N") + ".json");
    }

    [Fact]
    public void GenelAyarlardaIkiSecenekliAcilirListeKalmaz()
    {
        var counts = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = SettingsFile() };
            try
            {
                return window.GeneralSettingsPanel.GetLogicalDescendants().OfType<ComboBox>()
                    .Select(box => (box.Name, box.ItemCount)).ToList();
            }
            finally { window.Close(); }
        });

        Assert.NotEmpty(counts);
        Assert.All(counts, entry => Assert.True(entry.ItemCount > 2, $"{entry.Name} yalniz {entry.ItemCount} secenek tasiyor"));
    }

    [Fact]
    public void DilVeTemaYanYanaDurur()
    {
        var columns = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = SettingsFile() };
            try
            {
                var row = window.LanguageThemeRow;
                int ColumnOf(Control control) => Grid.GetColumn((Control)control.GetLogicalParent()!);
                return (Parent: window.CmbLanguage.GetLogicalParent()!.GetLogicalParent() == row
                               && window.CmbTheme.GetLogicalParent()!.GetLogicalParent() == row,
                        Language: ColumnOf(window.CmbLanguage), Theme: ColumnOf(window.CmbTheme));
            }
            finally { window.Close(); }
        });

        Assert.True(columns.Parent, "dil ve tema ayni satirda degil");
        Assert.NotEqual(columns.Language, columns.Theme);
    }

    [Fact]
    public void RadyoSecimiSeciciyiAcarKaydedilirVeGeriYuklenir()
    {
        var file = SettingsFile();
        try
        {
            var result = AppHost.Run(() =>
            {
                var first = new MainWindow { SettingsPathOverride = file };
                bool outputRowShown, ffmpegRowShown, ffmpegErrorShown, outputRowHidden;
                try
                {
                    first.RbOutputFixed.IsChecked = true;
                    outputRowShown = first.OutputFolderPickerRow.IsVisible;
                    first.RbFfmpegManual.IsChecked = true;
                    ffmpegRowShown = first.FfmpegPathPickerRow.IsVisible;
                    ffmpegErrorShown = first.TxtFfmpegPathError.IsVisible;
                }
                finally { first.Close(); }

                var saved = AppSettings.Load(file);

                var second = new MainWindow { SettingsPathOverride = file };
                bool fixedRestored, manualRestored;
                try
                {
                    second.RestoreAppSettingsForTest(saved);
                    fixedRestored = second.RbOutputFixed.IsChecked == true && second.RbOutputBesideSource.IsChecked != true;
                    manualRestored = second.RbFfmpegManual.IsChecked == true && second.RbFfmpegAuto.IsChecked != true;
                    second.RbOutputBesideSource.IsChecked = true;
                    outputRowHidden = !second.OutputFolderPickerRow.IsVisible;
                }
                finally { second.Close(); }

                return (outputRowShown, ffmpegRowShown, ffmpegErrorShown, saved.OutputFolderMode, saved.FfmpegPathMode,
                    fixedRestored, manualRestored, outputRowHidden, AfterBeside: AppSettings.Load(file).OutputFolderMode);
            });

            Assert.True(result.outputRowShown, "sabit klasor secilince klasor secici acilmadi");
            Assert.True(result.ffmpegRowShown, "elle secilince ffmpeg yolu secici acilmadi");
            Assert.True(result.ffmpegErrorShown, "bos elle yol hata gostermedi");
            Assert.Equal(1, result.OutputFolderMode);
            Assert.Equal(1, result.FfmpegPathMode);
            Assert.True(result.fixedRestored, "sabit klasor radyosu geri yuklenmedi");
            Assert.True(result.manualRestored, "elle ffmpeg radyosu geri yuklenmedi");
            Assert.True(result.outputRowHidden, "kaynagin yani secilince klasor secici kapanmadi");
            Assert.Equal(0, result.AfterBeside);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }
}
