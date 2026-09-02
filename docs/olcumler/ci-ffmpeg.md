# T115 — CI ffmpeg görmüyor: ölçüm

**Tarih:** 02.09.2026 · **Sözleşme:** `.claude/relay/contracts/T115.md`

CI koşucusunda ffmpeg yoktu; `[FfmpegFact]` işaretli ölçüler sessizce
atlanıyordu. Bu sözleşme onu görünür kıldı: `.github/workflows/ci.yml` artık
ffmpeg'i pinlenmiş bir sürümle kurup PATH'e ekliyor.

Ölçülen dal: `T115-ci-ffmpeg`. Nihai (rapordaki tüm "sonrası" sayılarının
kaynağı) koşum: **`33584487781`**, ölçülen commit **`5694bc6d`** (main'e
T109/T110 birleştikten sonra dal `origin/main` üstüne rebase edilmiş hali).
Taban (ffmpeg'siz) koşum: **`33582206982`**, ölçülen commit **`68cb3c93`**.

## K1 — sürüm

| Alan | Değer |
|---|---|
| Kaynak | GitHub Release varlığı, `GyanD/codexffmpeg` etiket `9.0`, dosya `ffmpeg-9.0-full_build.zip` |
| İndirme URL'i | `https://github.com/GyanD/codexffmpeg/releases/download/9.0/ffmpeg-9.0-full_build.zip` |
| sha256 (pinlendi) | `F42F0C4B04EAE3AC918707FF66E3E0FF0CEE527BFA6D322624D4BC1160D5055E` |
| CI'da `ffmpeg -version` (koşum `33583024971`) | `ffmpeg version 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers` |
| Yerelde `ffmpeg -version` | `ffmpeg version 9.0-full_build-www.gyan.dev` |
| Fark | Yok — sürüm dizisi birebir aynı. |

Neden bu kaynak: `README.md:273` — gerçek kullanıcı kurulumu Windows'ta WinGet
üzerinden `Gyan.FFmpeg` çekiyor, yani gyan.dev derlemesi. `GyanD/codexffmpeg`
gyan.dev'in aynı derlemelerini **sürüm etiketli, değişmez GitHub Release
varlığı** olarak yayınlıyor — `windows-latest` imajının kendi ffmpeg'ine ya da
gyan.dev'in "latest" bağlantısına (hareketli, sessizce kayar) güvenmek yerine
bu kullanıldı. sha256 indirilen dosyadan hesaplanıp iş akışına gömüldü; kurulum
adımı hash uyuşmazsa düşer (K7).

## K2 — libvmaf

| Alan | Değer |
|---|---|
| `ffmpeg -filters \| grep libvmaf` (yerel doğrulama) | `.. libvmaf  VV->V  Calculate the VMAF between two video streams.` |
| CI çıktısı (koşum `33583024971`, aynı adım her koşumda tekrarlanıyor) | `libvmaf filtresi var.` |
| VMAF model dosyası gerekiyor mu | Hayır — kod yerleşik modeli kullanıyor: `QualityMeter.cs:140` `model=version=vmaf_v0.6.1neg`, dışarıdan `.json`/`.pkl` yolu yok. Yerel doğrulama: `testsrc2` ile iki akış `split` edilip `libvmaf=model=version=vmaf_v0.6.1neg` çalıştırıldı, `vmaf.json` üretildi (ek dosya gerekmedi). |
| Atlanmaya devam eden ölçü | 0 — libvmaf CI'da da var, kalite ölçen testler libvmaf yokluğu yüzünden atlanmıyor (aşağıdaki 17 kalıcı atlanan tam başka nedenlerden, bkz. K3). |

## K3 — öncesi/sonrası Passed/Skipped/Total

| Aşama | Koşum | Commit | Failed | Passed | Skipped | Total | Süre |
|---|---|---|---|---|---|---|---|
| Öncesi (ffmpeg yok) | `33582206982` | `68cb3c93` | 0 | 1024 | 95 | 1119 | 10 m 21 s |
| Sonrası (ffmpeg kurulu, nihai dal ucu) | `33584487781` | `5694bc6d` | 1 | 1156 | 17 | 1174 | 16 m 10 s |

Skipped 95 → 17: **78 ölçü** atlanmaktan gerçekten koşmaya geçti — ~80
beklentisiyle örtüşüyor. Total 1119 → 1174 (+55) bu 78'den ayrı bir etki:
aradaki commit farkı (T109/T110) main'e yeni testler ekledi; iki koşum aynı
kod tabanında değil, dolayısıyla Total farkını doğrudan "atlamadan kurtulan
sayı" olarak okumak yanlış olur — Skipped farkı (78) izole edilmiş sayı,
Total farkı değil.

Ayrı, isimli doğrulama — T110'un kare-kilidi ölçüleri (5 adet, hepsi
`QualityMeterTests`, kilit tutulmazsa ilk kırılacak testler):

