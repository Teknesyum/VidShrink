# T137 — Yoklama üçlü cevabı

**Üçüncü cevap artık kullanıcıya ulaşan yolda taşınıyor.** Tur 1'de üçüncü cevap
*üretildi* ama tüketilmedi: geçidin girişi iki değerli API'den okuyordu, `Unmeasured`
`EncoderState`e varamadan ölüyordu. Tur 2 bunu ve iki kardeş kusuru kapattı.

Tur 1'in üç iddiası bu turda **geri çekildi**; eski metinleri bu dosyada bırakılmadı,
hangi cümlenin ne olduğu T3 / T6 / "Üretim davranışı" bölümlerinde eski-yeni olarak
yazılı.

## Tur 2 kayıt noktaları

| Commit | Ne |
| --- | --- |
| `879c88b` | Kusurlar üretildi (T1, T2, T4, T6) — dördü de **kırmızı**, düzeltme yok |
| `7555059` | Dört kusur düzeltildi — dört ölçü **yeşile** döndü |
| `e7246f0` | T9 ffmpeg'siz ikiz ölçü + `surucu-yoklugu` sütun etiketi |
| `fb6f782` | T1 ölçüsü geçidin iki cevabını kanıt dosyasına yazıyor |

`src/VidShrink.App/MainWindow.axaml.cs` bu turda son kez `7555059`de değişti;
aşağıdaki satır numaraları `7555059`den `fb6f782`ye kadar aynı.

## T1 — Geçidin girişi üçüncü cevabı içeri alıyor

