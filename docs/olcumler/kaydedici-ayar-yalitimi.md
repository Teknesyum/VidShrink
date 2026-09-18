# Kaydedici Ayar Dosyasının Yalıtımı

Dal `t0/kaydedici-ayar-yarisi`. Kapatılan kusur: `RecorderView`'ın süreç başına tek olan
`recorder-settings.json` üzerinden ölçüm sınıflarını birbirine karıştırması.
`docs/olcumler/aot-dalgasi.md` 12. bölümün "kaynak açık" bıraktığı borç burada kapandı.

## Mekanizma — sözleşmenin öncülü düzeltildi

"xUnit sınıf düzeyinde paralel koşuyor" **doğru değil**: `tests/VidShrink.Tests/LanguageTests.cs:17`
`[assembly: CollectionBehavior(DisableTestParallelization = true)]` bildiriyor ve
`PerformanceCheckTests.OlcumArtikBirakmiyor` bunu pimliyor. İki sınıf aynı anda koşmuyor,
dolayısıyla bu bir **iş parçacığı yarışı değil**.

Kanalı açan şey **ertelenmiş kuyruk işi**: `PersistChoices` Avalonia'nın `TextChanged` /
`SelectionChanged` / `IsCheckedChanged` olaylarıyla çağrılıyor, iş tek arayüz iş
parçacığının kuyruğunda bekliyor ve ölçümün kapısı `finally` ile kapandıktan **sonra**
boşalabiliyor. Bu yüzden kapının boşaltma/geri-yazmasını gövdeyle aynı iş parçacığına almak
(sözleşmenin 1. yolu) kaynağı kapatmıyor — yol örneğe bağlandı (2. yol).

`Dispatcher.UIThread.RunJobs()` bilerek kullanılmadı: CI `35296182516` iki koşumda
`OynaticiCiftTikSuresiTests`'i kırdı, çünkü yabancı kuyruk işini de boşaltıyor. Düzenek
kaydı eşzamanlı `PrepareRecording()` ile tetikliyor.

## Yarışı kasıtlı üreten düzenek

`tests/VidShrink.Tests/KaydediciAyarYalitimTests.cs`, iki pim, ikisi de pozitif denetimli:

- `BaskaGovdeninPaylasilanDosyayaYazdigiAyarOlcumeGecmez` — **okuma yönü**. Tek
  `AppHost.Run` içinde gerçek bir `new RecorderView()` (yol verilmemiş, yani paylaşılan
  dosyaya yazan gövde) Gif kabı + `livePreview: true` yazıp `PrepareRecording()` ile
  kaydediyor; hemen ardından `new RecorderView(ayarYolu)` ölçülüyor. Gif'te
  `MaxMegabytes` null kaldığı için `RecorderView.Onizleme.cs:24-27` kuralı önizleme yolunu
  düşürmüyor — CI'da kırmızıyı üreten bileşke tam bu. Paylaşılan dosyanın baytları ölçümden
  önce alınıp sonra geri konuyor.
- `GorunumYalnizKendisineVerilenDosyayaYazar` — **yazma yönü**. Özel yolla kurulan
  görünüme 1280x720 yazılıp eşzamanlı kaydettiriliyor; ölçek kendi dosyasında olmalı,
  paylaşılan dosyaya girmemeli.

Mutasyon: `RecorderView.axaml.cs`'te tek satır, `_settingsPath = settingsPath;` →
`_settingsPath = RecorderSettings.FilePath;`. Kaynak elle geri yazıldı (`git checkout`
kullanılmadı), `git diff` boş.

### Önce (mutasyon yerinde)

```
[xUnit.net 00:00:06.07]     VidShrink.Tests.KaydediciAyarYalitimTests.GorunumYalnizKendisineVerilenDosyayaYazar [FAIL]
[xUnit.net 00:00:06.18]     VidShrink.Tests.KaydediciAyarYalitimTests.BaskaGovdeninPaylasilanDosyayaYazdigiAyarOlcumeGecmez [FAIL]
  Başarısız VidShrink.Tests.KaydediciAyarYalitimTests.GorunumYalnizKendisineVerilenDosyayaYazar [387 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
Expected: "1280"
Actual:   null
  Başarısız VidShrink.Tests.KaydediciAyarYalitimTests.BaskaGovdeninPaylasilanDosyayaYazdigiAyarOlcumeGecmez [105 ms]
  Hata İletisi:
   Assert.False() Failure
Expected: False
Actual:   True
Başarısız! - Başarısız:     2, Başarılı:     0, Atlanan:     0, Toplam:     2, Süre: 491 ms - VidShrink.Tests.dll (net8.0)
```

