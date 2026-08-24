# Plan — ortadaki iki taraflı karşılaştırma oynatıcısı

Tarih: 24.08.2026. Durum: **T33 kısmen ölçüldü** — boru yaklaşımı G1'i geçti, G2'de kaldı;
libmpv (Aday B) henüz hiç ölçülmedi. Ölçüm: `docs/olcumler/T33-oynatma-olcumleri.md`.

> **Ölçülmüş sonuç — planın bant genişliği bölümü artık tahmin değil.**
>
> | Panel | Boruyla | Borusuz | Boruda kaybolan |
> |---|---|---|---|
> | 2×960×540 | 324,8 fps | 341,5 fps | %4,9 |
> | 2×1280×720 | 154,9 fps | 314,6 fps | %50,8 |
> | 2×1920×1080 | 37,3 fps | 171,8 fps | **%78,3** |
>
> **Duvar borunun kendisi, kod çözme değil.** 2×1080p'de ffmpeg 172 fps üretebiliyor ama
> boru 37 teslim ediyor. Kanıt CPU'da: çözünürlük yükseldikçe ffmpeg'in CPU'su **düşüyor**
> (%638 → %137) — süreç kareyi hesaplamakla değil boruya yazmakla meşgul.
>
> Kullanıcının "1080p sınırı" itirazının sayısal karşılığı buydu ve haklı çıktı: sınır kod
> çözmede değil, ham kareyi işlemciden geçirmekte.
>
> **Sonuç: boru yolunda 2×1080p'nin pratik tavanı 30 fps.** Kullanıcının istediği 60+ fps
> 1080p'de boruyla karşılanamıyor; 2×1280×720'de (153 fps) ve 2×960×540'ta (309 fps)
> rahat karşılanıyor. 60+ fps'i 1080p'de isteyen tek aday **libmpv** ve o henüz ölçülmedi.

## 1. İstenen

Kullanıcının kendi cümleleriyle, üç mesaja dağılmış hâlde:

> ortadaki ekranda bir video olacak (…) solda orjinal kalite sağda sahte kalite,
> fare bu karenin aşağısına gittiğinde media kontrol tuşları gözükecek, normalde
> saydam olacak, farenin tekerleği ile zoom (…) bu panel diğer panellerin üstünde olacak

> orjinal görüntüyü sıkıştırılmış diye sunmuyoruz, orjinal görüntünün bu kısmı işleme
> sokuluyor diye sunmuş olucaz

> ben orda 60+fps oynayan 2 videoyu karşılaştırabileceğim bir panel istiyorum,
> ajanların konseyi ancak destekler, kullanıcı talebi nihaidir

## 2. Kapanmış tartışmalar

**Oynatma olacak.** Konseyin "duran kare, oynatma yok" maddesi hükümsüz —
`docs/KONSEY-karsilastirma-paneli.md` §2'ye geçersizlik notu düşüldü.

**Oynatıcı çekirdeği gömmek yasak değil.** Konsey bunu +60-100 MB, Linux'ta
self-contained kaybı ve süreç içi çökme gerekçeleriyle reddetmişti; üçü de **bakımcı
tarafındaki** maliyetler ve kullanıcı ödemeyi açıkça kabul etti. T33 bu yüzden mpv'yi
yedek değil **eşit aday** olarak ölçüyor.

**Yakınlaştırma tavanı programın tamamı.** Terfi t=1.00'de, iniş t=0.92'de (histerezis,
yoksa titrek tekerlekte panel iki barınak arasında çırpınır). Tavana varınca tekerlek
durur.

## 3. Planın omurgası — üç maliyet sınıfı ayrılıyor

Bu ayrım planın en önemli parçası ve kullanıcının itirazından çıktı:

> nasıl yani, 2 videoyu aynı anda izleyebilecek bir altyapımız var, sadece bunların
> gözüken kısımlarını ayarlayacaksın, nasıl akıcı olmaz?

Haklıydı. Üç şey birbirine karıştırılmıştı:

| Sınıf | Ne yapıyor | Maliyet | Ölçen |
|---|---|---|---|
| **Sunum** | ayırıcıyı sürükle, yakınlaştır, panosunu kaydır | **sıfır kod çözme** — eldeki birleşik kare üstünde kırpma ve dönüşüm | — |
| **Oynatma** | 60 fps sürekli kod çözme | asıl maliyet, bant genişliği burada | T33 · G1, G2 |
| **Atlama** | zaman çizgisinde başka yere git | T32: süreç açılışı 692-800 ms p95 | T33 · kalıcı süreç bunu amorti ediyor mu |

