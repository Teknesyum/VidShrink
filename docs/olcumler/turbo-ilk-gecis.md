# Turbo ilk geçiş (T140)

Durum: K1–K7 kapalı. Dal `T140-turbo-ilk-gecis`, `main` üzerinden açıldı, `main`e
birleştirilmedi.

Bu sözleşme `docs/inceleme/handbrake-motoru.md:626` (§6 madde 6) ile
`docs/taramalar/handbrake.md` "Alınacak fikir" satırının açık bıraktığı maddeyi
kapatır: iki geçişli kodlamada analiz geçişi son geçişle aynı preset'te koşmak
zorunda değil.

Kazanç süredir. **Bu depoda ne süre ne kalite ölçüldü**, bu rapordaki hiçbir sayı
bir kodlama koşumundan gelmiyor; hepsi argüman üretiminin çıktısı. Ölçüm bir
sonraki sözleşmenin işi, K5'e bakın.

## Commit'ler

| Kriter | Commit | Ne yaptı |
| --- | --- | --- |
| K1 | `ec36303` | Kusuru ölçen kırmızı: ilk geçiş son geçişin preset'ini koşuyor |
| K2 | `2fb28db` | Turbo kümesi `CodecModel` içinde veri |
| K3 | `e3f9bde` | `FirstPassPreset` + `Build` içinde `pass == 1` dalı |
| K4 | `6018c6e` | Küme dışındaki kodlayıcıların argümanı değişmiyor ölçüsü |
| K5 | `ba1f6be` | Varsayılan kapalı, anahtar JSON'a kapalı |

Sayım: yukarıdaki tabloda **beş** satır var, yani beş kriter commit'i. Bu raporu
getiren commit altıncıdır ve tabloda yok.

## K1 — Kusur önce ölçüldü

`ec36303` yalnız iki şey getirdi: `EncodePlan.TurboFirstPass` anahtarı (davranışı
hiç okumayan salt veri) ve onu kullanan ilk ölçü. Anahtarın tanımı bu commit'te
duruyor çünkü ölçü onsuz **yazılamıyor**; anahtarın varsayılan kararı ve o kararı
tutan ölçüler K5'in kendi commit'inde.

Kırmızı çıktı (`dotnet test -c Release --filter "TurboFirstPassTests"`, `ec36303`):

```
[xUnit.net 00:00:12.42]     VidShrink.Tests.TurboFirstPassTests.Turbo_acikken_ilk_gecis_son_gecisin_on_ayarini_kosmaz [FAIL]
  Başarısız VidShrink.Tests.TurboFirstPassTests.Turbo_acikken_ilk_gecis_son_gecisin_on_ayarini_kosmaz [59 ms]
  Hata İletisi:
   Assert.Equal() Failure: Strings differ
           ↓ (pos 0)
Expected: "veryfast"
Actual:   "slow"
           ↑ (pos 0)
  Yığın İzleme:
     at VidShrink.Tests.TurboFirstPassTests.Turbo_acikken_ilk_gecis_son_gecisin_on_ayarini_kosmaz() in tests\VidShrink.Tests\TurboFirstPassTests.cs:line 49

Başarısız! - Başarısız:     1, Başarılı:     0, Atlanan:     0, Toplam:     1, Süre: < 1 ms - VidShrink.Tests.dll (net8.0)
```

`Actual: "slow"` bugünkü davranıştır: `preset slow` istenen bir planda analiz
geçişi de `slow` koşuyor.

## K2 — Turbo desteği kodek verisinde

Bilgi `CodecModel.TurboFirstPassCeilings` sözlüğünde duruyor; her satır hem
"turbo geçerli mi" hem "tavan ne" sorusunu taşıyor. `FfmpegArguments.Build`
içinde kodlayıcı adına bakan hiçbir koşul yok, tek soru
`CodecModel.TurboFirstPassCeiling(codec)` null mu değil mi.

`docs/taramalar/handbrake.md` bu bayrağı `src/VidShrink.Ffmpeg/EncoderCapabilities.cs`
içine öneriyordu; sözleşme K2 onu `CodecModel`e koydu ve `owns` listesi de öyle
diyor. Sözleşme kazandı; sapma bilerek.

### Kodlayıcı başına karar ve gerekçe