Kusur (`MainWindow.axaml.cs`, tur 1'deki hâli):

```csharp
else works = _source.WorksAsEncoder(codec);
```

Düzeltme (`:1429-1432`):

```csharp
private EncoderProbeState ProbedEncoderState(string codec)
    => _source is IEncoderProbeState prober
        ? prober.WorksAsEncoderState(codec)
        : _source.WorksAsEncoder(codec) ? EncoderProbeState.Working : EncoderProbeState.NotWorking;
```

`IEncoderProbeState`, `EncoderCapabilities.cs:14-17`e eklendi. Naif çözüm —
`_source.WorksAsEncoder(codec)` çağırıp sonra `_source.EncoderState(codec)` okumak —
sahte kaynakları **iki kez** yoklardı (`IEncoderAvailability`in varsayılan
`EncoderState`i `WorksAsEncoder`ı yeniden çağırıyor, `IEncoderAvailability.cs:22-23`);
`RepeatedRecalculatesDoNotRepeatTheProbe`in yoklama sayısı iddiası kırılırdı. Arayüz
denetimiyle her iki kolda da **tek** çağrı kalıyor.

Ölçü: `PlanCalculatorProbeTests.TheGateEntranceKeepsTheUnmeasuredAnswer`.
Ölçünün yazdığı kanıt (`.calisma/test-ciktilari/t130-yoklama/olcum.txt`):

```
T137/T1 App gecidi olcemeyen : Unmeasured / IsMeasured=False
T137/T1 App gecidi calismayan: NotWorking / IsMeasured=True
```

Denetçinin ölçtüğü birebir aynı `NotWorking / IsMeasured=True` çifti artık ayrışıyor
ve hiç ölçülmemiş kodek `IsMeasured=False` dönüyor. Mutasyon: aşağıdaki tabloda M5.

## T2 — `WorksAsEncoder`ın seçilen biçimi

**Seçilen biçim: ikincisi** — iki değerli imza kalıyor, ama `Unmeasured` artık "yok"
demiyor. Gerekçe: birinci biçim (`WorksAsEncoder`ı üç değerli cevaba taşımak)
`IEncoderAvailability.WorksAsEncoder` imzasını değiştirmeyi gerektiriyor; o dosya
`src/VidShrink.Core/` altında ve T137'nin `owns` listesinde değil.

`EncoderCapabilities.cs:93-97`in yeni hâli:

```csharp
public bool WorksAsEncoder(string codec)
{
    var result = Probe(codec);
    return result.Measured ? result.Succeeded : HasEncoder(codec);
}
```

Karar cümlesi: **ölçüm ekleyemediğinde zaten bilinen bilgi silinmez.** Yoklama sonuca
varamadıysa (zaman aşımı ya da süreç hiç başlayamadı) yoklamadan önce bilinene —
ffmpeg'in kendi kodlayıcı listesine — düşülür. Listede olmayan kodlayıcı ölçülmüş bir
yokluktur, o hâlâ `false`.

İki ölçü: `EncoderAvailabilityTests.WorksAsEncoderOlcemediyiCalismiyordanAyiriyor`
(ölçemeyen `true`, ölçülmüş ret `false`, ikisi eşit değil) ve
`OlcemeyenYoklamaListedeOlmayanKodlayiciyiVarSaymiyor` (ölçemeyen yoklama listede
olmayan kodeği var saymıyor — sınırın öbür ucu). Mutasyon: M6.

## T3 — Tur 1'in K1 cümlesi

**Geri çekilen cümle (tur 1, `:29`):**

> Düzeltme: `src/VidShrink.Ffmpeg/EncoderCapabilities.cs`e
> `public EncoderProbeState WorksAsEncoderState(string codec) => Probe(codec).State;`
> eklendi. Kusur/düzeltme ayrı commit'lerde (aşağıdaki "Kayıt noktaları").

Yanlıştı: `69854f9` kusur testini ve düzeltmeyi aynı commit'te taşıyordu, ve
yönlendirdiği "Kayıt noktaları" bölümü dosyada yoktu.

**Yeni:** cümle kaldırıldı. Bu turda kusur ve düzeltme gerçekten ayrı commit'lerde
(`879c88b` kırmızı, `7555059` yeşil); listesi yukarıdaki "Tur 2 kayıt noktaları"
tablosunda ve o tablo bu dosyada mevcut.

## T4 — Yeniden denemeye tavan

`Ready()` iki hızlı denemeden sonra 5000 ms bekleyip yeniden deniyordu, **tavan
yoktu**: oturum boyunca 5 sn'de bir ffmpeg doğabiliyordu. Konan tavan:

```csharp
internal const int MaxTotalAttempts = 3;
...
if (answer.Settled) return true;
if (answer.Attempts >= MaxTotalAttempts) return false;
var stuck = answer.Attempts >= MaxAttempts;
```

Değer 3, T130'un "en çok bir kez daha denenir" ruhunu koruyor: iki hızlı deneme +
soğuma sonrası **bir** deneme. Ondan sonra cevap bilinmeyen kalır ve `Unsettled`
üzerinden arayüze taşınır — sessizce "çalışmıyor"a düşmez.

Ölçü: `PlanCalculatorProbeTests.TheRetryCeilingStopsTheProbeStorm` — iki soğuma turu
boyunca geçit boşaltılıyor, sonunda `patlayan.Probes` `MaxTotalAttempts`e eşit
kalıyor. Mutasyon: M7.

## T5 — Belge kod ile uyuştu

**Eski (`main` `72064d8`, `MaxAttempts` üstü):**

> Yerleşmeyen bir yoklama en çok bir kez daha denenir. Sınır olmasa ölçüm ile yeniden
> hesap birbirini besleyip sonsuz yoklama üretirdi.

**Yeni (`:1233-1236`):**

> Yerleşmeyen bir yoklama art arda en çok bu kadar denenir; sonrası
> `RetryAfterFailureMs` beklemeye tabidir.

**Eski (`main` `72064d8`, `Ready` üstü):**

> Yoklama yerleşmediyse cevap "ölçüldü" sayılmaz: bir kez daha denenir, o da
> yerleşmezse deneme durur ama cevap yine bilinmeyen kalır. ...

**Yeni (`:1393-1401`):**

> Yoklama yerleşmediyse cevap "ölçüldü" sayılmaz. Deneme sırası şu: `MaxAttempts`
> kadar art arda denenir, sonra `RetryAfterFailureMs` beklenip **bir kez daha**
> denenir (`MaxTotalAttempts`), ondan sonra deneme **durur** ama cevap yine
> bilinmeyen kalır. ...

## T6 — `_probeStatusShown` iddiası geri çekildi, davranış düzeltildi

**Geri çekilen cümle (tur 1, `:130-137`):**

> `ReportUnsettledProbe` da artık yalnız probu tarafından yazılmış metni temizliyor
> (`_probeStatusShown` bayrağı) — önceki hâliyle `else return;` durumu hiç
> temizlemiyordu; naif bir `else text = string.Empty;` denemesi de yanlış olurdu,
> çünkü `TxtSystemStatus.Text` bağlantı/araç/ayar hatalarınca da yazılıyor ...
> bayraksız temizlik o mesajları da silerdi.

Yanlıştı: `bool _probeStatusShown` yalnız "prob bir kez yazdı mı" tutuyordu, "şu anki
metin probun mu" tutmuyordu. Prob hatası → başka altsistem yazısı → prob yerleşmesi
sırasında **ilgisiz metin siliniyordu.**

**Davranış düzeltildi** (iddia zayıflatılmadı). Bayrak metnin kendisini tutuyor:

```csharp
private string? _probeStatusText;
...
if (text is not null)
{
    if (TxtSystemStatus.Text != text) TxtSystemStatus.Text = text;
    _probeStatusText = text;
}
else if (_probeStatusText is not null)
{
    if (TxtSystemStatus.Text == _probeStatusText) TxtSystemStatus.Text = string.Empty;
    _probeStatusText = null;
}
```

Ölçü: `PlanCalculatorProbeTests.TheProbeStatusDoesNotEraseAnUnrelatedMessage` —
yoklama hata verip sonra iyileşirken, her iyileşme turundan hemen sonra
`TxtSystemStatus.Text` yakalanıyor; hiçbir yakalama boş olmamalı. Kurban elle
enjekte edilmiş bir metin değil, `UpdateToolStatus()`un kendi yazdığı FFmpeg yolu:
enjeksiyon `ApplyLoaded` içindeki sıra yüzünden (`UpdateToolStatus` → `Recalculate`)
yanlış yeşil üretiyordu. Ölçü başsız pencerede İngilizce metin gördüğü için
karşılaştırma `OrdinalIgnoreCase`. Mutasyon: M8.

## T7 — `grep -n "TxtSystemStatus"` gerçek çıktısı

Tur 1'in sunduğu on satır numarası (478, 700, 704, 796, 819, 1098, 1469, 1521, 1656,
3040) hiçbir commit'te üretilemiyordu; **geri çekildi.** Komut ve çıktısı, commit
`fb6f782`:

