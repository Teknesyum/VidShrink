# Sessiz düşürme: yoklama "destekleniyor" diyor, ffmpeg seçeneği düşürüyor

Sözleşme T147. Ölçüm makinesi: Windows 11 Pro 10.0.26100, ffmpeg 9.0-full_build-www.gyan.dev
(libavcodec 63.1.100), x265 4.3+2-5ab552e. Dal tabanı `b4161d7`.

## Teslimden önce: iki durdurucu

Bu belge **tamamlanmış bir sözleşmenin raporu değil.** K1'in ölçülebilir yarısı yapıldı;
K2 ve K4 T0'ın kararını bekliyor. İkisi de sözleşmenin kendi sınır maddelerinin işaret
ettiği durum.

### Durdurucu 1 — FfmpegDiagnostics main'de yok

K2 "`EncoderCapabilities` kendi kopyasını tutmaz, **onu çağırır**" diyor. Çağrılacak şey
bu dalın tabanında mevcut değil:

```
git log --oneline -1                        b4161d7 rele: T147 dagitildi
grep -rl "FfmpegDiagnostics" src/ tests/    (hiçbir dosyada yok)
wc -l src/VidShrink.Ffmpeg/FfmpegRunner.cs  90   (T144 öncesi hâli)
```

T144 (`T144-cikis-kodu-yalan`, uç `9a53acf`) bağımsız denetimde; `main`e girmedi.
Sözleşme bunu öngörmüş — "T144 mühürlenince FfmpegDiagnostics main'e girer" — ama
mühürlenmedi, dolayısıyla K2 bugün yazılamaz.

Sözleşmenin talimatı burada net: "`Unknown option:` desenini oraya eklemeden K1(a)
kapanmıyorsa **dur ve T0'a yaz.** Çözüm ya T144'e tur açmak ya bu sözleşmeye o dosyayı
vermektir; sen karar verme." Karar verilmedi, T0'a yazıldı.

### Durdurucu 2 — Açık 2'nin varsayımı ölçüde tutmuyor

Sözleşme "`SegmentEncoder` `-loglevel error` ile koşuyor, tanı satırı o seviyede hiç
basılmıyor, T144'te eklenen taşıma bugün **atıl**" diyor. Bu cümlenin kaynağı benim T144
raporum ve yeterince ayrıntılı yazılmamış. Ölçüm aşağıda: önizleme **iki** ffmpeg koşuyor
ve `-loglevel error` yalnız motorun ayarlarını hiç taşımayan koşumda.

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
