# Netleştirme: Karar ver: VidShrink oynatıcısı için (1) rakip özelliklerinin Standart / Gelişmi

- tarih: 2026-09-11
- girdi: 004-karar-ver-vidshrink-oynaticisi-icin-1-ra-girdi.md
- maliyet: 66414 token, 184 sn

## Dönen

# Karar: VidShrink Oynatıcısı

## 1. Sınıflandırma

Ölçüt: **Standart** = günlük oynatıcıda yokluğu hissedilir. **Gelişmiş** = ara sıra, ayar gerektirir, menü altında durur. **Alınmayacak** = VidShrink'in işiyle çelişir ya da bakım yükü faydayı aşar.

### Oynatma ve arama

| Özellik | Sınıf | Gerekçe |
|---|---|---|
| Hız (0,25–4×, perde korumalı) | Standart | Her oynatıcının ilk beklenen fazlası; bugün yok. |
| Kare kare (ileri/geri) | Standart | Sıkıştırma artefaktı incelemek için VidShrink'e özellikle gerekli. |
| Atlama adımları (1/10/60/300 sn) | Standart | Zaten var, klavyeye taşınır. |
| A-B tekrar | Standart | Segment karşılaştırmasıyla doğal eşleşir, motorda bedava. |
| Kaldığı yerden devam | Standart | Uzun dosyada yokluğu en çok hissedilen şey. |
| Bölüm işaretleri | Standart | Dosyada varsa göstermek maliyetsiz. |
| Yer imleri (N/B) | Standart | Kaldığı yerden devamla aynı depoyu kullanır. |
| Arama önizleme küçük resmi | Gelişmiş | Ayrı çözme hattı ister; mevcut anahtar kare önbelleği temel olur. |
| GOM AB atlama (reklam atlama) | Alınmayacak | Kullanım alanı yayın kaydı; VidShrink'in dosyalarında yok. |

### Görüntü ve pencere

| Özellik | Sınıf | Gerekçe |
|---|---|---|
| En-boy oranı | Standart | Yanlış oranlı dosya günlük vaka. |
| Zoom/pan | Standart | Var, korunur. |
| Döndür / aynala | Standart | Telefon videosu için sık gerek; motorda tek özellik. |
| Tam ekran (Enter, çift tık, orta tık) | Standart | Var, giriş yolları tamamlanır. |
| Her zaman üstte | Standart | Tek pencere bayrağı. |
| Kırpma | Gelişmiş | Dönüştür sekmesiyle örtüşür; oynatıcıda önizleme olarak kalır. |
| Parlaklık/kontrast/doygunluk/ton | Gelişmiş | Ayar gerektirir, günlük değil. |
| Keskinlik / deinterlace | Gelişmiş | Belirli kaynaklar için; varsayılan otomatik. |
| Mini mod | Gelişmiş | Pencere düzeni işi, motor değil. |
| 360° video, DVD menüsü | Alınmayacak | Ayrı işleme hattı; hedef kitle dışı. |

### Ses

| Özellik | Sınıf | Gerekçe |
|---|---|---|
| Ses seviyesi, sessiz | Standart | Bugün yokluğu oynatıcı olarak kullanımı engelliyor. |
| Ses parçası seçimi | Standart | Çok dilli dosya günlük vaka. |
| Ses gecikmesi | Standart | Kayma düzeltmesi sık ihtiyaç, tek kaydırıcı. |
| %100 üstü güçlendirme | Gelişmiş | Kırpma riski; menü altında, üst sınır 200. |
| Ekolayzer | Gelişmiş | Ayar paneli ister. |
| Normalleştirme | Gelişmiş | Filtre gecikmesi getirir, isteğe bağlı. |
| TTS | Alınmayacak | Erişilebilirlik işi işletim sistemine ait. |

### Altyazı

| Özellik | Sınıf | Gerekçe |
|---|---|---|
| Dış srt/ass/vtt, gömülü parça | Standart | Altyazısız oynatıcı günlük kullanılmaz. |
| Gecikme | Standart | Tek kaydırıcı. |
| Boyut / konum | Standart | Okunabilirlik. |
| Kodlama seçimi | Standart | Türkçe srt'lerde cp1254 hâlâ yaygın. |
| Renk / biçem | Gelişmiş | Ayar paneli ister. |
| Otomatik bulma/indirme | Alınmayacak | Ağ, hesap, hukuk; çekirdeğe yabancı. |