```
$ grep -n "TxtSystemStatus" src/VidShrink.App/MainWindow.axaml.cs
479:        catch (Exception ex) { TxtSystemStatus.Text = $"{Say("main.error.link")}: {ex.Message}"; }
701:            TxtSystemStatus.Text = Say("main.about.tool-missing", missing);
705:        TxtSystemStatus.Text = string.Join("\n",
797:            TxtSystemStatus.Text = $"{Say("settings.error.save")}: {ex.Message}";
820:            TxtSystemStatus.Text = $"{Say("settings.error.reset")}: {ex.Message}";
1099:            TxtSystemStatus.Text = $"{Say("main.error.setting")}: {ex.Message}";
1511:            TxtSystemStatus.Text = $"{Say("main.error.probe")}: {ex.Message}";
1563:            TxtSystemStatus.Text = $"{Say("main.error.setting")}: {ex.Message}";
1581:            TxtSystemStatus.Text = $"{Say("main.error.setting")}: {ex.Message}";
1698:        TxtSystemStatus.Text = message;
1970:            if (TxtSystemStatus.Text != text) TxtSystemStatus.Text = text;
1975:            if (TxtSystemStatus.Text == _probeStatusText) TxtSystemStatus.Text = string.Empty;
3104:            TxtSystemStatus.Text = $"{Say("main.error.folder")}: {ex.Message}";
```

Elle sayıldı: **13 satır.** İkisi `ReportUnsettledProbe`ın kendi satırları (1970,
1975); kalan 11'i başka altsistemlerin yazdığı metinler — T6'nın kurbanı bunlar.

## T8 — `tools/surucu-yoklugu` tam tablosu

Araç `EncoderCapabilities.Probe`ı çağırıyor; `Ayrisma()` çıktısına `Durum` sütunu
(`probe.State`) tur 1'de eklendi. Tur 2'de **sütun etiketi düzeltildi**:
`WorksAsEncoder` başlıklı sütun aslında `probe.Succeeded` yazıyordu; T2'den sonra bu
ikisi ayrışıyor, artık gerçekten `caps.WorksAsEncoder(codec)` yazıyor.

Gerçek ffmpeg ile canlı koşum — **kesit değil, yedi adayın tamamı**
(`dotnet run --project tools/surucu-yoklugu -c Release -- ayrisma`):

