# HandBrake Açık Analizi — Danışman Yanıtı

Tarih: 17 Eylül 2026. Danışman: fable. Girdi: `docs/handbrake/acik-analizi.md` (63 satır) ve
`docs/olcumler` altındaki ölçümler. Kod yazılmadı, ölçüm koşulmadı. Her kararın yanında dayandığı ölçüm
var; ölçülmemiş bir şey dayanak yapıldıysa "doğrulanacak" diye işaretlendi.

## Okunan Ölçümler

| Belge | Bu yanıtta neye dayanak |
|---|---|
| `origin/main:docs/olcumler/av1-esbayt.md` | e0 18/18 önde; variance boost VMAF-NEG'i −2,3…−2,6 düşürüyor; "öznel bantlaşma ölçülmedi" |
| `origin/main:docs/olcumler/av1-e0-hedef-bant.md` | Film 6/6 ilk denemede bantta; ekran 3/3 bant altı, 100 MB'a 44,3 MB; 360 sn'de 2. deneme tavanı aştı |
| `origin/main:docs/olcumler/handbrake-kesit-turu.md`, `handbrake-acigi-yazilim.md` | Ürün yolu ↔ HB x265 slow, kesit türüne göre; `parlak` karanlık PSNR −0,91…−1,40 (e1 dönemi) |
| `origin/main:docs/olcumler/ceza-kalibrasyonu.md` | 9/9 grupta model iyimser; fps sapması −5,4…−17,8; ölçek −1,4…−6,4; tutulan kesit doğrulaması geçmiyor |
| `docs/olcumler/ab-duzenegi.md:375-470` | Düşürülen 5 çiftin 5'inde kayıp, düşürülmeyen tek çiftte kazanç (eski motor) |
| `origin/main:docs/olcumler/whatsapp-karanlik.md` | VMAF-NEG karanlık gölge kalitesinin vekili değil (AQ kapanınca VMAF-NEG +2,4…+6,7, karanlık PSNR düştü) |
| `docs/olcumler/siyah-kenar.md` | Kırpma p10 kazancı 0,083 (< +0,30); kenarsız kaynakta yanlış kırpma 0/20; gürültülü bant hiç tetiklenmiyor |
| `docs/olcumler/videotoolbox.md` | Apple M1'de `hevc_videotoolbox` p10'da x265'in 6,4…33,4 altında |
| `docs/gpu-kodlama-bulgusu.md` | Kullanıcı PC'sinde (RTX 5070 Ti) `av1_nvenc` x265 slow'a 0,8 VMAF yakın, 7 kat hızlı; NVENC hedefin %12 altında kalıyor |
| `docs/olcumler/handbrake-acigi.md` | 8,79 açığı bayat; HB turbo %16,6 süre kazandırdı; HB comb-detect progressive kaynakta faydasız |

## Önce Bir Ön Koşul: Dalga 0

Çalışma ağacı `origin/main`in 53 commit gerisinde; e0 dizgesi, `kalite-olcumu.yml` ve güncel ölçümler orada.
Aşağıdaki hiçbir dalga bu ağaçtan açılmaz: önce `t0/birlesim-084` `origin/main`e yetişir, her dalga oradan dal alır.
Aksi hâlde 1. dalga e1 ölçer ve 2/3. dalgalar `FfmpegArguments.cs:532`de çatışır.

## On Karar

### 1. Kapsam — "her alanda" = hedef boyut aracının kullanıcısının karşılaştığı her alan

**Karar.** Beş satır bilerek **kapsam dışı**: 51 DVD/Blu-ray/ISO, 14'ün ProRes/DNxHR/FFV1 parçası, 42'nin
"85 ön ayarı birebir" okuması, 45'in HandBrake JSON içe alma parçası, 63'ün BM3D parçası. Geri kalan her şey
kapsam içi; 43 cihaz profilleri de içeride (oynatılabilirlik hedef boyut kadar kullanıcı sorunu).

**Gerekçe.** ProRes/DNxHR/FFV1 büyütmek için var; küçültme aracının kimliğine ters, dönüştürücüye eklense de
"HandBrake'i geçmek" sayımına girmez. DVD/Blu-ray şifresiz kaynak okumak 2026'da niş, ffmpeg derlemesine
(libdvdread/libbluray) bağlı ve L boyutunda; kazandırdığı tek satır kendisi. HandBrake ön ayarları CRF tabanlı;
hedef boyuta çevrilemez, içe alınca kullanıcıya yanlış vaat verir. 85 sayısı bir ürün kararı değil tarih birikimi.