**Mimari şart:** sunum katmanı hiçbir koşulda kod çözme tetiklemeyecek. Ayırıcı sürükleme
ve yakınlaştırma, o an elde olan kareyle çalışacak; yeni kare istemeyecek. Bu kural
tutulursa ayırıcı, oynatma dursa bile akıcı kalır.

T32'nin bulgusu bu ayrımı doğruluyor: p95 kuyruğu anahtar kare uzaklığı değil **süreç
açılışı** çıktı — hiç kare çözmeyen `ffprobe -show_format` bile aynı kuyruğu gösteriyor.
Yani maliyet süreç başına, kare başına değil. Kalıcı süreç bunu bir kez ödeyip bitirir.

## 4. Mimari — iki aday, kapı seçecek

### Aday A: tek ffmpeg süreci, birleştirilmiş boru

Tek süreç, iki girdi, `fps` + `scale` + `hstack` grafiği, `-f rawvideo -pix_fmt bgra`
ile çift genişlikte kare.

Üç sorunu tek hamlede kapatıyor: tek yüzey (airspace yok), tek süreç ve tek saat
(kare kilidi pazarlığı yok), kod çözen taraf ayrı süreç (çökme yalıtımı duruyor).

Bant genişliği — birleştirme boruya **girmeden önce** ve panel çözünürlüğünde olduğu için
kaynak çözünürlüğü değil panel boyutu belirliyor:

| Panel | Kare | 60 fps'te |
|---|---|---|
| 2×960×540 | 4,1 MB | ~249 MB/s |
| 2×1280×720 | 7,4 MB | ~442 MB/s |
| 2×1920×1080 | 16,6 MB | ~995 MB/s |

Son satır adayın duvarı. Kullanıcının aşmak istediği 1080p sınırı **borunun sınırı**.

