# Üretim yolu — T146

Üç yetenek yazılmış, ölçülmüş, mutasyonla pimlenmiş ve mühürlenmişti; hiçbiri üretim
yolunda koşmuyordu. Bu belge üçünün bağlanmasını, bağlamanın ne değiştirdiğini ve
bağlamanın gerçekten ölçüldüğünü kayda geçirir.

Dal: `T146-uretim-yolu`. Ölçümler bu dalın çalışma ağacında, `.calisma/T146/` altında
koştu; `.calisma/` git'e girmediği için ham çıktılar bu belgeye gömüldü.

Satır numaraları bu daldaki bugünkü hallerinden greple bulundu, sözleşmeden
kopyalanmadı.

## K1 — üç körlük önce kırmızı ölçüldü

Kusur commit'i `e1ec2db`. Üretimin yoklamaya verdiği değer adlandırılmış birer dikişe
çıkarıldı ve dikişlere **bugünkü** değerleri verildi (`null`, `null`,
`QualityMeasurement.Instance`); davranış değişmedi, ölçüler kırmızı koştu.

Her ölçü bir davranış farkı arar. `Assert.NotNull(map)` gibi bir ölçü yok: karşılaştırma
iki yerleşim listesi, iki en kötü birim ve iki ffmpeg argümanı arasındadır.

```
  Başarısız VidShrink.Tests.UretimYoluTests.KalibrasyonYerlesimiUretimdeSahneHaritasiniGoruyor [8 ms]
 uretim  : [39,667 119 198,333]
 sabit   : [39,667 119 198,333]
  Başarısız VidShrink.Tests.UretimYoluTests.KaliteOlcumuUretimdeSahneHaritasiniGoruyor [8 ms]
 uretim  : enkotu=55 at=2 birim=2
 sabit   : enkotu=55 at=2 birim=2
  Başarısız VidShrink.Tests.UretimYoluTests.HizliKipteIlkGecisSonGecistenHizliKosuyor [8 ms]
 kodek=libx264 kip=2pass ilk=slow son=slow
Başarısız! - Başarısız:     3, Başarılı:     2, Atlanan:     0, Toplam:     5
```

Üç satırın üçü de "üretim" ile "sabit"in **aynı** olduğunu gösteriyor: harita üretilip
atılıyordu, turbo bayrağı hiç kurulmuyordu.

## K2 — harita kalibrasyon yoklamasına bağlandı

Commit `5fc0fbe`. Bugünkü çağrı satırı, `src/VidShrink.App/MainWindow.axaml.cs`:

```
1815:    public static SceneMap? CalibrationScenes(SceneMapAttempt? attempt) => attempt?.Map;
1903:                var calibrated = await CalibrationProbe.RunAsync(info, draft, profile, speed, cts.Token, CalibrationScenes(_sceneMap));
```

`CalibrationProbe.RunAsync`'in altıncı parametresi `SceneMap? scenes = null` idi ve üretim
onu hiç doldurmuyordu. Yedek kol silinmedi: tarama başarısızsa `CalibrationScenes` `null`
döner ve yoklama eşit aralı ızgarada kalır.

## K3 — kalite ölçümünün en kötü birimi haritayı taşıyor

Commit `3913f37`. İki şey gerekti.

**Sıra.** Harita, kalite ölçümünden **sonra** kuruluyordu; ölçere verilecek harita o
noktada yoktu. Sıra ters çevrildi:

```
1886:            _sceneMap = await EncodeRunner.TryBuildSceneMapAsync(info, ct: cts.Token);
1890:            var profile = await ProbeWithMeasuredQualityAsync(info, speed, ProbeMeter(QualityScenes(_sceneMap)), cts.Token);
```

Bu sıra `HaritaKaliteOlcumundenOnceKuruluyor` ölçüsüyle kaynaktan pimlendi.