Argüman üretiminin tanıdığı kodlayıcılar `FfmpegArguments.Presets` tablosundan
türüyor (`KnownCodecs`).

| # | Kodlayıcı | Yazılım mı | İki geçiş | Turbo | Gerekçe |
| --- | --- | --- | --- | --- | --- |
| 1 | `libx264` | evet | evet | **evet** | HandBrake'in `turbo_supported` kümesi bu kodlayıcıyı içeriyor; preset merdiveni tek yönlü, hızlıdan yavaşa sıralı, "daha hızlı" tek anlamlı |
| 2 | `libx265` | evet | evet | **evet** | Aynı gerekçe; merdiveni x264 ile birebir aynı |
| 3 | `libvpx-vp9` | evet | evet | hayır | HandBrake kümesinde yok. Merdiveni `0…8` ve ters yönlü: burada büyük sayı hızlı. "Bir basamak hızlı" x264'tekiyle aynı işlem değil, ölçülmeden aynı kural uygulanamaz |
| 4 | `libsvtav1` | evet | evet | hayır | HandBrake kümesinde yok. Merdiveni `0…13`, yine ters yönlü. SVT-AV1'in preset semantiği x264'ünkinden farklı: basamak yalnız arama derinliğini değil açık kalan araç kümesini de değiştiriyor, ilk geçişin ürettiği istatistik dosyası son geçişin varsaydığı araçlarla eşleşmeyebilir. Ölçülmedi, kümeye alınmadı |
| 5 | `h264_nvenc` | hayır | hayır | hayır | Donanım: `NeedsTwoPasses` false, ilk geçiş yoluna hiç girmiyor |
| 6 | `hevc_nvenc` | hayır | hayır | hayır | Aynı |
| 7 | `h264_qsv` | hayır | hayır | hayır | Aynı |
| 8 | `hevc_qsv` | hayır | hayır | hayır | Aynı |
| 9 | `av1_nvenc` | hayır | hayır | hayır | Aynı |
| 10 | `av1_qsv` | hayır | hayır | hayır | Aynı |
| 11 | `h264_amf` | hayır | hayır | hayır | Aynı |
| 12 | `hevc_amf` | hayır | hayır | hayır | Aynı |
| 13 | `av1_amf` | hayır | hayır | hayır | Aynı |

Sayım — yukarıdaki tabloyu satır satır saydım: **13** kodlayıcı; **4**'ü yazılım
(1–4), **9**'u donanım (5–13); iki geçiş isteyen **4** (1–4, `NeedsTwoPasses`
donanımı dışarıda bırakıyor); turbo kümesinde **2** (1–2); iki geçişli olup
turbo tanımayan **2** (3–4).

Bu sayımı ölçü de tutuyor: `Bilinen_kodekler_dort_yazilim_dokuz_donanim`.

## K3 — İlk geçiş preset'i

### Merdivenin kaynağı

`src/VidShrink.Core/FfmpegArguments.cs` içindeki `Presets` sözlüğü. Kümedeki iki
kodlayıcının merdiveni **9 basamak** ve 0. basamak en hızlı:

```
ultrafast superfast veryfast faster fast medium slow slower veryslow
```

Dokuz basamağı saydım. Merdiven `FfmpegArguments.PresetLadder(codec)` ile ölçüye
açıldı. Test **kendi kopyasını da taşıyor**
(`tests/VidShrink.Tests/TurboFirstPassTests.cs:34-35`, `YazilimMerdiveni`) ve
`Kumedeki_kodeklerin_merdiveni_dokuz_basamak_ve_hizlidan_yavasa` deponun
tablosunu bu kopyayla karşılaştırıyor; yani ölçü, merdivenin bu içerik ve sırada
olduğunu pimliyor. Kopya bilerek duruyor: `PresetLadder`ı okuyup ona göre beklenti
kurmak, merdiven değişirse ölçüyü de birlikte kaydırırdı.

### Seçilen kural: tavan

İki seçenek vardı, "N basamak hızlı" ve "tavan". **Tavan** seçildi: ilk geçiş
merdivende `veryfast`ten (2. basamak) yavaş koşamaz.

