# fable Danışma — NVENC Kalite Kolları (19 Eylül 2026)

Ölçüm: `docs/olcumler/nvenc-kalite-kollari.md`. İki karar soruldu: `-highbitdepth 1`'in
10 bit çıktı uyumluluğu, ve lookahead'in `FfmpegArguments.cs:262`'deki anahtar kare
yerleşim gerekçesini bozup bozmadığı.

## Sorulan

> SORU 1 — `-highbitdepth 1`: 8 bit kaynaktan 10 bit çıktı üretiyor (HEVC Main10 / AV1 Main
> profili 10 bit). Kazanç gerçek ama çıktı kullanıcının telefonunda, TV'sinde, WhatsApp'ta,
> eski bir tarayıcıda açılmak zorunda. VidShrink'in işi "paylaşılabilir küçük dosya". 2026
> itibarıyla 10 bit HEVC ve 10 bit AV1 donanım/yazılım kod çözme yaygınlığı nedir, hangi
> cihaz sınıfları düşer? Bu kazanç uyumluluk riskine değer mi, yoksa kol yalnız kullanıcının
> açıkça seçtiği bir "kalite" kipinde mi açılmalı?
>
> SORU 2 — lookahead: ürünün kodunda (`src/VidShrink.Core/FfmpegArguments.cs:262`) şu gerekçe
> yazılı: "NVENC sahne kesiminde I-kare yalnız lookahead açıkken ekler (`ffmpeg -h encoder=hevc_nvenc`:
> '-no-scenecut ... When lookahead is enabled'); bu proje lookahead'i açmıyor, o yüzden donanımda
> tavan bütün yerleşim kuralıdır: gerçekleşen aralık tam tavana eşittir ve arama maliyeti tam
> tavanın söylediği kadardır. `HardwareKeyframeCeilingSeconds` bu yüzden arama bütçesinin
> kendisidir, içerikten türetilmiş bir sayı değildir." Lookahead açılınca NVENC sahne kesimlerinde
> fazladan I-kare ekleyeceği için gerçekleşen anahtar kare aralığı tavandan **küçük** olur. Bu,
> arama bütçesi garantisini bozar mı yoksa güçlendirir mi? Bir de: fazladan I-kare bayt ekler,
> hedef boyut hesabı `-b:v` ile sürüldüğü için kodlayıcı kaliteden kısarak dengeleyebilir. Kolu
> almalı mıyız, alırsak `FfmpegArguments.cs:262`'deki gerekçe nasıl yeniden yazılmalı?

## Gelen yanıt (aynen)