### Liste, dışa aktarma, bilgi, sistem

| Özellik | Sınıf | Gerekçe |
|---|---|---|
| Klasörde sonraki/önceki (PgUp/PgDn) | Standart | Çalma listesinin en ucuz ve en kullanılan hâli. |
| Sürükle-bırak (oynatıcıya) | Standart | Ana pencerede var, oynatıcıya bağlanır. |
| Karıştır / tekrar | Standart | Motorda iki özellik. |
| Ekran görüntüsü (Ctrl+E) | Standart | Sıkıştırma kalitesini paylaşmanın yolu. |
| Klip / GIF dışa aktarma | Gelişmiş | VidShrink'in ffmpeg çekirdeğine yönlendirilir; oynatıcı yalnız aralığı verir. |
| Bilgi paneli (codec/bitrate/çöz./fps) | Standart | Sıkıştırma aracında girdi bilgisi zorunlu. |
| Son açılanlar | Standart | Ucuz, her gün kullanılır. |
| URL açma | Gelişmiş | Yerel dosya birincil; motor destekler, menüde durur. |
| Dosya ilişkilendirme | Standart | "Oynatıcı olarak kullanamadım"ın kök nedeni. |
| Bitince bilgisayarı kapat | Alınmayacak | Sıkıştırma kuyruğuna ait bir özellik; oynatıcıda değil. |

Sayım: Standart 30, Gelişmiş 13, Alınmayacak 6.

## 2. Mimari temel

### Seçenekler

| Yol | Hız/altyazı/parça değişimi | A/V senkron | Paket yükü | Test edilebilirlik | Karar |
|---|---|---|---|---|---|
| İki boru + PlaybackClock (mevcut) | Her biri süreç yeniden başlatma (+105 ms, kayıp kare) | Yapısal olarak zayıf, ortak saat sonradan eklenir | 0 | İyi | Ret |
| Tek ffmpeg süreci (A+V tek boru) | Yine yeniden başlatma; ffmpeg altyazı/parça/hızı çalışırken değiştiremez | Tek borudan iyileşir | 0 | İyi | Ret |
| LibVLCSharp | Anında | İyi | ~90 MB | Video callback var, ancak API kaba | Ret |
| **libmpv, yazılım render API** | Anında, hepsi özellik ataması | Motorun kendi saati, `avsync` okunabilir | ~35 MB (kendi ffmpeg'i içinde) | `vo=libmpv` + `ao=null` ile başsız, BGRA tampon → WriteableBitmap | **Seçildi** |

Gerekçe: Standart listesindeki 30 özelliğin 27'si libmpv'de tek `mpv_set_property` / `mpv_command`. ffmpeg borularıyla her biri süreç yeniden başlatma, dolayısıyla ayrı bir "yeniden aç ve aynı noktaya ara" senkron sorunu demek. Yazılım render (`MPV_RENDER_API_TYPE_SW`) bugünkü BGRA→WriteableBitmap yolunu birebir korur; GL bağlamı, NativeControlHost, macOS `wid` sorunu yok. İleride GL'e geçiş ayrı bir dalga olur, gerekmezse olmaz.

Sınırlar: ffmpeg paketten çıkmaz, Küçült/Dönüştür ve karşılaştırma paneli (hstack) olduğu gibi kalır; oynatıcıda ffmpeg ikilemesi kabul. Bağlayıcı hazır NuGet değil, ~15 fonksiyonluk kendi P/Invoke katmanı (create/initialize/set_option/command/get-set-observe_property/wait_event/render_context_*/free). NAudio yalnız PreviewAudio'da kalır, 5. dalgada gider.

Önkoşullar (0. dalgadan önce):
- Lisans: libmpv LGPL derlemesi (`-Dgpl=false`) kullanılacak; VidShrink'in lisansı GPL ise sıradan derleme de olur. T0 lisansı teyit eder.
- İkili tedariki: Windows `libmpv-2.dll` (LGPL derleme), macOS `libmpv.dylib` (arm64+x64); CI'ya ikili yoksa ilgili testler **kırmızı** döner, atlanmaz (bellek: CI'da ffmpeg yokluğu yeşil okundu).
- Hafıza kuralı: `--no-build` yok, her dalga temiz derleme ile ölçülür.

