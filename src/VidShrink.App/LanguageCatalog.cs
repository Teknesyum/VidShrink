using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

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
            ["aac"] = "AAC",
            ["pcm"] = "PCM",
            ["opus"] = "Opus",
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
            ["fps"] = "FPS",
            ["kbps"] = "kbps",
            ["av1"] = "AV1",
            ["vp9"] = "VP9",
            ["whatsapp"] = "WhatsApp",
            ["vidshrink"] = "VidShrink",
            ["teknesyum"] = "Teknesyum",
            ["windows"] = "Windows",
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

    private static readonly IReadOnlyDictionary<string, string> EnglishSource = new Dictionary<string, string>
    {
        ["Minimize"] = "Simge durumuna küçült",
        ["Maximize"] = "Büyüt",
        ["Close"] = "Kapat",
        ["Target size media compression"] = "Boyut hedefli medya sıkıştırma",
        ["Media converter"] = "Medya dönüştürücü",
        ["Shrink"] = "Küçült",
        ["Convert"] = "Dönüştür",
        ["About"] = "Hakkında",
        ["Advanced"] = "Gelişmiş",
        ["Source"] = "Kaynak",
        ["Drop any media file here, or browse."] = "Herhangi bir medya dosyasını buraya bırakın veya gözatın.",
        ["Browse"] = "Gözat",
        ["Duration"] = "Süre",
        ["Size"] = "Boyut",
        ["Resolution"] = "Çözünürlük",
        ["Video codec"] = "Video kodeği",
        ["Audio"] = "Ses",
        ["Bitrate"] = "Bit hızı",
        ["Target"] = "Hedef",
        ["Estimated output"] = "Tahmini çıktı",
        ["Estimated time"] = "Tahmini süre",
        ["• VidShrink times the sample encodes it already ran on this clip, so the speed behind this figure comes from this machine and this file, not from a preset table.\n• Two-pass runs get a wide range because the first pass only analyses the picture and costs less than the second, and how much less is not measured.\n• When the plan moves to settings the samples were not encoded with, the time is left blank instead of guessed."] = "• VidShrink bu klip için koşturduğu örnek kodlamaların süresini ölçer; bu sayının arkasındaki hız ön ayar tablosundan değil, bu makineden ve bu dosyadan gelir.\n• İki geçişli işlerde aralık geniştir, çünkü birinci geçiş yalnızca görüntüyü çözümler ve ikinciden ucuza mal olur, ne kadar ucuz olduğu ise ölçülmez.\n• Plan, örneklerin kodlanmadığı ayarlara geçtiğinde süre tahmin edilmez, boş bırakılır.",
        ["Intent"] = "Amaç",
        ["Compression algorithm"] = "Sıkıştırma algoritması",
        ["Automatic"] = "Otomatik",
        ["• The target is a hard ceiling: VidShrink never hands you a file larger than this.\n• WhatsApp accepts files up to 2 GB, but it re-compresses any video you send in chat, and its own encoder is far weaker than this one.\n• Staying at or below 16 MB keeps VidShrink's quality instead of WhatsApp's.\n• 25 MB fits Discord and most e-mail, 8 MB fits older forums and chat apps."] = "• Hedef katı bir tavandır: VidShrink size bundan büyük bir dosya vermez.\n• WhatsApp 2 GB'a kadar dosya kabul eder, ancak sohbette gönderdiğiniz her videoyu yeniden sıkıştırır ve kendi kodlayıcısı buradakinden çok daha zayıftır.\n• 16 MB'ın altında kalmak WhatsApp'ın değil VidShrink'in kalitesini korur.\n• 25 MB Discord'a ve çoğu e-postaya, 8 MB eski forumlara ve sohbet uygulamalarına uyar.",
        ["• WhatsApp re-encodes in-chat video with its own low-bitrate encoder.\n• Under 16 MB it usually passes your file through with far less damage, so what the other side sees is VidShrink's quality, not WhatsApp's.\n• To bypass re-encoding entirely, send the result as a document instead of as a video."] = "• WhatsApp sohbetteki videoyu kendi düşük bit hızlı kodlayıcısıyla yeniden kodlar.\n• 16 MB'ın altında dosyanızı genellikle çok daha az zarar vererek geçirir; karşı tarafın gördüğü WhatsApp'ın değil VidShrink'in kalitesi olur.\n• Yeniden kodlamayı tamamen atlamak için sonucu video olarak değil belge olarak gönderin.",
        ["8 MB fits Discord without Nitro, older forums, and strict e-mail gateways."] = "8 MB, Nitro'suz Discord'a, eski forumlara ve katı e-posta ağ geçitlerine uyar.",
        ["25 MB fits Gmail attachments, Discord Nitro Basic, and most ticket systems."] = "25 MB, Gmail ekine, Discord Nitro Basic'e ve çoğu talep sistemine uyar.",
        ["100 MB suits archiving and uploads where quality matters more than transfer time."] = "100 MB, kalitenin aktarım süresinden önemli olduğu arşiv ve yüklemelere uygundur.",
        ["• Half of the source size.\n• A mild request, so the engine usually keeps the original resolution and frame rate."] = "• Kaynağın yarısı.\n• Yumuşak bir istek olduğu için motor genellikle özgün çözünürlüğü ve kare hızını korur.",
        ["• Intent sets how early the engine stops spending bits.\n• Archive stops only at visually lossless quality, so it can leave a lot of the target unused.\n• Sharing stops at the point where a normal viewer stops noticing — the right choice for WhatsApp.\n• Social media stops earlier still, because the platform will re-encode the file anyway."] = "• Amaç, motorun bit harcamayı ne kadar erken keseceğidir.\n• Arşiv yalnızca gözle kayıpsız kalitede durur, bu yüzden hedefin büyük kısmını kullanmadan bırakabilir.\n• Paylaşım, normal bir izleyicinin farkı sezmeyi bıraktığı noktada durur — WhatsApp için doğru seçim budur.\n• Sosyal medya daha da erken durur, çünkü platform dosyayı zaten yeniden kodlayacaktır.",
        ["• H.264 is universal: every phone made in the last fifteen years plays it, and WhatsApp never re-encodes it.\n• H.265 needs roughly a third fewer bits for the same picture, so it wins badly compressed targets — but some older Android phones and some web players refuse it.\n• Automatic picks H.264 for mild targets and H.265 once the target is tight enough that the quality gain outweighs the compatibility risk.\n• Speed is not chosen here any more: turn on fast shrink (GPU) below to let the graphics card encode."] = "• H.264 evrenseldir: son on beş yılda üretilmiş her telefon oynatır ve WhatsApp onu yeniden kodlamaz.\n• H.265 aynı görüntü için kabaca üçte bir daha az bit ister, bu yüzden sıkışık hedeflerde kazanır — ancak bazı eski Android telefonlar ve bazı web oynatıcılar kabul etmez.\n• Otomatik, yumuşak hedeflerde H.264'ü, kalite kazancı uyumluluk riskini aştığında H.265'i seçer.\n• Hız artık buradan seçilmiyor: kodlamayı ekran kartının yapmasını istiyorsanız aşağıdaki hızlı düşür (GPU) seçeneğini açın.",
        ["• When the target is tight, fewer pixels encoded well beat many pixels encoded badly — blur is far less objectionable than blocking.\n• The engine measures how much this particular clip actually saves by scaling down, then picks the largest resolution the budget can still hold cleanly.\n• On a phone screen a well-encoded 720p is indistinguishable from 1080p; leaving this on is almost always the better trade."] = "• Hedef sıkışıkken iyi kodlanmış az piksel, kötü kodlanmış çok pikseli yener — bulanıklık, bloklaşmadan çok daha az rahatsız eder.\n• Motor bu klibin çözünürlük düşürmekle gerçekte ne kadar kazandığını ölçer, sonra bütçenin temiz taşıyabileceği en büyük çözünürlüğü seçer.\n• Telefon ekranında iyi kodlanmış 720p, 1080p'den ayırt edilemez; bunu açık bırakmak neredeyse her zaman daha iyi bir takastır.",
        ["• Halving the frame rate frees bits for the frames left.\n• The engine only does this on hard targets and never drops below a level where motion starts to stutter.\n• Every phone plays reduced frame rates without trouble; the cost is smoothness, not compatibility."] = "• Kare hızını yarıya indirmek kalan karelere bit kazandırır.\n• Motor bunu yalnızca zor hedeflerde yapar ve hareketin takılmaya başladığı seviyenin altına asla inmez.\n• Her telefon düşük kare hızını sorunsuz oynatır; bedeli akıcılıktır, uyumluluk değil.",
        ["• Before planning, VidShrink encodes short samples of this clip at two resolutions and measures how many bits it needs — it does not guess from the source bitrate.\n• That is why the estimate is a measured number with a narrow range rather than a rule of thumb.\n• Two-pass runs land within a few percent; quality-capped runs come in under the target on purpose, because spending the rest would buy nothing you could see."] = "• VidShrink plan yapmadan önce tam olarak bu klipten kısa örnekleri iki çözünürlükte kodlar ve gerçekte kaç bit gerektiğini ölçer — kaynak bit hızından tahmin yürütmez.\n• Tahminin bir kestirme kural değil, dar aralıklı ölçülmüş bir sayı olmasının sebebi budur.\n• İki geçişli işler yüzde birkaç içinde iner; kalite tavanlı işler ise bilerek hedefin altında kalır, çünkü kalanı harcamak gözle görülür bir şey satın almaz.",
        ["Half"] = "Yarısı",
        ["Archive"] = "Arşiv",
        ["Sharing"] = "Paylaşım",
        ["Social media"] = "Sosyal medya",
        ["H.264 (compatible)"] = "H.264 (uyumlu)",
        ["H.265 (smallest)"] = "H.265 (en küçük)",
        ["May lower resolution"] = "Çözünürlük düşürülebilir",
        ["May lower frame rate"] = "Kare hızı düşürülebilir",
        ["Fast shrink (GPU)"] = "Hızlı düşür (GPU)",
        ["• Graphics cards encode many times faster than the CPU.\n• VidShrink picks the best encoder your card offers; on a modern card the AV1 encoder reaches nearly the software encoder's quality at about seven times the speed.\n• On older cards the speed still arrives, but it costs some quality per megabyte."] = "• Ekran kartı kodlamayı işlemciye göre kat kat hızlandırır.\n• VidShrink kartınızın sunduğu en iyi kodlayıcıyı seçer; modern kartlarda AV1 kodlayıcısı yazılım kodlayıcısıyla neredeyse aynı kaliteyi yaklaşık yedi kat hızlı verir.\n• Daha eski kartlarda hız yine gelir, ama bedeli megabayt başına bir miktar kalitedir.",
        ["• No usable hardware encoder was found on this computer, so fast shrink is unavailable.\n• The graphics card would normally encode many times faster than the CPU."] = "• Bu bilgisayarda kullanılabilir bir donanım kodlayıcı bulunamadı, bu yüzden hızlı düşürme kullanılamıyor.\n• Ekran kartı normalde işlemciden kat kat hızlı kodlar.",
        ["What it will do"] = "Yapılacak işlem",
        ["Drop a media file here"] = "Bir medya dosyasını buraya bırakın",
        ["Any format ffmpeg can open"] = "Ffmpeg'in açabildiği her biçim",
        ["Release to load this file"] = "Yüklemek için bırakın",
        ["Only one file at a time"] = "Bir seferde tek dosya",
        ["Folders cannot be dropped"] = "Klasör bırakılamaz",
        ["Load a file and every decision the engine makes is listed here."] = "Bir dosya yükleyin; motorun verdiği her karar burada madde madde listelenir.",
        ["Encoder"] = "Kodlayıcı",
        ["Mode"] = "Mod",
        ["Preset"] = "Ön ayar",
        ["Estimated size"] = "Tahmini boyut",
        ["FFmpeg command"] = "FFmpeg komutu",
        ["AI settings"] = "AI ayarları",
        ["• This step is optional.\n• Copy a prompt to any chat AI, then paste and validate its JSON answer."] = "• Bu adım isteğe bağlı.\n• İstemi herhangi bir sohbet AI'ına kopyalayın, ardından JSON yanıtını yapıştırıp doğrulayın.",
        ["Copy prompt"] = "İstemi kopyala",
        ["Apply pasted JSON"] = "Yapıştırılan JSON'u uygula",
        ["Back to automatic"] = "Otomatiğe dön",
        ["Output"] = "Çıktı",
        ["Stage"] = "Aşama",
        ["Remaining"] = "Kalan",
        ["Current output size"] = "Güncel çıktı boyutu",
        ["Show in folder"] = "Klasörde göster",
        ["Cancel"] = "İptal",
        ["Conversion"] = "Dönüştürme",
        ["Load a source in Shrink or browse here."] = "Küçült sekmesinde kaynak yükleyin veya buradan gözatın.",
        ["Browse source"] = "Kaynağa gözat",
        ["Container"] = "Kapsayıcı",
        ["Quality mode"] = "Kalite modu",
        ["Fixed bitrate"] = "Sabit bit hızı",
        ["CRF quality / bitrate K"] = "CRF kalitesi / bit hızı K",
        ["Custom"] = "Özel",
        ["Custom width x height"] = "Özel genişlik x yükseklik",
        ["Frame rate"] = "Kare hızı",
        ["Custom FPS"] = "Özel FPS",
        ["Copy"] = "Kopyala",
        ["Drop"] = "At",
        ["Audio bitrate K"] = "Ses bit hızı K",
        ["Start (HH:MM:SS)"] = "Başlangıç (SS:DD:SS)",
        ["End (HH:MM:SS)"] = "Bitiş (SS:DD:SS)",
        ["Progress"] = "İlerleme",
        ["• The container is the file type.\n• MP4 is the safest general choice and the only one WhatsApp reliably sends as a playable video — every phone opens it.\n• MKV supports many stream types but WhatsApp and most phone galleries treat it as a document.\n• WebM is for browsers and is not a phone format.\n• MOV suits Apple workflows and plays on iPhone, but Android support is uneven.\n• AVI is for older software only.\n• MP3, M4A, and WAV create audio-only files."] = "• Kapsayıcı dosya türüdür.\n• MP4 en güvenli genel seçimdir ve WhatsApp'ın oynatılabilir video olarak güvenilir biçimde gönderdiği tek biçimdir — her telefon açar.\n• MKV birçok akış türünü destekler ancak WhatsApp ve çoğu telefon galerisi onu belge sayar.\n• WebM tarayıcılar içindir, telefon biçimi değildir.\n• MOV Apple iş akışlarına uygundur ve iPhone'da oynar, Android desteği ise düzensizdir.\n• AVI yalnızca eski yazılımlar içindir.\n• MP3, M4A ve WAV yalnızca ses dosyası oluşturur.",
        ["• H.264 plays on nearly every device made and is what WhatsApp expects; pick it when the file must just work.\n• H.265 makes files a third smaller at the same picture, and every phone since 2016 decodes it in hardware.\n• But older handsets, some smart TVs, and several web players will not open it, and WhatsApp may re-encode it.\n• VP9 is a browser and WebM format; phones play it in apps but rarely in the gallery.\n• AV1 compresses best and is the slowest to encode, and only recent phones decode it.\n• Copy keeps the original video untouched — no quality loss, no waiting — whenever the container accepts it."] = "• H.264 üretilmiş hemen her cihazda oynar ve WhatsApp'ın beklediği biçimdir; dosyanın sorunsuz çalışması gerekiyorsa bunu seçin.\n• H.265 aynı görüntüyü üçte bir daha küçük dosyada verir ve 2016 sonrası her telefon donanımda çözer.\n• Ancak eski cihazlar, bazı akıllı televizyonlar ve birkaç web oynatıcı açmaz, WhatsApp da yeniden kodlayabilir.\n• VP9 bir tarayıcı ve WebM biçimidir; telefonlar uygulamalarda oynatır ama galeride nadiren.\n• AV1 en iyi sıkıştıranıdır ve en yavaş kodlanandır, yalnızca yeni telefonlar çözer.\n• Kopyala, seçilen kapsayıcı kabul ettiğinde özgün videoyu kalite kaybı ve bekleme olmadan olduğu gibi korur.",
        ["• CRF targets visual quality, so final size can vary.\n• Fixed bitrate targets a data rate, so size is predictable.\n• Use CRF for normal viewing quality and fixed bitrate when size or bandwidth matters more."] = "• CRF görüntü kalitesini hedefler, son boyut değişebilir.\n• Sabit bit hızı bir veri hızını hedefleyerek dosya boyutunu daha öngörülebilir yapar.\n• Normal izleme kalitesi için CRF, boyut veya bant genişliği daha önemliyse sabit bit hızı kullanın.",
        ["• In CRF mode, a lower number means higher quality and a larger file; 23 is a common H.264 starting point.\n• In fixed bitrate mode, enter kilobits per second: a higher value gives more quality and a larger file."] = "• CRF modunda düşük sayı daha yüksek kalite ve daha büyük dosya demektir; 23, H.264 için yaygın başlangıçtır.\n• Sabit bit hızı modunda saniyedeki kilobit değerini girin: yüksek değer daha fazla kalite ve daha büyük dosya verir.",
        ["• Source keeps the original dimensions.\n• A numbered option limits output height while preserving aspect ratio.\n• Lower resolution reduces file size and processing load.\n• Custom allows an exact width and height."] = "• Kaynak özgün boyutları korur.\n• Sayılı seçenekler en-boy oranını koruyarak çıktı yüksekliğini sınırlar.\n• Düşük çözünürlük boyutu ve işlem yükünü azaltır.\n• Özel, kesin genişlik ve yükseklik girmenizi sağlar.",
        ["• Used only when resolution is custom.\n• Enter width and height as 1280x720.\n• Even dimensions are safest for common video codecs."] = "• Yalnızca çözünürlük özel olduğunda kullanılır.\n• Genişlik ve yüksekliği 1280x720 biçiminde girin.\n• Yaygın video kodekleri için çift boyutlar en güvenlidir.",
        ["• Source preserves the original frame rate.\n• 60 is smoother but needs more data.\n• 30 suits most video.\n• 24 gives a cinema-like motion style.\n• Reducing frame rate can reduce file size."] = "• Kaynak özgün kare hızını korur.\n• 60 daha akıcıdır ancak daha fazla veri ister.\n• 30 çoğu videoya uygundur.\n• 24 sinema benzeri hareket verir.\n• Kare hızını düşürmek dosya boyutunu azaltabilir.",
        ["• Used only when frame rate is custom.\n• Enter the frames per second, such as 25 or 29.97.\n• Values above the source frame rate usually add no real motion detail."] = "• Yalnızca kare hızı özel olduğunda kullanılır.\n• 25 veya 29.97 gibi istenen kare hızını girin.\n• Kaynağın üzerindeki değerler genellikle gerçek hareket ayrıntısı eklemez.",
        ["• AAC is the safe pairing for MP4 and the only audio codec every phone and WhatsApp take without complaint.\n• Opus sounds better at low bitrates but belongs in WebM; inside MP4 it will not play on many phones.\n• MP3 suits older players.\n• PCM is uncompressed and very large.\n• Copy preserves the original audio without re-encoding when the container supports it.\n• Drop removes audio completely, which is the right call when you are squeezing a silent clip."] = "• AAC, MP4 için güvenli eşleşmedir ve her telefonun ve WhatsApp'ın sorunsuz işlediği tek ses kodeğidir.\n• Opus düşük bit hızlarında daha iyi duyulur ama yeri WebM'dir; MP4 içinde birçok telefonda oynamaz.\n• MP3 eski oynatıcılara uygundur.\n• PCM sıkıştırılmamış ve çok büyüktür.\n• Kopyala, kapsayıcı desteklediğinde özgün sesi yeniden kodlamadan korur.\n• At sesi tamamen kaldırır; sessiz bir klibi sıkıştırırken doğru seçim budur.",
        ["• Audio data rate in kilobits per second.\n• 128 is a compact general setting, 192 provides more detail, and 256 or 320 is useful for music.\n• This value is ignored for copy, drop, and raw PCM."] = "• Saniyedeki kilobit cinsinden ses veri hızıdır.\n• 128 kompakt genel ayardır, 192 daha fazla ayrıntı sağlar, 256 veya 320 müzik için kullanışlıdır.\n• Kopyala, at ve sıkıştırılmamış PCM seçeneklerinde bu değer kullanılmaz.",
        ["• Optional trim start time.\n• Leave empty to begin at the start of the source.\n• Use hours:minutes:seconds, for example 00:01:30."] = "• İsteğe bağlı kırpma başlangıcıdır.\n• Kaynağın başından başlamak için boş bırakın.\n• Saat:dakika:saniye biçimini kullanın; örneğin 00:01:30.",
        ["• Optional trim end time.\n• Leave empty to continue to the end of the source.\n• The end time must be later than the start time."] = "• İsteğe bağlı kırpma bitişidir.\n• Kaynağın sonuna kadar sürdürmek için boş bırakın.\n• Bitiş zamanı başlangıç zamanından sonra olmalıdır.",
        ["About VidShrink"] = "VidShrink hakkında",
        ["• VidShrink is an offline tool for target-size video compression and format conversion.\n• Give it a file and a size ceiling; it works out the settings that lose the least of what a person can actually see, and never returns a file larger than you asked for."] = "• VidShrink, hedef boyutlu video sıkıştırma ve format dönüştürme için çevrimdışı bir araçtır.\n• Ona bir dosya ve bir boyut tavanı verin; bir insanın gerçekten görebildiğinden en azını kaybettiren ayarları bulur ve istediğinizden büyük bir dosyayı asla geri vermez.",
        ["How the engine thinks"] = "Motor nasıl düşünüyor",
        ["• Most size-target compressors apply a fixed table: this many megabytes per minute becomes that resolution.\n• That table is wrong for every clip that is not average.\n• VidShrink measures instead. It encodes short samples of your actual file at two resolutions and reads how many bits it really costs, and how much of that cost disappears when the picture is scaled down.\n• Those two measurements make the plan specific to this clip rather than to video in general."] = "• Boyut hedefli sıkıştırıcıların çoğu sabit bir tablo uygular: dakikada şu kadar megabayt şu çözünürlüğe karşılık gelir.\n• Bu tablo, ortalama olmayan her klip için yanlıştır.\n• VidShrink bunun yerine ölçer. Gerçek dosyanızdan kısa örnekleri iki çözünürlükte kodlar ve gerçekte kaç bit tuttuğunu, görüntü küçültüldüğünde bu maliyetin ne kadarının kaybolduğunu okur.\n• Bu iki ölçüm planı genel olarak videoya değil, tam olarak bu klibe özel kılar.",
        ["Measured, not assumed"] = "Varsayılan değil, ölçülen",
        ["complexity      measured from your file, not its bitrate\ndetail falloff  measured; decides if scaling down is worth it\nresolution      continuous search, not a fixed ladder\nframe rate      searched alongside resolution, not after it\ncodec           chosen from how hard the target actually is\naudio           share of budget shrinks as the target tightens\nestimate        a measured number with a range, shown up front"] = "karmaşıklık     bit hızından değil, dosyanızdan ölçülür\nayrıntı düşüşü  ölçülür; küçültmenin değip değmediğine karar verir\nçözünürlük      sabit basamak değil, sürekli arama\nkare hızı       çözünürlükten sonra değil, onunla birlikte aranır\nkodek           hedefin gerçekte ne kadar zor olduğuna göre seçilir\nses             hedef sıkılaştıkça bütçe payı küçülür\ntahmin          aralığıyla birlikte, baştan gösterilen ölçülmüş sayı",
        ["Where the loss goes"] = "Kayıp nereye gidiyor",
        ["• Below a certain bit budget something has to give.\n• The engine spends the loss where the eye is least sensitive: softness before blocking, fewer pixels before broken pixels, mono audio before a starved picture.\n• It also knows when to stop — once quality reaches the point where more bits buy nothing you could see, it hands back a smaller file instead of padding it to the target."] = "• Belirli bir bit bütçesinin altında bir şeyden ödün vermek gerekir.\n• Motor kaybı gözün en az duyarlı olduğu yere yığar: bloklaşmadan önce yumuşaklık, bozuk pikselden önce daha az piksel, aç kalmış görüntüden önce tek kanallı ses.\n• Ne zaman duracağını da bilir — kalite, fazladan bitin gözle görülür hiçbir şey satın almadığı noktaya geldiğinde dosyayı hedefe kadar şişirmek yerine daha küçüğünü geri verir.",
        ["Scenario awareness"] = "Senaryo farkındalığı",
        ["• A 1.2x reduction and a 600x reduction are not the same problem, so they do not get the same treatment.\n• Light targets keep everything and simply spend the budget.\n• Balanced targets allow scaling.\n• Aggressive and extreme targets unlock frame-rate reduction, move to H.265, and cut the audio share — and the app tells you what it changed and why, in plain language, before you press start."] = "• 1,2 katlık bir küçültme ile 600 katlık bir küçültme aynı sorun değildir, bu yüzden aynı muameleyi görmezler.\n• Hafif hedefler her şeyi korur ve yalnızca bütçeyi harcar.\n• Dengeli hedefler ölçek düşürmeye izin verir.\n• Agresif ve uç hedefler kare hızı düşürmeyi açar, H.265'e geçer ve ses payını kısar — üstelik uygulama neyi neden değiştirdiğini, siz başlata basmadan önce, sade bir dille söyler.",
        ["AI mode"] = "AI modu",
        ["• AI mode only creates a prompt and validates pasted JSON.\n• No AI is embedded, so VidShrink works offline, asks for no API key, and falls back to the complete automatic engine when an answer is malformed or incompatible."] = "• AI modu yalnızca istem oluşturur ve yapıştırılan JSON'u doğrular.\n• Gömülü AI olmadığı için VidShrink çevrimdışı çalışır, API anahtarı istemez ve yanıt bozuk ya da uyumsuz olduğunda eksiksiz otomatik motora döner.",
        ["Codecs"] = "Kodekler",
        ["• H.264 offers the broadest compatibility.\n• H.265 compresses more efficiently.\n• VP9 is a strong WebM choice.\n• AV1 maximizes modern compression at higher CPU cost.\n• Stream copy is fastest and lossless when the destination supports the source streams."] = "• H.264 en geniş uyumluluğu sunar.\n• H.265 daha verimli sıkıştırır.\n• VP9, WebM için güçlü bir seçimdir.\n• AV1 daha yüksek işlemci maliyetiyle modern sıkıştırmayı en üst düzeye çıkarır.\n• Hedef kaynak akışlarını desteklediğinde akış kopyalama en hızlı ve kayıpsız seçenektir.",
        ["System status"] = "Sistem durumu",
        ["Idle"] = "Boşta",
        ["Preserve HDR"] = "HDR'yi koru",
        ["Convert to SDR"] = "SDR'ye çevir",
        ["• Preserving HDR keeps the source's wider color and brightness range, but the file is larger 10-bit and only recent devices and apps play it correctly.\n• Converting to SDR maps the picture to the standard range — smaller, safe on WhatsApp and any phone."] = "• HDR'yi korumak kaynağın daha geniş renk ve parlaklık aralığını saklar, ancak dosya daha büyük 10-bit olur ve yalnızca yeni cihazlar ve uygulamalar doğru oynatır.\n• SDR'ye çevirmek görüntüyü standart aralığa tone-map eder — daha küçük ve WhatsApp ile her telefonda güvenli.",
        ["Fill policy"] = "Doldurma politikası",
        ["Fill target"] = "Hedefi doldur",
        ["Stay at quality ceiling"] = "Kalite tavanında dur",
        ["• Fill target lands close to the target size and squeezes out the best quality the budget allows.\n• Stay at quality ceiling stops when quality stops improving: no padding, but the file can come out smaller."] = "• Hedefi doldur, hedef boyuta yakın durur ve bütçenin izin verdiği en iyi kaliteyi sıkar.\n• Kalite tavanında dur, kalite artmayı bıraktığında durur; dosyayı şişirmez ama belirgin biçimde küçük kalabilir.",
        ["Settings"] = "Ayarlar",
        ["Share target"] = "Paylaşım hedefi",
        ["• The share target is the service a finished file is uploaded to.\n• storage.to carries up to 25 GiB and lets VidShrink delete the file again, so a link can be closed early.\n• uguu.se carries up to 128 MiB and clears itself after 3 hours, but it hands out no delete token, so nobody can close the link early."] = "• Paylaşım hedefi, biten dosyanın yüklendiği servistir.\n• storage.to 25 GiB'a kadar taşır ve VidShrink'in dosyayı geri silmesine izin verir, bağlantı erken kapatılabilir.\n• uguu.se 128 MiB'a kadar taşır, 3 saat sonra kendi siler, ama silme jetonu vermez; bağlantı erken kapatılamaz.",
        ["Ceiling"] = "Tavan",
        ["Lifetime"] = "Ömür",
        ["Deletion"] = "Silme",
        ["Delete the shared file"] = "Paylaşılan dosyayı sil",
        ["Share the file"] = "Dosyayı paylaş",
        ["Cancel the upload"] = "Yüklemeyi iptal et",
        ["WhatsApp recommended"] = "WhatsApp için önerilen",
        ["Sharing maximum"] = "Paylaşım için en fazla",
        ["WhatsApp Web maximum"] = "WhatsApp Web için en fazla",
        ["• 128 MiB is the measured ceiling of uguu.se, the anonymous share target with the smallest limit.\n• A file at or under this size can be handed to either share target without being refused.\n• The other target, storage.to, carries far more, so this chip is the safe number for both."] = "• 128 MiB, en dar sınırı olan anonim paylaşım hedefi uguu.se'nin ölçülmüş tavanıdır.\n• Bu boyutta ya da altında bir dosya iki paylaşım hedefine de geri çevrilmeden verilebilir.\n• Öteki hedef storage.to çok daha fazlasını taşır, bu yüzden bu yonga ikisi için de güvenli sayıdır.",
        ["• On the phone: 16 MB in chat, 2 GB as a document.\n• WhatsApp Web takes 180 MB per file. The user reports this, WhatsApp does not publish it.\n• So a file of 180 MB or less goes through the web side as it is."] = "• Telefonda: sohbette 16 MB, belge olarak 2 GB.\n• Dosya başına 180 MB'ı web tarafı alır. Sayı kullanıcı bildirimi, WhatsApp yayımlamıyor.\n• 180 MB ve altı dosya bu yüzden web tarafından olduğu gibi geçer.",
        ["Updates"] = "Güncelleme",
        ["Update automatically"] = "Kendiliğinden güncelle",
        ["When this is off, VidShrink does not update itself: it only tells you that a new version exists and shows the command that installs it."] = "Bu kapalıyken VidShrink kendini güncellemez: yalnızca yeni bir sürüm olduğunu söyler ve kuran komutu gösterir.",
        ["VidShrink does not update itself on this system: it only tells you that a new version exists and shows the command that installs it."] = "VidShrink bu sistemde kendini güncellemez: yalnızca yeni bir sürüm olduğunu söyler ve kuran komutu gösterir.",
        ["A new version is available"] = "Yeni bir sürüm var",
        ["Updated to a new version"] = "Yeni sürüme geçildi",
        ["Over the target"] = "Hedefin üzerinde",
        ["Try again"] = "Tekrar dene",
        ["Leave it as is"] = "Bu haliyle bırak",
        ["Preview"] = "Önizleme",
        ["Comparison panel"] = "Karşılaştırma paneli",
        ["Load a file to see the two sides"] = "İki tarafı görmek için bir dosya yükleyin",
        ["The first frame is on its way"] = "İlk kare yolda",
        ["The panel moved to the front"] = "Panel üste alındı",
        ["Original"] = "Orijinal",
        ["Processed"] = "İşlenmiş",
        ["This part will be processed"] = "Bu kısım işleme sokulacak",
        ["There is no processed file yet, so this side stays empty"] = "Henüz işlenmiş bir dosya yok, bu yüzden bu taraf boş kalıyor",
        ["The comparison player could not start"] = "Karşılaştırma oynatıcısı başlayamadı",
        ["Approximate preview"] = "Yaklaşık önizleme",
        ["The preview sample could not be encoded"] = "Önizleme örneği kodlanamadı"
    };

    /// <summary>
    /// Both sides are stored already capitalised, so a lookup made from on-screen text finds its
    /// partner and the answer needs no further work.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> EnglishToTurkish =
        EnglishSource.ToDictionary(item => Title(item.Key, false), item => Title(item.Value, true));

    internal static readonly IReadOnlyDictionary<string, string> TurkishToEnglish =
        EnglishToTurkish.ToDictionary(item => item.Value, item => item.Key);

    /// <summary>
    /// Puts an English source string into the wanted language. A miss is not a failure: the
    /// English text stays and only its casing is fixed, exactly like the window's own walk.
    /// Controls that write their text from code call this instead of carrying a second copy
    /// of the Turkish wording.
    /// </summary>
    internal static string Localize(string english, bool turkish)
    {
        var titled = Title(english, false);
        return turkish && EnglishToTurkish.TryGetValue(titled, out var found) ? found : titled;
    }

    private static readonly IReadOnlyDictionary<string, string> ValidationTurkish = new Dictionary<string, string>
    {
        ["Trim times must use HH:MM:SS format."] = "Kırpma zamanları SS:DD:SS biçiminde yazılmalı.",
        ["Start time cannot be negative."] = "Başlangıç zamanı negatif olamaz.",
        ["End time must be greater than zero."] = "Bitiş zamanı sıfırdan büyük olmalı.",
        ["End time must be after start time."] = "Bitiş zamanı başlangıç zamanından sonra olmalı.",
        ["Start time must be before the end of the source."] = "Başlangıç zamanı kaynağın bitişinden önce olmalı.",
        ["Resolution dimensions must be positive."] = "Çözünürlük değerleri sıfırdan büyük olmalı.",
        ["Resolution dimensions must be even for the selected pixel format."] = "Seçili piksel biçimi için çözünürlük değerleri çift sayı olmalı.",
        ["Frame rate must be greater than zero."] = "Kare hızı sıfırdan büyük olmalı.",
        ["Stream copy cannot change resolution or frame rate."] = "Akış kopyalama çözünürlüğü veya kare hızını değiştiremez.",
        ["GIF requires video encoding and cannot use stream copy."] = "GIF video kodlaması gerektirir, akış kopyalama kullanamaz.",
        ["The source has no audio stream to copy."] = "Kaynakta kopyalanacak bir ses akışı yok.",
        ["The source has no audio stream to extract."] = "Kaynakta çıkarılacak bir ses akışı yok.",
        ["The trim end must come after the trim start."] = "Kırpma bitişi kırpma başlangıcından sonra olmalı."
    };

    private static readonly (Regex Pattern, string Turkish)[] ValidationPatterns =
    {
        (new Regex(@"^The (.+) container does not support the selected (.+) video encoder\.$", RegexOptions.Compiled),
            "{0} kapsayıcısı seçilen {1} video kodlayıcısını desteklemiyor."),
        (new Regex(@"^The (.+) container does not support the selected (.+) audio encoder\.$", RegexOptions.Compiled),
            "{0} kapsayıcısı seçilen {1} ses kodlayıcısını desteklemiyor."),
        (new Regex(@"^The (.+) container does not support copying the source (.*) video stream\.$", RegexOptions.Compiled),
            "{0} kapsayıcısı kaynaktaki {1} video akışını kopyalamayı desteklemiyor."),
        (new Regex(@"^The (.+) container does not support copying the source (.*) audio stream\.$", RegexOptions.Compiled),
            "{0} kapsayıcısı kaynaktaki {1} ses akışını kopyalamayı desteklemiyor.")
    };

    internal const string TrimFormatError = "Trim times must use HH:MM:SS format.";

    internal static string Validation(string english, bool turkish)
    {
        if (!turkish) return Title(english, false);
        if (ValidationTurkish.TryGetValue(english, out var known)) return Title(known, true);
        foreach (var (pattern, template) in ValidationPatterns)
        {
            var match = pattern.Match(english);
            if (match.Success) return Title(string.Format(template, match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim()), true);
        }
        return Title(english, false);
    }

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

    /// <summary>
    /// The question shown when an attempt lands over the target and another attempt is still allowed.
    /// Every number arrives already formatted by the caller, so no formatting lives in the translation.
    /// </summary>
    internal static (string Outcome, string Meaning) RetryQuestion(
        bool turkish,
        string attempt,
        string maxAttempts,
        string actualMb,
        string targetMb,
        string overMb,
        string overPercent,
        string attemptDuration,
        bool hasUnderBandFallback,
        string fallbackMb)
    {
        if (turkish)
        {
            var outcome = $"{attempt}/{maxAttempts}. deneme {actualMb} MB çıktı — {targetMb} MB hedefinin {overMb} MB (%{overPercent}) üzerinde. Bu deneme {attemptDuration} sürdü; yeni bir deneme kabaca aynı süreyi alır.";
            var meaning = hasUnderBandFallback
                ? $"“Bu haliyle bırak” taşan dosyayı teslim etmek değildir; koşuyu bitirir. Hedeften büyük dosya asla verilmez: hedefin altında kalan son sonuç ({fallbackMb} MB) teslim edilir."
                : "“Bu haliyle bırak” taşan dosyayı teslim etmek değildir; koşuyu bitirir. Hedeften büyük dosya asla verilmez ve hedefin altında kalan bir sonuç henüz yok, bu yüzden dosya yazılmaz.";
            return (Title(outcome, true), Title(meaning, true));
        }

        var outcomeEn = $"Attempt {attempt} of {maxAttempts} came out at {actualMb} MB — {overMb} MB ({overPercent}%) over the {targetMb} MB target. It took {attemptDuration}; another attempt would take about the same.";
        var meaningEn = hasUnderBandFallback
            ? $"“Leave it as is” does not hand you the oversized file; it ends the run. A file larger than the target is never handed back: the last result that stayed under the target ({fallbackMb} MB) is delivered instead."
            : "“Leave it as is” does not hand you the oversized file; it ends the run. A file larger than the target is never handed back, and no result has stayed under the target yet, so no file will be written.";
        return (Title(outcomeEn, false), Title(meaningEn, false));
    }
}
