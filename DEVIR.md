# Devir notu — 2026-09-05

Bu dosya bir oturumu başka makineye taşımak için yazıldı. Laptopta `git pull` yapıp
"devam" dendiğinde buradan okunur. **İş bitince bu dosya `trash/`'a taşınır.**

---

## 1. Nerede kaldık

Auto modunun kodek ve çözünürlük kararlarını **ölçümle** kalibre ediyoruz. Hedef:
`docs/olcumler/ab-duzenegi.md` tablosundaki altı satırda HandBrake'i her koşulda geçmek.

Başlangıç tablosu (HandBrake vs bugünkü Auto, VMAF-NEG harmonik ortalama):

| satır | HandBrake | Auto | fark |
|---|---|---|---|
| parça-1 @3,50 | 48,56 | 45,97 | −2,59 |
| parça-1 @34,97 | 83,64 | 72,80 | −10,84 |
| parça-2 @3,50 | 93,73 | 82,25 | −11,48 |
| parça-2 @35,00 | 95,79 | 96,12 | +0,33 |
| parça-3 @3,50 | 17,88 | 22,29 | +4,41 |
| parça-3 @34,99 | 74,96 | 66,11 | −8,85 |

**Altıda dört kayıp.** Ölçüm kaydı `docs/olcumler/kodek-matris.md`, ham günlükler
`docs/olcumler/kodek-matris-ham/`, düzenek `tools/kodek-matris/`.

---

## 2. Ölçümle kurulmuş üç bulgu

### 2.1 Küçültme kaybettiriyor (tek değişken tutulduğunda)

Tüm kollar AV1 preset 4, VB'siz, 2 geçiş, aynı hedef.

parça-3 (zor), hedef 483k — **parite kusursuz**, bayt farkı %0,01:

| çözünürlük | teslim bps | bayt | harm |
|---|---|---|---|
| 882x496 | 483.785 | 3.653.588 | 26,01 |
| 1280x720 | 483.852 | 3.654.094 | **27,63** |

parça-2 (kolay), hedef 484k — parite yok, kırılması bulgunun kendisi:

| çözünürlük | teslim bps | bayt | harm |
|---|---|---|---|
| 882x496 | 333.430 | 2.517.404 | 60,13 |
| 1280x720 | 431.158 | 3.255.248 | 74,54 |
| 1920x1080 | 465.502 | 3.514.541 | **95,15** |

Küçültülen kol bütçeyi kullanamıyor: 882 hedefin %31 altında, 1280 %11, 1080p %4.

Fable'ın mekanizma açıklaması: piksel 4,7 kat azalınca kodlayıcı q'yu 4-17 bandına
indiriyor; orada 10-bit AV1 neredeyse saydam ve her ek q basamağı çok az bayt alıyor.
SVT VBR hedefi son bayta kadar kovalamıyor (`minsection-pct` varsayılan 0).

### 2.2 HandBrake'i geçen tek değişiklik: küçültmemek

`docs/olcumler/kodek-matris.md` bölüm B, parça-2 @3,50:

| kol | bayt | harm |
|---|---|---|
| HandBrake x265 1080p | 3.735.428 | 93,73 |
| **1080p, AV1, preset 6, VB AÇIK** | 3.580.091 | **94,68** |
| 1080p, AV1, preset 4, VB kapalı | 3.514.541 | 95,15 |

VB'yi kapatmak + preset 4 buna yalnız **+0,47** ekliyor — algı eşiğinin altında ve tek
başına ölçülmedi. Bu satır bir turda "iki değişiklik birlikte geçti" diye yanlış
raporlandı; Fable düzeltti.

### 2.3 variance-boost oran denetimini kırıyor

`FfmpegArguments.cs:532-533` `libsvtav1` için **koşulsuz** ekliyor:
`tune=0:enable-variance-boost=1:variance-boost-strength=2`.
Pimleyen testler `FfmpegArgumentsTests.cs:135` ve `:463`.

CRF 63 azami-q çıktısı, parça-3, aynı preset, tek değişken VB:

| küme | azami-q çıktısı (bps) | VBR@483k teslimi |
|---|---|---|
| preset 6, VB'siz | 407.489 | 421.051 |
| preset 6, VB'li | **1.025.695** | 914.682 |
| preset 4, VB'siz | 435.383 | 393.113 |

VB kümenin bit talebini **2,52 katına** çıkarıyor. VB'li kol VBR'da kendi azami-q
çıktısının %89'unu teslim etmiş — denetimin kolu bitmiş. Mekanizma: VB kare q'sunun
*altına* süper-blok düzeyinde eksi delta-q uyguluyor; denetim yalnız kare q'sunu
yönetir ve 63'te durur.

**Kusur uykuda** çünkü üretim küçültüyor (parça-3 Auto 882x496, parça-2 Auto 1650x928);
piksel azalınca hedef tutuyor. 1080p'ye geçiş kusuru uyandırır — yani 2.1'in düzeltmesi
2.3'ün düzeltmesi olmadan yapılamaz.

