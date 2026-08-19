---
id: T2c
title: Sabit maliyetli karmaşıklık taraması — sapmanın kalan yarısını kapat
role: builder
model: opus
depends: [T2b]
owns: [src/VidShrink.Ffmpeg/ComplexityProbe.cs, src/VidShrink.Core/ComplexityProfile.cs, tests/VidShrink.Tests/ComplexityScanTests.cs]
side_effects: []
status: open
round: 0
agent_id: —
audit: —
auditor_id: —
diff: —
verification: —
---

## Amaç

T2b pencere sapmasını paket boyutlarından ölçtü ve 180 MB hedefteki tahmin hatasını
%20,6'dan %13,3'e indirdi. Hedef %5'ti, tutmadı — ve sebebi net ölçüldü:

Gerçek pencere sapması **x264 alanında 1,191**, kaynak paketlerinden okunabilen ise
**1,065**. Kaynak ~133 Mbit/sn ile neredeyse kayıpsız kaydedilmiş; o bit hızında paket
boyutu karmaşıklığın **zayıf vekili**. Sapmanın %11,8'i ölçülemeden kalıyor.

T2b alternatifi de ölçtü: tam dosyayı `scale=480:270 / ultrafast / crf 23` ile tarayıp
pencerelerle karşılaştırmak **1,176** veriyor — gerçeğe %1,3 uzak, hatayı %5'in altına
indirir. Bedeli 52 saniyelik kaynakta 5,9 saniye; 2 saatlik videoda ~13,5 dakika.
**Bu bedel kabul edilemez.** Bu sözleşme aynı doğruluğu sabit maliyetle almanın yolunu kurar.

## Kabul kriteri

1. **Tarama sabit maliyetlidir.** Tam dosyayı çözmek yerine dosya boyunca yayılmış
   **kısa noktalardan** örneklenir: yaklaşık 40 nokta × 0,25 saniye, `scale=480:270`,
   `ultrafast`, `crf 23`, `-an`, `-f null -`. Toplam süre **kaynak uzunluğundan bağımsız**
   olmalı: 52 saniyelik ve 2 saatlik kaynakta ölçülen süre farkı 2 katı geçmesin.
   Ölç ve ikisini de Çıktı'ya yaz.
2. **Noktalar kalibrasyon pencerelerini kapsar.** Oranın anlamlı olması için
   `ComplexityProbe.Windows()`'un seçtiği pencerelere denk gelen noktalar tarama
   kümesinde de bulunmalı; sapma `ortalamaPencereNoktaları / ortalamaTümNoktalar`
   olarak hesaplanır. Pencere noktaları ile dosya noktaları **aynı kodlayıcı ve ayarla**
   ölçülür, aksi hâlde oran anlamsızdır.
3. **T2b'nin paket tabanlı ölçümü yedek olarak kalır.** Tarama başarısız olursa
   (ffmpeg hatası, iptal, anlamsız oran) paket tabanlı bias'a düşülür; o da yoksa
   düzeltme uygulanmaz. Üç kademeli düşüş açıkça görünür olmalı — hangi kademenin
   kullanıldığı `ComplexityProfile` üzerinden okunabilsin.
4. **Kırpma korunur.** Ölçülen oran `[0,5 , 2,0]` dışına çıkarsa güvenilmez sayılır.
5. **Band yalnızca hak edildiğinde daralır.** `EstimateBandFor` dar bandı (0,05) ancak
   kalibrasyon **ve** tarama tabanlı bias birlikte uygulandığında döndürür. Paket
   tabanlı yedekle ara bir band (0,08) döner. Hiçbiri yoksa bugünkü değerler.
6. **İptal edilebilir ve kesintisiz.** Tarama `CancellationToken`'a uyar; iptalde
   `ct.Register(() => TryKill(process))` ile süreç ağacı öldürülür, iki akış da
   `Task.WhenAll` ile boşaltılır. Bu kalıp `QualityMeter.cs`'de ve T2b sonrası
   `ComplexityProbe.cs`'de mevcut, aynısını kullan.