**Sayıma etkisi.** Durum anahtarına beşinci değer eklenir: **kapsam dışı (gerekçeli)**. "Geçildi" iddiası
kapsam içi satırlarda `gerideyiz + yok = 0` demektir; kapsam dışı satırlar tabloda kalır, iddiaya girmez,
belge bunu açık yazar. Sayı oyunu yok: 63 satırın 5'i düşer, 58'i hesaba girer.

**Risk.** Kullanıcı "ne gerekiyorsa" dedi; kapsam daraltma benim kararım. İtiraz gelirse 51 ve 14 en sona,
ayrı sözleşme olarak eklenir; sıralama değişmez.

### 2. Donanım yolu — üç zemin, tek kural: ölçülmeyen "geçildi" sayılmaz

**Karar.**
- **VideoToolbox CI'da ölçülür.** `macos-libmpv-gate.yml` zaten `macos-15` koşuyor; Apple Silicon koşucuda
  `hevc_videotoolbox` donanım kodlayıcısı var (doğrulanacak: koşucuda `ffmpeg -encoders` VT'yi listeliyor mu).
  HandBrake'in VT ön ayarı ↔ ürün VT eş baytta. Bu, donanım yolunun ölçülebilen tek CI zemini.
- **NVENC tek seferlik, kısa, kullanıcı onaylı yerel ölçüm.** 3 kesit × 10 sn × {ürün `av1_nvenc`, ürün
  `hevc_nvenc`, HB NVENC H.265, HB NVENC AV1} = 12 kodlama; `handbrake-acigi.md` süre sütununa göre NVENC kesit
  başına 1–3 sn, GPU toplamı bir dakikanın altında. Ağır olan VMAF: 12 çift × ~10 sn ≈ 2–3 dk CPU. Emsal var:
  `gpu-kodlama-bulgusu.md` aynı makinede T0 tarafından ölçüldü. Kural "ağır kodlama yasak"; bu ağır değil ama
  karar kullanıcının, önceden bildirilir, bir kez koşar, koşum betiği `tools/`a, sonuç `docs/olcumler`e.
- **Self-hosted runner hayır.** Kullanıcının PC'si demek (yasak) ve açık depoda self-hosted runner PR'dan gelen
  kodu makinede koşturur; GitHub'ın kendi belgesi açık depoda önermiyor. GPU'lu bulut koşucu ücretli; ücretsiz
  projede yok.
- **QSV/AMF ölçülmez.** Donanım yok (`kodek-secimi-kapanisi.md`). Satır "ölçülmedi" kalır.

**İddianın sınırı.** "HandBrake geçildi" cümlesi yazılım yolu + VideoToolbox üzerine kurulur. Donanım yolu ürün
içinde "hız için" konumlanır; kalite iddiası taşımaz. NVENC ölçümü yapılırsa satır 6 doldurulur, yapılmazsa
"ölçülmedi" diye kalır ve bitiş ölçütü bunu kabul eder (aşağıda).

**Risk.** NVENC bit hızı hedefin %12 altında kalıyor (`gpu-kodlama-bulgusu.md`); donanım yolunda bant isabeti
ayrı kusur, bu kıyasa karışmamalı — eş bayt ölçümde HB'nin de NVENC'i sapar, iki tarafın gerçek kbps'i tabloya girer.

### 3. Düşük hedef — sabit değil, eşik değil: FPS düşürme otomatik plandan çıkar, ölçek iki adayla ölçülür

**Karar.**
- **Kare hızı düşürme otomatik plandan çıkar.** `ceza-kalibrasyonu.md`de 3 kesit × 2 bit hızı × 9 geometride
  fps düşürmenin **hiçbir hücresi** kazanmadı: en iyi −7,55, en kötü −41,93 VMAF-NEG; model −5,4…−17,8 puan
  iyimser. Bu bir sabit hatası değil, politika hatası. Kalır: kullanıcı isteği ve kodlayıcının kaynak hızda
  açılmadığı taban durumu (`RunnableVideoBitrateK` altı).
- **Ölçek için ceza sabiti kalibre edilmez, eşik de konmaz.** Aynı belge tutulan-kesit doğrulamasının geçmediğini
  ölçtü: iki kesitin ortalaması üçüncüde 0,7–4,1 puan şaşıyor. Eşik, kılık değiştirmiş sabittir; aynı içerik
  bağımlılığını taşır. Üstelik ölçek düşürme her yerde kayıp değil: `parlak` 300 kbit'te 1280x546 **+0,96**,
  `karanlik` 1200'de 960x408 **+0,21**. Yönü içerik belirliyor; bunu yalnız ölçüm görür.
- **İki aday, kısa kesit, mevcut yoklama aşamasında.** Ürün zaten `ComplexityProbe → PlanCalculator → iki tur
  CalibrationProbe → EncodeRunner` koşuyor (`ab-duzenegi.md`). Model düşürme istediğinde yalnız o durumda
  kalibrasyon kesiti iki yerleşimde kodlanır (kaynak çözünürlük ↔ modelin seçtiği) ve **kaynak çözünürlüğüne
  yükseltilerek** ölçülür (VMAF-NEG p10 ya da XPSNR; ölçüm belgeleri iki ölçüyü aynı yönde gösteriyor). Kazanan
  yerleşim planı belirler. Süre bedeli sınırlı: yol yalnız düşük hedefte açılır, iki kısa kodlama + iki ölçüm.

**Doğrulama (1. dalga).** CI `dusuk-hedef` işi: `karanlik/parlak/hareketli` × 300/600 kbit × {kaynak çözünürlük,
modelin seçimi, ölçülü seçim} ↔ HB 1080p aynı baytta. Kabul: ölçülü seçim 6/6 hücrede HB harmonik VMAF-NEG'in
−0,3 içinde ya da üstünde; ölçülü seçim hiçbir hücrede kaynak çözünürlükten kötü değil. `ab-duzenegi.md`nin
5/5 kaybı e1 ve eski motorla; e0 ile yeniden ölçülmeden 9. dalga sabit değiştirmez.

**Risk.** Kalibrasyon kesiti içeriği temsil etmeyebilir (tek pencere); `SceneMap` varsa iki pencere.
Yoklama süresi kullanıcıya biner; `siyah-kenar.md`deki 2,0 sn veto mantığı burada da yazılsın: yoklama bütçesi
aşılırsa kaynak çözünürlükte kal (güvenli taraf, çünkü düşürmenin ortalaması kayıp).

### 4. HDR — PQ'da doğrudan kıyas, kaynak açık lisanslı HDR10, tonemap hable kalır

**Karar.**
- **Ölçü uzayı PQ, doğrudan.** İki taraf da 10-bit PQ koruyorsa VMAF-NEG, XPSNR ve karanlık PSNR aynı PQ
  uzayında hesaplanır. VMAF modeli SDR'de eğitildi, PQ'daki mutlak değer yorumlanmaz; **fark** yorumlanır.
  Tonemap referansına çekmek ikinci bir dönüşüm ve operatör seçimi katar; `handbrake-acigi.md` bunun kıyası nasıl
  kirlettiğini yaşadı. `QualityMeter` zaten HDR↔SDR'yi "karşılaştırılamaz" diyor; kural kalır.
- **Kaynak.** Aday sırası: (a) Cosmos Laundromat HDR10 (Blender, CC BY 4.0; AOM CTC kümesinde PQ sürümü var —
  adres ve lisans indirirken doğrulanacak, sha256 belgeye), (b) Netflix Open Content Sol Levante (lisans
  doğrulanacak). Blender'ın Sintel'i SDR; HDR için kullanılamaz. Kaynakta mastering-display/MaxCLL yan verisi
  **olmalı** ki satır 28 ölçülebilsin; yoksa `ffmpeg -x265-params master-display=…` ile etiketli ara kaynak üretilir.
- **Kıyastan önce satır 28.** SVT-AV1 koluna `mastering-display` ve `content-light` parametreleri (S iş) HDR
  kıyasından önce girer; yoksa ürünün HDR çıktısı eksik metadata ile ölçülür ve "korundu" iddiası yalan olur.
  Kabul: çıktıda `ffprobe` yan verisi kaynağınkiyle eş.
- **Tonemap: hable kalır.** Yol nadir (yalnız 10-bit kodlayıcı yokken; SVT-AV1 ve x265 hep 10-bit). `bt.2390`
  ffmpeg'in düz `tonemap` filtresinde yok, `libplacebo`da var; her platform derlemesinde bulunmaz (doğrulanacak).
  Nesnel ölçüsü de yok: tonemap kıyasında referans hangisi sorusu cevapsız. Satır 30 "eşit" kapanır.
- **HDR10+/DV (29).** ffmpeg'in libsvtav1/libx265 sarmalayıcıları yan veri geçişini destekliyor (7.1+;
  doğrulanacak, bu makinede 9.0). Profil 8.1 passthru kapsam içi; profil 5 (IPT, tonemap'siz oynamaz) kapsam dışı.
  6. dalga.

**Doğrulama.** CI `hdr` işi: HDR kaynak 2 kesit × 2 bit hızı, ürün otomatik (SVT-AV1 10-bit PQ) ↔ HB x265 10-bit
PQ eş baytta; negatif kontrol yarım bit. Kabul: 4/4 hücrede VMAF-NEG ve XPSNR HB'nin −0,3 içinde ya da üstünde,
çıktı `color_transfer=smpte2084`, yan veri eş. `KaranlikOlcu` Y<64 eşiği 8-bit ölçekli; 10-bit girdide 256'ya
çekilmeli (`siyah-kenar.md` `signalstats` ölçek notu aynı tuzağı gösterdi) — doğrulanacak.

**Risk.** İki tarafın kbps'i eşitlenemezse (`handbrake-kesit-turu.md`de HB 1894 yerine 1498 üretti) fark bütçe
farkını taşır; tabloya gerçek kbps girer, ±%2 dışı hücre hükme girmez.

### 5. Bantlaşma ölçüsü — CAMBI kapı olur, karanlık PSNR ve ton oranı eşlik eder

**Karar.** Kapı **CAMBI** (Netflix'in bantlaşma indeksi; libvmaf 2.3+'ta `cambi` özelliği, ffmpeg `libvmaf`
filtresinden `feature=name=cambi` ile çıkar; CI'daki libvmaf 3.x'te var — doğrulanacak). Tam olarak bu iş için
tasarlandı ve öznel bantlaşmaya karşı doğrulandı; `KaranlikOlcu`nun ton oranı ve kayması gölge ezilmesini görür,
bantlaşmayı ölçmek için yazılmadı. VMAF-NEG kapı olamaz: `whatsapp-karanlik.md` AQ kapanınca VMAF-NEG'in
+2,4…+6,7 yükselip karanlık PSNR'ın düştüğünü ölçtü.

**Düzenek.** `karanlik` kesiti + sentetik karanlık rampa (ffmpeg `gradients`, Y 16–64 arası) × {e0, e1, e2} ×
2 bit hızı. Negatif kontrol iki yönlü: kaynağın kendisi CAMBI ≈ 0 vermeli; 6 bite kuantalanmış kopya yüksek
vermeli — ölçü ayırt etmiyorsa kapı olamaz. Kabul: e0'ın CAMBI'si hiçbir hücrede e1'den +1,0 fazla değil
(eşik ilk koşumda pimlenir, negatif kontrolün açtığı aralığa göre). Aşarsa variance boost kararı yeniden açılır:
strength 1 ya da yalnız karanlık içerikte (`ComplexityProfile` YAVG eşiği).

**Risk.** CAMBI 1080p izleme varsayımıyla ölçeklenir; 818 yükseklikte `enc_width/enc_height` verilmeli.
Sentetik rampa gerçek içeriği temsil etmez; iki kanıt birlikte okunur, tek başına hüküm vermez.

### 6. Ses ve altyazı — varsayılan tek iz yeniden kodlanır, passthru bütçe payına bağlı, kap ihtiyaçtan türer

**Karar.**
- **Ses.** Varsayılan: tek ses izi (dil tercihi: kullanıcı dili > kaynağın varsayılan izi > ilk iz), AAC/Opus'a
  yeniden kodlanır; bugünkü bütçe kuralı (`PlanCalculator` `AudioBitrateK`) kalır. Passthru yalnız üç şart
  birlikte: kodek hedef kapta taşınır (MP4: AAC/AC3/E-AC3/Opus/MP3/FLAC; TrueHD ve DTS-HD MP4'e girmez),
  iz baytı hedefin **≤ %15**'i, iz bit hızı yeniden kodlamadan büyük değil. TrueHD/DTS varsayılanda **asla
  passthru**: 25 MB hedefte 5 dk TrueHD tek başına hedefi aşar. "Tüm izleri koru" gelişmiş seçenek; seçilince
  kap MKV'ye geçer ve izlerin gerçek baytı (ffprobe) video bütçesinden düşer. HandBrake'in kendi varsayılanı da
  tek iz + AAC; `--all-audio` seçenek. Eşitlemek yetiyor, geçmek bütçe düşümüyle oluyor.
- **Altyazı.** Varsayılan MP4; metin altyazılar (SRT/ASS/mov_text) `mov_text` olarak kopyalanır (stil kaybı
  neden satırında), resim altyazılar (PGS/VobSub) MP4'te taşınamaz → düşürülür, neden satırında söylenir.
  Kap kararı kullanıcıya sorulmaz, ihtiyaçtan türer: korunacak resim altyazı ya da MP4-dışı passthru izi **ve**
  kullanıcı "izleri koru" demişse MKV. Platform çipleri (WhatsApp/Discord/…) her zaman MP4, altyazısız.
  Yakma yalnız istekle; forced bayraklı iz varsa öneri satırı çıkar, otomatik yakma yok.
- **Harici SRT/ASS ekleme** kapsam içi (23), sürükle-bırak; `-map` ile aynı katman.

**Doğrulama.** Testle (dokunulan alan): çok izli sentetik kaynak (ffmpeg `anullsrc` × 3 dil + SRT + sentetik PGS)
üzerinde plan çıktısı: iz seçimi, kap kararı, bütçe düşümü, neden kodları. CI kalite ölçümü gerekmez.

**Risk.** Kap değişince `faststart` ve paylaşım yüklemesi (`Core/Share`) MKV'yi kabul etmez; MKV yolu paylaşım
düğmesini kapatmalı ve söylemeli.

### 7. Deinterlace koşullu açık, denoise kapalı

**Karar.**
- **Deinterlace.** Varsayılan **koşullu açık**: ffprobe `field_order` taramalı diyorsa ya da (belirsizse ve kodek
  H.264/MPEG-2/DV ise) kısa `idet` yoklaması taraklı kare payı eşiği aşıyorsa zincire `idet,bwdif=deint=interlaced`
  girer. `deint=interlaced` yalnız taraklı işaretli kareye dokunur; HandBrake'in comb-detect → seçici decomb
  davranışının ffmpeg karşılığı budur. Progressive kaynakta yanlış pozitif bedeli: `idet` hafif, `bwdif`
  neredeyse hiç kare işlemez.
- **Denoise.** Varsayılan **kapalı**, gelişmiş seçenek. Gürültü gidermek kaynağı değiştirir; VMAF-NEG düzeltmesi
  keskinleştirmeyi cezalandırır ama detay kaybını ödüllendirebilir — kazanç gösterilse bile ölçünün kendisi
  şüphelidir. HandBrake'in genel ön ayarları da denoise'u kapalı tutar; eşitlik. `ComplexityProbe` yüksek
  zamansal gürültü görürse öneri satırı çıkar, otomatik açma yok.

**Doğrulama.** CI `filtre` işi: progressive üç kesitte zincir açık/kapalı → VMAF-NEG farkı |Δ| < 0,1 ve süre
farkı < %5 (yanlış pozitif bedeli); `tinterlace` ile üretilmiş taramalı kesitte kapalı kol çöker, açık kol düzelir
(negatif kontrol). Denoise ölçülürse referans **her zaman ham kaynak**; denoise'lu kaynağa karşı ölçmek hiledir.

**Risk.** `idet` eşiği içerik bağımlı; ilk koşumda pimlenir, `siyah-kenar.md`deki gibi ölçümden önce yazılır.

### 8. Otomatik kırpma — tespit açık, kırpma kapalı; karar ölçüme dayanır, mirasa değil

**Karar.** Kırpma varsayılan **kapalı**; `cropdetect` yoklaması varsayılan **açık** ve bant bulunca plan neden
satırında tek tıkla kırpma seçeneği sunar. Ölçüm net: p10 kazancı 0,083 (eşik +0,30), 2/4 kaynakta pozitif,
gürültülü bantta yoklama hiç tetiklenmiyor (`siyah-kenar.md`). Kenarsız kaynakta yanlış kırpma 0/20 — veto 1
geçti, seçenek güvenle sunulabilir; ama kazanç eşiğin altında, varsayılan açık olamaz.

**İzleyici beklentisi.** Kırpma bir kalite işi değil sunum tercihi; bugünün oynatıcıları bantlı 16:9 dosyayı
bantsız 2,39 dosyayla aynı görüntüler. HandBrake'in varsayılanı DVD/TV mirasından geliyor (kodlanmış bantlar);
ölçülmüş bir karar değil. Kullanıcı bantsız istiyorsa bir tık. Satır 36 böyle "eşit" kapanır: seçenek var,
varsayılan ölçümle gerekçeli. Ölçüm gerekmez.

**Risk.** KD türü gürültülü bant tespit edilmez; kullanıcı "neden önermedi" der. Neden satırı "bant bulunamadı"
yerine sessiz kalır; kabul edilebilir.

### 9. CLI ayrı başsız proje, klasör izleme CLI'ın alt komutu, GUI yalnız düğme

**Karar.** `src/VidShrink.Cli`: yalnız Core + Ffmpeg'e bağımlı, Avalonia yüklemez (hızlı açılış, sunucu ve
CI dostu). Alt komutlar: `kucult <dosya> --hedef 25 [--cikis] [--json]`, `izle <klasor> --hedef 25 --cikis <klasor>`.
Çıkış kodları: 0 bantta, 2 bant altı kabul (doygun), 3 tavan aşımı/hedefe ulaşılamaz, 1 hata. JSON rapor
Bench'in `deneme N:` izini ve plan neden kodlarını taşır.

**Klasör izleme uygulama içinde yaşamaz.** Sebep: uygulama kapalıyken de koşmalı, `SingleInstanceChannel`
(`ana-<kullanıcı>`) GUI'ye ait — izleyici kendi kanalını açar (`izle-<kullanıcı>`), görev zamanlayıcıya/systemd'ye
bağlanabilir, çökmesi GUI'yi düşürmez. GUI'de "izlemeyi başlat/durdur" düğmesi aynı kurulum klasöründen CLI
sürecini başlatır ve durumunu kanaldan okur. Kararlılık bekleme: boyut iki ardışık yoklamada değişmiyor ve dosya
yazma kilidi yok → kuyruğa; çıkış klasörü izlenenden farklı olmak zorunda (döngü koruması); işlenmiş dosya adı
listesi `.vidshrink-izle.json`.

**ffmpeg ve güncelleme.** `ToolLocator` ortak; CLI kendini güncellemez, kurucu/GUI güncelleyince aynı klasördeki
CLI da güncellenir (tek paket, `release.yml`e CLI eklenir). `VIDSHRINK_LIBMPV` CLI'a gerekmez.

**Kazanç.** Bench'in `shrink` komutu ürün yolu değildi (analiz satır 49); CLI ürün yolunun kendisi olur ve
1. dalga ölçümleri `bench shrink` yerine CLI ile koşabilir — "ürün yolu ölçülür" ilkesi kendiliğinden sağlanır.
Bu yüzden 4. dalga öne çekildi (aşağıda).

**Risk.** Windows'ta `Process.Start` ile başlatılan CLI oturum kapanınca ölür; kalıcı izleme için görev
zamanlayıcı kaydı ayrı S iş, ilk sürümde "GUI açıkken ya da elle başlatılınca" diye belgelenir.

### 10. Ekran içeriğinde bant altı — doğru davranış, iki kusurlu yan: boşa deneme ve sessizlik

**Karar.** Kusur değil. `av1-e0-hedef-bant.md` oran tablosu ekran içeriğinde doygunluğu gösteriyor: istenen
6400 kbit'e karşılık kodlayıcı 2148 kbps üretti ve VMAF-NEG 97,33; 3200 → 1866 kbps, 97,28; 1600 → 1347 kbps,
96,94. Bitin kaliteye çevrilmediği ölçülmüş. 100 MB hücresinde ikinci deneme 10837 kbit istedi, 44,3 MB çıktı
(etkin ~2950 kbps): dosyayı şişirmenin karşılığı yok. "Daha büyük dosya kaliteyi artırmaz" cümlesi doğru.

**İki düzeltme (9. dalga).**
- **Boşa deneme.** Verim (çıkan/istenen) 100 MB hücresinde 0,37; `Correct()` doğrusal ölçekleyip 2,7× bit hızı
  istedi, 300 sn CPU boşa gitti. Kural: ilk deneme verimi < 0,5 ise ikinci deneme yok, "doygun" olarak teslim.
  360 sn hücresinde tersi: verim 0,90, 1143 → 1270 kbit (%11) ile boyut %13 arttı ve **tavanı aştı** (50,18 MB,
  teslim edilmedi, üçüncü deneme). Ölçek doğrusal değil; verim 0,9 üstünde hedef bandın alt yarısına nişan alınır.
- **Sessizlik.** `EncodeResult.UnderBand` var; arayüz bunu kusur gibi değil bilgi gibi göstermeli:
  "kaynak bu kalitede 44 MB'a sığdı; daha büyük dosya kaliteyi artırmaz". Neden kodu `Saturated`.

**Doğrulama.** CI `hedefbant` ekran satırları: deneme sayısı ≤ 2, tavan aşımı 0, teslim taşması 0 (mevcut),
`Saturated` kodu 100 MB hücresinde; ek: 1. deneme çıktısı ile teslim çıktısının VMAF-NEG farkı < 0,5
(bitin kaliteye çevrilmediğinin kanıtı; henüz ölçülmedi, oran tablosundan bekleniyor).

**Kıyasa etkisi.** Satır 11 HandBrake'te karşılığı olmayan bir satır (hedef boyut yok); "öndeyiz" kalır,
kendi ölçütümüzde "gerideyiz" iki düzeltmeyle kapanır.

## Dalga Sırası — Düzeltilmiş

Analizdeki sıra (1 ölçüm ‖ 2 eşleme → 3 filtre; 4 CLI paralel) üç yerde değişiyor: **Dalga 0** eklendi; **CLI (4)
öne** çekildi çünkü ölçüm aracı olur; **düşük hedef (9) filtreden öne** alındı çünkü ürünün vaadi hedef boyut ve
paylaşım çipleri düşük hedef — kullanıcıya en görünür açık bu. HDR statik metadata (28) 1. dalgaya taşındı.

| Sıra | Dalga | İçerik | Bağımlılık |
|---|---|---|---|
| 0 | Yetişme | `t0/birlesim-084` → `origin/main`; tüm dallar oradan | — |
| 1a | CLI | `VidShrink.Cli` (`kucult`, `--json`, çıkış kodları); `izle` bu dalgada değil | 0 |
| 1b | Ölçüm (CI) | `handbrake` 4 kesit e0 ürün yolu; `dusuk-hedef` (K3 düzeneği); `bantlasma` CAMBI; `social` HB Social/Discord ↔ 25 MB; `turbo`; `hdr` (önce satır 28 S işi, kaynak seçimi); VT `macos-15` işi | 0; CLI hazırsa onunla, değilse `bench shrink` |
| 1c | Akış eşleme | `StreamMapping.cs`, kap kararı, passthru kuralı, altyazı, bölüm/meta, ses bütçesi | 0 |
| 2a | Düşük hedef | FPS düşürme otomatik plandan çıkar; iki aday ölçümü; doygunluk ve `Correct()` ölçeği; `Saturated` | 1b sonuçları, 1c (`PlanCalculator` ortak) |
| 2b | Filtre zinciri | `VideoFilterChain`, `idet`+`bwdif` koşullu, `CropProbe` tespit-açık/kırpma-kapalı, denoise/unsharp/deblock/transpose/pad/gray/zscale seçenek, küçültmeye aralık | 1c (`FfmpegArguments` ekleme noktası) |
| 2c | Klasör izleme | `izle` alt komutu, kararlılık bekleme, GUI düğmesi | 1a |
| 3a | HDR yan verisi | HDR10+/DV 8.1 passthru | 2b |
| 3b | Ön ayar kütüphanesi | Cihaz profilleri, Discord 10 MB/Telegram/e-posta; JSON içe alma yok | 1c |
| 3c | Kodlayıcı ve platform | VideoToolbox plan yoluna (1b VT ölçümüne göre), VP9 küçültmeye, arm64 RID'ler | 1b |
| 4 | Arayüz | İz/altyazı/filtre paneli (gelişmiş, varsayılan görünmez), kuyruk, bitince eylem, 42 dil | 1c, 2a, 2b, 3b |
| 5 | Kapanış ölçümü | Bitiş ölçütü listesi tek koşumda; `docs/olcumler/handbrake-kapanis.md` | hepsi |

Aynı harfli dalgalar paralel; `PlanCalculator.cs` 1c ve 2a'da, `FfmpegArguments.cs` 1c/2b/3a'da ortak, bu
yüzden sıralı. 1b'nin CI süresi: `hedefbant` tek hücre 4–40 dk (`av1-e0-hedef-bant.md` kodlama sn sütunu);
işler matrisle ayrı koşuculara dağıtılır, tek iş 6 saat sınırına yaklaşmaz.

## Bitiş Ölçütü — "Her Alanda Geçildi" Denebilmesi İçin

Hepsi tek kapanış koşumunda (5. dalga), HandBrakeCLI 1.11.2 sha256 pinli, her işte negatif kontrol, her sayı CI
koşum kimliğiyle `docs/olcumler`de. Gürültü bandı: VMAF-NEG ±0,3, XPSNR ±0,2 dB (`handbrake-kesit-turu.md`
tekrar koşumu −0,65 → −0,69 verdi; bant buna göre).

| # | Ölçüt | Kabul |
|---|---|---|
| B1 | SDR kalite, yazılım yolu, ürün yolu e0 ↔ HB x265 slow 2 geçiş, eş bayt ±%2 | 4 kesit × 2 bit hızı = 8 hücre: VMAF-NEG ve XPSNR 8/8 önde ya da bant içi; karanlık PSNR hiçbir hücrede −0,5 dB altı; CAMBI hiçbir hücrede HB'den +1,0 kötü |
| B2 | Düşük hedef, otomatik plan ↔ HB 1080p | 3 kesit × 300/600 kbit: harmonik VMAF-NEG 6/6 bant içi ya da önde; düşürme kararı verilen hücrelerde de |
| B3 | Hedef isabeti | 36/36 taşma yok; deneme ≤ 2; doygun içerikte `Saturated`; HB Social 25 MB'ın gerçek boyutu tabloda (bilgi, hüküm değil) |
| B4 | HDR10 | 2 kesit × 2 bit hızı: PQ korunmuş (`smpte2084`), yan veri eş, VMAF-NEG/XPSNR 4/4 bant içi ya da önde |
| B5 | Hız | B1 hücrelerinde kodlama süresi ≤ HB; turbo kararı ölçümle verilmiş (`turbo-ilk-gecis.md` "ölçülmedi" notu kapanmış) |
| B6 | Donanım | VT: macOS koşucusunda ürün VT ↔ HB VT eş baytta bant içi ya da önde; NVENC: tek seferlik yerel ölçüm belgesi **ya da** "ölçülmedi" işareti; QSV/AMF "ölçülmedi" |
| B7 | Özellik satırları | Kapsam içi 58 satırda `gerideyiz + yok = 0`; her satır testle (dokunulan alan) ve neden satırıyla; kapsam dışı 5 satır gerekçeli listede |
| B8 | Basitlik korunmuş | Varsayılanla yapılan iş hiçbir gelişmiş seçeneğe dokunmadan bitiyor (`auto-mod.md` K2: tek karar hedef MB); yeni panellerin hiçbiri ana ekranda değil |
| B9 | Filtre yanlış pozitifi | Progressive kesitlerde zincir açık/kapalı |Δ VMAF-NEG| < 0,1, süre < %5; taramalı sentetikte negatif kontrol çalışıyor |
| B10 | Belge | `docs/olcumler/handbrake-kapanis.md`: 63 satırlık tablo son durumla, "ölçülmedi" listesi, kaynak sha256'ları, koşum kimlikleri; YOL-HARITASI'ndaki 8,79 satırı güncel sayıyla değişmiş |

"Geçildi" cümlesi B1–B5 ve B7–B10 tamamsa kurulur; B6'nın NVENC/QSV/AMF kolu cümlenin **dışında** tutulur ve
belge bunu söyler. Kapsam dışı satırlar cümleye girmez.

## Kullanıcıya Kalan

**Soru (tek).** NVENC için tek seferlik yerel ölçüm onayı: 12 kısa kodlama (GPU toplam < 1 dk) + VMAF ölçümü
(CPU ≈ 2–3 dk), bir kez, önceden haber verilerek. Hayır denirse satır 6 "ölçülmedi" kalır; iddia değişmez.

**Bildirim (soru değil).** Kapsam dışı beş satır: DVD/Blu-ray, ProRes/DNxHR/FFV1, 85 ön ayar birebir, HandBrake
JSON içe alma, BM3D. İtiraz yoksa böyle kalır.
