# İkon Estetiği: Büyük Markaların İkonları Neden Daha Hoş Görünüyor

Araştırma tarihi: 13 Eylül 2026. İnceleme konusu `src/VidShrink.App/Themes/Icons.axaml` içindeki
26 adet elle yazılmış `StreamGeometry`.

Bu belgedeki her sayı ya resmî belgeden ya depo README'sinden ya da ölçülerek çıkarıldı.
Kendi ikonlarımızın ölçüleri `svgelements` ile sınır kutusu hesaplanarak alındı; kaynak
kümelerin gövdeleri Iconify API'sinden (`api.iconify.design`) indirilip okundu.

## 0. Kısa Cevap

Büyük oynatıcıların ikonları "daha hoş" göründüğü için değil, **bir sistemden çıktığı için**
öyle duruyor. Windows 11 Media Player'ın simgeleri Microsoft'un Fluent ikonografisinden
gelir; Fluent, ikonları 12/16/20/24/28/32/48 px boyları için **ayrı ayrı** çizer ve Regular
ile Filled iki temada tutar. Aradaki fark üç kalemde toplanıyor ve üçü de ölçülebilir:

1. Köşe yarıçapı geometriye gömülüdür (Lucide 2 px, Material 2 dp), kalemin `linejoin`
   yuvarlamasına bırakılmaz.
2. Şekiller anahtar çizgi (keyline) ölçülerine oturur — kare 18, daire 20 — yani optik
   boyut dengelemesi yapılır.