- *Neden N basamak değil:* N basamak göreli bir kural, ilk geçişin maliyetini
  son geçişe bağlı bırakır. `veryslow`dan dört basamak hızlı `fast`tır ve `fast`
  hâlâ pahalı bir analiz geçişidir. Turbo'nun amacı analizi **mutlak** olarak
  ucuzlatmak; tavan tek bir en kötü hâl veriyor.
- *Neden 2. basamak, 0. değil:* `ultrafast` x264'te CABAC/trellis/8x8dct gibi
  araçları tümden kapatıyor; üretilen istatistik dosyası son geçişin koşacağı
  kodlayıcıya benzemekten çıkar. `veryfast` aynı araç kümesini koruyan en hızlı
  basamak. **Bu bir gerekçedir, ölçüm değildir** — bu depoda ölçülmedi.
  Sözleşme K3 tavan örneğini `veryfast` olarak veriyor; sabit kendi kendime
  uydurulmadı.
- İstenen preset zaten tavandan hızlıysa ilk geçiş **yavaşlatılmıyor**, istenen
  preset olduğu gibi kalıyor.

### İlk geçiş tablosu

`FfmpegArguments.FirstPassPreset(codec, preset, turbo: true)` çıktısı, merdivenin
her basamağı için. Kümedeki iki kodlayıcı aynı sonucu veriyor:

| Son geçiş preset'i | İlk geçiş (`libx264`) | İlk geçiş (`libx265`) | Değişti mi |
| --- | --- | --- | --- |
| `ultrafast` | `ultrafast` | `ultrafast` | hayır |
| `superfast` | `superfast` | `superfast` | hayır |
| `veryfast` | `veryfast` | `veryfast` | hayır |
| `faster` | `veryfast` | `veryfast` | evet |
| `fast` | `veryfast` | `veryfast` | evet |
| `medium` | `veryfast` | `veryfast` | evet |
| `slow` | `veryfast` | `veryfast` | evet |
| `slower` | `veryfast` | `veryfast` | evet |
| `veryslow` | `veryfast` | `veryfast` | evet |

Dokuz satır, kodlayıcı başına dokuz basamak; değişen **6**, değişmeyen **3**
satır — saydım. Planın varsayılan preset'i `slow` (`EncodePlan.Preset`;
`FfmpegArguments.DefaultPreset` kümedeki iki kodlayıcı için de `slow` döner —
diğer kodlayıcılar başka değerler alıyor, K4 tablosuna bakın), yani varsayılan
yerleşimde ilk geçiş merdivende **dört basamak** yukarı çıkıyor:
`slow` (6) → `veryfast` (2).

### Son geçiş değişmedi

`Turbo_son_gecisin_argumanina_dokunmuyor`: beş preset × kümedeki iki kodlayıcı
için `Build(pass: 2)` ve `Build(pass: 0)` çıktıları turbo açıkken ve kapalıyken
**liste olarak eşit**. Karşılaştırma tek bir bayrağa değil argümanın tamamına
bakıyor.

## K4 — Küme dışında hiçbir şey değişmiyor

`Build(..., pass: 1, ...)` çıktısı, kodlayıcı başına, turbo kapalı (= bugünkü) ve
turbo açık. Kaynak 1920x1080@60, plan 1280x720@30, 1200k, iki geçiş; preset her
satırda o kodlayıcının `DefaultPreset` değeri.

| # | Kodlayıcı | Bugünkü `-preset` | Turbo açıkken `-preset` | Argümanın tamamı değişti mi |
| --- | --- | --- | --- | --- |
| 1 | `libx264` | `slow` | `veryfast` | **evet** |
| 2 | `libx265` | `slow` | `veryfast` | **evet** |
| 3 | `libvpx-vp9` | `4` | `4` | hayır |
| 4 | `libsvtav1` | `8` | `8` | hayır |
| 5 | `h264_nvenc` | `p4` | `p4` | hayır |
| 6 | `hevc_nvenc` | `p4` | `p4` | hayır |
| 7 | `h264_qsv` | `medium` | `medium` | hayır |
| 8 | `hevc_qsv` | `medium` | `medium` | hayır |
| 9 | `av1_nvenc` | `p6` | `p6` | hayır |
| 10 | `av1_qsv` | `medium` | `medium` | hayır |
| 11 | `h264_amf` | `quality` | `quality` | hayır |
| 12 | `hevc_amf` | `quality` | `quality` | hayır |
| 13 | `av1_amf` | `quality` | `quality` | hayır |

