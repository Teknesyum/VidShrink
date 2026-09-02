# Başlat kilidi, yutulan istisna, eskiyen cümle — T136 kapanışı

T130'un mühür notunda üç borç kaldı; üçü de `src/VidShrink.App/MainWindow.axaml.cs`
içinde. Bu belge onların kapanışını ölçümle gösterir. T128'in Core'da düzelttiği
gerekçe cümlesinin arayüzdeki dört kopyası da burada hizalandı.

Dal `T136-baslat-kilidi`, taban `main` = `235814c`.

## Hüküm

Yoklama cevap veremediğinde ürün artık kilitlenmiyor: `BtnStart.IsEnabled` donanım
ölçümüne bakmıyor, plan yazılım yoluna düşüyor ve kullanıcı sıkıştırabiliyor.
İstisna atan yoklama ile "çalışmıyor" ölçülen yoklama ayrı iki durum; istisnanın
metni durum satırına çıkıyor. Dört arayüz cümlesi Core'un cümlesiyle aynı şeyi
söylüyor ve bu artık bir ölçüyle bağlı.

Dört kriterin dördü mutasyonla kırıldı. Kırılan ölçülerin adları aşağıda,
mutasyonu üreten komutla birlikte.

## Nasıl koşturuldu

Ağaç `.claude/worktrees/T136`. `--no-build` kullanılmadı; her mutasyon kendi
derlemesini gördü.

| Adım | Komut |
| --- | --- |
| Derleme | `dotnet build -c Release --no-incremental` |
| Sözleşme süiti | `dotnet test -c Release --filter "PlanCalculatorProbeTests\|LanguageTests"` |
| Tam süit | `dotnet test -c Release` |
| Mutasyon | tek satirlik degisiklik → derle → kos → `git checkout -- src/VidShrink.App` |

Mutasyonlar tek satırlık; hangi satırın neyle değiştirildiği her K bölümünde
yazılı, yani bu belge tek başına yeniden üretilebilir. Sürücü betiği geçiciydi,
depoya girmedi.

Mutasyon çıktıları `.calisma/t136/mutasyon-k{1,2,3,6}.txt`; kusurun düzeltmeden
önceki kırmızısı `.calisma/t136/k1k2-kirmizi.txt` (commit `a90b5f5`). **`.calisma/`
`.gitignore`da**, yani bu dosyalar dala girmiyor ve denetçinin ağacında yok —
aşağıdaki blok alıntıları kaydın kendisi, özet değil.

**Derlemenin sessizce düşmesi bir kere yakalandı.** K2'nin ilk mutasyon biçimi
(`if (failure is null)` → `if (true)`) `-warnaserror` altında `CS0162 Ulaşılamayan
kod` verdi; derleme düştüğü hâlde `dotnet test` eski ikiliyi koşturup
`Başarılı! - Başarısız: 0, Başarılı: 70` yazdı. Yeşil gerçekti, ölçtüğü kod
yanlıştı. Mutasyon derlenen bir biçime çevrildi (`failure = ex;` → `_ = ex;`) ve
bu belgedeki K2 satırı o koşumdan geliyor.

## K1 — Başlat kalıcı kilitlenmiyor

Eski satır (`main`, `:1793`):

```
BtnStart.IsEnabled = _cts is null && ToolLocator.IsAvailable(out _) && !detailed.HardwareNotMeasured;
```

`Ready` (`:1370`) `Attempts >= MaxAttempts && !Settled` iken kalıcı `false` döner,
`HardwareNotMeasured` düşmez ve düğme sonsuza kadar sönük kalır. T128 bu yüzeyi
Kalite kipine de genişletmişti.

Yeni satır (`:1862`):

```
BtnStart.IsEnabled = _cts is null && ToolLocator.IsAvailable(out _);
```

Plan bayrağı kayboldu değil, ayrı bir okunur alana taşındı: `PlanHardwareNotMeasured`
(`:1877`, `:1843`te atanıyor). Plan düzeltilir, üreten durdurulmaz.

