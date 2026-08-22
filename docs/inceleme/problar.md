# Prob İncelemesi — CalibrationProbe & QualityMeter

Salt-okuma inceleme. Kaynak: `src/VidShrink.Ffmpeg/CalibrationProbe.cs` (195 satır),
`src/VidShrink.Ffmpeg/QualityMeter.cs` (138 satır).

## 1. Ne yapıyor

`CalibrationProbe.RunAsync` plan taslağını alıp kodlayıcının o taslakta gerçekte kaç bit teslim
ettiğini ölçer. Çapa CRF'i plandan veya modelden türetir (`CalibrationProbe.cs:86-99`), `anchor`
ve `anchor+4` olmak üzere iki CRF seçer (`:24-33`), 2–3 pencerede 2'şer saniyelik örnek kodlar
(`:39-45`), `-f null` çıktısının stderr'inden `video:` byte ve son `frame=` sayısını okur
(`:173-194`), iki bppf noktasını `ComplexityProfile.Calibrate`'e verir (`:61-74`). Orada
`HalvingStep = gap/log2(low/high)` ve `LevelFactor = ölçülen/modellenen` hesaplanır
(`ComplexityProfile.cs:157-183`). Her hata sessizce `WithoutCalibration()`'a düşer.
`QualityMeter.MeasureAsync` referans ile testi karşılaştırır: libvmaf (ortalama, harmonik, p10,
min), xpsnr, ssim — her biri ayrı bir tam ffmpeg geçişi (`QualityMeter.cs:12-29`).

## 2. Ölçüm doğruluğu

**1. Donanım kodlayıcıda kalibrasyon ya ölü ya da yanlış.** Prob donanımda kalite bayrağı olarak
`-cq` kullanıyor (`CalibrationProbe.cs:137`, `CodecModel.cs:54`). Bu makinedeki ffmpeg'de `-cq`
**yalnız nvenc**'te var; `h264_qsv/hevc_qsv/av1_qsv` ve `*_amf` için `ffmpeg -h encoder=...`
çıktısında yok (doğrulandı). QSV/AMF'de bayrak yok sayılırsa iki örnek de varsayılan hız
denetimiyle kodlanır, `lowBppf ≈ highBppf` olur, `ComplexityProfile.cs:163-166` korumaları
kalibrasyonu iptal eder — kullanıcı uyarı görmez. Aynı bayrak gerçek kodlamada da kullanılıyor
(`FfmpegArguments.cs:64`), sorun probun ötesine geçiyor.

**2. nvenc'te prob ile kodlama farklı hız denetimiyle koşuyor.** Kodlama donanımda
`-rc vbr -multipass fullres` ekliyor (`FfmpegArguments.cs:74-75,112-116`), prob hiçbirini
eklemiyor (`CalibrationProbe.cs:135-143`). `-multipass fullres` bit dağılımını belirgin
değiştirir; fark doğrudan `LevelFactor`'a yazılır (`ComplexityProfile.cs:174`).

**3. Hızlı mod proba hiç geçmiyor — `-hwaccel auto` ve 2 pencere ölü yol.** `speed` parametresi
varsayılan `Quality` (`CalibrationProbe.cs:15`) ve uygulamadaki tek çağrı bu argümanı geçmiyor
(`MainWindow.xaml.cs:217`; aynısı `ComplexityProbe` için `:210`). Kullanıcının "Hızlı Düşür (GPU)"
kutusu `PlanOptions.SpeedMode`'a gidiyor (`MainWindow.xaml.cs:138`) ama proba ulaşmıyor. Yani
`:111`'deki 2 pencere kısayolu ve `:119`'daki `-hwaccel auto` üretimde hiç çalışmıyor; hızlı modda
da 3 pencere × 2 CRF = 6 tam yazılım kodlaması koşuyor.