Onüç satır saydım: değişen **2**, değişmeyen **11**. "Değişti mi" sütunu tek
bayrağa değil `Build`'in ürettiği listenin tamamına bakıyor; ölçü
`Turbo_tanimayan_kodegin_ilk_gecis_argumani_birebir_ayni_kaliyor` iki listeyi
eleman eleman eşitliyor.

Değişmeyen bir satırın tam argümanı (`libsvtav1`):

```
bugün: -hide_banner -y -hwaccel auto -i kaynak.mp4 -vf scale=1280:720:flags=lanczos,fps=30 -c:v libsvtav1 -preset 8 -b:v 1200k -pass 1 -passlogfile gunluk -g 300 -svtav1-params keyint=300:scd=1 -pix_fmt yuv420p -an -f null NUL
yeni : -hide_banner -y -hwaccel auto -i kaynak.mp4 -vf scale=1280:720:flags=lanczos,fps=30 -c:v libsvtav1 -preset 8 -b:v 1200k -pass 1 -passlogfile gunluk -g 300 -svtav1-params keyint=300:scd=1 -pix_fmt yuv420p -an -f null NUL
```

Değişen iki satırdan biri (`libx264`):

```
bugün: -hide_banner -y -hwaccel auto -i kaynak.mp4 -vf scale=1280:720:flags=lanczos,fps=30 -c:v libx264 -preset slow -b:v 1200k -maxrate 1800k -bufsize 2400k -pass 1 -passlogfile gunluk -g 300 -keyint_min 30 -pix_fmt yuv420p -an -f null NUL
yeni : -hide_banner -y -hwaccel auto -i kaynak.mp4 -vf scale=1280:720:flags=lanczos,fps=30 -c:v libx264 -preset veryfast -b:v 1200k -maxrate 1800k -bufsize 2400k -pass 1 -passlogfile gunluk -g 300 -keyint_min 30 -pix_fmt yuv420p -an -f null NUL
```

Tek fark `-preset` değeri. Onüç kodlayıcının tam dökümü `.calisma/T140/tablolar.txt`
içinde üretildi; o klasör git'e girmiyor.

**Donanım varsayılmadı, ölçüldü.** `Ilk_gecis_yoluna_yalniz_iki_gecis_isteyen_kodekler_giriyor`
onüç kodlayıcının her biri için `NeedsTwoPasses(codec) == !IsHardware(codec)`
eşitliğini ve donanım kodlayıcılarının turbo kümesinde olmadığını tutuyor.

## K5 — Varsayılan kapalı

`EncodePlan.TurboFirstPass` varsayılanı `false`. Anahtara dokunulmadığında ilk
geçiş onüç kodlayıcının hepsinde son geçişin preset'ini koşmaya devam ediyor —
`Anahtara_dokunulmadiginda_ilk_gecis_son_gecisin_on_ayarini_kosmaya_devam_ediyor`.

Anahtar `[JsonIgnore]`. `PlanParser` dışarıdan yapıştırılan metinden `EncodePlan`
deserialize ediyor; ölçülmemiş bir özelliği o yoldan açılabilir bırakmak
varsayılanı kapalı tutma kararını delerdi. `Disaridan_gelen_plan_json_u_turboyu_acamaz`
bunu tutuyor: `"turboFirstPass": true` taşıyan bir JSON `false` bir plan üretiyor.
Anahtar bugün yalnız kod içinden set edilebiliyor ve **üretimde hiçbir yerden set
edilmiyor**; bu bilinçli, açma kararı ölçümün işi.

### Açılmadan önce koşturulması gereken ölçüm

