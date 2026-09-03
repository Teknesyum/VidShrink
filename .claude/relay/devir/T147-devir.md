sozlesme: T147
dal: T147-sessiz-dusurme-sondada
son-commit: 9dca031

# T147 devir

## Nerede kaldım

- **K1(a)** — GEÇTİ. Yoklamanın düşürülen seçeneği kabul ettiği ölçüldü, kırmızı yazıldı, K2 ile yeşile döndü.
- **K1(b)** — GEÇTİ, ama sözleşmenin öncülü çürütülerek. Ölçüm aşağıda.
- **K2** — GEÇTİ. `OptionAccepted` artık `FfmpegDiagnostics`i çağırıyor. Sözlüğe desen eklenmedi, sınır aşılmadı.
- **K3** — GEÇTİ. Dört korpuslu yanlış pozitif ölçüsü.
- **K4** — KOD GEÇTİ, ÖLÇÜSÜ YARIM. Karar verildi ve yazıldı, `PreviewClip.DroppedOptions` + `DroppedAcross` commit'li. Ama `BuildClip` dikişi commit'e **girmedi**.
- **K5** — YARIM. Izgara iki kez koştu, ikisi de tam sonuç vermedi.
- **K6** — HİÇ BAŞLANMADI. Kol başına test sayısı sayılmadı, CI koşum kimliği alınmadı.

## ÇALIŞMA AĞACI DERLENMİYOR — ilk yapılacak iş

`tests/VidShrink.Tests/SessizDusurmeTests.cs` commit'li ama `SegmentEncoder.BuildClip`
çağırıyor; o yordam **kaynakta yok**. Derleme hatası:

```
SessizDusurmeTests.cs(205,35): error CS0117: SegmentEncoder bir BuildClip tanimi icermiyor
SessizDusurmeTests.cs(224,35): error CS0117: SegmentEncoder bir BuildClip tanimi icermiyor
```

Sebep: `BuildClip` dikişini açtım, commit etmeden K5 ızgarasını koşturdum, ızgara betiği
her turda `git checkout -- src/VidShrink.App/Playback/SegmentEncoder.cs` yapıyor ve dikişi
geri aldı. Test dosyası checkout listesinde olmadığı için hayatta kaldı, kaynak kalmadı.

Dikiş uygulandıktan sonra `dotnet build -c Release --no-incremental` 0 hata 0 uyarı verdi
ve `--filter "SessizDusurmeTests"` **13/13 yeşil** oldu. Ölçüldü, tahmin değil.

Dikişi geri getiren betik dalda duruyor, gitignore dışında:

```
.claude/relay/devir/T147-buildclip-dikisi.py
```

Depo kökünden `python .claude/relay/devir/T147-buildclip-dikisi.py` ile uygulanır.
İki `assert` içeriyor; çapa bulunamazsa sessizce yanlış yere yazmaz, durur.

## Ölçtüğüm sayılar

Makine: Windows 11 Pro 10.0.26100, ffmpeg 9.0-full_build-www.gyan.dev (libavcodec
63.1.100), x265 4.3+2-5ab552e, SVT-AV1 v4.2.0-68-gc1e79b04f. Dal tabanı `359f37c`.

### K1(a) — sondanın kendi argüman şekli, x265, tanınmayan anahtar

```
ffmpeg -hide_banner -loglevel info -f lavfi -i "testsrc2=size=256x256:rate=30:duration=0.1" \
       -c:v libx265 -x265-params zzznotreal=1 -frames:v 1 -f null NUL
CIKIS KODU: 0
Error parsing option   : 0
Option not found       : 0
Unrecognized option    : 0
Unknown option:        : 1
[libx265 @ 000001eec9dac280] Unknown option: zzznotreal.
```

### K1(a) kırmızısının ham metni (K2 öncesi)

```
  Basarisiz VidShrink.Tests.SessizDusurmeTests.TheProbeMustNotCallADroppedOptionSupported [39 ms]
  Hata Iletisi:
   ffmpeg 'Unknown option: zzznotreal.' yazip 0 ile dondu; yoklama bunu destekleniyor saymamali.
Basarisiz! - Basarisiz: 1, Basarili: 15, Atlanan: 0, Toplam: 16, Sure: 4 s
```

### Bugünkü desen listesi — `EncoderCapabilities.cs`, dal tabanındaki hâli

```
318:           && !diagnostic.Contains("Incompatible pixel format", StringComparison.OrdinalIgnoreCase)
319:           && !diagnostic.Contains("auto-selecting format", StringComparison.OrdinalIgnoreCase);
344:                   && !diagnostic.Contains("Error parsing option", StringComparison.OrdinalIgnoreCase)
345:                   && !diagnostic.Contains("Option not found", StringComparison.OrdinalIgnoreCase)
346:                   && !diagnostic.Contains("Unrecognized option", StringComparison.OrdinalIgnoreCase);
```