Ölçü: `YerlesmeyenYoklamaBaslatDugmesiniKilitlemiyor`. Yoklama
`UnsettledProbeMs + 300` ms sürer, `MaxAttempts + 1` tur yüklenir; sonra
**hem** `PlanHardwareNotMeasured` **hem** `BtnStart.IsEnabled` doğru olmalıdır.

| Koşum | Sonuç |
| --- | --- |
| Kusur duruyorken (`a90b5f5`, iki yeni ölçü) | `Başarısız! - Başarısız: 2, Başarılı: 0, Atlanan: 0, Toplam: 2` |
| Düzeltmeden sonra | yeşil |
| Mutasyon k1: eski koşul geri kondu | `Başarısız! - Başarısız: 1, Başarılı: 69, Atlanan: 0, Toplam: 70` |

Kırılan tek ölçü, elle sayıldı — bir tane:

```
Başarısız VidShrink.Tests.PlanCalculatorProbeTests.YerlesmeyenYoklamaBaslatDugmesiniKilitlemiyor [4 s]
  Hata İletisi: yoklama yerlesmedigi icin Baslat kalici kilitli kaldi
```

## K2 — İstisna üçüncü durumda kalıyor

Eski `catch` (`main`, `:1339-1345`) gövdesi boştu: `works` `false`ta kalıyor ve
`answer.Works = works` ile **gerçek bir "çalışmıyor" cevabıyla aynı yere**
yazılıyordu. Üstelik `Settled` geçen süreye baktığı için hızlı fırlayan bir istisna
"yerleşti" sayılıyordu — T128'in `PickCodec`e eklediği üçüncü durum altta yoktu.

Bugün `Measure` istisnayı yakalayıp saklıyor (`:1400`), `Answer` üç yeni alan
taşıyor (`Attempts`, `Failure`, `ElapsedMs`) ve okuma tarafı beş durumlu:

```csharp
internal enum ProbeAnswer { Unknown, Working, NotWorking, Unsettled, Failed }
```

`AnswerFor` (`:1323`) sırayla bakar: kayıt yoksa `Unknown`, `Failure` doluysa
`Failed`, `Settled` değilse `Unsettled`, kalanı `Working`/`NotWorking`. İstisna
düşen yoklama `Settled = false` olarak yazılır, yani `IsMeasured` de `false` döner
— ölçüm sayılmaz.

Ölçü: `IstisnaAtanYoklamaOlculmusBasarisizliktanAyirtEdiliyor`. Aynı senaryo iki
kaynakla koşuyor; ikisi de `WorksAsEncoder == false` üretiyor:

| Kaynak | `AnswerFor` | `IsMeasured` | `FailureFor` |
| --- | --- | --- | --- |
| `RecordingAvailability` (ölçtü, çalışmıyor) | `NotWorking` | `true` | `null` |
| `ThrowingAvailability` (istisna fırlattı) | `Failed` | `false` | `yoklama surecine erisilemedi` |

| Koşum | Sonuç |
| --- | --- |
| Kusur duruyorken (`a90b5f5`) | kırmızı, `istisna atan yoklama olcum sayilmamali` |
| Düzeltmeden sonra | yeşil |
| Mutasyon k2: `failure = ex;` → `_ = ex;` (istisna yine yutuluyor) | `Başarısız! - Başarısız: 1, Başarılı: 69, Atlanan: 0, Toplam: 70` |

Kırılan tek ölçü, elle sayıldı — bir tane:

```
Başarısız VidShrink.Tests.PlanCalculatorProbeTests.IstisnaAtanYoklamaOlculmusBasarisizliktanAyirtEdiliyor [26 ms]
```

## K3 — Hiçbir şey ölçmeyen iddia davranış ölçüyor

Eski iddia (`PlanCalculatorProbeTests.cs:421`):

```csharp
Assert.All(durations, d => Assert.True(d >= 0));
```

Süre negatif olamaz; hiçbir mutasyon bunu kıramaz. Bu deponun kronik kusuru
(bkz. hata günlüğü *"Sabit karşılaştıran test davranış ölçmez"*).

