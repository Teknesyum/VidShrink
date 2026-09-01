# T90 — Çeviri ölçüsü mutasyonları

Durum: **tamamlandı — 2026-09-01.**

## C# interpolasyon sabit parçası

`ControlStrip.axaml.cs` içindeki zaman göstergesi interpolasyonuna geçici olarak `Embedded mutation text` sabit parçası eklendi. `LanguageTests.ArkaKoddaCumleKalmadi` kırmızı oldu ve şu ihlali verdi:

`Playback\ControlStrip.axaml.cs içinde anahtardan gelmeyen cümle var: Embedded mutation text /`

Mutasyon geri alındı. Bu sonuç, taramanın interpolasyonun yalnız ifadelerini değil kullanıcıya çıkan sabit parçalarını da gördüğünü kanıtlıyor.

## XAML öğe gövdesi

`MainWindow.axaml` içindeki `TaglineLead` geçici olarak öznitelik bağından çıkarılıp `<TextBlock>Embedded body mutation</TextBlock>` biçiminde düz gövde metnine çevrildi. `LanguageTests.BicimlemedeKullaniciyaGorunenDuzMetinKalmadi` kırmızı oldu ve şu ihlali verdi:

`MainWindow.axaml içinde anahtardan gelmeyen metin var: >Embedded body mutation<`

Mutasyon geri alındı. Ölçü artık hem kullanıcı metni taşıyan XAML özniteliklerini hem öğe gövdelerini tarıyor.

## Tanınmayan girdinin kararı

`LanguageCatalog.Validation` tanınmayan motor iletisini artık sessizce İngilizce göstermiyor; `main.validation.untranslated-engine` anahtarıyla açıkça etiketliyor. Aşama satırındaki tanınmayan motor/ffmpeg metni ise teşhis değerini kaybetmemesi için ham biçimde korunuyor; `TaninmayanAsamaHamMotorMetniOlarakKorunur` testi bu bilinçli düşürme yolunu sabitliyor.

## Doğrulama

Sözleşme filtresi:

`LocalizationTests|LanguageTests|PlaybackTests: 37 başarılı, 0 başarısız`

İlk tam süit, T90 kapsamı dışındaki ve T86 tur 2 sözleşmesinde açıkça kayıtlı süreçler arası geçici-dizin yarışında kırmızı oldu:

`Başarısız: 1, Başarılı: 976, Atlanan: 23, Toplam: 1000, Süre: 19 m 25 s`

Kırılan `PerformanceCheckTests.OlcumArtikBirakmiyor` ölçüsü tek başına art arda üç kez çalıştırıldı ve `1 başarılı, 0 başarısız` × 3 verdi. Makine sakin durumdayken tam süit yeniden kapıdan geçirildi:

`Başarısız: 0, Başarılı: 977, Atlanan: 23, Toplam: 1000, Süre: 18 m 28 s`

Kapı sonucu: `başarısız=0 toplam=1000 alt-sınır=974`.

---

# T91 — Kör noktaların mutasyonları

Durum: **tamamlandı — 2026-09-01.** Yukarıdaki iki T90 kaydı yerinde duruyor.

Her mutasyon üretim davranışını bozuyor, testin kendi sabitini değil. Her mutasyonun
ardından `git checkout --` ile geri alındı ve `git status` temiz doğrulandı.

## 1. `throw` gövdesindeki gömülü metin

`PanelHost.cs:414`'teki kesme iletisi Türkçeye çevrildi:
`throw new InvalidOperationException("Sol girdi yok; akış kurulamaz.")`.

`LanguageTests.KesmeIletisiEkranMetniDegil` kırmızı oldu:

`Playback\PanelHost.cs içindeki kesme iletileri: kesme iletisi kod dilinde değil: "Sol girdi yok; akış kurulamaz."`

Karşı ölçüm: aynı mutasyon dururken `LanguageTests.cs` `origin/main` sürümüne alındı,
`--filter "LanguageTests"` koştu ve **yeşil** verdi:
`Başarısız: 0, Başarılı: 27, Atlanan: 0, Toplam: 27`. Eski `Strip()` `throw new [^;]*;`
ile bütün kesme ifadelerini siliyordu, yani bu metni hiç görmüyordu.