Beş desen, iki yer, iki liste **ayrık**. Kendim saydım.

### Sözlükten düşen iki desen hangi çıkış koduyla geliyor

```
libx265 -vsync 0        cikis=8  Option not found Unrecognized option
libx265 -zzznotreal 1   cikis=8  Option not found Unrecognized option
```

İkisi de sıfırdan farklı, yani `exitCode == 0` kapısının arkasında erişilemez.

### K1(b) — önizlemenin iki koşumu

```
B: -loglevel error (kaynak parca sekli), x265 taninmayan anahtar
   CIKIS KODU: 0   stderr 0 bayt

C: Build sekli (-loglevel YOK), x265 taninmayan anahtar
   CIKIS KODU: 0   stderr 2351 bayt / 38 satir
   satir 7: [libx265 @ 0000020e481d3440] Unknown option: zzznotreal.
```

App katmanının tamamında `-loglevel` tek yerde: `SegmentEncoder.cs:176`
(`BuildSourceClipArguments`). `FfmpegArguments.Build` içinde loglevel sayımı **0**.

### K4 — seviyeyi yükseltmenin bedeli (aynı komut, iki seviye)

```
-loglevel error   cikis=0   0 satir      0 bayt
-loglevel info    cikis=0  39 satir   2747 bayt
```

`FfmpegRunner.ErrorTailLines` = **8**.

### K3 — temiz yoklama korpusu (hepsi çıkış kodu 0)

```
libx265 -x265-params "psy-rd=2:psy-rdoq=1:aq-mode=2"   cikis=0  37 satir
  x265 [warning]: Source height < 720p; disabling lookahead-slices
  [out#0/null @ ...] ... muxing overhead: unknown

libsvtav1 -svtav1-params "enable-variance-boost=1:variance-boost-strength=2"  cikis=0
  [out#0/null @ ...] ... muxing overhead: unknown

libx264 -pix_fmt yuvj420p -x264-params "psy-rd=1.0:aq-mode=2"  cikis=0
  [swscaler @ 0000017c24c5cb40] deprecated pixel format used, make sure you did set range correctly
```

### K5 — ilk ızgara koşumu (KULLANILABİLİR KISIM)

Bu koşum `--filter "SessizDusurmeTests|EncoderCapabilitiesTests|SegmentEncoderTests"` ile
koştu, 10 dakikada zaman aşımına düştü, M5/M6'ya hiç gelmedi. M0-M4 geçerli:

```
=================== M0 TABAN ===================
Basarili!  - Basarisiz: 0, Basarili: 34, Atlanan: 0, Toplam: 34

=================== M1 yoklama sozlugu hic okumuyor, yalniz cikis koduna bakiyor ===================
  Basarisiz SessizDusurmeTests.TheProbeMustNotCallADroppedOptionSupported
Basarisiz! - Basarisiz: 1, Basarili: 33, Toplam: 34

=================== M2 sozluk yerine genis desen: Unknown ===================
  Basarisiz SessizDusurmeTests.ACleanProbeIsNeverRejected(label: "libsvtav1, temiz variance-boost dizgisi")
  Basarisiz SessizDusurmeTests.ACleanProbeIsNeverRejected(label: "libx264, deprecated pixel format uyarisi")
  Basarisiz SessizDusurmeTests.ACleanProbeIsNeverRejected(label: "libx265, motorun gercekten sordugu psy dizgisi")
  Basarisiz SessizDusurmeTests.TheWordUnknownOnItsOwnDoesNotRejectAProbe
Basarisiz! - Basarisiz: 4, Basarili: 30, Toplam: 34

=================== M3 cikis kodu kapisi kaldirildi ===================
  Basarisiz SessizDusurmeTests.ANonZeroExitIsRejectedWhateverTheTextSays
Basarisiz! - Basarisiz: 1, Basarili: 33, Toplam: 34

=================== M4 onizleme tasimayi okumuyor ===================
Basarili!  - Basarisiz: 0, Basarili: 34, Atlanan: 0, Toplam: 34
```

**M4 HAYATTA KALDI.** `DroppedOptions = DroppedAcross(runs)` bağını `Array.Empty<string>()`
ile kesmek hiçbir ölçüyü kırmadı — `DroppedAcross`ı tek başına ölçmek bağın kurulduğunu
pimlemiyor. Bu yüzden `BuildClip` dikişini açıp iki ölçü yazdım
(`TheClipIsWiredToTheDropNotJustAbleToComputeIt`, `ACleanRunLeavesTheClipEmpty`).
**Bu ölçülerin M4'ü gerçekten öldürdüğü doğrulanmadı** — ızgara o hâliyle bir daha koşmadı.

