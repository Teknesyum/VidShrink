# Plan — Paylaşılan `recorder-settings.json` Yarışını Kapatmak

İş bitti, uygulandı; kayıt olarak duruyor. Canlı kod yolu bu dosyaya bakmıyor.

## Kusur

`RecorderView` ayar dosyasını sürecin **statik** yolundan (`RecorderSettings.FilePath`)
okuyor ve oraya yazıyor. Bu tek dosya, ölçüm sınıfları arasında bir kanal oluyor.

**Sözleşmenin öncülü bir yerde yanlış.** "xUnit sınıf düzeyinde paralel koşuyor" doğru
değil: `LanguageTests.cs:17` `[assembly: CollectionBehavior(DisableTestParallelization =
true)]` bildiriyor ve `PerformanceCheckTests.OlcumArtikBirakmiyor` bunu pimliyor. İki sınıf
aynı anda koşmuyor, dolayısıyla bu bir iş parçacığı yarışı değil. Kanalı açan şey
**ertelenmiş kuyruk işi**: `PersistChoices` arayüz olaylarıyla çağrılıyor ve iş tek
Avalonia arayüz iş parçacığının kuyruğunda bekliyor. Tek kök, iki yüz:

1. **Sızıntı (kaynak).** Bekleyen yazma, kapının `finally`'si dosyayı geri koyduktan
   **sonra** boşalıyor. `KaydediciArayuzTests.GelismisKollarIstegeVeAyaraGecer` kapının
   içinde olmasına rağmen paylaşılan dosyada `scaleWidth: 1280` bırakıyor
   (`docs/olcumler/aot-dalgasi.md` 12. bölüm).
2. **Okuma (kurban).** O iz bir sonraki sınıfın kapı penceresinde okunuyor. Gif kabı ve
   `livePreview: true` sızdığında Gif'te `MaxMegabytes` null kaldığı için önizleme yolu
   `RecorderView.Onizleme.cs:24-27` kuralına takılmıyor ve
   `KaydediciOnizlemeTests.KutuIsaretliyseIstegeYolGirerResimOkunurBozukKareEskisiniKorur`
   `Assert.Null` ile düşüyor. main CI `35302890518`, kod değişmeden ikinci koşumda yeşil.

## Seçilen yol: ayar yolu açıkça verilir (2. yol)

1. yol (kapının boşaltma/geri-yazmasını gövdeyle aynı iş parçacığına almak) belirtiyi
   örter, kaynağı kapatmaz. Kapı atomik olursa boşaltma kuyruktaki yabancı işten
   **sonraya** düşer, o yüzden kapılı ölçümler korunur — ama ertelenmiş `PersistChoices`
   hâlâ statik yola yazıyor, yani dosyada iz kalmaya devam eder. Kapı **hiç
   kullanılmayan** 12 görünüm kurulumu da o izi okumaya devam eder
   (`KaydediciArayuzTests` 169, 190, 285, 293, 295, 403, 438, 486, 638, 732, 815, 825,
   851; `KaydediciHedefTests` 147) — aot-dalgasının kurbanı `KaydediciPencereTests` tam
   böyle bir okumaydı.

Yol, yolu **örneğe** bağlar: görünüm kurulurken yolunu alır ve ertelenmiş yazma da o yola
düşer. Paylaşım biter, kanal kalmaz.

## Adımlar

1. `RecorderView`: `private readonly string? _settingsPath`. Parametresiz kurucu
   `: this(RecorderSettings.FilePath)` olarak kalır — `MainWindow.TembelSekme.cs:25`
   satırı ve onu metin olarak pimleyen `HipersurusTests.cs:150` değişmez.
   Dokuz `RecorderSettings.FilePath` kullanımı `_settingsPath` olur.
2. `KaydediciAyarTests.AyarDosyasiyla<T>(Func<T>)` → `Func<string, T>`: her çağrıya
   **özel** bir dosya yolu üretir, `finally`'de siler. Kilit ve geri-yazma gereksiz
   kalır, kaldırılır.
3. On test dosyasındaki 31 kapı çağrısı ve 38 `new RecorderView()` kurulumu yolu alır.
   Kapısız kurulumlar da kapıya alınır.
4. Dosyayı statik yoldan okuyan test yardımcıları (`DosyadakiDeger` ve benzerleri,
   5 dosya) yolu parametre olarak alır.
5. `TestAyarYoluTests.KaydediciAyariCalismaAltinaYazilirAppDataDegismez` **statik yolda
   kalır** — ölçünün konusu tam olarak `VIDSHRINK_SETTINGS_PATH` sözleşmesi. Bıraktığı
   `targetMegabytes: 37` artık kimseye ulaşmıyor, çünkü başka hiçbir sınıf o dosyayı
   okumuyor.

## Ölçü

Yeni sınıf `KaydediciAyarYalitimTests`, iki pim:

- **Yarış pimi.** Kapının içinde, görünüm kurulmadan hemen önce paylaşılan dosyaya
  başka sınıfın gövdesi taklit edilerek Gif + `livePreview: true` yazılır. Düzeltmeden
  önce yeni görünüm o dosyayı okuyup önizleme yolunu kurar (kırmızı), sonra kendi
  dosyasını okur (yeşil). Paylaşılan dosya ölçümden önce yedeklenip sonra geri konur.
- **Sızıntı pimi.** Özel yolla kurulan görünüme 1280x720 yazılıp `PrepareRecording()`
  ile eşzamanlı kaydettirilir; paylaşılan dosyanın baytları **değişmemiş**, özel
  dosyada 1280 **var** olmalı.

Koşulacak sınıflar: `KaydediciAyarYalitimTests`, `KaydediciOnizlemeTests`,
`KaydediciAyarTests`, `KaydediciArayuzTests`, `KaydediciGirdiTests`,
`KaydediciHedefTests`, `KaydediciKameraTests`, `KaydediciSeciciTests`,
`KaydediciTamponTests`, `KaydediciArkaPlanTests`, `KaydediciPencereTests`,
`TestAyarYoluTests`, `HipersurusTests`.
