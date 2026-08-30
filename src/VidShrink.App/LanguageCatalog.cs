using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using VidShrink.App.Localization;

// T27: ipucu satırlarının genişliği ekranda görünen metinle ölçülüyor. Ekranda görünen
// metin ham metin değil, Title() geçidinden çıkmış hâli — büyük harf daha geniştir.
// Ölçüm o geçidi yeniden yazmak yerine buradakini çağırsın diye test projesi içeri alındı.
[assembly: InternalsVisibleTo("VidShrink.Tests")]

namespace VidShrink.App;

internal static class LanguageCatalog
{
    /// <summary>
    /// Turkish casing. The invariant culture maps <c>i</c> to <c>I</c> and writes "Islem" where
    /// "İşlem" belongs, so every Turkish capitalisation goes through this culture instead.
    /// </summary>
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Conjunctions stay lower case unless they open the line.</summary>
    private static readonly HashSet<string> Conjunctions =
        new(StringComparer.Ordinal) { "ve", "veya", "ile", "ki", "da", "de" };

    /// <summary>
    /// Names and abbreviations that are written a fixed way. A word typed in lower case here is
    /// restored to its own spelling instead of getting a single capital.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Names =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ffmpeg"] = "FFmpeg",
            ["mp4"] = "MP4",
            ["mp3"] = "MP3",
            ["m4a"] = "M4A",
            ["wav"] = "WAV",
            ["mkv"] = "MKV",
            ["mov"] = "MOV",
            ["avi"] = "AVI",
            ["gif"] = "GIF",
            ["webm"] = "WebM",
            ["pcm"] = "PCM",
            ["crf"] = "CRF",
            ["json"] = "JSON",
            ["gpu"] = "GPU",
            // Servis adları kendi yazımlarını korur; büyük harf kuralı "Storage.to" yazardı.
            ["storage"] = "storage",
            ["uguu"] = "uguu",
            ["cpu"] = "CPU",
            ["api"] = "API",
            ["hdr"] = "HDR",
            ["sdr"] = "SDR",
            ["av1"] = "AV1",
            ["vp9"] = "VP9",
            ["whatsapp"] = "WhatsApp",
            ["vidshrink"] = "VidShrink",
            ["teknesyum"] = "Teknesyum",
            ["windows"] = "Windows",
        };

    /// <summary>
    /// Yazıldığı gibi kalan sözcükler: ölçü birimleri ve ffmpeg kodlayıcı/kodek
    /// tanımlayıcıları. Büyük harf kuralı ikisini de bozuyor — <c>ms</c> SI'da megasaniye
    /// olan <c>Ms</c>'e, <c>libx264</c> ffmpeg'in tanımadığı <c>Libx264</c>'e dönerdi.
    /// Kullanıcı kodlayıcı adını kaydedicisinin ayarlarında arıyor; yazımı değişirse
    /// bulamaz.
    ///
    /// <para>Zaten büyük harf taşıyan birimler (<c>MB</c>, <c>Hz</c>, <c>dB</c>) kuraldan
    /// halihazırda muaf; kendi yazımlarını burada da bildirmek listenin birim tarafını
    /// eksiksiz tutuyor. Liste tek yerde durur: ikiye bölünürse bir süre sonra ayrışır.</para>
    /// </summary>
    private static readonly HashSet<string> Verbatim =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ms", "kB", "MB", "GB", "kbps", "Mbps", "fps", "Hz", "kHz", "dB", "px",
            "libx264", "libx265", "libsvtav1", "libvpx", "libopus", "libmp3lame", "libvmaf",
            "h264_nvenc", "hevc_nvenc", "av1_nvenc",
            "h264_qsv", "hevc_qsv", "av1_qsv",
            "h264_amf", "hevc_amf", "av1_amf",
            "aac", "opus"
        };

    /// <summary>
    /// Marka yazımları. Tek sözcük kuralı bunları bozar — "Buy me a coffee" sözcük sözcük
    /// büyütülünce "Buy Me A Coffee" olur, oysa markanın kendi yazımı "Buy Me a Coffee".
    /// Bütün dizge eşleşince yazım olduğu gibi döner; bu bir çeviri değil, sabit yazımdır.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> Brands =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Buy me a coffee"] = "Buy Me a Coffee",
        };

    /// <summary>
    /// Capitalises the first letter of every word. Words that already carry a capital anywhere
    /// (MP4, GPU, H.264, WhatsApp, FFmpeg, VidShrink) and words that do not start with a letter
    /// (8, 1280x720, 00:01:30) are handed back untouched, so nothing is invented and nothing is
    /// flattened. Applying it twice changes nothing.
    /// </summary>
    internal static string Title(string text, bool turkish)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (Brands.TryGetValue(text, out var brand)) return brand;
        var culture = turkish ? TurkishCulture : CultureInfo.InvariantCulture;
        var builder = new StringBuilder(text.Length);
        var index = 0;
        var lineStart = true;

        while (index < text.Length)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                if (text[index] == '\n') lineStart = true;
                builder.Append(text[index]);
                index++;
                continue;
            }

            var end = index;
            while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
            var word = text[index..end];
            builder.Append(CapitaliseWord(word, culture, lineStart));
            lineStart = false;
            index = end;
        }

        return builder.ToString();
    }

    private static string CapitaliseWord(string word, CultureInfo culture, bool lineStart)
    {
        var offset = 0;
        while (offset < word.Length && !char.IsLetter(word[offset]))
        {
            // A digit before the first letter means the word is a measurement, not a name.
            if (char.IsDigit(word[offset])) return word;
            offset++;
        }

        if (offset == word.Length) return word;

        var body = word[offset..];
        var bare = new string(body.TakeWhile(char.IsLetter).ToArray());
        var token = new string(body.TakeWhile(char.IsLetterOrDigit).ToArray());

        var identifier = new string(body.TakeWhile(letter => char.IsLetterOrDigit(letter) || letter == '_').ToArray());
        if (Verbatim.Contains(identifier) || Verbatim.Contains(token) || Verbatim.Contains(bare)) return word;
        if (Names.TryGetValue(token, out var known))
            return string.Concat(word.AsSpan(0, offset), known, body.AsSpan(token.Length));
        if (Names.TryGetValue(bare, out var name))
            return string.Concat(word.AsSpan(0, offset), name, body.AsSpan(bare.Length));

        foreach (var letter in body)
            if (char.IsUpper(letter))
                return word;

        if (!lineStart && Conjunctions.Contains(bare)) return word;

        return string.Concat(
            word.AsSpan(0, offset),
            body[..1].ToUpper(culture),
            body.AsSpan(1));
    }

    /// <summary>
    /// Metnin ekrana çıkmadan önce geçtiği tek kapı. Yürürlükteki dilin büyük harf kuralı
    /// burada uygulanır: Türkçede <c>i</c> harfi <c>İ</c> olur, öteki dillerde olmaz.
    /// Çeviri değil, dil kuralıdır — sözlük <c>Locales</c> altında durur.
    /// </summary>
    internal static string Display(string text)
        => Title(text, Strings.Language.StartsWith("tr", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Motorun ürettiği doğrulama iletisinin anahtarı. Buradaki İngilizce dizgeler ekrana
    /// çıkan metin değil, <c>ConversionArguments.Validate</c>'in döndürdüğü iletinin
    /// kimliğidir; ekrana çıkan karşılık <c>Locales</c> dosyalarından gelir.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ValidationKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Trim times must use HH:MM:SS format."] = "main.validation.trim-format",
            ["Start time cannot be negative."] = "main.validation.start-negative",
            ["End time must be greater than zero."] = "main.validation.end-zero",
            ["End time must be after start time."] = "main.validation.end-before-start",
            ["Start time must be before the end of the source."] = "main.validation.start-past-end",
            ["Resolution dimensions must be positive."] = "main.validation.size-positive",
            ["Resolution dimensions must be even for the selected pixel format."] = "main.validation.size-even",
            ["Frame rate must be greater than zero."] = "main.validation.fps-zero",
            ["Stream copy cannot change resolution or frame rate."] = "main.validation.copy-fixed",
            ["GIF requires video encoding and cannot use stream copy."] = "main.validation.gif-copy",
            ["The source has no audio stream to copy."] = "main.validation.no-audio-copy",
            ["The source has no audio stream to extract."] = "main.validation.no-audio-extract",
            ["The trim end must come after the trim start."] = "main.validation.trim-order"
        };

    private static readonly (Regex Pattern, string Key)[] ValidationPatternKeys =
    {
        (new Regex(@"^The (.+) container does not support the selected (.+) video encoder\.$", RegexOptions.Compiled),
            "main.validation.container-video-encoder"),
        (new Regex(@"^The (.+) container does not support the selected (.+) audio encoder\.$", RegexOptions.Compiled),
            "main.validation.container-audio-encoder"),
        (new Regex(@"^The (.+) container does not support copying the source (.*) video stream\.$", RegexOptions.Compiled),
            "main.validation.container-video-copy"),
        (new Regex(@"^The (.+) container does not support copying the source (.*) audio stream\.$", RegexOptions.Compiled),
            "main.validation.container-audio-copy")
    };

    internal const string TrimFormatError = "Trim times must use HH:MM:SS format.";

    /// <summary>
    /// Motorun iletisini yürürlükteki dile çevirir. Tanınmayan ileti olduğu gibi geçer:
    /// ffmpeg'in kendi satırı da bu yoldan geliyor ve uydurulmaz.
    /// </summary>
    internal static string Validation(string english)
    {
        if (ValidationKeys.TryGetValue(english, out var key)) return Display(Strings.Get(key));

        foreach (var (pattern, patternKey) in ValidationPatternKeys)
        {
            var match = pattern.Match(english);
            if (match.Success)
                return Display(Strings.Get(patternKey, match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim()));
        }

        return Title(english, false);
    }

    /// <summary>
    /// Oynatma paneli (T82) kendi metnini hâlâ İngilizce dizgeyle taşıyor. Buradaki eşleme
    /// geçici bir köprüdür: tanınan dizge <c>Locales</c>'teki karşılığına düşer, tanınmayan
    /// (ffmpeg'in kendi iletisi gibi) olduğu gibi geçer.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> PlaybackKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Original"] = "playback.badge.original",
            ["Processed"] = "playback.badge.processed",
            ["Approximate preview"] = "playback.approximate-preview",
            ["The comparison player could not start"] = "playback.player-failed",
            ["The preview sample could not be encoded"] = "playback.sample-failed",
            ["Comparison panel"] = "playback.panel.title",
            ["Load a file to see the two sides"] = "playback.panel.hint",
            ["The panel moved to the front"] = "playback.panel.moved",
            ["This part will be processed"] = "playback.panel.pending",
            ["The first frame is on its way"] = "playback.panel.first-frame"
        };

    internal static string Playback(string english, bool turkish)
        => PlaybackKeys.TryGetValue(english, out var key)
            ? Title(Strings.GetIn(turkish ? "tr" : Strings.FallbackLanguage, key), turkish)
            : Title(english, false);

    /// <summary>
    /// T40 — the encode cursor label on the playback timeline: "analiz 1/2 · deneme 2".
    /// The numbers arrive from the caller so no counting lives in the translation.
    /// </summary>
    internal static string EncodeMarker(bool turkish, int pass, int passCount, int attempt)
    {
        var text = turkish
            ? $"analiz {pass}/{passCount} · deneme {attempt}"
            : $"analysis {pass}/{passCount} · attempt {attempt}";
        return Title(text, turkish);
    }
}