### Dalgalar

Her dalga: kendi dalı, `dotnet test` tam yeşil, `.calisma/` temiz, sayılar test içinde eşikle pimli, 43 dil alt ajanla aynı dalga içinde (LocalizationTests kırmızı kalmaz), kanıt dosyası `docs/olcumler/`.

| # | Dalga | Kapsam | Önkoşul | Kabul ölçütü |
|---|---|---|---|---|
| 0 | Çekirdek | `VidShrink.Player` projesi: P/Invoke, `IPlaybackEngine`, SW render → PlayerView; bugünkü paritesi (oynat/duraklat, arama, teker, zoom/pan, tam ekran, sağ tık, takılma = `paused-for-cache`) | Lisans, ikililer, CI | Mevcut PlayerView testleri yeni motorla yeşil; arama gecikmesi ≤150 ms (komut → ilk render); `avsync` 10 sn sonra ≤40 ms; başsız testte en az bir kare çözülür (tampon sıfır değil) |
| 1 | Günlük denetim | Ses/sessiz, hız, kare kare, atlama adımları, A-B, kaldığı yerden devam + yer imleri, tam klavye ve fare haritası | 0 | 2×'te 2 sn duvar saatinde `time-pos` ilerlemesi 4 sn ±%5; kare adımı `1/fps` ±%10; A-B döngüsü sınırı aşmaz; her kısayol satırı bir komuta bağlı (kaynak okuyan pim testi); devam konumu yeniden açılışta ±1 sn |
| 2 | Altyazı ve ses parçası | Dış/gömülü altyazı, gecikme, boyut/konum, kodlama; ses parçası, ses gecikmesi | 1 | Testte ffmpeg ile üretilen 2 sesli + 1 altyazılı dosyada `sid`/`aid` geçişi; `sub-delay` ve `audio-delay` yazılıp geri okunur; cp1254 srt bozuk karakter vermez |
| 3 | Görüntü, pencere, liste | En-boy, döndür/aynala, her zaman üstte, çift tık tam ekran, bilgi paneli, ekran görüntüsü, bölüm/yer imi çubuğu, son açılanlar, klasörde sonraki, oynatıcıya sürükle-bırak, karıştır/tekrar | 1 | Ekran görüntüsü dosyası kaynağın çözünürlüğünde; bilgi paneli `track-list`ten dört alanı gösterir; PgDn klasörde sıradaki dosyayı açar; son açılanlar 10 ile sınırlı ve kalıcı |
| 4 | Gelişmiş | Parlaklık/kontrast/doygunluk/ton, keskinlik/deinterlace, ekolayzer, normalleştirme, %200 güçlendirme, kırpma, arama küçük resmi, klip/GIF (ffmpeg çekirdeğine aralık verir), mini mod, URL, altyazı biçemi | 3 | Her ayar yazılıp geri okunur ve sıfırlanır; küçük resim istek → görüntü ≤300 ms; klip dışa aktarma mevcut sıkıştırma testlerinden geçer |
| 5 | Ortak çekirdek | Karşılaştırma paneli ve PreviewAudio `IPlaybackEngine`e; DecoderPipe, PipeComparisonFrameSource, NAudio → `trash/` | 4 | İki örnek arasında `time-pos` farkı ≤1 kare; eski yola canlı başvuru kalmaz (rg ile pim) |
| 6 | Sistem | Dosya ilişkilendirme (Windows kurulum kaydı, macOS Info.plist), komut satırından dosya açma | 3 | Çift tıklanan dosya oynatıcı sekmesinde açılır; ikinci örnek açılmaz, mevcut pencereye iletilir |

Kaba maliyet: 0. dalga en pahalısı (yeni proje + bağlayıcı + parite testleri), sonrakiler çoğunlukla arayüz ve test. 43 dil her dalgada bir sonnet turu.

## 3. Gelişmiş başlığının yeri

| Yer | İçerik |
|---|---|
| Denetim çubuğu | Yalnız Standart: oynat, süre, arama çubuğu (bölüm/yer imi işaretli), ses, hız, altyazı, ses parçası, tam ekran. Gelişmiş buraya çıkmaz. |
| Sağ tık menüsü | Standart maddeler üstte; en altta **Gelişmiş ▸** alt menüsü: Görüntü ayarları… / Ses ayarları… / Altyazı biçemi… / Kırpma / Mini mod / URL aç… / Klip-GIF dışa aktar… |
| Ayarlar sekmesi › Oynatıcı | Kalıcı varsayılanlar; **Gelişmiş** katlanır grup (kapalı açılır): güçlendirme sınırı, deinterlace politikası, küçük resim açık/kapalı, ekolayzer ön ayarı. |