Bir sonraki sözleşme şunu koşturmalı: aynı kaynak, aynı hedef boyut ve aynı plan
üzerinde `TurboFirstPass` kapalı ve açık iki tam iki geçişli koşum; ölçülecek üç
sayı **toplam duvar saati süresi**, **teslim edilen dosya boyutunun hedefe oranı**
ve **VMAF-NEG p10**. Süre kazancı yalnız ilk geçişten gelir, bu yüzden geçiş
başına süre ayrı yazılmalı. Asıl risk boyut tarafındadır: ilk geçiş ffmpeg'in
istatistik dosyasını üretiyor, son geçiş bit dağılımını o dosyadan okuyor; daha
ucuz bir analiz dağılımı bozarsa bu, kaliteden önce **hedef bandına düşme oranı**
olarak görünür. Ölçüm, süre bu projede hiç ölçülmediği ve önceki ölçümler
paylaşımlı makinede koştuğu için paylaşımsız bir makinede, kümedeki iki
kodlayıcının her biri (`libx264`, `libx265`) ve en az iki kaynak (hareketli ve
durgun) üzerinde koşmalı; `docs/olcumler/tepe-egrisi.md` düzeneği aynı biçimi
taşıyor. Kazanç ölçülene kadar varsayılan kapalı kalmalı.

## K6 — Mutasyon ızgarası

Her mutasyon tek tek uygulandı, `dotnet build -c Release --no-incremental` ile
yeniden derlendi, `dotnet test -c Release --filter "TurboFirstPassTests"`
koşturuldu, sonra `git checkout` ile geri alındı. `--no-build` kullanılmadı.
Yeşil taban: 75 ölçü.

| # | Mutasyon | Dosya | Kırmızı | Kırılan ölçüler |
| --- | --- | --- | --- | --- |
| M1 | `Build` içindeki `pass == 1` dalı geri alındı (`-preset` her geçişte `plan.Preset`) | `FfmpegArguments.cs` | 7 / 75 | `Turbo_acikken_ilk_gecis_son_gecisin_on_ayarini_kosmaz`, `Ilk_gecis_merdiveni_tavanda_kesiliyor` |
| M2 | `FirstPassPreset` içindeki `if (!turbo) return preset;` kaldırıldı | `FfmpegArguments.cs` | 2 / 75 | `Anahtara_dokunulmadiginda_ilk_gecis_son_gecisin_on_ayarini_kosmaya_devam_ediyor` |
| M3 | Tavan kıyaslaması kaldırıldı, hep tavan döndürülüyor | `FfmpegArguments.cs` | 3 / 75 | `Ilk_gecis_hicbir_zaman_son_gecisten_yavas_kosmuyor`, `Ilk_gecis_merdiveni_tavanda_kesiliyor` |
| M4 | `libsvtav1` turbo kümesine eklendi (tavan `10`) | `CodecModel.cs` | 10 / 75 | `Turbo_kumesi_tam_olarak_x264_ve_x265`, `Kume_disindaki_her_kodek_turbo_tanimiyor`, `Kumedeki_kodeklerin_merdiveni_dokuz_basamak_ve_hizlidan_yavasa`, `Bilinen_kodekler_dort_yazilim_dokuz_donanim`, `Ilk_gecis_merdiveni_tavanda_kesiliyor` |
| M5 | Anahtardaki `[JsonIgnore]` yerine `[JsonPropertyName("turboFirstPass")]` | `EncodePlan.cs` | 1 / 75 | `Disaridan_gelen_plan_json_u_turboyu_acamaz` |
| M6 | Tavan `veryfast` yerine `medium` | `CodecModel.cs` | 7 / 75 | `Turbo_acikken_ilk_gecis_son_gecisin_on_ayarini_kosmaz`, `Ilk_gecis_merdiveni_tavanda_kesiliyor` |

Altı mutasyon saydım; altısı da kırmızı verdi, hiçbiri sessiz geçmedi. Mutasyon
sürücüsü `.calisma/T140/mutasyon.py`, ham çıktılar `.calisma/T140/mut-M*.txt` —
ikisi de git'e girmiyor.

**M4'ün bir ayrıntısı:** `libsvtav1` kümeye girince
`Turbo_tanimayan_kodegin_ilk_gecis_argumani_birebir_ayni_kaliyor` ölçüsü o
kodlayıcıyı kapsamdan düşürüyor — ölçü kümedeki kodlayıcılarda erken dönüyor.
Izgarayı ayakta tutan bu değil zaten: M4'ün kırdığı beş ölçünün içinde
`Ilk_gecis_merdiveni_tavanda_kesiliyor` var ve o ölçü sabit karşılaştırmıyor,
`Build`i gerçekten koşturup ürettiği argümana bakıyor. Yani mutasyon sabit
pimlere değil davranışa çarpıyor.