```
kodlayici      HasEncoder  WorksAsEncoder  Durum        yoklama_ms
h264_nvenc     True        True            Working      287
h264_qsv       True        False           NotWorking   65
h264_amf       True        False           NotWorking   35
hevc_nvenc     True        True            Working      240
hevc_qsv       True        False           NotWorking   61
hevc_amf       True        False           NotWorking   32
av1_nvenc      True        True            Working      267
```

Bu makinede üç NVENC adayı yoklamayı geçti; QSV/AMF donanımı yok ve `NotWorking`
olarak doğru ayrışıyor. `Unmeasured` satırı bu koşumda oluşmadı: makine boştayken
hiçbir yoklama sınıra dayanmıyor.

## T9 — ffmpeg'siz ortamda sessiz yeşil

Karar: **ikisi birden.** İddia ffmpeg gerektirmeyen bir kola taşındı *ve* eski kol
atlarken görünür iz bırakıyor.

- `TheGateSettlesOnTheMeasuredDurationNotTheKillLimit` — `EncoderProbeHook` ile
  sürülen, süreç doğurmayan ikiz. K9'un iddiasını (`Settled` gerçek ölçülen süreye
  bağlı, öldürme sınırına değil) ffmpeg olmadan tutuyor; her ortamda koşuyor.
- `TheRealSoftwareProbeDurationIsMeasured` — ffmpeg yokken `return` etmeden önce
  kanıt dosyasına atlama satırı yazıyor.

ffmpeg PATH'ten gizlenerek koşum (`PATH` yalnız dotnet + System32):

```
Başarılı!  - Başarısız: 0, Başarılı: 2, Atlanan: 0, Toplam: 2, Süre: 4 s

$ cat .calisma/test-ciktilari/t130-yoklama/olcum.txt
TheRealSoftwareProbeDurationIsMeasured: ATLANDI — ffmpeg yok, gercek yoklama iddiasi
kosmadi; ffmpeg gerektirmeyen karsiligi TheGateSettlesOnTheMeasuredDurationNotTheKillLimit
```

4 saniyelik süre ikizin gerçekten koştuğunu gösteriyor. Mutasyon: M9.

## Mutasyon × ölçüm tablosu (tur 2)

Her satır: düzeltme elle geri alındı, `dotnet build -c Release --no-incremental` ile
**yeniden derlendi** (`--no-build` kullanılmadı), ilgili ölçüm koşturuldu, sonra
düzeltme geri yüklendi. Beşi de geri yüklendikten sonra verify 69/69 yeşil (aşağıda);
ızgara ağaçta iz bırakmadı.

| # | Geri alınan düzeltme | Kırılan ölçü | Sonuç |
| --- | --- | --- | --- |
| M5 | `ProbedEncoderState` iki değerli API'ye döndürüldü | `TheGateEntranceKeepsTheUnmeasuredAnswer` | **Kırmızı** — `Başarısız: 1, Başarılı: 1, Toplam: 2`; `RepeatedRecalculatesDoNotRepeatTheProbe` yeşil kaldı (çift çağrı tuzağı yok) |
| M6 | `WorksAsEncoder` → `Probe(codec).Succeeded` | `WorksAsEncoderOlcemediyiCalismiyordanAyiriyor` | **Kırmızı** — `Başarısız: 1, Başarılı: 1, Toplam: 2` |
| M7 | `if (answer.Attempts >= MaxTotalAttempts) return false;` silindi | `TheRetryCeilingStopsTheProbeStorm` | **Kırmızı** — `Başarısız: 1, Toplam: 1` |
| M8 | Durum yazısı koşulsuz siliniyor (`_probeStatusText` denetimi kaldırıldı) | `TheProbeStatusDoesNotEraseAnUnrelatedMessage` | **Kırmızı** — `Başarısız: 1, Toplam: 1` |
| M9 | `answer.Settled = false;` | `TheGateSettlesOnTheMeasuredDurationNotTheKillLimit` | **Kırmızı** — `Başarısız: 1, Toplam: 1` |

Elle sayıldı: **beş mutasyon, beşi de öldürüldü.** M5 ve M6'nın kırdığı ölçülerin
adları koşum çıktısından:

```
Başarısız VidShrink.Tests.PlanCalculatorProbeTests.TheGateEntranceKeepsTheUnmeasuredAnswer [28 ms]
Başarısız VidShrink.Tests.EncoderAvailabilityTests.WorksAsEncoderOlcemediyiCalismiyordanAyiriyor [< 1 ms]
```