**Ek not:** `FfmpegArguments.cs:354-355` AV1'i VBV kelepçesinden muaf tutuyor
(`SupportsRateLimits`), yani aşıma fren yok. Gerekçe `docs/olcumler/bppf-tabani.md:244`
ve eski bir SVT-AV1 sürümüyle ölçülmüş — yeniden ölçülmeli.

---

## 3. Yanlış çıkan iddialar (tekrarlanmasın)

| iddia | neden düştü |
|---|---|
| "Sapma preset'e bağlı, preset 4/2 hedefi tutturuyor" | preset 4 + VB de 483k'da +%73,8 aştı |
| "Zor içerikte küçültmek doğru, +5,09" | iki değişken oynatılmıştı (kodek + çözünürlük) |
| "parça-3 Auto 652x366" | doğrusu **882x496**; parça-2 Auto **1650x928** |
| "CRF 63 mutlak tabandır" | preset 4'te VBR teslimi (393k) CRF63'ün (435k) altında |
| "Küçültme yok + VB yok birlikte HandBrake'i geçti" | küçültmemek tek başına geçiyor |
| "VB boyut serbestken +12,11 kazandırır" | boyut serbest değildi, 2,1 kat bayttı |
| "Preset farkı 0,5-0,8 ile sınırlı" | bir alt sınır, üst sınır ölçülmedi |
| "`-svtav1-params` şu anahtarı kabul etti" | SVT tanımadığı anahtarı **sessizce yutuyor** |

---

## 4. Fable'ın istediği, henüz koşulmamış ölçümler

Tam metin `docs/danisma/003-fable-taban-kucultme-vb.md`.

1. **Çözünürlük tavanı** (kodlamasız, ucuz) — `tools/kodek-matris/kos-tavan.sh`.
   Kaynak → lanczos indir → geri 1080'e çık → VMAF. Hiçbir bütçenin aşamayacağı üst
   sınır. Beş kol: parça-2 @882/1280/1650, parça-3 @882/1280.
   **Bu koşum bu oturumda başlatıldı, bitmedi.** Laptopta baştan koşulacak.
2. **VB yalıtımı sabit boyutta** — `kos-kucultme.sh`'nin p3_882 kolu, yalnız
   `enable-variance-boost=1:variance-boost-strength=2` eklenerek. ~1 dk + VMAF.
   Yanına **CAMBI** (`--feature cambi`): VMAF bantlaşmaya kör, VB tam olarak bitleri
   dokudan düz alanlara taşıyor. CAMBI olmadan VB kalite kararı verilmemeli.
3. **1080p'nin 483k'daki noktası** — eş baytlı 1080p/720p kıyası hiç yapılmadı.
4. **Merdiven** — Fable CRF üstüne kurulmasını istiyor; VBR bu turda −%31 ile +%89
   arasında saptı, yani VBR merdiveni kodeği değil denetim hatasını ölçer.
   Klip başına ~24 kodlama: AV1 1080p × 6 CRF (63/59/55/51/45/39), x265 1080p × 6,
   SDR'de x264 × 1, küçültme kolu ≤9, VBR doğrulama 2.
   Eksen `teslim_bppf / ReferenceBppf` olarak normalize edilir — SDR/HDR, küçük/büyük
   hedef aynı eksende okunur.

### Kaynak kümesi kusurlu

`.calisma/kaynak-genis/` altındaki dört yeni SDR klip (git'e girmez, laptopta yeniden
indirilecek):

| klip | bitrate | boyut/fps | sorun |
|---|---|---|---|
| genis-1 animasyon (BBB) | 5,3 Mbit | 1920x1080/60 | kabul |
| genis-2 gren (ToS) | 6,3 Mbit | 1920x800/24 | **gren sınıfını temsil etmiyor**, gren ezilmiş |
| genis-3 hareket (ToS) | 6,3 Mbit | 1920x800/24 | kabul |
| genis-4 gren2 (old_town_cross) | 162 Mbit | 1920x1080/50 | gren sınıfını tek başına taşıyor |

- **Dördünde de renk etiketi yok** — kodlarken `bt709` açıkça yazılmalı, yoksa A/B'nin
  renk kapısı düşürür.
- Eksik sınıflar: telefon çekimi (SDR 30 fps, el titremesi + düşük ışık gürültüsü),
  ekran kaydı / metin / arayüz, SDR oyun yakalama.
- 1920x800 klipler 16:9 değil — küçültme basamakları "yükseklik ölçeği" olarak
  tanımlanmalı, sabit 1280x720 değil.

### İki teknik not

- SVT 882'yi içeride **888'e dolguluyor**; 6 sütun boşa kodlanıyor. Basamaklar mod-16.
- ffmpeg ilerleme satırındaki `q=` **son paketin** qp'si, ortalama değil. Kare başına q
  için `ffprobe -show_entries packet=size,flags` kullan.

