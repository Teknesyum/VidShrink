# D3 Çizelge Ölçümleri — 028 Kabul 1, 4 Ve 5

Tarih: 28 Eylül 2026. Makine: geliştirme makinesi (Windows 11, yerel), Release derlemesi,
gerçek libmpv (`tools\libmpv\libmpv-2.dll`). Kaynak:
`tests/VidShrink.Tests/DuzenleyiciOlcumTests.cs`. Koşum:

```
dotnet test tests/VidShrink.Tests -c Release --no-build --filter "FullyQualifiedName~DuzenleyiciOlcum"
```

Üç test de yeşil. Kabul 1 ve 5 `[HedefMakineFact]`: CI'da atlanır, yerelde sessiz makinede koşar.
Kabul 4 `[Fact]`, her yerde koşar.

## Kabul 1 — Çizim Süresi Kaynak Uzunluğuna Bağlı Değil (±%20)

Tek başına `EditorTimeline`, 1136 px. Her adımda görünüm başı rastgele bir klip sınırına kayar,
ardından yerleşim yapılır ve `RenderTargetBitmap.Render` çağrılır. Süre bu üçünün toplamıdır.
20 ısınma ve 150 ölçüm alındı; kollar sırayla serpiştirildi.

| Kol | Medyan | p95 | En çok kurulan klip | Oran |
|---|---|---|---|---|
| 1 dk, 10 klip × 6 sn, 60 sn görünüm | 0,237 ms | 0,432 ms | 10 | 1 |
| 60 dk, 600 klip × 6 sn, 60 sn görünüm | 0,230 ms | 0,445 ms | 12 | 0,967 |
| 60 dk, 10 klip × 6 dk, tamamı görünür | 0,249 ms | 0,450 ms | 10 | 1,049 |

Sonuç: fark ±%5 içinde, şart ±%20. 600 klipli modelde en çok 12 klip kuruldu, 600 değil;
test 10–12 aralığını şart koşar. Fazladan iki klibin nedeni ölçülmedi.

## Kabul 4 — Bırakışta Oynatma Başı Motorla Aynı Karede (≤1 Kare)

Kaynak 20 sn, 640x360, 30 fps, `-g 30`. Model 5 sn ve 12 sn'de kesildi, ortadaki parça silindi.
Beş hedef çizelge süresinin 0,13 / 0,37 / 0,52 / 0,71 / 0,94'üne düştü. Her hedefte:

1. Sürükleme taklidi: `ScrubTo(x - 6, false)`, `ScrubTo(x, false)`, ardından bırakış `ScrubTo(x, true)`.
   Bırakış anındaki `Playhead` hedef alındı.
2. `EdlPreviewDriver.TimelinePosition` 300 ms değişmeyene dek beklendi (en çok 5 sn).

`TimelinePosition` motorun `PositionSeconds` değerinden türer, sürücünün istediği değer değildir.

| Bırakış | Motor | Fark |
|---|---|---|
| 1,700 sn | 1,700 sn | 0 kare |
| 4,800 sn | 4,800 sn | 0 kare |
| 6,767 sn | 6,767 sn | 0 kare |
| 9,233 sn | 9,233 sn | 0 kare |
| 12,233 sn | 12,233 sn | 0 kare |

## Kabul 5 — Düzenlemeden Sonra İlk Kare ≤ 1 sn (10 dk 1080p, 5 Tekrar Medyanı)

Kaynak 10 sn'lik 1920x1080 30 fps libx264 parçasından (`ultrafast`, `-crf 30`, `-g 60`)
yapıldı. Parça concat demuxer ve `-c copy` ile 60 kez birleştirildi; sonuç 10 dakikalık tek dosya.

Süre, işlem çağrısından `PlayerView.DrawnFrames`'in artmasına kadar ölçüldü. Bu sayaç motorun kare
geri çağrısında artar. Her ölçümden önce 0,5 sn beklendi ve her işlemin tam bir önizleme yeniden
yüklemesi yaptığı (`Reloads` +1) doğrulandı. Önizleme duraklatılmış haldeydi.

| İşlem | Medyan | En çok | Tekrarlar (ms) |
|---|---|---|---|
| Böl (ortadan) | 31,2 ms | 61,6 ms | 61,6 · 31,2 · 31,6 · 30,9 · 30,2 |
| Taşı (0 → 1) | 31,3 ms | 32,1 ms | 31,3 · 31,1 · 32,1 · 30,8 · 31,6 |
| Hız (2× / 1,5×) | 31,5 ms | 62,5 ms | 31,5 · 30,9 · 62,5 · 30,4 · 45,9 |
| Sil (ilk klip) | 30,8 ms | 41,2 ms | 41,2 · 32,2 · 30,7 · 30,5 · 30,8 |

