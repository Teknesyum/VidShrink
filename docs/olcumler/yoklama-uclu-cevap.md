# T137 — Yoklama üçlü cevabı

**Üçüncü cevap artık çağırana ulaşıyor.** `EncoderCapabilities.WorksAsEncoderState(codec)`
ve `MainWindow.DeferredEncoderAvailability.EncoderState(codec)` eklendi; ikisi de
`EncoderProbeState.Unmeasured` / `NotWorking` / `Working` döndürüyor, `Probe`/`AnswerFor`
gecidinin taşıdığı bilgiyi artık kaybetmiyor.

**Üretimde seçilen kodek K1-K3/K7 için değişmedi, K8 için değişti.** Aşağıda K5.

## K1 — Kırmızıdan yeşile

Kusur: `EncoderCapabilities.cs:70`teki `WorksAsEncoder` hâlâ iki değerli (arayüz
sözleşmesi gereği bilerek böyle bırakıldı — bkz. K2/K5); çağıran taraf üçüncü cevabı
görecek bir yol bulamıyordu.

Kırmızı kanıt (`WorksAsEncoderState` eklenmeden önce, test dosyasındaki yeni test
yerinde dururken derleme):

```
tests\VidShrink.Tests\EncoderAvailabilityTests.cs(193,62): error CS1061:
'EncoderCapabilities' bir 'WorksAsEncoderState' tanımı içermiyor ...
tests\VidShrink.Tests\EncoderAvailabilityTests.cs(194,63): error CS1061: ...
tests\VidShrink.Tests\EncoderAvailabilityTests.cs(196,23): error CS1061: ...
tests\VidShrink.Tests\EncoderAvailabilityTests.cs(197,24): error CS1061: ...
```

Düzeltme: `src/VidShrink.Ffmpeg/EncoderCapabilities.cs`e
`public EncoderProbeState WorksAsEncoderState(string codec) => Probe(codec).State;`
eklendi. Kusur/düzeltme ayrı commit'lerde (aşağıdaki "Kayıt noktaları").

## K2 — Ayırt etme kanıtı

Ölçüm: `EncoderAvailabilityTests.WorksAsEncoderStateOlcemediyleCalismiyoriAyirtEdiyor`.