## 2. Gövdesinde `{` geçen XAML metni

`MainWindow.axaml`'e `<TextBlock x:Name="MutasyonB">Hazır {0} dosya</TextBlock>` eklendi.

`LanguageTests.BicimlemedeKullaniciyaGorunenDuzMetinKalmadi` kırmızı oldu:

`MainWindow.axaml içinde anahtardan gelmeyen metin var: >Hazır {0} dosya<`

Karşı ölçüm: mutasyon yerinde dururken `ScreenBody` deseni eski hâline
(`>([^<>{}]*)<`) döndürüldü, aynı test **yeşil** verdi:
`Başarısız: 0, Başarılı: 7, Toplam: 7`. Süslü parantez dışlaması metni körlüyordu.

## 3. Taranmayan XAML dosyası

`Playback/ControlStrip.axaml:74`'teki `EncodeText` öznitelik bağından çıkarılıp
`>İşleniyor<` düz gövdesine çevrildi.

`LanguageTests.BicimlemedeKullaniciyaGorunenDuzMetinKalmadi` kırmızı oldu:

`Playback\ControlStrip.axaml içinde anahtardan gelmeyen metin var: >İşleniyor<`

Karşı ölçüm: aynı mutasyon dururken `LanguageTests.cs` `origin/main` sürümüne alındı,
aynı test **yeşil** verdi ve tek olay olarak koştu:
`Başarısız: 0, Başarılı: 1, Toplam: 1`. Eski sürüm tek dosya
(`TipSources.WindowXamlPath` = `MainWindow.axaml`) okuyordu; T91'de test `Theory` oldu
ve `src/VidShrink.App` altındaki yedi XAML dosyasının hepsini tarıyor.

## 4. Anahtarı olmayan Core doğrulama iletisi

`ConversionArguments.Validate`'e yeni bir ileti eklendi:
`if (plan.Fps is > 480) errors.Add("Frame rate is above the supported ceiling.");`

`LanguageTests.MotorunDogrulamaIletilerinePerdeArkasindaAnahtarVar` kırmızı oldu:

`ConversionArguments.Validate ↔ ValidationKeys: Core iletisinin anahtarı yok — "Frame rate is above the supported ceiling."`

Karşı ölçüm: aynı mutasyon dururken `LanguageTests.cs` `origin/main` sürümüne alındı,
`--filter "LanguageTests"` **yeşil** verdi:
`Başarısız: 0, Başarılı: 27, Atlanan: 0, Toplam: 27`. Eski ölçü kümesinde Core
iletilerini katalogla karşılaştıran bir test yoktu; yeni ileti sessizce
`main.validation.untranslated-engine` etiketine düşerdi.

## Kusurun kendisi — 1. ve 3. madde

`Locales/en/playback.json` içindeki `playback.error.exit-code` değeri kusurun özgün
metnine (`"ffmpeg {0} ile döndü."`) geri çevrildi.

`LanguageTests.OrnekHatasiIngilizceArayuzdeTumuyleIngilizce` kırmızı oldu ve
sözleşmenin tarif ettiği ekran metnini birebir bastı:

`'playback.error.exit-code' İngilizce arayüzde Latin dışı harf taşıyor — "The Preview Sample Could Not Be Encoded: FFmpeg 3 ile Döndü."`

## Sabit yerine anahtar — 6. madde

`LanguageCatalog.EncodeMarker` dil seçimini bırakıp her iki dilde de
`Strings.FallbackLanguage`'dan okuyacak biçimde bozuldu.

`LanguageTests.KodlamaImleciMetniDilAnahtarindanGelir` kırmızı oldu:

`Assert.Equal() Failure: Expected: "Analiz 1/2 · Deneme 3" / Actual: "Analysis 1/2 · Attempt 3"`

Beklenen değer `Locales/tr/main.json` içindeki `main.playback.encode-marker`
anahtarından okunup biçimlenerek üretildi; testte artık sabit metin yok.

## Doğrulama

Sözleşme filtresi (`dotnet test -c Release --filter "LocalizationTests|LanguageTests"`):