**Ölçer.** `IQualityMeasurement.MeasureWindowAsync` harita parametresi taşımıyor ve
`src/VidShrink.Core/IQualityMeasurement.cs` bu sözleşmenin `owns` kümesinin dışında. İmza
değiştirilmedi; harita gerçeklemenin içinde taşındı:

```
1821:    public static SceneMap? QualityScenes(SceneMapAttempt? attempt) => attempt?.Map;
1829:    public static IQualityMeasurement ProbeMeter(SceneMap? scenes)
             => scenes is null ? QualityMeasurement.Instance : new SceneAwareQualityMeasurement(scenes);
```

`SceneAwareQualityMeasurement`, `QualityMeasurement` ile aynı ölçümü yapar, tek farkı
`QualityMeter.MeasureWindowAsync`'in harita taşıyan aşırı yüklemesine gitmesidir.
**Bu bir kopya:** yaklaşık yirmi satır `QualityMeasurement`'tan tekrarlanıyor. Tekrarın
sebebi imza sınırıdır; borç olarak açıkta bırakıldı (aşağıda "Sınırda durulan yer").

Harita gelmediğinde `QualityMeasurement.Instance` dönüyor — bugünkü yol silinmedi.

## K4 — turbo anahtarı için karar

### Karar

**Yalnız belirli plan sınıfında açıldı:** `SpeedMode.Fast` **ve** `libx265`.
`src/VidShrink.Core/PlanCalculator.cs`:

```
415:        TurboFirstPass = options.SpeedMode == SpeedMode.Fast && TurboFirstPassIsSafe(codec),
429:    private static bool TurboFirstPassIsSafe(string codec)
             => codec.Equals("libx265", StringComparison.OrdinalIgnoreCase);
```

### Neden varsayılan `true` değil

Ölçüm turbonun libx264'te **çıktıyı yok ettiğini** gösterdi. İki geçişli x264'te ikinci
geçiş birinci geçişin `weightp` ayarına uymak zorundadır: `veryfast` weightp=1, `slow`
weightp=2 koşar. İkinci geçiş kodlayıcıyı hiç açmıyor:

```
[libx264 @ ...] different weightp setting than first pass (2 vs 1)
[vost#0:0/libx264 @ ...] Error while opening encoder - maybe incorrect parameters such as bit_rate, rate, width or height.
[out#0/mp4 @ ...] Nothing was written into output file, because at least one of its streams received no packets.
```

İki klipte de tekrarlandı; çıktı sıfır bayt. `CodecModel.TurboFirstPassCeilings` hem
`libx264` hem `libx265` için tavan tanımlıyor — yani yetenek yazıldığında x264 da kapsam
içindeydi ve bu kol hiç koşturulmamış. `CodecModel.cs` `owns` dışında olduğu için tavan
tablosu değiştirilmedi; plan turboyu x264'te açmıyor.

Kullanıcıya ayar olarak açılmadı: ayarın doğru cevabı ölçümden çıkıyor ve ölçüm
"x265'te aç, x264'te açma" diyor — kullanıcıya sorulacak bir şey kalmıyor.

### Kalite ölçümü

Başsız, kısa kesitle, `.calisma/T146/k4/` altında. Kaynak
`.calisma/kaynak/parca-1.mkv` (HEVC 10 bit, 1080p60); iki kesit **aynı komutla**
çıkarıldı (`-t 10`, 1280x720, 30 fps, sessiz, libx264 `-crf 12`), yalnız başlangıç
saniyeleri farklı: klip 5 (27,2 MB, hareketli) ve klip 35 (7,1 MB, durgun).

Her kol iki geçişli ABR, hedef 1500 kbit/sn; iki kol arasındaki **tek** fark birinci
geçişin `-preset`'idir. VMAF ffmpeg `libvmaf` (`vmaf_v0.6.1`), çıktı kendi referans
klibine karşı. Toplam 10 kodlama (20 geçiş) ve 8 VMAF koşumu.

Ham çıktı (`sonuc2.tsv`):

