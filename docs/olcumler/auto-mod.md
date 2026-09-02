# Auto mod — hiçbir ayar bilmeyen kullanıcı ne alıyor

T102. Ölçüm tarihi 2026-09-02 (K1-K6 tur 1; anahtar kare hizalama A/B'si tur 3,
aynı gün ve aynı makinede). Dal `T102-auto-mod`, taban `main@2b4477c`.

Bu belge ölçer, düzeltmez. İçindeki her sayı bu makinede koşturulmuş bir ölçümden
gelir. Ölçülmemiş olan yerde açıkça **ölçülmedi** yazar.

## Ölçüm düzeneği

| | |
|---|---|
| Kaynak | `parca-2.mkv` — 1920x1080@60, HDR10 (PQ / bt2020nc), 60,442 s, 110,56 MiB, AAC 128k stereo |
| Kaynağın yeri | `.calisma/kaynak/parca-2.mkv` (ortak); ölçüm için `.calisma/t102/gui/` altına birebir kopyalandı |
| Hedef boyut | 16 MB — uygulamanın kendi varsayılanı, kullanıcı dokunmadan kutuda yazan sayı |
| Sıkıştırma rejimi | 110,56 / 16 = 6,91 → `Aggressive` (`CompressionStrategy.cs:50-52`) |
| Kalite ölçüsü | VMAF-NEG (`vmaf_v0.6.1neg`) ortalama / harmonik / p10 |
| Ölçüm komutu | Bench'in kendi düzeni: test lanczos ile 1920x1080'e ölçeklenir, sonra kaynağa karşı `libvmaf` (`tools/VidShrink.Bench/Program.cs:836-848`) |

Makine paylaşımlıydı: ölçüm boyunca başka ajanlar da koşuyordu (sayı bana
bildirildi, kendim doğrulamadım). **Süreler ölçülmedi** — duvar saati bu koşullarda
anlamlı değil. Tur 2'de burada duran "kalite sayıları yükten etkilenmez" cümlesi
**geri çekildi** — ölçülmemişti. K3/K4 koşumları iş parçacığını sabitlemeden koştu.

Tur 3'te eklenen koşumlar `-threads 4` + `svtav1-params lp=4` ile sabitlendi
(`tools/auto-mod-olcumu/hizalama.sh`). Bu, ölçülmemiş iddianın yerine tek bir ölçülen
sayı koyuyor: `y1-g300-izgara`, `e2-gop300` ile birebir aynı ayarların iş parçacığı
sabitlenmiş hâli. Dosya boyutu **%0,79** oynuyor (11 903 000 → 11 809 579 bayt), puan
ise **0,003** (ortalama 94,617 → 94,614; p10 94,868 → 94,870). Yani sabitlenmemiş iş
parçacığı sayısının bu ölçüdeki payı virgülden sonra üçüncü hanede. Tek ayarda, tek
kaynakta ölçüldü; genel bir iddia değil.

**Ölçüm düzeneği nerede.** Auto modun planı bench ile alınamadığı için (kusur 1)
`.calisma/` altında tek seferlik başsız bir sonda yazıldı: uygulamanın kendi yolunu
birebir izler — `FfprobeClient.ProbeAsync` → `ComplexityProbe` → iki turlu
`CalibrationProbe` → `PlanCalculator.BuildDetailed` (`Codec = Auto`) → `EncodeRunner`.
Tablolardaki sayılar VMAF JSON'larından mekanik olarak üretildi, elle yazılmadı.
Üretici betikler `tools/auto-mod-olcumu/` altında, `main`de. Kodlama çıktıları
(mkv/mp4, 266 MB) `.calisma/t102/` altında ve git'e sızmıyor; silinmeleri T0'a kalıyor.

**Ham VMAF çıktısı korundu.** Bu belgedeki her VMAF sayısını üreten kare kare JSON
`tools/auto-mod-olcumu/vmaf/*.json.gz` altında, git'te — on bir koşum
(sıkıştırılmamış 11 × 2,6 MB depoya konmayacak kadar büyüktü; gzip ile toplam 2 288 809 bayt). `gunzip -k tools/auto-mod-olcumu/vmaf/*.gz`
`tablolar.py`'nin beklediği dizini birebir geri veriyor. Kodlama çıktıları silinse bile
tablolar bu arşivden yeniden üretilebilir; tur 3'te K3 ve K4'ün sekiz sayısı bu
arşivden yeniden hesaplandı ve birebir tuttu. Bu ölçümü **bench'ten** tekrarlanabilir
kılmak ayrı bir şey ve kusur 1'in düzeltilmesine bağlı.

### Harmonik ortalama bu ölçümde kullanılamaz — sebebi ölçüldü

Sözleşme üç metrik istiyor. Üçüncüsü, harmonik ortalama, bu kaynakta **satırlar arası
karşılaştırma için geçersiz**. Neden geçersiz olduğu tahmin değil, ölçüldü.

VMAF-NEG, SVT-AV1 çıktılarımızın **26 karesinde 1 puanın altında** kalıyor;
bunların **25'i tam 0**. Kareler: 1699, ve 3385-3410 aralığındaki 25 kare. Blok
kesintisiz değil — 3409. kare aralığın içinde ama 12,38 alıyor, 3406. kare ise
0,946 ile eşiğin hemen altında. Harmonik ortalama `n / Σ(1/max(x,1))` olduğu için
bu 26 kare sayıyı 94,5'ten 56,3'e çekiyor.

Dağılım kodlayıcıya göre keskin biçimde ikiye ayrılıyor:

| satır | kodek | 1 puan altı kare | bunlardan tam 0 | auto'nun 26 karesiyle örtüşme |
|---|---|---|---|---|
| auto | libsvtav1 | 26 | 25 | — |
| auto-olceksiz | libsvtav1 | 26 | 25 | 26 / 26 |
| e1-preset4 | libsvtav1 | 26 | 25 | 26 / 26 |
| e2-gop300 | libsvtav1 | 26 | 25 | 26 / 26 |
| e3-olcek810 | libsvtav1 | 26 | 25 | 26 / 26 |
| uzman-biz | libsvtav1 | 26 | **24** | 26 / 26 |
| uzman-handbrake | x265 | **0** | 0 | 0 / 26 |

Birebir aynı olan **kare kümesi**: altı AV1 koşumunun 1 puan altı kare listesi
arasındaki simetrik fark boş — aynı 26 kare numarası, istisnasız. Tam 0 sayısı ise
24 ile 25 arasında oynuyor: her koşumda 3406. kare eşiğin hemen altında kalıyor
(0,76-0,96), `uzman-biz`de ayrıca 3389. kare 0,133 alıyor. Ayar (preset, `-g`,
çözünürlük) kümeyi değiştirmiyor, yalnız sıfırın ne kadar dibine inildiğini
değiştiriyor. İki x265 koşumunda kümenin tamamı 96-100 arasında.