Her işlemin en kötü tekrarı bile 1 sn şartının çok altında.

## Açık Kalanlar

- **Kabul 5'te çizelge küçülüyor.** Beş tekrar aynı modelde art arda koştu. Her turdaki silme ve
  hız işlemi çizelgeyi kısalttı: ilk tur 10 dakikalık çizelgede, son tur 18,75 sn'lik çizelgede
  ölçüldü. Kaynak dosya her turda 10 dakikalık 1080p dosyaydı; arama o dosyanın içine yapıldı.
- **Kaynak, gerçek çekim değil.** `testsrc2` ile üretilen yapay bir görüntü. Anahtar kare aralığı
  2 sn. Gerçek içerikle (Sintel, 10 sn GOP ve kayıpsız FFV1) tekrarı aşağıda, "Gerçek Kaynakla
  Tekrar" bölümünde. Kamera çekimi, sesli kaynak, HEVC ve 2160p hâlâ ölçülmedi.
- **Kabul 1 CPU süresini ölçüyor.** `RenderTargetBitmap` yazılım çizimidir; ekrana basış
  (GPU birleştirme, vsync) ölçüye girmiyor.
- **Tek makine, tek koşum.** Sayılar yük altında okunmamalı.
## Gerçek Kaynakla Tekrar (2026-10-05)

