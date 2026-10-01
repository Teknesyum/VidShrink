using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using VidShrink.App.Playback;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kaldığı yerden devam yalnız son beş videodan biri ve dosya birebir aynıyken olur: bayt boyu,
/// son yazma zamanı ve süre tutmalı. Aynı ad tek başına yetmez; izi olmayan eski kayıt baştan açılır.
/// </summary>
public sealed class OynaticiDevamKosulTests : IDisposable
{
    private readonly string _klasor = Path.Combine(GirdiKanit.Root, ".calisma", "oynatici-devam", Guid.NewGuid().ToString("N")[..8]);

    public OynaticiDevamKosulTests() => Directory.CreateDirectory(_klasor);

    public void Dispose() => OynaticiListeTests.Sil(_klasor);

    private string Dosya(string ad, int bayt = 1000)
    {
        var yol = Path.Combine(_klasor, ad);
        File.WriteAllBytes(yol, new byte[bayt]);
        return yol;
    }

    private PlaybackHistory Dongu(PlaybackHistory gecmis)
    {
        var dosya = Path.Combine(_klasor, "gecmis.json");
        gecmis.Save(dosya);
        return PlaybackHistory.Load(dosya);
    }

    [Fact]
    public void AyniDosyaKaldigiYerdenAcilir()
    {
        var video = Dosya("a.mp4");
        var gecmis = new PlaybackHistory();
        gecmis.Remember(video, 42, false, 120);
        Assert.Equal(42, Dongu(gecmis).ResumeFor(video, 120), 3);
    }

    [Fact]
    public void BoyuDegisenAyniAdliDosyaBastanAcilir()
    {
        var video = Dosya("a.mp4");
        var gecmis = new PlaybackHistory();
        gecmis.Remember(video, 42, false, 120);
        var zaman = File.GetLastWriteTimeUtc(video);
        File.WriteAllBytes(video, new byte[1001]);
        File.SetLastWriteTimeUtc(video, zaman);
        Assert.Equal(0, Dongu(gecmis).ResumeFor(video, 120));
    }

    [Fact]
    public void YazmaZamaniDegisenDosyaBastanAcilir()
    {
        var video = Dosya("a.mp4");
        var gecmis = new PlaybackHistory();
        gecmis.Remember(video, 42, false, 120);
        File.SetLastWriteTimeUtc(video, File.GetLastWriteTimeUtc(video).AddSeconds(-5));
        Assert.Equal(0, Dongu(gecmis).ResumeFor(video, 120));
    }

    [Fact]
    public void SuresiTutmayanDosyaBastanAcilir()
    {
        var video = Dosya("a.mp4");
        var gecmis = new PlaybackHistory();
        gecmis.Remember(video, 42, false, 120);
        var geri = Dongu(gecmis);
        Assert.Equal(0, geri.ResumeFor(video, 121));
        Assert.Equal(42, geri.ResumeFor(video, 120.4), 3);
    }

    [Theory]
    [InlineData(4, 42)]
    [InlineData(5, 0)]
    public void YalnizSonBesVideoDevamEder(int sonrakiler, double beklenen)
    {
        var video = Dosya("a.mp4");
        var dosyalar = new JsonObject();
        dosyalar[video] = Kayit(video, 42, 1000);
        for (var i = 0; i < sonrakiler; i++)
        {
            var diger = Dosya($"b{i}.mp4");
            dosyalar[diger] = Kayit(diger, 10, 2000 + i);
        }

        var yol = Path.Combine(_klasor, "gecmis.json");
        File.WriteAllText(yol, new JsonObject { ["files"] = dosyalar }.ToJsonString());
        Assert.Equal(beklenen, PlaybackHistory.Load(yol).ResumeFor(video, 120), 3);
    }

    [Fact]
    public void IziOlmayanEskiKayitBastanAcilir()
    {
        var video = Dosya("a.mp4");
        var yol = Path.Combine(_klasor, "gecmis.json");
        var dosyalar = new JsonObject { [video] = new JsonObject { ["position"] = 42, ["seen"] = 1000, ["bookmarks"] = new JsonArray() } };
        File.WriteAllText(yol, new JsonObject { ["files"] = dosyalar }.ToJsonString());
        Assert.Equal(0, PlaybackHistory.Load(yol).ResumeFor(video, 120));
    }

    private static JsonObject Kayit(string yol, double konum, double gorulme)
    {
        var bilgi = new FileInfo(yol);
        return new JsonObject
        {
            ["position"] = konum,
            ["seen"] = gorulme,
            ["size"] = bilgi.Length,
            ["modified"] = bilgi.LastWriteTimeUtc.Ticks,
            ["duration"] = 120,
            ["bookmarks"] = new JsonArray()
        };
    }
}