3. Koordinatlar tam sayıdır ve çizgi kalınlığı ızgaranın tam böleni seçilir (24'te 2), yani
   çizginin iki kenarı da piksel sınırına oturur.

Bizim 26 yolumuzda birincisi hiç yok, ikincisi yarım, üçüncüsü kalınlık 1.5 seçildiği için
bozuk. Ayrıntı ve sayılar aşağıda.

## 1. Profesyonel Setleri Ayıran Ölçülebilir Özellikler

### 1.1 Izgara, Canlı Alan ve Dolgu Payı

| Küme | Tuval | Canlı alan | Kenar payı |
|---|---|---|---|
| Material (sistem ikonu) | 24×24 dp | 20×20 dp | 2 dp her kenar |
| Material (yoğun yerleşim) | 20×20 dp | 16×16 dp | 2 dp |
| Lucide | 24×24 px | 22×22 px | en az 1 px |
| Bootstrap Icons | 16×16 px | — | — |
| Phosphor | 256×256 px | — | — |
| Fluent UI System Icons | 12/16/20/24/28/32/48 px, her boy ayrı çizilir | — | — |

Material'ın kuralı açık: "24 dp × 24 dp alanda 2 dp dolgu bırakın ve yalnız canlı alanın
içine çizin". Lucide'ın kuralı: "İkonlar 24×24 piksel tuvale çizilmeli, tuval içinde en az
1 piksel dolgu bulunmalı".

### 1.2 Keyline (Anahtar Çizgi) Şekilleri — Optik Boyut Dengelemesi

Material'ın 24 dp sistem ikonu için tanımladığı dört anahtar şekil:

| Şekil | Ölçü |
|---|---|
| Kare | 18 × 18 dp |
| Daire | 20 dp çap |
| Dikey dikdörtgen | 16 geniş × 20 yüksek dp |
| Yatay dikdörtgen | 20 geniş × 16 yüksek dp |

Buradaki kritik nokta **kare 18, daire 20**: aynı boyda çizilen bir kare ile bir daire, göz
tarafından aynı büyüklükte algılanmaz — daire köşelerini kaybettiği için küçük görünür.
Bu yüzden daire karenin %11 üstünde çizilir. Bu "optik boyut dengelemesi"dir; geometrik
eşitlik değil, algısal eşitlik hedeflenir.

Lucide aynı sistemi uyguluyor: `square` = 18×18 (x=3, y=3, rx=2), `circle` = r 10 → 20 çap.

### 1.3 Çizgi Kalınlığı Tutarlılığı

- Material: "tüm çizgi örneklerinde 2 dp kalınlık koruyun" — eğrilerde, açılarda, her yerde.
- Lucide: kalınlık **tam olarak 2 px** olmak zorunda.
- Feather: varsayılan `stroke-width="2"`, `viewBox="0 0 24 24"`.
- Tabler: 24×24 ızgara, 2 px çizgi.

24'lük ızgarada 2 px kalınlık bir tesadüf değil: 24/2 = 12, yani kalınlık ızgaranın tam
bölenidir; koordinatlar tam sayı olduğunda çizginin iki kenarı da tam sayı piksel sınırına
oturur (örn. x=9 merkezli 2 px çizgi → 8.00 ve 10.00).

### 1.4 Terminal Uçları ve Birleşimler

- Material: uçlar **köşeli** (squared terminals), dış köşelerde **2 dp yuvarlama**, iç
  köşeler kare kalır.
- Lucide/Feather/Tabler: `stroke-linecap="round"`, `stroke-linejoin="round"`, ve ayrıca
  şekillerin kendi köşe yarıçapı **2 px**.

Buradaki ayrım bizim için en önemlisi: `stroke-linejoin="round"` bir köşeyi yalnız
kalınlığın yarısı kadar yuvarlar (2 px kalınlıkta 1.0, bizde 1.5 kalınlıkta **0.75**).
Lucide'ın "hoş" görünmesini sağlayan yuvarlama bu değil, **geometrinin kendi içine
gömülmüş 2 birimlik yay**tır. Lucide'ın `play` ikonu:

```
M5 5a2 2 0 0 1 3.008-1.728l11.997 6.998a2 2 0 0 1 .003 3.458l-12 7A2 2 0 0 1 5 19z
```

Üçgenin üç köşesinin de `a2 2 0 0 1` yayıyla yuvarlandığına dikkat. Aynı şekilde Lucide'ın
`pause` ikonu iki düz çizgi değil, `rx="1"` köşe yarıçaplı iki **dikdörtgen**:
`<rect width="5" height="18" x="5" y="3" rx="1"/>` ve aynısı x=14'te.

### 1.5 Optik Merkez ve Optik Hizalama

Geometrik merkez (sınır kutusunun ortası) ile optik merkez (gözün ağırlık merkezi) aynı
değildir. Sağa bakan bir üçgende kütle sola yığılır: `A(6,4) B(20,12) C(6,20)` üçgeninin
alan ağırlık merkezi x = (6+20+6)/3 = **10.67**, oysa sınır kutusu merkezi **13.00**.
Sektörün pratiği ikisinin arasında bir uzlaşma: sınır kutusunu merkezin sağına kaydırmak.

Ölçülen gerçek değerler (24'lük kutuda, kutu merkezi 12.00):

| Küme | play yolu | x aralığı | kutu merkezi | alan ağırlık merkezi |
|---|---|---|---|---|
| Material Symbols | `M8 19V5l11 7z` | 8 → 19 | 13.50 (+1.50) | 11.67 |
| Tabler | `M7 4v16l13-8z` | 7 → 20 | 13.50 (+1.50) | 11.33 |
| Lucide | yukarıdaki yay'lı yol | 5 → 21 | 13.00 (+1.00) | ≈10.33 |
| Feather | `m5 3l14 9l-14 9z` | 5 → 19 | 12.00 (0.00) | 9.33 |
| **VidShrink** | `M 6,4 L 20,12 L 6,20 Z` | 6 → 20 | **13.00 (+1.00)** | 10.67 |

Yani kaydırma miktarı sektörde 0 ile +1.5 arasında değişiyor; bizim +1.00'imiz bu aralığın
tam ortasında ve Lucide'ın kaydırmasıyla birebir aynı. **Play üçgenimizin yatay yerleşimi
bozuk değil.** Bozuk olan şey
başka yerde (bkz. 2. bölüm).

### 1.6 Piksel Hizalama (Hinting / Pixel Snapping)

Material'ın kuralı: ikonlar "piksel üstüne" konumlanır, X/Y koordinatları ondalıksız tam
sayı olur ve %100 ölçekte çizilir. Bir çizginin net görünmesi için **çizginin iki kenarının
da** tam sayıya oturması gerekir; bu, merkez koordinatı tam sayıysa kalınlığın çift sayı
olmasını (2, 4) şart koşar.

Çizgi kalınlığı 1.5, merkezi x=9 olan bir çizgi 8.25–9.75 arasını kaplar: 8. piksel sütunu
%75, 9. sütun %75 örtülür, hiçbir sütun tam dolmaz. Sonuç iki taraflı gri bulanıklık.

### 1.7 Optik Boy (Optical Size) Ekseni

Profesyonel setler tek bir çizimi ölçekleyip küçültmez, her boy için ayrı çizer:

- **Material Symbols** değişken font eksenleri: `opsz` 20–48 (varsayılan 24), `wght` 100–700
  (varsayılan 400), `GRAD` -50…200, `FILL` 0/1.
- **Fluent UI System Icons**: 12, 16, 20, 24, 28, 32, 48 px boyları ayrı ayrı çizilmiş,
  Regular ve Filled iki tema.
- **Phosphor**: Thin / Light / Regular / Bold / Fill / Duotone ağırlıkları.

Tek bir 24'lük çizimi 16 px'e küçülttüğünüzde çizgi kalınlığının göreli oranı değişir; bu
eksenler tam olarak bu sorunu çözmek için var.

## 2. Bizim Yollarımız Nerede Hata Yapıyor

Ölçüm: her `StreamGeometry`den `M 0,0 M 24,24` sabitleyicisi çıkarıldı, kalan yolun sınır
kutusu hesaplandı.

| Anahtar | x0 | x1 | y0 | y1 | en | boy | cx | cy |
|---|---|---|---|---|---|---|---|---|
| IconPlayer | 2.00 | 22.00 | 2.00 | 22.00 | 20.00 | 20.00 | 12.00 | 12.00 |
| IconPlay | 6.00 | 20.00 | 4.00 | 20.00 | 14.00 | 16.00 | 13.00 | 12.00 |
| IconPause | 9.00 | 15.00 | 4.00 | 20.00 | 6.00 | 16.00 | 12.00 | 12.00 |
| IconRewind | 2.00 | 22.00 | 5.00 | 19.00 | 20.00 | 14.00 | 12.00 | 12.00 |
| IconFastForward | 2.00 | 22.00 | 5.00 | 19.00 | 20.00 | 14.00 | 12.00 | 12.00 |
| IconVolume | 2.00 | 21.28 | 5.00 | 19.00 | 19.28 | 14.00 | **11.64** | 12.00 |
| IconVolumeMute | 2.00 | 22.00 | 5.00 | 19.00 | 20.00 | 14.00 | 12.00 | 12.00 |
| IconSpeed | 2.00 | 22.00 | 2.73 | 18.00 | 20.00 | 15.27 | 12.00 | **10.37** |
| IconFullScreen | 3.00 | 21.00 | 3.00 | 21.00 | 18.00 | 18.00 | 12.00 | 12.00 |
| IconCamera | 3.00 | 21.00 | 5.00 | 19.00 | 18.00 | 14.00 | 12.00 | 12.00 |
| IconChevronDown | 5.00 | 19.00 | 9.00 | 16.00 | 14.00 | 7.00 | 12.00 | **12.50** |
| IconChevronUp | 5.00 | 19.00 | 8.00 | 15.00 | 14.00 | 7.00 | 12.00 | **11.50** |
| IconClose | 5.00 | 19.00 | 5.00 | 19.00 | 14.00 | 14.00 | 12.00 | 12.00 |
| IconMaximize | 4.00 | 20.00 | 4.00 | 20.00 | 16.00 | 16.00 | 12.00 | 12.00 |
| IconMinimize | 4.00 | 20.00 | 12.00 | 12.00 | 16.00 | 0.00 | 12.00 | 12.00 |
| IconCoffee | 5.00 | 20.50 | **0.50** | 17.50 | 15.50 | 17.00 | **12.75** | **9.00** |
| IconRestart | 5.00 | 20.00 | 5.00 | 19.00 | 15.00 | 14.00 | **12.50** | 12.00 |

### 2.1 Köşe Yuvarlaması Sistemi Yok (En Görünür Fark)

26 ikonun hiçbirinde geometriye gömülü köşe yarıçapı yok. `IconMaximize` (`M 4,4 H 20 V 20
H 4 Z`) keskin 90°, `IconPlay` üçgeninin tepesi keskin sivri uç. `StrokeJoin="Round"` bu
köşeleri yalnız **kalınlığın yarısı = 0.75 birim** yuvarlar. Lucide'ın karşılığı **2.0
birim** — yaklaşık **2.7 kat** daha yumuşak. "Hoş görünme" farkının en büyük tek kalemi bu.

`IconPlay` tepesindeki iç açı ≈ 59.5°; 0.75 yarıçaplı bir yuvarlama bu açıda görsel olarak
iğne ucu bırakır, 2.0 yarıçap bırakmaz.

### 2.2 Çizgi Kalınlığı 1.5 — Piksel Izgarasına Oturmuyor

`Theme.axaml:261` → `IconStroke = 1.5`. Sektörün tamamı 24'lük ızgarada 2 kullanıyor
(Material, Lucide, Feather, Tabler). 1.5 ile tam sayı koordinatlı her çizginin iki kenarı
da yarım pikselde kalır (8.25 / 9.75) ve hiçbir DPI'da netleşmez.

Üstüne: ikonlar `Controls.axaml:871-879`'da `IconSizeSm = 16` kutuya `Stretch="Uniform"`
ile çiziliyor. Avalonia'nın `Shape.CalculateSizeAndTransform` metodu geometriyi ölçekler
ama kalemi ölçeklemez — kaynaktaki yorum bunu açıkça söylüyor:

> "This should probably use GetRenderBounds(strokeThickness) but then the calculations will
> multiply the stroke thickness as well, which isn't correct."

Sonuç: 24'lük geometri 16 px kutuda 0.667 ile küçülürken kalınlık 1.5 kalıyor, yani tasarım
ızgarasındaki **göreli kalınlık 2.25**'e çıkıyor. Aynı ikon 24 px'te 1.5, 16 px'te 2.25
ağırlığında görünüyor. Profesyonel setlerin optik boy eksenlerinin (Material `opsz`, Fluent
ayrı boylar) çözdüğü sorun tam olarak budur.

İkinci sonuç: sınır kutusu hesabına kalınlık dahil edilmediği için, kutunun kenarına dayanan
ikonlarda çizginin yarısı (0.75) kutunun **dışına** taşar.

### 2.3 (a) IconPause — Simetrik, Ama Kütlesi Yok

`M 9,4 V 20 M 15,4 V 20`. 24'lük kutuda optik merkez 12.00'dir ve 9 ile 15, 12'nin tam
±3 birim iki yanındadır: **evet, simetriktir.** Sorun simetri değil:

| Ölçü | Bizim | Lucide `pause` | Material `pause` |
|---|---|---|---|
| Çubuk genişliği | 1.50 (kalem) | 5.00 (rect) | 4.00 (dolgu) |
| Çubuk yüksekliği | 16.00 | 18.00 | 14.00 |
| Boşluk | 4.50 | 4.00 | 4.00 |
| Toplam en | 7.50 | 14.00 | 12.00 |
| Köşe yarıçapı | 0.75 (kalem) | 1.00 (rx) | 0 |
| Mürekkep alanı | 48 br² | 180 br² | 112 br² |

Bizim pause ikonumuz Lucide'ınkinin **%42'si kadar geniş** ve mürekkebinin **%27'si**
kadar. Aynı araç çubuğundaki `IconMaximize` (16×16 kare) yanında cılız kalmasının sebebi
bu. Ayrıca çubuk kenarları 8.25/9.75 ve 14.25/15.75'e düşüyor — hiçbiri piksel sınırı değil.

Düzeltme, mevcut çizgi diline sadık kalarak (kalınlık 2'ye çıkarılırsa):

```
M 9,5 V 19 M 15,5 V 19
```

2 kalınlıkta çubuklar 8.00–10.00 ve 14.00–16.00 olur, dört kenar da tam sayı. Boy 14,
Material'ın pause yüksekliğiyle aynı. Dolgu diline geçilirse köşe yarıçaplı hâli:

```
F1 M 7,5 H 10 A 1,1 0 0,1 11,6 V 18 A 1,1 0 0,1 10,19 H 7 A 1,1 0 0,1 6,18 V 6 A 1,1 0 0,1 7,5 Z M 14,5 H 17 A 1,1 0 0,1 18,6 V 18 A 1,1 0 0,1 17,19 H 14 A 1,1 0 0,1 13,18 V 6 A 1,1 0 0,1 14,5 Z
```

Çubuklar 6–11 ve 13–18, genişlik 5, boşluk 2, toplam 6–18, merkez 12.00, köşe yarıçapı 1.

### 2.4 (b) IconPlay — Kaydırma Değil, Yuvarlama ve Kütle Sorunu

1.5'te gösterilen ölçümlere göre üçgenimizin sınır kutusu merkezi 13.00, yani kutu
merkezinin **1.00 sağında**; Material ve Tabler +1.50, Lucide +0.50, Feather 0.00
kullanıyor. **Ek kaydırmaya gerek yok; mevcut yerleşim sektör aralığının içinde.**

Gerçek sorunlar:

1. **Tepe keskin.** Lucide, Material Rounded ve Fluent hepsi tepede yarıçap kullanıyor.
2. **Boy uyumsuz.** Üçgenimiz 16 birim yüksek; pause çubuklarımız da 16. Material'da ikisi
   de 14, Lucide'da play 18 / pause 18. Bizde play üçgeninin mürekkebi (çevre 48.24 × 1.5 =
   **72 br²**) pause'un mürekkebinden (**48 br²**) %50 fazla — aynı düğmede yer değiştiren
   iki simge olarak ağırlıkları eşleşmiyor, tıklayınca "zıplıyor".
3. **Genişlik fazla.** 14 birim en (Material 11, Tabler 13, Lucide 16).

Önerilen düzeltilmiş yol — Lucide'ın kendi `play` yolu (ISC, dosya başlığındaki beyanla da
tutarlı), köşeleri 2 birim yaylı:

```
M 5,5 A 2,2 0 0,1 8.008,3.272 L 20.005,10.27 A 2,2 0 0,1 20.008,13.728 L 8.008,20.728 A 2,2 0 0,1 5,19 Z
```

Bu yolun ölçülen sınır kutusu 5–21 × 3–21: en 16, boy 18, cx 13.00, cy 12.00.

Çizgi dilini korumak ve elde çizmeye devam etmek isterseniz, 2 birim yarıçaplı ve Material
oranlarına yakın bir üçgen — ölçülen sınır kutusu 7–19 × 5–19, yani en 12, boy 14,
cx 13.00, cy 12.00:

```
M 7,7 A 2,2 0 0,1 10,5.27 L 18,10.27 A 2,2 0 0,1 18,13.73 L 10,18.73 A 2,2 0 0,1 7,17 Z
```

### 2.5 Keyline İhlalleri — Kare Küçük, Daire Doğru

- `IconPlayer`, `IconAbout`, `IconSettings` daireleri 20×20 → **keyline daire (20) doğru.**
- `IconMaximize` / `IconRestore` kareleri 16×16 → keyline kare **18** olmalı; %11 küçük.
  Lucide'ın `square`'i de 18×18 (rx 2). Kare, dairenin yanında olması gerekenden küçük
  duruyor, yani optik dengeleme ters yönde uygulanmış.
- Pencere düğmesi üçlüsü: kapat 14, büyüt 16, küçült 16. Lucide'ın karşılıkları: `x` 12,
  `square` 18, `minus` 14 → 12 : 18 : 14. Bizde 14 : 16 : 16, yani üç düğme birbirine göre
  yanlış oranda. Çapraz çizgiler (X) piksel ızgarasına göre eğik olduğu için anti-aliasing
  ile yayılır ve daha ince/soluk görünür; bu yüzden X profesyonel setlerde en küçük
  şekildir, bizde ortanca.

### 2.6 Dikey Optik Merkez Kaymaları

- `IconSpeed`: dikey merkez **10.37**, kutu merkezinin 1.63 birim üstünde. Bir satırda diğer
  ikonlarla yan yana durunca yukarı kaçmış görünür.
- `IconCoffee`: dikey merkez **9.00** (3 birim yukarıda) ve y0 = **0.50** — 2 birimlik
  kenar payını 1.5 birim ihlal ediyor; üstelik çizgi kalınlığının yarısıyla birlikte
  -0.25'e taşıyor, yani tuvalin dışına çıkıyor. `Stretch="Uniform"` sabitleyicisi yüzünden
  16 px kutuya sığdırılırken üst kenarı kırpılma riski taşıyor.
- `IconChevronDown` cy 12.50 / `IconChevronUp` cy 11.50: ikisi birbirinin aynası olduğu için
  geometrik olarak tutarlı, ama ikisi de kutu merkezinde değil. Lucide'ın `chevron-down`'ı
  `m6 9l6 6l6-6` → y 9–15, cy **12.00**, en 12 (bizde 14).
- `IconVolume` cx **11.64** / `IconVolumeMute` cx 12.00: sesi kapatınca ikon 0.36 birim
  yana sıçrıyor ve eni 0.72 birim değişiyor. Aynı düğmede değişen iki simgenin sınır kutusu
  aynı olmalı.

### 2.7 Sabitleyici Hilesi İşe Yarıyor Ama Bedava Değil

Her yolun başındaki `M 0,0 M 24,24`, `Stretch="Uniform"`un her ikonu kendi sınır kutusuna
göre farklı oranda büyütmesini engelliyor — bu doğru bir çözüm. Bedeli: geometri artık
24'lük kutuya sabitlendiği için `IconMinimize` gibi 0 yükseklikli yollarda ölçek yalnız
genişlikten hesaplanır ve kalem kutunun dışına taşar; ayrıca `Stretch="None"` + sabit
`Width/Height="24"` ile aynı sonuç hileye gerek kalmadan elde edilebilirdi.

## 3. Ücretsiz ve Lisans Olarak Temiz Hazır Setler

Sayılar Iconify koleksiyon kataloğundan (13 Eylül 2026) ve depo README'lerinden.

| Küme | Lisans | İkon sayısı | Varyant | Izgara | Tek `<path>`? |
|---|---|---|---|---|---|
| Material Symbols | Apache-2.0 | 15.642 | Outlined / Rounded / Sharp, FILL 0-1, wght 100-700, opsz 20-48 | 24 (20/40/48 opsz) | Evet (dolgu) |
| Fluent UI System Icons | MIT | 19.757 | Regular / Filled, 12-48 px ayrı çizim | 12/16/20/24/28/32/48 | Evet (dolgu) |
| Phosphor | MIT | 9.072 (≈1.512 × 6 ağırlık) | Thin/Light/Regular/Bold/Fill/Duotone | 256 | Evet (dolgu) |
| Tabler | MIT | 6.184 | Outline / Filled | 24, 2 px çizgi | Hayır (bazen `<g>`, çoklu path) |
| Remix Icon | Apache-2.0 | 3.188 | Line / Fill | 24 | Evet (dolgu) |
| Bootstrap Icons | MIT | 2.078 | Tek stil (dolgu) | 16 | Hayır (bazen çoklu path) |
| Lucide | ISC | 1.829 | Tek stil (çizgi, 2 px) | 24, ≥1 px pay | Hayır (`<rect>`, `<circle>`, `<g>`) |
| Feather | MIT | 286 | Tek stil (çizgi, 2 px) | 24 | Hayır (`<circle>`, `<g>`) |

### 3.1 Medya Oynatıcı İkonlarının Kapsamı

Iconify API'sine gerçek ad sorgusu atılarak doğrulandı (hepsi bulundu / bulunamadı olarak):

| İhtiyaç | Lucide | Material Symbols | Fluent | Tabler | Remix | Phosphor | Bootstrap | Feather |
|---|---|---|---|---|---|---|---|---|
| play | `play` | `play-arrow` | `play-24-regular` | `player-play` | `play-line` | `play` | `play` | `play` |
| pause | `pause` | `pause` | `pause-24-regular` | `player-pause` | `pause-line` | `pause` | `pause` | `pause` |
| rewind | `rewind` | — (`fast-rewind`) | `rewind-24-regular` | `rewind-backward-10` | `rewind-line` | `rewind` | `rewind` | `rewind` |
| fast-forward | `fast-forward` | `fast-forward` | `fast-forward-24-regular` | `rewind-forward-10` | `forward-15-line` | `fast-forward` | `fast-forward` | `fast-forward` |
| volume | `volume-2` | `volume-up` | `speaker-2-24-regular` | `volume` | `volume-up-line` | `speaker-high` | `volume-up` | `volume-2` |
| mute | `volume-x` | `volume-off` | `speaker-mute-24-regular` | `volume-3` | `volume-mute-line` | `speaker-x` | `volume-mute` | `volume-x` |
| speed | `gauge` | `speed` | `top-speed-24-regular` | `gauge` | `speed-line` | `gauge` | `speedometer` | — |
| fullscreen | `maximize` | `fullscreen` | `full-screen-maximize-24-regular` | `maximize` | `fullscreen-line` | `corners-out` | `fullscreen` | `maximize` |
| camera | `camera` | `photo-camera` | `camera-24-regular` | `capture` | `camera-line` | `camera` | `camera` | `camera` |
| record | `circle-dot` | `fiber-manual-record` | `record-24-regular` | `player-record` | `record-circle-line` | `record` | `record` | `circle` |

Sekiz kümenin sekizi de ihtiyacımız olan on simgenin tamamını karşılıyor; ad sözleşmeleri
farklı (Material `play-arrow`, Fluent `play-24-regular`, Remix `play-line`, Tabler
`player-play`), o yüzden dönüştürme betiğinde bir ad eşleme tablosu tutmak şart.

### 3.2 Avalonia `StreamGeometry`'ye Dönüşüm Kolaylığı

İncelenen ikon gövdelerinde (on medya simgesi × sekiz küme) `transform=`, `mask` veya
`clipPath` **hiç** görülmedi. Kullanılan yol komutları M, L, H, V, C, Q, A, Z ve bunların
küçük harfli göreli hâlleri — hepsi Avalonia'nın ayrıştırıcısında var.

- **Sorunsuz (tek `<path>`, dolgu tabanlı):** Material Symbols, Remix, Fluent, Phosphor.
  Gövde doğrudan `d` içeriği; `F1` ön eki ekleyip `x:Key` ile yapıştırmak yetiyor.
- **Ön işlem ister (`<rect>`, `<circle>`, `<g>`, çoklu path):** Lucide (`pause` iki `rect`,
  `camera` bir `circle` + path, `square` tek `rect`), Feather (`circle`), Tabler (`gauge`,
  `rewind-backward-10` gibi çok parçalılar), Bootstrap (`camera`, `rewind`).
  `<rect rx>` → iki/dört yay, `<circle>` → iki yay dönüşümü gerekir; SVGO'nun
  `convertShapeToPath` eklentisi bunu otomatik yapar.
- **Yay kullanımı:** Lucide'ın köşe yuvarlamaları `a2 2 0 0 1` biçiminde göreli yaylardır,
  Avalonia `A/a`'yı destekler, sorun çıkarmaz.

## 4. Avalonia'da SVG → `StreamGeometry` Pratiği

### 4.1 Mini Dilin Sınırları — Kaynaktan Doğrulanmış

`Avalonia.Base/Media/PathMarkupParser.cs` içindeki komut tablosu:

```
{ 'F', Command.FillRule },  { 'M', Command.Move },      { 'L', Command.Line },
{ 'H', Command.HorizontalLine }, { 'V', Command.VerticalLine },
{ 'Q', Command.QuadraticBezierCurve }, { 'T', Command.SmoothQuadraticBezierCurve },
{ 'C', Command.CubicBezierCurve },     { 'S', Command.SmoothCubicBezierCurve },
{ 'A', Command.Arc },       { 'Z', Command.Close }
```

Yani **F, M, L, H, V, Q, T, C, S, A, Z** — büyük/küçük harf, mutlak/göreli. İnternette
dolaşan "Avalonia S ve T desteklemiyor" bilgisi eski sürümlere ait; 12.x'te doğru değil.

Desteklenmeyen, ön işlemle çözülmesi gerekenler:

1. **`transform=` öznitelikleri** — `<g transform="translate(...) scale(...)">` bir geometri
   dizesinde ifade edilemez, koordinatlara pişirilmeli.
2. **`mask`, `clipPath`, `<use>`, gradyan, `fill-opacity`** — tek bir `StreamGeometry`
   bunları taşıyamaz; ya ikonu yeniden çizmek ya da `DrawingGroup` kullanmak gerekir.
3. **`<rect>`, `<circle>`, `<ellipse>`, `<line>`, `<polyline>`, `<polygon>`** — yola
   çevrilmeli.
4. **Farklı dolgulu çoklu `<path>`** — birleştirilirse renk bilgisi kaybolur.

### 4.2 Dolgu Kuralı Tuzağı (En Sık Yakalanan Hata)

`Avalonia.Skia/StreamGeometryImpl.cs` içinde varsayılan:

```
FillType = SKPathFillType.EvenOdd
```

SVG'nin varsayılanı ise `nonzero`. Delikli (counter'lı) her ikonda — kamera merceği,
halka, çerçeve içi boşluk — kaynak yolunu **`F1 ` ön ekiyle** yapıştırmak gerekir, yoksa
delik yanlış yerde açılır ya da hiç açılmaz. `F0` = EvenOdd, `F1` = NonZero, ve ön ek
yolun en başında olmalıdır.

