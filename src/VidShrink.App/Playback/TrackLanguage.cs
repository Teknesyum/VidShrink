using System.Collections.Generic;
using System.Linq;
using VidShrink.Core;
using VidShrink.Player;

namespace VidShrink.App.Playback;

/// <summary>
/// Açılışta iz seçimi (OYA-P5). Ses: tercih edilen dildeki ilk iz, seçili olan zaten o dildeyse çağrı yok.
/// Tam altyazı yalnız duyulan dil tercih edilen dil değilse açılır. Tam altyazı açılmadıysa zorunlu iz
/// kendiliğinden açılır: tercih edilen dil → duyulan dil → ilk zorunlu iz (<see cref="StreamMapping.DefaultForced"/> sırası).
/// Dış altyazı bu seçime girmez; yandaki dosyalar kendi kuralıyla yüklenir. 0 dönen alan dokunulmaz demektir.
/// </summary>
internal static class TrackLanguage
{
    internal readonly record struct Choice(long Audio, long Subtitle);

    internal static Choice Pick(IReadOnlyList<PlaybackTrack> tracks, string? audioLanguage, string? subtitleLanguage)
    {
        var audio = tracks.Where(track => track.Kind == PlaybackTrackKind.Audio).ToList();
        var selected = audio.FirstOrDefault(track => track.Selected);
        var wanted = audio.Where(track => StreamMapping.LanguageMatches(track.Language, audioLanguage)).ToList();
        var pick = wanted.FirstOrDefault(track => track.Selected) ?? wanted.FirstOrDefault();
        var heard = pick ?? selected ?? audio.FirstOrDefault();
        var audioId = pick is { Selected: false } ? pick.Id : 0;

        var subtitles = tracks.Where(track => track.Kind == PlaybackTrackKind.Subtitle && !track.External).ToList();
        if (!StreamMapping.LanguageMatches(heard?.Language, subtitleLanguage)
            && subtitles.FirstOrDefault(track => !track.Forced && StreamMapping.LanguageMatches(track.Language, subtitleLanguage)) is { } full)
            return new Choice(audioId, full.Id);

        var forced = subtitles.Where(track => track.Forced).ToList();
        var forcedPick = forced.FirstOrDefault(track => StreamMapping.LanguageMatches(track.Language, subtitleLanguage))
            ?? forced.FirstOrDefault(track => StreamMapping.LanguageMatches(track.Language, heard?.Language))
            ?? forced.FirstOrDefault();
        return new Choice(audioId, forcedPick?.Id ?? 0);
    }
}
