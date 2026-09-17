# Fable Kararları — 17 Eylül 2026 (On Soru)

Danışman: fable. Girdi: `acik-durumu-2026-09-17.md` (D1-D4, 2. bölüm), `yol-haritasi-kalanlar-2026-09-17.md`
(7. bölüm 16-19), `handbrake-yanit.md` (önceki on karar). Kod yazılmadı, ölçüm koşulmadı. Her karar
`docs/olcumler` altındaki bir sayıya dayanıyor; dayanağı ölçülmemiş bir şeyse "doğrulanacak" yazıyor.

Ölçüt sırası: önce kullanıcı kolaylığı, sonra kalite, sonra bakım. Gürültü bandı B1'in: VMAF-NEG ±0,3,
XPSNR ±0,2 dB, kbps ±%2. "Yerel ölçüm" = kısa, tek seferlik, kullanıcı PC'sinde (stres yok).

## Okunan Ölçümler

| Belge | Dayanak olduğu soru |
|---|---|
| `nvenc-2.md`, `nvenc-gop10.md`, `nvenc-tamkare.md`, `butce-doldur.md` | 2 |
| `handbrake-kiyas-b7-aciklar.md` (Açık 1, 7, Ölçer Düzeltmesi), `videotoolbox.md`, `videotoolbox-baglama.md` | 1, 3, 4 |
| `karanlik-x265.md`, `handbrake-kiyas-hb2b.md`, `danisma/2026-09-17-karanlik-x265-fable.md` | 4 |
| `danisma/2026-09-17-hb2b-yanit-fable.md` (C.3 merkez), `PlanCalculator.RetryAimMb` | 3 |
| `libmpv-macos-gomme.md` (minos=15.0, 47/48 dylib) | 5 |
| `whatsapp-karanlik.md` | 10 |
| `docs/plan-duzenleyici.md` (D0-D5, çatal 1-2), `docs/arastirma/ikon-estetigi.md` §5, `docs/plan.md` Paket 2b md. 3/6, İş 14 md. 6 | 7, 8, 9 |

---

## 1. VideoToolbox plan yoluna — Hızlı kipin donanım kapısıyla, HB eşitlik kapısıyla değil

**Karar.** `hevc_videotoolbox` plan yoluna **girer**, ama yalnız NVENC'in bugün girdiği yerden: macOS'ta
**Hızlı kip** ve **açık kodek seçimi**. Otomatik + Dengeli/Kaliteli yazılımda kalır. `h264_videotoolbox`
girmez (HDR10 taşımıyor, p10 açığı daha büyük; `videotoolbox.md` K3/K2). Kapı, önceki karar 2'nin
donanım tanımıyla aynı: **hız yolu, kalite iddiası dışında.** XPSNR ≥ −0,2 HB eşitlik kapısı yazılım
iddiasının kapısıdır; VT'ye uygulanmaz.

**Gerekçe.** B7 Açık 1: HB VT'ye karşı VMAF-NEG 6/6 önde, ekran 2/2 ≥ +0,3, çıplak kodlama en kötü 1,36×.
XPSNR(ii) 2/6, toplam −1,11 dB, karanlik 2000 −0,231 — kapının 0,03 dışında. Mac kullanıcısı için
gerçek fark: M1'de SVT-AV1 yazılım yolu dakikalar, VT saniyeler; "Hızlı" kipin Mac'te donanımsız kalması
kullanıcı kolaylığı ölçütünde en büyük açık. Kalite satırı 7 "gerideyiz" kalır ama **hız yolu** satırı 15
kapanır; iki satır birbirine karışmaz.

**Kabul ölçütü.** (a) `PlanParser.AllowedCodecs`'e `hevc_videotoolbox` yalnız çalışma zamanı yoklaması
(`EncoderCapabilities`) onu bulunca girer; Windows/Linux'ta parser hâlâ reddeder (mevcut pim
`ParserStillRejectsVideoToolboxEncoders` platform koşullu olur, silinmez). (b) Saf Core testi: macOS + Hızlı +
VT var → `hevc_videotoolbox`; Otomatik/Dengeli → değişmez (negatif); WhatsApp kilidi → `libx264` kalır.
(c) CI `macos-15` `vt` işi, 6 film hücresi + 2 ekran: VMAF-NEG ≥ −0,3 HB VT'ye karşı 8/8, bant isabeti 8/8,
tavan aşımı 0, çıplak kodlama ≤ 1,5× HB VT, uydurma anahtar negatifi. XPSNR(ii) tabloya girer, hükme girmez.
(d) `acik-durumu` satır 15 "eşit (VT hız yolu)", satır 7 XPSNR notuyla kalır.