**Bu kareler bozuk değil.** Aynı karelerde auto'nun kaynağa karşı PSNR'ı
**46,30-49,34 dB** (3380-3415 penceresi ölçüldü, etkilenen 25 kare) — mutlak
olarak yüksek kalite. Parlaklık da kaynakla birebir örtüşüyor (`YAVG` 352,5 → 339,1;
kaynakta 352,3 → 339,0). HandBrake aynı karelerde 96-100 alıyor. Yani ortada görüntü
çöküşü yok; VMAF-NEG bu AV1 dosyalarında bu karelerde yapay olarak sıfırın dibine
iniyor. Ölçüm düzeninin `scale` adımı da sebep
değil: aynı çift ölçekleme olmadan yeniden ölçüldü, sonuç birebir aynı.

Sonuç, iki yönlü:

1. **Bu belgede** satırlar arası farkı **ortalama ve p10** taşır. Harmonik sütunu
   sözleşme gereği tabloda duruyor ama okunmamalı; yanında 1 puan altı kare sayısı var.
2. **Bu bir kusur** (kusur 4). Depo'nun kendi ölçüm aracı harmonik ortalamayı
   raporluyor; bu kaynakta AV1 ile x265 arasında **39 puanlık** bir fark üretirdi ve
   o farkın PSNR'a göre karşılığı yok. Kusurun kendisi burada değil, **T106**
   sözleşmesinde ele alınıyor; bu belge yalnız ölçüp adlandırıyor.

---

## K1 — Auto modun bugün aldığı kararlar

Kullanıcı dosyayı sürükleyip "Küçült"e bastığında, onun adına alınan kararlar.
"Görüyor mu" sütunu Küçült sekmesindeki durumu anlatır.

