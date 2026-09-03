# Sessiz düşürme: yoklama "destekleniyor" diyor, ffmpeg seçeneği düşürüyor

Sözleşme T147. Ölçüm makinesi: Windows 11 Pro 10.0.26100, ffmpeg 9.0-full_build-www.gyan.dev
(libavcodec 63.1.100), x265 4.3+2-5ab552e. Dal tabanı `b4161d7`, rebase sonrası uç `cea77e3`
üstünde.

## Durum

T0'ın "iki durdurucu" kararıyla K2 ve K4'ün önündeki engel kalktı. Bu belge artık kapanmış
bir ölçüm raporu: K1-K6 hepsi aşağıda, ham çıktısıyla.

## K1 — Kusur önce ölçüldü

### Bugünkü desen listesi, kendim saydım

Rebase öncesi dal tabanındaki `EncoderCapabilities.cs`'de tüm tanı kontrollerinin ham hâli:

```
318:           && !diagnostic.Contains("Incompatible pixel format", StringComparison.OrdinalIgnoreCase)
319:           && !diagnostic.Contains("auto-selecting format", StringComparison.OrdinalIgnoreCase);
344:                   && !diagnostic.Contains("Error parsing option", StringComparison.OrdinalIgnoreCase)
345:                   && !diagnostic.Contains("Option not found", StringComparison.OrdinalIgnoreCase)
346:                   && !diagnostic.Contains("Unrecognized option", StringComparison.OrdinalIgnoreCase);
```

**Beş desen, iki yer, iki ayrı liste** (satır numaraları rebase sonrası kaymış olabilir,
sembolle grep edildi, dosya adı ve yordam adı sabit):

| Yer | Yordam | Desenler |
|---|---|---|
| `PixelFormatAccepted` | HDR10 piksel biçimi yoklaması | `Incompatible pixel format`, `auto-selecting format` |
| `RunOptionProbe` (eski `OptionAccepted`) | seçenek yoklaması | `Error parsing option`, `Option not found`, `Unrecognized option` |

`PixelFormatAccepted` kapsam dışı bırakıldı (sözleşme: "PixelFormatAccepted'in ... iki
deseni kalabilir") — dokunulmadı. K1(a)/K2 yalnız ikinci listeyi hedefliyor.

### K1(a) — Yoklama düşürülen seçeneği kabul ediyor (kırmızı, ölçüm A)

`RunOptionProbe`un kendi argüman şekliyle (`-loglevel info`, `testsrc2=size=256x256`,
`-frames:v 1`) ölçüm:

```
ffmpeg -hide_banner -loglevel info -f lavfi -i "testsrc2=size=256x256:rate=30:duration=0.1" \
       -c:v libx265 -x265-params zzznotreal=1 -frames:v 1 -f null NUL
```

Çıkış kodu **0**. Eski üç desenin bu çıktıdaki eşleşme sayısı — hepsi **0**, `Unknown
option:` (sözlükte o zaman **yoktu**) — **1**:

```
[libx265 @ 000001eec9dac280] Unknown option: zzznotreal.
```

Yani eski `supported` üç olumsuzlamanın üçünden de geçiyor, çıkış kodu 0, sonuç
`ProbeOutcome.Accepted`. Yoklama "destekleniyor" diyor; ffmpeg seçeneği düşürmüş. Motorun
x265 için gerçekten sorduğu dizgi `-x265-params psy-rd=2:psy-rdoq=1:aq-mode=2`
(`FfmpegArguments.cs:528-530`, dışarıdan grep, dosyaya dokunulmadı) — dizginin herhangi bir
anahtarı kurulu x265'te yoksa x265 o anahtar için `Unknown option:` yazıp 0 ile çıkıyor,
yoklama dizginin tamamını destekleniyor sayıyor, motor üretmeye devam ediyor.

Bu ölçüm A `tests/VidShrink.Tests/SessizDusurmeTests.cs:19-34`'te
`X265DroppedKeyAtProbeShape` sabiti olarak duruyor (inherited commit'ten, değiştirilmedi).
Bunu pinleyen test `TheProbeMustNotCallADroppedOptionSupported` — fix öncesi kırmızı, fix
sonrası yeşil.

## K2 — Tek sözlük, EncoderCapabilities çağırıyor

Rebase `FfmpegDiagnostics`i main'den getirdi (`src/VidShrink.Ffmpeg/FfmpegRunner.cs`,
çağrılabilir, düzenlenemez). Yapılan değişiklik, `src/VidShrink.Ffmpeg/EncoderCapabilities.cs`:

```diff
     internal static bool OptionAccepted(int exitCode, string diagnostic)
-        => exitCode == 0
-           && !diagnostic.Contains("Error parsing option", StringComparison.OrdinalIgnoreCase)
-           && !diagnostic.Contains("Option not found", StringComparison.OrdinalIgnoreCase)
-           && !diagnostic.Contains("Unrecognized option", StringComparison.OrdinalIgnoreCase);
+        => exitCode == 0 && !FfmpegDiagnostics.ReportsADroppedOption(diagnostic);
```

Çağrı yeri tek: `RunOptionProbe` içinde `OptionAccepted(process.ExitCode, stderr.Result)`.
`FfmpegDiagnostics` düzenlenmedi — yalnız çağrıldı, sınır aşılmadı.

**`Error parsing option` / `Option not found` / `Unrecognized option` neden düşürüldü,
regresyon yok:** İkisi de yalnız çıkış kodu **sıfırdan farklı** olduğunda görülüyor —
`exitCode == 0` kapısı zaten onları eliyordu, yani ölü koddular. Kanıt iki kaynaktan:

1. T144'ün kendi ölçümü (`docs/olcumler/cikis-kodu-yalan.md:125-161`, o rapor bu tabloyu
   çıkış kodu 8 için zaten göstermiş).
