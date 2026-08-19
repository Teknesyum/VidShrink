---
id: T2b
title: Temsili pencere seçimi — kalibrasyonun kalan sapmasını kapat
role: builder
model: opus
depends: [T2]
owns: [src/VidShrink.Ffmpeg/ComplexityProbe.cs, src/VidShrink.Core/ComplexityProfile.cs, src/VidShrink.Ffmpeg/FfmpegArguments.cs, tests/VidShrink.Tests/WindowSamplingTests.cs]
side_effects: []
status: done
round: 1
agent_id: teknesyum-builder
audit: passed
auditor_id: T2b-auditor
diff: ComplexityProfile.cs, ComplexityProbe.cs, FfmpegArguments.cs, WindowSamplingTests.cs
verification: build 0 uyarı / test 56/56 / gerçek dosyada 180 MB + 8 MB ölçüldü
---

## Amaç

T2 kalibrasyon probunu kurdu ve ölçümün kendisi doğru çıktı: bulduğu yarılanma adımı 4,65,
tam dosyadaki gerçek ölçüm de 4,65 — modelin sabit 6,0 değeri yanlıştı. 2 saniyelik örnek
klipler tam kodlamaya göre yalnızca %1-1,6 sapıyor.

Buna rağmen 180 MB hedefte tahmin hatası **+%8,3'ten +%20,6'ya çıktı**. Sebep kalibrasyon
değil: `ComplexityProbe.Windows()`'un seçtiği 3 pencere dosya ortalamasından **%18,9 daha
yüklü**. Bu sapma `ReferenceBppf`'e zaten gömülüydü; eski modelde yanlış CRF adımı ve
medium→slow preset farkıyla **tesadüfen** kısmen götürülüyordu. Kalibrasyon o tesadüfi
telafiyi kaldırınca sapma çıplak kaldı.

Yani T2 tek başına yayına giderse tahmin **kötüleşir**. Bu sözleşme onu kapatır ve
T3'ün doluluk bandının ön koşuludur.

## Kabul kriteri

1. **Pencere yükü ölçülür.** `ffprobe -select_streams v -show_entries packet=pts_time,size`
   ile video paket boyutları okunur ve saniye başına bit profili çıkarılır. Bu çağrı
   1080p48 / 52 sn bir kaynakta **1 saniyeyi aşmamalı**; aşıyorsa `-read_intervals` ile
   örneklenir. Ölçüm başarısızsa (ffprobe hatası, paket bilgisi yok) bugünkü davranışa
   sessizce düşülür.
2. **Sapma hesaplanır ve düzeltilir.** Seçilen pencerelerin paket profilindeki ortalaması
   ile dosyanın tamamının ortalaması karşılaştırılır:
   `windowBias = ortalamaPencere / ortalamaDosya`. `ComplexityProfile.ReferenceBppf`
   bu oranla düzeltilir. Oran `[0,5 , 2,0]` aralığına kırpılır; dışına taşan ölçüm
   güvenilmez sayılır ve düzeltme uygulanmaz.
   **Pencereleri yeniden seçmek yerine sapmayı ölçüp çıkarmak tercih edilir** — pencere
   seçimini "temsili" hâle getirmeye çalışmak sahne algılama gerektirir ve pahalıdır;
   sapmayı bilmek yeterlidir.
3. **Bias profilde görünür.** `ComplexityProfile` üzerinde uygulanan `WindowBias` okunabilir
   olur; T5 raporunda bu değer tabloya yazılacak. T2'nin eklediği kalibrasyon alanlarını
   (`CalibrationSignature`, `LevelFactor`, `HalvingStep`, `EstimateBandFor`, `AppliesTo`)
   **bozma**, üzerine ekle.
4. **Band gerçeği yansıtır.** `EstimateBandFor` yalnızca hem kalibrasyon hem bias düzeltmesi
   uygulandığında dar bandı (0,05) döndürür. Bias ölçülemediyse band 0,05'e inmez —
   ölçülmemiş bir kesinliği iddia etme.
5. **Kapsam dışı hata düzeltilir.** `FfmpegArguments.Build` iki geçişli modda
   `-maxrate/-bufsize` ekliyor; `libsvtav1` bunu reddedip kodlamayı düşürüyor. Auto codec +
   küçük hedef bileşimi kullanıcının gerçekten karşılaşacağı yol. Bu bayraklar yalnızca
   destekleyen kodlayıcılarda üretilsin. Düzeltmenin gerçek dosyada işe yaradığı
   doğrulanmalı — sadece test değil, `libsvtav1` ile bir kodlama koştur.
6. **Doğrulama ölçümü.** Gerçek dosyada 180 MB ve 8 MB hedefte tahmin/gerçek farkı
   yeniden ölçülür ve Çıktı'ya yazılır. Hedef: 180 MB'da mutlak hata **%5'in altı**.
   Altına inemiyorsan **inememiş olmayı raporla, sayıyı süsleme** — kalan sapmanın
   nereden geldiğini tek paragrafla açıkla.