Yerine geçen iddia geçidin **kendi kaydettiği süreyi kendi verdiği kararla
yüzleştiriyor**:

```csharp
var olculen = gate.ElapsedMsFor("libsvtav1");
var yerlesti = olculen >= 0 && olculen < UnsettledProbeMs;
var beklenen = yerlesti ? ProbeAnswer.Working : ProbeAnswer.Unsettled;
Assert.True(olculen >= 0, "gecit yoklamayi hic kosturmadi");
Assert.Null(gate.FailureFor("libsvtav1"));
Assert.Equal(beklenen, gate.AnswerFor("libsvtav1"));
Assert.Equal(yerlesti, gate.IsMeasured("libsvtav1"));
```

Bu iddia makine yükünden bağımsız — yük altında da doğru, boşta da doğru — ama
`Settled` hesabı bozulursa kırılıyor. **İki yönde de gerçekten koştu**;
`.calisma/test-ciktilari/t130-yoklama/olcum.txt` içinden, tekrar koşumlarından:

| Ölçülen süre | Eşik | Geçidin kararı | Beklenen |
| --- | --- | --- | --- |
| 77 ms | 2000 ms | `Working` | `Working` |
| 75 ms | 2000 ms | `Working` | `Working` |
| 2187 ms | 2000 ms | `Unsettled` | `Unsettled` |

Süre listesi ölçülüyor ama bu iddia "gerçek bir sınıra bağlı" değildi:
`Assert.All(durations, d => Assert.True(d < EncoderCapabilities.ProbeKillMs))`
gerçek sürelerin (60-100 ms) 15 000 ms'lik öldürme sınırına hiç yaklaşmaması
yüzünden mutasyona dayanıklı değildi — killer mantığı tamamen bozulsa bile bu
karşılaştırma yeşil kalırdı. T137, bu iddiayı `dogrudan.State` üzerinden gerçekten
kırılabilir bir ölçüme çevirdi (bkz. `docs/olcumler/yoklama-uclu-cevap.md`, K9);
8 tekrarlık süre dağılımı artık yalnız kanıt (`WriteEvidence`) olarak kalıyor,
"ölçüldü" iddiası taşımıyor.

**Bir sessiz atlama düzeltildi.** İlk hâlde bu iddia `MachineIsQuiet` kapısının
*altında* duruyordu; makinede başka ffmpeg koşarken ölçü hiç çalışmadan yeşil
sayılıyordu (kanıt dosyasında yalnız `atlandi, makinede baska ffmpeg kosuyor`
satırı vardı). Yük gerektirmeyen pin kapının üstüne alındı; kapı yalnız 8 tekrarlık
süre dağılımını koruyor.

| Koşum | Sonuç |
| --- | --- |
| Mutasyon k3: `answer.Settled = clock.ElapsedMilliseconds < UnsettledProbeMs;` → `answer.Settled = false;` | `Başarısız! - Başarısız: 3, Başarılı: 67, Atlanan: 0, Toplam: 70` |

Kırılan ölçüler, elle sayıldı — üç tane:

```
Başarısız ...ASettledProbeIsReadWithoutSpawningAgain [14 ms]                       Expected: True   Actual: False
Başarısız ...IstisnaAtanYoklamaOlculmusBasarisizliktanAyirtEdiliyor [69 ms]        Expected: NotWorking  Actual: Unsettled
Başarısız ...TheRealSoftwareProbeDurationIsMeasured [254 ms]                       Expected: Working     Actual: Unsettled
```

## K4 — Yerleşmemiş yoklamanın kendi cümlesi var

`ReportUnsettledProbe` (`:1891`) `main.error.probe` anahtarını kullanıyordu:
*"Donanım kodlayıcı yoklaması başarısız"*. Ama `Unsettled` "başarısız" demiyor,
"denendi, sonuç yerleşmedi" diyor. İki yeni anahtar eklendi, iki dilde:

| Anahtar | Satır | tr | en |
| --- | --- | --- | --- |
| `main.status.probe-unsettled` | `:262` | Donanım kodlayıcı yoklaması sonuca varamadı, plan yazılım kodlayıcısına düştü. Sıkıştırma yine de çalışıyor. | The hardware encoder probe did not settle, so the plan falls back to the software encoder. Compression still works. |
| `main.status.probe-failed` | `:263` | Donanım kodlayıcı yoklaması koşturulamadı: {0}. Plan yazılım kodlayıcısına düştü; sıkıştırma yine de çalışıyor. | The hardware encoder probe could not run: {0}. The plan falls back to the software encoder; compression still works. |

`{0}` istisnanın metni — K2'nin sakladığı `FirstFailure`. İstisna sessizce
kaybolmuyor, kullanıcının gördüğü satıra çıkıyor.

Yeni gövde iki durumu ayırıyor:

```csharp
if (_planEncoders.FirstFailure is { } failure) text = Say("main.status.probe-failed", failure);
else if (_planEncoders.Unsettled) text = Say("main.status.probe-unsettled");
else return;
```

**Eski anahtar kullanımda kaldı**, sözleşmenin istediği gibi: `main.error.probe`
gerçek başarısızlık için `ProbeHardwareEncodersAsync` içinde,
`MainWindow.axaml.cs:1456` —
`TxtSystemStatus.Text = $"{Say("main.error.probe")}: {ex.Message}";`

`LanguageTests` yeşil; iki anahtar iki dilde de var.

## K6 — Dört arayüz cümlesi Core ile hizalandı

T128 `PlanCalculator.cs:194`teki cümleyi düzeltti; kullanıcının gördüğü dört satır
eski metinde kaldı ve bunu **hiçbir ölçü görmedi** — kusurun sağ kalma biçimi
buydu. Core'un bugünkü cümlesi:

```
the {preferredCodec} encoder could not be used on this machine, so encoding falls back to {codec}
```

| Dosya:satır | Önce | Sonra |
| --- | --- | --- |
| `en/main.json:285` | the {0} encoder **is not available on this ffmpeg build**, so encoding falls back to {1} | the {0} encoder **could not be used on this machine**, so encoding falls back to {1} |
| `tr/main.json:285` | {0} kodlayıcısı **bu ffmpeg sürümünde yok**, bu yüzden {1}'e düşüldü | {0} kodlayıcısı **bu makinede kullanılamadı**, bu yüzden {1}'e düşüldü |
| `en/main.json:314` | The preferred encoder **is not available on this ffmpeg build**; falling back to a software encoder. | The preferred encoder **could not be used on this machine**; falling back to a software encoder. |
| `tr/main.json:314` | Tercih edilen kodlayıcı **bu ffmpeg sürümünde yok**; yazılım karşılığına düşüldü. | Tercih edilen kodlayıcı **bu makinede kullanılamadı**; yazılım karşılığına düşüldü. |

Neden: artık derleme listesine değil yoklamaya bakılıyor. Kodlayıcı derlemede
olabilir ve yine de bu makinede çalışmayabilir.

Ölçü: `ArayuzunDusmeCumlesiCoreunkiyleAyniSeyiSoyluyor`. En kalıbını depodaki
JSON'dan okuyup planın `RequestedCodec`/`FallbackCodec` değerleriyle biçimliyor
ve **Core'un ürettiği `Plan.Reason` içinde birebir arıyor**:

```csharp
var kalip = Locales.Values("en")["main.reason.encoder-fallback"];
var arayuz = string.Format(CultureInfo.InvariantCulture, kalip, note.RequestedCodec, note.FallbackCodec);
Assert.Contains(arayuz, detailed.Plan.Reason, StringComparison.Ordinal);
```

| Koşum | Sonuç |
| --- | --- |
| Mutasyon k6: en `:285` eski metne döndürüldü | `Başarısız! - Başarısız: 1, Başarılı: 69, Atlanan: 0, Toplam: 70` |

Kırılan tek ölçü, elle sayıldı — bir tane:

```
Başarısız ...ArayuzunDusmeCumlesiCoreunkiyleAyniSeyiSoyluyor [4 ms]
  String:    "the av1_nvenc encoder could not be used o"···
  Not found: "the av1_nvenc encoder is not available on"···
```

## K5 — Tam süit

`dotnet test -c Release`, commit `702d2a0`, ağaç `.claude/worktrees/T136`:

```
Başarılı!  - Başarısız:     0, Başarılı:  1331, Atlanan:    17, Toplam:  1348, Süre: 18 m 36 s
```

T128'in mühür koşumu 1345 ölçü sayıyordu; bu sözleşme üç ölçü ekledi
(`YerlesmeyenYoklamaBaslatDugmesiniKilitlemiyor`,
`IstisnaAtanYoklamaOlculmusBasarisizliktanAyirtEdiliyor`,
`ArayuzunDusmeCumlesiCoreunkiyleAyniSeyiSoyluyor`) ve toplam 1348 oldu — elle
sayıldı, üç tane.

Süit bu koşumda **yalnız değildi**: aynı makinede başka bir ajanın `testhost`u
koşuyordu. Süre (18 dk 36 sn) o yüzden bir başarım sayısı değil; K5'in iddiası
yalnızca kırmızı olmadığıdır.

CI de yeşil — **koşum 33631540902**, aynı commit `702d2a0`, `success`:

```
Passed!  - Failed:     0, Passed:  1330, Skipped:    18, Total:  1348, Duration: 21 m 51 s
KOŞUM KAPISI GEÇTİ: başarısız=0 toplam=1348 alt-sınır=1134 atlanan=18 ust-sinir=30
```

Üç yeni ölçünün üçü de CI'da **koştu**: xUnit yalnız `SKIP`/`FAIL` satırı yazıyor,
CI günlüğünde 18 `Skipped` satırı var ve hiçbiri bu üçü değil.

Rapor commit'i (`ab1aca6`) `docs/` altında; CI iş akışının `paths-ignore` listesi
yüzünden yeni koşum açmadı ve koşandakini iptal etmedi. Son kod commit'i `702d2a0`.

## Kapsam dışı — denetçinin 2. borcu

`EncoderCapabilities.WorksAsEncoder` (`src/VidShrink.Ffmpeg`, `:70`) `Unmeasured`ı
hâlâ `false`a katlıyor; ölçülmüş sonucu `docs/olcumler/surucu-yoklugu.md:322`de
duruyor. Bu sözleşme `src/VidShrink.Core` ve `src/VidShrink.Ffmpeg` altına yazmıyor,
dokunulmadı. Ayrı sözleşme konusu.

Aynı şey `MainWindow`un kendi `WorksAsEncoder`ı (`:1312`) için de sürüyor: geçit
üstte beş durum tutuyor, `IEncoderAvailability` arayüzü altta iki durum istiyor.
Üçüncü durum `AnswerFor`/`IsMeasured` ile taşınıyor, `WorksAsEncoder` ile değil.

## Ölçülmeyenler

- **Eşiğin kendisi.** K3'ün iddiası "yerleşme kararı ölçülen süreden türüyor"u
  bağlıyor; `UnsettledProbeMs = 2000` değerinin doğru sayı olduğunu ölçmüyor.
- **Ekranda ne göründüğü.** `main.status.probe-failed` cümlesi bir ölçüyle
  `TxtSystemStatus`a kadar izlenmedi; sözleşmede ekran açan koşum kapalı.
  `ReportUnsettledProbe`un anahtar seçimi kodda okunuyor, gözle değil.
- **Gerçek donanım istisnası.** İstisna yolu `ThrowingAvailability` ile üretildi;
  gerçek bir sürücü çökmesinin aynı yola girdiği bu ağaçta ölçülmedi.
- **`MaxAttempts` sonrası yeniden deneme.** Yoklama kalıcı olarak yerleşmediğinde
  geçit bir daha denemiyor; K1 bunu düzeltmiyor, yalnız düğmeyi serbest bırakıyor.