```
klip	kodek	kol	gecis1_on	gecis2_on	g1_ms	g2_ms	toplam_ms	g2_exit	boyut	vmaf
5	libx264	kapali	slow	slow	998	1272	2270	0	1884400	68.623
5	libx264	acik	veryfast	slow	686	88	775	127	0	-
5	libx264	acik_weightp	veryfast	slow	551	1281	1832	0	1888380	67.407
5	libx265	kapali	slow	slow	6796	6859	13655	0	1851262	77.629
5	libx265	acik	veryfast	slow	2227	6667	8894	0	1870959	77.151
35	libx264	kapali	slow	slow	589	1076	1665	0	1844124	93.578
35	libx264	acik	veryfast	slow	476	75	551	127	0	-
35	libx264	acik_weightp	veryfast	slow	374	1007	1381	0	1842279	93.173
35	libx265	kapali	slow	slow	6194	4785	10980	0	1868020	94.199
35	libx265	acik	veryfast	slow	1413	4488	5901	0	1882818	94.247
```

Girdi × eski çıktı × yeni çıktı:

| girdi | eski (turbo kapalı) | yeni (turbo açık) | fark |
|---|---|---|---|
| klip 5, libx265 | 13655 ms · 1 851 262 B · VMAF 77,629 | 8894 ms · 1 870 959 B · VMAF 77,151 | süre −34,9% · boyut +1,06% · VMAF −0,478 |
| klip 35, libx265 | 10980 ms · 1 868 020 B · VMAF 94,199 | 5901 ms · 1 882 818 B · VMAF 94,247 | süre −46,3% · boyut +0,79% · VMAF **+0,048** |
| klip 5, libx264 | 2270 ms · 1 884 400 B · VMAF 68,623 | **çıktı yok** (0 bayt) | kabul edilmedi |
| klip 35, libx264 | 1665 ms · 1 844 124 B · VMAF 93,578 | **çıktı yok** (0 bayt) | kabul edilmedi |

`acik_weightp` kolu, iki geçişe de `-x264-params weightp=2` verilen kontrol koludur;
x264'ün duvarının yalnız `weightp` olduğunu gösterir (klip 5: 2270→1832 ms, VMAF
68,623→67,407; klip 35: 1665→1381 ms, VMAF 93,578→93,173). Bu kol **üretime girmedi** —
ikinci geçişin argümanına `weightp` eklemek `FfmpegArguments.cs`'i gerektirir, o dosya
`owns` dışında.

Kullanıcının çıktı dosyası libx265 hızlı planlarda değişiyor: aynı hedef bit hızında
dosya yaklaşık %1 büyüyor, VMAF bir klipte 0,478 düşüyor, diğerinde 0,048 yükseliyor.
Yönü kötü olan girdi budur: **hareketli kaynakta VMAF yarım puana yakın düşüyor**;
karşılığı toplam sürede üçte birden fazla kısalma.

Ölçüm koşumunun bir sapması var ve gizlenmiyor: A/B düzeneği üretimin geçiş yapısını ve
ön ayar merdivenini taşır, ama `-maxrate`/`-bufsize` değerlerini üretimin
`PeakRateFactor` hesabından değil sabit çarpanlardan (1,5× / 3×) alır. Sapma iki kola da
aynı uygulandığı için karşılaştırmayı kaydırmaz, mutlak VMAF değerlerini kaydırabilir.

## K5 — gerileme tabloları

Üç tablo ölçü olarak yazıldı (`TabloKalibrasyonYerlesimi`, `TabloKaliteEnKotuBirim`,
`TabloTurboIlkGecis`); her biri satırlarını basar ve değişen satır sayısının ne sıfır ne
de tümü olduğunu pimler. Aşağısı ham `dotnet test` çıktısıdır.

### Kalibrasyon yerleşimi