İddia: Aynı tek-kodlayıcılı derleme listesinde, `EncoderProbeHook` sırayla
`Unmeasured` ve `Rejected` döndürdüğünde `WorksAsEncoder` ikisini de `false` yapıp
ayırt etmiyor (`WorksAsEncoderOlcemediyleCalismiyoriAyirtEtmiyor` bunu doğruluyor,
bilerek — arayüz sözleşmesi), ama `WorksAsEncoderState` ikisini `Unmeasured` ve
`NotWorking` olarak ayırt ediyor. İki test de yeşil (bkz. K6'daki tam koşum).

## K3 — Mutasyon × ölçüm tablosu

Her satır: düzeltme elle geri alındı, `dotnet build -c Release --no-incremental`
ile **yeniden derlendi** (`--no-build` kullanılmadı), ilgili ölçüm koşturuldu.

| Düzeltme | Geri alma | Ölçüm | Sonuç |
| --- | --- | --- | --- |
| K1: `WorksAsEncoderState` (Ffmpeg) | Metod silindi | `EncoderAvailabilityTests` derlemesi | Derleme **kırmızı**, CS1061 × 4 |
| K7: `EncoderState` (App) | Metod silindi | `PlanCalculatorProbeTests` derlemesi | Derleme **kırmızı**, CS1061 × 5 |
| K8: `Ready()` bekleme mantığı | `cooling` denetimi kaldırılıp eski `Attempts >= MaxAttempts` bırakıldı (sabit/alan derlemede kaldı) | `FirstFailureCooldownSonrasiYerlesenYoklamaylaTemizleniyor` | Derleme yeşil, test **kırmızı**: `Assert.Null(gate.FirstFailure)` → `Actual: "gecici yoklama hatasi"` |
| K9: `dogrudan.State` iddiası | Yoklanan kodek `"libsvtav1"` → var olmayan `"bogus_codec_adi_yok"` yapıldı | `TheRealSoftwareProbeDurationIsMeasured` | Derleme yeşil, test **kırmızı**: `Expected: Working, Actual: NotWorking` — eski `Assert.All(d < ProbeKillMs)` iddiası bu mutasyonda bile yeşil kalırdı (süre karşılaştırması kodek adından bağımsız), yeni iddia gerçekten kırılıyor |

Her satırda düzeltme geri yüklenip `dotnet build -c Release --no-incremental` +
ilgili test tekrar koşturularak yeşile dönüldüğü doğrulandı.

## K4 — `tools/surucu-yoklugu` hizalaması

Araç zaten ham API'yi (`EncoderCapabilities.Probe`) çağırıyordu; `Ayrisma()`'nın
çıktısına ayrı bir `Durum` sütunu eklendi (`probe.State`). Gerçek ffmpeg ile canlı
koşum (`dotnet run ... -- ayrisma`):

```
kodlayici      HasEncoder  WorksAsEncoder  Durum        yoklama_ms
h264_nvenc     True        True            Working      184
h264_qsv       True        False           NotWorking   48
hevc_nvenc     True        True            Working      151
av1_nvenc      True        True            Working      159
```

Bu makinede tüm NVENC adayları yoklamayı geçti; QSV/AMF donanımı yok, `NotWorking`
olarak doğru ayrışıyor. `Unmeasured` satırı bu koşumda oluşmadı (yoklama sınırına
dayanan bir aday yoktu) — sütun üçüncü cevabı taşımaya hazır, üretmesi yük/zaman
aşımı durumuna bağlı.

## K5 — Üretim davranışı değişti mi

**K1-K3 ve K7: değişmiyor.** `grep -rn "\.EncoderState(\|\.WorksAsEncoderState("
src/ tools/` yalnız iki metodun kendi tanımını buluyor — hiçbir üretim çağıranı
(`PlanCalculator.PickCodec`/`PickFastCodec`, `PerformanceProbe.SelectHardwareCodec`)
bu yeni metotları kullanmıyor; ikisi de hâlâ iki değerli `WorksAsEncoder`
üzerinden `IEncoderAvailability`/`IEncoderMeasurementState.IsMeasured` gecidiyle
çalışıyor. Bu iki dosya `owns:` dışında olduğu için T137 onları değiştirmedi
(bkz. "Sınır" bölümü).

**K8: değişiyor.** `_planEncoders` (yani `DeferredEncoderAvailability`), `MainWindow`
içinde `PlanCalculator.BuildDetailed`/`TargetMbForQuality` çağrılarına doğrudan
geçiriliyor (`MainWindow.axaml.cs:1776,1790,1855,2381`). Önceki mantıkta bir
kodek iki deneme boyunca yoklama hatası verirse `Ready()` sonsuza dek `false`
dönüyordu — `IsMeasured` hiç `true` olmuyor, `PlanCalculator` o kodeği hep
"henüz ölçülmedi" gecidinde bırakıyordu, oturum boyunca. Düzeltmeden sonra
5000 ms'lik bekleme geçince yeniden denenir; deneme yerleşirse (`Working`)
`PlanCalculator` artık o donanım kodekini gerçekten seçebilir. Kanıt:
`FirstFailureCooldownSonrasiYerlesenYoklamaylaTemizleniyor` — cooldown sonrası
`gate.WorksAsEncoder("av1_nvenc")` `true` dönüyor, önceki mantıkta bu asla
gerçekleşmezdi (K3 tablosundaki kırmızı satır).

## K6 — verify

```
dotnet test tests/VidShrink.Tests/VidShrink.Tests.csproj -c Release --no-build \
  --filter "EncoderAvailabilityTests|PlanCalculatorTests|PlanCalculatorProbeTests" \
  --logger "console;verbosity=normal"

Toplam test sayısı: 64
     Geçti: 64
 Toplam süre: 25,8322 Saniye
```

Filtre `--list-tests` ile önceden doğrulandı (64 test eşleşti, sıfır eşleşmeli
ölü kol yok). CI: push sonrası `gh run list` ile bir kez bakılacak, sonucu bu
belgeye eklenmeyecek — dönüş metninde ayrı raporlanacak.

## K7 — App tarafı ikiz

`MainWindow.axaml.cs:1312`teki `DeferredEncoderAvailability.WorksAsEncoder` de
Ffmpeg tarafıyla aynı kusuru taşıyordu (varsayılan arayüz metodu `WorksAsEncoder
? Working : NotWorking` diye daraltıyordu — `Failed`/`Unsettled` ayrımı kayboluyordu).
`EncoderState(codec)` eklendi, `AnswerFor(codec)`in dört durumunu (`Working`,
`NotWorking`, `Unsettled`, `Failed`, `Unknown`) doğru üç duruma indiriyor.
Ölçüm: `EncoderStateGecidinDortDurumunuUcDurumaDogruDusuruyor` — istisna atan
kodlayıcı ve hiç yoklanmamış kodlayıcı `Unmeasured`, olculmus ret `NotWorking`,
olculmus kabul `Working`. Mutasyon: K3 tablosunun ikinci satırı.

## K8 — İki debt

**a) `FirstFailure` yapışkanlığı.** Kök neden `Ready()`teki kalıcı kilitti:
`Attempts >= MaxAttempts` sonsuza dek `false` dönüyordu, `EncoderCapabilities.
Instance`'ın zaten kullandığı `ReloadAfterFailureMs=5000` deseni App tarafında
yoktu. `RetryAfterFailureMs=5000` + `Answer.LastAttemptTicks` eklenip `Ready()`e
bekleme-sonrası-yeniden-dene mantığı kondu. `ReportUnsettledProbe` da artık
yalnız probu tarafından yazılmış metni temizliyor (`_probeStatusShown` bayrağı) —
önceki hâliyle `else return;` durumu hiç temizlemiyordu; naif bir
`else text = string.Empty;` denemesi de yanlış olurdu, çünkü
`TxtSystemStatus.Text` bağlantı/araç/ayar hatalarınca da yazılıyor
(`grep -n "TxtSystemStatus" src/VidShrink.App/MainWindow.axaml.cs` → 478, 700,
704, 796, 819, 1098, 1469, 1521, 1656, 3040 satırları) — bayraksız temizlik
o mesajları da silerdi. Ölçüm ve mutasyon: K3 tablosunun üçüncü satırı.

