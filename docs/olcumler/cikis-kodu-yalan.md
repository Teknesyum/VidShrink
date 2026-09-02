# Çıkış kodu yalan söylüyor: ffmpeg 0 dönüp ayarı sessizce düşürüyor

Sözleşme T144. Ölçüm makinesi: Windows 11 Pro 10.0.26100, ffmpeg 9.0-full_build-www.gyan.dev
(libavcodec 63.1.100), SVT-AV1 v4.2.0-68-gc1e79b04f, x265 4.3+2-5ab552e.

## K1 — Kusur önce ölçüldü

### Ölçüm A: tanınmayan anahtar, çıkış kodu 0

```
ffmpeg -hide_banner -loglevel info -f lavfi -i "testsrc2=size=128x128:rate=30:duration=0.1" \
       -c:v libsvtav1 -svtav1-params zzznotreal=1 -frames:v 2 -f null NUL
```

Çıkış kodu **0**. stderr'in 35 satırından 12.'si:

```
[libsvtav1 @ 00000138e021a4c0] Error parsing option zzznotreal: 1.
```

`docs/olcumler/handbrake-acigi.md:139` ve üç tarama belgesindeki ölçüm bu makinede yeniden
üretildi. Aynı davranış iki kodlayıcıda daha ölçüldü; tam liste K2'de.

### Bugün ne kontrol ediliyor — teslim yolunun tam kapı listesi