**4. Kalibrasyon kendi kendini geçersiz kılıyor.** İmza taslak planın codec/scale/fps üçlüsüne
bağlı (`CalibrationProbe.cs:65-72`) ve yalnız tam eşleşmede uygulanıyor
(`ComplexityProfile.cs:154-155`). Kalibrasyondan sonra `Recalculate()` planı yeniden kuruyor
(`MainWindow.xaml.cs:219-220`) ve yeni `LevelFactor` çoğu zaman farklı çözünürlük/fps seçtiriyor.
O anda `AppliesTo` false döner: ölçüm boşa gider, bant 0.05'ten 0.14'e geri açılır
(`ComplexityProfile.cs:74-77`), yeniden kalibrasyon yapılmaz.

**5. Çok kısa videoda tek pencere ve o pencere jenerik.** 3 sn altında tek pencere, `start=0`
(`CalibrationProbe.cs:104-107`); 12 sn altında 2 pencere (`:111`). `start=0` neredeyse her zaman
açılış/fade — dosyanın en ucuz yeri. Minimum kare veya byte eşiği yok: `:52` yalnız `>0` arıyor,
5 karelik örnek de kabul. Böyle bir örnekte ilk IDR karesi byte'ların çoğunu oluşturur, bppf
şişer, `LevelFactor` üst sınıra (10.0, `ComplexityProfile.cs:41`) tırmanabilir.

**6. Filtre zinciri kodlamayla aynı değil.** Prob `scale=W:H` (bicubic) kullanıyor
(`CalibrationProbe.cs:168`), kodlama `scale=W:H:flags=lanczos` (`FfmpegArguments.cs:50`) — lanczos
daha keskin, daha çok bit ister. Sıra da ters: probda HDR filtresi ölçeklemeden önce (`:167-168`),
kodlamada sonra (`FfmpegArguments.cs:49-52`). Kare hızı düşüşünde prob `-vf fps=`, kodlama `-r`
kullanıyor (`:169` / `FfmpegArguments.cs:56-57`).

**7. CRF modundaki `-maxrate/-bufsize` probda yok** (`FfmpegArguments.cs:66-67`). Tavana çarpan
sahnelerde gerçek çıktı ölçülenden küçük olur, tahmin yukarı sapar.

**8. Ses payı hesaba doğru giriyor.** Prob `-an -sn -dn` ile yalnız video ölçüyor
(`CalibrationProbe.cs:125`); ses boyut tahmininde ayrı kalem (`PlanCalculator.cs:251-252,312-313`).
Ölçek tanımı iki tarafta aynı: `draft.Height/info.Height` (`CalibrationProbe.cs:71` =
`PlanCalculator.cs:249`). Pencere alanı düzeltmesi de tutarlı: `FromProbe` `ReferenceBppf`'i
bias'a bölüyor (`ComplexityProfile.cs:101`), `Calibrate` aynı çarpanı geri veriyor (`:169-171`).
Burada hata yok.

**9. `video:` satırı tam sayı kB.** Çarpan doğru uygulanıyor (`CalibrationProbe.cs:179-186`) ama
ffmpeg yuvarlaması küçük örneklerde %1-3 gürültü bırakır; bu gürültü 4 CRF'lik dar aralıkta
`log2(low/high)` üzerinden `HalvingStep`'e büyütülerek yansır (`ComplexityProfile.cs:165-168`).

## 3. Sabitler

- `WindowSeconds = 2.0` (`:10`), `MaxWindows/MinWindows = 3/2` (`:11-12`) — `ComplexityProbe.cs:12-13`
  ile aynı değerler, ölçüm dayanağı yok.
- `CrfGap = 4.0` (`:13`) — dayanak yok; testler 4'ü sabit varsayıyor (`CalibrationProbeTests.cs:34-35`).
- Kısa video eşikleri `1.5×` ve `6×` pencere (`:104,111`) — uydurulmuş.
- `HalvingStep` sınırı 3–12 (`ComplexityProfile.cs:38-39`) codec varsayılanları 6/7'yi
  (`CodecModel.cs:36-40`) kapsayacak şekilde seçilmiş; `LevelFactor` sınırı 0.1–10 (`:40-41`)
  o kadar geniş ki hatalı ölçümü kesmiyor. Bantlar 0.05/0.08/0.14/0.32 (`:45-48`) kayıtsız.