2. Bugün burada tazelenen ölçüm C, libx264 ile bilinmeyen üst-düzey seçenek:

```
$ ffmpeg -hide_banner -loglevel info -f lavfi -i "testsrc2=..." -c:v libx264 -zzznotreal ...
Unrecognized option 'zzznotreal'.
Error splitting the argument list: Option not found
EXIT:8
```

Metin sözlükteki hiçbir deseni (eski üçün ikisi de bu satırlarda var, ama zaten
`exitCode==0` kapısından geçmiyor) taşımasa da kapıyı aşan şey **çıkış kodu**, metin değil.
Bu, `tests/VidShrink.Tests/SessizDusurmeTests.cs:89-92` `NonZeroExitUnrelatedDiagnostic`
sabiti ve `ANonZeroExitIsRejectedRegardlessOfDiagnosticText` testiyle pinlendi.

Ayrıca `tests/VidShrink.Tests/EncoderCapabilitiesTests.cs`'nin mevcut 15 testinin hiçbiri
`OptionAccepted`i ya da literal desen dizgilerini doğrudan çağırmıyor — hepsi
`OptionProbeHook`/`Hdr10ProbeHook`/`EncoderProbeHook` dikişlerinden geçiyor (grep ile
doğrulandı), yani K2 değişikliği bu 15 testi etkilemiyor; nitekim hepsi yeşil kaldı.

## K3 — Yanlış pozitif korpusu, gerçek kaynaktan

Üç temiz kodlayıcı koşumu, bugün bu makinede ölçüldü, `-x265-params`/`-x264-params` yok,
çıkış kodu hepsinde 0:

```
$ ffmpeg -hide_banner -loglevel info -f lavfi -i "testsrc2=..." -c:v libx265 -frames:v 1 -f null NUL
...
[out#0/null @ ...] video:5KiB ... muxing overhead: unknown
EXIT:0

$ ffmpeg -hide_banner -loglevel info -f lavfi -i "testsrc2=..." -c:v libx264 -frames:v 1 -f null NUL
EXIT:0

$ ffmpeg -hide_banner -loglevel info -f lavfi -i "testsrc2=..." -c:v libsvtav1 -frames:v 1 -f null NUL
EXIT:0
```

Üçü de `[out#0/null ...] ... muxing overhead: unknown` satırını taşıyor — önceki rapordaki
riskli tuzak satırı bu, ve yeni sözlük onu düşürülen seçenek saymıyor. Bu üç korpus
`tests/VidShrink.Tests/SessizDusurmeTests.cs:95-144`'te `CleanX265Run`/`CleanX264Run`/
`CleanSvtAv1Run` sabitleri, `ACleanRunIsNeverReadAsADroppedOption` teorisiyle pinlendi
(`CleanRuns` member-data, 3 vaka).