İkinci düşüş CI'nın biçiminin aynısı: kutu `True` dönüyor, çünkü yabancı gövdenin
`livePreview: true`'sü okunuyor.

### Sonra (düzeltme yerinde)

```
Başarılı!  - Başarısız:     0, Başarılı:     2, Atlanan:     0, Toplam:     2, Süre: 545 ms - VidShrink.Tests.dll (net8.0)
```

## `GelismisKollarIstegeVeAyaraGecer` borcu

`aot-dalgasi.md` 12. bölüm bu kolun paylaşılan dosyada `scaleWidth: 1280` bıraktığını
ölçmüştü. Aynı yöntemle yeniden ölçüldü — ayar klasörü sınıf koşumundan sonra inceleniyor:

- Düzeltmeden önce: klasörde `recorder-settings.json` var ve içinde
  `"containerChoice": "Mov"`, `"scaleWidth": 1280`, `"scaleHeight": 720`.
- Düzeltmeden sonra: `KaydediciArayuzTests` 86/86 yeşil koştuktan sonra klasörde
  **hiç `recorder-settings.json` yok**. 1280x720 yazması kolun kendi özel dosyasına
  düşüyor, kimse okumuyor.

Arada bir tur, ertelenmiş `PersistChoices`'ın `OzelAyar.Dispose()`'dan sonra dosyayı
yeniden yaratması yüzünden geride beş `recorder-settings-olcu-<guid>.json` bırakıyordu.
`OzelAyar.Dispose` artık sahibi kapanmış ölçüm dosyalarını da topluyor (canlı yollar bir
kümede tutuluyor, açık bir kapının dosyasına dokunulmuyor); aynı koşumdan sonra klasör
tamamen boş.

## Koşulan sınıflar

Filtre sınıf adını eşleştirdiği için her kol adıyla ve sayısıyla yazıldı.

Tablonun şartı: her kol **`VIDSHRINK_LIBMPV` verilmiş** bir kabukta koştu. Bu değişken
olmadan `TestAyarYoluTests` kırmızı döner ve tablo tutmaz — kod kusuru değil ortam eksiği.

```powershell
$env:VIDSHRINK_LIBMPV = "C:\Users\Administrator\Desktop\Projeler\VidShrink\tools\libmpv\libmpv-2.dll"
dotnet test tests/VidShrink.Tests -c Release --filter "FullyQualifiedName~Kaydedici"
```

| Sınıf | Test |
| --- | --- |
| `KaydediciAyarYalitimTests` | 3 |
| `KaydediciOnizlemeTests` | 3 |
| `KaydediciAyarTests` | 13 |
| `KaydediciArayuzTests` | 86 |
| `KaydediciGirdiTests` | 8 |
| `KaydediciHedefTests` | 9 |
| `KaydediciKameraTests` | 15 |
| `KaydediciSeciciTests` | 8 |
| `KaydediciTamponTests` | 4 |
| `KaydediciArkaPlanTests` | 6 |
| `KaydediciPencereTests` | 5 (+1 atlandı) |
| `TestAyarYoluTests` | 4 |
| `HipersurusTests` | 7 |

Toplam 171 geçti, 1 atlandı. Hiçbir kol sıfır teste denk gelmedi.

Sayı 169'dan 171'e çıktı ve tablo 2026-09-18'de yeniden sayıldı: kapının kendi pimi
(`KapiKapandiktanSonraBekleyenYazmaDosyayiKirletmiyor`) eklendi, `KaydediciHedefTests` de bir ölçü
kazanmış. Satırlar tek tek koşularak sayıldı, toplamdan geriye dağıtılmadı.

## Yalıtımın dışında bırakılan

`TestAyarYoluTests` **bilerek** statik yolda kalıyor; ölçünün konusu tam olarak
`VIDSHRINK_SETTINGS_PATH` sözleşmesi. Bıraktığı değer artık kimseye ulaşmıyor, çünkü başka
hiçbir sınıf o dosyayı okumuyor.
