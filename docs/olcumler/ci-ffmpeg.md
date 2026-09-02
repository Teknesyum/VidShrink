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

Skipped 95 → 17: **78 ölçü** atlanmaktan gerçekten koşmaya geçti (isim
kümesi farkı, `comm` ile — aritmetikle değil) — ~80 beklentisiyle örtüşüyor.
Total 1119 → 1174 (+55) bu 78'den ayrı bir etki: aradaki commit farkı
(T109/T110) main'e yeni testler ekledi; iki koşum aynı kod tabanında değil,
dolayısıyla Total farkını doğrudan "atlamadan kurtulan sayı" olarak okumak
yanlış olur — Skipped farkı (78) izole edilmiş sayı, Total farkı değil.

**İkinci, daha sıkı kontrollü bir çift** aynı isim-kümesi yöntemiyle **83**
veriyor: `33582483001` (`3868d621`, ffmpeg yok, Skipped 100/Total 1124) →
`33582768395` (`0a56868f`, ffmpeg kurulu, Skipped 17/Total 1126). Bu çift
neredeyse aynı ağaç üzerinde (aradaki commit farkı çok daha dar), bu yüzden
**83 daha güvenilir sayı** — 78, T109/T110 gibi araya giren commit'lerin de
karıştığı daha geniş bir aralığı ölçüyor. İkisi de doğru, farklı şeyi
ölçüyorlar: 78 = geniş aralık (`33582206982`→`33584487781`), 83 = dar/
neredeyse-aynı-ağaç karşılaştırması (`33582483001`→`33582768395`).

