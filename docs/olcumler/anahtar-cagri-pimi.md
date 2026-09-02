# T148 — Yerelleştirme ölçüsü çağrı yerlerini okumaya başladı

**Tarih:** 02.09.2026 · **Sözleşme:** `.claude/relay/contracts/T148.md` ·
**Dal:** `T148-anahtar-cagri-pimi` · **Taban:** `origin/main` = `d74a57f`

Bu sözleşme kod düzeltmedi. `tests/VidShrink.Tests/LocalizationTests.cs` içine
beş ölçü ekledi; ölçülen şey, Serkan'ın 2 Eylül'de gerçek Mac'te gördüğü
kusur sınıfının neden statik olarak yakalanmadığı.

Sözleşme düzeltmenin `b692684` ile girdiğini yazıyor; o commit `.DS_Store`
temizliği. Üç anahtar düzeltmesi `b2f2c62`, `main`e `a2b9664` birleştirmesiyle
girdi. Çağrı yerleri de `MainWindow.axaml.cs:1893/1895/1980`, sözleşmedeki
2054/2056/2140 değil.

## Ortam

| Alan | Değer |
|---|---|
| Makine | Windows 11 Pro 22631 |
| .NET | 8.0, `dotnet build -c Release --no-incremental` (her mutasyonda yeniden) |
| Ölçülen ikili | `tests/.../bin/Release/net8.0/VidShrink.App.dll` |
| Verify kolu | `dotnet test -c Release --filter "LocalizationTests"` |

`--no-build` hiçbir adımda kullanılmadı; her mutasyon kendi tam derlemesiyle
koştu. Ağır ffmpeg işi başlatılmadı, tam süit yerelde koşturulmadı (sözleşme
yasakladı) — tamamı CI'ya bırakıldı.

## K1 — Kusur önce ölçüldü

Kusur kolu, olayın kendisiyle birebir aynı durumu üretir: üç anahtar **iki
dilden de** silindi (`main.quality.target`, `main.quality.loss-points`,
`main.plan.fact.estimated-size`). Çağrı yerlerine dokunulmadı; kümeler eşit
kaldığı için bugünkü ölçüler bunu görmemeli.

Bugünkü on test, üç anahtar iki dilde de yokken:

```
Başarılı!  - Başarısız:     0, Başarılı:    10, Atlanan:     0, Toplam:    10, Süre: 46 ms - VidShrink.Tests.dll (net8.0)
```

Aynı ağaçta yeni ölçü koşunca:

```
[xUnit.net 00:00:21.24]     VidShrink.Tests.LocalizationTests.KodunCagirdigiHerAnahtarIkiKatalogdaDaVar [FAIL]
  Hata İletisi:
   Çağrılan ama olmayan anahtar:
'main.plan.fact.estimated-size' anahtarı en ve tr kataloğunda yok; 1 çağrı yeri, ilki VidShrink.App.MainWindow::RefreshPlanView -> VidShrink.App.MainWindow::Say.
'main.quality.loss-points' anahtarı en ve tr kataloğunda yok; 1 çağrı yeri, ilki VidShrink.App.MainWindow::QualityBody -> VidShrink.App.MainWindow::Say.
'main.quality.target' anahtarı en ve tr kataloğunda yok; 1 çağrı yeri, ilki VidShrink.App.MainWindow::QualityBody -> VidShrink.App.MainWindow::Say.
Başarısız! - Başarısız:     1, Başarılı:    14, Atlanan:     0, Toplam:    15, Süre: 3 s
```

Üç kırmızı, üç anahtar, tek ölçüde. Anahtarlar geri konunca kol yeşile döndü
(K4). Kusur kolu commit'e girmedi.

## K2 — Kapalı küme türden çıkarıldı

Sayım metin deseniyle değil, iki adımda türden çıkarılıyor:

1. **Tohum:** `VidShrink.App.Localization` ad alanının `string key` alan her
   üyesi. Yansımayla bulunuyor, uydurulmuyor.
2. **Kapı:** tohuma varan çağrı zincirindeki sarmalayıcılar. `string key`
   parametreli her üye aday; adayın gövdesi bilinen bir kapıyı çağırıyorsa
   kapı olur. Sabit noktaya kadar tekrarlanır, dolayısıyla sonlanır.

Çağrı yerleri kaynak metninden değil **derlenmiş IL'den** okunuyor
(`System.Reflection.Metadata`, `ldstr` + `call`/`newobj`). Bunun tek sebebi
şu: Avalonia biçimlemeyi aynı derlemeye IL olarak derliyor, yani
`{loc:Text ...}` anahtarları da bu taramanın içinde.

