# AOT Dalgası — fable Danışması (Soru)

Tarih: 2026-09-18. Dal: `t0/aot-dalga`, taban `a721f6c0`.
Düzenek: `tools/acilis-hizi/EkranSaati`, ayrı Win32 masaüstü, `KayitKalkani` HKCU kalkanı,
sıfır noktası başlatıcının `Process.StartTime`'ı. Kip: sıcak. Her koşum 10 tekrar.

Bu makine oturumlar arasında 1,7 kata kadar sürükleniyor; bu yüzden hüküm **yalnız eşli fark
tablosundan** (çift içi farkların ortancası ve kaç çiftin aynı yöne baktığı) çıkıyor.

## 1. Bugünün taban profili (taban, n=10, ortanca, ms)

```
createprocess            1.1
kalkan-surec             2.0
iz:baslatici            60.9
iz:app-dogdu            69.8
iz:main                123.9
iz:tek-ornek           125.4
iz:libmpv-hazir        135.4
iz:cerceve             300.2
iz:ayar-okundu         303.0
iz:palet               303.2
iz:pencere-yapici      325.1
iz:xaml-sekmeler       355.5
iz:xaml                409.0
iz:gecici-temizlik     410.3
iz:yapici-bitti        429.9
iz:pencere-kuruldu     434.6
iz:pencere-yuklendi    473.4
iz:motor-acildi        508.7
iz:kare-kaynagi        526.1
iz:ilk-kare            526.3
iz:ilk-boya            662.5
```

`ilk-kare` en_az 449.6 / ortanca 526.3 / p95 677.9.

**595 ms yeniden üretilemedi.** `docs/olcumler/hipersurus-h.md`'nin "dosyayla açılış ilk-kare
≈595" satırı bu oturumda tutmuyor; ortanca 526.3. 595 dağılımın içinde kalıyor (449.6–677.9),
yani çelişki değil sürüklenme; ama eşlenmemiş bir oturum-arası sayı hüküm değil.

## 2. 175 ms'lik bloğun ayrıştırılması (yeni prob)

`App.Initialize`'a `AvaloniaXamlLoader.Load` çağrısının **önüne** ve **arkasına** iki prob
koydum (`app-init`, `app-xaml`). Aynı ikili içinde okundu (n=10, ortanca):

```
iz:tek-ornek           140.2
iz:libmpv-hazir        152.6
iz:app-init            303.9
iz:app-xaml            316.8
iz:cerceve             317.1

libmpv-hazir -> app-init  (Avalonia çerçeve kuruluşu)   145.2 ms
app-init     -> app-xaml  (App.axaml'in TAMAMI)          13.4 ms
app-xaml     -> cerceve   (kalan)                         0.2 ms
iz:palet adımı (PaletteCatalog.Use)                       0.2 ms
```

`App.axaml`'in tamamı = beş `ResourceInclude` (Palette/Neon, Theme, Icons, Playback 523 satır,
Recorder 69 satır) + `FluentTheme` + `Controls.axaml` (1428 satır) = **13.4 ms**.

Buna dayanarak "palet/kaynak yüklemesini tembelleştirme" işini **yapmadım**: tavanı 13.4 ms,
gürültü tabanı ±60 ms.

## 3. Eşli ölçümler

### (a) JSON kaynak üretimi + derlenmiş bağlama (taban vs v1/v2)

Değişiklik: `System.Text.Json` kaynak üretimi (dört `JsonSerializerContext`) + `loc:Text`'in
yansımalı `Binding("Value")`'sinin yerine anahtar başına tek `CompiledBinding` (493 bağ noktası).

İlk koşum (taban vs v1) — `ilk-kare` ortanca **-35.0 ms**, 8/10 çift lehte.
Doğrulama koşumu (taban vs v2, aynı kod + bir prob) — `ilk-kare` ortanca **-1.1 ms**, 5/10.

