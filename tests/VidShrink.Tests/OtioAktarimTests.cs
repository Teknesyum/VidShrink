using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

public sealed class OtioAktarimTests
{
    private const string Ana = @"C:\videolar\çekim 1.mp4";
    private const string Ek = @"C:\videolar\ikinci #2.mov";
    private const string Muzik = @"C:\müzik\tema.wav";

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static SourceStamp? Damga(string yol) => new(yol, yol.Length * 1000L, 638_000_000_000_000_000L + yol.Length);

    private static CutListContext Baglam(double fps, bool ses = true) => new(new[] { Ana, Ek }, fps) { HasAudio = ses, Title = "Kurgu" };

    private static EditTimeline Cizelge()
    {
        var model = new EditTimeline(new[]
        {
            new EditClip(S(10), S(40)) { Label = "Giriş" },
            new EditClip(S(100.123457), S(160), 1.5m, true) { Group = 3, Effects = ClipEffects.None with { Brightness = 0.2, Saturation = 1.5, VolumeDb = -3, FadeIn = S(1) } },
            new EditClip(S(5), S(25)) { Source = 1, Transition = new ClipTransition(TransitionKind.Wipe, S(1)) },
            new EditClip(S(300), S(330)) { Label = "Son", Effects = ClipEffects.None with { Rotation = 90, FlipH = true, Temperature = 5200 } },
        }, S(600), new[] { new TextLayer("Başlık", S(2), S(6)) { Bold = true } }, new[] { S(120) });
        model.SetMusic(new EditMusic(Muzik, S(200), S(12.5), -6));
        return model;
    }

    [Theory]
    [InlineData(25.0)]
    [InlineData(30000.0 / 1001.0)]
    [InlineData(60.0)]
    public void GidisDonusuProjeyiAynenKorur(double fps)
    {
        var model = Cizelge();
        var isaretler = new[] { new EditMarker(S(3), "A"), new EditMarker(S(1), null) };
        var beklenen = EditProject.From(Damga(Ana)!, model, extraSources: new[] { Damga(Ek)! }, markers: isaretler).ToJson();

        var okunan = Otio.Read(Otio.Write(model, Baglam(fps), isaretler));

        Assert.NotNull(okunan);
        Assert.Equal(0, okunan!.Skipped);
        Assert.Equal(new[] { Ana, Ek }, okunan.Sources);
        Assert.Equal(fps, okunan.Fps);
        Assert.Equal("Kurgu", okunan.Name);
        var proje = okunan.ToProject(Damga, out var eksik);
        Assert.Null(eksik);
        Assert.Equal(beklenen, proje!.ToJson());
    }

    [Fact]
    public void EksikKaynakProjeKurmaz()
    {
        var okunan = Otio.Read(Otio.Write(Cizelge(), Baglam(25)))!;
        var proje = okunan.ToProject(yol => yol == Ek ? null : Damga(yol), out var eksik);
        Assert.Null(proje);
        Assert.Equal(Ek, eksik);
    }

    [Fact]
    public void YazimSemayaUyar()
    {
        using var belge = JsonDocument.Parse(Otio.Write(Cizelge(), Baglam(25)));
        var kok = belge.RootElement;
        Assert.Equal("Timeline.1", kok.GetProperty("OTIO_SCHEMA").GetString());
        var yigin = kok.GetProperty("tracks");
        Assert.Equal("Stack.1", yigin.GetProperty("OTIO_SCHEMA").GetString());
        var izler = yigin.GetProperty("children").EnumerateArray().ToArray();
        Assert.Equal(new[] { "V1:Video", "A1:Audio", "A2:Audio" }, izler.Select(t => t.GetProperty("name").GetString() + ":" + t.GetProperty("kind").GetString()));
        Assert.All(izler, t => Assert.Equal("Track.1", t.GetProperty("OTIO_SCHEMA").GetString()));

        var klip = izler[0].GetProperty("children")[0];
        Assert.Equal("Clip.2", klip.GetProperty("OTIO_SCHEMA").GetString());
        var aralik = klip.GetProperty("source_range");
        Assert.Equal("TimeRange.1", aralik.GetProperty("OTIO_SCHEMA").GetString());
        Assert.Equal("RationalTime.1", aralik.GetProperty("start_time").GetProperty("OTIO_SCHEMA").GetString());
        Assert.Equal(250.0, aralik.GetProperty("start_time").GetProperty("value").GetDouble());
        Assert.Equal(750.0, aralik.GetProperty("duration").GetProperty("value").GetDouble());
        Assert.Equal(25.0, aralik.GetProperty("duration").GetProperty("rate").GetDouble());
        var kaynak = klip.GetProperty("media_references").GetProperty(klip.GetProperty("active_media_reference_key").GetString()!);
        Assert.Equal("ExternalReference.1", kaynak.GetProperty("OTIO_SCHEMA").GetString());
        Assert.Equal("file:///C:/videolar/%C3%A7ekim%201.mp4", kaynak.GetProperty("target_url").GetString());
        Assert.Equal(15000.0, kaynak.GetProperty("available_range").GetProperty("duration").GetProperty("value").GetDouble());

        var hizli = izler[0].GetProperty("children")[1].GetProperty("metadata").GetProperty("vidshrink");
        Assert.Equal(1.5m, hizli.GetProperty("speed").GetDecimal());
        Assert.True(hizli.GetProperty("reversed").GetBoolean());
        Assert.Equal(0.2, hizli.GetProperty("effects").GetProperty("brightness").GetDouble());
    }