| Ölçü | Öncesi (`33582483001`, `3868d621`, ffmpeg yok) | Sonrası (`33582768395`, `0a56868f`, ffmpeg kurulu) |
|---|---|---|
| `OneFrameOfSlipIsWorthTensOfVmafPointsOnThisFixture` | [SKIP] | koştu, geçti |
| `SubFrameTimestampSlipDoesNotCostTheScoreAWholeFrame` | [SKIP] | koştu, geçti |
| `ShiftedSourceIsReportedNotSilentlyRepaired` | [SKIP] | koştu, geçti |
| `VideoStartAheadOfTheContainerIsTheOffsetThatReachesTheFilterGraph` | [SKIP] | koştu, geçti |
| `UntaggedSourceAgainstANonBt709TagIsRefusedInsteadOfAssumed` | [SKIP] | koştu, geçti |

("Koştu, geçti" = günlükte `[SKIP]`/`[FAIL]` olarak görünmüyor; xUnit varsayılanında
geçen testler ayrı satır basmıyor, yokluğu geçtiğinin kanıtı.) Bu beş ölçü
T110'dan önce hiç koşmadan geçiyordu; bugün kilidi kaldıran bir değişiklik
CI'da yeşil kalırdı. T115'in var oluş nedeni tam bu.

## K4 — koşan yeni ölçüler yeşil mi

Hayır, biri değil. Tek kırmızı, iki bağımsız koşumda (`33583024971` rebase
öncesi commit `e3c73115`, ve nihai `33584487781` commit `5694bc6d`) aynı
şekilde tekrarlandı — kararsız (flaky) değil, ortam-yapısal:

**`VidShrink.Tests.PerformanceCheckTests.IslemciZamaniSayaciDogruOkuyorMu`**
— `tests/VidShrink.Tests/PerformanceCheckTests.cs:713` (çağrı yeri, `h264_nvenc`
kolu, `EncoderCapabilities.Instance.HasEncoder("h264_nvenc")` koruması
satır 710'da), gerçek düşme `tests/VidShrink.Tests/PerformanceCheckTests.cs:763`
(`Kos()` yardımcısındaki `Assert.True(p.ExitCode == 0, ...)`).

Ölçülen hata metni (koşum `33584487781`, `--log-failed`):
```
(d) nvenc -threads 1 #0 kosumu -1 ile dondu: [h264_nvenc @ ...] Cannot load nvcuda.dll
```

Kök neden: `EncoderCapabilities.HasEncoder` (`src/VidShrink.Ffmpeg/EncoderCapabilities.cs:27`)
`ffmpeg -encoders` çıktısında adın **listede olup olmadığına** bakıyor —
donanımın gerçekten çalışır olduğunu doğrulamıyor. GitHub Actions
`windows-latest` koşucusunda NVIDIA sürücüsü/GPU'su yok; ffmpeg `h264_nvenc`'i
derlenmiş kodek olarak listeliyor ama çalışma anında `nvcuda.dll`'i
yükleyemiyor. Aynı `HasEncoder` mekanizması `src/VidShrink.Ffmpeg/PerformanceProbe.cs:81`'de
de kullanılıyor — bu üründe de aynı yanlış-pozitife açık bir yüzey olabilir,
ayrı inceleme ister.

Bu dosya benim `owns`'umda değil (`tests/VidShrink.Tests/**` T115'in kapsamı
dışında) — **düzeltilmedi**. Kaynak sözleşme **T63**
(`.claude/relay/contracts/done/T63.md`, kapalı) bu testi yazan sözleşme.
T0 yeni bir tur açmalı: test ya CI'da NVENC'in gerçekte kullanılamayacağını
bilip `[FfmpegFact]`'e ek bir donanım-var mı kontrolüyle atlamalı, ya da
`HasEncoder` ürün kodunda gerçek bir prob'a (küçük bir encode denemesi)
dönüşmeli — ikinci seçenek `PerformanceProbe.cs:81`'deki aynı kusuru da kapatır.