**Sahip dosyalar.** `src/VidShrink.Core/PlanParser.cs:13`, `src/VidShrink.Core/CodecModel.cs:159-172`
(`IsHardware` VT'yi dışarıda tutmaya devam eder; Hızlı kip seçimi ayrı üye), `src/VidShrink.Core/PlanCalculator.cs`
(Hızlı kip donanım seçimi), `src/VidShrink.Ffmpeg/EncoderCapabilities.cs`, `tests/.../PlanParserTests.cs`,
`tests/.../PreviewSegmentTests.cs` (sınıflandırma), `tools/kalite-paketi-3/hb.ps1` (`vt`).

## 2. NVENC — kalite açığı kapalı (bant içi), boş bütçe için tek aday: donanımda yüksek nişan + aşağı düzeltme

**Karar.** hevc −0,20 / av1 −0,10 **bant içi** (±0,3); üç kapı (gop10, tam kare, la20) ve AQ taraması
kaldıktan sonra kalan anahtar yok. Kalite işi **kapanır**; donanım yolu karar 2'deki gibi iddianın dışında,
satır 6 "eşit" kalır. %4,8 boş bütçe için **aday 3**: donanımda ilk nişan bant merkezi (tavanın %96'sı)
değil `Aim` (%98,5); tavan aşarsa **aşağı** bir deneme. Aday 1'in tersi yön.

**Gerekçe.** `butce-doldur.md` aday 1: yukarı deneme NVENC'in basamaklı yanıtına takıldı (istek +%2,6,
boyut 0; istek +%2,6, boyut +%4,8), %3'lük tek pencere dar. Ama NVENC kodlaması kesit başına 1-3 sn
(`nvenc-2.md`); donanımda fazladan deneme **ucuz**, yazılımda pahalı. Bu asimetri kuralı tersine çevirir:
yazılımda alçak nişan + bir yukarı (bugünkü `BudgetFill`), donanımda yüksek nişan + bir aşağı. `CeilingGuard`
tavan üstünü zaten teslim etmiyor. Boş bütçenin kaynağı merkez nişan kuralının kendisi (%96); aday 3 donanımda
o kuralı değiştirir, yazılımda dokunmaz (C.3 kararı ayakta, soru 3).

**Kabul ölçütü (ölçümden önce yazılır).** Yerel NVENC ölçümü, `nvenc-2` ile aynı 26 donanım hücresi:
(1) teslim/hedef ortalaması −%4,8 → **≥ −%2,5**; (2) teslim taşması 0 (tavan bekçisi); (3) ortalama deneme
≤ 2,0; (4) hiçbir hücrede VMAF-NEG öncekine göre < −0,3. Biri kalırsa kod geri alınır, `butce-doldur.md`
"donanımın basamaklı yanıtı, kapalı" yazar ve bütçe konusu **bir daha açılmaz**. Negatif kol: yazılım
hücreleri byte-aynı kalır (md5).

**Sahip dosyalar.** `src/VidShrink.Core/PlanCalculator.cs:1052-1087` (`RetryAimMb`/ilk nişan, satıcıya göre),
`src/VidShrink.Core/CodecModel.cs` (donanım nişan sabiti), `src/VidShrink.Core/BudgetFill.cs` (değişmez, test pimi),
`src/VidShrink.Ffmpeg/EncodeRunner.cs` (aşağı deneme kolu), `tools/VidShrink.Bench`, `docs/olcumler/butce-doldur.md`
(aday 3 bölümü).

## 3. Social 1080p60 ~%96 dolum — merkez nişan kuralı değişmez

**Karar.** Değişmez. Yazılımda merkez nişan (C.3, iki kez veriyle onaylandı: ilk denemelerin 17/42'si ±%1,2
içinde) kalır; Social'a özel nişan yok; 135,6 sn ikinci deneme kabul edilir.

**Gerekçe.** B7 Açık 7: eş baytta svt-p4 parlak −0,24 (bant içi); ürünün −0,35'i %4,8 az bayttan geliyor,
fark 0,11 puan, gürültünün üçte biri. Social'ın tavanı platform tavanı (25 MB Discord/WhatsApp): taşan dosya
kullanıcı için **reddedilen yükleme**, 110 sn fazla bekleme ondan küçük bedel. Yüksek nişan riski donanımda
ucuz (soru 2), yazılımda değil. Ayrıca `BudgetFill` (`Floor` 0,97) bu hücreyi zaten %98,5'e çekiyor; kalan
iş yok.

**Kabul ölçütü.** Değişiklik yok; B3 ölçütü sürer: 36/36 taşma yok, deneme ≤ 2, Social p4 hücreleri
HB'ye karşı bant içi ya da önde. `acik-durumu` satır 12'deki "kalan: ~%96 dolum" notu "tasarım gereği,
C.3" diye kapanır.

**Sahip dosyalar.** Yalnız belge: `.calisma/hb3/acik-durumu-2026-09-17.md` satır 12,
`docs/olcumler/handbrake-kiyas-b7-aciklar.md` Açık 7 kapanış cümlesi.

## 4. Karanlık x265 geçişi — kapsam ölçümle genişler (yalnız Dengeli), hüküm dither'sız 8 bit izleyiciye

**Karar.**
- **MaxCompression ve kodek kilidi asla.** Kullanıcı açıkça en küçük dosyayı ya da kodeği seçti; x265
  VMAF-NEG'de e0'ın −3,29/−0,40 altında (`karanlik-x265.md`), bu bedel istenmeden ödenmez.
- **Dengeli (Otomatik) karanlıkta bugün libx264 8 bit.** Ölçülmedi. Genişleme **politikayla değil kapıyla**:
  CI'da karanlik × {600, 2000} Dengeli kolu CAMBI(ii) ölçülür; > 7,5 ise `DarkContentSwitch` rejim kümesine
  `Balanced` eklenir (hedef yine libx265 10 bit, turbo), ≤ 7,5 ise kapsam bugünkü gibi kalır ve belge yazar.
- **Luma eşiği 44 değişmez** (dört kesitin geometrik ortası, 1,5× pay; veriden türedi).
- **CAMBI hükmü dither'sız 8 bit okumaya (ii).** libmpv'nin dither'lı çıkışı bilgi sütunu, kapı değil.

**Gerekçe.** Çıktı paylaşılıyor: WhatsApp, Discord, tarayıcı, TV. Ürünün oynatıcısı azınlık izleyici;
kendi kontrolümüzde olmayan panel için hüküm verilir, en kötü durum (ii)'dir. Ayrıca hb2b ölçer kontrolü:
`dither=ordered` gürültüsüz gradyanda CAMBI'yi düşürmedi (19,73 → 20,49), yani dither'lı okuma
doğrulanmış bir ölçü bile değil. B7 Ölçer Düzeltmesi'nin (ii) seçimi böyle kapanır.

**Kabul ölçütü.** CI `karanlikgecis` işine Dengeli kolu: karanlik 600/2000 günlükte kodek, CAMBI(ii),
VMAF-NEG, süre. Genişleme geçerse: Dengeli+karanlık → libx265 CAMBI(ii) ≤ 7,5 2/2, süre ≤ 2× libx264 kolu,
parlak/hareketli/ekran Dengeli 2000 kodek değişmez (negatif, md5 eş). Saf Core testleri: `SafKararHerKoluAyriTutar`
rejim kümesi için güncellenir; MaxCompression+karanlık → libsvtav1 pimi kalır. Belge `karanlik-x265.md`
"Dengeli" bölümü ve izleyici kararı bir cümle.

**Sahip dosyalar.** `src/VidShrink.Core/DarkContentSwitch.cs:15` (rejim kümesi), `src/VidShrink.Core/PlanCalculator.cs:333-338`,
`tests/.../DarkContentSwitch*` (mutasyon tablosundaki beş test), `tools/kalite-paketi-3/hb.ps1` (`karanlikgecis`),
`docs/olcumler/karanlik-x265.md`, `docs/olcumler/handbrake-kiyas-b7-aciklar.md:29`.

## 5. macOS 13-14 — kendi derleme hayır; tek sınırlı deneme: MPVKit xcframework, tutmazsa macOS 15+ kalır

**Karar.** libmpv + 48 bağımlılığı `MACOSX_DEPLOYMENT_TARGET` ile CI'da **kendimiz derlemeyiz**. İlan
bugün **macOS 15+** kalır. Ayrı, sınırlı bir CI denemesi açılır: `libmpv-macos-gomme.md`'nin "denenmedi"
dediği hazır kaynak **MPVKit xcframework** (evrensel arm64+x86_64, düşük minos; sürüm ve lisans kolu
indirirken doğrulanacak). Tutarsa 13+ ya da 14+ ilan edilir, tutmazsa satır 57 "bilerek: hazır ikili yok"
diye kapanır.

**Gerekçe.** Tek kullanıcı Windows'ta; macOS 13-14 kimseye kolaylık getirmiyor, bakım ölçütünde en pahalı
kalem (48 dylib kaynak derlemesi, imza/notarizasyon zaten denenmedi, tedarik pimleme yok). MPVKit ise aynı
belgedeki üç açığı (minos, evrensel değil, boyut) tek kaynakla kapatma ihtimali taşıyor; deneme CI'da,
sıfır yerel bedel.

**Kabul ölçütü.** CI işi: MPVKit'ten dylib'ler, `otool -l` ile 48/48 `minos ≤ 14.0`, `lipo -info` iki
mimari, mevcut `macos-libmpv-gate.yml` oynatıcı testleri `macos-15` **ve** (etiket varsa) `macos-14`
koşucuda yeşil, paket boyutu belgeye. Biri kalırsa kod değişmez; `docs/kurulum.md:91` ve `kurulum.tr.md:72`
15+ kalır.

**Sahip dosyalar.** `.github/workflows/macos-libmpv-gate.yml`, `.github/workflows/release.yml` (macOS kolu),
`tools/` kurucu betiği (macOS libmpv indirme), `docs/olcumler/libmpv-macos-gomme.md` (yeni bölüm),
`docs/kurulum.md`, `docs/kurulum.tr.md`.

## 6. P28 altyazı indirme — yapılır: OpenSubtitles.com REST, kullanıcı kendi anahtarını girer, önce hash

**Karar.** Yapılır, en küçük dilim: `ISubtitleProvider` + tek sağlayıcı **OpenSubtitles.com REST API**.
Anahtar **kullanıcının** (Ayarlar'da tek alan, ayar dosyasında, depoda değil). Arama sırası: moviehash
(OpenSubtitles hash, dosya boyutu + ilk/son 64 KB) → ad+süre. Dil: arayüz dili, sonra İngilizce. İndirilen
dosya videonun yanına `<ad>.<dil>.srt` yazılır ve mevcut P28 yandaki-altyazı yolu onu kendiliğinden yükler.
Anahtar yoksa menü öğesi "anahtar nereden alınır" der ve sayfayı açar; hesap açma kullanıcının işi.

**Gerekçe.** GOM paritesi kullanıcının kendi maddesi. Açık AGPL depoya gömülü uygulama anahtarı herkesin
kotasını paylaşır ve iptal edilir; kullanıcı anahtarı tek doğru yol. Hash araması tam eşleşme verir (GOM'un
hissettirdiği "kendiliğinden buldu" davranışı), ad araması yedek. İndirme için kullanıcı oturumu gerekip
gerekmediği ve günlük kota **doğrulanacak** (API belgesinden, koddan önce); gerekiyorsa kullanıcı adı/parola
aynı ayar panelinde, saklama işletim sisteminin anahtar deposunda.

**Kabul ölçütü.** (a) Saf test: bilinen 10 MB sentetik dosyada moviehash sabit değeri (referans
uygulamayla üretilip pimlenir). (b) Sahte HTTP ile: arama isteği hash+dil taşır, ilk sonuç indirilir,
`.tr.srt` yazılır, `PlayerView.Tracks` yükler. (c) Bütünleşik test yalnız `VIDSHRINK_OPENSUBTITLES_KEY`
varken koşar, yoksa atlanır (CI'da atlanır, yerelde bir kez yeşil). (d) 42 dilde üç metin. (e) Anahtar
yokken hata değil yönlendirme.

**Sahip dosyalar.** Yeni `src/VidShrink.Player/Subtitles/{ISubtitleProvider,OpenSubtitlesProvider,MovieHash}.cs`,
`src/VidShrink.App/Playback/PlayerView.Tracks.cs:30-32`, `src/VidShrink.App/Playback/PlayerView.axaml.cs:430-449`
(menü), Ayarlar paneli + ayar modeli, `src/VidShrink.App/Locales/*/main.json`, `tests/.../AltyaziIndirmeTests.cs`.

## 7. R12 — "hayır" duruyor: modelli arka plan ayırma yok, kayıt sürerken yükleme yok

**Karar.** İkisi de hayır, bu turda ve sonrakinde. Yeniden açma koşulu tek: kullanıcı webcam bindirmesini
gerçekten kullandığını ve `chromakey`/`backgroundkey`'in yetmediğini söylerse. O gün bile ilk aday ONNX değil,
işletim sisteminin kendi kamera efekti (Windows Studio Effects / macOS Continuity) — ürün dışı, sıfır bakım.

**Gerekçe.** Modelli ayırma: onnxruntime yerel ikili (RID başına on MB'lar), model dağıtımı, ffmpeg
grafiğinin dışında kare başına çıkarım yolu (standart ffmpeg'de dnn filtresi yok) — L+ boy, üç platform,
tek kullanıcının kullanmadığı özellik. Kayıt sürerken yükleme: MP4 bitene kadar `moov` yok, boyut bitene
kadar bilinmiyor, storage.to bitmiş dosya alır; kazanç birkaç saniye, bedel yarım dosya yükleme hataları.

**Kabul ölçütü.** Yok (karar). `docs/plan-kaydedici-dalgalari.md:117-119` bayat durum sütunu güncellenirken
bu iki madde "kararla kapalı, koşul: kullanıcı talebi" yazar (yol haritası kalem 11 zaten bunu yapıyor).

**Sahip dosyalar.** `docs/plan-kaydedici-dalgalari.md:112-119`.

## 8. K19 Editör — ayrı tur; bu turda yalnız D0 (hipersürüş G kapandıktan sonra); ilk dilim D0+D1+D2+D4 ve şeritsiz D3

**Karar.** Editör **ayrı tur**. Çatal 2 kararı ("önce hipersürüş") ayakta; K15 hâlâ 595 ms. Bu turda
K19'dan yalnız **D0 ortak odak nesnesi** (`CurrentMedia` + "kim değiştirir" ayarı), o da hipersürüş G
grubu `MainWindow.axaml.cs`'i bıraktıktan sonra; aynı dosyaya iki el değmesin. Editörün **en küçük faydalı
dilimi** (sonraki tur): D1 kesim listesi + D2 `edl://` önizleme + D4 teslim (smart cut varsayılan, hızlı/tam
yanında) + **şeritsiz D3**: oynatıcının mevcut ilerleme çubuğunda I/O işaretleri, "aralığı at" listesi,
"Dışa aktar". Hız ve geri yön bu dilimde yok (teslimde bile), ikinci dilim.

**Gerekçe.** Kullanıcının cümlesindeki ilk fiil "kes, birleştir, kaldır"; hız ve −100 sonra ve ölçülmüş
kısıtlarla zaten sınırlı. Şerit (D3) planın en büyük kalemi ve belirteç kararı istiyor; işaret tabanlı
kesim aynı işi bugünkü çubukla yapar, D3 şeridi sonra üstüne oturur. D0 bugünkü altı sekmeye tek başına
değer katıyor (çift ffprobe kalkar) ve editörün ön koşulu.

**Kabul ölçütü.** D0: oynatıcıdan Küçült'e geçişte ffprobe **bir** kez (sahte yoklayıcı sayacı), ayar
kapalıyken Küçült sekmesinin dosyası kayıt bitince değişmez, açıkken değişir. İlk dilim: 10 sn sentetik
kaynakta iki aralık atılır → çıktı süresi kaynak − aralıklar (smart cut ±1 kare, hızlı ±1 GOP), gövde
paketleri kopya (ffprobe `-show_packets` bayt eş), mpv'de açılır; geri al/ileri al komut yığını testi.

**Sahip dosyalar.** D0: yeni `src/VidShrink.App/CurrentMedia.cs`, `MainWindow.axaml.cs:2726,2760-2767`,
`Recorder/RecorderView.axaml.cs:117,150`, ayar modeli. Dilim: `src/VidShrink.Core/Edit/{EditClip,EditTimeline}.cs`,
`src/VidShrink.Player/IPlaybackEngine.cs:80` (`edl://`), `Playback/PlayerView.Serit.cs` (işaretler),
`src/VidShrink.Core/EditExportArguments.cs`, testleri. Plan: `docs/plan-duzenleyici.md` "İlk dilim" bölümü.

## 9. Fluent simgeler evet; K20 pp standardı depoya taşınabildiği ölçüde; S12 Opus kapalı

**Karar.**
- **Fluent UI System Icons (MIT), Filled, 20/24 px** — 26 yolun tamamı bir dalgada değişir; K4'ün dişli ve
  `<>` (Fluent `code`) simgeleri bu dalgada kapanır. Yol haritası "karar kullanıcının" diyordu; bu belgede
  karar verilmiştir, kullanıcı itiraz ederse geri alınır (tek dosya).
- **K20**: standart depo dışında (`pp` rafı, özel depo `private/`), depodan doğrulanamaz. T0 raftan
  **ölçülebilir** kuralları (bar yüksekliği, iç boşluk, yarıçap, hizalama) `docs/tasarim/kabuk-standardi.md`
  (≤ 40 satır, Türkçe) olarak depoya alır ve tek pin testine bağlar; rafta ölçülebilir kural yoksa K20
  "doğrulanamaz, kapalı" olur. Raf okunmadan kod yazılmaz.
- **S12 Opus kapalı.** MP4'te AAC varsayılan kalır; Opus yalnız MKV/WebM yolunda (mevcut). Yeniden açma
  koşulu: iOS/WhatsApp gerçek cihaz ölçümü; CI'da yok, bu yüzden pratikte kapalı.

**Gerekçe.** `ikon-estetigi.md` §2 ölçtü: bugünkü 26 yolda optik boy ekseni yok, tepe yarıçapı yok, 1,5
kalınlık yanlış ağırlıkta — bu estetik değil, ölçülmüş kusur. Fluent Windows 11'de yerli duran tek set,
tek `<path>` dolgu = `StreamGeometry`'ye doğrudan (§4), elle çizim bakımı biter. K20 için depoda kanıt
üretilemeyen iş "yapıldı" sayılamaz; önce standart depoya girer.

**Kabul ölçütü.** Simgeler: `Themes/Icons.axaml`'da 26/26 geometri Fluent kaynağından, `THIRD-PARTY-NOTICES`
MIT metni; test: her geometri boş değil ve sayı 26; başsız ekran görüntüsü %100/%150/%200'de kırpılan simge
yok (`BiciminTests` deseni). K20: `kabuk-standardi.md` var ve her kuralı bir ölçü testi okuyor. Opus:
değişiklik yok, `docs/plan.md` İş 14 md. 6 "kapalı, koşul: gerçek cihaz" cümlesi.

**Sahip dosyalar.** `src/VidShrink.App/Themes/Icons.axaml`, `THIRD-PARTY-NOTICES.md`, `tests/.../IconsTests.cs`,
`docs/tasarim/kabuk-standardi.md` (yeni), `docs/YOL-HARITASI.md:12`, `docs/plan.md:644`.

## 10. WhatsApp karanlık — önce bir gerçek gönderim ölçülür, taklit ona kalibre edilir; şimdilik "belge olarak gönder" ipucu

**Karar.** Üç adım, sırayla.
1. **Gerçek gönderim, bir kez, kullanıcının telefonuyla** (hesap = kullanıcının; kural buna izin verir):
   WhatsApp "kendine mesaj" sohbetine üç dosya — (a) bugünkü WhatsApp çipi çıktısı (karanlık kaynak),
   (b) aynı dosya **belge olarak**, (c) aynı kaynak WhatsApp'ın kendi hedef geometrisinde (adım 1'in ffprobe'u
   söyleyecek; ilk turda 720p/H.264 High/AAC tahmin, doğrulanacak). Alınan üç dosya PC'ye geri gelir; ffprobe
   (çözünürlük, kbps, profil/seviye, GOP, `color_range`, `pix_fmt`) ve kaynağa karşı karanlık PSNR, ton
   oranı, kayma, CAMBI(ii) `docs/olcumler/whatsapp-gercek.md`'ye.
2. **Taklit düzeneği** `tools/whatsapp-taklit/`: adım 1'in ffprobe'undan libx264 argümanları (geometri,
   kbps, profil, GOP, aralık etiketi). Taklidin **doğrulaması**: taklit çıktısı ↔ gerçek alınan dosya karanlık
   PSNR ±0,5 dB, boyut ±%15. Geçmezse taklit kapı olmaz, yalnız gerçek gönderim sayılır.
3. **Çıktı hedefi** adım 1'e göre, aday sırası: (i) renk aralığı/etiket — bugün `FfmpegArguments` çıktıya
   `color_range`/`colorspace` etiketi **yazmıyor** (`:452-470` arası yalnız `-profile:v`, `+faststart`);
   ekran kaydı bgra→yuv420p dönüşümünde etiketsiz dosya ikinci kodlayıcıda yanlış aralık okunursa gölge
   ezilir — hipotez, adım 1'in ffprobe'u yanıtlar; (ii) WhatsApp'ın kendi geometri/kbps'inde teslim
   (ikinci kodlama kendine benzer girdiyi daha az bozar; ölçülür); (iii) hiçbiri tutmazsa çipin ipucu.
   **Bugün, ölçümden bağımsız**: WhatsApp çipinin ipucuna "yeniden kodlanmasın istiyorsan belge olarak
   gönder" cümlesi (42 dil) — sıfır risk, kullanıcının şikâyetine anında cevap.

**Gerekçe.** `whatsapp-karanlik.md` ürünün x264 tarafını kapattı (aq-mode 3 ±0,07 dB) ve "sunucu tarafı
yeniden kodlama ölçülmedi" yazdı; kullanıcının gördüğü şey tam o ölçülmemiş yarı. Gerçek gönderim olmadan
taklit uydurma olur; bir gönderim ise hesap dışında hiçbir şey istemiyor. VMAF-NEG burada vekil değil
(aynı belge), kapı karanlık PSNR + kayma.

**Kabul ölçütü.** Adım 1 belgesi üç dosya için tam tablo. Adım 2 taklit doğrulaması geçti/kaldı yazılı.
Adım 3'te seçilen ayar: **gerçek alınan** dosyada karanlık PSNR bugünkü çipe göre ≥ +0,5 dB ya da kayma
≤ yarısı; belge yolu için alınan dosya md5 eş. Çip ipucu: 42 dilde, `BiciminTests` kırpma yok.

**Sahip dosyalar.** `docs/olcumler/whatsapp-gercek.md` (yeni), `tools/whatsapp-taklit/kos.ps1` (yeni),
`src/VidShrink.Core/FfmpegArguments.cs:440-470` (etiketler / WhatsApp profili), `src/VidShrink.Core/StreamMapping.cs`
(gerekirse), `src/VidShrink.App/MainWindow.axaml:297-355` (çip ipucu), `Locales/*/main.json`.

---

## Kullanıcıya Kalan (hesap / cihaz gerektirenler)

1. **Soru 10 adım 1**: WhatsApp'ta kendine üç dosya gönderip alınanları PC'ye geri almak.
2. **Soru 6**: OpenSubtitles.com'da hesap ve API anahtarı (ürün anahtarı kullanıcıdan alır).
3. **Soru 2**: 26 hücrelik kısa yerel NVENC ölçümü (kodlama < 1 dk, VMAF birkaç dk) — izin daha önce
   verilmiş sayılır ("kısa yerel ölçüm izinli"), haber verilerek koşulur.

Bildirim (soru değil): Fluent geçişi ve VT'nin Hızlı kipe girmesi bu belgeyle kararlaştırıldı; itiraz
gelirse ikisi de tek dalga geri alınır.
