# Sessiz düşürme: yoklama "destekleniyor" diyor, ffmpeg seçeneği düşürüyor

Sözleşme T147. Ölçüm makinesi: Windows 11 Pro 10.0.26100, ffmpeg 9.0-full_build-www.gyan.dev
(libavcodec 63.1.100), x265 4.3+2-5ab552e. Dal tabanı `b4161d7`.

## İki durdurucu ve T0'ın kararı

Bu sözleşme bir kez durdu. İki soru T0'a yazıldı, ikisi de 3 Eylül 2026'da karara bağlandı
ve **ikisi de engel çıkmadı.**

### Durdurucu 1 — taban eskiydi, engel yoktu

K2 "`EncoderCapabilities` kendi kopyasını tutmaz, **onu çağırır**" diyor. İlk ölçümde
çağrılacak şey yoktu:

```
git log --oneline -1                        b4161d7 rele: T147 dagitildi
grep -rl "FfmpegDiagnostics" src/ tests/    (hiçbir dosyada yok)
wc -l src/VidShrink.Ffmpeg/FfmpegRunner.cs  90
```

T0'ın cevabı: **taban eski.** T144 mühürlenmişti (`contracts/done/T144.md`); dalın tabanı
`b4161d7` ise T144 birleşmeden önceki `main`di. Güncel `origin/main`e (`359f37c`) rebase
edildi ve `FfmpegDiagnostics` göründü:

```
src/VidShrink.Ffmpeg/FfmpegRunner.cs:18:public static class FfmpegDiagnostics
```

Bu, bu depoda üçüncü kez aynı sınıf kusur: **araç güncel olmayan ağacı okuyup gerçek
durumu yanlış bildiriyor.** Ölçümün kendisi doğruydu, okuduğu ağaç eskiydi.

### Durdurucu 2 — Açık 2'nin öncülü yanlıştı, amacı doğru

Sözleşmenin öncül cümlesi ("`SegmentEncoder` `-loglevel error` koşuyor, taşıma atıl")
benim T144 raporumdan geliyordu ve ölçüm onu çürüttü. T0'ın kararı: **öncül değişir, açık
kapsamda kalır** — Açık 2'nin amacı sessiz düşürmeydi, ölçülen şey tam o amaca giriyor,
yalnız yeri farklı. Yeni öncül K1(b)'de.

Geri çekilen cümle T144 raporundan **gerçekten çıkarıldı**: silinmedi, yanlış olduğu
yazıldı ve doğrusu altına kondu (`docs/olcumler/cikis-kodu-yalan.md:223` ve `:388`).

## K1 — Kusur önce ölçüldü

### Bugünkü desen listesi, kendim saydım

Sözleşme "üç desen" diyor. `EncoderCapabilities.cs` içindeki tüm tanı kontrollerinin ham
çıktısı, dal tabanındaki dosya üzerinde:

```
318:           && !diagnostic.Contains("Incompatible pixel format", StringComparison.OrdinalIgnoreCase)
319:           && !diagnostic.Contains("auto-selecting format", StringComparison.OrdinalIgnoreCase);
344:                   && !diagnostic.Contains("Error parsing option", StringComparison.OrdinalIgnoreCase)
345:                   && !diagnostic.Contains("Option not found", StringComparison.OrdinalIgnoreCase)
346:                   && !diagnostic.Contains("Unrecognized option", StringComparison.OrdinalIgnoreCase);
```

**Beş desen, iki yer, iki ayrı liste:**

| Yer | Yordam | Desenler |
|---|---|---|
| `:316-319` | `PixelFormatAccepted` | `Incompatible pixel format`, `auto-selecting format` |
| `:343-346` | `RunOptionProbe` (satır içi) | `Error parsing option`, `Option not found`, `Unrecognized option` |

Sözleşmenin "üç desen"i `RunOptionProbe`u kastediyor ve **doğru**. Sözleşmenin sorduğu
diğer soruya cevap: iki liste **ayrık**, tek bir ortak desen yok. `PixelFormatAccepted`
piksel biçimine, `RunOptionProbe` seçenek kabulüne bakıyor — farklı sorular, ama ikisi de
"çıkış kodu 0 iken metne bak" kalıbının ayrı ayrı yazılmış kopyaları.