M1 ile M6 aynı iki ölçüyü kırıyor: ikisi de ilk geçişin preset'ini bozuyor.
Ayrıştıkları yer kırmızının içeriği — M1'de ilk geçiş `slow`, M6'da `medium`
dönüyor.

## K7 — Verify kollarının test sayısı

`dotnet test -c Release --list-tests --filter "<kol>"`:

| Kol | Bulunan test |
| --- | --- |
| `FfmpegArgumentsTests` | 66 |
| `TurboFirstPassTests` | 75 |
| `ConversionArgumentsTests` | 7 |

Üç kol saydım, sıfır bulan kol yok. Üçü birlikte
(`"FfmpegArgumentsTests|TurboFirstPassTests|ConversionArgumentsTests"`) **148**
test buluyor; 66 + 75 + 7 = 148, kollar örtüşmüyor.

## Tam süit

`dotnet test -c Release`, iki koşum:

| Koşum | Ağaç | Başarısız | Başarılı | Atlanan | Toplam | Süre |
| --- | --- | --- | --- | --- | --- | --- |
| Taban | `main` = `b88bb66`, dal açılmadan önce | 0 | 1334 | 18 | 1352 | 20 m 39 s |
| Dal sonu | `ba1f6be` | 0 | 1408 | 19 | 1427 | 21 m 24 s |

Toplam **+75**; bu sözleşmenin getirdiği ölçü sayısı da 75, birebir tutuyor.

Bu makinede ffmpeg kurulu (`ffmpeg 9.0-full_build`), yani `[FfmpegFact]`
ölçüleri atlanmadı. Dal sonu koşumundaki 19 atlanan ölçünün tamamı — günlükte
19 `Atlandı` satırı var, saydım:

`CalibrationProbeTests.LiveEncodeTimeMatchesTheMeasuredEstimate`,
`CalibrationProbeTests.LiveFastModeLandsInsideTheBandOnTheFirstAttempt`,
`CalibrationProbeTests.LiveTimeSurvivesTheTargetsThatKeepThePlanShape`,
`ExtremeCompressionTests.LiveExtremeTargetsProduceAPlayablePicture`,
`ExtremeCompressionTests.LiveProbeMeasuresMotionWithinItsTimeBudget`,
`ExtremeCompressionTests.LiveQualityCurveShowsWhereTheCodecStopsCarryingThePicture`,
`FillBandTests.LiveFillTargetRunStaysInsideTheBand`,
`HardwareFlagTests.LiveFastRunDoesNotSpendEveryAttempt`,
`HardwareRateControlTests.LiveFastTargetsLandInsideTheBandOnTheFirstAttempt`,
`HardwareRateControlTests.LiveProcessorTargetsStillLandInsideTheBandOnTheFirstAttempt`,
`HardwareVerdictTests.LiveProbeDecidesOnThisMachine`,
`HardwareVerdictTests.TheFirstLayoutDoesNotWaitForTheProbe`,
`PerformanceCheckTests.DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu`,
`PerformanceCheckTests.YukAltindaKararHafiflemiyorMu`,
`PlaybackFrameSourceTests.Canli_kaynak_iki_paneli_besliyor`,
`PlaybackFrameSourceTests.Duraklatma_sureci_oldurmez`,
`UpdaterTests.EveryLaunchChecksAndStaysWithinTheTimeout`,
`UpdaterTests.SwitchedOffLauncherMakesNoNetworkRequestAtAll`,
`UpdaterTests.TheIncomingBinaryRenamesItselfOntoTheTargetName`.

Ondokuzunun da kapısı bir ortam değişkeni ya da canlı donanım/ağ koşulu
(`VIDSHRINK_LIVE_PROBE`, `VIDSHRINK_LIVE_SOURCE`, `VIDSHRINK_LAUNCHER_EXE`
kalıbı), ffmpeg'in yokluğu değil.

**Kapatamadığım küçük bir açık:** atlanan sayısı 18'den 19'a çıktı, yani bir ölçü
tabanda koşarken dal sonunda atlandı (1334 + 75 = 1409, teslim edilen başarılı
sayısı 1408). Hangi ölçü olduğunu **söyleyemiyorum**: taban koşumunun günlüğü
kuyruktan kesilmiş halde kaldı, 19 adın hangi 18'i olduğunu o günlükten
çıkaramıyorum. Kod tarafında bunun karşılığı yok — atlama kararı xUnit'in
öznitelik kurucusunda, ortam değişkeni ve araç varlığına bakılarak veriliyor;
bu sözleşmenin dokunduğu üç dosyanın hiçbiri o yolda değil. Yukarıdaki 19 adın
hepsi de canlı ortam kapılı. Yine de bu bir çıkarım, ölçüm değil.

