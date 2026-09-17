# HandBrake Dalga 2 — Danışman Yanıtı

Tarih: 17 Eylül 2026. Danışman: fable. Girdi: `.calisma/danisma/hb2-soru.md`, koşum 35158725446'nın altı belgesi
(`docs/olcumler/handbrake-kiyas-b1..b6*.md`, `handbrake-kiyas-karar10-ekran.md`), önceki yanıt, `PlanCalculator.cs`
(FillBand 89-102, RetryAimMb 1008, Correct 1017, MeasuredEncoderEfficiency 879, SearchLayout 1164, FpsCandidates 1228),
`EncodeRunner.cs` (RunAsync 101-275), `FfmpegArguments.cs` (399-427, 529-533), `CodecModel.cs` (146-183),
`tools/kalite-paketi-3/hb.ps1` (EkOlcu 68-92). Kod yazılmadı, ölçüm koşulmadı.

## Üç Ortak Kural

- **Her kol önce md5 ile kanıtlanır.** SVT-AV1 tanımadığı anahtarı sessizce yutuyor (hafıza notu). Her kolun çıktısı
  tabanla **bayt-farklı**, uydurma anahtarın çıktısı tabanla **bayt-eş** olmalı; yoksa kol "ölçülmedi" yazılır.
  x265 ve ffmpeg CLI için tersi geçerli: uydurma anahtar sürecin düşmesiyle (`exit ≠ 0`) kanıtlanır.