### 4.3 Çizgi mi Dolgu mu

| | Çizgi (`Stroke` + `StrokeThickness`) | Dolgu (`Fill`) |
|---|---|---|
| Uygun setler | Lucide, Feather, Tabler outline | Material, Fluent, Remix, Phosphor, Bootstrap |
| Ölçeklemede kalınlık | Kalem ölçeklenmez, göreli ağırlık boyla değişir | Her boyda oran sabit |
| Köşe kontrolü | `StrokeJoin` yalnız kalınlık/2 yuvarlar | Yarıçap geometride, tam kontrol |
| Delik/counter | Yok | `F1`/`F0` ile |
| `PathIcon` uyumu | Hayır (`PathIcon` dolgu çizer) | Evet, `Foreground` doğrudan dolgu olur |

Kural: **kaynak küme hangi dille çizildiyse o dille kullan.** Lucide'ı `Fill` ile çizerseniz
açık şekiller (chevron, dalga) kapanıp lekeye döner; Material'ı `Stroke` ile çizerseniz her
şekil çift konturlu görünür.

Çizgi tabanlı kullanımda kalınlığın boyla birlikte değişmesi için, tek bir `IconStroke`
yerine boya bağlı belirteç gerekir; 24'lük geometri 16 px kutuya çizildiğinde tasarım
kalınlığı 2'yi korumak için `StrokeThickness = 2 × (16/24) = 1.333` olmalıdır.

