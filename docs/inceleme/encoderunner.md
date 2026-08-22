# EncodeRunner / DiskSpaceGuard / TempCleanup — kapsam incelemesi

Salt okuma. Çıplak satır referansları (`:58`) `EncodeRunner.cs`e aittir.
## 1. Ne yapıyor

En fazla 3 deneme; plan iki geçişliyse deneme başına iki ffmpeg çağrısı (`:46-56`). Sonra boyut
`partialPath`ten ölçülür ve ölçülen verimden sonraki hedef çıkar (`:58-60`).

Üç sonuç: band içi → taşı ve dön (`:67-72`); band altı ve hak varsa → `fallbackPath`e taşı, planı
düzelt, devam (`:74-85`); tavan üstü → düzelt, deneme bittiyse yedeğe düş ya da başarısız dön
(`:87-109`). Son denemede band altı olduğu gibi teslim edilir (`:111-113`). `finally` yedeği siler, pass
loglarını temizler (`:128-132`).

## 2. Tavan sözü tutuyor mu

Karar mantığı doğru: eşik `actualMb > targetMb` (`:61`, `ToleranceOver = 1.0`), teslim eden üç dalın üçü
de hedefin altında — `:70` band içi, `:98` yedek (tanımı gereği `LowerMb` altında), `:112` band altı.
Hedefi aşanı `outputPath`e taşıyan dal yok. **Söz yine de tutmuyor; dosya sisteminde kırılıyor.**

**T1 — Hedefi aşan dosya çıktı klasöründe kalıyor (yüksek).** `PartialPathFor` geçici dosyayı %TEMP%'e
değil **çıktı klasörüne**, doğru uzantıyla yazar: `vidshrink_partial_<guid>.mp4` (`:241-246`). 3 deneme
de tavanı aşınca `:93` silmeye çalışır, ama `TryDelete` her istisnayı yutar (`:248-251`). Windows'ta
yeni yazılmış mp4'ü antivirüs/indeksleyici kısa süre kilitler; silme sessizce başarısız olur ve
kullanıcının çıktı klasöründe hedeften **büyük**, tam, oynatılabilir bir video kalır — arayüz "Dosya
teslim edilmedi" derken (`MainWindow.xaml.cs:452-456`).

**T2 — Kill yarışı T1'i olağan hale getiriyor (yüksek).** `TryKill` (`:236-239`)
`Kill(entireProcessTree)` çağırıp süreç ölmeden döner; hemen ardından `:120` siler, ffmpeg tutamacı hâlâ
açıktır, hata yutulur. İptal edilen her kodlama yarım bir `.mp4` bırakabilir.

**T3 — Sızan partial'ı hiçbir temizlik toplamıyor (yüksek).** `CleanupStaleArtifacts` yalnız
`Path.GetTempPath()`e bakar (`App.xaml.cs:17`), partial ise çıktı klasöründedir. Üstelik `"*.partial"`
deseni (`TempCleanup.cs:8`) uygulamanın **hiç ürettiği bir ad değil** — gerçek ad
`vidshrink_partial_*.mp4` (`:244`). Desen ölü, klasör yanlış: T1/T2 artığı kalıcıdır.

**T4 — Sözü koruyan kilit yok (orta).** `ToleranceOver` bugün 1.0, gerçek tolerans yok; ama adı yüzünden
birinin 1.02 yapması sert tavanı sessizce kırar. `tests/` altında `CeilingExceeded`, yedeğe düşme veya
döngüyü kapsayan **tek test yok**; sadece `FillBandTests.cs` var.

**T5 — `File.Move` hatası iyi sonucu yok ediyor (orta).** `:70/:98/:112`'deki `Move` hedef kilitliyse
`IOException` atar; `:123-127` partial'ı silip yeniden fırlatır, dakikalarca süren geçerli sonuç
kurtarılmadan gider. Aynı sürücüde rename olduğu için üstüne yazma yarım kalmaz — teslim değil, veri
kaybı riski.

**T6 — Hata yolunda dolu `OutputPath` (düşük).** `:102` dosya yazılmadığı halde `OutputPath`i doldurur,
`MainWindow.xaml.cs:444` `_lastOutput`u koşulsuz atar (`BtnReveal` gizlendiği için bugün zararsız).

## 3. Deneme döngüsü

`MaxAttempts = 3` (`:18`), ama `PlanCalculator.Correct` her düzeltmede planı `Mode = "2pass"`e zorlar
(`PlanCalculator.cs:296`) — her yeniden deneme **iki** ffmpeg çağrısı, en kötü durumda 5 süreç ve
tavansız süre.

Band altı denemesi iki bayrakla sınırlı (`:64-65, :77-78`). İlk deneme CRF ise verim null döner
(`PlanCalculator.cs:258`); `usedMeasuredUnderBandRetry` pratikte ölü mantık, çünkü `attempt <
MaxAttempts` zaten en fazla 2 denemeye izin veriyor.

**Boşa harcanan tur var.** 1. deneme tavan üstü → düzelt; 2. deneme band altı → `usedUnderBandRetry`
false olduğu için 3. deneme harcanır; 3. deneme yine tavan üstü çıkarsa `:91` yedeğe düşer ve **2.
denemenin dosyası** teslim edilir. Band altı zaten teslim edilebilir bir sonuçtur; son denemenin
beklenen getirisi negatif.

