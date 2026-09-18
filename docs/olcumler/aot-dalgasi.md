# AOT Dalgası — Üç Engelin Ölçümü

Dal: `t0/aot-dalga`, taban `a721f6c0`. Tarih: 2026-09-18. Makine: DESKTOP-0J80KVV,
Windows 11 22631.

Düzenek: `tools/acilis-hizi/EkranSaati`. Uygulama ayrı bir Win32 masaüstünde
(`WinSta0\vidshrink-olcum`) açılır, kullanıcının ekranına pencere çıkmaz. Her sürece
`KayitKalkani` başlangıç kancası girer ve HKCU'yu özel bir kovana yönlendirir; her koşumdan
önce ve sonra sağ tık menüsü, etiketler ve ilişkilendirme değerleri karşılaştırılır. Sıfır
noktası başlatıcının `Process.StartTime`'ı. Kip: sıcak, her koşum 10 tekrar, giriş
`klip1080.mp4` (6,4 MB).

**Yöntem kuralı.** Bu makine oturumlar arasında 1,7 kata kadar sürüklenir
(`tools/acilis-hizi/AGENTS.md`). Bu yüzden buradaki **hiçbir hüküm eşlenmemiş bir sayıdan
çıkmıyor**; hüküm yalnız eşli fark tablosundan, çift içi farkların ortancası ve kaç çiftin
aynı yöne baktığıyla verildi. Bu oturumda ölçülen gürültü tabanı **±60 ms**; n=10'da ~30 ms
altı etkiler görünmez.

## 1. Taban profili (`a721f6c0`, n=10, ms)

```
adim                                 n     en_az   ortanca       p95    en_cok
createprocess                       10       0.9       1.1      12.7      12.7
kalkan-surec                        10       2.0       2.0       2.0       2.0
iz:baslatici                        10      48.5      60.9      74.6      74.6
iz:app-dogdu                        10      55.3      69.8      84.3      84.3
iz:main                             10     107.6     123.9     141.1     141.1
iz:tek-ornek                        10     109.2     125.4     142.7     142.7
iz:libmpv-hazir                     10     119.3     135.4     179.9     179.9
iz:cerceve                          10     261.3     300.2     383.1     383.1
iz:ayar-okundu                      10     263.4     303.0     386.6     386.6
iz:palet                            10     263.6     303.2     386.9     386.9
iz:pencere-yapici                   10     282.3     325.1     418.8     418.8
iz:xaml-sekmeler                    10     303.5     355.5     455.9     455.9
iz:xaml                             10     349.4     409.0     568.3     568.3
iz:gecici-temizlik                  10     350.1     410.3     578.5     578.5
iz:yapici-bitti                     10     365.9     429.9     590.3     590.3
iz:pencere-kuruldu                  10     368.8     434.6     593.4     593.4
iz:pencere-yuklendi                 10     404.1     473.4     631.4     631.4
iz:motor-acildi                     10     430.5     508.7     660.6     660.6
iz:kare-kaynagi                     10     449.4     526.1     677.6     677.6
iz:ilk-kare                         10     449.6     526.3     677.9     677.9
iz:ilk-boya                         10     560.4     662.5     812.4     812.4
```

### 595 ms yeniden üretilemedi

`docs/olcumler/hipersurus-h.md`'nin "dosyayla açılış ilk-kare ≈595 ms" satırı bu oturumda
tutmadı: ortanca **526,3 ms** (en az 449,6, p95 677,9). 595 dağılımın içinde kalıyor, yani bu
bir çelişki değil sürüklenme. Ama tersi de doğru: **eşlenmemiş bir oturum-arası sayı hüküm
değildir**, ne 595 ne 526,3. Bu dalgadaki bütün hükümler aşağıdaki eşli tablolardan çıktı.

## 2. 165 ms'lik bloğun ayrıştırılması

Tabanda `libmpv-hazir → cerceve` tek bir aralıktı ve içinde ne olduğu okunamıyordu: bölüm 1
tablosunda `300,2 − 135,4 = 164,8 ms`. `App.Initialize`'a `AvaloniaXamlLoader.Load` çağrısının
önüne ve arkasına iki prob koydum (`app-init`, `app-xaml`; `src/VidShrink.App/App.axaml.cs`).
Aynı ikilinin içinde okundu:

```
libmpv-hazir -> app-init  (Avalonia cerceve kurulusu)   145.2 ms
app-init     -> app-xaml  (App.axaml'in TAMAMI)          13.4 ms
app-xaml     -> cerceve   (kalan)                         0.2 ms
iz:palet adimi (PaletteCatalog.Use)                       0.2 ms
```

Bu üçlünün toplamı 158,8 ms, taban tablosundaki 164,8 ms değil: **iki ayrı koşum**. Problar
tabana sonradan eklendiği için blok yalnız problu ikilide ayrışıyor; iki sayı arasındaki 6 ms
±60 ms'lik gürültü tabanının çok altında ve bir fark olarak okunmamalı. Aynı koşum içinde
karşılaştırılabilecek olan, bloğun içindeki üç payın birbirine oranı.

Bu sayı 3. engelin içeriğini değiştirdi; bkz. bölüm 4c.

## 3. Aşama aşama önce/sonra (sevk edilen hâl)

Aynı oturumda, eşli koşum: `taban` = `a721f6c0`, `v2` = bu dalın sevk edilen hâli + iki prob.

| aşama | önce (taban) | sonra (v2) | eşli fark ortanca | lehine |
|---|---:|---:|---:|---:|
| iz:baslatici | 57.2 | 57.5 | +2.5 | 4/10 |
| iz:main | 121.2 | 117.8 | +1.3 | 3/10 |
| iz:libmpv-hazir | 132.4 | 128.7 | +1.8 | 4/10 |
| iz:cerceve | 278.2 | 282.5 | -3.7 | 6/10 |
| iz:palet | 280.8 | 284.9 | -3.8 | 6/10 |
| iz:pencere-yapici | 299.4 | 304.1 | -1.6 | 5/10 |
| iz:xaml | 369.8 | 370.1 | -2.5 | 6/10 |
| iz:pencere-yuklendi | 426.5 | 427.6 | -1.9 | 5/10 |
| iz:motor-acildi | 455.6 | 453.8 | -8.4 | 6/10 |
| **iz:ilk-kare** | **474.5** | **473.2** | **-1.1** | **5/10** |
| iz:ilk-boya | 596.1 | 601.3 | +4.5 | 5/10 |

**Hüküm: bu dalganın ölçülebilir açılış kazancı yoktur.** `ilk-kare` farkı -1,1 ms ve 5/10
çift, yani yazı tura. Tek bir aşamada bile ±60 ms gürültü tabanını aşan bir fark çıkmadı.
Dalganın değeri ms değil, **AOT/trim uygunluğu**: bölüm 7'deki çözümleyici sayımı.

## 4. Engel başına

### 4a. JSON kaynak üretimi (engel 1)

`System.Text.Json`'ın yansımalı `Serialize`/`Deserialize` aşırı yüklemeleri dört
`JsonSerializerContext`'le değiştirildi: `Core/CoreJson.cs` (`GevsekJson`, `PaylasimJson`,
`IzlemeJson`, `YolJson`), `App/Localization/CatalogJson.cs`, `Cli/CliJson.cs`. Her bağlam
değiştirdiği seçenek nesnesini birebir yansıtıyor (`PropertyNameCaseInsensitive`,
`ReadCommentHandling`, `WriteIndented`, `DefaultIgnoreCondition`, CamelCase).
`PresignedUploadProvider`'ın `Dictionary<string, object>` gövdeleri `JsonObject`'e çevrildi;
`JsonNode` ağacı zaten yansımasız.

**Kazanç: 0 ms** (bölüm 3). 13 IL2026/IL3050 noktası kapandı.

### 4b. Yansımalı bağlama (engel 2)

`{loc:Text}` biçimlemede 493 yerde `new Binding("Value")` kuruyordu: yol dizgesi çalışma
anında ayrıştırılıp özellik yansımayla aranıyor. Yerine anahtar başına **tek** bir
`CompiledBinding`; yol (`CompiledBindingPath`) bir kez, statik olarak çıkarılıyor
(`App/Localization/Text.cs`). `TextExtension.ProvideValue` artık `BindingBase` döndürüyor.