`tools/VidShrink.Bench` boru hattı `CalibrationProbe`'u hiç çağırmıyor (`Program.cs:84-98`);
bu katsayıların hiçbiri bench ile doğrulanmıyor.

## 4. Kaynak yönetimi

- **Zaman aşımı yok.** `SampleAsync` süresiz bekliyor (`CalibrationProbe.cs:151-153`), aynısı
  `QualityMeter.cs:116-118`. Karşılaştırma: `EncoderCapabilities` 4-5 sn sınır koyuyor
  (`EncoderCapabilities.cs:54,130`). Takılan ffmpeg arayüzü süresiz "Kalibre ediliyor"da bırakır.
- **Eşzamanlılık sınırı yok.** 6 ffmpeg süreci aynı anda başlıyor (`:40-47`); `ComplexityProbe`
  en azından `ScanConcurrency = 8` ile sınırlıyor (`ComplexityProbe.cs:20`). Donanımda eşzamanlı
  oturum sınırına çarpan süreç `ExitCode != 0` verip sessizce (0,0) döner (`:155`).
- **İptal** `ct.Register(() => TryKill(...))` ile ağaç dahil öldürüyor (`:147,159-162`), doğru.
  Ancak `ReadToEndAsync(ct)` iptalde fırlarsa `WaitForExitAsync` hiç beklenmez.
- **Geçici dosya yok** — prob `-f null` kullanıyor. `QualityMeter`'ın vmaf json'u `finally` ile
  siliniyor (`QualityMeter.cs:34,63`), adı `vidshrink_*` desenine uyduğu için `TempCleanup.cs:7`
  artıkları da topluyor.
- **Sessiz yutma:** `:80-83` her istisnayı kalibrasyonsuz profile çeviriyor, günlük yok. `-cq`
  reddi, eksik ffmpeg, sürücü hatası — hepsi aynı sessiz sonuca varıyor.

## 5. QualityMeter

- **Uygulamada ölü kod.** `src/` içinde tek referans dosyanın kendisi; çağıranlar yalnız
  `tests/VidShrink.Tests/QualityMeterTests.cs` ve `tools/VidShrink.Bench/Program.cs:52,98`.
  Kullanıcıya gösterilen kalite ölçülmüş değil, `PlanCalculator`'ın tahmini.
- **Yetenek kapısı eksik.** libvmaf/xpsnr/ssim için `HasFilter` var (`:17,20,24`), zincirin ilk
  halkası `zscale` için yok (`:106`). zscale'siz ffmpeg'de üç ölçüm de `InvalidOperationException`
  fırlatır (`:120-121`), sessizce null dönmez.
- **Kare hizalaması yapılmıyor.** Yalnız çözünürlük eşitleniyor (`:106`); plan kare hızını
  düşürdüğünde test ve referans kare kare eşleşmez, framesync kopyalayarak/atlayarak hizalar ve
  skor anlamsızlaşır. Süre farkı ve HDR→SDR tonemap için de kapı yok.
- **Maliyet.** Metrik başına bir tam geçiş, her geçişte iki dosyanın tam kod çözümü — 3 metrik =
  3 geçiş × 2 kod çözme. libvmaf'a `n_threads` verilmiyor (`:37`), varsayılan tek iş parçacığıyla
  koşar; uzun dosyada gerçek zamanın kat kat üstüne çıkar.
- **Ayrıştırma kırılgan.** xpsnr ve ssim `Regex.Match` ile **ilk** eşleşmeyi alıyor (`:69,81`);
  `CalibrationProbe.cs:175` aynı riskte `RightToLeft` kullanıyor. Ayrıca `:72-74` `TryParse`
  değil `double.Parse` kullanıyor.
- **Doğru olan:** girdi sırası libvmaf'ın beklediği `[distorted][reference]` (`:18,38,106`),
  harmonik ortalama ve p10 hesabı tutarlı (`:57-59,87-96`).