**Sonradan kapandı (T0 denetimi):** bağımsız denetçi ölçtü — bu dalda
`Skip`/`Conditional`/`Trait` eşleşmesi 0 ve verify kollarında `Atlanan: 0`. Kayan
ölçü `VIDSHRINK_LIVE_*` korumalı, makine durumuna bağlı; dalla ilgisi yok.

## CI

| Alan | Değer |
| --- | --- |
| Koşum kimliği | `33652799954` |
| Bağlantı | https://github.com/Teknesyum/VidShrink/actions/runs/33652799954 |
| Commit | `ba1f6be` (K5, dalın son kod commit'i) |
| Sonuç | **success** |
| Süre | 2026-09-02 16:06:04Z → 16:28:44Z |

`.github/workflows/ci.yml` `docs/**` ve `**/*.md` yollarını tetikleyicinin
dışında tutuyor, bu yüzden bu raporu getiren commit yeni bir CI koşumu
başlatmayacak. Kodun tamamı yukarıdaki koşumda yeşil.

## Sınırlar ve T0'a sorular

1. **Merdiven yönü kümeye bağlı.** `FirstPassPreset` "hızlı" ile "merdivende daha
   küçük indeks"i eşitliyor. Bu, kümedeki iki kodlayıcı için doğru; `libvpx-vp9`
   ve `libsvtav1` merdivenleri ters yönlü. Kümeye o kodlayıcılardan biri
   eklenirse kural sessizce yanlış yöne çalışır. Bugünkü koruma
   `Turbo_kumesi_tam_olarak_x264_ve_x265` ölçüsü: küme değişirse kırmızı olur ve
   ekleyeni buraya getirir. Yön bilgisini de veriye taşımak `CodecModel`de bir
   alan daha ister. **T0'a soru:** bu şimdi mi yapılsın, yoksa üçüncü kodlayıcı
   kümeye girdiğinde mi?
2. **Anahtarı kimse açmıyor.** `TurboFirstPass` üretim yollarının hiçbirinden set
   edilmiyor; özelliği bir kullanıcı ya da bir ayar açamaz. Açma yolu
   `CompressionStrategy` / `PlanCalculator` / ayarlar sekmesi tarafında ve
   hiçbiri `owns` listesinde değil, bu yüzden dokunulmadı. **T0'a soru:** açma
   yolu K5'in istediği ölçümden önce mi açılsın, sonra mı?
3. **`docs/taramalar/handbrake.md` `EncoderCapabilities.cs` diyor.** Sözleşme
   `CodecModel` dedi ve `owns` da öyle; sapma bilerek yapıldı, tarama dosyasına
   dokunulmadı (o dosya `owns` dışında).
4. **HandBrake'in turbo geçişte hangi x264 parametrelerini kullandığı bu oturumda
   kaynaktan okunmadı.** Kümenin x264 + x265 olduğu bilgisi deponun kendi tarama
   dosyasından alındı (`docs/taramalar/handbrake.md`, `turbo_supported`). Tavanın
   `veryfast` olması HandBrake'ten değil, sözleşme K3'ün verdiği örnekten ve
   yukarıdaki gerekçeden geliyor.

### T0'ın cevapları (mühürleme denetimi)

1. **Merdiven yönü:** şimdi taşınmayacak. İki üyeli kümede veri yapmak erken
   soyutlama; üçüncü kodlayıcı kümeye girdiğinde taşınacak, borç olarak yazıldı.
2. **Anahtarı üretimde açan yok:** tespit doğru, bu sözleşmenin borcu değil.
   Açma yolu için **T146** açıldı; `PlanCalculator` üzerinde başka aktif sözleşme
   olduğu için bekliyor.
3. **`CodecModel` seçimi doğru:** sözleşmenin `owns` listesi bağlayıcı, tarama
   belgeleri yol gösterici.
4. Değişiklik yok.
