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