Denetçinin M1-M4 ızgarası bozulmadı; bu beş satır onların üstüne geldi.

## T11 — `baslat-kilidi.md` mutasyon tablosu

`docs/olcumler/baslat-kilidi.md:176`daki `Toplam: 70` satırı T130 zamanına aitti.
Aynı mutasyon (`answer.Settled = false;`) aynı filtreyle
(`--filter "PlanCalculatorProbeTests|LanguageTests"`) commit `e7246f0`de yeniden
koşturuldu: `Başarısız: 8, Başarılı: 68, Atlanan: 0, Toplam: 76`. O satır ve altındaki
kırılan-ölçü listesi güncellendi.

## Değişmeyen — HDR10 anahtarları

`AnswerFor`/`EncoderState` yalnız `"works:{codec}"` anahtarına bakıyor, `hdr10:`
anahtarına dokunmuyor. Karar: **bilerek böyle.** `Hdr10PixelFormat`/`IsHdr10Measured`
zaten kendi ayrı gecidini kullanıyor (`Key("hdr10", codec)`); ikisini birleştirmek
"kodek çalışıyor mu" ile "HDR10 piksel formatı destekleniyor mu" sorularını
karıştırırdı. Kod değişikliği yok.

## Üretim davranışı — tur 1'in "Sınır" iddiası geri çekildi

**Geri çekilen cümle (tur 1, "Sınır — üretimde kalan asıl kusur"):**

> `PerformanceProbe.SelectHardwareCodec` ve `PlanCalculator.PickCodec`/`PickFastCodec`
> ... hâlâ doğrudan iki değerli `WorksAsEncoder`i çağırıyor ... T137 yalnız üçüncü
> cevabı *taşıyacak* API'yi ekledi, bu iki dosyayı ona bağlamadı.

Tur 2'den sonra doğru değil: `WorksAsEncoder`ın kendisi değişti, o çağıranlar hiç
dokunulmadan yeni davranışı alıyor.

`WorksAsEncoderState` çağıran üretim satırı — elle sayıldı, **bir tane**:

| Satır |
| --- |
| `src/VidShrink.App/MainWindow.axaml.cs:1431` (`ProbedEncoderState`) |

`WorksAsEncoder` çağıran üretim satırları — elle sayıldı, **altı tane**:

| Satır | Ne yapıyor |
| --- | --- |
| `src/VidShrink.App/MainWindow.axaml.cs:1432` | Geçidin, `IEncoderProbeState` olmayan kaynak için yedek kolu |
| `src/VidShrink.Core/HdrResolver.cs:82` | HDR10 kodek elemesi |
| `src/VidShrink.Core/IEncoderAvailability.cs:23` | Varsayılan `EncoderState` gövdesi |
| `src/VidShrink.Core/PlanCalculator.cs:889` | `PickCodec` — tercih edilen kodek |
| `src/VidShrink.Core/PlanCalculator.cs:909` | `PickCodec`/`PickFastCodec` — aday sırası |
| `src/VidShrink.Ffmpeg/PerformanceProbe.cs:97` | `SelectHardwareCodec` — aday sırası |

Altısı da `IEncoderAvailability` üzerinden çağırıyor; üretimde arkalarındaki somut
sınıf `EncoderCapabilities` (doğrudan, ya da `DeferredEncoderAvailability`in `_source`u
olarak). **Değişen davranış:** yoklaması sonuca varamayan bir donanım kodeki artık
"yok" sayılıp elenmiyor, ffmpeg listesinde varsa aday kalıyor. Ölçülmüş ret ve listede
hiç olmama davranışı aynı.

## verify

```
dotnet test tests/VidShrink.Tests/VidShrink.Tests.csproj -c Release --no-build \
  --filter "EncoderAvailabilityTests|PlanCalculatorTests|PlanCalculatorProbeTests" \
  --logger "console;verbosity=normal"

Toplam test sayısı: 69
     Geçti: 69
 Toplam süre: 59,4918 Saniye
```

Filtrenin üç kolu `--list-tests` ile tek tek sayıldı — sıfır eşleşmeli ölü kol yok:
`EncoderAvailabilityTests` 12, `PlanCalculatorTests` 32, `PlanCalculatorProbeTests` 25.
Toplamı 69, koşum sayısıyla uyuşuyor.