```
 240sn kalite / 8 sahne     | eski [39,667 119 198,333] | yeni [14 44 104 134 224] | DEGISTI
 240sn hizli  / 8 sahne     | eski [59,5 178,5] | yeni [14 44] | DEGISTI
 60sn hizli   / 6 sahne     | eski [14,5 43,5] | yeni [4 14] | DEGISTI
 20sn kalite  / 4 sahne     | eski [3 9 15] | yeni [1,5 6,5 11,5 16,5] | DEGISTI
 240sn kalite / harita yok  | eski [39,667 119 198,333] | yeni [39,667 119 198,333] | AYNI
 degisen satir: 4/5
```

**Yönü kötü olan girdi:** `60sn hizli / 6 sahne` satırı. Pencereler `[4 14]`'e, yani
klibin ilk çeyreğine yığılıyor; eski eşit aralı yerleşim `[14,5 43,5]` klibi baştan sona
tarıyordu. T142 bunu borç olarak kaydetmişti; harita üretime bağlandığı için borç
bugünden itibaren **canlı**. Bu sözleşme bandı genişletmiyor, eşiği gevşetmiyor ve
yerleşim kuralını değiştirmiyor — `SceneMap` ve `CalibrationProbe` `owns` dışında.

### Kalite ölçümünde en kötü birim

```
 dip 2,5-5,5 / kesik 2,5;5,5 | eski 55@2/2sn | yeni 40@2,5/3sn | DEGISTI
 dip 2,5-5,5 / kesik 2;4;6;8 | eski 55@2/2sn | yeni 55@2/2sn | AYNI
 duz 100     / kesik 2,5;5,5 | eski 100@0/2sn | yeni 100@0/2,5sn | DEGISTI
 dip 2,5-5,5 / harita yok    | eski 55@2/2sn | yeni 55@2/2sn | AYNI
 degisen satir: 2/4
```

Sahne sınırları sabit iki saniyelik ızgaraya denk düştüğünde sonuç değişmiyor (ikinci
satır). Düz skorlarda en kötü değer aynı kalıyor, yalnız birimin uzunluğu değişiyor
(üçüncü satır) — rapora giren sayı 100'de kalıyor.

### Turbo ilk geçiş

```
 hizli  / azami sikistirma | libx265     2pass       son=slow   | eski ilk=slow      | yeni ilk=veryfast  | DEGISTI
 hizli  / uyumlu           | libx264     2pass       son=slow   | eski ilk=slow      | yeni ilk=slow      | AYNI
 kalite / azami sikistirma | libsvtav1   2pass       son=6      | eski ilk=6         | yeni ilk=6         | AYNI
 kalite / uyumlu           | libx264     2pass       son=slow   | eski ilk=slow      | yeni ilk=slow      | AYNI
 hizli  / donanim var      | av1_nvenc   2pass       son=p5     | eski ilk=p5        | yeni ilk=p5        | AYNI
 degisen satir: 1/5
```

Beş satırın dördü değişmiyor. Son geçişin ön ayarı hiçbir satırda değişmiyor: turbo
yalnız birinci geçişi ilgilendirir.

## K6 — mutasyon ızgarası

Her mutasyon tek bir bağlamayı geri alır. Her kol `dotnet build -c Release
--no-incremental` ile yeniden derlendi (`--no-build` kullanılmadı), sonra
`dotnet test -c Release --no-build --filter "UretimYoluTests"` koştu. Ham çıktı:

```
M1 CalibrationScenes -> null -> KIRILDI (toplam 12) ['KalibrasyonYerlesimiUretimdeSahneHaritasiniGoruyor', 'TabloKalibrasyonYerlesimi']
M2 QualityScenes -> null -> KIRILDI (toplam 12) ['KaliteOlcumuUretimdeSahneHaritasiniGoruyor', 'TabloKaliteEnKotuBirim', 'UretimOlceriHaritayiOlcumeTasiyor']
M3 ProbeMeter -> hep QualityMeasurement.Instance -> KIRILDI (toplam 12) ['UretimOlceriHaritayiOlcumeTasiyor']
M4 TurboFirstPass -> false -> KIRILDI (toplam 12) ['HizliKipteIlkGecisSonGecistenHizliKosuyor', 'TabloTurboIlkGecis']
M5 harita kurulumu kalite olcumunden sonraya -> KIRILDI (toplam 12) ['HaritaKaliteOlcumundenOnceKuruluyor']
M6 TurboFirstPassIsSafe -> libx264 de acik -> KIRILDI (toplam 12) ['Libx264HizliKiptedeTurboyaAcilmiyor']
```