Ayrı, isimli doğrulama — T110'un kare-kilidi ölçüleri (5 adet, hepsi
`QualityMeterTests`, kilit tutulmazsa ilk kırılacak testler, **83'ün
içinde**, 78'in değil — 78'in tabanı T110 main'e girmeden önce alındı):

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
yükleyemiyor. **Düzeltme**: yeni bir prob yazmaya gerek yok, gerçek donanım
prob'u zaten var — `EncoderCapabilities.cs:30`, `WorksAsEncoder(codec) =>
Probe(codec).Succeeded`. `src/VidShrink.Ffmpeg/PerformanceProbe.cs:81`'deki
`HardwareCandidates.FirstOrDefault(availability.HasEncoder)` çağrısı bu
mevcut prob'a bağlanmalı — liste-üyeliği yerine gerçek çalışırlık testi.
Bu iş **T115'in kapsamında değil**, **T117'ye taşındı**.

Bu dosya benim `owns`'umda değil (`tests/VidShrink.Tests/**` T115'in kapsamı
dışında) — **düzeltilmedi**. Kaynak sözleşme **T63**
(`.claude/relay/contracts/done/T63.md`, kapalı) bu testi yazan sözleşme.
Düzeltme yukarıda tarif edildi (`WorksAsEncoder` → `PerformanceProbe.cs:81`),
iş **T117**'de.

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

## K6 — `kosum-kapisi.ps1 -MinimumTotal` / `-MaximumSkipped` (tur 2, düzeltildi)

**Tur 1'in hatası** (denetçi buldu, `.claude/relay/contracts/T115.md` Tur 2):
eşik 950 → 1000 türetimi ölçülmemiş bir tarihsel Total'e (~1119, "yaklaşık
referans") dayanıyordu. Denetçi eşiğin ilk konduğu commit'i (`9ae6dce`)
gerçekten ölçtü: koşum **`33525816911`** — `Failed: 0, Passed: 911,
Skipped: 72, Total: 983`, `KOSUM KAPISI GECTI: toplam=983 alt-sinir=950`.
Doğrulama: `gh run view 33525816911 --json headSha,conclusion` → headSha
`9ae6dce`, `conclusion: success`, çıktısında yukarıdaki özet satırı var.

Gerçek taban **983**, gerçek oran 950/983 ≈ **%96,6** (**alt sınır**, üst
sınır değil — Total zamanla yalnız büyür, yani `950/Total_tarihsel` gerçek
orana bir alt sınırdır; ben bunu üst sınır gibi kullanıp eşiği gevşek
tarafa düşürmüştüm). Oran korunarak türetilen doğru değer:
0,966 × 1174 ≈ **1134**. Tur 1'de seçtiğim 1000, tasarımın bıraktığı asıl
boşluğun (983 − 950 = 33 ölçü, %3,4) **5,3 katı** (174 ölçü) boşluk
bırakıyordu — "gevşetmez" iddiam yanlıştı, kapı gevşetilmişti. Bu turda
`-MinimumTotal` **1134** olarak düzeltildi (`.github/workflows/ci.yml:74`).

**Yapısal soru — `-MinimumTotal` tek başına sessiz atlama-körlüğünü hiçbir
değerde yakalayamaz**, çünkü `Total = Passed + Failed + Skipped`: bir
testin Passed'den Skipped'e geçmesi Total'i değiştirmez. Denetçi bunu iki
sahte-geçme senaryosuyla gösterdi (`kosum-kapisi.ps1 -InputFile` ile):

| Senaryo | Eski (`-MinimumTotal 1000`, tek kapı) | Yeni (`-MinimumTotal 1134 -MaximumSkipped 30`) |
|---|---|---|
| Körlük tümüyle geri geldi: `Failed: 0, Passed: 1079, Skipped: 95, Total: 1174` | **GEÇTİ** (yanlış) | **DÜŞTÜ**, `kod=69`, `"Atlanan sayisi ust sinirin ustunde: 95 > 30."` |
| 78 ölçü büsbütün yok oldu: `Failed: 0, Passed: 1079, Skipped: 17, Total: 1096` | **GEÇTİ** (yanlış) | **DÜŞTÜ**, `kod=68`, `"Toplam test sayisi alt sinirin altinda: 1096 < 1134."` |

Seçilen yol: **(a) ikinci kapı eklendi** — `tools/kosum-kapisi/kosum-kapisi.ps1`'e
opsiyonel `-MaximumSkipped` parametresi ve yeni çıkış kodu **69**
(`Atlanan/Skipped ozeti yok` ya da üst sınır aşımı). Gerekçe: dosya zaten
`owns`'umda, ekleme geriye-uyumlu (parametre `Nullable[int]`, verilmezse
eski davranış birebir), ve gösterilen deliği doğrudan kapatıyor — ayrı bir
sözleşmeye ertelemek (seçenek c) gereksiz gecikme, ölçmeden bırakmak
(seçenek b) denetçinin ölçtüğü deliği kapatmıyor. Değer **30**: mevcut
meşru kalıcı atlama sayısının (17) kabaca iki katı, organik ortam-gated
test büyümesine yer bırakıyor ama tarihsel "körlük" değerlerinin (72, 95,
97, 100 — hepsi bu ve tur-1 raporunda ölçülen gerçek koşumlar) çok altında.

Bu davranış kalıcı regresyon testine bağlandı:
`tools/kosum-kapisi/fixtures/korluk-geri-en.txt` (`Failed: 0, Passed: 1079,
Skipped: 95, Total: 1174`) ve `tools/kosum-kapisi/test-kapi.ps1`'e eklenen
`-MinimumTotal 1134 -MaximumSkipped 30` reddetme durumu (beklenen çıkış
69) — `test-kapi.ps1` yerelde çalıştırıldı, tüm durumlar (4 geçerli, 6
reddetme, ikisi yeni) beklenen kodu verdi.

Kapının **asıl işi** (çökmüş/yarım koşumu yakalamak) hâlâ çalışıyor:
mekanizma değişmedi, `-MinimumTotal` hâlâ "Failed: 0, Total küçük"
durumunu 1134 altında yakalıyor. Kapının **yeni işi** (sessiz atlama-körlüğü)
yukarıdaki iki senaryoyla ölçüldü ve **yakalandığı doğrulandı** — önceden
"yakalar" diye iddia edip ölçmemiştim, bu turda hem iddia hem ölçüm var.

**Düzeltilmiş `ci.yml`'in gerçek koşumu** (tur 1'in borcu — teslim edilen
`ci.yml` denetim anında hiç tamamlanmış bir koşuma sahip değildi, ölçülen
koşumların hepsi eski `950` ile koşmuştu): koşum **`33589639249`**, commit
**`0e122f2`**, `conclusion: failure` — `Failed: 1, Passed: 1162, Skipped: 17,
Total: 1180, 18 m 59 s`, `KOSUM KAPISI DUSTU: kod=66 sart=Basarisiz/Failed
ozeti sifir degil: Failed: 1.` Bu, K4'ün bilinen `nvcuda.dll` ortam-yapısal
kırmızısından geliyor (aynı bulgu, değişmedi) — kapı `Failed:1`'de daha
`-MinimumTotal`/`-MaximumSkipped` kontrollerine gelmeden reddediyor (kod 66,
sırayla önce gelen kontrol). Yani bu koşum eşiklerin kendisini yeşil bir
kapıda sınamadı; onu yukarıdaki `-InputFile` senaryolarıyla yerelde sınadım.
Kanıtladığı şey: düzeltilmiş `ci.yml` gerçekten tamamlanıyor, yeni
parametreler (`-MinimumTotal 1134 -MaximumSkipped 30`) hatasız kabul
ediliyor ve gerçek bir Failed durumunda kapı hâlâ doğru reddediyor.
Doğrulama: `gh run view 33589639249 --json headSha,conclusion`.

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

## K9 — kod=66 yanlış kırmızısı (tur 3)

T118 ölçerken yakaladı: bu makinede `tools/kosum-kapisi/kosum-kapisi.ps1`
gerçekten yeşil bir koşumda (`Failed 0, Passed 1163, Skipped 17, Total 1180`)
`kod=66 "özet yok"` ile düşüyor. İki ayrı, birbirinden bağımsız kök neden
ölçüldü — ikisi de aynı canlı-yakalama bloğunda yaşıyor.

### Kök neden 1 — konsol kod sayfası, dil değil

Bu makine `pwsh` içermiyor (`command -v pwsh` → çıkış 1); kapı yalnız
Windows PowerShell 5.1 ile çalışıyor. Ölçülen kod sayfası: `[Console]::
OutputEncoding` = Türkçe (DOS), CodePage 857; `$OutputEncoding` = US-ASCII,
CodePage 20127 — ikisi de UTF-8 değil.

Yalıtılmış kanıt: `gecerli-tr.txt`'yi (`Başarısız: 0, ...`) `cmd /c type` ile
okuyup PS 5.1'in `2>&1 | ForEach-Object` deseniyle yakalayınca, metin ekranda
doğru görünse de bellekteki baytlar bozuluyor (`ş`,`ı` yerine kutu-çizim
karakterleri) ve `(?:Başarısız|Failed)` deseni **0 eşleşme** veriyor —
oysa betiğin kalıbı zaten Türkçe anahtar kelimeyi de arıyordu. Demek ki
sorun **dil değil, kod sayfası**: dil kalıbı doğruydu, taşıyan bayt akışı
bozuktu. `$OutputEncoding = UTF8` tek başına düzeltmiyor (yine 0 eşleşme);
düzelten `[Console]::OutputEncoding = UTF8` — PowerShell'in dış süreç
çıktısını bu değere göre çözdüğünü doğruluyor. Script başına eklendi
(`try { } catch { }` ile sarılı, `pwsh`'de zararsız no-op).

### Kök neden 2 — kendi düzeltmem aynı hatayı bir kez daha üretti

İlk düzeltme denemesinde regex kalıplarındaki `\u`-kaçışlarını (`Ba\u015far
\u0131s\u0131z`) düz Türkçe karaktere (`Başarısız`) çevirdim — ve
`gecerli-tr.txt` fikstürü aniden kod=66 ile reddedilmeye başladı. Neden:
BOM'suz `.ps1` dosyasını PS 5.1 **kaynak kodu olarak** ayrıştırırken de
sistem kod sayfasını (857) kullanıyor; kaynaktaki UTF-8 baytları o kod
sayfasıyla yanlış çözülüyor ve regex deseni artık fikstürdeki (doğru
okunmuş) metinle eşleşmiyor. Orijinal betiğin `\u015f`/`\u0131` gibi kaçış
dizileri kullanması bilinçliydi — kaynak kod sayfasından bağımsız kalmak
için. Geri döndüm: tüm işlevsel (regex'te kullanılan) Türkçe literaller
yine `\u`-kaçışlı; yalnız kozmetik `Write-Host` başarı mesajı (orijinalde
de öyleydi, işlevsel değil) düz karakterle kaldı.

### Seçilen çözüm — `.trx` logger, ikinci dil kalıbı değil

İki seçenek sözleşmede sıralıydı: (a) iki dilin kalıbını yan yana arama,
(b) `--logger "trx"` ile dilden bağımsız sayı okuma. (a) **zaten mevcuttu**
(`Başarısız|Failed`, `Toplam|Total`, `Atlanan|Skipped`) ve yine de
düşüyordu — çünkü hata dil değil kod sayfasıydı; ikinci bir dil kalıbı
eklemek bu sınıftaki hiçbir hatayı kapatmazdı. (b) seçildi: `.trx` bir XML
dosyası, disktan doğrudan okunuyor (konsol yakalamasından geçmiyor),
kendi `encoding="utf-8"` bildirimini taşıyor ve `Counters` özniteliklerinin
adları (`total`, `passed`, `failed`) yerelden bağımsız sabit. Bu, hem dil
hem kod sayfası sorununu aynı anda kapatıyor — (a)'nın kapatamadığı ikinci
sınıf.

Ölçülen bir tuzak: gerçek bir `.trx` çıktısında (`dotnet test --filter
UpdaterTests`, 3 `[Skip]`'li test) `Counters/@notExecuted` **0** kaldı,
oysa 3 test atlandı — xUnit'in `[Skip]` özniteliği trx'te `outcome=
"NotExecuted"` olarak tekil sonuçlara yazılıyor ama toplayıcı `Counters`
bunu ayrı bir sayaçta biriktirmiyor. Çözüm: `Atlanan = Toplam - Geçen -
Başarısız` — kapının kendi yapısal özdeşliğini (tur 2, `Total = Passed +
Failed + Skipped`) kaynak değiştirince de kullanmaya devam etmek.

Dokunulmayan: metin-regex yolu `-InputFile <düz metin>` ile hâlâ çalışıyor
(geriye uyum), yalnız canlı koşum (`-InputFile` verilmeden) artık önce
`.trx` arıyor, bulamazsa metne düşüyor.

### Bulunan üçüncü hata — EAP=Stop, native stderr'i sonlandırıcıya çeviriyor

Gerçek tam-suit koşumunda `ComplexityProbeTests.CancellationReachesQuality
Measurement` zaman aşımına uğradı ve xUnit `[FAIL]` satırını stderr'e
yazdı. `$ErrorActionPreference = 'Stop'` altında `2>&1 | ForEach-Object`
bunu `NativeCommandError`'a çevirip yakalama döngüsünü **betiğin kendi
çıkış kodunu hiç yazmadan** çökertti — betik ne 65/66/68/69 ne de 0 verdi,
PowerShell'in kendi hata kodunda düştü. Yalıtılmış kanıt: `cmd /c "echo
err 1>&2"` aynı desenle `RemoteException` fırlatıyor. Bu, T115 tur 3'ün
konusu olan hatadan **ayrı, önceden var olan** bir kusur (orijinal betikte
de aynı desen vardı, tetiklenmemişti) — aynı bloğu değiştirdiğim ve
sözleşmenin istediği "gerçekten tamamlanmış koşum" kanıtını üretemeden
engellendiğim için düzelttim: `$ErrorActionPreference` yakalama bloğunda
geçici olarak `Continue`'ya çekiliyor, blok bitince eski değerine dönüyor.

`ComplexityProbeTests.CancellationReachesQualityMeasurement`'in kendisi
(31s zaman aşımı) T115'in `owns`'unda değil — ayrı, muhtemelen kararsız
bir ölçü; buraya not düşüldü, dokunulmadı.

### Kanıt — fikstür + gerçek koşum

Fikstür seviyesi: `tools/kosum-kapisi/fixtures/*.trx` (5 yeni dosya —
`gecerli`, `basarisiz`→66, `eksik-toplam`→68, `korluk-geri`→69,
`ozet-yok`→66) + var olan 9 metin fikstürü, `test-kapi.ps1` üzerinden
toplam 15 durum, hepsi beklenen kodu verdi (geriye uyum korundu).

Gerçek koşum: bu makinede, `pwsh` yokluğunda, `-InputFile` **verilmeden**
tam suit çalıştırıldı — `Başarılı!  - Başarısız: 0, Başarılı: 1163,
Atlanan: 17, Toplam: 1180, Süre: 15 m 13 s`, kapı `.trx`'i buldu
(`Counters total="1180" passed="1163" failed="0"`, atlanan = 1180-1163-0 =
17) ve **`$LASTEXITCODE = 0`** verdi — T118'in bildirdiği tam sayılarla,
tam bu makinede, düzeltmeden önce kod=66 üreten senaryo artık geçiyor.
Çıkış kodu ayrı bir dosyaya yazılarak doğrulandı (`tail | $?` borusunun
son komutun kodunu döndürdüğü ilk ölçüm hatalıydı, düzeltilip tekrar
ölçüldü).
