using System.Diagnostics;
using VidShrink.App;

namespace VidShrink.Tests;

public sealed class JitProfiliTests
{
    [Fact]
    public void KlasorAyarYolunuIzler()
    {
        var ayar = Path.Combine("C:\\olcum", "kovan", "settings.json");
        Assert.Equal(Path.Combine("C:\\olcum", "kovan"), JitProfili.Klasor(ayar, "C:\\yerel"));
    }

    [Fact]
    public void AyarYoluYoksaYerelVeriKlasorunuKullanir()
    {
        Assert.Equal(Path.Combine("C:\\yerel", "VidShrink"), JitProfili.Klasor(null, "C:\\yerel"));
        Assert.Equal(Path.Combine("C:\\yerel", "VidShrink"), JitProfili.Klasor(" ", "C:\\yerel"));
        Assert.Null(JitProfili.Klasor(null, ""));
    }

    [Fact]
    public async Task ProfilAcilisGoruntusundenOnceYazilmaz()
    {
        var acilis = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var yazilan = 0;
        var is_ = JitProfili.DurdurAsync(acilis.Task, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(50), () => Interlocked.Increment(ref yazilan));

        await Task.Delay(400);
        Assert.Equal(0, Volatile.Read(ref yazilan));
        Assert.False(is_.IsCompleted);

        var saat = Stopwatch.StartNew();
        acilis.SetResult();
        Assert.True(await is_.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(1, yazilan);
        Assert.True(saat.ElapsedMilliseconds >= 40);
    }

    [Fact]
    public async Task GoruntuGelmezseYedekBeklemedenSonraYazilir()
    {
        var yazilan = 0;
        var sonuc = await JitProfili.DurdurAsync(new TaskCompletionSource().Task, TimeSpan.FromMilliseconds(100), TimeSpan.Zero, () => yazilan++)
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(sonuc);
        Assert.Equal(1, yazilan);
    }

    [Fact]
    public async Task YazmaHatasiAcilisiDusurmez()
    {
        var sonuc = await JitProfili.DurdurAsync(Task.CompletedTask, TimeSpan.FromSeconds(30), TimeSpan.Zero, () => throw new IOException("disk"))
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(sonuc);
    }

    [Fact]
    public void ProfilYalnizTekOrnekSahibindeBaslar()
    {
        var program = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "Program.cs"));
        var sahiplik = program.IndexOf("if (!instance.IsOwner)", StringComparison.Ordinal);
        var baslat = program.IndexOf("JitProfili.Baslat(AcilisBitti.Task);", StringComparison.Ordinal);
        Assert.True(sahiplik > 0);
        Assert.True(baslat > sahiplik);
        Assert.Equal(baslat, program.LastIndexOf("JitProfili.Baslat(", StringComparison.Ordinal));
    }
}