Yukarıdaki kabul 4 ve kabul 5 sayıları `testsrc2` (ffmpeg'in ürettiği yapay desen) ile alınmıştı.
Burada aynı iki test, aynı makinede, gerçek içerikli 10,5 dakikalık iki dosyayla yeniden koştu.
Kabul 1 tekrar edilmedi: o ölçü dosya açmıyor, klip listesini bellekte kuruyor; kaynağın yapay ya
da gerçek olması ona girmiyor.

### Kaynaklar

Yerelde gerçek içerik olarak yalnız `.calisma/nvenc-2` altındaki üç Sintel kesiti var (her biri
10 sn, 1920x818, 24 fps, kayıpsız FFV1, sessiz). `.calisma/oynatici-motor` altındaki
`h264_2160p30.mp4`, `hevc_1080p60.mp4` ve `h264_1080p60.mp4` gerçek değil: `OynaticiMotorTests.cs`
içindeki `MotorKlipleri` onları `testsrc2` ile üretiyor. Bu yüzden kullanılmadılar.

İki dosyayı `tools/d3-gercek-kaynak/hazirla.ps1` üretir (ffmpeg 9.0 gyan.dev, her çağrı `-threads 2`):

| Kol | Nasıl yapıldı | Çözünürlük | Bit hızı | Anahtar kare aralığı | Süre | Boyut |
|---|---|---|---|---|---|---|
| H.264 uzun GOP | üç kesit art arda, 1080'e siyah bantla tamamlandı, libx264 `veryfast -crf 16 -g 240`; 30 sn'lik parça 21 kez `-c copy` | 1920x1080 | 8,61 Mbit/sn | 10 sn | 630 sn | 678 MB |
| FFV1 kayıpsız | üç kesit 21 kez `-c copy`, yeniden kodlama yok | 1920x818 | 87,1 Mbit/sn | her kare | 630 sn | 6,86 GB |

Parça sayısı bilerek tek (21): kabul 5'in ilk bölmesi çizelgenin ortasına, 315. saniyeye düşer ve
bu nokta H.264 kolunda son anahtar kareden 5 sn (120 kare) uzaktadır. Sentetik kaynakta aynı bölme
300. saniyeye, yani tam bir anahtar kareye düşüyordu.

### Koşum

```
dotnet build tests/VidShrink.Tests -c Release -m:2 -warnaserror
$env:VIDSHRINK_LIBMPV = '<kök>\tools\libmpv\libmpv-2.dll'
$env:VIDSHRINK_D3_KISA_KAYNAK = '<dosya>'
$env:VIDSHRINK_D3_UZUN_KAYNAK = '<dosya>'
dotnet test tests/VidShrink.Tests -c Release --no-build --filter "FullyQualifiedName~DuzenleyiciOlcumTests.BirakistanSonra|FullyQualifiedName~DuzenleyiciOlcumTests.DuzenlemedenSonra"
```

İki değişken bu turda eklendi. Verilmezse testler eskisi gibi kendi `testsrc2` kaynağını üretir;
verilirse o dosyayı açar ve dökümü `kabul-4-birakis-<ad>.txt` / `kabul-5-ilk-kare-<ad>.txt` adıyla
yazar. Kabul 4'te kare süresi artık sabit 30 değil, çizelgenin dosyadan okuduğu kare hızı.

Üç koşum art arda, tek tek yapıldı: değişkensiz (sentetik, bugünkü derleme), H.264 kolu, FFV1 kolu.
Üçü de yeşil, her biri yaklaşık 25 sn. İki testte her koşumda aynı dosya kullanıldı.

### Kabul 4 — Bırakışta Oynatma Başı Motorla Aynı Karede (≤1 Kare)

Sentetik kaynak 20 sn'ydi; gerçek kollarda 630 sn'lik dosya açıldı, 5 ve 12. saniyede kesilip orta
parça silindi (çizelge 623 sn). Beş bırakış yine çizelgenin 0,13 / 0,37 / 0,52 / 0,71 / 0,94'ünde.

| Bırakış | Sentetik (28 Eylül) | Sentetik (5 Ekim) | H.264 uzun GOP | FFV1 kayıpsız |
|---|---|---|---|---|
| 1. (0,13) | 0 kare | 0 kare | 81,000 sn → 0 kare | 81,000 sn → 0 kare |
| 2. (0,37) | 0 kare | 0 kare | 230,500 sn → 0 kare | 230,500 sn → 0 kare |
| 3. (0,52) | 0 kare | 0 kare | 323,958 sn → 0 kare | 323,958 sn → 0,008 kare |
| 4. (0,71) | 0 kare | 0 kare | 442,333 sn → 0 kare | 442,333 sn → 0,008 kare |
| 5. (0,94) | 0 kare | 0 kare | 585,625 sn → 0 kare | 585,625 sn → 0 kare |
| En büyük fark | 0 kare | 0 kare | 0 kare | 0,008 kare |

Fark: sentetikte 0, gerçekte en çok 0,008 kare (FFV1, iki bırakış). Şart 1 kare.

### Kabul 5 — Düzenlemeden Sonra İlk Kare ≤ 1 sn (5 Tekrar Medyanı)

Medyan, ms. "Fark" sütunları gerçek kol eksi aynı gün ölçülen sentetik (5 Ekim).

| İşlem | Sentetik (28 Eylül) | Sentetik (5 Ekim) | H.264 uzun GOP | Fark | FFV1 kayıpsız | Fark |
|---|---|---|---|---|---|---|
| Böl (ortadan) | 31,2 | 31,4 | 38,7 | +7,2 | 46,2 | +14,8 |
| Taşı (0 → 1) | 31,3 | 42,0 | 141,0 | +99,0 | 61,2 | +19,3 |
| Hız (2× / 1,5×) | 31,5 | 42,2 | 139,5 | +97,2 | 61,6 | +19,4 |
| Sil (ilk klip) | 30,8 | 46,4 | 39,1 | −7,3 | 46,3 | −0,1 |

En kötü tekrar, ms:

| İşlem | Sentetik (28 Eylül) | Sentetik (5 Ekim) | H.264 uzun GOP | FFV1 kayıpsız |
|---|---|---|---|---|
| Böl | 61,6 | 63,1 | 47,6 | 47,6 |
| Taşı | 32,1 | 46,3 | 185,1 | 78,4 |
| Hız | 62,5 | 46,1 | 189,8 | 77,7 |
| Sil | 41,2 | 47,3 | 47,4 | 46,8 |

Ham döküm (testin yazdığı dosyalar, sırayla sentetik 5 Ekim, H.264, FFV1):

```
kabul 5: 10 dk 1920x1080 h264 testsrc2 (10 sn parca x 60, -c copy), 30 fps, ilk cizelge suresi 600 sn, gercek libmpv, islem cagrisindan PlayerView.DrawnFrames artisina
bol: medyan 31.434 ms, en cok 63.063 ms, tekrarlar 63.063 31.434 45.847 30.925 30.41
tasi: medyan 41.95 ms, en cok 46.264 ms, tekrarlar 41.95 31.41 41.949 44.088 46.264
hiz: medyan 42.226 ms, en cok 46.091 ms, tekrarlar 45.802 31.225 41.461 42.226 46.091
sil: medyan 46.377 ms, en cok 47.316 ms, tekrarlar 39.447 46.169 47.316 46.473 46.377
son cizelge suresi 18.75 sn

kabul 5: gercek kaynak sintel-1080p-h264-10dk.mp4, 24 fps, ilk cizelge suresi 630 sn, gercek libmpv, islem cagrisindan PlayerView.DrawnFrames artisina
bol: medyan 38.662 ms, en cok 47.584 ms, tekrarlar 38.662 47.584 33.185 46.033 32.131
tasi: medyan 140.971 ms, en cok 185.135 ms, tekrarlar 117.825 96.316 185.135 140.971 183.287
hiz: medyan 139.471 ms, en cok 189.758 ms, tekrarlar 115.227 92.683 171.207 139.471 189.758
sil: medyan 39.052 ms, en cok 47.411 ms, tekrarlar 32.238 47.411 46.272 39.052 23.745
son cizelge suresi 19.688 sn

kabul 5: gercek kaynak sintel-818p-ffv1-10dk.mkv, 24 fps, ilk cizelge suresi 629.999 sn, gercek libmpv, islem cagrisindan PlayerView.DrawnFrames artisina
bol: medyan 46.205 ms, en cok 47.552 ms, tekrarlar 46.878 46.205 47.552 46.036 45.915
tasi: medyan 61.204 ms, en cok 78.366 ms, tekrarlar 55.018 46.895 61.204 78.366 64.239
hiz: medyan 61.596 ms, en cok 77.7 ms, tekrarlar 45.226 46.415 61.596 77.7 63.88
sil: medyan 46.256 ms, en cok 46.813 ms, tekrarlar 46.256 46.813 45.142 46.369 46.013
son cizelge suresi 19.687 sn
```

### Hüküm

**Değişmedi: kabul 4 ve kabul 5 gerçek kaynakla da geçiyor.** Koda dokunulmadı.

Tabloyla tek tek:

- Kabul 4: beş bırakışın en büyük farkı H.264 kolunda 0 kare, FFV1 kolunda 0,008 kare. Şart 1 kare.
- Kabul 5: en büyük medyan 141,0 ms (H.264, Taşı); 1 sn şartının %14,1'i. En kötü tek tekrar
  189,8 ms (H.264, Hız); şartın %19,0'ı.

Değişen şey sayının büyüklüğü, hüküm değil:

- **Taşı ve Hız uzun GOP'ta yaklaşık 3,3 kat yavaş.** H.264 kolunda Taşı 141,0 ms, Hız 139,5 ms;
  aynı gün sentetikte 42,0 ve 42,2 ms (141,0 / 42,0 = 3,36; 139,5 / 42,2 = 3,30). Böl ve Sil aynı
  kolda sentetiğin ±8 ms yakınında kaldı (+7,2 ve −7,3). Taşı ile Hız'ın neden ayrıştığı ölçülmedi.
- **Kayıpsız FFV1 uzun GOP'tan hızlı.** 87,1 Mbit/sn'ye rağmen dört medyan 46,2–61,6 ms aralığında.
  Her karesi anahtar kare; bu kol bit hızının tek başına süreyi büyütmediğini gösteriyor, ama
  1920x818 olduğu için "10 dk 1080p" şartının kendisi değil, yan kol.
- **Sentetik sayı da günden güne oynuyor.** 28 Eylül'de dört medyan 30,8–31,5 ms'ti;
  5 Ekim'de aynı testte 31,4–46,4 ms. Tek koşum, beş tekrar; iki gün arasındaki fark derlemeden mi
  makineden mi, ayrılmadı. Bu yüzden gerçek kollar 28 Eylül sütunuyla değil 5 Ekim sütunuyla kıyaslandı.

### Bu Tekrarın Kapsamadıkları

- **Kamera çekimi değil.** Sintel bilgisayarda üretilmiş bir film; yerelde kamera dosyası yok ve
  indirme yasak. H.264 kolu kayıpsız kesitten bir kez yeniden kodlandı (GOP 10 sn, 8,61 Mbit/sn).
- **Ses yok.** İki kaynak da sessiz; açılışta arka planda koşan ses dalgası okuması bu ölçümde
  çalışmadı. Sesli uzun dosyada ilk kare süresi ölçülmedi.
- **HEVC ve 2160p ölçülmedi.** Yereldeki HEVC ve 2160p dosyaları `testsrc2`.
- **Çizelge yine küçülüyor.** Beş tur aynı modelde art arda koştu: 630 sn'den 19,7 sn'ye.
- **Tek makine, kol başına tek koşum, beş tekrar.**
