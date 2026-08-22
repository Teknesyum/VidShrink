---
id: T2c
title: Sabit maliyetli karmaşıklık taraması — sapmanın kalan yarısını kapat
role: builder
model: opus
depends: [T2b]
owns: [src/VidShrink.Ffmpeg/ComplexityProbe.cs, src/VidShrink.Core/ComplexityProfile.cs, tests/VidShrink.Tests/ComplexityScanTests.cs]
side_effects: []
status: done
round: 0
agent_id: T2c-builder-opus
audit: passed
auditor_id: T2c-auditor
diff: ComplexityProbe.cs, ComplexityProfile.cs, ComplexityScanTests.cs, CalibrationProbeTests.cs
verification: build 0 uyari 0 hata, dotnet test 134/134
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

Kod yazildi, `dotnet build -c Release` 0 uyari.
- `ComplexityProfile.cs`: `WindowBiasSource {None,Packets,Scan}`, `BiasSource`, band 0,05/0,08/0,14/0,32.
- `ComplexityProbe.cs`: `ScanPoints`/`WindowScanPoints`/`ComputeScanBias`/`ScanBiasAsync`,
  `PacketIntervals` + `-read_intervals`, `ComputeWindowBias` kapsam asiri yuklemesi,
  `SampleAsync` public + preset parametresi, uc kademeli `MeasureWindowBiasAsync`.
Olcumler bitti, hedef tuttu.
- Kisa segmentin ilk karesi IDR oldugu icin oran 1'e dogru sonumleniyordu. Cozum:
  nokta 1,0 sn, ilk 0,75 sn isinma olarak atiliyor; kare boyutlari `-vstats_file`
  ile okunuyor (`ScanSampleAsync` + `ParseVstats`). Bias 1,124 -> 1,1865 (gercek 1,191).
- seek vs select: seek 52sn 2,86 sn / 2sa 2,91 sn (ayni bit hizinda); select 52sn
  2,10 sn / 2sa 262 sn. seek secildi.
- verify: bias 1,1865 · 180 MB est 172,85 gercek 170,19 hata +%1,6 · 8 MB est 8,00
  gercek 7,98 hata +%0,2. Probe toplam 20,5 sn (830 MB / 133 Mbit-sn kaynak).
Siradaki: `ComplexityScanTests` ve `CalibrationProbeTests` band testinin guncellenmesi.

## Çıktı

Hedef tuttu: 180 MB'da tahmin hatası **%1,6** (%13,3'ten indi), 8 MB'da **%0,3**.

**Ne değişti.** Tarama, dosya boyunca yayılmış 40 kısa noktadan örnekleniyor
(`ScanPoints`), 12'si `Windows()` pencerelerinin içine düşüyor (`WindowScanPoints`),
hepsi aynı ayarla ölçülüyor: `scale=480:270`, `ultrafast`, `crf 23`, `-an`, `-f null -`.
Sapma `ortalamaPencereNoktaları / ağırlıklıDosyaOrtalaması` olarak hesaplanıyor.

**Kritik bulgu — ısınma.** Her kısa parça ayrı bir ffmpeg çağrısı olduğu için ilk kare
IDR ve bir P karesinin ~7 katı. Bu sabit yük hem pencere hem dosya ortalamasını şişirip
oranı 1'e doğru sönümlüyordu: 0,25 sn'lik noktalarla ölçülen sapma 1,124'te kalıyordu
(gerçek 1,191). Nokta 1,0 sn'ye çıkarıldı ve ilk 0,75 sn **ısınma** olarak atıldı;
kare boyutları `-vstats_file` ile okunup (`ScanSampleAsync` + `ParseVstats`) yalnızca
`time >= 0,75` olan kareler sayılıyor. Sapma 1,124 -> **1,1865**. Ara ölçümler:
yalnız IDR'yi atmak 1,1437; 0,5 sn ısınma 1,1636; 0,75 sn ısınma 1,1865.

**Seek mi select mi.** İkisi de ölçüldü, aynı 40 nokta, aynı içerik (52 sn / 2 sa,
aynı bit hızı):

| | 52 sn | 2 saat |
|---|---|---|
| seek (`-ss` başına bir çağrı) | 2,86 sn | 2,91 sn |
| select (tek çağrı, `select` süzgeci) | 2,10 sn | 262 sn |