    [Fact]
    public void MuzikBaslangiciBoslukOlarakYazilirVeSessizKaynakIziYazmaz()
    {
        using var belge = JsonDocument.Parse(Otio.Write(Cizelge(), Baglam(24, ses: false)));
        var izler = belge.RootElement.GetProperty("tracks").GetProperty("children").EnumerateArray().ToArray();
        Assert.Equal(new[] { "V1", "A2" }, izler.Select(t => t.GetProperty("name").GetString()));

        var muzik = izler[1].GetProperty("children").EnumerateArray().ToArray();
        Assert.Equal(new[] { "Gap.1", "Clip.2" }, muzik.Select(c => c.GetProperty("OTIO_SCHEMA").GetString()));
        Assert.Equal(300.0, muzik[0].GetProperty("source_range").GetProperty("duration").GetProperty("value").GetDouble());

        var okunan = Otio.Read(belge.RootElement.GetRawText())!;
        Assert.Equal(new EditMusic(Muzik, S(200), S(12.5), -6), okunan.Music);
    }

    private const string ElleYazilmis = """
        {
          "OTIO_SCHEMA": "Timeline.1",
          "metadata": {},
          "name": "Resolve kurgusu",
          "global_start_time": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 86400.0 },
          "tracks": {
            "OTIO_SCHEMA": "Stack.1",
            "metadata": {},
            "name": "tracks",
            "source_range": null,
            "effects": [],
            "markers": [],
            "enabled": true,
            "children": [
              {
                "OTIO_SCHEMA": "Track.1",
                "metadata": {},
                "name": "Video 1",
                "source_range": null,
                "effects": [],
                "markers": [],
                "enabled": true,
                "kind": "Video",
                "children": [
                  {
                    "OTIO_SCHEMA": "Clip.1",
                    "metadata": {},
                    "name": "A001",
                    "source_range": {
                      "OTIO_SCHEMA": "TimeRange.1",
                      "duration": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 48.0 },
                      "start_time": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 86424.0 }
                    },
                    "effects": [],
                    "markers": [],
                    "enabled": true,
                    "media_reference": {
                      "OTIO_SCHEMA": "ExternalReference.1",
                      "metadata": {},
                      "name": "a b.mov",
                      "available_range": {
                        "OTIO_SCHEMA": "TimeRange.1",
                        "duration": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 2400.0 },
                        "start_time": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 86400.0 }
                      },
                      "target_url": "file:///C:/Videos/a%20b.mov"
                    }
                  },
                  {
                    "OTIO_SCHEMA": "Gap.1",
                    "metadata": {},
                    "name": "",
                    "source_range": {
                      "OTIO_SCHEMA": "TimeRange.1",
                      "duration": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 24.0 },
                      "start_time": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 0.0 }
                    },
                    "effects": [],
                    "markers": [],
                    "enabled": true
                  },
                  {
                    "OTIO_SCHEMA": "Clip.2",
                    "metadata": { "Resolve": { "color": "Orange" } },
                    "name": "b.mp4",
                    "source_range": {
                      "OTIO_SCHEMA": "TimeRange.1",
                      "duration": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 72.0 },
                      "start_time": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 12.0 }
                    },
                    "effects": [],
                    "markers": [],
                    "enabled": true,
                    "media_references": {
                      "DEFAULT_MEDIA": {
                        "OTIO_SCHEMA": "ExternalReference.1",
                        "metadata": {},
                        "name": "b.mp4",
                        "available_range": null,
                        "available_image_bounds": null,
                        "target_url": "/media/b.mp4"
                      }
                    },
                    "active_media_reference_key": "DEFAULT_MEDIA"
                  },
                  {
                    "OTIO_SCHEMA": "Clip.1",
                    "metadata": { "vidshrink": { "speed": 2, "effects": { "brightness": "bozuk" } } },
                    "name": "Kapanış",
                    "source_range": {
                      "OTIO_SCHEMA": "TimeRange.1",
                      "duration": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 24.0 },
                      "start_time": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 86520.0 }
                    },
                    "effects": [],
                    "markers": [],
                    "enabled": true,
                    "media_reference": {
                      "OTIO_SCHEMA": "ExternalReference.1",
                      "metadata": {},
                      "name": "a b.mov",
                      "available_range": {
                        "OTIO_SCHEMA": "TimeRange.1",
                        "duration": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 2400.0 },
                        "start_time": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 86400.0 }
                      },
                      "target_url": "file:///C:/Videos/a%20b.mov"
                    }
                  }
                ]
              },
              {
                "OTIO_SCHEMA": "Track.1",
                "metadata": {},
                "name": "Video 2",
                "source_range": null,
                "effects": [],
                "markers": [],
                "enabled": true,
                "kind": "Video",
                "children": [
                  {
                    "OTIO_SCHEMA": "Clip.1",
                    "metadata": {},
                    "name": "logo",
                    "source_range": null,
                    "effects": [],
                    "markers": [],
                    "enabled": true,
                    "media_reference": { "OTIO_SCHEMA": "MissingReference.1", "metadata": {}, "name": "" }
                  }
                ]
              },
              {
                "OTIO_SCHEMA": "Track.1",
                "metadata": {},
                "name": "Audio 1",
                "source_range": null,
                "effects": [],
                "markers": [],
                "enabled": true,
                "kind": "Audio",
                "children": [
                  {
                    "OTIO_SCHEMA": "Clip.1",
                    "metadata": {},
                    "name": "A001",
                    "source_range": {
                      "OTIO_SCHEMA": "TimeRange.1",
                      "duration": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 48.0 },
                      "start_time": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 86424.0 }
                    },
                    "effects": [],
                    "markers": [],
                    "enabled": true,
                    "media_reference": {
                      "OTIO_SCHEMA": "ExternalReference.1",
                      "metadata": {},
                      "name": "a b.mov",
                      "available_range": null,
                      "target_url": "file:///C:/Videos/a%20b.mov"
                    }
                  }
                ]
              },
              {
                "OTIO_SCHEMA": "Track.1",
                "metadata": {},
                "name": "Audio 2",
                "source_range": null,
                "effects": [],
                "markers": [],
                "enabled": true,
                "kind": "Audio",
                "children": [
                  {
                    "OTIO_SCHEMA": "Gap.1",
                    "metadata": {},
                    "name": "",
                    "source_range": {
                      "OTIO_SCHEMA": "TimeRange.1",
                      "duration": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 48.0 },
                      "start_time": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 0.0 }
                    },
                    "effects": [],
                    "markers": [],
                    "enabled": true
                  },
                  {
                    "OTIO_SCHEMA": "Clip.1",
                    "metadata": {},
                    "name": "muzik",
                    "source_range": {
                      "OTIO_SCHEMA": "TimeRange.1",
                      "duration": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 240.0 },
                      "start_time": { "OTIO_SCHEMA": "RationalTime.1", "rate": 24.0, "value": 0.0 }
                    },
                    "effects": [],
                    "markers": [],
                    "enabled": true,
                    "media_reference": {
                      "OTIO_SCHEMA": "ExternalReference.1",
                      "metadata": {},
                      "name": "muzik.wav",
                      "available_range": {
                        "OTIO_SCHEMA": "TimeRange.1",
                        "duration": { "OTIO_SCHEMA": "RationalTime.1", "rate": 48000.0, "value": 14400000.0 },
                        "start_time": { "OTIO_SCHEMA": "RationalTime.1", "rate": 48000.0, "value": 0.0 }
                      },
                      "target_url": "file:///C:/Music/muzik.wav"
                    }
                  }
                ]
              }
            ]
          }
        }
        """;