**Kazanç: 0 ms.** İlk koşum (taban vs v1) `ilk-kare`'de -35,0 ms / 8/10 göstermişti;
doğrulama koşumu (taban vs v2, aynı kod) -1,1 ms / 5/10 verdi. **-35 taban tarafındaki sapan
koşumlardan geliyordu**, değişiklikten değil. Doğrulama koşumu olmasa bu dalga rapora yanlış
bir kazanç yazacaktı.

### 4c. Palet ve kaynak yüklemesi (engel 3)

Ölçüm engelin tanımını değiştirdi. `PaletteCatalog.Use` adımı **0,2 ms** (C3 dalgasının hızlı
yolu zaten oradaydı) ve `App.axaml`'in tamamı — beş `ResourceInclude` (Palette/Neon, Theme,
Icons, Playback 523 satır, Recorder 69 satır) + `FluentTheme` + `Controls.axaml` (1428 satır) —
**13,4 ms**. Karşısında 145,2 ms'lik Avalonia çerçeve bloğu ve ±60 ms gürültü var.

**Tembelleştirme yapılmadı**: tavanı 13,4 ms, ölçülemez. Engel 3'ün gerçek içeriği ms değil:
`PaletteCatalog.cs:102` ve `:123`'teki çalışma anı `ResourceInclude(Uri)` çağrısı, kalan iki
IL2026'nın kaynağı ve NativeAOT'un önündeki tek yansıma engeli. Çözümü bölüm 9'da.

## 5. Eşli fark tabloları (birebir)

Ham çıktılar `.calisma/aot/olcum-*` altındaydı ve iş bitince silindi; tablolar buraya birebir
taşındı.

### (a) taban vs v1 — JSON + bağlama, ilk koşum

```
== eslesik fark (v1 eksi taban, eksi deger v1 lehine) ==
adim                               cift   ortanca     en_az    en_cok  lehine
iz:baslatici                         10       5.9      -5.1      17.1    3/10
iz:main                              10       7.8     -11.8      30.7    2/10
iz:libmpv-hazir                      10       5.5     -33.8      36.3    3/10
iz:cerceve                           10      -1.8    -205.2      68.2    6/10
iz:palet                             10      -1.6    -206.3      68.1    6/10
iz:pencere-yapici                    10      -5.1    -234.6      67.8    6/10
iz:xaml                              10     -13.9    -277.7      73.6    7/10
iz:pencere-yuklendi                  10     -14.8    -326.9      70.7    7/10
iz:motor-acildi                      10     -34.4    -332.5      66.5    8/10
iz:ilk-kare                          10     -35.0    -332.8      62.4    8/10
iz:ilk-boya                          10     -55.1    -357.5     100.2    7/10
```

`en_az` sütunundaki -205…-357 ms taban tarafındaki sapan koşumlar. Bu yüzden doğrulama koşumu
koşuldu:

### (b) taban vs v2 — aynı kod, doğrulama koşumu

```
== eslesik fark (v2 eksi taban, eksi deger v2 lehine) ==
adim                               cift   ortanca     en_az    en_cok  lehine
createprocess                        10       0.0      -1.4       0.2    4/10
iz:baslatici                         10       2.5      -9.5      25.3    4/10
iz:main                              10       1.3     -16.9      33.8    3/10
iz:libmpv-hazir                      10       1.8     -17.4      33.8    4/10
iz:cerceve                           10      -3.7     -65.1      35.1    6/10
iz:palet                             10      -3.8     -65.4      34.5    6/10
iz:pencere-yapici                    10      -1.6     -67.0      36.7    5/10
iz:xaml                              10      -2.5     -78.7      39.5    6/10
iz:yapici-bitti                      10      -1.7     -83.2      40.4    6/10
iz:pencere-yuklendi                  10      -1.9     -82.5      43.2    5/10
iz:motor-acildi                      10      -8.4     -77.9      47.2    6/10
iz:ilk-kare                          10      -1.1     -74.1      47.3    5/10
iz:ilk-boya                          10       4.5    -100.1      41.9    5/10
```

### (c) v2 vs v3 — `InvariantGlobalization=true`