### 4.4 Kullanım Kalıbı

Çizgi tabanlı (Lucide):

```xml
<Path Width="24" Height="24"
      Stretch="None"
      Data="{StaticResource IconPlay}"
      Stroke="{TemplateBinding Foreground}"
      StrokeThickness="{StaticResource IconStroke}"
      StrokeLineCap="Round"
      StrokeJoin="Round"/>
```

Dolgu tabanlı (Material / Fluent):

```xml
<PathIcon Width="20" Height="20"
          Data="{StaticResource IconPlayFilled}"
          Foreground="{TemplateBinding Foreground}"/>
```

`Stretch="None"` + sabit `Width/Height=24` kullanıldığında `M 0,0 M 24,24` sabitleyicisine
gerek kalmaz. Renk her iki durumda da denetimin `Foreground`'undan gelir; palet dosyası
dışında renk tanımlanmaz.

### 4.5 Dönüştürme Hattı

1. **Kaynak**: `npm i lucide-static` (SVG dosyaları) ya da doğrudan
   `https://api.iconify.design/<prefix>.json?icons=<ad1>,<ad2>` — JSON içinde `body` alanı
   normalize edilmiş SVG gövdesini verir, ek indirme gerekmez.
2. **Normalize**: SVGO `convertShapeToPath` (+`convertArcs`, `mergePaths`),
   `removeAttrs` ile `stroke`/`fill` özniteliklerini temizle, `applyTransforms` ile
   dönüşümleri pişir. Alternatif: Inkscape CLI `--actions="select-all;object-to-path"`.