    [Fact]
    public void ElleYazilmisOtioOkunur()
    {
        var okunan = Otio.Read(ElleYazilmis);

        Assert.NotNull(okunan);
        Assert.Equal("Resolve kurgusu", okunan!.Name);
        Assert.Equal(24.0, okunan.Fps);
        Assert.Equal(new[] { @"C:\Videos\a b.mov", "/media/b.mp4" }, okunan.Sources);
        Assert.Equal(new[] { S(100), S(3.5) }, okunan.Durations);
        Assert.Equal(
            new[] { (S(1), S(3), 0, "A001", 1m), (S(0.5), S(3.5), 1, (string?)null, 1m), (S(5), S(6), 0, null, 2m) },
            okunan.Clips.Select(c => (c.SourceStart, c.SourceEnd, c.Source, c.Label, c.Speed)));
        Assert.All(okunan.Clips, c => Assert.True(c.Effects.IsNeutral));
        Assert.Equal(new EditMusic(@"C:\Music\muzik.wav", S(300), S(2)), okunan.Music);
        Assert.Equal(2, okunan.Skipped);
        Assert.Empty(okunan.Texts);
    }

    [Fact]
    public void CokluIzdeIlkGoruntuIziAnaIzdir()
    {
        var model = new EditTimeline(new[] { new EditClip(S(0), S(4)), new EditClip(S(8), S(10)) }, S(20));
        var kok = JsonNode.Parse(Otio.Write(model, new CutListContext(new[] { Ana }, 25)))!;
        var izler = kok["tracks"]!["children"]!.AsArray();
        Assert.Equal(2, izler.Count);
        var v0 = izler[0]!.DeepClone();
        v0["name"] = "V0";
        var tek = v0["children"]!.AsArray();
        tek.RemoveAt(1);
        tek[0]!["media_references"]!["DEFAULT_MEDIA"]!["target_url"] = "file:///C:/videolar/baska.mp4";
        izler.Insert(0, v0);

        var okunan = Otio.Read(kok.ToJsonString())!;

        Assert.Equal(new[] { @"C:\videolar\baska.mp4" }, okunan.Sources);
        Assert.Equal(new[] { (S(0), S(4)) }, okunan.Clips.Select(c => (c.SourceStart, c.SourceEnd)));
        Assert.Equal(2, okunan.Skipped);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("""{"OTIO_SCHEMA":"Clip.2"}""")]
    [InlineData("""{"OTIO_SCHEMA":"Timeline.1","tracks":{"OTIO_SCHEMA":"Stack.1","children":[]}}""")]
    [InlineData("""{"OTIO_SCHEMA":"Timeline.1","tracks":{"OTIO_SCHEMA":"Stack.1","children":[{"OTIO_SCHEMA":"Track.1","kind":"Video","children":[{"OTIO_SCHEMA":"Gap.1"}]}]}}""")]
    [InlineData("""{"OTIO_SCHEMA":"Timeline.1","tracks":{"OTIO_SCHEMA":"Stack.1","children":[{"OTIO_SCHEMA":"Track.1","kind":"Video","children":[{"OTIO_SCHEMA":"Clip.1","source_range":{"duration":{"rate":0,"value":1}},"media_reference":{"OTIO_SCHEMA":"ExternalReference.1","target_url":"a.mp4"}}]}]}}""")]
    public void BozukYaDaKlipsizGirdiNullDoner(string json) => Assert.Null(Otio.Read(json));

    [Theory]
    [InlineData(@"C:\a b\c.mp4", "file:///C:/a%20b/c.mp4")]
    [InlineData("/home/u/v#1.mp4", "file:///home/u/v%231.mp4")]
    public void DosyaAdresiGidisDonusu(string yol, string adres)
    {
        Assert.Equal(adres, Otio.FileUrl(yol));
        Assert.Equal(yol, Otio.LocalPath(adres));
        Assert.Equal(@"C:\x\y.mov", Otio.LocalPath("file://localhost/C:/x/y.mov"));
    }
}