**Ölçüldü ve doğrulandı.** Makinenin boru tavanı ~590 MB/s (16,6 MB'lık karelerde) ile
~1285 MB/s (4,1 MB'lık karelerde) arasında — kare büyüdükçe taşıma verimi düşüyor.
Konseyin öngördüğü ~1 GB/s rakamı doğru çıktı ve tavan tam oraya düşüyor.

**K3 kararı ölçümle desteklendi.** Sürdürülen fps 2×1080p'de hedeften bağımsız olarak
~37,7 kalıyor, çünkü duvar bayt/saniye cinsinden. Fps'i 30'a düşürmek tam çözünürlüğü
kurtarıyor (37,7 > 30). Aynı bütçeyi çözünürlük düşürerek de almak mümkün ama o zaman
incelenecek artefakt yok oluyor — **fps düşer, çözünürlük düşmez** kararı geçerli.

### Aday B: libmpv render API

`OpenGlControlBase` üzerinde `mpv_render_context`, iki örnek, iki doku, bileşimlemeyi biz
yaparız. Kareler ekran kartında kalır — yukarıdaki tablonun tamamı ortadan kalkar.

İki şeyi boru hiç veremiyor: **ses** (Avalonia'nın ses çıkışı yok, mpv kendi çalar) ve
**4K**. Karşılığında +60-100 MB kurulum, Linux'ta self-contained sorunu, süreç içi çökme
riski ve karışık GPLv2+/LGPL lisans durumu.

`VideoView` yaklaşımı ölçülmüyor — airspace yüzünden ayırıcı çizgi kurulamıyor, elenmiş.

### Karar kuralı (T33'te yazılı)

İkisi de G1-G3'ten geçerse **boru** seçilir (0 MB, yalıtım, Linux). Boru kalıp mpv geçerse
**mpv** seçilir ve maliyetleri kabul edilir. İkisi de kalırsa vekil dilim yoluna geçilir.

Ses ve 4K mpv'nin lehine ayrı ağırlık taşıyor: boru sınırda geçip mpv rahat geçerse karar
T0'a kalır.

## 5. Panelin katmanları

```
┌─ Kök katman ─ terfi edince panel buraya doğar, t=1'de program boyu ────┐
│ ┌─ Panel ────────────────────────────────────────────────────────────┐ │
│ │ ┌─ Sunum yüzeyi ─ tek birleşik kare ──────────────────────────────┐│ │
│ │ │        sol: orijinal        │        sağ: işlenmiş             ││ │
│ │ │                       ▲ ayırıcı (sürükle)                      ││ │
│ │ └─────────────────────────────────────────────────────────────────┘│ │
│ │ ┌─ Denetim şeridi ─ normalde saydam, fare alta gelince belirir ───┐│ │
│ │ │  ▶  ──────●─────────────  00:12 / 01:30   [analiz 1/2 · dnm 2] ││ │
│ │ └─────────────────────────────────────────────────────────────────┘│ │
│ └────────────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────┘
```

**Ayırıcı.** Tek birleşik kare üstünde kırpma sınırı. Sürükleme kod çözme tetiklemez,
kare istemez, oynatma dursa bile akıcıdır.

**Yakınlaştırma.** Tekerlek hem paneli hem görüntüyü büyütür, tek jest parametresiyle
(iki sayaç değil). Terfi/iniş histerezisi §2'de. Tavanda tekerlek durur.

**Denetim şeridi.** Normalde saydam; fare panelin alt bölgesine girince belirir.
Azaltılmış hareket ayarı açıksa geçiş anlık olur.

**Panel üstte durur.** Kök katmanda doğar, bandında aynı boyutta yer tutucu kalır — terfi
anında altındaki düzen zıplamaz.

## 6. Kodlama sürerken

Kullanıcının düzeltmesi bağlayıcı: **sahte çıktı sunulmuyor.** Sağ taraf "orijinalin bu
kısmı işleme sokuluyor" diye sunulacak.

Konsey bundan bağımsız olarak daha iyisini buldu ve o seçildi: sağ tarafta **gerçekten
kodlanmış örnek kareler**. İlerleme göstergesi de kalıyor — zaman çizgisinde
`out_time_ms`'ten gelen konum imleci, "analiz 1/2 · deneme 2" etiketiyle.

**Dürüstlük borcu ve kapısı:** örnek kodlama, tam koşumdaki hız denetimiyle birebir aynı
değil. Etiket "örnek" diyecek ve koşum bitince gerçek dosyaya dönecek. **Örneğin nihai
çıktıdan ne kadar saptığı ölçülmeden bu parça ilerlemeyecek** — sapma büyükse kullanıcı
olmayan bir artefakta bakıp yanlış hedef seçer.

T30'un kodlama yavaşlaması ölçümü (%17,8-28,4, ayrı süreç sağanağı) burada geçerli:
kodlama sürerken oynatma açıksa **G3 kapısı** (≤%10) karar verecek. Kalırsa kodlama
sırasında oynatma kapatılır, örnek kareler durur.

## 7. Sözleşme sırası

| # | İş | Rol | Bağlı |
|---|---|---|---|
| **T33** | ölçüm kapısı — boru ve mpv, G1/G2/G3 | builder | koşuyor |
| T38 | kare kaynağı: kazanan mimarinin kalıcı servisi | builder | T33 |
| T39 | panel denetimi: sunum yüzeyi, ayırıcı, yakınlaştırma, terfi/histerezis | ui-builder | T38 |
| T40 | denetim şeridi: saydamlık, fare bölgesi, zaman çizgisi, azaltılmış hareket | ui-builder | T39 |
| T41 | örnek kare sapma ölçümü — dürüstlük kapısı | builder | T38 |
| T42 | kodlama sürerken davranış: örnek kareler, ilerleme imleci | builder | T41, G3 |

T38'in içeriği T33'ün sonucuna göre iki farklı sözleşme olacak; ikisi de yazılmadı çünkü
hangisinin yazılacağı ölçüme bağlı.

**T39 ve T40 T38'e bağlı ama sunum katmanı kare kaynağından bağımsız tasarlanacak** —
ayırıcı ve yakınlaştırma "elde bir kare var" varsayımıyla çalışır, o karenin nereden
geldiğini bilmez. Bu sayede kapı ters sonuç verirse sunum katmanı yeniden yazılmaz.

## 8. Riskler

**Bant genişliği duvarı (Aday A).** 2×1080p60'ta ~1 GB/s. *Nasıl anlarız:* G2 kalır.
*Ne yaparız:* konseyin kararı — **fps düşer, çözünürlük düşmez.** Panelin varlık sebebi
artefakt incelemek; çözünürlüğü düşürmek incelenecek şeyi yok eder. T33 bu kararı
destekliyor mu çürütüyor mu, ölçüp yazacak.

**Kullanıcının ffmpeg'inde filtre yok.** WinGet'ten gelen yapıda `hstack`, `zscale`,
`tonemap` ya da `d3d11va` olmayabilir. T33 bunu liste sorgusuyla değil gerçek denemeyle
sınıyor. *Ne yaparız:* eksik filtre panelin hangi yeteneğini kapatıyor, arayüz söyler.

**Kare başına bellek ayırma.** 4-16 MB'lık tamponlar kare başına ayrılırsa çöp toplayıcı
duraklamaları 60 fps'i öldürür. Hedef sıfır: sabit havuz, sıfır kopya okuma. T33 · Ö6.

**mpv lisansı (Aday B).** Gövde GPLv2+/LGPL karışık; `RAPOR.md:106` shinchiro yapılarının
lisanssız olduğunu söylüyor, o kaynak kullanılamaz. Proje AGPL-3.0-or-later — uyum var ama
hangi ikili kaynağının kullanılabileceği T33'te ayrıca araştırılıyor.

**Örnek kare gerçeği yansıtmıyor.** §6'daki dürüstlük kapısı. Ölçülmeden geçilmeyecek.

**Airspace.** `VideoView` yaklaşımı bu yüzden zaten elendi; kazanan mimari ne olursa olsun
ayırıcı çizgisi native bir yüzeyin üstüne çizilemez. Her iki aday da tek yüzeye çizdiği
için sorun yok — bu şart kapı sonrasında da korunacak.

---

## 9. Platform ayrışması — ikinci görüş ve karar

Kullanıcı şunu önerdi:

> eğer bu dediğim linux versiyon için mümkün değilse linux ve macos için ses olmayan
> max 1080p destekleyen çözümü uygulayıp uygulamayı onlar için farklı bizim için farklı
> yapabiliriz

Geri alınması pahalı bir mimari seçim olduğu için ikinci görüş alındı (opus, advisor).

### Karar: ayrışma evet, iki mimari hayır

Görüş öneriyi kabul etti ama biçimini değiştirdi ve gerekçesi ikna edici:

**İki eşit kod yolu kurma. Tek mimari, tek yetenek bayrağı kur.**

- Kare kaynağı bir arayüz olarak yazılır.
- **Boru uygulaması üç platformda da varsayılandır** ve tek gerçek yoldur.
- libmpv, üstüne takılan bir **hızlandırıcıdır** — alternatif bir program değil.
- libmpv yüklenemezse (kütüphane yok, çökme) sessizce boruya düşülür.

Kritik nokta: **bu düşüş zaten Linux/macOS davranışıdır.** Yani ayrı bir kod yolu değil,
her gün çalışan ve test edilen yoldur. İki eşit yol kurulursa ikisi de "asıl" olur ve
Linux'ta çıkan bir hata Windows'ta üretilemez hâle gelir; bakım maliyeti kod satırından
değil buradan doğar.

### Bayrak "Windows mu" değil, "libmpv var mı"

Görüşün yakaladığı ve planın kaçırdığı şey: **macOS'u Linux'la aynı kefeye koymak
yanlış.** libmpv macOS'ta Homebrew üzerinden gelir ve Linux'takinden temiz paketlenir.
Asıl sorunlu olan yalnız **Linux'un self-contained yayımı**.

Bayrak `OperatingSystem.IsWindows()` değil, `libmpv bulunabildi mi` olacak. Böylece
macOS de sesi bedavaya alır ve Windows'ta mpv kurulu değilse doğru davranır.

### Ses ve 4K yetenektir, farklı davranış değil

Program "iki platformda farklı davranmıyor" — panel daha az yetenek gösteriyor. Denetim
şeridi ses düğmesini göstermezse kullanıcı bunu bozukluk saymaz.

Kabul edilemez olan tek fark **30 fps ile 60 fps arası**. O bayrakla açıklanmayacak;
§4'ün **fps düşer, çözünürlük düşmez** kararıyla açıklanacak ve üç platformda da aynı
kural uygulanacak.

### Önce ölçüm — bu karar henüz uygulanmayacak

Görüş bir sıra hatası daha yakaladı: **Ö4 hiç ölçülmedi.** Avalonia'nın
`WriteableBitmap` sunum yolu 309 fps'i taşıyamıyorsa boru ile mpv tartışması yanlış
yerde yapılıyor demektir.

Bu, T33 tur 1'in Ö10 ölçümüyle örtüşüyor (tüketicisiz boru tavanı) — bağımsız olarak
aynı yere işaret ettiler. **libmpv indirme izni istenmeden önce o ölçüm koşulacak.**

Ölçümlerden biri 2×1080p'de 60 fps verirse bu bölümün tamamı gereksizleşir: boru
kurtulur, libmpv hiç ödenmez, ayrışma sorusu ortadan kalkar.
