using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.App.Localization;
using VidShrink.Core.Share;
using VidShrink.Core.Subtitles;

namespace VidShrink.App.Playback;

/// <summary>
/// P28: oynatılan videonun altyazısını OpenSubtitles'tan indirme.
/// </summary>
/// <remarks>
/// Anahtar kullanıcınındır ve Ayarlar'da girilir; depoda anahtar yoktur. Anahtar yokken
/// özellik kendini kapatır: menü satırı "anahtar nasıl alınır" der ve tıklanınca sağlayıcının
/// sayfasını açar, hata vermez. Sıralamayı ve ağı <see cref="ISubtitleProvider"/> yapar;
/// buradaki iş adayın ilkini indirtip dosyayı motora vermek ve her kolu kullanıcıya
/// tek satırla söylemek.
/// </remarks>
internal partial class PlayerView
{
    /// <summary>Testler buraya sahte sağlayıcı koyar; ağa çıkılmaz.</summary>
    internal Func<ISubtitleProvider>? SubtitleProviderSource { get; set; }

    private bool _subtitleDownloadRunning;

    /// <summary>Anahtar girilmiş mi; menü satırının iki yüzünden hangisinin çizileceği buna bakar.</summary>
    internal bool SubtitleDownloadReady => Provider().IsConfigured;

    private ISubtitleProvider Provider()
    {
        if (SubtitleProviderSource is { } source) return source();
        return new OpenSubtitlesProvider(new HttpClientTransport(), AppSettings.Load().OpenSubtitlesApiKey);
    }

    /// <summary>Kullanıcının anahtarı alacağı sayfa.</summary>
    internal static void OpenSubtitleKeyPage()
    {
        try { Process.Start(new ProcessStartInfo(OpenSubtitlesProvider.KeyPageUrl) { UseShellExecute = true }); }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or System.IO.IOException or InvalidOperationException)
        {
            // Tarayıcı açılamazsa yapacak bir şey yok; indirme zaten anahtarsız çalışmıyor.
        }
    }

    /// <summary>
    /// Arayüz dili önce, İngilizce sonra. Aynı kod iki kez yazılmaz.
    /// </summary>
    internal static IReadOnlyList<string> SubtitleLanguages(string uiLanguage)
    {
        var first = (uiLanguage ?? "").Trim().ToLowerInvariant();
        var cut = first.IndexOf('-');
        if (cut > 0) first = first[..cut];
        if (first.Length == 0) first = "en";
        return first == "en" ? new[] { "en" } : new[] { first, "en" };
    }

    internal async Task DownloadSubtitleAsync(CancellationToken cancellationToken = default)
    {
        if (_subtitleDownloadRunning) return;

        if (_path is not { } path || _engine is not { IsOpen: true })
        {
            Notice("player.subtitle.novideo");
            _trace.Add("subdl -> novideo");
            return;
        }

        var provider = Provider();
        if (!provider.IsConfigured)
        {
            Notice("player.subtitle.download.nokey");
            _trace.Add("subdl -> nokey");
            OpenSubtitleKeyPage();
            return;
        }

        _subtitleDownloadRunning = true;
        try
        {
            Notice("player.subtitle.download.working");
            _trace.Add("subdl -> working");

            var query = new SubtitleQuery(
                path,
                MovieHash.Compute(path),
                System.IO.Path.GetFileNameWithoutExtension(path),
                SubtitleLanguages(Strings.Language));

            var found = await provider.SearchAsync(query, cancellationToken).ConfigureAwait(true);
            if (found.Outcome != SubtitleOutcome.Ok || found.Candidates.Count == 0)
            {
                Report(found.Outcome == SubtitleOutcome.Ok ? SubtitleOutcome.NoResult : found.Outcome);
                return;
            }

            var pick = found.Candidates[0];
            _trace.Add("subdl pick -> " + pick.FileId.ToString(CultureInfo.InvariantCulture)
                       + " " + pick.Language + (pick.HashMatch ? " hash" : " name"));

            var got = await provider.DownloadAsync(pick, path, cancellationToken).ConfigureAwait(true);
            if (got.Outcome != SubtitleOutcome.Ok || got.Path is not { } file)
            {
                Report(got.Outcome);
                return;
            }

            if (!LoadSubtitle(file))
            {
                _trace.Add("subdl -> loadfailed");
                return;
            }

            Notice("player.subtitle.download.done", System.IO.Path.GetFileName(file));
            _trace.Add("subdl -> " + System.IO.Path.GetFileName(file));
        }
        finally
        {
            _subtitleDownloadRunning = false;
        }
    }

    /// <summary>
    /// Her kol ayrı bir anahtara düşer; kullanıcı "olmadı" değil ne olduğunu okur.
    /// </summary>
    internal static string NoticeKeyFor(SubtitleOutcome outcome) => outcome switch
    {
        SubtitleOutcome.NoKey => "player.subtitle.download.nokey",
        SubtitleOutcome.BadKey => "player.subtitle.download.badkey",
        SubtitleOutcome.NeedAccount => "player.subtitle.download.needaccount",
        SubtitleOutcome.NoResult => "player.subtitle.download.noresult",
        SubtitleOutcome.QuotaExceeded => "player.subtitle.download.quota",
        SubtitleOutcome.RateLimited => "player.subtitle.download.toofast",
        SubtitleOutcome.NetworkError => "player.subtitle.download.offline",
        SubtitleOutcome.WriteError => "player.subtitle.download.writefail",
        _ => "player.subtitle.download.offline"
    };

    private void Report(SubtitleOutcome outcome)
    {
        Notice(NoticeKeyFor(outcome));
        _trace.Add("subdl -> " + outcome.ToString().ToLowerInvariant());
    }

    private void Notice(string key, params object?[] args)
    {
        _trackNotice = key;
        _trackNoticeArgs = args.Length == 0 ? Array.Empty<object?>() : args;
        RefreshState();
    }
}
