using VidShrink.Player;

namespace VidShrink.App.Playback;

/// <summary>
/// Kabuktan gelen dosyanin motorunu pencere kurulurken acar. Olcum motorun kendi payini
/// 297 ms gosterdi ve o pay bugun acikligin en sonunda, pencere hazir olduktan sonra
/// oduniyor; oysa <c>mpv_create</c> ile <c>loadfile</c> arayuzden bagimsiz ve libmpv
/// zaten <see cref="Program.WarmPlayback"/> ile onden yukleniyor.
///
/// <para>Motor bir kez devralinir: <see cref="Devral"/> ayni yolu isteyen ilk cagirana
/// hazir motoru verir, ikinci cagiriya bos doner. Acilis basarisiz olursa gorev hatayi
/// tasir ve cagiran kendi motorunu kurar, boylece onisitma bir kirilma noktasi olmaz.</para>
/// </summary>
internal static class AcilisMotoru
{
    private static readonly object Kilit = new();
    private static string? _yol;
    private static Task<IPlaybackEngine>? _gorev;

    internal static void Isit(string yol, Func<IPlaybackEngine> uret)
    {
        lock (Kilit)
        {
            if (_gorev is not null) return;
            _yol = yol;
            _gorev = Task.Run(async () =>
            {
                var motor = uret();
                try
                {
                    await motor.OpenAsync(yol).ConfigureAwait(false);
                    return motor;
                }
                catch
                {
                    motor.Dispose();
                    throw;
                }
            });
        }
    }

    internal static Task<IPlaybackEngine>? Devral(string yol)
    {
        lock (Kilit)
        {
            if (_gorev is null || !string.Equals(_yol, yol, StringComparison.OrdinalIgnoreCase)) return null;
            var gorev = _gorev;
            _gorev = null;
            _yol = null;
            return gorev;
        }
    }

    internal static void Birak()
    {
        Task<IPlaybackEngine>? gorev;
        lock (Kilit)
        {
            gorev = _gorev;
            _gorev = null;
            _yol = null;
        }

        if (gorev is null) return;
        _ = gorev.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully) t.Result.Dispose();
        }, TaskScheduler.Default);
    }
}