7. Testler (`WindowSamplingTests`): bilinen bir paket profilinde bias'ın doğru hesaplandığı;
   kırpma sınırının dışındaki oranın uygulanmadığı; ffprobe verisi yokken profilin
   bugünkü hâline döndüğü; bias yokken bandın 0,05'e inmediği.
8. **`ComplexityProbe` süreç kalıbı düzeltilir.** Bugün `SampleAsync` yalnızca stderr
   okuyor ve `CancellationToken` için süreç öldürme kaydı yok; kullanıcı iptal ettiğinde
   .NET beklemesi kesiliyor ama alt `ffmpeg.exe` süreçleri arka planda kalıyor.
   `QualityMeter.cs`'deki düzeltilmiş kalıba geçir: iki akış da `Task.WhenAll` ile
   boşaltılır, `ct.Register(() => TryKill(process))` ile süreç ağacı öldürülür
   (`process.Kill(entireProcessTree: true)`). Yeni eklediğin ffprobe çağrısı da aynı
   kalıbı kullanır.
9. `dotnet build VidShrink.sln -c Release` 0 uyarı, `dotnet test VidShrink.sln` yeşil.

## Arayüzler

- `ComplexityProbe.SampleAsync(path, start, length, filter, ct)` — stderr'den `video:NNNKiB`
  ve son `frame=NN` okuyor. `Windows()` pencere seçimini yapıyor.
- `ComplexityProfile.FromProbe(fullScaleBppf, halfScaleBppf, sampledSeconds, sampledFrames)`
- T2'nin eklediği: `CalibrationSignature`, `Calibrate/WithoutCalibration/AppliesTo`,
  `LevelFactor`, `HalvingStep`, `EstimateBandFor`.
- `ToolLocator.Ffprobe` → ffprobe yolu. `ToolLocator.StartInfo` `internal`'dır.

## Bağlam

- Test dosyası: `C:\Users\Administrator\Videos\gothic2026-08-15 14-01-29.mp4`
  — 830 MB, 52,6 sn, 1920x1080@48, h264 + aac stereo. Çıktıları masaüstüne yaz.
- T4 ileride `ComplexityProbe`'a üçüncü bir ölçüm noktası (yarı fps) ekleyecek; bias
  hesabını ona engel olmayacak biçimde kur.
- **Process tuzağı:** `RedirectStandardOutput` ve `RedirectStandardError` açıkken iki akışı
  da okumadan `WaitForExitAsync` çağırırsan pipe buffer dolar ve süreç kilitlenir. Bugün
  bu hata iki ayrı yerde çıktı, 25 dakika kaybettirdi. Kalıp:
  `var o = p.StandardOutput.ReadToEndAsync(ct); var e = p.StandardError.ReadToEndAsync(ct);`
  `await Task.WhenAll(o, e); await p.WaitForExitAsync(ct);`
  `ffprobe -show_entries packet` çıktısı **büyüktür**, burada gerçekten kilitlenirsin.
- Kod yorumu yazma.

## Doğrulama

```
dotnet build VidShrink.sln -c Release
dotnet test VidShrink.sln
```

Ek olarak gerçek dosyada 180 MB ve 8 MB: tahmin, gerçek, mutlak hata, ölçülen
`WindowBias` — Çıktı'ya tablo.

## Kayıt noktası

Tamamlandı, submitted.

- `ComplexityProfile`: `WindowBias`, `WindowBiasKnown`, `IsTrustedBias`, `FromProbe(..., windowBias)`.
  Bias `[0,5 , 2,0]` dışındaysa uygulanmaz ve `WindowBias = 0` kalır. `EstimateBand` ve
  `EstimateBandFor` yalnızca kalibrasyon **ve** bias birlikte varken 0,05 döndürür.
  `Calibrate` içindeki `modelled` pencere alanına taşındı (`WindowDomainFactor`), yoksa
  `LevelFactor` bias düzeltmesini birebir geri emiyordu — ölçüldü, doğrulandı.
- `ComplexityProbe`: `ParsePackets`, `ComputeWindowBias`, `Windows` public; ffprobe
  `packet=pts_time,size` okuması eklendi; `SampleAsync` ve yeni ffprobe çağrısı
  `Task.WhenAll` + `ct.Register(TryKill)` kalıbına geçirildi.
- `FfmpegArguments.SupportsRateLimits` — `libsvtav1`'de `-maxrate/-bufsize` üretilmiyor.
- `WindowSamplingTests` 9 test.

Kalan iş yok. 180 MB hedefte hata %5'in altına inmedi (%13,3) — sebep ve ölçülmüş
alternatif Çıktı'da.

## Çıktı

**Değişen dosyalar**

