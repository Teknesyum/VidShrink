using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.App.Playback;

/// <summary>
/// OYB-21: uyarlamalı yayında kalite seçimi. libmpv iz listesi yalnız seçili katmanın izlerini
/// taşıdığı için katmanlar ana listeden okunur (<see cref="HlsVariants"/>); seçim motorun
/// <c>hls-bitrate</c>'ine yazılır ve yayın aynı konumdan yeniden açılır. Menü iki katman varken görünür.
/// </summary>
internal partial class PlayerView
{
    private IReadOnlyList<HlsVariant> _streamVariants = Array.Empty<HlsVariant>();

    internal IReadOnlyList<HlsVariant> StreamVariants => _streamVariants;

    internal Func<string, CancellationToken, Task<IReadOnlyList<HlsVariant>>>? StreamVariantReader { get; set; } = ListTitleDisabled ? null : ReadStreamVariantsAsync;

    private void LoadStreamVariants(string path)
    {
        _streamVariants = Array.Empty<HlsVariant>();
        if (HlsVariants.IsCandidate(path) && StreamVariantReader is { } reader) _ = FetchStreamVariantsAsync(path, reader);
    }

    private async Task FetchStreamVariantsAsync(string path, Func<string, CancellationToken, Task<IReadOnlyList<HlsVariant>>> reader)
    {
        IReadOnlyList<HlsVariant> variants;
        try
        {
            variants = await reader(path, CancellationToken.None);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_path != path) return;
            _streamVariants = variants;
            _trace.Add("variants " + variants.Count.ToString(CultureInfo.InvariantCulture));
        });
    }

    internal static async Task<IReadOnlyList<HlsVariant>> ReadStreamVariantsAsync(string path, CancellationToken token)
        => await ReadListHeadAsync(path, token) is { } head ? HlsVariants.Parse(Encoding.UTF8.GetString(head)) : Array.Empty<HlsVariant>();

    private MenuItem? StreamQualityMenu()
        => _streamVariants.Count >= 2 && _engine is { IsOpen: true }
            ? Submenu(Strings.Get("player.tracks.quality"), BuildStreamQualityItems())
            : null;

    internal List<Control> BuildStreamQualityItems()
    {
        var current = _engine?.StreamBitrate ?? 0;
        return _streamVariants
            .Select(variant => (Control)Choice(VariantName(variant), variant.Bandwidth == current, () => _ = SelectStreamQualityAsync(variant.Bandwidth)))
            .ToList();
    }

    internal static string VariantName(HlsVariant variant)
    {
        var rate = Strings.BitHizi(Math.Round(variant.Bandwidth / 1000.0).ToString("N0", Strings.Culture));
        return variant.Height > 0
            ? variant.Width.ToString(CultureInfo.InvariantCulture) + "×" + variant.Height.ToString(CultureInfo.InvariantCulture) + " · " + rate
            : rate;
    }

    internal async Task<bool> SelectStreamQualityAsync(long bitrate)
    {
        if (_engine is not { IsOpen: true } engine || engine.StreamBitrate == bitrate) return false;
        _trace.Add("quality -> " + bitrate.ToString(CultureInfo.InvariantCulture));
        var done = await engine.SetStreamBitrateAsync(bitrate);
        if (!done) _trace.Add("quality -> failed");
        return done;
    }
}