### K5 — ikinci ızgara koşumu (KULLANILAMAZ)

`BuildClip` commit'li olmadığı için M1'den sonrası hep `DERLEME HATASI` verdi. Tek çıktısı
yukarıdaki CS0117 hatası. Bu koşumdan hiçbir sonuç çıkarılmasın.

## Ölçtüklerim ile varsaydıklarım

**Gerçek koşum var:** yukarıdaki bütün cikis/satir/bayt sayıları, desen eşleşme tablosu,
K1(a) kırmızısının metni, grep çıktıları, ilk ızgaranın M0-M4 satırları, `BuildClip`
uygulandıktan sonraki 13/13 yeşil.

**Varsayım, doğrulanmadı:**
- `BuildClip` ölçülerinin M4'ü öldürdüğü. Mantıken öldürmeli (mutasyon hedefi artık
  `BuildClip` gövdesinde) ama **koşturulmadı**.
- M5 (birleşim yalnız ilk koşumu okuyor) ve M6 (kaynak parça seviyesi info'ya yükseltildi)
  hiç ölçülmedi. M6'yı `TheSourceClipRunStaysAtLogLevelError`, M5'i `TheUnionReadsBothRuns`
  öldürmeli — ikisi de tahmin.
- `Past duration ... too large` satırı bu makinede **üretilemedi**; K3 korpusuna
  sözleşmeden alınmış hâliyle konuldu ve raporda öyle işaretlendi.

## Güvenilmeyecek şeyler

- **Çalışma ağacı derlenmiyor.** Dikişi uygulamadan hiçbir ölçü koşmaz.
- `docs/olcumler/sessiz-dusurme-sondada.md` K4'te bitiyor: **K5 ve K6 bölümü yok**, özet
  tablosu yok. K5 sayıları yalnız bu devir dosyasında.
- CI koşum kimliği alınmadı. Son itilen commit `1ee0afc` içindi (`33679940911`,
  `in_progress` görüldü, sonucu okunmadı). K2/K3/K4 commit'leri **hiç itilmedi**.
- `.calisma/T147/` gitignore'da; oradaki hiçbir şeye atıfta bulunma. Gereken iki betik
  `.claude/relay/devir/` altına kopyalandı.

## Dokunduğum dosyalar

`owns` içinde:
- `src/VidShrink.Ffmpeg/EncoderCapabilities.cs`
- `src/VidShrink.App/Playback/SegmentEncoder.cs`
- `tests/VidShrink.Tests/SessizDusurmeTests.cs`
- `docs/olcumler/sessiz-dusurme-sondada.md`

**`owns` dışı, T0'ın açık talimatıyla:**
- `docs/olcumler/cikis-kodu-yalan.md` — T144'ün raporu. Geri çekilen "SegmentEncoder
  -loglevel error koşuyor, taşıma atıl" iddiası silinmedi; yanlış olduğu yazıldı ve
  doğrusu altına kondu. T0 bunu zorunlu tuttu.

`tests/VidShrink.Tests/EncoderCapabilitiesTests.cs` `owns` içinde ama dokunulmadı.

## Sıradaki adım

Ben olsam sırayla: `python .claude/relay/devir/T147-buildclip-dikisi.py` ile dikişi uygula,
`dotnet build -c Release --no-incremental` ile 0 hata doğrula ve **hemen ayrı bir commit'le
kaydet** — bu turun bütün zararı o dikişin commit'siz kalmasından çıktı. Sonra
`.claude/relay/devir/T147-mutasyon.sh`i koştur (filtresi `SessizDusurmeTests|EncoderCapabilitiesTests`
olarak ayarlı; `SegmentEncoderTests` gerçek ffmpeg koşturuyor ve ızgarayı 10 dakika
sınırının üstüne çıkarıyordu), M0-M6'yı baştan al, M4'ün artık öldüğünü ve M5/M6'nın
kırıldığını gör. Ardından K5 bölümünü rapora ham ızgarayla yaz. K6 için üç kolu
`--list-tests` ile say — `SessizDusurmeTests` sınıf adı kol adıyla birebir aynı, T144'teki
alt-dize tuzağı burada yok ama yine de say. En son dalı it, CI kimliğini ve
`completed success` satırını rapora geçir.

**Uyarı:** mutasyon betiğini koşturmadan önce çalışma ağacında commit'siz değişiklik
bırakma — `git checkout --` onları geri alır. Bu turda tam bu oldu.