- `src/VidShrink.Core/ComplexityProfile.cs`
- `src/VidShrink.Core/FfmpegArguments.cs`  ← sözleşmedeki `owns` yolu
  `src/VidShrink.Ffmpeg/FfmpegArguments.cs` yazıyor, dosya gerçekte `Core` altında;
  aynı dosya olduğu için orada düzenlendi.
- `src/VidShrink.Ffmpeg/ComplexityProbe.cs`
- `tests/VidShrink.Tests/WindowSamplingTests.cs` (yeni)

**Ölçüm** — `gothic2026-08-15 14-01-29.mp4`, 830 MB / 52,6 sn / 1920x1080@48

| hedef | plan | tahmin | gerçek | tahmin hatası | hedef sapması |
|---|---|---|---|---|---|
| 180 MB | libx264 / crf 20 / 1920x1080@48 | 143,59 MB | 126,70 MB | **+%13,3** | −%29,6 |
| 8 MB | libx264 / 2pass / 1190x670@48 | 8,00 MB | 7,98 MB | +%0,2 | −%0,2 |
| 8 MB (Auto → libsvtav1 / 2pass) | 1190x670@48 | 8,00 MB | 7,84 MB | +%2,0 | −%2,0 |

Ölçülen `WindowBias` = **1,0646**. ffprobe paket çağrısı 0,56 sn sürdü (1 sn sınırının
altında, `-read_intervals` gerekmedi).

**Hedefe ulaşılamadı — %5 yerine %13,3.** Sebebi ölçtüm: gerçek pencere sapması x264
alanında **1,191** (tam dosya CRF 23 medium = 0,12597 bppf, pencerelerin ölçtüğü 0,15003
bppf). Kaynak paketlerinden okunan oran ise yalnızca 1,065. Fark yöntemin kendisinde:
bu kaynak ~133 Mbit/s ile neredeyse kayıpsız kaydedilmiş, o bit hızında paket boyutu
sahne karmaşıklığını değil ağırlıkla gürültü/detayı izliyor, dolayısıyla kaynak kodlayıcının
bit dağılımı x264-CRF dağılımının zayıf bir vekili. Sapmanın 1,191/1,065 = **%11,8'i
ölçülemeden kalıyor**; kalan tahmin hatası tam olarak bu.

**T0 için karar (uygulamadım, kapsam dışı):** aynı sapma kodlama alanında ucuza
ölçülebiliyor — tam dosyayı `scale=480:270, libx264, crf 23, preset ultrafast` ile tarayıp
aynı ayarla kodlanmış pencerelerle karşılaştırınca oran **1,176** çıkıyor (gerçek 1,191'e
%1,3 uzak). Bu, 180 MB hatasını %5'in altına indirir. Bedeli: 52 sn'lik kaynakta 5,9 sn
(≈9x gerçek zaman) — 2 saatlik bir videoda ~13,5 dakika. Bu yüzden ya süre eşiğine
bağlanmalı ya da T4'ün üçüncü ölçüm noktasıyla birleştirilmeli; ikisi de sözleşme dışı
tasarım kararı.

**Not (kapsam dışı, dokunmadım):** `PlanCalculator.Estimate` bandı hâlâ sabit 0,14/0,32
kullanıyor, `EstimateBandFor`'u çağırmıyor — T3'ün işi. `libsvtav1` düzeltmesi gerçek
kodlamayla doğrulandı: `-maxrate` ile çıktı 0 bayt ("Terminating thread with return code
-22"), `-maxrate` olmadan 413 KB sağlam çıktı.

## Denetim

Auditor K2 (bias yonu ve cifte-duzeltme), K4, K5, K6, K8 ve kod yorumu maddelerini
gecirdi. `PlanCalculator.cs` owns disi ve dokunulmamis; T0 `git status` ile owns disi
degisiklik olmadigini ayrica dogruladi.

K1 kaldi: `ReadPacketsAsync` kaynagin uzunluguna bakmaksizin dosyanin tamaminin
paketlerini okuyor, `-read_intervals` ile adaptif dusme yok. 52 saniyelik test
dosyasinda 0,56 sn suruyor ama uzun veya yuksek bit hizli kaynakta sinirsiz.

Bu madde T2c'ye devredildi (madde 9). Gerekce: T2c ayni dosyayi (`ComplexityProbe.cs`)
sahipleniyor ve zaten "olcum suresi kaynak uzunlugundan bagimsiz olmali" ilkesini
getiriyor; ayni sorunu iki ayri turda cozmek yerine tek yerde cozulur. T2b'nin uretimi
bu maddeyi bekletmeyi gerektirmiyor — paket tabanli bias T2c'de zaten yedek kademeye
iniyor.

K6'nin sayisal hedefi (180 MB'da hata %5 alti) tutmadi: %20,6 -> %13,3. Auditor
dususlu raporlamayi dogru buldu — sayi suslenmemis, sebep olculup aciklanmis,
uygulanmayan alternatif ayrica isaretlenmis. Kalan sapmanin kapatilmasi T2c'nin isi.