```
== eslesik fark (v3 eksi v2, eksi deger v3 lehine) ==
adim                               cift   ortanca     en_az    en_cok  lehine
iz:baslatici                         10      -1.5     -43.3       8.7    5/10
iz:main                              10      -5.2    -101.8      17.3    7/10
iz:libmpv-hazir                      10      -4.5    -109.3      43.8    7/10
iz:app-init                          10     -24.9    -172.4      33.1    7/10
iz:app-xaml                          10     -26.3    -177.5      34.7    7/10
iz:cerceve                           10     -26.2    -177.6      34.7    7/10
iz:pencere-yapici                    10     -25.4    -183.3      38.6    7/10
iz:xaml                              10     -38.8    -210.0      39.1    7/10
iz:pencere-yuklendi                  10     -39.1    -298.7      39.9    7/10
iz:motor-acildi                      10     -43.7    -348.4      41.2    7/10
iz:ilk-kare                          10     -43.5    -351.5      43.6    7/10
iz:ilk-boya                          10     -44.9    -434.3     126.1    7/10
```

Kazanç gerçek ve mekanik olarak doğru yerde: Windows'ta pakette `icudt` yok, ICU işletim
sisteminden yükleniyor ve ilk kültür dokunuşunda geliyor — `app-init` -24,9 ms. **Buna rağmen
açılmadı**, gerekçe bölüm 9'da.

### (d) v2 vs v5 — `System.Globalization.UseNls=true`, birinci koşum

```
== eslesik fark (v5-nls eksi v2, eksi deger v5-nls lehine) ==
adim                               cift   ortanca     en_az    en_cok  lehine
iz:baslatici                         10      -3.4     -64.2       4.5    6/10
iz:main                              10     -13.3    -205.0      19.9    6/10
iz:libmpv-hazir                      10     -15.3    -198.9      18.7    6/10
iz:app-init                          10     -26.5    -308.5      23.3    6/10
iz:cerceve                           10     -28.7    -318.1      26.1    6/10
iz:pencere-yapici                    10     -29.0    -344.4      26.2    8/10
iz:xaml                               9     -82.1    -497.8       9.5    7/9
iz:pencere-yuklendi                  10     -91.5    -569.2      15.2    8/10
iz:ilk-kare                          10     -89.9    -558.2      25.3    8/10
iz:ilk-boya                          10     -46.0    -777.4      26.5    7/10
```