```
== eslesik fark (v2 eksi taban, eksi deger v2 lehine) ==
adim                               cift   ortanca     en_az    en_cok  lehine
iz:cerceve                           10      -3.7     -65.1      35.1    6/10
iz:pencere-yapici                    10      -1.6     -67.0      36.7    5/10
iz:xaml                              10      -2.5     -78.7      39.5    6/10
iz:pencere-yuklendi                  10      -1.9     -82.5      43.2    5/10
iz:ilk-kare                          10      -1.1     -74.1      47.3    5/10
iz:ilk-boya                          10       4.5    -100.1      41.9    5/10
```

**Hükmüm: ölçülebilir açılış kazancı yok.** İlk koşumdaki -35 ms taban tarafındaki sapan
koşumlardan geliyordu. Değişikliğin değeri ms değil, trim/AOT uygunluğu (aşağıda).

### (b) InvariantGlobalization (v2 vs v3)

```
== eslesik fark (v3 eksi v2, eksi deger v3 lehine) ==
adim                               cift   ortanca     en_az    en_cok  lehine
iz:baslatici                         10      -1.5     -43.3       8.7    5/10
iz:main                              10      -5.2    -101.8      17.3    7/10
iz:libmpv-hazir                      10      -4.5    -109.3      43.8    7/10
iz:app-init                          10     -24.9    -172.4      33.1    7/10
iz:cerceve                           10     -26.2    -177.6      34.7    7/10
iz:ilk-kare                          10     -43.5    -351.5      43.6    7/10
iz:ilk-boya                          10     -44.9    -434.3     126.1    7/10
```

Windows'ta pakette `icudt` yok, ICU işletim sisteminden geliyor; kazanç mekanik olarak doğru
yerde (`app-init`, -24.9 ms).

**Buna rağmen açmadım.** `src/` taraması 44 kültüre duyarlı çağrı noktası buldu; ikisi tek
başına ölümcül:
- `MainWindow.axaml.cs:532` — invariant modda `CultureInfo.CurrentUICulture.Name` boş dizgedir;
  `ResolveLanguage` hiçbir dili tutmaz ve `FallbackLanguage` ("en") döner. Kaydedilmiş dili
  olmayan kullanıcı, 42 yerelleştirme dosyasının hiçbirini almadan uygulamayı İngilizce açar.
- `LanguageCatalog.cs:307` — `body[..1].ToUpper(culture)`; Türkçe `i` → `İ` yerine `I` olur
  ("iptal" → "Iptal"). Bu tek çağrı arayüzdeki her metnin satır başı harfini üretiyor.

### (c) PublishTrimmed (partial)

1. Yayın **iki IL2026 ile düştü**, ikisi de aynı yerde:
   `PaletteCatalog.cs:102` ve `:123` — çalışma anında `ResourceInclude(Uri)`.
2. Uyarıları bastırıp yayınladım: 317.9 MB / 229 dosya (kesilmemiş: 366.3 MB / 235 dosya).
3. **Ölçemedim.** Trim `System.StartupHookProvider.IsSupported`'ı kapatıyor; `KayitKalkani`
   yüklenemedi ve düzenek kalkansız koşumu durdurdu (`kalkan=False`).
4. `StartupHookSupport=true` ile anahtarı geri verdim: kanca artık destekleniyor ama süreç
   `Main`e varmadan ölüyor (`iz:app-dogdu` var, `iz:main` yok). Kesilmiş süreç içine kesilmemiş
   kanca derlemesi yükleniyor ve orada patlıyor. Trim'li uygulama **kancasız tek başına
   çalışıyor**; yani kusur uygulamada değil, ölçüm kalkanında.

Bu, H4'ün NativeAOT için yazdığı duvarın aynısı: *"AOT `DOTNET_STARTUP_HOOKS` desteklemiyor,
`KayitKalkani` AOT ikilisini koruyamaz."* Trim de aynı duvara çarpıyor.

### (d) Trim/AOT çözümleyicisi: önce/sonra

`EnableTrimAnalyzer` + `EnableAotAnalyzer` ile derleme (ölçüm değil, sayım):

