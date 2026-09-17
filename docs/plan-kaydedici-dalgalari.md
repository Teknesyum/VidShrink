# Plan — Kaydedici: OBS/Bandicam Düzeyi Yetenek + TinyTask Ölçüsünde Mini Kip

Girdi: kullanıcının 15 Eylül 2026 turu, 3. iş.

> recorder sekmesi hiç istediğim komplekslik sunmuyor istediğim kullanışlılık sunmuyor
> bandicamda obs te ne yapabiliyorsak burda da aynısını istiyorum kapsamlı araştır 50+
> repo incele kapsamlı plan yap recorder penceresini düzeltelim tinytask benzeri küçük
> bir arayüze küçülebilecek ve tinytask kadar kullanışlı şekilde ordan ayarlarımızı
> yapacağız kullanıcı dostu değiliz olalım

## Araştırmanın kaynağı

Üç bağımsız tarama, toplam **69 proje**, hepsi kaynak okunarak:

- [`docs/arastirma/kaydedici-saha-2026-09-15.md`](arastirma/kaydedici-saha-2026-09-15.md)
  — 29 proje, yetenek matrisi, "herkeste var" ve "ayırt edici" listeleri.
- [`docs/arastirma/kaydedici-mini-arayuz-2026-09-15.md`](arastirma/kaydedici-mini-arayuz-2026-09-15.md)
  — 31 araç, 22'sinin kompakt kip ölçüsü kaynaktan okundu.
- [`docs/arastirma/kaydedici-kucuk-denetim-seridi-2026-09-15.md`](arastirma/kaydedici-kucuk-denetim-seridi-2026-09-15.md)
  — 9 proje, kayıt sırasındaki denetim şeridinin piksel ölçüleri.

Önceki turlardan duran iki rapor da bu planın girdisi:
[`kaydedici-kullanislilik.md`](arastirma/kaydedici-kullanislilik.md),
[`kaydedici-otomatik-ayar-ve-hedef-boyut.md`](arastirma/kaydedici-otomatik-ayar-ve-hedef-boyut.md).

## Elimizde ne var, ne yok

Motor sanılandan güçlü. `src/VidShrink.Core/RecorderArguments.cs` bugün şunları
üretiyor: üç platformun yakalaması (gdigrab/avfoundation/x11grab), bölge kırpma,
çoklu monitör ofseti, kap seçimi (mp4/mkv), kodek + ön ayar, kalite ya da bit hızı
kolu, `-g`/`-profile:v`/`-tune`, ölçekleme, `-pix_fmt`/`-colorspace`/`-color_range`,
süre sınırı, bölme, ayrı ses izleri (`-c:a:N`), iki girdide `amix`, tek kare
`BuildSnapshot`. `RecorderAutoPlan` donanım kodlayıcı merdivenini deniyor,
`RecorderBudget` hedef boyutu bit hızına çeviriyor.

Eksik olan motor değil, **yüzey ve refleks**. Kullanıcının cümlesi de zaten
"komplekslik" ve "kullanışlılık" diyor — ikisi ayrı iş.

Bugünkü şerit `RecorderView.axaml:20-99`, `Border x:Name="Strip"`. Yüksekliği
40 + 2×8 = **56 px** — saha aralığının (30–64) içinde. Genişliği ~**700 px** — dışında.
Küçültülecek eksen genişlik.

---

## A dalgası — Mini kip (TinyTask ölçüsü)

Kullanıcının asıl istediği bu. Önce bu.

### A1. `RecorderMini` penceresi

Ayrı bir `Window`, `SystemDecorations="None"`, `CanResize="False"`,
`ShowInTaskbar="False"`. İçeriği bugünkü `Strip`'in daraltılmış hali.

