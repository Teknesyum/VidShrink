using System.Text.Json;
using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class HandBrakeOnAyarCeviriTests
{
    internal static readonly string FixturePath = Path.Combine(
        TipSources.Root, "tests", "VidShrink.Tests", "Veri", "handbrake", "sentetik-sosyal-10mb-720p.json");

    private static HandBrakeTranslation Single(string json) => Assert.Single(HandBrakePresetImport.Translate(json));

    private static string Synthetic(string fields) =>
        "{\"PresetList\":[{\"PresetName\":\"Deneme\",\"Type\":1" + fields + "}]}";

    [Fact]
    public void SentetikOnAyarHedefTabanliPlanaCevrilir()
    {
        var translation = Assert.Single(HandBrakePresetImport.TranslateFile(FixturePath));
        Assert.Equal("Sentetik Sosyal 10 MB 720p", translation.PresetName);

        var profile = translation.Profile;
        Assert.Equal("hb-sentetik-sosyal-10-mb-720p", profile.Id);
        Assert.Equal(PresetKind.User, profile.Kind);
        Assert.Equal(10, profile.TargetMb);
        Assert.True(profile.SizeCapped);
        Assert.Equal(FillPolicy.FillTarget, profile.Fill);
        Assert.Equal(CodecPreference.Compatible, profile.Codec);
        Assert.Equal(720, profile.MaxShortEdge);
        Assert.Equal(128, profile.AudioKbps);
        Assert.Equal(OutputContainer.Mp4, profile.Container);

        var plan = profile.ToPlanOptions();
        Assert.Equal(10, plan.TargetMb);
        Assert.Equal(720, plan.FixedResolution);
        Assert.Equal(128, plan.LockedAudioKbps);
    }

    [Fact]
    public void TasinanVeDusenAlanlarRaporlanir()
    {
        var translation = Assert.Single(HandBrakePresetImport.TranslateFile(FixturePath));

        Assert.Equal(PresetNoteOutcome.Carried, translation.Note("PresetName")!.Outcome);
        Assert.Equal(PresetNoteOutcome.Carried, translation.Note("PictureWidth")!.Outcome);
        Assert.Equal(PresetNoteOutcome.Carried, translation.Note("FileFormat")!.Outcome);

        var size = translation.Note("PresetName:MB")!;
        Assert.Equal((PresetNoteOutcome.Approximated, PresetNoteReason.SizeFromName, "10"), (size.Outcome, size.Reason, size.Value));
        Assert.Equal(PresetNoteOutcome.Approximated, translation.Note("VideoEncoder")!.Outcome);
        Assert.Equal("128", translation.Note("AudioList")!.Value);

        var bitrate = translation.Note("VideoAvgBitrate")!;
        Assert.Equal((PresetNoteOutcome.Dropped, PresetNoteReason.EngineDecides, "2100"), (bitrate.Outcome, bitrate.Reason, bitrate.Value));
        Assert.Equal(PresetNoteReason.EngineDecides, translation.Note("VideoFramerate")!.Reason);
        Assert.Equal(PresetNoteReason.NoEquivalent, translation.Note("PictureDenoiseFilter")!.Reason);
        Assert.Equal(PresetNoteReason.NoEquivalent, translation.Note("SubtitleBurnBehavior")!.Reason);
        Assert.Equal(PresetNoteReason.Structural, translation.Note("Type")!.Reason);
        Assert.Equal(PresetNoteReason.Structural, translation.Note("ChildrenArray")!.Reason);

        using var document = JsonDocument.Parse(File.ReadAllText(FixturePath));
        var fields = document.RootElement.GetProperty("PresetList")[0].GetProperty("ChildrenArray")[0]
            .EnumerateObject().Select(property => property.Name).ToList();
        Assert.Equal(fields.OrderBy(f => f, StringComparer.Ordinal),
            translation.Notes.Where(n => n.Field != "PresetName:MB").Select(n => n.Field).OrderBy(f => f, StringComparer.Ordinal));

        Assert.Contains(translation.Notes, note => note.Outcome == PresetNoteOutcome.Dropped);
        Assert.Contains(translation.Notes, note => note.Outcome == PresetNoteOutcome.Carried);
    }

    [Fact]
    public void KlasorCevrilmez()
    {
        var names = HandBrakePresetImport.TranslateFile(FixturePath).Select(t => t.PresetName).ToList();
        Assert.DoesNotContain("Sentetik Klasor", names);

        var nested = "{\"PresetList\":[{\"Folder\":true,\"PresetName\":\"Dis\",\"ChildrenArray\":[" +
                     "{\"Folder\":true,\"PresetName\":\"Ic\",\"ChildrenArray\":[{\"PresetName\":\"A 5 MB\"}]}," +
                     "{\"PresetName\":\"B\"}]}]}";
        Assert.Equal(new[] { "A 5 MB", "B" }, HandBrakePresetImport.Translate(nested).Select(t => t.PresetName));
    }

    [Fact]
    public void AddaBoyutYoksaKaliteTavani()
    {
        var crf = Single(Synthetic(",\"VideoEncoder\":\"x265_10bit\",\"VideoQualityType\":2,\"VideoQualitySlider\":22,\"PictureWidth\":1920,\"PictureHeight\":1080"));
        Assert.Null(crf.Profile.TargetMb);
        Assert.False(crf.Profile.SizeCapped);
        Assert.Equal(FillPolicy.QualityCeiling, crf.Profile.Fill);
        Assert.Equal(CodecPreference.Auto, crf.Profile.Codec);
        Assert.Equal(1080, crf.Profile.MaxShortEdge);
        Assert.Null(crf.Note("PresetName:MB"));

        var sizedQuality = Single("{\"PresetName\":\"Kalite 8 MB\",\"VideoQualityType\":2}");
        Assert.False(sizedQuality.Profile.SizeCapped);
        Assert.Null(sizedQuality.Profile.TargetMb);

        var portrait = Single(Synthetic(",\"PictureWidth\":721,\"PictureHeight\":1281"));
        Assert.Equal(720, portrait.Profile.MaxShortEdge);

        var noPicture = Single(Synthetic(",\"PictureWidth\":0,\"PictureHeight\":720"));
        Assert.Null(noPicture.Profile.MaxShortEdge);
        Assert.Equal(PresetNoteOutcome.Dropped, noPicture.Note("PictureWidth")!.Outcome);

        Assert.Equal(CodecPreference.MaxCompression, Single(Synthetic(",\"VideoEncoder\":\"svt_av1\"")).Profile.Codec);
    }

    [Fact]
    public void DesteklenmeyenKodlayiciVeKapDuser()
    {
        var vp8 = Single(Synthetic(",\"VideoEncoder\":\"VP8\",\"FileFormat\":\"av_avi\",\"AudioList\":[]"));
        var encoder = vp8.Note("VideoEncoder")!;
        Assert.Equal((PresetNoteOutcome.Dropped, PresetNoteReason.UnsupportedCodec), (encoder.Outcome, encoder.Reason));
        Assert.Null(vp8.Profile.LockedCodec);
        Assert.Equal(PresetNoteReason.NoEquivalent, vp8.Note("FileFormat")!.Reason);
        Assert.Null(vp8.Profile.Container);
        Assert.Equal(PresetNoteOutcome.Dropped, vp8.Note("AudioList")!.Outcome);
        Assert.Null(vp8.Profile.AudioKbps);

        Assert.Equal(OutputContainer.Mkv, Single(Synthetic(",\"FileFormat\":\"av_mkv\"")).Profile.Container);
    }

    /// <summary>
    /// WebM gerçek bir kol: kap WebM olarak taşınıyor ve kodek libvpx-vp9'a kilitleniyor,
    /// kodlayıcı adı verilmese de (HandBrake'in WebM'i VP9 ister). mkv karşılaştırma için taşınıyor.
    /// </summary>
    [Theory]
    [InlineData("av_webm", "")]
    [InlineData("webm", "")]
    [InlineData("av_webm", ",\"VideoEncoder\":\"VP9\"")]
    public void WebmVp9OlarakTasiniyor(string bicim, string kodlayici)
    {
        var ceviri = Single(Synthetic($",\"FileFormat\":\"{bicim}\"{kodlayici}"));
        Assert.Equal(OutputContainer.WebM, ceviri.Profile.Container);
        Assert.Equal("libvpx-vp9", ceviri.Profile.LockedCodec);
        Assert.Equal("libvpx-vp9", ceviri.Profile.ToPlanOptions().LockedCodec);
        Assert.Equal(PresetNoteOutcome.Carried, ceviri.Note("FileFormat")!.Outcome);
        Assert.Equal(PresetNoteOutcome.Carried, Single(Synthetic(",\"FileFormat\":\"av_mkv\"")).Note("FileFormat")!.Outcome);
    }

    /// <summary>
    /// VP9 kodlayıcısı birebir taşınıyor; 10 bit türevi 8 bit libvpx-vp9'a yaklaşıyor. WebM'e
    /// VP9 dışı bir kodlayıcı adı verilince kilit kurulmuyor ve kap notu "yaklaşık" kalıyor.
    /// </summary>
    [Fact]
    public void Vp9KodlayicisiKilitOluyor()
    {
        var vp9 = Single(Synthetic(",\"VideoEncoder\":\"VP9\",\"FileFormat\":\"av_mkv\""));
        Assert.Equal("libvpx-vp9", vp9.Profile.LockedCodec);
        Assert.Equal(PresetNoteOutcome.Carried, vp9.Note("VideoEncoder")!.Outcome);

        var onBit = Single(Synthetic(",\"VideoEncoder\":\"vp9_10bit\""));
        Assert.Equal("libvpx-vp9", onBit.Profile.LockedCodec);
        Assert.Equal(PresetNoteOutcome.Approximated, onBit.Note("VideoEncoder")!.Outcome);

        var av1Webm = Single(Synthetic(",\"VideoEncoder\":\"svt_av1\",\"FileFormat\":\"av_webm\""));
        Assert.Null(av1Webm.Profile.LockedCodec);
        Assert.Equal(PresetNoteOutcome.Approximated, av1Webm.Note("FileFormat")!.Outcome);

        Assert.Null(Single(Synthetic(",\"VideoEncoder\":\"x264\"")).Profile.LockedCodec);
    }

    [Fact]
    public void CiftAnahtarTekNotVerir()
    {
        var duplicate = Single("{\"PresetName\":\"Cift 4 MB\",\"Type\":0,\"Type\":1}");
        Assert.Single(duplicate.Notes, note => note.Field == "Type");
        Assert.Equal(4, duplicate.Profile.TargetMb);
    }

    [Fact]
    public void GecersizGirdiAnlasilirHataVerir()
    {
        var broken = Assert.Throws<PresetFileException>(() => HandBrakePresetImport.Translate("{\"PresetList\": ["));
        Assert.Equal(PresetFileError.NotJson, broken.Error);

        var empty = Assert.Throws<PresetFileException>(() => HandBrakePresetImport.Translate("{\"PresetList\": []}"));
        Assert.Equal(PresetFileError.NoHandBrakePreset, empty.Error);
        Assert.Equal("main.preset.error.no-hand-brake-preset", empty.LocaleKey);

        var ours = PresetLibrary.Serialize(PresetLibrary.BuiltIn.Profiles.Take(2));
        Assert.Equal(PresetFileError.NoHandBrakePreset,
            Assert.Throws<PresetFileException>(() => HandBrakePresetImport.Translate(ours)).Error);
    }
}