```
Tohum (VidShrink.App.Localization, 'string key' alan uye): 5 ad, 7 asiri yukleme
  VidShrink.App.Localization.LocalizedText::.ctor
  VidShrink.App.Localization.LocalizedText::For
  VidShrink.App.Localization.Strings::Get
  VidShrink.App.Localization.Strings::GetIn
  VidShrink.App.Localization.TextExtension::.ctor
Aday ('string key' parametreli her uye): 25
Kapi (tohuma varan cagri zinciri): 11
  VidShrink.App.Localization.LocalizedText::.ctor
  VidShrink.App.Localization.LocalizedText::For
  VidShrink.App.Localization.Strings::Get
  VidShrink.App.Localization.Strings::GetIn
  VidShrink.App.Localization.TextExtension::.ctor
  VidShrink.App.MainWindow::<FastGpuVerdictLine>g__Line|103_0
  VidShrink.App.MainWindow::Say
  VidShrink.App.MainWindow::Speak
  VidShrink.App.Playback.ComparisonPanel::Text
  VidShrink.App.Playback.ControlStrip::Text
  VidShrink.App.Playback.PanelHost::PlaybackText
```

25 adayın 14'ü kapı değil: `Look`, `Motion`, `Paint`, `Scalar`, `Fill`,
`Inset`, `Delay` Avalonia kaynak anahtarı arıyor
(`TryFindResource`), `DeferredEncoderAvailability::Ready` ve `::Measure`
kodlayıcı önbelleği anahtarı taşıyor. Hiçbiri kataloğa gitmiyor; kapı
listesine parametre adına bakılarak girselerdi ölçü 14 sahte yol taşıyacaktı.

### XAML tarafı anahtar tüketiyor — evet, hem de en çok oradan