Yerleşim (mini-arayüz raporu §5.2'nin toplamı):

| Öğe | Genişlik | Kaynak |
|---|---|---|
| Canlı nokta | `RecorderDotSize` = 12 | mevcut |
| Sayaç `00:00:00` | `MonoValue` teması ölçülerek | **ölçülecek**, uydurulmayacak |
| Başlat / Duraklat (tek sütun) | `RecorderButtonSize` = 40 | mevcut |
| Durdur | 40 | mevcut |
| Büyüt | `IconSizeSm` | mevcut belirteç |

Yükseklik `RecorderMiniHeight` = 56, bugünkü şeritle aynı — yeni sayı gerekmiyor.
`RecorderMiniMinWidth` sayaç ölçüldükten sonra `SpaceMd` katına yuvarlanır;
rapor ~254 px tahmin ediyor, **kesin değer ölçümden çıkacak**.

### A2. Geçiş düğmesi

Kaydedici sekmesindeki şeride bir "küçült" düğmesi. Tıkla-geç, tıkla-dön; ikon ve
ipucu yön değiştiriyor. Ayarlar kutusuna gömülmeyecek — ScreenToGif'in bulunamayan
onay kutusu hatası.

### A3. Kayıt sırasında `Topmost`

LICEcap modeli: kayıt başlarken `Topmost = true`, durunca `false`. Kayıt yokken
sürekli üstte duran pencere sinir bozucu.

### A4. Sayaç = sürükleme tutamağı

`Cursor="SizeAll"`, `PointerPressed` → `BeginMoveDrag`. Ayrı tutamak sütunu 24 px
yer alır, sayaç zaten orada.

### A5. Kadrajın dışına konumlanma

Kritik. gdigrab bölgeyi yakalarken mini pencere kadrajın içine düşerse kayda karışır.
Varsayılan konum seçili bölgenin dışı; bölge tam ekransa kullanıcıya bunun kayda
gireceği söylenir.

### A6. Sıcak tuşlar

**F7** başlat/duraklat, **F8** durdur. ScreenToGif ve TinyTask'ın varsayılanı;
Game Bar'ın Win+Alt+R'siyle çakışmıyor. Tanım büyük pencerede kalır, tuş mini kipte
de çalışır.

### A7. Kayıt bitince büyük pencereye dön

Mini kip kapanır, `ResultPanel` açılır. Kap, Fluent ve ShareX üçü de böyle yapıyor.

### Mini kipte **olmayacaklar**

Kare ve düşen kare okumaları, hedef seçimi, kodek/ön ayar/FPS/kalite listeleri,
ses aygıtı seçimi, çıktı klasörü, hedef boyut bütçesi, `BtnAutoMeasure`, sonuç paneli.
Hepsi kayıt **öncesi** kararlar; kayıt başladıktan sonra zaten değişemiyorlar.

---

## B dalgası — "Herkeste var" boşlukları

Saha raporu §3: bu şeyler yoksa ürün eksik sayılıyor.

| Yetenek | Durum | Kanıt testi |
|---|---|---|
| Bölge seçimi | var | `KaydediciSeciciTests.FareyleCizilenBolgeAyaraVeArgumanaGecer` |
| Pencere seçimi | var — Windows başlık, Linux `-window_id` (Wayland açıkça reddedilir), macOS pencere dikdörtgeni ekran kırpmasına çevrilir | `KaydediciPencereTests.SeciciLinuxtaKimlikVeEkranMacteKirpmaYazarWaylandiReddeder`; Linux canlı kol `X11PenceresiListedenBulunupIkiSaniyeKaydedilir` (CI Xvfb); macOS yalnız argüman testi |
| Mikrofon + sistem sesi ayrı | var (`amix`, ayrı izler) | `SesliKayitTests.IkiCihazSecilinceGrafikVeEslemArgumandaDurur`, `KayitFfmpegKoluTests.AyriIzlerAmixYerineIkiMapVerir` |
| Çoklu kap/kodek | var | `KayitFfmpegKoluTests` |
| Sıcak tuşla başlat/durdur | var — **A6** | `KaydediciArayuzTests.GenelKisayolBasilincaEylemCalisir` |
| GIF çıktısı | var — **B1** | `KayitFfmpegKoluTests.GifKabiUzantisiniVerirVeMatroskayaYakalar` |
| Pencerenin kendini gizlemesi | var — mini kip ana pencereyi gizler, çerçeve kayda girmez | `KaydediciCerceveTests.TamEkranCercevesiKaydaGirmez`, `MiniKipTests.KadrajinDisinaKonumlaniyor` |

### B1. GIF çıktısı

14 projede var, artık varsayılan beklenti. Kaydedicinin kap listesine GIF eklenir;
palet üretimi (`palettegen`/`paletteuse`) motorda zaten olan filtre zincirine oturur.
Oynatıcı sekmesindeki GIF koluyla ortak kod.

---

## C dalgası — Ayırt edici yetenekler (OBS/Bandicam farkı)

Saha raporu §4'ten, maliyet/fayda sırasıyla. Bu dalganın sırası **kullanıcının**;
aşağıdaki sıra öneridir, karar değil.

### C1. Geri sarmalı tampon (Replay Buffer)

OBS'in ayırt edici tek özelliği. Son N saniyeyi bellekte tutar, sıcak tuşla diske yazar.
**Kaydı başlatmayı unutan kullanıcıyı kurtaran tek şey.** Motor tarafı: sürekli koşan
ffmpeg + halka tampon; `-f segment` ile parçalı yazım en ucuz yol.

### C2. Kayıt sırasında canlı önizleme

SSR'ın cümlesi: yanlış ayarla çekilmiş bir saatlik kaydı önleyen şey. Bizde
`IPlaybackEngine` ve karşılaştırma paneli zaten var; kayıt borusunun bir kolu oraya
bağlanır.

### C3. Tıklama ve imleç vurgusu

vokoscreenNG'nin Showclick + Halo'su, screenity'nin "highlight your clicks". Eğitim
videosunda ölçülür fayda.

### C4. Tuş vuruşu gösterimi

Yalnız Captura ve ScreenToGif'te var, maliyeti düşük.

### C5. Otomatik durdurma alarmı

`MaxDuration` motorda **zaten var**; eksik olan yalnız arayüz kutusu ve bitince
bildirim. En ucuz kalem.

### C6. Boşta kalan süreyi kırpma

asciinema'nın fikri. Uzun ve ölü anları olan ekran kayıtlarına doğrudan uyarlanabilir;
VidShrink'in sıkıştırma kimliğiyle en uyumlu ayırt edici özellik.

### C7. Webcam bindirmesi + arka plan ayırma

obs-backgroundremoval'ın kapsamı. En pahalı kalem; en sona.

### C8. Kaydederken yükle, durunca bağlantı

Cap'in Instant Mode'u. `Core/Share` altyapısı zaten duruyor.

---

## D dalgası — Kullanıcı dostuluğu

Kullanıcının "kullanıcı dostu değiliz olalım" cümlesi bu dalga.

### D1. Sihirbaz akışı + makul varsayılanlar

SSR'ın modeli: "no need to change anything if you don't want to". Kaydedici sekmesi
bugün bütün ayarları aynı anda gösteriyor. İki kademe: **Basit** (hedef + Başlat) ve
**Gelişmiş** (bugünkü her şey).

### D2. İki kip, iki hedef

Cap'in Instant/Studio ayrımı: "hızlı" ile "iyi" arasındaki gerilimi kullanıcıya açıkça
sorar. Bizde karşılığı zaten var — `RecorderAutoPlan` (otomatik) ve elle ayar — ama
kullanıcıya bir seçim olarak sunulmuyor.

### D3. Çökmeye dayanıklı varsayılan kap

OBS'in cümlesi: MKV düzgün durdurulmayan kaydı bozmaz. Motorda `RecorderContainer.Mkv`
var ve öldürülen kaydı yalnız Matroska'nın taşıdığı `KayitFfmpegKoluTests`'te pimli.
Eksik olan: **varsayılanın mp4 olması**. Uzun kayıtta varsayılan mkv olmalı, mp4'e
dönüştürme kayıttan sonra.

---

## Sıra ve ölçü

**A dalgası ilk.** Kullanıcının cümlesinin merkezi orası ve beş dosyadan azına dokunuyor:
`RecorderMini.axaml(.cs)`, `RecorderView.Serit.cs`, `Themes/Recorder.axaml`,
`RecorderView.axaml`.

B ve C dalgalarının sırasını kullanıcı seçer; C1 (replay buffer) ile C5 (alarm)
arasındaki maliyet farkı on kat.

Her dalga kendi pimini getirir: mini kipin ölçüleri belirteçten okunuyor mu,
`Topmost` yalnız kayıt sırasında mı, sıcak tuş tanımı tek yerde mi.
Ölçü uydurulmuyor — `RecorderMiniReadoutWidth` `MonoValue` temasının `00:00:00`
genişliği ölçülerek yazılacak.