**Daha iyi yedek daha kötüsüyle eziliyor.** `:79-82` yedeği koşulsuz günceller; ikinci band altı sonucu
daha aşağı düşerse iyi dosya `:79`'da silinip yerine kötüsü konur. Hedefe en yakın (hedefin altındaki en
büyük) sonuç tutulmalıydı.

## 4. Kaynak ve dosya yönetimi

Pass logları %TEMP%'te GUID önekli (`:35`, `FfmpegArguments.cs:79`) ve `finally` içinde `name + "*"` ile
süpürülüyor (`:253-263`) — `-0.log` ve `.log.mbtree` dahil; zorla öldürmede açılışta `App.xaml.cs:17`
toparlıyor. Bu taraf temiz.

**Eşzamanlı örnek artığı siliyor (orta).** `CleanupStaleArtifacts` (`TempCleanup.cs:5-9`) `vidshrink_*`
desenindeki **her** dosyayı yaş, kilit veya sahiplik kontrolü olmadan siler; adı "stale" dese de
`LastWriteTime` süzgeci yok (`TempCleanup.cs:11-21`). Kodlama sürerken ikinci bir VidShrink açılırsa
birincinin canlı pass loglarını ve GIF paletini (`:147`) siler.

**İptalde kalan:** partial silinmeye çalışılır (`:120`, T2 nedeniyle sık başarısız), yedek `finally`de
gider (`:130`), loglar temizlenir. `ConvertAsync` (`:135-163`) `finally` kullanmaz ama iki `catch`
partial'ı siler, palet kendi `finally`sinde (`:153`). `:118-122` ile `:123-127` gövde olarak birebir
aynı.

**Disk dolu:** ffmpeg sıfırdan farklı kodla çıkar, `:223-224` stderr kuyruğunu istisnaya sarar, partial
silinir. `DiskSpaceGuard` ise başlamadan **bir kez** ve yalnız çıktı sürücüsüne bakar
(`MainWindow.xaml.cs:432`); bütçe `hedef * 3 + 200 MB` (`DiskSpaceGuard.cs:5-9`). (a) Tavanı aşan
deneme tanım gereği hedeften büyük ve üst sınırsızdır — 3x bütçe tek bir aşırı tahminle patlar; (b) pass
logları ve mbtree %TEMP%'te, muhtemelen başka sürücüde, hiç bütçelenmiyor; (c) UNC yolda `DriveInfo`
istisna verir (`DiskSpaceGuard.cs:25`), `false` döner, koruma sessizce devre dışı kalır. Ayrıca
`TryGetFreeBytes` `false`u iki zıt varsayılanla döndürüyor: kök boşsa `long.MaxValue` (`:22-23`),
istisnada `0` (`:31`).

## 5. Süreç yönetimi

**Klasik stderr kilitlenmesi yok** — stderr ayrı `Task.Run`da, stdout ana akışta (`:187-195`,
`:199-217`), kuyruk 15 satırla sınırlı. **Ama `stderrTask` yalnız başarı yolunda bekleniyor (orta).**
`await stderrTask` `:221`de; iptal/hata `:199` veya `:219`'da fırlarsa oraya varılmaz. `using var
process` (`:180`) `Process`i, okuyucu hâlâ `ReadLineAsync` içindeyken dispose eder → gözlenmeyen
`ObjectDisposedException`; okuyucu `CancellationToken.None` ile açıldığı için kendi başına da durmaz.

**Zaman aşımı hiç yok (orta).** Ne `RunCommandAsync`ta ne döngüde süre sınırı var; ffmpeg takılırsa
`ReadLineAsync(ct)` sonsuza bekler, tek çıkış kullanıcı iptali. `FfmpegArguments.Build`
(`FfmpegArguments.cs:46`) `-nostdin` de eklemiyor, oysa `ComplexityProbe.cs:388`, `QualityMeter.cs:103`,
`CalibrationProbe.cs:118` ekliyor; stdin de yönlendirilmiyor (`ToolLocator.StartInfo`). WPF'te risk
düşük, tutarsız.

**İlerleme ayrıştırma doğru:** `out_time_ms` mikrosaniyedir, kod 1e6'ya bölüyor (`:211`). **Ama sıfır
süre NaN üretiyor (düşük).** `RunOneAsync` `info.DurationSeconds`i ham geçirir (`:170`), oysa
`ConvertAsync` `Math.Max(0.1, …)` ile korur (`:233`). Süre 0 → `:211` NaN → `Fraction` NaN olarak
`Progress.Value`ya gider (`MainWindow.xaml.cs:439`). Kozmetik: `:226` her geçiş sonunda `Remaining =
TimeSpan.Zero` bildirir; 1→2 geçişte ETA bir an 00:00'a sıçrar.

## Önem sırası

1. T1+T2+T3 — tavan üstü/yarım dosya çıktı klasöründe kalıyor ve hiç toplanmıyor
2. Döngünün ve tavan davranışının hiç testi olmaması (T4)
3. `TempCleanup` eşzamanlı örneğin canlı dosyalarını siliyor
4. `stderrTask` iptalde gözlenmiyor; `Process` altından dispose ediliyor; zaman aşımı yok
5. Boşa harcanan deneme turu; daha kötü yedek iyisini eziyor
6. `DiskSpaceGuard`: tek seferlik, %TEMP% bütçesiz, UNC'de devre dışı
7. `File.Move` kilitli hedefte iyi sonucu yok ediyor (T5); sıfır sürede NaN ilerleme