`Başarısız: 0, Başarılı: 60, Atlanan: 0, Toplam: 60, Süre: 2 s`

Koşum kapısı bu okumanın günlüğü üzerinden geçirildi:
`KOŞUM KAPISI GEÇTİ: başarısız=0 toplam=60 alt-sınır=60`.

Release yapısı (`dotnet build VidShrink.sln -c Release`): `0 Uyarı, 0 Hata`.

# T93 — İçi boşalmış ölçünün mutasyonları

Durum: **tamamlandı — 2026-09-01.** Dal: `T93-ici-bosalmis-olcu`.

Üç ölçü yeşil okuyordu ama ölçtükleri şey artık o değildi. Her madde için bozulan şey
üretim davranışı ya da taranan gerçek dosyadır; hiçbir mutasyon testin kendi sabitine
dokunmuyor. Her mutasyonda **karşı ölçüm** de var: aynı bozukluk dururken ölçünün
`HEAD` sürümü koşturuldu, deliğin gerçekten var olduğu gösterildi.

## 1. `LastError` iddiası — anahtar yerine tanı döndürüldü

`SegmentEncoder.cs:128` `LastFailure?.Key` yerine `LastFailure?.Detail` döndürecek
biçimde bozuldu. `LastError` bozuk kaynakta yine dolu geliyor, yani eski iddia
(`Assert.False(string.IsNullOrWhiteSpace(...))`) hiçbir şey görmüyor.

Yeni ölçüler kırmızı oldu — ikisi de aynı yerden:

`Assert.Equal() Failure: Strings differ / Expected: "playback.error.engine" / Actual: "[in#0 @ 0000026742c74100] moov atom not f"···`

Kırılanlar: `SegmentEncoderTests.Bozuk_kaynakta_hata_yutulmaz`,
`PanelHostTests.Bozuk_kaynakta_panel_cokmez`.

## 2. Anahtarın metne dönüşü — çeviri geçidi atlandı

`PanelHost.SampleFailureText` içindeki `PlaybackText(failure.Key)` çağrıları kaldırıldı;
sebep, sözlükten geçmeden ham anahtar olarak birleştirildi.

`PanelHostTests.Bozuk_kaynakta_panel_cokmez` kırmızı oldu ve anahtarın ekrana sızdığını
birebir bastı:

`Assert.DoesNotContain() Failure: Sub-string found / String: ···"Örneği Kodlanamadı: playback.error.engine"··· / Found: "playback.error.engine"`

`SegmentEncoderTests.Bozuk_kaynakta_hata_yutulmaz` bu mutasyonda haklı olarak yeşil
kaldı: kodlayanın kendi sebebi bozulmamıştı.

### Karşı ölçüm — 1. ve 2. madde

Her iki mutasyon birlikte dururken `PanelHostTests.cs` ve `SegmentEncoderTests.cs`
`HEAD` sürümlerine alındı. Aynı filtre **yeşil** verdi:

`Başarısız: 0, Başarılı: 2, Atlanan: 0, Toplam: 2`

İki üretim davranışı da bozukken eski iddialar hiçbir şey söylemiyordu. Ölçülen tek
şey `LastError`'ın boş olmamasıydı ve `Detail` de boş değildi.

## 3. `IsResourceValue` toptan muafiyeti — tema dosyasına cümle yazıldı

`Themes/Theme.axaml` sonuna `<x:String x:Key="SizanCumle">Bu bir ekran cumlesidir</x:String>`
eklendi. Açılış etiketinde `x:Key=` geçtiği için eski örüntü bu gövdeyi taramadan
düşürüyordu.

`LanguageTests.BicimlemedeKullaniciyaGorunenDuzMetinKalmadi` kırmızı oldu:

`Themes\Theme.axaml içinde anahtardan gelmeyen metin var: >Bu bir ekran cumlesidir<`

## 4. `ScreenBody` deliği — yer tutucuyla başlayan metin

`Playback/ComparisonPanel.axaml` içine `<TextBlock>{0} dosya hazir</TextBlock>` eklendi.
Eski ölçüt `value.StartsWith('{')` olduğu için gövde bağ sanılıp düşürülüyordu.

