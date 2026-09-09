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
    /// Satır başında değilse küçük kalan sözcükler, dile göre. Bu bir çeviri değil, dilin
    /// kendi yazım kuralıdır — sözlükte değil burada durur. Listesi olmayan bir dil bütün
    /// sözcüklerini büyütür; İngilizce böyle, çünkü başlıkları her sözcüğü büyük yazılıyor.
    ///
    /// <para>Dil kodu ülkesiyle gelebilir (<c>tr-TR</c>); eşleşme kodun ilk parçasına bakar,
    /// böylece yeni bir dil eklendiğinde ülke kırılımı ayrıca yazılmak zorunda değil.</para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, HashSet<string>> SmallWords =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["tr"] = new(StringComparer.Ordinal) { "ve", "veya", "ile", "ki", "da", "de" }
        };

    private static HashSet<string>? SmallWordsOf(string language)
    {
        var cut = language.IndexOf('-');
        var bare = cut < 0 ? language : language[..cut];
        return SmallWords.TryGetValue(bare, out var words) ? words : null;
    }

    /// <summary>
    /// Names and abbreviations that are written a fixed way. A word typed in lower case here is
    /// restored to its own spelling instead of getting a single capital.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> Names =
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
    /// Baglaclar, ilgecler, tanimliklar, adillar, yardimci fiiller ve soru sozcukleri.
    /// Bunlardan biri gecen metin cumledir; baslik kurali ona uygulanmaz. Liste bilerek
    /// dar: icerik sozcugu (ad, sifat, asil fiil) buraya girmez, cunku o zaman her
    /// baslik cumle sayilirdi.
    /// </summary>
    private static readonly HashSet<string> FunctionWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "but", "nor", "of", "to", "in", "into", "on",
            "at", "by", "for", "from", "with", "without", "than", "as", "if", "so",
            "it", "its", "this", "that", "these", "those", "they", "them", "you", "your",
            "is", "are", "was", "were", "be", "will", "would", "can", "may", "does", "do",
            "what", "why", "how", "when", "where", "which", "who",

            "ve", "veya", "ile", "ki", "da", "de", "ya", "ama", "ancak", "cunku", "çünkü",
            "icin", "için", "gibi", "kadar", "gore", "göre", "her", "bir", "bu", "su", "şu",
            "ne", "neden", "niye", "nasil", "nasıl", "hangi", "kim", "nerede", "hep", "daha",
        };

    /// <summary>
    /// Başlık kuralı yalnız başlıklara uygulanır. Cümle işareti (<c>.</c> <c>;</c> <c>!</c>
    /// <c>?</c>) taşıyan metin gövdedir: yalnız satır başındaki harf büyütülür, gerisi dil
    /// dosyasında yazıldığı gibi kalır. Başlık kolunda ise:
    /// Capitalises the first letter of every word. Words that already carry a capital anywhere
    /// (MP4, GPU, H.264, WhatsApp, FFmpeg, VidShrink) and words that do not start with a letter
    /// (8, 1280x720, 00:01:30) are handed back untouched, so nothing is invented and nothing is
    /// flattened. Applying it twice changes nothing.
    /// </summary>
    internal static bool ReadsAsProse(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is not ('.' or ';' or '!' or '?')) continue;
            if (index + 1 == text.Length || char.IsWhiteSpace(text[index + 1])) return true;
        }

        return CarriesFunctionWord(text);
    }

    /// <summary>
    /// Cumle isareti tek belirti degildi. "Load a file to see the two sides" nokta
    /// tasimadigi icin baslik kolundan geciyor ve ekranda
    /// "Load A File To See The Two Sides" oluyordu. Ayirt eden sey noktalama degil
    /// dilbilgisi: baglac, ilgec, tanimlik, adil ya da soru sozcugu tasiyan metin bir
    /// tamlama degil bir cumledir. Sinav <b>butun sozcuge</b> bakar, sozcugun icindeki
    /// harf obegine degil: <c>storage.to</c> tek bir sozcuktur ve listede yoktur, oysa
    /// icindeki <c>to</c> aransaydi govde sayilirdi. Listede sozcuk yoksa metin baslik
    /// kolunda kalir ("Video codec", "Current output size", "Fill policy").
    /// </summary>
    private static bool CarriesFunctionWord(string text)
    {
        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var start = 0;
            var end = token.Length;
            while (start < end && !char.IsLetter(token[start])) start++;
            while (end > start && !char.IsLetter(token[end - 1])) end--;
            if (end > start && FunctionWords.Contains(token[start..end])) return true;
        }

        return false;
    }

    /// <summary>
    /// Govde kolu: yalniz satir basi sozcugu buyutulur, geri kalan dil dosyasinda
    /// yazildigi gibi kalir — ad ve birim yazimlari (<see cref="KnownSpelling"/>) haric.
    ///
    /// <para>Satir basi sayaci harfe degil <b>harf ya da rakama</b> bakar. Yer tutucu
    /// (<c>{0}</c>) bir icerik sozcugudur: yerine kodlayici adi ya da sayi gelir. Yalniz
    /// harfe bakildiginda yer tutucu satir basini tuketmiyordu ve ondan sonraki sozcuk
    /// buyuyordu — "{0} Kodlayicisi bu FFmpeg derlemesinde yok". Noktalama (madde imi
    /// <c>&#8226;</c>, tirnak) satir basini tuketmez, cunku o gercekten sozcuk degil.</para>
    /// </summary>
    private static string Sentence(string text, CultureInfo culture)
    {
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

            builder.Append(lineStart ? CapitaliseWord(word, culture, null, true) : KnownSpelling(word) ?? word);
            if (word.Any(char.IsLetterOrDigit)) lineStart = false;
            index = end;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Bir başlığı hedef dilin kuralına göre büyütür. Dil kodu iki şeyi belirler: büyük harf
    /// kültürü ve satır başında değilken küçük kalan sözcükler. İkisi de <see cref="Strings"/>
    /// ile aynı çeviriden geldiği için yeni bir dil eklemek burada değişiklik istemez.
    /// </summary>
    internal static string Title(string text, string language)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (Brands.TryGetValue(text, out var brand)) return brand;
        var culture = Strings.CultureOf(language);
        var smallWords = SmallWordsOf(language);
        if (ReadsAsProse(text)) return Sentence(text, culture);
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
            builder.Append(CapitaliseWord(word, culture, smallWords, lineStart));
            lineStart = false;
            index = end;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Sozcugun <see cref="Verbatim"/> ya da <see cref="Names"/> listesinde bildirilmis
    /// yazimi. Yoksa <c>null</c> doner.
    ///
    /// <para>Bu gecit hem baslik hem govde kolunda gecerlidir: bir adin yazimi metnin
    /// neresinde durduguna bagli olamaz. Once yalniz <see cref="CapitaliseWord"/> icinde
    /// duruyordu, <see cref="Sentence"/> ise onu sadece satir basi sozcugu icin
    /// cagiriyordu; boylece "Any format ffmpeg can open" gibi govde cumlelerinde ortadaki
    /// <c>ffmpeg</c> dil dosyasindaki yazimiyla kaliyordu (T192 tur 3, K11).</para>
    /// </summary>
    private static string? KnownSpelling(string word)
    {
        var offset = 0;
        while (offset < word.Length && !char.IsLetter(word[offset]))
        {
            if (char.IsDigit(word[offset])) return null;
            offset++;
        }

        if (offset == word.Length) return null;

        var body = word[offset..];
        var bare = new string(body.TakeWhile(char.IsLetter).ToArray());
        var token = new string(body.TakeWhile(char.IsLetterOrDigit).ToArray());

        var identifier = new string(body.TakeWhile(letter => char.IsLetterOrDigit(letter) || letter == '_').ToArray());
        if (Verbatim.Contains(identifier) || Verbatim.Contains(token) || Verbatim.Contains(bare)) return word;
        if (Names.TryGetValue(token, out var known))
            return string.Concat(word.AsSpan(0, offset), known, body.AsSpan(token.Length));
        if (Names.TryGetValue(bare, out var name))
            return string.Concat(word.AsSpan(0, offset), name, body.AsSpan(bare.Length));

        return null;
    }

    private static string CapitaliseWord(
        string word, CultureInfo culture, HashSet<string>? smallWords, bool lineStart)
    {
        if (KnownSpelling(word) is { } spelled) return spelled;

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

        foreach (var letter in body)
            if (char.IsUpper(letter))
                return word;

        if (!lineStart && smallWords is not null && smallWords.Contains(bare)) return word;

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
    internal static string Display(string text) => Title(text, Strings.Language);

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
    /// Motorun iletisini yürürlükteki dile çevirir. Tanınmayan ileti açık bir
    /// "çevrilmemiş motor iletisi" etiketiyle gösterilir; sessizce İngilizceye düşmez.
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

        return Display(Strings.Get("main.validation.untranslated-engine", english));
    }

    /// <summary>
    /// Oynatma zaman çizgisindeki kodlama imleci. Sayılar çağırandan gelir; metin ve
    /// sözcük sırası dil alanındaki tek anahtardan okunur.
    /// </summary>
    internal static string EncodeMarker(string language, int pass, int passCount, int attempt)
        => Title(
            Strings.GetIn(language, "main.playback.encode-marker", pass, passCount, attempt),
            language);
}