**K7 ile karıştırılmasın:** `33582680807` (ayrı, bilerek bozulmuş kanıt dalı
`T115-kanit-t110`, commit `f2f05f5f`) kurulum adımının kendisinde düştü —
hiçbir test koşmadı (bkz. K7). Yukarıdaki `IslemciZamaniSayaciDogruOkuyorMu`
ise kurulum başarılıyken, testin kendisi gerçekten koşup gerçekten kırmızıya
düştü — iki farklı bulgu, iki farklı koşum.

## K5 — koşum süresi

| Aşama | Adım | Süre |
|---|---|---|
| Öncesi (`33582206982`) | `dotnet test` adımı (kosum-kapisi) | 10 m 29 s (10 m 21 s xUnit içi) |
| Öncesi (`33582206982`) | iş (job) toplamı, `checkout`→`complete` | 12 m 29 s |
| Sonrası (`33584487781`) | ffmpeg kurulumu | 9 s |
| Sonrası (`33584487781`) | sürüm/libvmaf kontrolü | 1 s |
| Sonrası (`33584487781`) | `dotnet test` adımı (kosum-kapisi) | 16 m 22 s (16 m 10 s xUnit içi) |
| Sonrası (`33584487781`) | iş (job) toplamı, `checkout`→`complete` | 17 m 48 s |

İş toplamı 12 dk 29 sn'den 17 dk 48 sn'ye çıktı (+5 dk 19 sn) — ikiye
katlanmadı, ~1.43x. Fark neredeyse tamamı test adımından (+5 dk 53 sn,
kurulum ve libvmaf kontrolü toplam 10 saniyeyle karşılaştırıldığında
gözle görülür payı yok — test adımındaki artış iş toplamındaki artıştan
biraz daha büyük çünkü checkout/setup-dotnet gibi diğer adımlar da koşum
koşum küçük farklar taşıyor). Artış, 78 ölçünün artık gerçekten ffmpeg
çağırması (encode/probe/kalite ölçümü — CPU-yoğun işler) demek; kabul
edilebilir, çünkü CI'nın ölçtüğü şey artık gerçek: önceki 10 dk 21 sn,
78 ölçüyü hiç çalıştırmadan geçen bir sayıydı.

## K6 — `kosum-kapisi.ps1 -MinimumTotal`

Eşik **950 → 1000** olarak değiştirildi (bu sözleşmenin `owns`'unda,
`.github/workflows/ci.yml:74`).