**b) `hdr10:` anahtarlarının da taranması.** `AnswerFor`/`EncoderState`
yalnız `"works:{codec}"` anahtarına bakıyor (`Key("works", codec)`), `hdr10:`
anahtarına dokunmuyor — bu, kod okumasıyla doğrulandı, ayrı bir tarama yok.
Karar: **bilerek böyle.** `Hdr10PixelFormat`/`IsHdr10Measured` zaten kendi ayrı
gecidini kullanıyor (`Key("hdr10", codec)`); `WorksAsEncoder`/`EncoderState`in
HDR10 anahtarlarını da taraması iki farklı yoklamayı (kodek çalışıyor mu / HDR10
piksel formatı destekleniyor mu) birbirine karıştırırdı. Kod değişikliği yok,
sadece bu karar yazılı hâle getirildi.

## K9 — Anlamsız ölçüm

`PlanCalculatorProbeTests.cs:448`teki
`Assert.All(durations, d => Assert.True(d < EncoderCapabilities.ProbeKillMs))`
gerçek süreler (60-100 ms) ile 15 000 ms sınırı arasındaki uçurum yüzünden
mutasyona dayanıksızdı — killer mantığı komple bozulsa bile yeşil kalırdı.
`MachineIsQuiet` kapısının altında olduğu için de çoğu koşumda hiç çalışmıyordu.

Düzeltme: kapının **üstüne**, gerçekten kırılabilir yeni bir iddia eklendi —
`FreshCapabilities().Probe("libsvtav1")` çağrılıp `dogrudan.State ==
EncoderProbeState.Working` doğrulanıyor (K3 tablosunun dördüncü satırı: yoklanan
kodek adı bozulunca `NotWorking` dönüp iddia gerçekten kırılıyor). Eski sabit
karşılaştırma tamamen kaldırıldı; 8 tekrarlık süre listesi artık yalnız kanıt
dosyasına yazılıyor (`WriteEvidence`), "ölçüldü" iddiası taşımıyor.

`docs/olcumler/baslat-kilidi.md:159-161`teki eski sunum de düzeltildi — o metin
bu zayıf iddiayı "gerçek bir sınıra bağlandı" diye sunuyordu; artık T137'nin
bulgusuna ve yeni ölçüme atıf yapıyor.

## Sınır — üretimde kalan asıl kusur

`src/VidShrink.Ffmpeg/PerformanceProbe.cs`teki `SelectHardwareCodec` ve
`src/VidShrink.Core/PlanCalculator.cs`teki `PickCodec`/`PickFastCodec`,
donanım kodek seçiminde hâlâ doğrudan iki değerli `WorksAsEncoder`i çağırıyor
(`IEncoderAvailability` arayüzü üzerinden). Bu iki dosya T137'nin `owns:`
listesinde değil; asıl "yoklayamama = çalışmıyor" karışıklığı bu üretim yolunda
hâlâ var — T137 yalnız üçüncü cevabı *taşıyacak* API'yi ekledi
(`WorksAsEncoderState`/`EncoderState`), bu iki dosyayı ona bağlamadı. Devredilen
borç: gelecekteki bir sözleşme `SelectHardwareCodec`i `WorksAsEncoderState`e
taşırsa, `Unmeasured` bir adayı atlayıp bir sonrakine geçmek yerine yeniden
yoklamayı deneyebilir hâle gelir.