### K1(a) — Yoklama düşürülen seçeneği kabul ediyor

`RunOptionProbe`un **kendi argüman şekliyle** (`-loglevel info`, `testsrc2=size=256x256`,
`-frames:v 1`) ölçüm A:

```
ffmpeg -hide_banner -loglevel info -f lavfi -i "testsrc2=size=256x256:rate=30:duration=0.1" \
       -c:v libx265 -x265-params zzznotreal=1 -frames:v 1 -f null NUL
```

Çıkış kodu **0**. Mevcut üç desenin her birinin bu çıktıdaki eşleşme sayısı:

| Desen | Eşleşme |
|---|---|
| `Error parsing option` | **0** |
| `Option not found` | **0** |
| `Unrecognized option` | **0** |
| `Unknown option:` (sözlükte **yok**) | **1** |

Basılan satır:

```
[libx265 @ 000001eec9dac280] Unknown option: zzznotreal.
```

Yani `supported` üç olumsuzlamanın üçünden de geçiyor ve çıkış kodu 0 olduğu için sonuç
**`ProbeOutcome.Accepted`**. Yoklama "x265 bu seçeneği destekliyor" diyor; ffmpeg seçeneği
düşürmüş.

Motorun x265 için gerçekten sorduğu dizgi `-x265-params psy-rd=2:psy-rdoq=1:aq-mode=2`
(`FfmpegArguments.cs:528-530`). Dizginin herhangi bir anahtarı kurulu x265 yapısında yoksa
x265 o anahtar için `Unknown option:` yazıp 0 ile çıkıyor, yoklama dizginin tamamını
"destekleniyor" sayıyor ve motor dizgiyi üretmeye devam ediyor.

Kırmızının ham metni:

```
  Başarısız VidShrink.Tests.SessizDusurmeTests.TheProbeMustNotCallADroppedOptionSupported [39 ms]
  Hata İletisi:
   ffmpeg 'Unknown option: zzznotreal.' yazip 0 ile dondu; yoklama bunu destekleniyor saymamali.

Başarısız! - Başarısız: 1, Başarılı: 15, Atlanan: 0, Toplam: 16
```

### K1(b) — Önizleme: sözleşmenin varsayımı tutmuyor

`SegmentEncoder` **iki** ffmpeg koşuyor (`SegmentEncoder.cs:259-261`):

| # | Koşum | Argüman kaynağı | `-loglevel` | Motorun psy ayarlarını taşıyor mu |
|---|---|---|---|---|
| 1 | kaynak parça | `BuildSourceClipArguments` (`:172-184`) | **`error`** | **Hayır** — `libx264 -preset ultrafast -qp 0`, kayıpsız çıkarma |
| 2 | kodlanmış parça | `FfmpegArguments.BuildSegment` → `Build` | **verilmiyor** | **Evet** |

App katmanının tamamında `-loglevel` **tek** yerde geçiyor: `SegmentEncoder.cs:176`.
`Build` içinde hiç geçmiyor, yani ikinci koşum ffmpeg'in varsayılan `info` seviyesinde.

Ölçüm B — kaynak parça şekli (`-loglevel error`):

```
ffmpeg -hide_banner -loglevel error -nostdin -y ... -c:v libx265 -x265-params zzznotreal=1 ...
çıkış kodu 0, stderr 0 bayt
```

Ölçüm C — kodlanmış parça şekli (`-loglevel` yok):

```
ffmpeg -hide_banner -y -hwaccel auto ... -c:v libx265 -x265-params zzznotreal=1 ...
çıkış kodu 0, stderr 2351 bayt / 38 satır
satır 7: [libx265 @ 0000020e481d3440] Unknown option: zzznotreal.
```

**Sonuç: Açık 2 tarif edildiği şekliyle bir kusur değil.** Tanı satırının görünmediği
koşum, motorun ayarlarını hiç taşımayan kayıpsız kaynak çıkarması; düşecek bir ayar yok.
Ayarları taşıyan koşum tanıyı **basıyor**. T144'te eklenen taşıma bu koşum için atıl
değil.