Gerekçe: günlük kullanan kişi Gelişmiş'i hiç görmez; arayan tek tıkla bulur. Renk paletten, ölçü belirteçten; menü üç satırdan büyürken ayrı stil yazılmaz.

## 4. Kısayol şeması

Temel katman **GOM varsayılanları**; kullanıcı GOM'u ölçüt gösterdi. Mevcut VidShrink haritasıyla çakışmalar aşağıdaki gibi çözülür.

| Girdi | GOM | VidShrink bugün | Karar | Gerekçe |
|---|---|---|---|---|
| Teker | Ses | Arama 10 sn | **Ses** | GOM alışkanlığı; arama klavyede ve Ctrl+tekerde duruyor. |
| Ctrl+teker | — | Arama 60 sn | **Arama 10 sn** | 10 sn günlük adım; testler yeniden pimlenir. |
| Shift+teker | — | Arama 300 sn | **Arama 60 sn** | Kademe korunur. |
| Ctrl+Shift+teker | — | — | **Arama 300 sn** | 300 sn adım kaybolmaz. |
| Alt+teker | — | Zoom/pan | **Zoom/pan** | GOM'la çakışmaz, korunur. |
| Orta tık | — | Tam ekran | **Tam ekran** | Korunur; çift tık da eklenir. |
| Çift tık | Tam ekran | — | **Tam ekran** | GOM. |
| Sağ tık | Menü | Menü | Menü | Aynı. |
| ← / → | 10 sn | — | 10 sn (Ctrl 60, Shift 300, Alt 1 sn) | 1 sn adım buraya taşınır; kare için F var. |
| ↑ / ↓ | Ses | — | Ses | GOM. |
| Boşluk | Oynat/duraklat | Aynı | Aynı | — |
| Enter / Esc | Tam ekran / çık | Esc var | GOM | — |
| C / X / Z | Hız + / − / sıfırla | — | GOM | — |
| F / Shift+F | Kare ileri | — | F ileri, Shift+F geri | mpv `frame-back-step` ile geri de gelir. |
| [ / ] | A-B başı / sonu | — | GOM | — |
| M | Sessiz | — | GOM | — |
| A / S | Ses parçası / altyazı döngüsü | — | GOM | — |
| Ctrl+E | Ekran görüntüsü | — | GOM | — |
| Ctrl+F1 | Bilgi | — | GOM | — |
| Ctrl+A | Her zaman üstte | — | GOM | — |
| PgUp / PgDn | Önceki / sonraki dosya | — | GOM | — |
| N / B | Yer imi ekle / git | — | GOM | — |
| Menu tuşu | — | Sağ tık menüsü | Korunur | Çakışma yok. |

Kural: harita tek bir tablo sınıfından (`Keymap`) üretilir; sağ tık menüsü ve Ayarlar›Oynatıcı›Kısayollar sayfası aynı tablodan okur, böylece belge ve davranış ayrışamaz (bellek: sabit karşılaştıran test davranış ölçmez, harita testi kaynaktan okur). Üst bar sekmelerinin Alt kısayolları varsa 1. dalgada tabloya alınıp çakışma testi yazılır.

### Önbilgilendirme

libmpv, mpv oynatıcısının kütüphane hâli: dosyayı kendisi çözer, ses ve görüntüyü kendi saatiyle eşler, altyazı ve filtreleri içinde taşır. "Yazılım render" demek, çizdiği kareyi bize BGRA bayt dizisi olarak vermesi; bugün ffmpeg borusundan aldığımızla aynı biçim, o yüzden arayüz tarafı değişmiyor. Bedeli iki şey: paket ~35 MB büyür ve lisans derlemesine dikkat edilir.

## Senden istediklerim

1. 0. dalga açılmadan VidShrink'in lisansını teyit et; GPL değilse ikili olarak LGPL derlemesini (`libmpv-2.dll`, `-Dgpl=false`) tedarik listesine yaz.