**Dürüstlük notu:** T144'ün raporunda anılan "deprecated pixel format" swscaler uyarısını
bu makinede yeniden üretmeyi iki farklı piksel-biçim dönüştürme bayrağıyla denedim, ikisi de
temiz çıktı verdi (`.calisma/T147/k3_deprecated.txt` — içeriği `CleanX264Run` ile aynı satır
kalıbında, uyarı yok). Bu satırı korpusa **uydurmadım**; T3'ün mevcut üç gerçek koşumu K3'ün
gerektirdiği "gerçek kaynak" şartını zaten karşılıyor.

## K4 — Loglevel kararı, iki ffmpeg koşumu ayrıldı

T0'ın Karar 2'si Açık 2'nin önermesini değiştirdi: `SegmentEncoder` **tek değil iki** ffmpeg
koşturuyor (`SegmentEncoder.cs:259-261`, `Task.WhenAll`):

| # | Koşum | Argüman kaynağı | `-loglevel` | Motorun psy/AQ ayarlarını taşıyor mu |
|---|---|---|---|---|
| 1 | kaynak parça | `BuildSourceClipArguments` (`:172-184`) | **`error`** (sabit) | **Hayır** — `libx264 -preset ultrafast -qp 0`, kayıpsız kopya |
| 2 | kodlanmış parça | `FfmpegArguments.BuildSegment` (Core, off-limits) | **verilmiyor** | **Evet** |

Ölçüm D — kodlanmış parça şekli (`-loglevel` verilmeden, yani ffmpeg'in varsayılan `info`
seviyesinde), bugün ölçüldü:

```
[libx265 @ 000001207e661280] Unknown option: zzznotreal.   <- satır 7, toplam 39 satır
...
EXIT:0
```

**Karar: kod değişikliği gerekmiyor.** Kaynak-parça koşumu güvenli çünkü hiçbir düşürülebilir
`-*-params` seçeneği taşımıyor — pinlendi (`TheSourceClipArgumentsCarryNoDroppableEncoderOptions`,
`Assert.Contains("-loglevel"/"error")`, `Assert.DoesNotContain(a => a.Contains("-params"))`).
Kodlanmış-parça koşumu zaten varsayılan seviyede tanıyı basıyor.

**Kuyruk ölçümü:** `FfmpegRunner.ErrorTailLines` **8** (kaynak: `FfmpegRunner.cs`, doğrudan
okundu — sözleşmenin "15" dediği yanlış, düzeltilen değer budur). Tanı satırı 39 satırın
7'si, yani son 8 satır (32-39) **içinde değil**. Ama `FfmpegDiagnostics.DroppedOptionLines`
`DroppedOptions`ı kuyruktan değil **tam metinden** hesaplıyor, dolayısıyla kuyruk penceresi
bu yolu kesmiyor. Bu, `ADroppedOptionOutsideTheTailIsStillCaught` testiyle iki yönlü
pinlendi: `DroppedOptionLines` satırı buluyor, `FfmpegRunner.Tail` aynı metinde onu
**taşımıyor** — yani kuyruk penceresi teşhis için güvenilemez ama teşhis kuyruğu kullanmıyor
zaten.

**`docs/olcumler/cikis-kodu-yalan.md` düzeltmesi (T0 Karar 2, zorunlu):** o rapor
"`SegmentEncoder.cs:176` tam olarak `-loglevel error` kullanıyor, taşıma atıl" diyordu — bu
cümle iki ayrı ffmpeg koşumunu tek koşum gibi anlatıyor, yanlış. Cümle **silinmedi**, altına
"Düzeltme (T147, sözleşme T0 kararı — Karar 2)" başlığıyla doğrusu eklendi. Bu dosya T147'nin
`owns` kümesinde değil; düzenleme yalnız Karar 2'nin yazılı ve açık zorunluluğu yüzünden
yapıldı.

## K5 — Mutasyon ızgarası

Her mutasyon: `.calisma/T147/*.orig2` yedeğinden geri yükleme, `dotnet build -c Release
--no-incremental` (asla `--no-build`), filtrelenmiş testin ham çıktısı, geri yükleme.