7. **Doğrulama ölçümü.** Gerçek dosyada 180 MB ve 8 MB hedefte tahmin/gerçek farkı
   ölçülür. Hedef: 180 MB'da mutlak hata **%5'in altı**. Tutmazsa **tutmadığını yaz,
   sayıyı süsleme**; kalan sapmanın nereden geldiğini ölç ve tek paragrafla açıkla.
8. Testler (`ComplexityScanTests`): nokta kümesinin kaynak uzunluğundan bağımsız sabit
   kaldığı; pencere noktalarının tarama kümesinin alt kümesi olduğu; üç kademeli düşüşün
   her kademesinin doğru bandı verdiği; kırpma sınırı dışındaki oranın uygulanmadığı.
9. **T2b'den devredilen madde.** T2b denetiminde kaldı: `ReadPacketsAsync` kaynağın
   uzunluğuna bakmaksızın her zaman dosyanın **tamamının** paketlerini okuyor,
   `-read_intervals` ile adaptif düşme yok. 52 saniyelik test dosyasında 0,56 sn sürüyor
   ama uzun veya yüksek bit hızlı kaynakta sınırsız. Bu sözleşmenin 1. maddesindeki
   "sabit maliyet" ilkesi paket okumaya da uygulanır: ölçüm süresi kaynak uzunluğundan
   bağımsız kalmalı, gerekiyorsa `-read_intervals` ile örneklenmeli. 2 saatlik kaynakta
   ölçülen paket okuma süresini Çıktı'ya yaz.
10. `dotnet build VidShrink.sln -c Release` 0 uyarı, `dotnet test VidShrink.sln` yeşil.

## Arayüzler

T2b sonrası hâl (commit'e bak, bu alanları bozma):
- `ComplexityProfile`: `CalibrationSignature`, `Calibrate/WithoutCalibration/AppliesTo`,
  `LevelFactor`, `HalvingStep`, `EstimateBandFor`, `WindowBias`.
- `ComplexityProbe`: `Windows()`, `SampleAsync(path, start, length, filter, ct)`,
  paket tabanlı bias ölçümü.
- T2b'nin bulduğu gizli hata: `Calibrate` içindeki `modelled` de pencere alanında
  ölçülmeliydi, yoksa `LevelFactor` bias düzeltmesini geri emiyor. Bu düzeltmeyi **bozma**.

## Bağlam

- Test dosyası: `C:\Users\Administrator\Videos\gothic2026-08-15 14-01-29.mp4`
  — 830 MB, 52,6 sn, 1920x1080@48, ~133 Mbit/sn. Çıktıları masaüstüne yaz.
- Uzun dosya testi için kaynağı kendin uzat (`-stream_loop`) veya lavfi ile üret;
  depoya video ekleme.
- `-hwaccel cuda` bu makinede çalışıyor ve çözmeyi %19 hızlandırıyor, kaliteyi
  etkilemez. Taramada kullanmak serbest ama **zorunlu değil** ve GPU yoksa
  sessizce CPU'ya düşmeli. Ayrıntı: `docs/gpu-kodlama-bulgusu.md`.
- Seek maliyeti gerçektir: 40 ayrı `-ss` çağrısı yerine tek bir ffmpeg çağrısında
  `select` filtresiyle örneklemek daha ucuz olabilir — ikisini de ölç, ucuz olanı seç,
  ölçtüğün sayıları Çıktı'ya yaz.
- Kod yorumu yazma.

## Doğrulama

```
dotnet build VidShrink.sln -c Release
dotnet test VidShrink.sln
```

Ek olarak Çıktı'ya: 52 sn ve ~2 saatlik kaynakta tarama süresi · ölçülen bias ·
180 MB ve 8 MB'da tahmin/gerçek/mutlak hata.

## Kayıt noktası

—

## Çıktı

—