3. **Ölçekle/kaydır**: Phosphor 256'lık ızgarada, Bootstrap 16'lık ızgarada; 24'e taşımak
   için Python `svgelements` ile `Path(d) * Matrix.scale(24/256)` yeterli, sonra `d()` ile
   geri yaz.
4. **Üret**: `x:Key="Icon<Ad>"` satırlarını betikle yazdır, delikli olanların başına `F1 `
   ekle. Betik `tools/` altında dursun, çıktısı doğrudan `Icons.axaml`.
5. **Doğrula**: Her yolu `PathGeometry.Parse` ile ayrıştıran ve sınır kutusunu 2–22 aralığı
   için sınayan bir test, bu belgedeki 2. bölümdeki hataların tekrarını engeller.

## 5. Somut Tavsiye

**Birinci: Lucide (ISC).** Dosya başlığımız zaten "Şekiller Lucide takımından" diyor, yani
tasarım dili tutarlı kalır ve mevcut `Path` + `Stroke` altyapısı (`StrokeLineCap="Round"`,
`StrokeJoin="Round"`) hiç değişmeden çalışır; 1.829 ikonla ihtiyacımız olan on medya
simgesinin tamamı adıyla mevcut, 24'lük ızgara ve 2 px kalınlık bizim tuvalimizle birebir
aynı, ISC lisansı MIT kadar serbest ve yalnız telif bildiriminin korunmasını istiyor; en
önemlisi, bugün elimizde eksik olan tek şey olan **2 birimlik geometrik köşe yarıçapı**
Lucide'ın zorunlu kuralı, yani seti alır almaz "hoş görünme" farkının en büyük kalemi
kapanıyor — bedeli sadece `<rect>`/`<circle>` içeren birkaç ikonu yola çevirmek ve
`IconStroke`'u 1.5'ten 2'ye taşımak.