> ## SORU 1 — `-highbitdepth 1`
>
> **Görüşüm: taban ürüne girmesin; yalnız açık seçilen "kalite" kipinde açılsın.**
>
> Uyumluluk tablosu (2026 itibarıyla, bildiğim kadarıyla):
>
> - **HEVC Main10 çözme:** Apple A9+/M-serisi, Snapdragon 6xx+ (2016 sonrası), Intel Skylake+, NVIDIA Maxwell GM206+, AMD Polaris+, akıllı TV'ler 2016+. Kapsam geniş; düşen sınıf 2014–2015 telefonlar, eski Chromecast/Fire TK ve Main-only medya oynatıcılar.
> - **AV1 10 bit çözme:** Donanımda Intel Tiger Lake+, NVIDIA Ampere+, AMD RDNA2+, Snapdragon 8 Gen 2+, MediaTek Dimensity 1000+, Apple A17 Pro / M3+. Yazılımda dav1d her yerde 10 bit çözer ama 1080p60'ta orta segment telefonda pil yakar. Buradaki düşüş HEVC'ten büyük.
> - **Asıl kırılma çözme değil dağıtım kanalı:** WhatsApp, Telegram, Instagram yüklenen videoyu kendi sunucusunda yeniden kodluyor; 10 bit girdiyi kabul edip 8 bit'e çeviriyorlar, kazanç kanalda buharlaşıyor. Tarayıcılarda Safari ve Chrome HEVC Main10'u donanım varsa oynatıyor, Firefox HEVC'i hiç oynatmıyor (bit derinliğinden bağımsız). Eski Windows 10 kurulumlarında "HEVC Video Extensions" olmadan ne 8 ne 10 bit açılıyor — yani HEVC'in kendi riski zaten 10 bit'inkinden büyük.
>
> Karar gerekçesi: kazanç ölçülebilir ama küçük (VMAF +0,4, XPSNR +0,5 dB); kayıp ise "hiç açılmıyor" biçiminde, geri dönüşsüz. "Paylaşılabilir küçük dosya" vaadi asimetrik riski kaldırmaz. Kalite kipinde açılırsa etiketi "10 bit çıktı — eski cihazlarda açılmayabilir" şeklinde bilgi ver; av1_nvenc'te varsayılan olarak hiç açmazdım, hevc_nvenc'te kalite kipinde açardım.
>
> ## SORU 2 — lookahead
>
> **Bütçe garantisi bozulmaz, güçlenir; ama gerekçe metni yanlışlanır, yeniden yazılmalı.**
>
> Mekanik: `-g 120` üst sınır olarak kalır, lookahead sahne kesimine I-kare **ekler**, aralığı asla uzatmaz. Arama maliyeti en kötü durumda değişmez, ortalama durumda küçülür. Yani `HardwareKeyframeCeilingSeconds` "gerçekleşen aralık = tavan" olmaktan çıkar, "gerçekleşen aralık ≤ tavan" olur — bu tam da yazılım kolundaki (`libx264` scenecut) semantiğe eşdeğerdir. Tavan hâlâ arama bütçesidir; artık "kendisi" değil "üst sınırı"dır.
>
> Bayt endişesi: sahne kesimindeki P/B kare zaten I'ye yakın pahalıdır (referans yok), oraya I koymak neredeyse bedavadır; ölçümün "çoğu zaman daha az baytla kazandı" okuması bununla tutarlı. `-b:v`+`-maxrate` sürücüde kodlayıcı dengeler, ama dengelediği şey sizin ölçtüğünüz VMAF'ta zaten görünüyor ve kazanç var. Ek risk: av1/parlak'taki kayıp tek hücre; "gürültü mü, sistematik mi" bilmeden reddetmezdim ama aynı nedenle kolu `-lookahead_level 3` yerine `1`'de de ölçmeden kesin almazdım (level 3 en pahalı; hız kaybı raporda yok, onu da yazın).
>
> **Kolu alın**, şu koşulla: ölçüm tablosuna kodlama süresi ve gerçekleşen I-kare sayısı eklensin; `HardwareKeyframeCeilingSeconds` testinde "aralık == tavan" eşitliği varsa "≤ tavan" olarak gevşetilsin.
>
> `FfmpegArguments.cs:261-266` için önerdiğim yeni gerekçe (İngilizce, kod dili):
>
> > Hardware is a different mechanism and gets its own ceiling. NVENC inserts an I-frame at a scene cut only when lookahead is on (ffmpeg -h encoder=hevc_nvenc: "-no-scenecut ... When lookahead is enabled"). This project turns lookahead on (-rc-lookahead 20 -lookahead_level 3, measured in docs/olcumler/&lt;dosya&gt;: mean VMAF up in 5 of 6 cells at equal or smaller size), so on hardware the ceiling is an upper bound, not the placement rule: scene cuts may shorten the realized interval, nothing may lengthen it. The seek cost is therefore at most what the ceiling says. HardwareKeyframeCeilingSeconds is still the seek budget, not a content-derived number; content can only spend less of it.

## Alınan karar

`-highbitdepth 1` **taban ürüne girmiyor**. Kol reddedilmedi, ertelendi: açık bir kalite
kipi olmadan bu bayrak açılmaz.

Lookahead kolu **alınıyor**, fable'ın üç koşuluyla: (1) ölçüm tablosuna kodlama süresi ve
gerçekleşen I-kare sayısı eklenecek, (2) `-lookahead_level 1` ile 3 karşılaştırılacak,
(3) `HardwareKeyframeCeilingSeconds`'ın "eşit" pimi "≤" olarak gevşetilecek.