`LanguageTests.BicimlemedeKullaniciyaGorunenDuzMetinKalmadi` kırmızı oldu:

`Playback\ComparisonPanel.axaml içinde anahtardan gelmeyen metin var: >{0} dosya hazir<`

### Karşı ölçüm — 3. ve 4. madde

Her iki sızıntı dosyalarda dururken `LanguageTests.cs` `HEAD` sürümüne alındı.
`--filter "BicimlemedeKullaniciyaGorunenDuzMetinKalmadi"` **yeşil** verdi:

`Başarısız: 0, Başarılı: 7, Atlanan: 0, Toplam: 7`

Yedi biçimleme dosyasının hepsi taranıyordu ve iki gömülü metin de görünmüyordu.

## 5. Muafiyet listesinin ölü adı — kaynak gövdesi boşaltıldı

`Themes/Theme.axaml` içindeki `<x:String x:Key="LinkGitHub">` gövdesi harf taşımayan
bir değere (`12345`) çevrildi; ad artık hiçbir kaynak gövdesine karşılık gelmiyor.

Yeni `LanguageTests.Kaynak_muafiyetinde_kullanilmayan_ad_yok` kırmızı oldu:

`muafiyet listesinde karşılığı olmayan ad: LinkGitHub`

Bu ölçü muafiyetin ölü adla büyümesini engelliyor (sözleşme 5. madde).

## Muafiyetin ölçüsü

Eski örüntüden geçen gövde sayısı sayıldı: `Themes/Theme.axaml` **33**,
`Themes/Playback.axaml` **3** — toplam **36**, denetimin bildirdiği sayıyla birebir.

Otuz altısı da çevrilmeyecek kaynak değeri: 24 `Color`, 3 `BoxShadows`,
2 `FontFamily`, 2 `StreamGeometry`, 4 `x:String` (üç bağlantı adresi ve bir varlık
adresi), 1 perde rengi. Hiçbiri ekran metni taşımıyor, bu yüzden **36 gövdenin
0'ı taramaya girdi** — bugünkü ağaçta muafiyetten çıkan gövde yok.

Değişen şey sayı değil, muafiyetin kapsamı: örüntü bugünkü 36 gövdeyi olduğu kadar
yarın yazılacak her `x:Key` gövdesini de düşürüyordu. Ada bağlandıktan sonra muafiyet
adı yazılı 36 anahtarla sınırlı; listede olmayan her `x:Key` gövdesi taranıyor. 3.
maddedeki mutasyon bunun kanıtı.

## Ölçülmedi

- Panelin kendi `RightCurtainText` denetimine düşen metin okunmadı. `PanelHost`
  perdeyi yalnız panel açıkken (`_open`) tazeliyor; başsız ölçümde o yol koşmuyor.
  Ölçülen şey perdenin metnini üreten üretim yolu (`PanelHost.SampleFailureText`),
  denetimin `Text` özelliği değil.
- `playback.error.window`, `playback.error.exit-code` ve `playback.error.no-file`
  yollarının **gerçek koşumdan** doğuşu ölçülmedi. `Clamp` pencereyi kaynağın içine
  çektiği için `RequestAsync` üzerinden bu üç yola ulaşılamıyor; üçü de T91'in
  `FirstFailure` ölçüsünde elle kurulmuş koşumlarla duruyor.
- Tam süit koşturulmadı. Başka ajan paralel çalışıyordu; sözleşmenin dar filtresi
  koşturuldu.

## Doğrulama

Sözleşme filtresi (`dotnet test -c Release --filter "LanguageTests|PanelHostTests|SegmentEncoderTests"`):

`Başarısız: 0, Başarılı: 72, Atlanan: 0, Toplam: 72, Süre: 1 m 11 s`

Aynı filtrenin `HEAD` üzerindeki başlangıç okuması 71'di; fark yeni eklenen
`Kaynak_muafiyetinde_kullanilmayan_ad_yok`. Atlanan test yok — ffmpeg bu makinede
bulunuyor, `[FfmpegFact]` ölçüleri gerçekten koştu.

Release yapısında `TreatWarningsAsErrors` açık; derleme uyarısız geçti.
Bütün mutasyonlar geri alındı: `git diff -- src/` boş.