Gerekçe — "yeni sayıya uydurmak" değil, kapının kendi tarihindeki oranı
korumak: eşik ilk konduğunda (`9ae6dce`, 01.09.2026) 950 seçilmişti; o
commit ile bu raporun tabanı (`68cb3c93`, Total 1119) arasında test
dosyalarını değiştiren onlarca commit var (T96–T105 aralığı), yani o
andaki gerçek Total'i geriye dönük ölçmedim — 1119'u yaklaşık referans
alıyorum. Oran 950/1119 ≈ **%85** (üst sınır; gerçek tarihsel oran
muhtemelen bir miktar farklıydı). Kapının işi Total'i izlemek değil,
"Test host process crashed" gibi çökmüş/yarım bir koşumu (Failed: 0 basıp
0 ile çıkan) yakalamak — bunun için gerçek Total'in büyük bir kısmının
altında, ama normal test sayısı büyümesine tolerans tanıyan bir taban
gerekiyor. Nihai ölçülen Total artık 1174; aynı %85 oranı 1174 × 0.85 ≈ 998,
yuvarlanarak **1000**. Bu, eski eşiğin bıraktığı boşluğu (1119 − 950 = 169
~ %15) korur, gevşetmez ya da rastgele daraltmaz.

Kapının asıl işi hâlâ çalışıyor mu: evet — mekanizma (`kosum-kapisi.ps1`)
değişmedi, sadece parametre değişti; çökmüş/yarım koşum hâlâ "Failed: 0,
Total küçük" üretir ve 1000 tabanının altında kalır, kapı hâlâ reddeder.

## K7 — kurulum başarısızlığı kırmızı mı

Evet — gerçek, sahnelenmemiş kanıt: koşum **`33582680807`** (dal
`T115-kanit-t110`, commit `f2f05f5f`, `conclusion: failure`). Sha256
sabitimin ilk yazımında son "E" harfi eksikti (63 hane); kurulum adımı
`Write-Error: ffmpeg indirmesi sha256 uyusmuyor: beklenen=...5055
gelen=...5055E` ile düştü ve iş akışı **hiçbir testi çalıştırmadan** durdu —
adım listesi `ffmpeg kur` adımını `X`, ondan sonraki tüm adımları `-`
(hiç çalışmamış) gösteriyor. Sabit düzeltilip (`sed` ile, doğru değer
`...5055E`) yeniden itildi; sonraki koşumlar kurulumu geçti. Bu, K7'nin
istediği "kurulum düşerse sessizce devam etmez" davranışının canlı
kanıtı — ayrıca kendi hatamdan çıkan, gerçek bir hard-fail örneği.

## K8 — lisans notu

`ci.yml` içine tek cümlelik not eklendi (kurulum adımının üstünde):
ffmpeg'i CI koşucusuna kurmak dağıtım değildir, `release.yml` başındaki
GPLv3/AGPL-3.0-or-later notuyla çelişmez. Yerinde, doğrulandı.

## `tools/ci-gibi-kos.sh` — artık CI'yı temsil etmiyor

Bu dosya T115'in `owns`'unda değil, **dokunulmadı**. Betik PATH'ten
`ffmpeg`/`WinGet` girdilerini bilerek siliyor ve "bu koşum CI'ın hâlini
temsil eder" diyor (kendi üstündeki yorum). Kaynağı: commit `aed7ee0`,
kapalı sözleşme **T66** (`.claude/relay/contracts/done/T66.md`) —
"CI'ın gördüğü hali yerelde koşturan düzenek".

T115 CI'ya ffmpeg'i soktuğu için bu öncül artık yanlış: betik hâlâ
ffmpeg'i PATH'ten siliyor ama CI artık ffmpeg görüyor, yani "CI gibi kos"
adıyla **CI'dan farklı** bir şey koşturuyor — betiğin ismi ve üstündeki
Türkçe yorum yanıltıcı hale geldi. T0'ın bunu T66'nın mirasçısı bir tur
olarak açması gerekir: ya betik ffmpeg'i PATH'te bırakıp yalnız donanım
kodlayıcıyı (NVENC) simüle etsin, ya da adı/amacı "CI'ın artık neyi
görmediğini" (örn. gerçek GPU) yansıtacak şekilde yeniden yazılsın.