**Mutasyon 1 — `exitCode == 0` kapısı kaldırıldı** (`OptionAccepted`):

```
Başarısız VidShrink.Tests.SessizDusurmeTests.ANonZeroExitIsRejectedRegardlessOfDiagnosticText [FAIL]
   cikis kodu 8 iken metin sozlukteki hicbir deseni tasimasa da sonuc kabul olmamali.
Başarısız! - Başarısız: 1, Başarılı: 29, Toplam: 30
```

Beklenen tek testi öldürdü, başka hiçbirini etkilemedi.

**Mutasyon 2 — negasyon ters çevrildi** (`!FfmpegDiagnostics...` → `FfmpegDiagnostics...`):

```
Başarısız VidShrink.Tests.SessizDusurmeTests.ACleanRunIsNeverReadAsADroppedOption(label: "temiz libx265", ...) [FAIL]
Başarısız VidShrink.Tests.SessizDusurmeTests.ACleanRunIsNeverReadAsADroppedOption(label: "temiz libsvtav1", ...) [FAIL]
Başarısız VidShrink.Tests.SessizDusurmeTests.ACleanRunIsNeverReadAsADroppedOption(label: "temiz libx264", ...) [FAIL]
Başarısız VidShrink.Tests.SessizDusurmeTests.TheProbeMustNotCallADroppedOptionSupported [FAIL]
Başarısız! - Başarısız: 4, Başarılı: 26, Toplam: 30
```

Beklenen 4 testi (3 temiz-koşum vakası + K1(a) kırmızısı) öldürdü.

**Mutasyon 3 — `SegmentEncoder.BuildSourceClipArguments`'ta `"error"` → `"info"`:**

```
Başarısız VidShrink.Tests.SessizDusurmeTests.TheSourceClipArgumentsCarryNoDroppableEncoderOptions [FAIL]
   Assert.Contains() Failure: Item not found in collection
   Collection: ["-hide_banner", "-loglevel", "info", "-nostdin", "-y", ...]
   Not found:  "error"
Başarısız! - Başarısız: 1, Başarılı: 29, Toplam: 30
```

Beklenen tek testi öldürdü.

Mutasyon sonrası her seferinde `.orig2` yedeğinden geri yükleme + `--no-incremental` rebuild
+ tam yeşil koşum ile temizlik doğrulandı. Hiçbiri sabit-sabit karşılaştırması değil — her
biri gerçek stderr/exit-code girdisinin ürettiği kararı kırıyor.

## K6 — Test sayıları, kendi saydım

```
$ dotnet test tests/VidShrink.Tests -c Release --no-build --filter "FullyQualifiedName~EncoderCapabilitiesTests" --list-tests | grep -c "^    "
15
$ dotnet test tests/VidShrink.Tests -c Release --no-build --filter "FullyQualifiedName~SegmentEncoderTests" --list-tests | grep -c "^    "
8
$ dotnet test tests/VidShrink.Tests -c Release --no-build --filter "FullyQualifiedName~SessizDusurmeTests" --list-tests | grep -c "^    "
7
```

Hiçbir kolon sıfır eşleşmedi. Verify filtresiyle (`EncoderCapabilitiesTests|SegmentEncoderTests|SessizDusurmeTests`)
toplu koşum, `dotnet build -c Release --no-incremental` sonrası:

```
Başarılı!  - Başarısız: 0, Başarılı: 30, Atlanan: 0, Toplam: 30, Süre: 13 s
```

15+8+7=30, toplamla eşleşiyor. CI run id: bu ölçüm dalın push'undan önce yerelde alındı;
push sonrası CI koşum kimliği bu bölüme eklenecek (aşağıdaki "Kalan" notuna bakınız).

## Kalan

- CI'da bu dalın koşum kimliği henüz yok — push sonrası eklenmeli, bu belgeye elle
  işlenmedi çünkü push bu ölçüm anında henüz yapılmamıştı.
- `docs/olcumler/cikis-kodu-yalan.md` düzeltmesi bu sözleşmenin `owns` kümesi dışında;
  T0'ın Karar 2 zorunluluğuyla yapıldı, ayrıca not düşülüyor.