---

## 5. Yapılacaklar — kullanıcının verdiği liste

Sıra kullanıcının yazdığı sıradır.

### 5.1 Statik/dinamik kodek seçimi (yeni özellik)

Varsayılan dinamik ayarlama kalır. Kullanıcı isterse **bir tiki kapatıp** statik
seçebilmeli: "AV1" ya da "x264" der, **kalan değerlerin en iyisi** motor tarafından
seçilir. Yani kodek kilitlenir, geri kalan karar (bitrate, preset, çözünürlük) yine
otomatik.

Dokunulacak yerler:

- `src/VidShrink.Core/EncodePlan.cs:9` — `enum CodecPreference { Compatible, MaxCompression, Fast, Auto }`
- `src/VidShrink.Core/CompressionStrategy.cs:58` — `AutoPreference(regime)`
- `src/VidShrink.Core/PlanCalculator.cs:181` — `options.Codec == CodecPreference.Auto` dalı
- `src/VidShrink.App/MainWindow.axaml` + `.axaml.cs` — tik ve seçim arayüzü
- `MainWindow.axaml.cs:1513` — WhatsApp yolu `CodecPreference.Auto` kullanıyor ve
  **yalnız Auto şu an x264'e düştüğü için** çalışıyor. Auto'nun varsayılanı değişirse
  bu satır kırılır; birlikte ele alınmalı.

Renk/ölçü belirteci gerekirse **uydurma** — `teknesyum-ui` kurulu değil, kullanıcıya sor.

### 5.2 Önizleme paneli — baştan başlama kusuru

Belirti: oynatırken durdur/başlat yapılınca, ayar değişmediği halde en baştan işleme
giriyor. Yordam: belirtiyi susturma, nedeni bul, testle sabitle, testin düzeltme geri
alındığında **kaldığını** doğrula (mutasyon denetimi).

Bakılacak yerler:

- `src/VidShrink.App/Playback/PanelHost.cs` (40 KB — durum makinesi burada)
- `src/VidShrink.App/Playback/SegmentEncoder.cs`
- `src/VidShrink.App/Playback/ComparisonPanel.axaml.cs`
- `src/VidShrink.Ffmpeg/Playback/PipeComparisonFrameSource.cs`

Muhtemel yön: durdur/başlat, önbelleğe alınmış parçayı yeniden kullanmak yerine segment
kodlamasını yeniden tetikliyor. Önce **ölç** — hangi çağrı tekrar ediyor.

### 5.3 README yenilemesi

- Ekran görüntüleri çok eski; güncel arayüzle yeniden alınacak.
- Biçim örneği: **teknesyum-core'un README'si** — bol görsel, düzgün şekiller.
- **Türkçesi de olacak**, teknesyum-core'daki gibi ayrı dosyada.
- Depo dokümanı İngilizce yazılır (`RULES.md`); Türkçe sürüm ayrı dosyada durur.
- Başlıklar ve afişler Title Case.

Ekran görüntüsü almak için ekran kapısı gerekir: `/ekran <dakika>`.

### 5.4 Push

Her şey bitince ana depoda en güncel sürüm dursun.

---

## 6. Kalıcı kurallar (bu projede kanla yazıldı)

- **Tek değişken.** İki şeyi birden oynatan kıyas hiçbir şey ölçmez. Bu oturumda üç kez oldu.
- **Boyut paritesi ±%2.** Bandın dışındaki satır kıyaslanabilir değil; kırıldıysa yönü söylenir.
- **ffmpeg sıralı koşar.** İki eşzamanlı kodlama hem süreyi hem kaliteyi bozar.
- **Gelen düzeltme de bir iddiadır.** Fable'ın "senin sayın yanlış"ı da doğrulanmadan
  kabul edilmez — bu oturumda Fable iki kez haklı, iki kez haksız çıktı.
- **Geri çekilen iddia dosyadan da çekilir.** Rapor düzeltilir, tablo öyle bırakılmaz.
- **Parametre kabul sınaması negatif kontrol ister.** SVT tanımadığı anahtarı susarak geçer.
- **`.calisma/` git'e girmez.** Rapora giren sayı `docs/`e, düzenek `tools/`a taşınır.
- **`main`e yalnız T0 birleştirir.** Kendi dalında çalış.
- Kod yorumu yazma. Cevaplar kısa. Türkçe konuş.

---

## 7. Laptopta ilk adımlar

1. `git pull`
2. Bu dosyayı ve `docs/olcumler/kodek-matris.md`'yi oku.
3. Ölçüme devam edilecekse kaynakları yerleştir: `.calisma/kaynak/` (parça-1/2/3, HDR)
   ve `.calisma/kaynak-genis/` (dört SDR klip). İkisi de git'te yok.
4. Kullanıcı "devam" dediğinde sıra **§5**'tir; ölçüm ve mükemmelleştirme sonraya kaldı.