| | uyarı | nokta |
|---|---:|---:|
| önce (`a721f6c0`) | 33 | 13 |
| sonra (bu dal) | 7 | 5 |

Kaldırılanlar: `CliText.cs:39`, `DefaultAppSuggestionBar.cs:91`, `PlanParser.cs:35`,
`PresignedUploadProvider.cs:202`, `ShareResult.cs:162` ve `:209`, `ShareTargets.cs:113`,
`SingleInstanceChannel.cs:79` ve `:84`, `Strings.cs:347`, `Text.cs:56`,
`WatchFolder.cs:161` ve `:175`.

Kalanlar: `PaletteCatalog.cs:102`, `:123` (çalışma anında `ResourceInclude(Uri)`) ve
`PresetLibrary.cs:91`, `:174`, `:201` — **bu son üçü H4'ün engel listesinde hiç yok.**
`PresetLibrary` `JsonStringEnumConverter(allowIntegerValues: false)` kullanıyor; .NET 8'de
kaynak üretimiyle birlikte enum dönüştürücüsü ayrı bir iş (`JsonStringEnumConverter<T>`
enum'ların üstüne öznitelik olarak) ve önayarlar açılış yolunda değil.

## 4. Sorular

1. **≈200 ms teorik sınırı gerçekçi mi?** Ölçülen tabloda `libmpv-hazir → app-init` tek başına
   145.2 ms ve bu Avalonia'nın kendi çerçeve kuruluşu (Win32 + oluşturucu + yazı tipi yöneticisi).
   Buna süreç başlangıcı (135 ms `libmpv-hazir`'a kadar) ekleniyor. AOT süreç başlangıcını ve
   JIT kuyruğunu kısaltır, ama 145 ms'lik Avalonia bloğunun ne kadarı AOT ile gider — yoksa bu
   blok donanıma bağlı (D3D/ANGLE aygıt kurulumu) ve AOT'dan etkilenmez mi? 200 ms hedefi
   `ilk-kare` için mi savunulabilir, yoksa yalnız `main`e kadar mı?

2. **Hangi anahtar hangi riskle ne kazandırır?** Avalonia 12.1.2 + .NET 8 için
   `PublishTrimmed` (partial vs full), `TrimmerRootAssembly`, `PublishReadyToRunComposite`
   (bizde açık) ve `InvariantGlobalization` sıralamasını nasıl yaparsın? Yukarıdaki (b)'de
   ölçülen -43.5 ms'lik InvariantGlobalization kazancını, 42 dilin ilk açılış algılamasını
   kaybetmeden almanın bir yolu var mı (`PredefinedCulturesOnly`? kültür verisini kendi
   tablomuza taşımak?)?

3. **Bu uygulamada tam NativeAOT mümkün mü?** İki somut engel: (i) libmpv P/Invoke —
   `src/VidShrink.Player` libmpv-2.dll'i çalışma anında yüklüyor; (ii) Avalonia yansıması —
   kalan iki IL2026 `PaletteCatalog`'un çalışma anındaki `ResourceInclude(Uri)`'si, yani 26
   paletin çalışma anında seçilmesi. 26 paleti `x:Class` taşıyan kod-arkalı
   `ResourceDictionary`'lere çevirmek (26 × 2 dosya) bu engeli kaldırır mı, yoksa
   `AvaloniaXamlLoader.Load(object)` da AOT'da yansıma mı istiyor?

4. **Ölçüm duvarı.** (c)'deki durum şu: trim veya AOT'u ölçmek için `KayitKalkani` yerine
   uygulama içi HKCU kalkanı gerekiyor (H4 de bunu yazmış). Bunu yazmadan trim/AOT hakkında
   ölçüyle konuşmanın bir yolu var mı — örneğin kayıt defterine hiç dokunmayan bir açılış kolu
   (dosya ilişkilendirmesini bir ortam değişkeniyle atlamak) kabul edilebilir bir ölçüm
   düzeneği mi, yoksa ölçtüğü şey artık ürünün açılışı olmaz mı?