- **Gürültü bandı B1'in kendisi:** VMAF-NEG ±0,3, XPSNR ±0,2 dB, kbps ±%2. Yeni eşik icat edilmiyor.
- **Tek doygunluk kuralı** (Soru 3a, 3b, 5, 6b'nin kesişimi, aşağıda "Doygunluk" başlığında): bit hızı değişince
  bayt değişmiyorsa sorun bit hızı değildir; ya düzen iner (tavan üstü) ya `Saturated` teslim edilir (bant altı).

## Soru 1 — SVT-AV1 Karanlık Bantlaşma

**(a) Kol listesi.** 10 bit birincil hipotez. Dayanak: B6 aynı `karanlik` kesitinde HB'nin 10 bitlik VT çıktısı CAMBI
2,84, ürünün 8 bitlik çıktısı 9,06; yarım bit hızlı negatif kontrol bile 2,93 — fark bit hızından değil (B6:22-23).
Ama 8 bitlik HB x265 de 6,45 ile önümüzde (B1:27), yani bit derinliği tek başına kapatmayabilir; ikinci grup onun için.

| Kol | Neden | Karar |
|---|---|---|
| 8 bit taban (e0) | referans | kalır |
| 10 bit `yuv420p10le`, başka hiçbir şey değişmez | bit derinliği izole | kalır, birincil |
| 10 bit + `enable-qm=1:qm-min=0:qm-max=15` | düz alanda kuantalama matrisi; varsayılan `qm-min=8` bu QP'lerde nadiren devreye girer (doğrulanacak) | kalır |
| 10 bit + `tune=1:enable-variance-boost=1:variance-boost-strength=1` | B3'teki e1 `tune=0` ile karışıktı; VMAF kaybı tune'dan mı vb'den mi bilinmiyor | kalır |
| 10 bit + `enable-tf=0` | **yeni.** Rampa altı kolda da 23,3-23,7: dither siliniyor (B3:31). Zamansal filtre (TF) tam bunu yapar; `rampa`da anlamlı tek şüpheli | eklenir |
| 10 bit + `film-grain=8:film-grain-denoise=0` | gürültü sentezi CAMBI'yi maskeleyerek düşürür, ölçtüğü şey bant değil örtü; yalnız bilgi | kalır, yalnız 10 bit; 8 bit çeşidi düşer |
| `sharpness`, `ac-bias`, `psy-rd` | mainline SVT-AV1'de yok ya da sürüme bağlı; yutulursa boş kol ölçülür | bu dalgada yok. CI günlüğüne `ffmpeg -h encoder=libsvtav1` ve SVT sürümü yazılır; anahtar listeleniyorsa ikinci dalga |

Toplam 6 kol × 2 kesit × 2 bit hızı = 24 kodlama; B3 süreleri (21-88 sn) ile bir işte biter.

**(b) Ölçer 8 bite indiriyor — hüküm bu hâliyle geçersiz.** `hb.ps1:72` test çıktısını `format=$($rb.Pix)` ile
referansın `yuv420p`'sine swscale üzerinden indiriyor; swscale'in `sws_dither` varsayılanı `auto`, derinlik
düşürürken **dither uygular** (bayer). 10 bitlik kol ölçere girerken bedava dither alır, CAMBI'si haksız düşer.
Ürünün kendi `QualityMeter.cs:527`si de SDR'de `format=yuv420p` yazıyor; aynı tuzak, bu dalganın dışında ama not.

Düzeltme, iki okuma: **(i)** iki girdi de `format=yuv420p10le` (8→10 kayıpsız kaydırma; libvmaf CAMBI'yi zaten
10 bit iç hassasiyette hesaplar) — "10 bit ekran" okuması; **(ii)** test 8 bite `zscale=...:dither=none` ile
(kaydırma, dither yok) — "dither'sız 8 bit panel" okuması, en kötü durum. Hüküm için **ikisi de** eşiği geçmeli.

Negatif kontrol (ölçerin kendisi için, kollardan önce): kaynak 10 bite kaydırılıp (i) yoluyla kendine ölçülür →
`karanlik` 0,404, `rampa` 0,000 çıkmalı (B3:50,60); aynı kaynak 10→8 `dither=none` → yine aynı; 10→8 swscale
varsayılanla → daha düşük çıkmalı (hediyenin belgesi). Üçü tutmuyorsa kollar koşulmaz.

Ek not: `karanlik`ta 6 bit kopya CAMBI'yi yalnız +0,57 oynattı, `rampa`da 0→5,5 (B3:23-24). Eşik +1,0 film
kesitinde kaba; rampa hassas kontrol. Kazanan kol `karanlik`ta eşiği geçip `rampa`yı hiç oynatmıyorsa belge
bunu "kesit-özgü" diye yazar, genel hüküm vermez.

**(c) Seçim kuralı** — yeni sabit yok, B1'in payları kullanılır:

1. `karanlik` iki bit hızında CAMBI (iki okuma) ≤ HB x265 + 1,0 (B1: 6,45 / 6,48 → ≤ 7,45 / 7,48).
2. Kolun e0'a göre kaybı, aynı hücrede B1'in HB'ye karşı payından gürültü bandı düşülünce kalanı aşamaz.
   En dar hücre `karanlik` 2000: pay +0,74 / +0,35 dB → izin verilen kayıp VMAF-NEG ≤ 0,4, XPSNR ≤ 0,15 dB.
   600'de pay +3,88 / +1,14, bol. Kural hücre başına hesaplanır, tek sayı ezberlenmez.
3. Kodlama saniyesi ≤ 1,25 × e0 (doğrulanacak; B1'de ürün HB'nin 0,30-0,86 katı, yer var).

Şartları sağlayanlar arasında **en az VMAF-NEG kaybeden** seçilir — amaç kapıyı kapatmak, CAMBI'yi sıfırlamak
değil. Hiçbiri sağlamıyorsa değişiklik yok. B3'ün e1'i (−2,74'e −0,41) bu kuraldan kendiliğinden elenir.

**CI.** `bantlasma` işi 6 kolla; kazanan varsa `handbrake` işi 4 kesitte kazananla yeniden koşar: `parlak`,
`hareketli`, `ekran` 8/8 önde kalmalı (B1:24). Kazanan 4 kesit kapısını geçmeden ürüne girmez.

## Soru 2 — libx265 Turbo İlk Geçiş

**Karar.** Asıl kol `slow` + `slow-firstpass=0`; ön ayar değiştiren kollar ikincil. Gerekçe: x265'te kare
tipleri ilk geçişte kararlaştırılıp ikinci geçişte **yeniden kullanılır**. `veryfast` ön ayarı `ctu 64→32`,
`rc-lookahead 25→15`, `ref 4→2` değiştirir (x265 ön ayar tablosu); ilk geçişin kare tipi kararları bozulunca
ikinci geçiş onları miras alır — `hareketli` 600'deki −1,84 (B5:27) bunun imzası, hareketli içerik lookahead'e
en duyarlı olan. `slow-firstpass=0` ise x265'in kendi turbosu: yapıyı (ctu, bframes, lookahead) korur, yalnız
arama derinliğini kısar. HandBrake'in x265 `--turbo`su bu bayrağa gidiyor (doğrulanacak: `libhb/encx265.c`);
HB'nin ≤0,29 kaybı (B5:30) tutarlı.

Kollar: 2pass slow (taban); ilk geçiş veryfast (bugünkü), faster, fast, medium; slow + `slow-firstpass=0`.
`faster` ve `fast` sıralamayı görmek için kalır; bütçe daralırsa ikisi düşer.

**Eşik.** Bir kol güvenli sayılır ancak 4 hücrenin **hepsinde** tabana göre ΔVMAF-NEG ort ≥ −0,3, Δharmonik ≥ −0,5
ve ΔXPSNR ≥ −0,2 dB (gürültü bandı; harmonik B5'te −1,73'e indi, o yüzden ayrıca) **ve** iki geçişin toplam
kodlama saniyesi her hücrede ≥ %15 kısalıyorsa. %15'in dayanağı: HB'nin turbosu 5-29 arası verdi (B5:29-30),
ikisi 27-29; %15 altı kazanç için ayrı kod yolu taşımaya değmez. Sağlayanların en hızlısı seçilir; hiçbiri
sağlamıyorsa `TurboFirstPassCeilings["libx265"].Safe = false`, x264'le aynı raf.

**CI.** `turbo` işi 6 kolla, 2 kesit × 2 bit hızı; süre sütunu ilk ve ikinci geçiş ayrı yazılır (kazancın
nereden geldiği görünsün). İkinci geçiş günlüğünde `different ... than first pass` uyarısı aranır, varsa kol düşer.

**Negatif kontrol.** `-x265-params foo=1` → x265 `Unknown option`, ffmpeg `exit ≠ 0` (kol "hata" satırı).
Ayrıca `slow-firstpass=1` açıkça verilen kol tabanla **bayt-eş** olmalı: bayrağın yola girdiğinin kanıtı.

## Soru 3 — Düşük Hedefler

**(a) Çelişki yok; iki ayrı dal.** Karar 10 **bant altı** içindi: bit harcanmıyor, verim < 0,5, dosya küçük →
doygun teslim. `karanlik` 100 **tavan üstü**: bit **atılamıyor**, kodlayıcı kendi tabanında. B3:27-28 bunu ölçtü:
e0 azalan bit hızında 0,137 / 0,134 / 0,134 MB (%2 oynama), e1 98k/29k/12k'da üç kez 0,392 MB (sıfır oynama).
İkinci durumda bit hızını bir daha düşürmek boş deneme; düzen inmeli. Kural:

- **Taban tespiti:** tavan üstü ikinci deneme, istenen bit hızı ≥ %20 düşmüşken çıkan MB ilk denemenin ≥ %95'i
  ise dal "kodlayıcı tabanı". Üçüncü deneme bit hızı değil **düzen** değiştirir.
- **Sıra:** çözünürlük → ses bütçesi (varsa; B3 `-a none`) → kare hızı, o da yalnız Karar 3'ün şartıyla
  (kaynak fps'te hiçbir ölçek teslim edilebilir değilse). Çözünürlük adımı 0,02 basamak değil, orandan hesaplanır:
  `scale = sqrt(0,9 × hedef / çıkan)` (0,117/0,134 → 0,886 → 1702x724). Tabanın piksel sayısıyla ölçeklendiği
  **doğrulanacak**: `karanlik`ta sabit `-b:v 98k` ile ölçek 1,0 / 0,886 / 0,75 / 0,5 → çıkan MB, üs buradan.
- **Deneme bütçesi:** taban dalı bir deneme daha alır (3 → 4, yalnız bu dalda). Daha küçük düzen varken dosyasız
  bitilmez; dördüncü de tavan üstündeyse `CeilingExceeded` aynen kalır.

CI: `bantlasma` `karanlik` 100 e0 → ≤ 0,117 MB dosya, ≤ 4 deneme, izde "taban → düzen" dalı. Negatif kontrol:
`karanlik` 300 e0 (B3:54, ilk denemede bantta) dalı tetiklemez, 1 deneme, 1920x818; B1'in 8 hücresinde deneme
sayısı değişmez.

**(b) Rampa e1 300, %3,3 teslim — mekanizma kodda.** `MeasuredEncoderEfficiency` verim < 0,5'i `null` döndürüyor
(`PlanCalculator.cs:886`); `Correct` bunu "ölçülmedi" sayıp doğrusal ölçekliyor, `EncodeRunner.cs:158`deki ikinci
bant altı denemesi `informedByYield` istiyor, o da yok → tek denemeden sonra "under band accepted". Üstelik koşumda
bir **köşe** vardı: 300k → 0,589 MB (tavan üstü), 116k → 0,012 MB (bant altı); uçurum, doğrusal ölçek burada kör.

Kural: verim < 0,5 (kodun kendi kesme noktası; ikinci sabit yok) ise doğrusal ölçek **yapılmaz**. Aynı koşumda
tavan üstü ve bant altı örnek varsa bir kez **geometrik ikiye bölme** (√(116×300) ≈ 187k); köşe yoksa bir kez
2× bit hızı, `videoBudgetK` tavanlı. Sonuç bantta ise o, değilse en yakın bant altı `Saturated` koduyla teslim.
Taban eşiği **0,5**: hâlihazırda kodun ölçülmüş verimi attığı sınır. Doluluk %50 altında kalan teslim sessiz
geçmez, `Saturated` yazar. Öncelik düşük: e1 ürün değil; ürünün kusuru kabul etmesi, üretmesi değil.

CI: `rampa` e1 300 → doluluk ≥ %50 ya da izde bölme denemesi + `Saturated`. Negatif kontrol: e0 rampa 300 (%95,
B3:64) tek deneme kalır; e0 rampa 1200 (%76, verim 0,78 > 0,5) bu kurala girmez, olduğu gibi kalır.

## Soru 4 — FPS Düşürme

**Onay, bir keskinleştirmeyle.** Tetik "kaliteye yetmiyor" (`LayoutClearsFloor`) değil, "**teslim edilebilir değil**"
(`videoK ≥ CodecModel.UsableBitrateK` hiçbir kaynak-fps ölçeğinde tutmuyor) olmalı. Dayanak: `ceza-kalibrasyonu.md`
54 hücrenin hiçbirinde fps düşürme kazanmadı, taban altı bpp hücreleri dahil; tetik kalite tabanı olursa fps
düşürme tam kaybettiği yerden geri girer. B2:27-31: `hareketli` 300'de 16 fps −5,78 / harmonik −49,09; düşürmeyen
kol +10,99.

Uygulama: `SearchLayout` dış döngüde kaynak fps önce, bütün ölçekler taranır; bir düzen teslim edilebilirse
düşük fps adayları hiç üretilmez. Kullanıcının `MinFps`/açık isteği ve `--no-fps-drop` yolu değişmez.

CI: B2 `dusuk` `hareketli` 300 otomatik plan → 24 fps, `urun-dusurme-kapali`nin ±0,3 içinde (VMAF-NEG 65,91),
HB önünde, deneme sayısı aynı. Birim: `ceza-kalibrasyonu` 54 hücre plan düzeyinde 0 fps kararı (kodlama yok).

Negatif kontrol: sentetik 3840x2160@60, hedef `MinVideoBitrateK` (48k) civarı → kaynak fps'te hiçbir ölçek teslim
edilebilir değil → plan fps düşürür; aynı girdi `--no-fps-drop` ile → `densest` yedeği, fps kaynakta.

## Soru 5 — Social Bütçe Doldurma

**İki bulgu, iki kol.**

**(1) FillBand %3-4'ü açıklıyor, hata değil tasarım.** 7 MB hedef → alt 0,92, merkez 0,96; `RetryAimMb(null)` =
max(0,92, min(0,96, 0,988)) = **0,96**. Teslim: 6,811/7,092 = %96,0; 6,932/7,167 = %96,7; 7,066/7,334 = %96,3
(B2:94,115,136) — tam merkez. `target/1,012` kırpması burada devrede değil (0,988 > 0,96).

**(2) FillBand tek neden değil.** `urun-dusurme-kapali` `parlak` eş baytta (%0,95 sapma) hâlâ −0,53 / −0,38 dB
(B2:197). B1 eğilimi: üstünlük bit hızıyla eriyor (+2,62 @600 → +0,35 @2000 → eksi @6000). Bu bir kodlayıcı açığı,
bant açığı değil. Kol: `parlak` + `karanlik` Social 1080p60 eş baytta libsvtav1 p6 (bugünkü) / p4 / libx265 slow;
B1 kuralıyla seçim, süre ≤ 2× (p4 bedeli). `parlak` satırını kapatması en olası kol budur.

**Nişan kuralı.** İlk nişanı koşulsuz `target/1,012`e çekmem: Karar 10 ilk verimleri 1,001 / 1,009 / **1,018** ölçtü
(x264 ekran) ve %98,6 nişanda bile 50,18 MB tavanı aştı. `ToleranceOver = 1,0` (bir bayt bile üstü yeniden kodlama)
ile %98,8 nişan, verim > 1,012'de tavanı aşar — ölçülen üç verimin biri aşıyor; bedeli tam bir 2 geçiş daha,
kazancı %3-4 bayt (≈ 0,1 VMAF: −0,64'e karşı −0,53).

Kural: verim bilinmiyorsa merkez **kalır**. Üst yarı nişanı (0,98 × hedef) yalnız ilk denemeden **önce** bir verim
tahmini varsa: ürün zaten iki tur `CalibrationProbe` koşuyor. **Doğrulanacak:** sonda verimi ilk denemenin
verimini ±%1,2 (p90) içinde tahmin ediyor mu — B1/B2/social hücrelerinin `results.json` izleri elde, kodlama
gerekmez. Tutuyorsa sonda-bilgili nişan 0,98; tutmuyorsa merkez kalır ve %3-4 tasarım bedeli belgeye yazılır.

CI: `social` işi yalnız 1080p60 ön ayarıyla, 3 kesit × kodlayıcı kolları; negatif kontrol yarım bit ve
md5-eş uydurma anahtar. Sonda-verim sayımı ayrı, koşumsuz.

## Soru 6 — VideoToolbox

**Ön bulgu.** HB'nin Apple ön ayarları 1.6'dan beri `vt_h265_10bit` (doğrulanacak: `handbrake-preset-list.txt`
JSON'unda `VideoEncoder`). B6'nın imzası bit derinliği: XPSNR açığı içerik ve bit hızından bağımsız −0,54…−1,17
(B6:20), `karanlik` CAMBI 9,06'ya karşı 2,84, yarım bitli negatif kontrol bile 2,93 (B6:22-23). 10 bit birincil kol.

**(a) maxrate/bufsize — kol değişkeni, akılla karar yok.** ffmpeg `videotoolboxenc` `rc_max_rate`/`rc_buffer_size`i
`kVTCompressionPropertyKey_DataRateLimits`e bağlar (doğrulanacak, brew ffmpeg sürümünün kaynağı); HB'nin VT ABR'si
yalnız `AverageBitRate` verir (doğrulanacak, `encvt.c`). Sınır VT'de tutuyorsa 1,5× tepe karanlık filmde bedel
öder. Kollar: sınırlı (bugünkü) / sınırsız — fark cevaptır.

**(b) VT'ye ayrı verim düzeltmesi yok.** `ekran` bant altı doygunluk: Karar 10 tablosunda VT 5500'de iki farklı
istek aynı 0,695 MB'ı verdi — verim bit hızına cevap vermiyor; B6'da `ekran` +8 / +9 önde. Aynı doygunluk kuralı
(verim < 0,5 → yeniden deneme yok, `Saturated`) yeter; onun dışında yalnız ölçülmüş verimle `Correct`.
`IsHardware` T149 gereği kapalı kalır; ama `NeedsTwoPasses` VT'de iki tam kodlama yaptırıyor ve `-pass` yok
sayılıyor (doğrulanacak: toplam 26-40 sn'ye karşı kodlama 5 sn, B6:27 — ilk geçişin payı). NVENC sabitlerini
taşımadan ayrı bir `SinglePassRateControl(codec)` kapısı: VT için tek geçiş. `-preset slow` `hevc_videotoolbox`ta
seçenek değil; ffmpeg yalnız "not used for any stream" uyarır (doğrulanacak: `vt-karanlik-2000-urun.log`).

**(c) Kollar** (macos-15, 4 kesit, 2000/5500, HB VT ile eş bayt):

| Kol | Ne izole eder |
|---|---|
| bugünkü ürün (2 geçiş, preset, maxrate) | referans |
| 8 bit tek geçiş `-b:v` | geçiş etkisi (VT deterministik değil; kalite eş beklenir, bayt değil) |
| 10 bit tek geçiş `-profile:v main10 -pix_fmt p010le` | bit derinliği — birincil |
| 10 bit, maxrate/bufsize yok | (a) |
| 10 bit + `-prio_speed 0` | hız/kalite önceliği; `prio_speed 1` düşer, hız sorulmuyor |
| 10 bit + `-spatial_aq 1` | yalnız `ffmpeg -h encoder=hevc_videotoolbox` listeliyorsa (7.1+) |
| eksik: `-allow_sw 0` | koşucuda yazılım VT'ye düşmediğinin kanıtı; her kola eklenir, ayrı kol değil |

Hüküm B1 kuralıyla HB VT'ye karşı: 6 film hücresi bantta ya da önde, `ekran` 2 hücre önde kalır, süre ≤ 1,5 × HB VT.
Şartı sağlayan **en küçük değişiklik kümesi** ürüne girer.

**Negatif kontrol, iki katlı.** Uydurma seçenek `-foo 1` → ffmpeg `Unrecognized option`, `exit ≠ 0`. Ama başka
kodlayıcının tanıdığı seçenek (`-preset`) yalnız uyarır — o yüzden ikinci kontrol: bugünkü kolun günlüğünde
`-preset` için "not used" uyarısı **aranır ve bulunur**; bulunmazsa "preset ölü" iddiası düşer. 10 bit kolunda
`ffprobe` `pix_fmt=yuv420p10le`/`p010le` ve `profile=Main 10`; değilse kol "8 bit üretti" diye düşer.

## Doygunluk — Tek Kural

| Belirti | Tespit | Sonraki adım |
|---|---|---|
| Tavan üstü, bit hızı düşünce bayt inmiyor | 2. deneme MB ≥ %95 × 1., istek ≥ %20 düştü | düzen iner (çözünürlük → ses → fps Karar 3 şartıyla); +1 deneme |
| Bant altı, bit hızı artınca bayt çıkmıyor | verim < 0,5 | doğrusal ölçek yok; köşe varsa bir bölme, yoksa bir 2×; sonra `Saturated` teslim |
| Bant altı, verim 0,5-1 | mevcut yol | ölçülmüş verimle `Correct`, değişmez |

Kodlayıcıya özel sabit yok; SVT-AV1, x265, VT aynı tabloyu okur. `Saturated` arayüzde kusur değil bilgi
(önceki yanıt, Karar 10).

## Sıra ve Doğrulanacaklar

Önce bit derinliği (Soru 1 ve 6): ikisi aynı hipotez, biri Soru 5'in `karanlik` XPSNR −0,25'ini de kapatabilir.
Sonra Soru 2 ve 4 (bağımsız, paralel). Soru 3 ve 5'in nişan kuralı koşumsuz sayımla başlar.

Doğrulanacaklar listesi, hiçbiri ölçülmedi: swscale dither hediyesi (ölçer kontrolü ile), 10 bit süre bedeli
≤ 1,25×, SVT tabanının piksel üssü, sonda veriminin ilk verimi ±%1,2 tahmini, HB `--turbo` ↔ `slow-firstpass`,
HB VT ön ayarının 10 bit olduğu, ffmpeg VT'de `DataRateLimits` eşlemesi, `-preset`/`-pass`'ın VT'de ölü olduğu,
`spatial_aq`nun listelenmesi, `qm-min=0`un bu QP'lerde fark yaratması.