Kuyruk ölçümü (K4'ün istediği): `FfmpegRunner.ErrorTailLines` **8**. Tanı satırı 38
satırın 7.'si, yani son 8 satır (31-38) **içinde değil**. Ama T144'ün `Decide` yordamı
`DroppedOptions`ı kuyruktan değil **tam metinden** hesaplıyor; dolayısıyla kuyruk penceresi
bu yolu kesmiyor. `StandardError` alanında sebep görünmez, `DroppedOptions`ta görünür.

Bu bulgu bir kusuru **geri çekiyor**, eklemiyor: T144 raporundaki "SegmentEncoder
`-loglevel error` koşuyor, taşıma atıl" cümlesi iki koşumu ayırmadığı için yanlış yere
işaret ediyordu. T0'a bildirildi.

## Bundan sonrası

K2, K3, K4, K5, K6 T0'ın iki kararına bağlı:

1. `FfmpegDiagnostics`e `Unknown option:` deseni nasıl girecek — T144'e tur mu, dosya bu
   sözleşmeye mi veriliyor?
2. Açık 2 kapsamda kalıyor mu — ölçü onun bir kusur olmadığını söylüyor.

Karar gelene kadar `OptionAccepted` dikişi ve K1(a) kırmızısı dalda duruyor; ikisi de
hangi karar çıkarsa çıksın geçerli kalır.

## K2 — Sözlük tek yerde

Çağrı yeri, ham çıktı:

```
src/VidShrink.Ffmpeg/EncoderCapabilities.cs:359:        => exitCode == 0 && FfmpegDiagnostics.DroppedOptionLines(diagnostic).Count == 0;
```

`RunOptionProbe`un satır içi üç desenlik kopyası kaldırıldı; karar artık tek sözlükten
okunuyor. Yoklama yolu ile teslim yolu aynı metni aynı desenlerle görüyor.

### Hangi desen nereden geliyor

| Desen | Kaynağı | Ölçüm |
|---|---|---|
| `Error parsing option` | `FfmpegDiagnostics` (T144) | libsvtav1 ve libx264, çıkış kodu 0 |
| `Unknown option:` | `FfmpegDiagnostics` (T144) | libx265, çıkış kodu 0 — T147'de sondanın kendi argüman şekliyle yeniden ölçüldü, ölçüm A |

### Sınır aşılmadı

**`FfmpegDiagnostics`e hiçbir desen eklenmedi.** Sözleşme "`Unknown option:` oraya girmeli
ve o T144'ün dosyası" diyordu; mühürlenmiş hâli okununca desen **zaten içindeydi**:

```
    public static readonly IReadOnlyList<string> DroppedOptionPatterns = new[]
    {
        "Error parsing option",
        "Unknown option:"
    };
```

Yani T144 bu deseni kendi turunda eklemişti. `src/VidShrink.Ffmpeg/FfmpegRunner.cs`
bu sözleşmede **okundu, değiştirilmedi**; `git diff` o dosyada boş.

### Düşen iki desen ve gerekçesi

Eski satır içi liste üç desen taşıyordu; sözlükte ikisi yok: `Option not found` ve
`Unrecognized option`. Bunları kaybetmek bir gerileme değil, çünkü ikisi de
`exitCode == 0` kapısının arkasında zaten erişilemez. Sondanın kendi argüman şekliyle
ölçüldü:

```
libx265 -vsync 0        çıkış kodu 8   Option not found, Unrecognized option
libx265 -zzznotreal 1   çıkış kodu 8   Option not found, Unrecognized option
```

İkisi de **sıfırdan farklı** kodla geliyor; `OptionAccepted` çıkış kodu 0 değilse zaten
`false` dönüyor. Bu iki desenin bu kapıda hiçbir koşumda etkisi olamaz.

### `PixelFormatAccepted`e dokunulmadı

Sözleşme piksel biçimine özgü iki desenin kalabileceğini söylüyor. Kaldı: o yordam
HDR10 piksel biçimi yoklamasını değerlendiriyor ve argümanlarında kodlayıcı parametre
dizgisi (`-*-params`) yok — sessiz düşürmenin ölçülen yolu orada geçmiyor. Farklı soru,
ayrı liste.