Altı mutasyonun altısı da kırıldı ve **hiçbiri başka bir bağlamanın ölçüsünü
kırmadı**: kalibrasyon mutasyonu (M1) turbo ve kalite ölçülerini yeşil bıraktı, turbo
mutasyonları (M4, M6) harita ölçülerini yeşil bıraktı, sıra mutasyonu (M5) yalnız sıra
pimini kırdı. M2 üç ölçü kırar çünkü üçü de aynı bağlamanın — üretimin ölçere verdiği
harita — sonucunu okur.

## K7 — koşum kayıtları

`--list-tests` ile kol başına bulunan test sayısı:

| kol | bulunan test |
|---|---|
| `UretimYoluTests` | 12 |
| `PlanCalculatorTests` | 32 |
| `MainWindowPlanTests` | **0** |

`MainWindowPlanTests` **hiçbir teste denk gelmiyor**; bu adda bir sınıf depoda yok.
`dotnet test` bu kolda çıkış 0 ile sessizce geçer. Sözleşmenin `verify` satırı bu
dosyadan değiştirilmedi — `.claude/relay/**` `owns` dışında.

Koşum: `dotnet test -c Release --no-build --filter
"UretimYoluTests|PlanCalculatorTests|MainWindowPlanTests"` sonucu
`Başarısız: 0, Başarılı: 44, Atlanan: 0, Toplam: 44` (12 + 32 + 0).

Ek koşum `dotnet test -c Release --filter "OluUyeTests"` sonucu
`Başarısız: 0, Başarılı: 11, Toplam: 11`. Ölçünün bastığı sayılar:
`uye: 129  bu dosyada adi gecmeyen: 93  pimlenen: 31` ve
`sifir tuketici: 26  hic kullanilmayan: 5`. **Pim 26'da kaldı**, düşmedi;
`tests/VidShrink.Tests/OluUyeTests.cs` bu dalda `origin/main` ile bit bit aynı.
(31, `Pinned` dizisindeki bulgu sayısıdır — 22 borç, 9 meşru — 26 ile karıştırılmamalı.)

CI: `gh run list --branch T146-uretim-yolu` koşum kimliği **33740299463**.

## Sınırda durulan yer

`IQualityMeasurement.MeasureWindowAsync` harita parametresi taşımıyor.
`src/VidShrink.Core/IQualityMeasurement.cs` `owns` kümesinde değil, imza
değiştirilmedi. Sonuç: harita `MainWindow.SceneAwareQualityMeasurement` içinde
taşınıyor ve o sınıf `QualityMeasurement`'ın gövdesini tekrarlıyor. Tekrarı kaldırmanın
yolu arayüze harita parametresi eklemektir; bu ayrı bir sözleşmedir.

İkinci sınır: `EncodePlan.TurboFirstPass` `[JsonIgnore]` işaretli. `PlanParser` ile
dışarıdan yapıştırılan plan JSON'ında bayrak taşınmaz, o yolda turbo hep kapalıdır.
`EncodePlan.cs` `owns` dışında; davranış değiştirilmedi.

Üçüncü sınır: libx264'ün turbo kolu ölçüldü ve çalışıyor — yeter ki iki geçiş aynı
`weightp` değerini koşsun. Bunu üretime almak `FfmpegArguments.Build`'in ikinci geçiş
argümanlarına dokunmayı gerektirir; o dosya `owns` dışında, dokunulmadı.
