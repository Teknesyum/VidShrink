using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using VidShrink.Core;

namespace VidShrink.App;

public partial class MainWindow
{
    private string? _donusturKodlayiciKodegi;

    private void InitializeConvertEncoderUi()
    {
        foreach (var box in new[] { CmbConvertSpeed, CmbConvertTune, CmbConvertCodecProfile, CmbConvertLevel, CmbConvertColorSpace, CmbConvertColorRange })
            Watch(box, SelectingItemsControl.SelectedIndexProperty, OnConvertChanged);
        Watch(ChkConvertTwoPass, ToggleButton.IsCheckedProperty, OnConvertChanged);
        Watch(RbConvertHdrSdr, ToggleButton.IsCheckedProperty, OnConvertChanged);
        DonusturKodlayiciAlanlari();
    }

    private void OnConvertEncoderToggle(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConvertEncoderBody.IsVisible = !ConvertEncoderBody.IsVisible;
        ConvertEncoderGlyph.Classes.Set("open", ConvertEncoderBody.IsVisible);
    }

    private string DonusturSeciliKodek()
    {
        var tag = SelectedTag(CmbConvertCodec) ?? ConversionDefaults.VideoCodec;
        return IntermediateCodecs.FromId(tag)?.Encoder ?? tag;
    }

    /// <summary>
    /// DON-15/25/26: kodlayıcı kutuları seçili kodeğin listeleriyle dolar; kodek değişince seçim
    /// Otomatik'e döner. Kutu, kodeğin listesi boşsa gizlenir. HDR seçimi yalnız HDR kaynakta,
    /// renk seçimi yalnız SDR kaynakta görünür: HDR kolunda renk etiketlerini <see cref="HdrResolver"/> yazar.
    /// </summary>
    private void DonusturKodlayiciAlanlari()
    {
        var kodek = DonusturSeciliKodek();
        if (kodek != _donusturKodlayiciKodegi)
        {
            _donusturKodlayiciKodegi = kodek;
            var syncing = _syncing;
            _syncing = true;
            Doldur(CmbConvertSpeed, ConversionArguments.PresetOptions(kodek));
            Doldur(CmbConvertTune, ConversionArguments.TuneOptions(kodek));
            Doldur(CmbConvertCodecProfile, ConversionArguments.ProfileOptions(kodek));
            Doldur(CmbConvertLevel, ConversionArguments.LevelOptions(kodek));
            _syncing = syncing;
        }
        ConvertPresetRow.IsVisible = CmbConvertSpeed.ItemCount > 1;
        ConvertTuneRow.IsVisible = CmbConvertTune.ItemCount > 1;
        ConvertProfileRow.IsVisible = CmbConvertCodecProfile.ItemCount > 1;
        ConvertLevelRow.IsVisible = CmbConvertLevel.ItemCount > 1;
        ChkConvertTwoPass.IsVisible = ConversionArguments.TwoPassAvailable(kodek) && CmbQualityMode.SelectedIndex == 1;
        var hdr = ConvertInfo is { IsHdr: true };
        ConvertHdrRow.IsVisible = hdr && kodek != "copy";
        ConvertColorRow.IsVisible = ConvertRangeRow.IsVisible = !hdr;
    }

    private static void Doldur(ComboBox box, IReadOnlyList<string> secenekler)
    {
        var items = new List<ComboBoxItem> { new() { Content = Say("main.plan.automatic"), Tag = "" } };
        items.AddRange(secenekler.Select(value => new ComboBoxItem { Content = value, Tag = value }));
        box.ItemsSource = items;
        box.SelectedIndex = 0;
    }

    private static string? DonusturSecim(ComboBox box, IReadOnlyList<string> secenekler)
        => box.IsVisible && SelectedTag(box) is { Length: > 0 } value && secenekler.Contains(value) ? value : null;

    /// <summary>DON-15/25/26: <see cref="ReadConversionPlan"/>'ın kodlayıcı, HDR ve renk alanları.</summary>
    private ConversionPlan DonusturKodlayiciPlani(ConversionPlan plan)
    {
        var kodek = plan.VideoCodec;
        var renk = ConvertColorRow.IsVisible ? SelectedTag(CmbConvertColorSpace) : null;
        var aralik = ConvertRangeRow.IsVisible ? SelectedTag(CmbConvertColorRange) : null;
        return plan with
        {
            EncoderPreset = DonusturSecim(CmbConvertSpeed, ConversionArguments.PresetOptions(kodek)),
            Tune = DonusturSecim(CmbConvertTune, ConversionArguments.TuneOptions(kodek)),
            CodecProfile = DonusturSecim(CmbConvertCodecProfile, ConversionArguments.ProfileOptions(kodek)),
            Level = DonusturSecim(CmbConvertLevel, ConversionArguments.LevelOptions(kodek)),
            TwoPass = ChkConvertTwoPass.IsVisible && ChkConvertTwoPass.IsChecked == true,
            HdrPolicy = RbConvertHdrSdr.IsChecked == true ? HdrPolicy.TonemapToSdr : HdrPolicy.Preserve,
            Filters = plan.Filters with
            {
                ColorMatrix = renk switch
                {
                    "bt601" => ColorMatrixTarget.Bt601,
                    "bt709" => ColorMatrixTarget.Bt709,
                    "bt2020" => ColorMatrixTarget.Bt2020,
                    _ => ColorMatrixTarget.Keep
                }
            },
            ColorRange = aralik switch
            {
                "limited" => ColorRangeTarget.Limited,
                "full" => ColorRangeTarget.Full,
                _ => ColorRangeTarget.Keep
            }
        };
    }

    /// <summary>DON-15: iki geçişte önizleme iki komutu sırayla gösterir.</summary>
    private static string DonusturKomutu(MediaInfo info, ConversionPlan plan, string output)
    {
        if (!ConversionArguments.UsesTwoPass(info, plan))
            return FfmpegArguments.ToCommandLine(ConversionArguments.Build(info, plan, output, plan.Gif ? "palette.png" : null));
        const string log = "vidshrink_pass";
        return FfmpegArguments.ToCommandLine(ConversionArguments.Build(info, plan, output, pass: 1, passLogPrefix: log))
            + "\n" + FfmpegArguments.ToCommandLine(ConversionArguments.Build(info, plan, output, pass: 2, passLogPrefix: log));
    }
}