Bu koşumun `ilk-kare` -89,9 ms'si **alınmadı**: v2 tarafının kendi dağılımı bozuk
(`ilk-kare` p95 1159,2 ms, en çok 1486,8 ms — taban p95'i 677,9). Sürüklenmiş bir tarafa karşı
ölçülen kazanç kazanç değil. Koşum tekrarlandı:

### (e) v2 vs v5 — aynı ikili, temiz koşum

```
== eslesik fark (v5-nls eksi v2, eksi deger v5-nls lehine) ==
adim                               cift   ortanca     en_az    en_cok  lehine
iz:baslatici                         10       6.7     -87.1     112.4    3/10
iz:main                              10       3.5    -125.4     307.2    4/10
iz:libmpv-hazir                      10       1.3    -126.0     375.5    5/10
iz:app-init                          10     -12.2    -134.7     620.2    7/10
iz:app-xaml                          10     -13.1    -130.4     619.9    7/10
iz:cerceve                           10     -13.1    -130.4     619.9    7/10
iz:pencere-yapici                    10      -8.9    -122.8     623.6    7/10
iz:xaml                              10      -9.3    -125.2     591.7    5/10
iz:pencere-yuklendi                  10      -3.1    -191.7     598.0    6/10
iz:motor-acildi                      10       0.2    -212.5     591.1    5/10
iz:ilk-kare                          10       1.6    -211.0     591.8    5/10
iz:ilk-boya                          10       5.4    -165.9     437.8    3/10
```

**NLS hükmü: `app-init`'te yön var (-26,5 ve -12,2; 6/10 ve 7/10), `ilk-kare`'de yok
(+1,6; 5/10).** ICU yükü kalkmıyor, yer değiştiriyor: aşamada kazanılan geri kalanda
soğuruluyor. Kullanıcıya ulaşmayan kazanç kazanç değil — anahtar dala girmedi
(`docs/olcumler/kazanc-kullaniciya-ulasiyor-mu.md`).

## 6. Trim/AOT anahtarları — ölçerek karar

| anahtar | ölçüm | karar | gerekçe |
|---|---|---|---|
| `PublishReadyToRunComposite` | önceki dalgada ölçüldü | **açık kalıyor** | değişmedi |
| `InvariantGlobalization` | `ilk-kare` -43,5 ms, 7/10 | **hayır** | iki ölümcül çağrı noktası, ikisi de pimli (bölüm 9) |
| `System.Globalization.UseNls` | `app-init` -26,5 / -12,2; `ilk-kare` +1,6 | **hayır** | kazanç ilk kareye ulaşmıyor, NLS riski bedava değil |
| `PublishTrimmed` (partial) | **ölçülemedi** | **ertelendi** | ölçüm kalkanı trim'li süreçte yüklenmiyor (bölüm 8) |
| `TrimMode=full` | ölçülmedi | **ertelendi** | partial'a göre açılış kazancı beklenmiyor; yalnız AOT öncülü |
| `TrimmerRootAssembly` | — | **kullanılmadı** | meşru tek kullanımı ölçüm yayınında Registry'yi köklemek |

Kesme yayını boyut olarak ölçüldü: **317,9 MB / 229 dosya** (kesilmemiş 366,3 MB / 235 dosya).

## 7. Trim/AOT çözümleyicisi: önce/sonra

Sayım, ölçüm değil; iki bayrak da dalda bırakılmadı, komut satırından verildi. Taban için
`a721f6c0`'a ayrı bir worktree açıldı ve iki ağaçta aynı komut koşuldu:

```
dotnet build VidShrink.sln -c Release -m:2 -t:Rebuild \
  -p:EnableTrimAnalyzer=true -p:EnableAotAnalyzer=true -p:TreatWarningsAsErrors=false
```

Sayılan: çıktıdaki `src\...*.cs(satır,sütun): warning IL2026|IL3050` satırları, tekilleştirilmiş.
**Test ve araç projeleri kapsam dışı** — `tests/` ve `tools/` altında tabanda 43 nokta daha var,
hiçbiri sevk edilen ikilide değil.

| | uyarı | nokta |
|---|---:|---:|
| önce (`a721f6c0`) | 33 | 18 |
| sonra (bu dal) | 7 | 5 |
| kapanan | 26 | 13 |

Kapanan noktalar: `CliText.cs:39`, `DefaultAppSuggestionBar.cs:91`, `PlanParser.cs:35`,
`PresignedUploadProvider.cs:202`, `ShareResult.cs:162` ve `:209`, `ShareTargets.cs:113`,
`SingleInstanceChannel.cs:79` ve `:84`, `Strings.cs:347`, `Text.cs:56`, `WatchFolder.cs:161`
ve `:175`.

Kalan noktalar: `PaletteCatalog.cs:102` ve `:123` (çalışma anı `ResourceInclude(Uri)`) ve
`PresetLibrary.cs:91`, `:174`, `:201`. Sonuncular `JsonStringEnumConverter(allowIntegerValues:
false)` yüzünden yansıma istiyor; önayarlar açılış yolunda değil ama AOT derlemesi IL3050'yi
hata sayar.

**H4'ün engel listesi eksikmiş.** `docs/olcumler/hipersurus-h.md:111-122` on iki nokta sayıyor,
tabanda on sekiz var. Listede hiç olmayan **altı** nokta:

| nokta | durum |
|---|---|
| `CliText.cs:39` | bu dalgada kapandı |
| `WatchFolder.cs:161` | bu dalgada kapandı |
| `WatchFolder.cs:175` | bu dalgada kapandı |
| `PresetLibrary.cs:91` | kaldı |
| `PresetLibrary.cs:174` | kaldı |
| `PresetLibrary.cs:201` | kaldı |

## 8. Ölçüm duvarı: trim ölçülemedi

1. Kesme yayını **iki IL2026 ile düştü**: `PaletteCatalog.cs:102`, `:123`.
   (`TreatWarningsAsErrors` Release'de bunları hataya çeviriyor.)
2. Uyarılar yalnız ölçüm değişkeni için bastırıldı, yayın alındı.
3. **Ölçülemedi**: trim `System.StartupHookProvider.IsSupported`'ı kapatıyor, `KayitKalkani`
   yüklenmedi ve düzenek kalkansız koşumu durdurdu (`kalkan=False`, çıkış 3).
4. `StartupHookSupport=true` anahtarı runtimeconfig'de geri verdi, ama süreç `Main`'e varmadan
   öldü (`iz:app-dogdu` var, `iz:main` yok). Teşhis: kesilmiş sürece kesilmemiş kanca
   derlemesi yükleniyor ve `Microsoft.Win32.Registry`'nin kesilmiş üyelerini çözemiyor.
   Trim'li uygulama **kancasız tek başına çalışıyor**; kusur uygulamada değil ölçüm kalkanında.

Bu, H4'ün NativeAOT için yazdığı duvarın aynısı. Çözüm biliniyor ama bu dalgada uygulanmadı:
ölçüm yayınına `<TrimmerRootAssembly Include="Microsoft.Win32.Registry" />` (fable yanıtı,
4. başlık).

**Güvenlik olayı, kayda geçiyor.** Kesme değişkeninin stderr'ini yakalamak için yayınlanmış
uygulamayı düzeneğin dışında elle çalıştırdım; uygulama tam açıldı ve gerçek HKCU'ya yazdı —
`Applications\VidShrink.exe\shell\open\command`,
`Teknesyum.VidShrink.Video\shell\open\command` ve `…\DefaultIcon` `.calisma\aot\v4`'e döndü.
Üçü de gerçek kuruluma (`%LOCALAPPDATA%\Programs\VidShrink\VidShrink.exe`) geri alındı ve temiz
çıkana kadar tarandı; sonraki bağımsız tarama üç anahtarın da gerçek kuruluma baktığını ve
01:20:30'dan beri yazılmadığını doğruladı. Ayrı bir anahtar, `Applications\VidShrink.App.exe`,
bu dalgadan önceki bir worktree yoluna bakıyordu; ona hiçbir `UserChoice` bağlı değildi ve T0
sildi. **Kural: yayınlanmış uygulama düzeneğin dışında hiç çalıştırılmaz.**

## 9. Yapılmayanlar ve gerekçeleri

- **Kaynak yüklemesini tembelleştirme** — tavanı 13,4 ms, gürültü tabanı ±60 ms. Ölçülemez.
- **`InvariantGlobalization`** — iki ölümcül çağrı noktası, ikisi de artık pimli:
  - `MainWindow.axaml.cs:532` — invariant kipte `CurrentUICulture.Name` boş dizge,
    `ResolveLanguage` hiçbir dili tutmaz ve kaydedilmiş dili olmayan kullanıcı 42 dilin
    hiçbirini almadan İngilizce açar.
    Pim: `SettingsTests.LanguageUsesSavedThenOperatingSystemThenEnglish`, `(null, "", "en")` kolu.
  - `LanguageCatalog.cs:307` — `body[..1].ToUpper(culture)` Türkçe `i`'yi `İ` değil `I` yapar
    ("iptal" → "Iptal") ve bu tek çağrı arayüzdeki her metnin satır başını üretiyor.
    Pim: `KulturSozlesmesiTests.TurkceBuyukHarfKuraliKulturdenGeliyor`.

  -43,5 ms bu bedeli ödemez. (Daha önce burada "44 kültüre duyarlı çağrı noktası" yazıyordu;
  o sayıyı üreten tarama kayıtlı değildi ve hiçbir test onu pimlemiyordu — çıkarıldı. Kararın
  ölçüsü yukarıdaki iki pim.)
- **`UseNls`** — bölüm 5e: kazanç `ilk-kare`'ye ulaşmıyor.
- **`PublishTrimmed`** — bölüm 8: ölçüm kalkanı olmadan ölçülemez, ölçülmeyen değişiklik dalda
  bırakılmaz.
- **26 paletin `x:Class`'a çevrilmesi** — `PaletteCatalog`'un iki IL2026'sını kaldıracak tek iş,
  ama açılış kazancı 0 (palet adımı 0,2 ms). NativeAOT kararı verilince yapılır.
- **`PresetLibrary` JSON kaynak üretimi** — açılış yolunda değil; AOT derlemesi gündeme gelince.
- **Başlatıcıyı NativeAOT yapmak** — fable'ın en ucuz kazanç önerisi: `main`'e kadarki 124 ms'in
  70'i uygulama süreci doğmadan geçiyor, başlatıcı Avalonia'sız ve kalkan sorunu yok. Bu dalganın
  kapsamı dışında; `src/VidShrink.Launcher/` başka bir ajanda.

## 10. 200 ms teorik sınırı

fable danışması: `docs/danisma/2026-09-18-aot-dalgasi-soru-fable.md` (soru, kendi ölçümlerim
birebir içinde) ve `docs/danisma/2026-09-18-aot-dalgasi-yanit-fable.md` (yanıt birebir).

Özet hüküm: **`ilk-kare` için 200 ms savunulamaz.** `cerceve → ilk-kare` 226 ms ve içinde
AOT'un dokunamadığı kalemler var (Win32 pencere kurulumu 39 ms, libmpv çekirdek + dosya açma
35 ms, ilk kare çözme 17 ms); `libmpv-hazir → app-init` 145,2 ms'in içinde yerel kütüphane
yüklemesi ve D3D11/EGL aygıt kurulumu AOT'tan hiç etkilenmiyor. fable'ın tahmini bu makinede
gerçekçi sınır **300–350 ms**; savunulabilir 200 ms hedefi yalnız `main`'e kadar (bugün 124 ms).
H4'ün 200'ü "AOT + yazılım kare yolu"nu birlikte varsayıyor ve yazılım yolunun ilk boyayı
yavaşlattığını hesaba katmıyor.

Ölçmeden AOT'un tavanını okumanın ucuz yolu (fable, 1. başlık; yapılmadı): `app-init` probuna
`JitInfo.GetCompilationTime()` yazdırmak — 145 ms'in içindeki JIT payını süreç içinden doğrudan
verir.

## 11. Pimler ve mutasyon kanıtı

Dokuz yeni pim: `tests/VidShrink.Tests/AotDalgasiTests.cs` — `AotDalgasiTests` (3 bağlama pimi),
`AotJsonTests` (3 JSON pimi), `KulturSozlesmesiTests` (3 kültür pimi). Kültür pimleri
`InvariantGlobalization`/NLS kararının ölçüsünü koda bağlıyor: Türkçe büyük harf kuralı, Türkçe
ondalık ayırıcı, dil kodu başına boş olmayan kültür.

Buna bir de var olan bir theory'ye eklenen kol katıldı:
`SettingsTests.LanguageUsesSavedThenOperatingSystemThenEnglish`'e `(null, "", "en")`. Bu kol
invariant kipin ilk açılış dil algılamasına ne yaptığını pimliyor; gerekçesi testin kendi
docstring'inde.

Her pim, koruduğu davranışı bozan bir mutasyonla sınandı; verdikt `dotnet test` çıkış kodundan:

```
M1 yansimali baglama geri: PIM OLDU (mutasyon yakalandi)
M2 anahtar basina tek ornek bozuldu: PIM OLDU (mutasyon yakalandi)
M3 bag tek yonlu yenilenmiyor: PIM OLDU (mutasyon yakalandi)
M4 gevsek json katilasti: PIM OLDU (mutasyon yakalandi)
M5 paylasim defteri girintisiz: PIM OLDU (mutasyon yakalandi)
M6 kanal yolu kirpiyor: PIM SAGKALDI -- ZAYIF PIM
M7 buyuk harf kulturu yok sayiyor: PIM OLDU (mutasyon yakalandi)
M8 kultur degismez kulture dustu: PIM OLDU (mutasyon yakalandi)
--- mutasyonlar geri alindi, agac: 13 files changed, 104 insertions(+), 33 deletions(-)
```

M6 incelendi: mutasyon `SingleInstanceChannel.Encode`'da
`YolJson.Default.IReadOnlyListString` yerine `…StringArray` koyuyordu. **İki bağlam aynı
JSON'u üretiyor**, yani mutasyon anlamca eşdeğerdi — pim zayıf değil, mutasyon geçersizdi.
Davranışı gerçekten bozan bir mutasyonla tekrarlandı (`Encode` boş yolları yutuyor):

```
M6b bos yolu yutan kanal: PIM OLDU (mutasyon yakalandi)
geri alindi
```

Sekiz mutasyonun sekizi yakalandı.

## 12. CI'da düşen kaydedici testi: sıra bağımlılığı, dalın kusuru değil

İlk CI koşumu (35291760780) tek testte kırmızı geldi:

```
Failed VidShrink.Tests.KaydediciPencereTests.SeciciLinuxtaKimlikVeEkranMacteKirpmaYazarWaylandiReddeder [10 s]
  Assert.Equal() Failure: Strings differ
  Expected: "crop=640:480:200:100"
  Actual:   "crop=640:480:200:100,scale=1280:720"
Failed!  - Failed: 1, Passed: 3314, Skipped: 27, Total: 3342, Duration: 29 m 24 s
KOSUM KAPISI DUSTU: kod=66 sart=Basarisiz/Failed ozeti sifir degil: 1.
```

**Kusur bu dalın değil.** `git diff main...HEAD` kaydedici kaynaklarında ve kaydedici
testlerinde boş; dalın yaptığı tek şey, `AotDalgasiTests`'in üç yeni test sınıfıyla xUnit
koleksiyon sırasını oynatıp main'de duran gizli bir sıra bağımlılığını görünür kılmak.

Mekanizma, salt ölçümle (`.calisma/test-ciktilari/appdata/<pid>/recorder-settings.json`,
koşum sonrası kalan dosya):

```
--filter "FullyQualifiedName~KaydediciArayuzTests"   -> 86/86 yeşil
kalan dosya: "scaleWidth": 1280, "scaleHeight": 720, "tune": "zerolatency", ...
aynı filtre, GelismisKollarIstegeVeAyaraGecer dışlanınca -> kalan "scaleWidth": 0
```

Yani `KaydediciArayuzTests.GelismisKollarIstegeVeAyaraGecer` (satır 938-939) paylaşılan
ayar dosyasına 1280x720 bırakıyor, `KaydediciPencereTests`'in görünümü onu kurucuda
(`RecorderView.axaml.cs:40`) okuyor ve `-vf`'e `scale=` ekleniyor
(`RecorderArguments.cs:1185`).

Kanal kapatıldı, **kaynak kapatılmadı**:

1. **Kurban yalıtıldı.** `KaydediciPencereTests` mac isteğini `Scale = null` ile kuruyor.
   Ölçünün konusu pencere kırpması; kalıcı ölçek o ölçüye hiç girmemeli. Bu, sızıntının
   kurbana ulaşan kanalını tümden kapatıyor.
2. **Kaynak açık.** `GelismisKollarIstegeVeAyaraGecer` sınıf bütün koşulduğunda paylaşılan
   dosyada hâlâ 1280x720 bırakıyor. Ayrı iş olarak kaydedici sahibine devredildi.

### Reddedilen düzeltme: dispatcher pompası

İlk denemede `GelismisOlc`'a ölçümden sonra `Dispatcher.UIThread.RunJobs()` konuldu; bekleyen
`PersistChoices` işini `finally`'den önce boşaltsın diye. Yerelde pimlendi ve mutasyon da
yakalandı (pompa sökülünce `Assert.DoesNotContain` kırmızı). **CI iki koşumda reddetti:**

```
kosum 35296182516 (ilk):    Failed OynaticiCiftTikSuresiTests.HizAdimiVeCiftTikSuresiHamGirdiyle
                            ham sol tik: 618 ms'de pause yes, 3629 ms'de pause yes
kosum 35296182516 (tekrar): ayni test, ham sol tik: 612 ms'de pause yes, 3638 ms'de pause yes
```

İki koşumda neredeyse aynı sayı: kararsızlık değil, belirleyici bir kırılma. Sebep,
`AppHost`'un tek bir Avalonia arayüz iş parçacığını **bütün test sınıflarıyla** paylaşması
(`tests/VidShrink.Tests/AppHost.cs`): sınıflar paralel koşarken `RunJobs()` yalnız çağıranın
değil, o sırada kuyrukta ne varsa hepsinin işini boşaltıyor. Oynatıcının tık hakemi 900 ms'lik
pencereye bakıyor ve sırası bozulunca tek tık 3,6 saniyeye kayıyor.

Pompa ve ona bağlı pim geri alındı; yerinde `GelismisOlc`'un başındaki açıklama duruyor.
Ders, ölçüm kuralının aynısı: **paylaşılan iş parçacığında sıra da paylaşılan bir kaynaktır.**

**Not — ilk kırmızı sıraya bağlıydı:** koşum 35291760780 yeniden başlatılınca aynı commit'te
yeşil geldi. Yani kaydedici kırılması da makineye ya da koda değil sıraya bağlıydı.