`EncodeRunner.RunAsync` bir denemeyi teslime çevirirken dokuz karar noktasından geçiyor.
Ham çıktı (`grep -n`, K1 commit'indeki dosya üzerinde):

```
118:            while (attempt < MaxAttempts)
136:                var actualMb = new FileInfo(partialPath).Length / 1024.0 / 1024.0;
139:                var over = actualMb > effectiveTargetMb * ToleranceOver;
140:                var belowBand = !over && fillPolicy == FillPolicy.FillTarget && actualMb < band.LowerMb;
144:                var retryUnderBand = underBand && attempt < MaxAttempts
179:                        if (fallbackPlan is not null && File.Exists(fallbackPath))
191:                    if (attempt >= MaxAttempts)
205:                        if (!await askBeforeRetry(prompt, ct))
449:        if (outcome.ExitCode != 0)
```

| # | Kapı | Neye bakıyor |
|---|---|---|
| 1 | `attempt < MaxAttempts` | deneme tavanı (3) |
| 2 | `new FileInfo(partialPath).Length` | dosya yoksa fırlatır — örtük varlık kapısı |
| 3 | `actualMb > effectiveTargetMb * 1.0` | boyut tavanı |
| 4 | `actualMb < band.LowerMb` | boyut tabanı (yalnız `FillTarget`) |
| 5 | `retryUnderBand` | yeniden deneme hakkı |
| 6 | `File.Exists(fallbackPath)` | geri düşülecek dosya var mı |
| 7 | `attempt >= MaxAttempts` | koşumu bitir |
| 8 | `askBeforeRetry` | kullanıcının kararı |
| 9 | `outcome.ExitCode != 0` | ffmpeg'in kendi sonucu |

Dokuz kapının **biri** ffmpeg'in ne dediğine bakıyor (9), o da yalnız çıkış koduna.
**Hiçbiri stderr metnini okumuyor.** Kusur "yalnız çıkış koduna bakılıyor" değil; teslim
yolunda boyut ve dosya kapıları da var. Doğru ifade: *ffmpeg'in ne yaptığını anlatan tek
sinyal çıkış kodu, ve o sinyal bu durumda yalan söylüyor.*

`FfmpegRunner`de `Ok` üç yerde belirleniyor (`FfmpegRunner.cs:58`, `:73`, `:82`); ikisi
süreç hiç koşmadığı durumlar, üçüncüsü `exitCode == 0`. Yine hiçbiri stderr metnini
okumuyor — `StandardError` tutuluyor ama karara girmiyor.

### Kuyruk penceresi tanıyı zaten kaçırıyor

`EncodeRunner` hata metni için son 15 satırı tutuyordu. Ölçüm A'da tanı satırı 35 satırın
12.'si: son 15 satırın **içinde değil**. Yani hata mesajı bir şekilde gösterilseydi bile
sebep görünmeyecekti. Bu, kırmızı olmayıp ilk turda yeşil geçen dördüncü ölçüyle pimlendi
(`TheDiagnosticLineDoesNotSurviveTheTailWindow`).

### İki kırmızının ham metni

`dotnet test -c Release --filter "EncodeRunnerDroppedOptionTests|FfmpegRunnerTests"`:

```
  Başarısız VidShrink.Tests.EncodeRunnerDroppedOptionTests.ARealEncodeThatDropsAnOptionReportsTheDrop [113 ms]
  Hata İletisi:
   Assert.Contains() Failure: Filter not matched in collection
Collection: []
  Başarısız VidShrink.Tests.EncodeRunnerDroppedOptionTests.ExitZeroWithADroppedOptionDoesNotFailButTheDropIsCarried [< 1 ms]
  Hata İletisi:
   Assert.NotEmpty() Failure: Collection was empty
  Başarısız VidShrink.Tests.FfmpegRunnerTests.ExitZeroWithADroppedOptionIsStillOkButTheDropIsCarried [1 ms]
  Hata İletisi:
   ffmpeg 'Error parsing option zzznotreal: 1.' yazip 0 ile dondu; dusurulen ayar sonuca tasinmali.

Başarısız! - Başarısız:     3, Başarılı:     1, Atlanan:     0, Toplam:     4, Süre: 142 ms
```

Sözleşme iki kırmızı istedi; üç kırmızı var. Üçüncüsü (`ARealEncodeThatDropsAnOptionReportsTheDrop`)
sahte metin değil, **gerçek ffmpeg** koşumu: 0,1 saniyelik `testsrc2` kaynağı, `libx264`,
`-x264-params zzznotreal=1`. `ExitCode`ın 0 olduğu ilk savı geçiyor, düşürülen ayarı
taşıdığı ikinci savda kırılıyor — kullanıcıya teslim edilen yolun kendisi kırmızı.

### K1'de açılan sınır

Süreç başlatmak ile kararı ayırmak için iki `internal` giriş açıldı; ikisi de üretimin
kendi kod yolu, ölçüye özel kopya değil:

- `FfmpegRunner.Decide(int exitCode, string standardError, TimeSpan elapsed)`
- `EncodeRunner.StderrWatch` (satır satır besleniyor) + `EncodeRunner.ThrowIfFailed`

`InternalsVisibleTo("VidShrink.Tests")` bu derlemede zaten vardı (`TempCleanup.cs:5`);
yeni bir görünürlük eklenmedi.

## K2 — Tanılı hata sözlüğü tek yerde

Çözümleyicinin yeri: `src/VidShrink.Ffmpeg/FfmpegDiagnostics` (`FfmpegRunner.cs` içinde).
Üretimdeki iki çağrı yeri, ham çıktı:

```
src/VidShrink.Ffmpeg/EncodeRunner.cs:437:            if (FfmpegDiagnostics.ReportsADroppedOption(line)) dropped.Add(line);
src/VidShrink.Ffmpeg/FfmpegRunner.cs:138:               FfmpegDiagnostics.DroppedOptionLines(standardError));
```

İki koşucu da aynı sözlüğü okuyor; desen ikinci kez yazılmadı. `BothRunnersReadTheSameDictionary`
ölçüsü aynı stderr metnini iki yoldan geçirip çıkan listeleri eşitliyor.

### Sözlüğe giren desenler ve kanıt kaynakları

| Desen | Kodlayıcı | Çıkış kodu | Kanıt |
|---|---|---|---|
| `Error parsing option` | libsvtav1 | **0** | Ölçüm A, bu belge; `docs/olcumler/handbrake-acigi.md:139`; `docs/taramalar/lav-filters.md:23`; `mpv.md:23`; `svt-av1-psy.md:25` |
| `Error parsing option` | libx264 | **0** | Ölçüm D, bu belge: `[libx264 @ …] Error parsing option 'zzznotreal = 1'.` |
| `Unknown option:` | libx265 | **0** | Ölçüm D, bu belge: `[libx265 @ …] Unknown option: zzznotreal.` |

### Sözlüğe **girmeyen** desenler ve nedeni

Sözleşme `Unrecognized option` ve `Option not found` desenlerinin de "üç belgede ölçüldüğünü"
söylüyor. Saydım: bu iki ifade depoda tek bir belgede geçiyor
(`docs/olcumler/tepe-tavani-ve-psy.md:30`), orada da ölçüm olarak değil, `EncoderCapabilities`
sondasının ne yaptığının anlatımı olarak. `Error parsing option` gerçekten dört belgede;
diğer ikisi bir belgede. **Sözleşmenin cümlesi bu noktada tutmuyor.**

İkisini de bu makinede kendim ölçtüm — ölçüm C:

```
ffmpeg … -c:v libx264 -vsync 0 …      → çıkış kodu 8
Unrecognized option 'vsync'.
Error splitting the argument list: Option not found

ffmpeg … -c:v libx264 -zzznotreal 1 … → çıkış kodu 8
Unrecognized option 'zzznotreal'.
Error splitting the argument list: Option not found
```

İkisi de **sıfırdan farklı** kodla geliyor. Bu sözlüğün işi "çıkış kodu 0 iken düşürülen
ayar"; çıkış kodu zaten sıfırdan farklıysa çağıran onu kapı 9'da yakalıyor. Bu yüzden
ikisi de dışarıda bırakıldı — kanıtsız oldukları için değil, **bu kapının hiç göremeyeceği**
oldukları için.

Üçüncü bir ölçüm sınırı çiziyor: `-c:v libx264 -preset zzznotreal` çıkış kodu **127** ve
`Conversion failed!` veriyor. Yani sessiz düşme kodlayıcı parametre dizgisi yoluna
(`-*-params`) özgü; düz AVOption yolu yüksek sesle düşüyor.

### Depodaki mevcut sözlük hakkında bir bulgu (borç, `owns` dışı)

`src/VidShrink.Ffmpeg/EncoderCapabilities.cs:343-346` seçenek sondasında üç desen tutuyor:
`Error parsing option`, `Option not found`, `Unrecognized option`. Yukarıdaki ölçümlere göre
o listenin son iki deseni orada da atıl — sonda da `exitCode == 0` şartını ayrıca koyuyor.
Buna karşılık liste **libx265'in gerçekten yazdığı `Unknown option:` ifadesini içermiyor**;
yani x265 seçenek sondası bugün düşürülen anahtarı kaçırıyor. `EncoderCapabilities.cs`
bu sözleşmenin `owns` kümesi dışında, dokunulmadı. Borç olarak yazıldı.

## K3 — Yanlış pozitif ölçüsü

Sözlük fazla genişse çalışan kodlamayı düşürülmüş sayar. Ölçü, **hiçbir ayarı düşmemiş**,
çıkış kodu 0 ile biten gerçek koşumların çıktısını sözlükten geçirip listenin boş kaldığını
pimliyor (`CleanRunsAreNeverReadAsADroppedOption`, dört korpus).

| # | Korpus | Kaynak | İçindeki tuzak |
|---|---|---|---|
| 1 | temiz `libx265` | bu makinede ölçüldü, çıkış 0 | `x265 [warning]: Too few rows/columns, --wpp disabled`, `x265 [warning]: Source height < 720p; …`, `tools: rd=3 psy-rd=2.00 …`, `muxing overhead: unknown` |
| 2 | temiz `libx264` | bu makinede ölçüldü, çıkış 0 | `[swscaler @ …] deprecated pixel format used, make sure you did set range correctly` |
| 3 | ffmpeg sürüm banner'ı + kodek banner'ları | bu makinede ölçüldü, çıkış 0 | `configuration: --enable-…`, `Svt[info]: SVT [config]: …`, `frame= … speed=…` |
| 4 | `Past duration 0.999992 too large` | **bu makinede üretilemedi**, sözleşmeden alındı | muxer uyarısı |

Dördüncü korpus için dürüst olmak gerekiyor: `Past duration … too large` satırını bu ffmpeg
9.0 derlemesinde iki tetikleyiciyle (`rate=5` kaynağı `-r 30`'a; ses+video `-shortest -r 25`)
**üretemedim**. Satırı yine de korpusa koydum, çünkü bu ölçüde satırların hepsi *negatif*:
listeye girmesi yalnızca "buna da takılmıyoruz"un kapsamını genişletir, hiçbir savı
güçlendirmez. Ölçülmüş gibi göstermemek için tabloda ayrı işaretlendi.

Ayrıca `TheWordUnknownOnItsOwnIsNotEnough` üç satırı tek tek pimliyor. En kritik olanı
`muxing overhead: unknown`: **her başarılı koşumun son satırlarında geçiyor.** Sözlükteki
desen `Unknown option:` — iki nokta dahil. Desen `Unknown`'a kısaltılsaydı ölçülen üç
başarılı koşumun **üçü de** düşürülmüş sayılırdı. K5'te bu tam olarak mutasyonla kırıldı.

Sözlüğün dar tutulmasının ikinci gerekçesi K2'de: çıkış kodu sıfırdan farklı olan iki desen
dışarıda bırakıldı, çünkü genişlik burada bedava değil.

## K4 — Karar: öldürme, bildir

| Koşucu | Tanılı hata bulununca | Gerekçe |
|---|---|---|
| `EncodeRunner` (teslim dosyası) | Koşum **başarılı** sayılır; düşürülen satırlar `EncodeResult.DroppedOptions` ile taşınır | Dosya çalışıyor, yalnız bir psikogörsel ayarı taşımıyor. Koşumu öldürmek kullanıcıya **hiç dosya vermemek** demek; K3'ün dediği gibi bu, sessiz kalite kaybından büyük bir zarar |
| `FfmpegRunner` (önizleme borusu) | `Ok` değişmez; satırlar `FfmpegRun.DroppedOptions` ile taşınır | Aynı gerekçe, üstüne: tek tüketici `SegmentEncoder` önizleme koşturuyor, düşen bir ayar yüzünden önizlemeyi karartmak kullanıcıyı sebepsiz bloklar |

Karar iki koşucuda **aynı**. Sözleşme farklı olabileceğini söylüyordu; farklı yapmak için
bir gerekçe bulamadım — ikisinde de düşürülen ayar, koşumun kendisi hakkında değil,
koşumun *ne kadarının uygulandığı* hakkında bilgi. Bunu başarısızlığa çevirmek her iki
tarafta da çalışan işi atmak olurdu.

Kararı tutan üç ölçü:

- `ADroppedOptionNeverFailsTheDeliveryPath` — çıkış 0 + düşürülen ayar → `ThrowIfFailed` fırlatmıyor.
- `ANonZeroExitStillFailsWhateverTheDiagnosticSays` — karar simetrik: düşürülen ayar bir koşumu kurtarmıyor da (çıkış 3, iki yönde de fırlatıyor).
- `TheLowLevelRunnerKeepsOkIndependentOfTheDiagnostic` — `Ok` yalnız çıkış koduna bağlı; `DroppedAnOption` ondan bağımsız dolabiliyor.

### Kararın bugünkü sınırı — iki borç

**1. `-loglevel error` altında tanı hiç görünmüyor.** Ölçüldü:

```
ffmpeg -hide_banner -loglevel error … -c:v libx265 -x265-params zzznotreal=1 …
→ çıkış kodu 0, stderr tamamen boş
ffmpeg -hide_banner -loglevel error … -c:v libx264 -x264-params zzznotreal=1 …
→ çıkış kodu 0, stderr tamamen boş
ffmpeg -hide_banner -loglevel error … -c:v libsvtav1 -svtav1-params zzznotreal=1 …
→ çıkış kodu 0, yalnız SVT'nin kendi Svt[info] banner'ı; tanı satırı yok
```

Yani üç kodlayıcının da bu tanısı **uyarı seviyesinde ya da altında** basılıyor.
`SegmentEncoder.cs:176` tam olarak `-loglevel error` kullanıyor. Sonuç: `FfmpegRunner`e
eklenen taşıma bugünkü tek tüketicisi için **atıl** — kod doğru, ama önizleme yolunun
göreceği bir şey yok. `SegmentEncoder.cs` bu sözleşmenin `owns` kümesi dışında,
loglevel'ine dokunulmadı. Borç.

`EncodeRunner` bu sorunu taşımıyor: `FfmpegArguments.cs:364` `-loglevel` vermiyor, yani
varsayılan `info` seviyesinde koşuyor ve tanıyı görüyor. Gerçek ffmpeg koşumuyla pimlendi
(`ARealEncodeThatDropsAnOptionReportsTheDrop`).

**2. Taşıma `EncodeResult`ta duruyor, arayüze çıkmıyor.** Kullanıcıya uyarı göstermek
`src/VidShrink.App` altında bir değişiklik ister; orası `owns` dışında. Veri teslim
noktasına kadar geliyor, gösterilmesi ayrı bir iş. Borç.

## K5 — Mutasyon ızgarası

Yedi mutasyon tek tek uygulandı; her turda `dotnet build -c Release --no-incremental`
koştu ve `dotnet test` `--no-build` **olmadan** çalıştı. Ham çıktı `.calisma/T144/K5-izgara.txt`
altında üretildi; özeti:

| Mutasyon | Kırılan ölçüler | Kırılan / 16 |
|---|---|---|
| **M0** taban, mutasyon yok | — | 0 |
| **M1** sözlükten `Error parsing option` çıkarıldı | `ARealEncodeThatDropsAnOptionReportsTheDrop`, `TheLowLevelRunnerKeepsOkIndependentOfTheDiagnostic`, `ExitZeroWithADroppedOptionDoesNotFailButTheDropIsCarried`, `EveryEncoderMeasuredAtExitZeroReportsItsDroppedKey(SvtAv1)`, `…(X264)`, `ExitZeroWithADroppedOptionIsStillOkButTheDropIsCarried` | 6 |
| **M2** sözlükten `Unknown option:` çıkarıldı | `EveryEncoder…(X265)`, `BothRunnersReadTheSameDictionary`, `ADroppedOptionNeverFailsTheDeliveryPath` | 3 |
| **M3** desen `Unknown`a genişletildi | `TheWordUnknownOnItsOwnIsNotEnough`, `CleanRunsAreNeverReadAsADroppedOption(libx265)`, `…(libx264)` | 3 |
| **M4** `EncodeRunner` tanılı satırı kaydetmiyor | `BothRunnersReadTheSameDictionary`, `ADroppedOptionNeverFailsTheDeliveryPath`, `ARealEncode…`, `ExitZeroWithADroppedOption…` | 4 |
| **M5** `FfmpegRunner` tanılı satırı taşımıyor | `ExitZeroWithADroppedOptionIsStillOkButTheDropIsCarried`, `BothRunnersReadTheSameDictionary`, `TheLowLevelRunnerKeeps…` | 3 |
| **M6** karar tersine: düşürülen ayar kodlamayı öldürüyor | `ADroppedOptionNeverFailsTheDeliveryPath`, `ARealEncode…`, `ExitZeroWithADroppedOption…` | 3 |
| **M7** tanılı satır tüm akış yerine yalnız kuyruktan aranıyor | `ARealEncode…`, `ExitZeroWithADroppedOption…` | 2 |

**Yedi mutasyonun yedisi de yakalandı; hiçbiri hayatta kalmadı.**

Sözleşme "her mutasyon yalnız kendi ölçüsünü kırmalı" diyor; ızgara bunu harfiyen
karşılamıyor ve karşılamamalı da: tek bir desen (`Error parsing option`) üç kodlayıcının
metninde birden geçtiği için M1 altı ölçüyü birden düşürüyor. Sağlanan ve sağlanması
gereken özellik şu: **yedi kümenin yedisi de birbirinden farklı**, yani hiçbir iki mutasyon
ölçülerden ayırt edilemez değil.

İki satır ayrıca dikkate değer:

- **M3 tek başına sözlüğün genişlemesini yakalayan mutasyon.** Deseni `Unknown option:`ten
  `Unknown`a açmak *hiçbir* tespit ölçüsünü kırmıyor — düşürülen ayarlar hâlâ bulunuyor.
  Yalnız K3'ün yanlış pozitif ölçüleri kırılıyor, çünkü `muxing overhead: unknown` her
  başarılı koşumun sonunda duruyor. Genişleme ancak yanlış pozitif ölçüsüyle görülüyor.
- **M7 tanının kuyruğa girmediğini pimliyor.** Yalnız `EncodeRunner`in iki ölçüsü kırılıyor,
  ikisi de uzun (35 satırlık) gerçek metni kullananlar; kısa metinli ölçüler etkilenmiyor.

Hiçbir ölçü sabiti sabitle karşılaştırmıyor: `DroppedOptionPatterns` dizisi test
dosyalarında hiç geçmiyor (`grep -n "DroppedOptionPatterns" tests/…` boş döndü). Ölçüler
verilen stderr metninin ürettiği kararı pimliyor.

Mutasyon koşumu bitince çalışma ağacı geri alındı; `git status --short` boş, taban 16/16
yeşil.