| # | Karar | Auto'nun verdiği | Nereden (`dosya:satır`) | Görüyor mu | Değiştirebilir mi |
|---|---|---|---|---|---|
| 1 | Hedef boyut | **16 MB** (kaynak 16 MB'ın altındaysa `max(1, kaynak/2)`) | `MainWindow.axaml.cs:41`, `:1303-1307`; kutu `MainWindow.axaml:299-300`; kalıcı ayar `UpdateCheck.cs:402` | Evet, kutuda yazıyor | Evet |
| 2 | Amaç | **Paylaşım** (`Intent.Sharing`) → şeffaflık CRF'ine −3 kaydırma | `MainWindow.axaml:383` `SelectedIndex=1`; `UpdateCheck.cs:404`; etki `CompressionStrategy.cs:92-97` | Evet, açılır kutu | Evet |
| 3 | Kalite hedefi (1-100) | **60** kayıtlı varsayılan. Dosya yüklenince kutunun üzerine, geçerli hedef boyutun tahmini puanı yazılıyor — bu kaynakta **87,1**. Denetim `PlanCalculator.BuildDetailed`'e girmiyor; yalnız `TargetMbForQuality` ile puandan hedef MB üretiyor, yani 1. satırın ters yönden sorulmuş hâli | `MainWindow.axaml:369` `Value=60`; `UpdateCheck.cs:403`; geri yazma `MainWindow.axaml.cs:2080-2091`; tüketici `PlanCalculator.cs:444` | Evet, kaydırıcı + kutu | Evet |
| 4 | Kodek tercihi | **Auto** → rejimden türer: Light/Balanced → `Compatible`, Aggressive/Extreme → `MaxCompression` | `MainWindow.axaml:398` `SelectedIndex=0`; `CompressionStrategy.cs:58-63` | Kutu "Auto" der; **hangi kodeğe döndüğünü söylemez** | Evet (Uyumlu / En küçük) |
| 5 | Somut video kodeği | Bu kaynakta **`libsvtav1`** (bulunamazsa `libx265`); Compatible yolunda `libx264` | `PlanCalculator.cs:757-777`; ölçülen çıktı: `av1` | Hayır | Dolaylı (4 üzerinden) |
| 6 | Kodlayıcı preset'i | **`6`** (libsvtav1). Yazılım x264/x265'te `slow`, donanımda `p4`/`p6`/`medium`/`quality` | `PlanCalculator.cs:812-826`; donanım tabloları `FfmpegArguments.cs:19-51` | Hayır | **Hayır** — arayüzde hiç yok |
| 7 | Oran kipi | **2-pass VBR**, `-b:v 2026k`. CRF'e ancak bütçe CRF'i şeffaflık tavanının altında kalırsa geçilir | `PlanCalculator.cs:242-310`; argüman `FfmpegArguments.cs:146-159` | Hayır | Hayır |
| 8 | Doldurma politikası | **`FillTarget`** — bandı doldurmak için CRF düşürülür / 2-pass'e geçilir. Band 16 MB'da `[15,20 – 16,00]`, sert taban `14,40` | `PlanCalculator.cs:15`, `:25-32`; `MainWindow.axaml:439` | Evet, ama adı motor jargonu | Evet |
| 9 | Çözünürlük | **1920x1080 korundu.** İzin var (kutu açık) ama arama 1,00 ölçeği seçti; izin verilseydi taban 0,20 / 180 px | `PlanCalculator.cs:622-670`; `CompressionStrategy.cs:65,85-90`; kutu `MainWindow.axaml:406` | Kutu "düşürülebilir" der; **sonucun ne olduğunu plan satırı dışında söylemez** | Kısmen (yalnız izin verir/vermez) |
| 10 | Kare hızı | **60 fps korundu.** Aggressive rejimde düşürmeye izin var, taban 10 fps | `PlanCalculator.cs:672-691`; `CompressionStrategy.cs:67`; kutu `MainWindow.axaml:414` | Aynı | Kısmen |
| 11 | Ses kodeği | **`aac`**, her zaman yeniden kodlanır — kopyalama yolu yok | `PlanCalculator.cs:810`, `:328` | Hayır | **Hayır** |
| 12 | Ses bit hızı | **128k stereo** (mono 96k, Arşiv 160/112); kaynak bit hızı ve rejim payı (`Aggressive` → toplamın %18'i) tavan; 56k altında zorla mono | `PlanCalculator.cs:724-755`; `CompressionStrategy.cs:70-76` | Hayır | **Hayır** |
| 13 | HDR politikası | **Koru.** Kodeğin 10-bit yolu yoksa sessizce `tonemap`e düşer (hable, npl=100, bt709) | `PlanCalculator.cs:14`; `HdrResolver.cs:14-52` | Yalnız kaynak HDR'ken görünen kutu (`MainWindow.axaml.cs:1566`) | Evet, kaynak HDR ise |
| 14 | Piksel biçimi | **`p010le` istenir** (yoklama sırası `p010le` → `yuv420p10le`, `EncoderCapabilities.cs:125-131`). ffmpeg bunu sessizce `yuv420p10le`'ye çevirir — teslim edilen dosyada ölçülen biçim `yuv420p10le` | `HdrResolver.cs:51`; `FfmpegArguments.cs:163` | Hayır | Hayır |
| 15 | Anahtar kare aralığı | **`-g 120`** = `round(fps × 2)`, yani sabit 2 saniye | `FfmpegArguments.cs:162` | Hayır | **Hayır** |
| 16 | Psychovisual / tune | **`-svtav1-params tune=0:enable-variance-boost=1:variance-boost-strength=2`** (x265'te `psy-rd=2:psy-rdoq=1:aq-mode=2`, nvenc'te `-spatial-aq 1 -temporal-aq 1`) | `FfmpegArguments.cs:254-278` | Hayır | Hayır |
| 17 | Hız kipi (GPU) | **`Quality`** (yazılım). Kutu ilk açılışta kilitli gelir; donanım yoklaması karar verip ayara yazar ve bir daha üstüne yazmaz | `MainWindow.axaml:422` `IsEnabled=False`; yoklama `MainWindow.axaml.cs:1206-1262`; kural `HardwareVerdict.cs:66-101`; ayar `UpdateCheck.cs:399-400` | Kutu görünür ama kararı program verdi | Evet, karar verildikten sonra |
| 18 | Kap ve çıktı adı | **`mp4`**, `<ad>_shrunk.mp4`, kaynağın yanına, `-movflags +faststart` | `MainWindow.axaml.cs:1983-1994`, `:2356`; `FfmpegArguments.cs:185` | Hayır (yol sonuçta görünür) | **Hayır** |
| 19 | Çözme ivmesi | **`-hwaccel auto`** her girişte | `FfmpegArguments.cs:123` | Hayır | Hayır |
| 20 | Yeniden deneme | En çok **3 deneme**; hedefi aşan çıktı asla teslim edilmez, band altı kalan bir kez (ölçüm varsa iki kez) tekrarlanır | `EncodeRunner.cs:39-40`, `:71-167` | Kısmen (sonuç metninde deneme sayısı) | Hayır |

**Sayım:** 20 kararın **9'u** kullanıcıya sorulabilir — "Değiştirebilir mi" sütunu
Evet ya da Kısmen olanlar: hedef boyut (1), amaç (2), kalite hedefi (3), kodek
tercihi (4), doldurma politikası (8), çözünürlük izni (9), kare hızı izni (10),
HDR biçimi (13, yalnız HDR kaynakta) ve GPU hız kipi (17). Bu dokuz, K5'te tek tek
sınanan dokuz sorunun aynısı.

Kalan **11'i** hiç sorulmaz. Biri (somut video kodeği, 5) yalnız 4 üzerinden dolaylı
belirlenir; onu da sorulmayanlara yazdık, çünkü kullanıcı `libsvtav1` ile `libx264`
arasında doğrudan seçim yapamıyor. Sorulmayanların içinde kaliteyi doğrudan
belirleyen üç tanesi var: preset (6), anahtar kare aralığı (15) ve ses bütçesi
payı (12).

### Bu kaynakta üretilen tam plan

Başsız yoldan, uygulamanın kendi kod yolu çağrılarak (`ComplexityProbe` → 2 tur
`CalibrationProbe` → `PlanCalculator.BuildDetailed`, `MainWindow.axaml.cs:1499-1540`
ile birebir):

```
PLAN mode=2pass codec=libsvtav1 preset=6 1920x1080@60 vbit=2026k abit=128k
     pix=p010le  tahmin=15,60 MB  tahmini kalite=99,3
```

```
ffmpeg -hide_banner -y -hwaccel auto -i parca-2.mkv -c:v libsvtav1 -preset 6
  -b:v 2026k -pass 2 -passlogfile pl -g 120 -pix_fmt p010le
  -svtav1-params tune=0:enable-variance-boost=1:variance-boost-strength=2
  -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc
  -c:a aac -b:a 128k -movflags +faststart cikti.mp4
```

---

## K2 — Kullanıcının vermek zorunda olduğu karar sayısı

Gerçekten denendi: uygulama Release yapısıyla açıldı, kaynak dosya komut satırından
verildi (`ShellIntegration.ResolveStartupPath`, `Program.cs:11` — sürükle-bırakla aynı
yükleme yolu), hiçbir denetime dokunulmadan "Küçült"e basıldı.

**Zorunlu karar sayısı: 0.** Zorunlu eylem sayısı: 2 — dosyayı ver, düğmeye bas.

- Başlat düğmesi yalnız "dosya yüklü + ffmpeg var + koşum yok" ile açılır
  (`MainWindow.axaml.cs:1616`, `:2682`). Hiçbir alanda başlatmayı engelleyen doğrulama
  yok; hedef kutusu boş ya da bozuk metin içerirse sessizce 16 MB'a düşer
  (`MainWindow.axaml.cs:1314`). Bloke eden doğrulama yalnız Dönüştür sekmesinde var.
- Arayüz ağacından okunan açılış durumu: hedef `16`, kalite `87.1`, "Çözünürlük
  düşürülebilir" açık, "Kare hızı düşürülebilir" açık, "Hızlı düşür (GPU)" kapalı
  (ilk anlık görüntüde bu kutu daha **kilitliydi**; donanım yoklaması bitince açıldı ve
  kapalı olarak karara bağlandı).

**Dokunmazsa ne oluyor:** çalışıyor. Teslim edilen dosya
`parca-2_shrunk.mp4`, **15 766 933 bayt = 15,04 MiB**; AV1, 1920x1080@60,
`yuv420p10le`, PQ/bt2020 korunmuş, video **1947,5** kbit/s, AAC 129,3 kbit/s stereo,
60,441 s. Hata yok, uyarı yok. (Bit hızları paket toplamından hesaplandı;
belgedeki tüm bit hızı sayıları aynı yöntemle.)

Bir çentik: 15,04 MiB, `FillTarget` bandının alt kenarının (0,95 × 16 = **15,20 MiB**)
altında, sert tabanın (14,40 MiB) üstünde. Yani auto hedefi tutturdu ama kendi
doldurma bandını dolduramadı — bütçenin %1'i kullanılmadan kaldı. Kaç denemede
bittiği **ölçülmedi** (uygulama sonuç metni ekran kapısı yüzünden okunamadı).

### Ekran kapısı

`Windows-MCP`'nin ekran görüntüsü yolu bu oturumda **çalışmıyor**: pencere ağacı
VidShrink'i "odakta, tam ekran" gösterirken dönen görüntü karesi başka bir uygulamayı
gösteriyor (tek ekran var, `display=[1]` reddediliyor). Bu yüzden uygulamanın metinsel
durum satırları — sonuç özeti, deneme sayısı, "Neden Böyle" gerekçesi — okunamadı.
Etkileşim ve durum okuması **UIA ağacı** üzerinden yapıldı; ağaç doğru çalışıyor,
düğmeye basmak ve kutu değerlerini okumak sorunsuz. Plan ve argümanlar ayrıca başsız
yoldan (`PlanCalculator` + `EncodeRunner.EncodeArguments` doğrudan çağrılarak)
üretildi ve teslim edilen dosyayla karşılaştırıldı — ikisi tutuyor.

---

## K3 — Uzman açığı

Üç çıktı, **aynı kaynaktan ve aynı teslim boyutunda**. Boyut eşlemesi elle yapıldı:
her iki uzman koşumunun bit hızı, teslim edilen dosya auto'nun boyutuna oturana
kadar yeniden ayarlandı (uzman-biz için üç, HandBrake için iki koşum).

| satır | ne yapıldı |
|---|---|
| `auto` | Uygulamanın kendi varsayılanları. Hiçbir ayara dokunulmadı. Motor `libsvtav1`, preset 6, `-g 120`, 1920x1080@60, `aac 128k` seçti; istenen `-b:v 2026k`. |
| `uzman-biz` | Aynı motor, elle ayarlanmış: `libsvtav1` **preset 4**, **`-g 300`**, çözünürlük düşürülmedi; istenen `-b:v 2605k`. Diğer her şey auto ile birebir aynı. |
| `uzman-handbrake` | Koşum adı **`uzman-hb2`**: `HandBrakeCLI -e x265_10bit --encoder-preset slow --multi-pass --turbo -E ca_aac -B 128 -w 1920 -l 1080 --crop-mode none -r 60 --cfr -b 1900`. Boyut eşitlemenin ikinci denemesi; ilk deneme `uzman-hb` aynı komutu `-b 2026` ile koştu ve 15,97 MiB verdi — K3'e girmedi, kusur 3'ün oran tablosunda kullanıldı. |

| satır | boyut | ortalama | p10 | harmonik | 1 puan altı kare | en düşük kare |
|---|---|---|---|---|---|---|
| auto | 15,04 MiB (15766933 bayt) | 94,462 | 94,534 | 56,313 | 26 | 0,00 |
| uzman-biz | 15,02 MiB (15752039 bayt) | 94,861 | 95,337 | 56,472 | 26 | 0,00 |
| uzman-handbrake | 15,02 MiB (15754005 bayt) | 95,731 | 95,361 | 95,727 | 0 | 74,67 |

**Uzman açığı = uzman-biz - auto:** ortalama +0,400, harmonik +0,159, p10 +0,803

Bu açığın tamamı ayar başına ayrıştırılamadı; ne kadarının ayrıştırıldığı ve kalanın
neden ölçülmediği K4'te yazılı.

Boyut farkı: uzman-biz 15,02 MiB, auto 15,04 MiB (-0,1%).

**HandBrake - auto:** ortalama +1,269, harmonik +39,414, p10 +0,827; boyut 15,02 MiB (-0,1%).

Okuma: pozitif sayı uzmanın önde olduğunu söyler. Üç satır da 15,02-15,04 MiB
aralığında, aralarındaki en büyük boyut farkı **%0,1** — yani puan farkı boyut
farkından gelmiyor.

**Harmonik sütunu bu tabloda okunmamalı.** Altı SVT-AV1 koşumunun tamamında aynı
26 kare VMAF-NEG'den 1 puanın altında alıyor (25'i tam 0), iki x265 koşumunda hiç
almıyor; o kareler bozuk değil (PSNR 46,30-49,34 dB). Sütun sözleşme üç metrik istediği için duruyor, yanında
1 puan altı kare sayısıyla. Ayrıntı ölçüm düzeneği bölümünde ve kusur 4'te.

---

## K4 — Açığın ayar başına ayrıştırması

`uzman-biz` auto'dan **iki** ayarda ayrılıyor: kodlayıcı çabası ve anahtar kare
aralığı. Her biri tek tek geri alındı — yani auto'nun argümanına o ayar **yalnız
başına** uygulandı, geri kalan her şey (kaynak, istenen `-b:v 2026k`, ses,
`-pix_fmt`, `-svtav1-params`, renk argümanları) birebir sabit tutuldu. Üçüncü satır,
uzman-biz'in **almadığı** bir ayar: çözünürlük düşürme. Auto bunu yapmaya yetkili
(`AllowResolutionDrop = true`) ama bu kaynakta yapmadı; elle denendi ve reddedildi.

| değiştirilen tek ayar | auto değeri | uzman değeri | boyut | Δ ortalama | Δ p10 |
|---|---|---|---|---|---|
| kodlayıcı çabası (preset) | 6 | 4 | 13,97 MiB (-7,1%) | -0,042 | -0,293 |
| anahtar kare aralığı (-g) | 120 (fps × 2) | 300 | 11,35 MiB (-24,5%) | +0,155 | +0,333 |
| çözünürlük | 1920x1080 | 1440x810 | 13,57 MiB (-9,8%) | -5,691 | -5,365 |

### En büyük kalem: anahtar kare aralığı

Tabloda tek satır iki eksende birden kazanıyor: `-g 300`. Dosya **%24,5 küçülürken**
puan da **yükseliyor** (ortalama +0,155, p10 +0,333). Boyut eşitliği tartışmasından
bağımsız bir sonuç — daha küçük dosyada daha iyi puan, hangi eksenden bakılırsa
bakılsın kayıp yok.

**Yerleşim sebep değil — ölçüldü.** Tur 2'de bu satırda "sebebi anahtar kare sayısı
değil, yeri" yazıyordu; o cümle türetilmişti, ölçülmemişti. Tur 3'te yapılan A/B onu
çürüttü — aşağıda. Ölçülen tek şey bu **dışlama**; `-g 120 → 300` değişiminde geriye
kalan tek değişken aralığın (dolayısıyla sabit ızgarada sayının) kendisi.

Üretilen dosyalardaki anahtar kare zamanları doğrudan sayıldı
(`ffprobe -skip_frame nokey`):

| çıktı | anahtar kare | en kısa aralık | en uzun aralık |
|---|---|---|---|
| auto (`-g 120`) | 31 | 2,00 s | 2,00 s |
| `-g 300` | 13 | 5,00 s | 5,00 s |
| uzman-handbrake | 7 | 8,33 s | 10,00 s |

Bizim iki satırımızda en kısa ile en uzun aralık **birebir eşit** — yani anahtar kare
katı bir ızgaraya diziliyor, içeriğe hiç bakılmıyor. Sabit `FfmpegArguments.cs:162`'de:
`-g = max(2, round(fps × 2))`. **Bu, kazanan koşum için de geçerli:** `-g 300` çıktısı da
katı ızgara, yalnız adımı 5,00 s. Dolayısıyla "ızgara olması" tek başına `-g 300`'ün
kazancını açıklayamaz — kazanan koşum da ızgara.

**Sahne kesmeleri nereden geliyor.** Kaynakta iki kesme var: 28,353 s ve 56,870 s.
Üreten komut:

    ffmpeg -i gui/parca-2.mkv \
      -vf "select='gt(scene,0.2)',metadata=print:file=-" -an -f null -

Çıktı: `pts_time 28.353` (skor 0,314) ve `pts_time 56.870` (skor 0,261). Eşik 0,3'te
yalnız birincisi geçiyor; **"iki kesme" ifadesi 0,2 eşiğine bağlıdır.**

HandBrake'in anahtar kare zamanları `0,02 / 10,02 / 20,02 / 28,35 / 38,35 / 48,35 / 56,87`
— düzenli 10 s'lik tavanın **arasına** tam bu iki kesmeyi yerleştirmiş. Bizimkiler
`0,02 / 2,02 / 4,02 / … / 60,02`; kesmelerin ikisi de iki ızgara noktasının arasına
düşüyor. Bu bir gözlem; aşağıdaki koşumlar bunun bir **sebep** olmadığını gösteriyor.

### Yerleşimin payı ölçüldü: sıfır değil, negatif

Yerleşimi aralıktan ayırmak için aynı `-g 300`'de üç koşum yapıldı
(`tools/auto-mod-olcumu/hizalama.sh`; üçü de preset 6, `-threads 4`, `lp=4`, aynı
kaynak, aynı ses):

| koşum | anahtar kare | boyut | istenen bit hızı | ortalama | p10 |
|---|---|---|---|---|---|
| `y1` düz ızgara | 13 (5,00 s adım) | 11 809 579 B | 2026k | 94,614 | 94,870 |
| `y2` kesmelere hizalı | 13 | 11 160 196 B (-5,5%) | 2026k | 93,368 | 92,778 |
| `y3` kesmelere hizalı, boyut eşitlenmiş | 13 | 11 973 383 B (+1,4%) | 2144k | 93,389 | 92,824 |

`-force_key_frames 28.353,56.870` anahtar kare **sayısını değiştirmiyor**: zorlanan
anahtar kare ızgara sayacını sıfırladığı için üçünde de 13 tane var. Değişen tek şey
yer — `y2`/`y3` anahtar kareleri `0,02 / 5,02 / … / 25,02 / 28,37 / 33,37 / … / 53,37 / 56,88`,
yani iki kesmenin ikisi de (bir kare sonrasında) anahtar kare.

**Sonuç: hizalamanın payı negatif.** Boyutu eşitlenmiş `y3`, `y1`'den %1,4 **büyük**
olduğu hâlde ortalamada **-1,225**, p10'da **-2,046** puan veriyor. `y2` ile `y3`'ün
neredeyse eşit olması (fark 0,021 ortalama, 0,046 p10) kaybın bit bütçesinden değil
yapıdan geldiğini gösteriyor: 108 kbit/s daha fazla bit hiçbir şey kazandırmadı.

Yani `-g 300`'ün kazancı **aralığın uzunluğundan** geliyor; anahtar kareyi sahne
kesmesine oturtmak bu kaynakta kaybettiriyor. **Neden kaybettirdiği ölçülmedi** —
zorlanan anahtar karenin 16 karelik mini-GOP yapısını ortasından kesmesi bir aday,
ama denenmedi. Ölçülen tek şey işaret ve büyüklük.

Bu ölçüm **tek kaynakta ve tek `-g` değerinde** yapıldı. Başka içerikte (daha sık ve
daha sert kesmeli) sonucun aynı çıkacağı **ölçülmedi**.

**Düzeltme bu sözleşmenin işi değil.** `FfmpegArguments.cs` T98'in `owns`'unda;
burası ölçüp adlandırıyor.

### Ayrıştırma açığın ne kadarını açıklıyor

**Toplam tutmuyor, ve bu belgenin söylemesi gereken bir şey.** İki ablasyonun
Δ ortalaması toplamı `+0,155 − 0,042 = **+0,113**`; K3'teki açık **+0,400**. p10'da
durum daha keskin: `+0,333 − 0,293 = **+0,040**`, açık ise **+0,803**. Yani ayar
başına ayrıştırma açığın ortalamada kabaca **dörtte birini**, p10'da **yirmide
birini** açıklıyor.

**Kalan kısım ayrıştırılamadı — ölçülmedi.** İki aday var, ikisi de bu sözleşmede
ölçülmedi: (a) iki ayarın birlikte kullanıldığındaki etkileşimi, (b) boyut eşitlemek
için gereken bit hızı farkı — ablasyonlar auto'nun `-b:v 2026k` isteğiyle koştu,
`uzman-biz` ise 15,02 MiB'a oturmak için `2605k` ile koştu. Ayırmak için her
ablasyonun ayrıca boyut eşitlenmiş bir koşumu gerekirdi; o koşumlar yapılmadı.

Bu yüzden `-g`'ye "en büyük kalem" demek, "açığın açıklaması" demek değil. `-g`
bulgusunun gücü açığa katkısından gelmiyor — **daha küçük dosyada daha yüksek puan**
verdiği için, boyut eşitliği tartışmasından bağımsız olarak tek başına duruyor.

### Satır satır okuma

Üç ablasyon da auto ile **aynı** `-b:v 2026k` isteğiyle koşturuldu, ama `libsvtav1`
istenen bit hızını ayara göre farklı tutturuyor (bkz. kusur 3). Bu yüzden satırlar
farklı boyutlara düşüyor ve puan farkı ile boyut farkı birlikte okunmalı:

- **preset 4**: %7,1 küçük dosyada ortalama −0,042. Puan pratikte aynı, yer kazancı
  gerçek. Boyut eşitlendiğinde net kazanç — nitekim `uzman-biz` bunu aldı.
- **`-g 300`**: %24,5 küçük dosyada puan **artıyor**. Tek yönlü kazanç.
- **1440x810 ölçek**: %9,8 küçük dosyada ortalama −5,691, p10 −5,365. Kötü takas;
  `uzman-biz` bunu almadı. Auto'nun bu kaynakta çözünürlük düşürmemesi doğru karar.

---

## K5 — HandBrake'in sormadığı, bizim sorduğumuz

HandBrakeCLI 1.11.2 bu makinede doğrulandı: `--help` çıktısında **hedef boyut seçeneği
yok** — yalnız `-q/--quality` (RF) ve `-b/--vb` (kbit/s) var. Bilmeyen kullanıcıya
verdiği tek soru `-Z/--preset` (`-z` ile listelenen preset adı); geri kalan her şeyi
preset sabitliyor.

Bizim Küçült sekmemizin sorduğu **9** soru (biri koşullu). Her satırın tek sınavı:
*bilmeyen kullanıcı buna doğru cevap verebilir mi?*

| Soru | HandBrake sorar mı | Bilmeyen doğru cevaplar mı | Ne olmalı |
|---|---|---|---|
| **Hedef boyut (MB)** | Hayır — bu bizim tek gerçek farkımız | **Evet.** Kullanıcı zaten "25 MB'ı geçmesin" diye geliyor; sayı onun dünyasından | Kalsın, birincil kalsın |
| **Amaç** (Arşiv / Paylaşım / Sosyal) | Hayır (preset adına gömülü) | **Evet, sınırda.** Kullanıcının diliyle sorulmuş; yanlış cevabın bedeli küçük — yalnız ±3 CRF kayması (`CompressionStrategy.cs:92-97`) | Kalsın |
| **Kalite hedefi** (1–100 kaydırıcı) | Hayır | **Hayır.** Soyut bir sayı, üstelik hedef boyutla aynı şeyi ters yönden soruyor; ikisi aynı ekranda birbirini eziyor | Hedefe bağlansın, birincil yüzeyden kalksın |
| **Sıkıştırma Algoritması** (Auto / Uyumlu / En küçük) | Hayır (preset kodeği sabitler) | **Hayır.** "Hangi kodek" uzman sorusunun ta kendisi. Varsayılan `Auto` zaten rejimden cevabı üretiyor | Auto'da kalsın, gelişmiş bölüme insin |
| **Çözünürlük düşürülebilir mi** | Hayır (preset tavan koyar) | **Hayır.** "İzin verirsem ne kaybederim" sorusunun cevabı ölçüm gerektiriyor | Varsayılan açık doğru; kutu gelişmişe insin |
| **Kare hızı düşürülebilir mi** | Hayır (preset tavan koyar) | **Hayır.** Aynı gerekçe | Aynı |
| **Hızlı düşür (GPU)** | Hayır | **Hayır.** Hız/kalite takası; zaten donanım yoklaması karar veriyor (`HardwareVerdict.cs:66-101`) ve kutu karar verilene kadar kilitli duruyor | Karar programda kalsın; kutu gelişmişe insin |
| **Doldurma politikası** (Hedefi doldur / Kalite tavanı) | Hayır | **Hayır.** İki terim de motor jargonu; projenin dışından kimse ne olduğunu bilmiyor | Arayüzden kalksın, `FillTarget` sabitlensin |
| **HDR biçimi** (Koru / SDR'a indir) — yalnız HDR kaynakta | Hayır (preset kodeğe göre sabitler) | **Kısmen.** "Telefonumda soluk görünür mü" gerçek bir kullanıcı sorusu, ama böyle sorulmuyor | Kalsın, kullanıcı diliyle sorulsun |

**Tersi — HandBrake'in sorup bizim sormadığımız.** Onların preseti bunları sabitler;
bizde de sabit, ama **preset adı gibi görünür bir kapağı yok**:

| Onlarda | Bizde | Bilmeyen doğru cevaplar mı |
|---|---|---|
| Kodlayıcı preset'i (`--encoder-preset`) | Yok, motor seçer (`PlanCalculator.cs:812-826`) | Hayır — sormamak doğru, ama sabitin bedeli K4'te ölçüldü |
| Sabit kalite RF (`-q`) | Yok, CRF motorda | Hayır — sormamak doğru |
| Ses kodeği ve bit hızı (`-E`, `-B`) | Yok, sabit `aac` + rejim payı | Hayır — sormamak doğru |
| Kap (`-f`) | Yok, `mp4` sabit (`MainWindow.axaml.cs:2356`) | Hayır — sormamak doğru |
| Kırpma (`--crop-mode`) | Yok, kırpma yapılmıyor | Hayır — sormamak doğru |
| Anahtar kare aralığı | Yok, `fps × 2` sabit (`FfmpegArguments.cs:162`) | Hayır — sormamak doğru, **ama sabitin kendisi hiç ölçülmemişti** (K4) |

Özet: HandBrake bilmeyen kullanıcıya **1** soru soruyor, biz **9**. Dokuzun
**ikisi** (hedef boyut, amaç) bilmeyen birinin doğru cevaplayabileceği sorular;
**altısı** cevaplayamayacağı, **biri** (HDR) doğru soru ama yanlış dille sorulmuş.

---

## K6 — Sıradaki adım

Üç madde, üçü de K4'teki bir sayıya bağlı. Hiçbiri bu sözleşmede uygulanmadı.

**1. Anahtar kare aralığını uzat — sahne kesmesine bağlama.** K4'te ölçülen
kalemlerin en büyüğü: `-g 300` dosyayı %24,5 küçültürken puanı yükseltiyor
(ortalama +0,155, p10 +0,333). Bu maddenin gerekçesi açığa katkısı değil — açığın
çoğu zaten ayrıştırılamadı — **daha küçük dosyada daha yüksek puan** vermesi; iki
eksende birden kazandığı için boyut eşitliği tartışmasından bağımsız duruyor.

Ölçülen şey **yerleşimin sebep olmadığı**: aynı `-g 300`'de anahtar kareyi iki sahne
kesmesine hizalamak, sayı sabitken ve boyut eşitlenmişken ortalamada **-1,225**,
p10'da **-2,046** kaybettiriyor (`y1`/`y3`, K4'teki hizalama tablosu). Geriye kalan
tek değişken aralığın uzunluğu; bu bir dışlama, doğrudan ölçüm değil.
Yapılacak iş `FfmpegArguments.cs:162`'deki `fps × 2` sabitini daha uzun bir sabit
aralığa çevirmek. **Sahne kesmesi tetikli anahtar kare denenmemeli** — bu kaynakta
ölçüldü ve kaybettiriyor.

Ölçülen tek nokta `-g 300`'dür; **hangi `-g` değerinin en iyi olduğu ölçülmedi**,
120 ile 300 arası taranmadı. **Bu dosya T98'in `owns`'unda; iş oraya ait.**

**2. Yazılım AV1'in bit hızı sapmasını modelle.** Kusur 3'te ölçüldü: teslim oranı
ayara göre 0,709 ile 0,961 arasında değişiyor, HandBrake aynı istekte 1,024 veriyor.
Auto bu yüzden kendi doldurma bandının altına düşüyor (15,04 MiB teslim, band alt
kenarı 15,20 MiB). Madde 1 uygulanırsa sapma daha da büyür (`-g 300` ölçümünde oran
0,709'a iniyor) ve kazanılan yer boş kalır — yani **madde 1 bu düzeltme olmadan
kazancının bir kısmını çöpe atar.** `PlanCalculator.cs:82-96`'daki
`DeliveryReserveK` / `HardwareBitrateYield` yalnız donanım yolunu kapsıyor.

**3. Preset varsayılanını 6'dan 4'e almayı tartışmaya aç — ama süre ölçülmeden değil.**
Kalite tarafı ölçüldü: preset 4, %7,1 küçük dosyada ortalama −0,042 veriyor, yani puan
pratikte aynı, yer kazancı gerçek. Karşılığında ödenen kodlama süresi **ölçülmedi** —
makine bu ölçüm boyunca paylaşımlıydı, duvar saati anlamlı değil.
Bu madde ancak yalıtılmış bir makinede süre ölçüldükten sonra karara bağlanabilir.
Çözünürlük düşürme ise ölçülüp **reddedildi**: %9,8 yer için ortalama −5,691 kötü takas,
auto'nun bu kaynakta çözünürlüğe dokunmaması doğru karardı.

---

## T111 — kare kilidiyle yeniden ölçüm

T110 mühürlendi: ölçer artık iki girdiyi de `settb=AVTB,setpts=N` ile kare
indeksine kilitliyor. Bu bölümün üstündeki **bütün** VMAF sayıları kilitsiz
ölçerle, yani damga eşlemesiyle üretildi. T111 aynı düzeneği yeniden koşturup
kilidin her sayıya ne yaptığını ölçüyor. Düzenek `tools/auto-mod-olcumu/t111-*.sh`
altında; çıktılar `.calisma/t111/`.

### Kayma her koşum için tek tek doğrulandı

`ffprobe` kaptaki **her** akışın `start_time`'ını okuyor; grafik kayması videonun
`start_time`'ı eksi kaptaki en erken akışın `start_time`'ı
(`tools/auto-mod-olcumu/t111-kayma.sh`). Onbir koşumun tamamı ve kaynak ölçüldü:

| dosya | akış | video `start_time` | en erken akış | kap içi kayma |
|---|---|---|---|---|
| `parca-2.mkv` (**kaynak**) | 2 | 0,020000 | 0,000000 | 0,020000 s = 1,200 kare |
| `auto` | 2 | 0,016667 | 0,000000 | 0,016667 s = 1,000 kare |
| `e1-preset4` | 2 | 0,016667 | 0,000000 | 0,016667 s = 1,000 kare |
| `e2-gop300` | 2 | 0,016667 | 0,000000 | 0,016667 s = 1,000 kare |
| `e3-olcek810` | 2 | 0,016667 | 0,000000 | 0,016667 s = 1,000 kare |
| `uzman-biz3` | 2 | 0,016667 | 0,000000 | 0,016667 s = 1,000 kare |
| `y1-g300-izgara` | 2 | 0,016667 | 0,000000 | 0,016667 s = 1,000 kare |
| `y2-g300-hizali` | 2 | 0,016667 | 0,000000 | 0,016667 s = 1,000 kare |
| `y3-hizali-boyutesit` | 2 | 0,016667 | 0,000000 | 0,016667 s = 1,000 kare |
| `uzman-hb` (x265) | 2 | 0,020000 | 0,000000 | 0,020000 s = 1,200 kare |
| `uzman-hb2` (x265) | 2 | 0,020000 | 0,000000 | 0,020000 s = 1,200 kare |

Framesync tek bir dosyanın kaymasına değil, **iki kaymanın farkına** bakıyor.
Test eksi kaynak:

| aile | kaynak | çıktı | fark | kare cinsinden |
|---|---|---|---|---|
| dokuz SVT-AV1 koşumu | 0,020000 | 0,016667 | **−0,003333 s** | −0,200 kare |
| iki HandBrake koşumu | 0,020000 | 0,020000 | **0,000000 s** | 0,000 kare |

**HandBrake koşumlarının temiz çıkması beklentiydi; doğrulandı — ama beklenen
sebeple değil.** HandBrake çıktısı kaymasız değil: kaynağın kaymasının aynısını,
0,020000 s'yi taşıyor. Temiz olan mutlak damgası değil, farkı. Bu ayrımı yazmak
gerekiyor, çünkü "x265 kaymıyor" cümlesi ölçülen şeyi yanlış anlatır: ölçülen
şey, x265 yolunun kaynağın kap ofsetini olduğu gibi geçirdiği, bizim AV1
yolumuzun ise 0,020000'i 0,016667'ye çevirdiğidir.

Kap ofseti dolaylı bir okuma; kare kare damga farkı doğrudan ölçüldü
(`tools/auto-mod-olcumu/t111-damga.sh`, `showinfo` `pts_time` dökümü, 3624 kare):

| koşum | kare 0 | ortalama | en düşük | en yüksek | negatif kare |
|---|---|---|---|---|---|
| `auto` (AV1) | −3,33 ms | −3,02 ms | −4,33 ms | −1,67 ms | **3624 / 3624** |
| `uzman-biz3` (AV1) | −3,33 ms | −3,02 ms | −4,33 ms | −1,67 ms | **3624 / 3624** |
| `uzman-hb` (x265) | +0,00 ms | +0,31 ms | −1,00 ms | +1,67 ms | 180 / 3624 |
| `uzman-hb2` (x265) | +0,00 ms | +0,31 ms | −1,00 ms | +1,67 ms | 180 / 3624 |

İki ölçüm birbirini tutuyor: AV1 tarafında sapma **tek yönlü** — 3624 karenin
3624'ü kaynağın damgasının gerisinde, ortalama −3,02 ms. Kaynağın kendi kare
aralığı 16,6666 ms (en kısa 14,00, en uzun 19,00), yani sapma bir karenin
beşte biri kadar; ama işareti hiç değişmediği için framesync her karede bir
önceki kaynak karesini eşliyor. x265 tarafında sapmanın işareti değişiyor
(3624 karenin 3444'ü pozitif), ortalaması sıfırın üstünde ve kare 0 tam sıfır —
tam kare kayması üretmiyor.

Kalan yedi koşumun kare kare damgası **ölçülmedi**; onlar için kanıt yalnız
kap ofseti. Dördü ölçüldü çünkü belgedeki karşılaştırmaları bu dört koşum
taşıyor.

## Ölçüm sırasında bulunan kusurlar — düzeltilmedi

T102 kod değiştirmiyor. Bunlar ayrı sözleşme ister.

**1. Bench auto modu ölçemiyor.** `tools/VidShrink.Bench` `shrink` alt komutu
`PlanOptions.Codec` alanını hiç ayarlamıyor (`tools/VidShrink.Bench/Program.cs:663-670`),
yani `PlanOptions` varsayılanı olan `CodecPreference.Compatible` ile ölçüyor.
Uygulamanın varsayılanı ise `Auto` (`MainWindow.axaml:398` `SelectedIndex=0`).
Aggressive/Extreme rejimlerde ikisi **farklı kodek** seçiyor
(`CompressionStrategy.cs:58-63`): bu kaynakta `Auto` → `libsvtav1`, bench → `libx264`.
Yani "rapora giren her sayı bench'ten çıkar" kuralı auto mod için bugün tutmuyor;
bu sözleşmenin ölçümü ayrı bir başsız sonda ile alındı.

**2. `p010le` yoklaması yalancı geçiyor.** `EncoderCapabilities.ProbeHdr10PixelFormat`
sırayla `p010le` ve `yuv420p10le` deniyor (`src/VidShrink.Ffmpeg/EncoderCapabilities.cs:125-131`)
ve ilk geçeni HDR10 biçimi diye döndürüyor. Ama `libx264` de `libsvtav1` de `p010le`
desteklemiyor — `ffmpeg -h encoder=libx264` listesinde yok. ffmpeg bunu hata saymıyor,
uyarıp `yuv420p10le`'ye çeviriyor, yoklama "geçti" diyor. Sonuç doğru çıkıyor
(teslim edilen dosyada ölçülen biçim `yuv420p10le`) ama üretilen komut satırı
`-pix_fmt p010le` diyor ve yoklamanın verdiği "destekliyor" kararı gerçeğe dayanmıyor.

**3. Yazılım AV1'in bit hızı sapması modellenmiyor.** `libsvtav1` istenen `-b:v`
değerini tutturamıyor ve sapma **ayara bağlı olarak değişiyor**. Bu kaynakta ölçülen
teslim oranları (teslim edilen video bit hızı / istenen):

| koşum | K3/K4'te | istenen | teslim | oran |
|---|---|---|---|---|
| auto (preset 6, g=120) | K3 `auto`, K4 taban | 2026 | 1947,5 | 0,961 |
| preset 4 | K4 ablasyon | 2026 | 1800,4 | 0,889 |
| g=300 | K4 ablasyon (`e2-gop300`) | 2026 | 1436,1 | 0,709 |
| ölçek 1440x810 | K4 ablasyon | 2026 | 1743,9 | 0,861 |
| preset 4 + g=300 (`uzman-biz-2975`) | boyut eşitleme denemesi | 2975 | 2145,2 | 0,721 |
| preset 4 + g=300 (`uzman-biz-2775`) | boyut eşitleme denemesi | 2775 | 2038,8 | 0,735 |
| preset 4 + g=300 (`uzman-biz3`) | **K3 `uzman-biz` satırı** | 2605 | 1946,0 | 0,747 |
| HandBrake x265 slow (`uzman-hb`) | boyut eşitleme denemesi | 2026 | 2073,7 | **1,024** |
| HandBrake x265 slow (`uzman-hb2`) | **K3 `uzman-handbrake` satırı** | 1900 | 1942,5 | **1,022** |
| `y1` düz ızgara, g=300 (tur 3, `-threads 4`) | hizalama A/B | 2026 | 1423,7 | 0,703 |
| `y2` kesmelere hizalı, g=300 (tur 3) | hizalama A/B | 2026 | 1337,8 | **0,660** |
| `y3` hizalı, boyut eşitlenmiş (tur 3) | hizalama A/B | 2144 | 1445,4 | 0,674 |

İlk sürümde bu tabloda K3'ün iki satırını **üreten** koşumlar (`uzman-biz3`,
`uzman-hb2`) yoktu; yerlerine boyut eşitlemenin ara denemeleri konmuştu. İkisi de
yukarıya eklendi. Boyut eşitleme `uzman-biz` için üç (2975 → 2775 → 2605), HandBrake
için iki (2026 → 1900) koşum sürdü; betiklerde geçen `uzman-biz` adı `uzman.sh`'in
varsayılanıdır, o adla bir çıktı üretilmedi.

HandBrake istediğini %2,4 içinde tutturuyor; bizim yolumuz %4 ile %34 arasında altına
düşüyor. En kötü oran zorlanmış anahtar kareli koşumda (`y2`, 0,660) —
`-force_key_frames` sapmayı büyütüyor. Plan hesabı bu sapmayı yalnız donanım kodlayıcıları için modelliyor
(`PlanCalculator.cs:82-96`: `DeliveryReserveK`, `HardwareBitrateYield`); yazılım yolunda
sapma sıfır varsayılıyor. Auto'nun kendi doldurma bandını dolduramamasının
(15,04 MiB teslim, band alt kenarı 15,20 MiB) ölçülen sebebi bu. Sapma preset'e ve
anahtar kare aralığına bağlı olduğu için bu ikisini değiştiren her öneri bu düzeltmeyi
de ister — aksi halde kazanılan yer boş bırakılır.

**4. Harmonik ortalama AV1 çıktılarında yapay olarak çöküyor ve bench bunu
raporluyor.** VMAF-NEG bu kaynakta altı SVT-AV1 koşumunun **tamamında birebir aynı
26 karede** 1 puanın altına iniyor — bunların 25'i tam 0, `uzman-biz`de 24'ü. İki
x265 koşumunda hiç inmiyor. Kareler bozuk değil: aynı aralıkta auto'nun PSNR'ı
46,30-49,34 dB, parlaklık kaynakla örtüşüyor ve HandBrake aynı karelerde 96-100
alıyor. Harmonik ortalama `n / Σ(1/max(x,1))` olduğu için bu 26 kare sayıyı
94,5'ten 56,3'e indiriyor.

Bench aynı formülü kullanıyor (`tools/VidShrink.Bench/Program.cs:820`) ve sonucu üç
yerde raporluyor (`:527`, `:775`, `:913`). Bugün bench'e AV1 ile x265 aynı kaynakta
karşılaştırtılsa **39 puanlık** bir kalite farkı raporlardı; o farkın PSNR'a göre
karşılığı yok. Kodek kararı bu sayıya bakılarak verilirse yanlış kodek seçilir.

Bench zaten XPSNR de ölçüyor (`:775`) — çelişki oradan yakalanabilirdi, ama sayılar
yan yana okunmuyor ve düşük puanlı kare sayısı hiç raporlanmıyor.

Sebep bench'in ölçekleme adımı değil: aynı çift `scale` filtresi olmadan yeniden
ölçüldü, sonuç birebir aynı çıktı (3624 kare, 1 puan altı 26 kare, 25'i tam 0, aynı
kare numaraları, ortalama 94,462, harmonik 56,313). Çıktı dosyasının kendisiyle
libvmaf arasında kalıyor.

**5. Ölçüm betiğinin sütun adı sayıyla uyuşmuyordu — kapandı.** `tablolar.py`
sütunu `sifir = sum(1 for x in s if x < 1.0)` ile hesaplayıp başlığına "sıfır puanlı
kare" yazıyordu. Saydığı şey 1 puanın altındaki kare; tam sıfır sayısı bundan bir ya
da iki eksik. Sayı doğruydu, adı yanlıştı — ve bu belgenin ilk sürümündeki iki yanlış
cümlenin kaynağı bu oldu.

**T111'de okundu: başlık düzeltilmiş.** `b552142` ("ölçü aracı: sütun adı saydığı
şeye göre düzeltildi") başlığı "1 puan altı kare" yaptı; bugün
`tools/auto-mod-olcumu/tablolar.py:78` böyle diyor. Geriye yalnız değişken adı
(`sifir`, `:24`) kaldı, o da rapora girmiyor. Bu maddenin "hâlâ duruyor" cümlesi
düzeltmeden sonra güncellenmemişti; **kusur kapandı, cümle geç kaldı.**