**İkinci: Fluent UI System Icons (MIT).** Windows 11 tarafında yerli duran tek set bu;
12'den 48'e kadar her boy ayrı çizildiği için bizim "16 px kutuda kalem ölçeklenmiyor"
sorunumuzu tasarım düzeyinde ortadan kaldırıyor, 19.757 ikonla kapsam sorunu bırakmıyor,
tek `<path>` dolgu gövdeleri `PathIcon`'a doğrudan yapışıyor ve Regular/Filled ikilisi
"seçili sekme dolu, seçilmemiş sekme çizgi" gibi durum ayrımlarını bedavaya veriyor; bedeli
arayüzün çizgi dilinden dolgu diline geçmesi, yani `Controls.axaml`'daki bütün `Path`
şablonlarının `Stroke`'tan `Fill`'e çevrilmesi ve `IconStroke` belirtecinin anlamını
yitirmesi.

## Kaynaklar

- [Icons – Material Design 3](https://m3.material.io/styles/icons/designing-icons)
- [Icons - Style - Material Design 1](https://m1.material.io/style/icons.html) — 24 dp ızgara, 20 dp canlı alan, keyline şekilleri, 2 dp kalınlık, 2 dp köşe
- [Material Symbols guide](https://developers.google.com/fonts/docs/material_symbols) — opsz/wght/GRAD/FILL eksenleri
- [google/material-design-icons (Apache-2.0)](https://github.com/google/material-design-icons/blob/master/LICENSE)
- [Lucide icon design guide](https://lucide.dev/contribute/icon-design-guide) — 24×24 tuval, ≥1 px pay, 2 px kalınlık, round cap/join, 2 px köşe yarıçapı
- [lucide-icons/lucide (ISC)](https://github.com/lucide-icons/lucide/blob/main/LICENSE)
- [feathericons/feather (MIT)](https://github.com/feathericons/feather) — 24×24 ızgara, stroke-width 2, round cap/join
- [tabler/tabler-icons (MIT)](https://github.com/tabler/tabler-icons) — 24×24, 2 px çizgi
- [Remix-Design/RemixIcon (Apache-2.0)](https://github.com/Remix-Design/RemixIcon) — 24×24, Line/Fill
- [twbs/icons (MIT)](https://github.com/twbs/icons) — 16×16 ızgara, düz dolgu SVG
- [phosphor-icons/core (MIT)](https://github.com/phosphor-icons/core)
- [microsoft/fluentui-system-icons (MIT)](https://github.com/microsoft/fluentui-system-icons) — Regular/Filled, 12/16/20/24/28/32/48
- [Iconify koleksiyon kataloğu](https://api.iconify.design/collections) — ikon sayıları ve lisans alanları
- [Avalonia PathMarkupParser.cs](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Base/Media/PathMarkupParser.cs) — desteklenen komut tablosu
- [Avalonia StreamGeometryImpl.cs](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Skia/Avalonia.Skia/StreamGeometryImpl.cs) — varsayılan `SKPathFillType.EvenOdd`
- [Avalonia Shape.cs](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Controls/Shapes/Shape.cs) — `CalculateSizeAndTransform` ve kalem ölçeklenmemesi
- [Fluent 2 Iconography](https://fluent2.microsoft.design/iconography) — Regular/Filled temalar, boya özel çizim