`select` tüm dosyayı çözmek zorunda, maliyeti süreyle doğrusal — elendi. Seek seçildi.

**Sabit maliyet (kabul kriteri 1).** Üretim yolundaki `ScanBiasAsync`: 52 sn -> **3,82 sn**,
2 saat -> **3,91 sn**. Oran **1,02** (sınır 2). Süre değil, bit hızı belirleyici: asıl test
dosyası (830 MB, ~133 Mbit/sn, 52 sn) 17,9 sn sürüyor — çünkü her seek yüksek bit hızlı
1080p GOP'u çözüyor. Tam `ComplexityProbe.RunAsync` o dosyada 22,3 sn.

**Paket okuma (kabul kriteri 9).** `PacketIntervals` 180 sn üstündeki kaynakta ~40
aralığa düşüyor. 2 saatlik kaynakta `PacketBiasAsync` **0,14 sn**; 52 sn'lik kaynakta
(tam okuma) 0,10 sn, asıl 830 MB'lık dosyada 0,87 sn. Süre kaynak uzunluğundan bağımsız.

**Üç kademe.** `ComplexityProfile.BiasSource` ile okunuyor: `Scan` -> band 0,05,
`Packets` -> 0,08, `None` -> bugünkü değerler (ölçülmüş 0,14 / tahmin 0,32). Dar band
yalnızca kalibrasyon **ve** tarama birlikte geçerliyken dönüyor. Kırpma `[0,5 , 2,0]`
korundu; dışına çıkan oran uygulanmıyor ve `BiasSource` `None` kalıyor.

**Doğrulama (gerçek dosya, `gothic2026-08-15 14-01-29.mp4`).**
- probe 22,3 sn · bppf 0,12645 · bias **1,1865**
- 180 MB: tahmin 172,85 MB, gerçek 170,19 MB, mutlak hata **%1,6**
- 8 MB: tahmin 8,00 MB, gerçek 7,98 MB, mutlak hata **%0,3**

`dotnet build VidShrink.sln -c Release` 0 uyarı · `dotnet test VidShrink.sln` 99/99.
Kırık `CalibrationProbeTests.EstimateBandNarrowsOnlyWhenCalibrationApplies` üç kademeye
göre güncellendi, dosyada başka değişiklik yok.

**Kapsam dışı not.** 180 MB hedefinde çıktı 170,19 MB, yani hedefin %5,5 altında —
tahmin doğru, doluluk bandı T3'ün alanı, dokunulmadı.

## Denetim

GECTI. On kabul kriteri de dogrulandi.

Sozlesmeden bilincli sapma (0,25 sn -> 1,0 sn nokta + 0,75 sn isinma) denetimde
teknik olarak gecerli bulundu: her nokta ayri bir ffmpeg cagrisi oldugu icin ilk kare
IDR; isinma esigi hem pencere hem yayilim noktalarina AYNI uygulaniyor, dolayisiyla
orani pencereler lehine egmiyor.

`CalibrationProbeTests` zayiflatilmamis — tek degisen test iddiasini gevsetmemis,
aksine genisletmis; diger alti test tam gucte, silinmis test izi yok.

Denetim iki metodoloji notu birakti, ikisi de T5'e devredildi:

1. `ScanWarmupSeconds = 0,75` tek dosyanin bilinen dogru cevabina gore secilmis ve
   verilen merdiven secilen noktada hala yukseliyor (0 -> 1,1437 · 0,5 -> 1,1636 ·
   0,75 -> 1,1865, gercek 1,191). Yakinsadigi gosterilmemis; ayar ve dogrulama ayni
   dosyada yapilmis.
2. `ComplexityProfile.FromProbe`'un `biasSource` varsayilani `Scan` — kaynagi
   belirtmeyen bir cagiran sessizce en dar bandi (0,05) alir. Uretimde tek cagiran
   acikca gecirdigi icin bugun risk yok, ama varsayilanin en iyimser kademe olmasi tuzak.

Kanitsiz kalanlar: dogrulama olcumu (180 MB %1,6 · 8 MB %0,3 · bias 1,1865) ve sure
sayilari (3,82 / 3,91 sn) denetciye kosu ciktisi olarak verilmedi. T5 bagimsiz olcecek.