**Sözleşmedeki "bugün `.axaml` dosyalarında `Say("` geçmiyor (T0 ölçtü, 0
eşleşme)" öncülü yanlış bir sonuca götürüyor.** `Say(` gerçekten geçmiyor,
ama biçimlemenin kendi bağlama biçimi var: `Localization/Text.cs` içindeki
`TextExtension`, yani `{loc:Text anahtar}`. Kaynakta 196 geçiş var ve derlenmiş
IL'de tam olarak 196 `TextExtension::.ctor` çağrı yeri duruyor — uygulamanın
**en kalabalık** tüketim yolu bu, `Say`'in 141'inden fazla.

| Kapı | Çağrı yeri | Sabit anahtarsız |
|---|---:|---:|
| `TextExtension::.ctor` (XAML `{loc:Text}`) | 196 | 0 |
| `MainWindow::Say` | 141 | 0 |
| `Strings::Get` | 40 | 6 |
| `MainWindow::Speak` | 23 | 1 |
| `Strings::GetIn` | 10 | 7 |
| `MainWindow::<FastGpuVerdictLine>g__Line\|103_0` | 7 | 0 |
| `ComparisonPanel::Text` | 6 | 1 |
| `ControlStrip::Text` | 5 | 0 |
| `PanelHost::PlaybackText` | 5 | 2 |
| `LocalizedText::For` | 1 | 1 |
| `LocalizedText::.ctor` | 1 | 1 |
| **Toplam** | **435** | **19** |

`loc:Bullets.Text` ayrı bir yol değil: değeri yine `{loc:Text ...}` veriyor
(`MainWindow.axaml:289` ve devamı), yani aynı 196'nın içinde.

### Kör nokta: sabit olmayan anahtar

19 sabit-anahtarsız çağrı yerinin 13'ü kapıların kendi gövdesinde: `Say`
kendi `key` parametresini `Strings.Get`'e aktarıyor, bu bir kör nokta değil,
aynı anahtarın ikinci kez sayılması. Geriye **6 gerçek kör nokta** kalıyor:

```
  VidShrink.App.LanguageCatalog::Validation -> VidShrink.App.Localization.Strings::Get
  VidShrink.App.LanguageCatalog::Validation -> VidShrink.App.Localization.Strings::Get
  VidShrink.App.MainWindow::LocalizeStage -> VidShrink.App.Localization.Strings::Get
  VidShrink.App.Playback.ComparisonPanel::RefreshTexts -> VidShrink.App.Playback.ComparisonPanel::Text
  VidShrink.App.Playback.PanelHost::SampleFailureText -> VidShrink.App.Playback.PanelHost::PlaybackText
  VidShrink.App.Playback.PanelHost::SampleFailureText -> VidShrink.App.Playback.PanelHost::PlaybackText
```

Altısı da anahtarı bir diziden ya da bir alandan alıyor
(`MainWindow.axaml.cs:3071` `StageWords`, `LanguageCatalog.cs:197`
`ValidationPatternKeys`, `EncodeFailure.Key`). **Statik ölçü bu altı çağrı
yerinin hangi anahtara gittiğini göremez.** Ölçü bu yüzden ikinci bir küme
daha topluyor: derlemenin tamamındaki anahtar biçimli dizeler (aşağıda),
böylece dizide duran anahtar "hiç kullanılmıyor" sanılmıyor.

Ölçünün göremediği ikinci şey: `string key` yerine başka adla parametre alan
bir sarmalayıcı yazılırsa aday kümesine girmez. Bugün öyle bir üye yok;
`AnahtarTuketenKapilarKaynaktakiBildirimlerleAyni` ölçüsü kapı listesi
değişince kırmızı verir.

## K3 — İki yönün kararı

| Yön | Karar | Bugünkü sayı |
|---|---|---:|
| 1. Çağrılan ama katalogda olmayan | **kırmızı** — `KodunCagirdigiHerAnahtarIkiKatalogdaDaVar` | 0 |
| 1b. Çağrı yerine bağlanamayan, derlemede geçen anahtar biçimli dize | **kırmızı** — `CagriYerineBaglanamayanAnahtarBicimliDizelerDeKatalogdaVar` | 0 |
| 2. Katalogda var, hiçbir çağrı yerine bağlanmadı | **kırmızı yapılmadı** | 34 |
| 2b. Katalogda var, derlemenin hiçbir yerinde geçmiyor | **kırmızı** — `KatalogdaBirikenOluCeviriListesiBuyumuyor` | **1** |

Yön 2'nin ham hâli (34) kırmızı yapılamaz, çünkü sayının 33'ü meşru: dizide
ya da sözlükte duran, çalışma anında seçilen anahtarlar. Ham liste:

```
Cagri yerine baglanamayan ama derlemede gecen: 33
  main.drop.no-folder, main.drop.release, main.drop.single
  main.stage.attempt, main.stage.converting, main.stage.encoding,
  main.stage.gif-encode, main.stage.gif-palette, main.stage.pass
  main.validation.container-audio-copy, main.validation.container-audio-encoder,
  main.validation.container-video-copy, main.validation.container-video-encoder,
  main.validation.copy-fixed, main.validation.end-before-start,
  main.validation.end-zero, main.validation.fps-zero, main.validation.gif-copy,
  main.validation.no-audio-copy, main.validation.no-audio-extract,
  main.validation.size-even, main.validation.size-positive,
  main.validation.start-negative, main.validation.start-past-end,
  main.validation.trim-format, main.validation.trim-order
  playback.error.engine, playback.error.exit-code, playback.error.no-file,
  playback.error.window, playback.panel.first-frame, playback.panel.pending
  settings.update.no-self-effect
```

Bu 33'ü çıkarınca **tek bir gerçek ölü anahtar** kalıyor:

```
Yon 2b - katalogda var, derlemenin hicbir yerinde yok (OLU): 1
  main.plan.reasons-count
```

`main.plan.reasons-count` iki katalogda da duruyor (`main.json:118`), koda
`b976332` (T83) ile girdi ve o günden beri hiçbir yerden çağrılmadı — doğduğu
gün ölüydü. **Silinmedi:** sözleşmenin K4 maddesi "gerçek bir kusur bulursan
bildir, kendin düzeltme" diyor. Bunun yerine ölçünün `KnownDead` listesine
yazıldı; liste iki yönlü çalışıyor, yani anahtar kullanılmaya başlarsa ya da
katalogdan çıkarsa ölçü yine kırmızı verir ve pim bayatlamaz.

Yön 1b'nin bilinen riski: ilk parçası bir alan adıyla (`main`, `playback`,
`performance`, `settings`) çakışan ama anahtar olmayan bir dize — örneğin
`"settings.json"` — sahte kırmızı verir. `VidShrink.App` derlemesinde bugün
böyle bir dize yok (389 aday, 0 sahte).

## K4 — `main` tabanında yanlış pozitif yok

Anahtarlar yerine konduktan sonra, `origin/main` içeriğiyle:

```
Başarılı!  - Başarısız:     0, Başarılı:    15, Atlanan:     0, Toplam:    15, Süre: 1 s - VidShrink.Tests.dll (net8.0)
```

İlk taslak üç sahte anahtar üretmişti (`0.0`, `0.00`,
`paylasim-hedefleri.json`): biçim dizeleri ve bir dosya adı, anahtar
şekline uyduğu için çağrı yerine yapışmıştı. Aday dizeler kataloğun kendi
alan öneklerine daraltılınca üçü de düştü; sayı uydurulmadı, kataloğun ilk
parçalarından üretiliyor.

Bulunan gerçek kusur: yukarıdaki tek ölü anahtar. Üretim kodunda kırık anahtar
kalmadı.

## K5 — Mutasyon ızgarası

Beş mutasyon, teker teker, her biri kendi `dotnet build -c Release
--no-incremental` derlemesiyle. Sütunlar yeni beş ölçü; `E` sütunu bugünkü on
testten etkilenen.

| Mutasyon | 1 Çağrılan⊄katalog | 1b Dize⊄katalog | 2b Ölü | Kapı pimi | 5c Kaynak alt sınırı | E (eski) |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
| (a) `main.quality.predicted` yalnız `tr`'den silindi | **KIRMIZI** | yeşil | yeşil | yeşil | yeşil | **KIRMIZI** (küme eşitliği) |
| (b) `MainWindow.axaml` çağrı yeri bozuldu (`main.tagline.lead` → `leed`) | **KIRMIZI** | yeşil | **KIRMIZI** | yeşil | yeşil | yeşil |
| (c) küme üretimi boş döndürüldü (`Grow` yerine boş küme) | yeşil | yeşil | yeşil | yeşil | **KIRMIZI** | yeşil |
| (d) `Strings`'e yeni kapı eklendi (`Ask(string key)`) | yeşil | yeşil | yeşil | **KIRMIZI** | yeşil | yeşil |
| (e) dizideki anahtar bozuldu (`main.stage.pass` → `pas`) | yeşil | **KIRMIZI** | **KIRMIZI** | yeşil | yeşil | yeşil |

Beş ölçünün beşi de en az bir mutasyonda kırmızı verdi; ölü kol yok.

İki mutasyon iki ölçüyü birden kırdı ve ikisi de doğru:
(b)'de anahtar hem çağrılıp bulunamıyor hem de eski anahtar ölü kalıyor;
(e)'de aynısı dizi tarafında. (a) bugünkü küme eşitliği ölçüsünü de kırıyor —
beklenen, çünkü anahtar gerçekten yalnız `tr`'den silindi.

(c) ölçünün kendi kör kalmasını yakalıyor. Ham çıktısı:

```
[xUnit.net 00:00:08.37]     VidShrink.Tests.LocalizationTests.OlcuKaynaktaGorunenCagriYerlerininTamaminiBuluyor [FAIL]
  Hata İletisi:
   Kaynakta görünen 281 anahtarın 281 tanesi ölçünün çağrı yeri kümesinde yok; ölçü kör kalmış:
Başarısız! - Başarısız:     1, Başarılı:    14, Atlanan:     0, Toplam:    15
```

Bu kolun dayanağı bağımsız bir alt sınır: `*.axaml` dosyalarındaki 196
`{loc:Text ...}` geçişi ve `MainWindow.axaml.cs` içindeki 137 `Say("...")`
geçişi, birlikte 281 ayrı anahtar. IL'den çıkan küme bunun üst kümesi olmak
zorunda. Sayı toplam iddiası için kullanılmıyor — yalnız "ölçü boş dönerse
kırmızı olsun" diye.

## K6 — Verify kolu gerçekten test buluyor

```
$ dotnet test -c Release --filter "LocalizationTests" --list-tests   # taban
    ... 10 test
$ dotnet test -c Release --filter "LocalizationTests" --list-tests   # teslim
    ... 15 test
```

10 → 15. Fark 5, eklenen ölçü sayısı 5:

| # | Ölçü |
|---|---|
| 1 | `KodunCagirdigiHerAnahtarIkiKatalogdaDaVar` |
| 2 | `CagriYerineBaglanamayanAnahtarBicimliDizelerDeKatalogdaVar` |
| 3 | `KatalogdaBirikenOluCeviriListesiBuyumuyor` |
| 4 | `AnahtarTuketenKapilarKaynaktakiBildirimlerleAyni` |
| 5 | `OlcuKaynaktaGorunenCagriYerlerininTamaminiBuluyor` |

Filtre `FullyQualifiedName~LocalizationTests` olarak çalışıyor ve sıfır teste
denk gelmiyor; sayı `--list-tests` ile sayıldı, tahmin edilmedi.

## Kapsam dışı kalanlar

- **T125 tur 2'nin kaydettiği çelişki bu ölçüyle bulunamaz.** Çeviri metni bir
  kodek adı sayarken üretimin başka bir kodek seçmesi: anahtar var, çeviri var,
  çağrı yeri doğru; yanlış olan **metnin içeriği**. Buradaki beş ölçünün hiçbiri
  metne bakmıyor, yalnız anahtarın varlığına bakıyor.
  Not: sözleşmenin verdiği satırlar `d74a57f` üzerinde tutmuyor —
  `Locales/en/main.json:293` `main.reason.target-capped`, `PlanCalculator.cs:762-767`
  ölçek adayları döngüsü; ikisinde de kodek adı geçmiyor. Örneği yeniden
  konumlandırmadım, kapsam dışı.
- Sabit olmayan anahtar taşıyan 6 çağrı yeri (yukarıda listelendi).
- `en` ile `tr` metinlerinin biçim argümanı sayısının tutup tutmadığı
  ölçülmüyor.
