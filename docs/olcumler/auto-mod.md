# Auto mod — hiçbir ayar bilmeyen kullanıcı ne alıyor

T102. Ölçüm tarihi 2026-09-02. Dal `T102-auto-mod`, taban `main@3ede43d`.

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

Makine paylaşımlı ve ölçüm sırasında dört ajan daha koşuyordu. **Süreler ölçülmedi** —
duvar saati bu koşullarda anlamlı değil. Kalite sayıları yükten etkilenmez.

---

## K1 — Auto modun bugün aldığı kararlar

Kullanıcı dosyayı sürükleyip "Küçült"e bastığında, onun adına alınan kararlar.
"Görüyor mu" sütunu Küçült sekmesindeki durumu anlatır.

| # | Karar | Auto'nun verdiği | Nereden (`dosya:satır`) | Görüyor mu | Değiştirebilir mi |
|---|---|---|---|---|---|
| 1 | Hedef boyut | **16 MB** (kaynak 16 MB'ın altındaysa `max(1, kaynak/2)`) | `MainWindow.axaml.cs:41`, `:1303-1307`; kutu `MainWindow.axaml:299-300`; kalıcı ayar `UpdateCheck.cs:402` | Evet, kutuda yazıyor | Evet |
| 2 | Amaç | **Paylaşım** (`Intent.Sharing`) → şeffaflık CRF'ine −3 kaydırma | `MainWindow.axaml:383` `SelectedIndex=1`; `UpdateCheck.cs:404`; etki `CompressionStrategy.cs:92-97` | Evet, açılır kutu | Evet |
| 3 | Kodek tercihi | **Auto** → rejimden türer: Light/Balanced → `Compatible`, Aggressive/Extreme → `MaxCompression` | `MainWindow.axaml:398` `SelectedIndex=0`; `CompressionStrategy.cs:58-63` | Kutu "Auto" der; **hangi kodeğe döndüğünü söylemez** | Evet (Uyumlu / En küçük) |
| 4 | Somut video kodeği | Bu kaynakta **`libsvtav1`** (bulunamazsa `libx265`); Compatible yolunda `libx264` | `PlanCalculator.cs:757-777`; ölçülen çıktı: `av1` | Hayır | Dolaylı (3 üzerinden) |
| 5 | Kodlayıcı preset'i | **`6`** (libsvtav1). Yazılım x264/x265'te `slow`, donanımda `p4`/`p6`/`medium`/`quality` | `PlanCalculator.cs:812-826`; donanım tabloları `FfmpegArguments.cs:19-51` | Hayır | **Hayır** — arayüzde hiç yok |
| 6 | Oran kipi | **2-pass VBR**, `-b:v 2026k`. CRF'e ancak bütçe CRF'i şeffaflık tavanının altında kalırsa geçilir | `PlanCalculator.cs:242-310`; argüman `FfmpegArguments.cs:146-159` | Hayır | Hayır |
| 7 | Doldurma politikası | **`FillTarget`** — bandı doldurmak için CRF düşürülür / 2-pass'e geçilir. Band 16 MB'da `[15,20 – 16,00]`, sert taban `14,40` | `PlanCalculator.cs:15`, `:25-32`; `MainWindow.axaml:439` | Evet, ama adı motor jargonu | Evet |
| 8 | Çözünürlük | **1920x1080 korundu.** İzin var (kutu açık) ama arama 1,00 ölçeği seçti; izin verilseydi taban 0,20 / 180 px | `PlanCalculator.cs:622-670`; `CompressionStrategy.cs:65,85-90`; kutu `MainWindow.axaml:406` | Kutu "düşürülebilir" der; **sonucun ne olduğunu plan satırı dışında söylemez** | Kısmen (yalnız izin verir/vermez) |
| 9 | Kare hızı | **60 fps korundu.** Aggressive rejimde düşürmeye izin var, taban 10 fps | `PlanCalculator.cs:672-691`; `CompressionStrategy.cs:67`; kutu `MainWindow.axaml:414` | Aynı | Kısmen |
| 10 | Ses kodeği | **`aac`**, her zaman yeniden kodlanır — kopyalama yolu yok | `PlanCalculator.cs:810`, `:328` | Hayır | **Hayır** |
| 11 | Ses bit hızı | **128k stereo** (mono 96k, Arşiv 160/112); kaynak bit hızı ve rejim payı (`Aggressive` → toplamın %18'i) tavan; 56k altında zorla mono | `PlanCalculator.cs:724-755`; `CompressionStrategy.cs:70-76` | Hayır | **Hayır** |
| 12 | HDR politikası | **Koru.** Kodeğin 10-bit yolu yoksa sessizce `tonemap`e düşer (hable, npl=100, bt709) | `PlanCalculator.cs:14`; `HdrResolver.cs:14-52` | Yalnız kaynak HDR'ken görünen kutu (`MainWindow.axaml.cs:1566`) | Evet, kaynak HDR ise |
| 13 | Piksel biçimi | **`p010le` istenir** (yoklama sırası `p010le` → `yuv420p10le`, `EncoderCapabilities.cs:125-131`). ffmpeg bunu sessizce `yuv420p10le`'ye çevirir — teslim edilen dosyada ölçülen biçim `yuv420p10le` | `HdrResolver.cs:51`; `FfmpegArguments.cs:163` | Hayır | Hayır |
| 14 | Anahtar kare aralığı | **`-g 120`** = `round(fps × 2)`, yani sabit 2 saniye | `FfmpegArguments.cs:162` | Hayır | **Hayır** |
| 15 | Psychovisual / tune | **`-svtav1-params tune=0:enable-variance-boost=1:variance-boost-strength=2`** (x265'te `psy-rd=2:psy-rdoq=1:aq-mode=2`, nvenc'te `-spatial-aq 1 -temporal-aq 1`) | `FfmpegArguments.cs:254-278` | Hayır | Hayır |
| 16 | Hız kipi (GPU) | **`Quality`** (yazılım). Kutu ilk açılışta kilitli gelir; donanım yoklaması karar verip ayara yazar ve bir daha üstüne yazmaz | `MainWindow.axaml:422` `IsEnabled=False`; yoklama `MainWindow.axaml.cs:1206-1262`; kural `HardwareVerdict.cs:66-101`; ayar `UpdateCheck.cs:399-400` | Kutu görünür ama kararı program verdi | Evet, karar verildikten sonra |
| 17 | Kap ve çıktı adı | **`mp4`**, `<ad>_shrunk.mp4`, kaynağın yanına, `-movflags +faststart` | `MainWindow.axaml.cs:1983-1994`, `:2356`; `FfmpegArguments.cs:185` | Hayır (yol sonuçta görünür) | **Hayır** |
| 18 | Çözme ivmesi | **`-hwaccel auto`** her girişte | `FfmpegArguments.cs:123` | Hayır | Hayır |
| 19 | Yeniden deneme | En çok **3 deneme**; hedefi aşan çıktı asla teslim edilmez, band altı kalan bir kez (ölçüm varsa iki kez) tekrarlanır | `EncodeRunner.cs:39-40`, `:71-167` | Kısmen (sonuç metninde deneme sayısı) | Hayır |

**Sayım:** 19 kararın **6'sı** kullanıcıya sorulur (1, 2, 3, 7, 8/9 izinleri, 12),
**13'ü** hiç sorulmaz. Sorulmayanların içinde kaliteyi doğrudan belirleyen üç tanesi var:
preset (5), anahtar kare aralığı (14) ve ses bütçesi payı (11).

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
`yuv420p10le`, PQ/bt2020 korunmuş, video 1948,8 kbit/s, AAC 129,2 kbit/s stereo,
60,441 s. Hata yok, uyarı yok.

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
