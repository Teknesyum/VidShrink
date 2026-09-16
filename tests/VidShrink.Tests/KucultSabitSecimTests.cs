using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Küçült sekmesi, kullanıcı 09-04: dinamik çözünürlük varsayılan, kutu kalkınca sabit boy
/// seçilir; WhatsApp uyumu işaretlenirse kodek H.264'e kilitlenir. Ölçü pencerenin plana
/// verdiği <see cref="PlanOptions"/>'tan okunur.
/// </summary>
public sealed class KucultSabitSecimTests
{
    private static string SettingsFile()
    {
        var folder = Path.Combine(TestPaths.OutputRoot, "kucult-sabit");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "settings-" + Guid.NewGuid().ToString("N") + ".json");
    }

    [Fact]
    public void DinamikKutusuKalkincaSabitBoyPlanaGider()
    {
        var r = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = SettingsFile() };
            try
            {
                var rowHiddenWhileDynamic = !window.FixedResolutionRow.IsVisible;
                window.RbFixed720.IsChecked = true;
                var dynamicPlan = window.PlanOptionsForTest().FixedResolution;

                window.ChkResolution.IsChecked = false;
                var rowShown = window.FixedResolutionRow.IsVisible;
                var fixedPlan = window.PlanOptionsForTest().FixedResolution;

                window.RbFixed1080.IsChecked = true;
                var fixed1080 = window.PlanOptionsForTest().FixedResolution;

                window.RbFixedSource.IsChecked = true;
                var sourcePlan = window.PlanOptionsForTest().FixedResolution;
                var sourceKeepsResolution = window.PlanOptionsForTest().AllowResolutionDrop;

                return (rowHiddenWhileDynamic, dynamicPlan, rowShown, fixedPlan, fixed1080, sourcePlan, sourceKeepsResolution,
                    Label: window.RbFixed1080.Content as string);
            }
            finally { window.Close(); }
        });

        Assert.True(r.rowHiddenWhileDynamic, "dinamik kutu isaretliyken sabit boy satiri gorunuyor");
        Assert.Null(r.dynamicPlan);
        Assert.True(r.rowShown, "dinamik kutu kalkinca sabit boy satiri acilmadi");
        Assert.Equal(720, r.fixedPlan);
        Assert.Equal(1080, r.fixed1080);
        Assert.Null(r.sourcePlan);
        Assert.False(r.sourceKeepsResolution);
        Assert.Equal("1080p", r.Label);
    }

    [Fact]
    public void WhatsAppUyumuKodegiH264eKilitler()
    {
        var r = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = SettingsFile() };
            try
            {
                window.RbCodecSmallest.IsChecked = true;
                window.CmbAdvCodecLock.SelectedIndex = window.CmbAdvCodecLock.ItemCount - 1;
                var before = window.PlanOptionsForTest();

                window.ChkWhatsAppCompatible.IsChecked = true;
                var after = window.PlanOptionsForTest();
                var rowLocked = !window.CodecChoiceRow.IsEnabled;

                window.ChkWhatsAppCompatible.IsChecked = false;
                var released = window.PlanOptionsForTest();

                return (BeforeCodec: before.Codec, BeforeLock: before.LockedCodec, AfterCodec: after.Codec, AfterLock: after.LockedCodec,
                    rowLocked, ReleasedCodec: released.Codec, RowReleased: window.CodecChoiceRow.IsEnabled);
            }
            finally { window.Close(); }
        });

        Assert.Equal(CodecPreference.MaxCompression, r.BeforeCodec);
        Assert.NotNull(r.BeforeLock);
        Assert.Equal(CodecPreference.Compatible, r.AfterCodec);
        Assert.Null(r.AfterLock);
        Assert.True(r.rowLocked, "WhatsApp uyumu isaretliyken kodek secimi acik");
        Assert.Equal(CodecPreference.MaxCompression, r.ReleasedCodec);
        Assert.True(r.RowReleased, "WhatsApp uyumu kalkinca kodek secimi kilitli kaldi");
    }

    [Fact]
    public void KayitliSecimGeriYuklenincePlanVeSatirlarUyar()
    {
        var file = SettingsFile();
        try
        {
            new UpdateSettings { MayLowerResolution = false, FixedResolution = 3, WhatsAppCompatible = true }.Save(file);

            var r = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = file };
                try
                {
                    window.RestoreSettingsForTest(UpdateSettings.Load(file));
                    var options = window.PlanOptionsForTest();
                    return (options.FixedResolution, options.Codec, Row: window.FixedResolutionRow.IsVisible, CodecOpen: window.CodecChoiceRow.IsEnabled,
                        Saved: window.CaptureSettingsForTest());
                }
                finally { window.Close(); }
            });

            Assert.Equal(480, r.FixedResolution);
            Assert.Equal(CodecPreference.Compatible, r.Codec);
            Assert.True(r.Row, "geri yuklenen sabit boy satiri gizli");
            Assert.False(r.CodecOpen, "geri yuklenen WhatsApp uyumu kodek secimini kilitlemedi");
            Assert.Equal(3, r.Saved.FixedResolution);
            Assert.True(r.Saved.WhatsAppCompatible);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }
}
