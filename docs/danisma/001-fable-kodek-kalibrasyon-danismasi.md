# Fable kodek kalibrasyon danismasi

- soran: T0
- danisilan: fable
- tarih: 2026-09-04

## Sorulan

Sen bir video sıkıştırma danışmanısın. VidShrink adlı bir araç için kodek ve
çözünürlük kararlarını kalibre etmeye çalışıyoruz ve senin görüşünü alıyoruz.

Sana bir soru dosyası hazırladık. Şu dosyayı oku ve **her numaralı soruya sırayla,
gerekçesiyle cevap ver**:

C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\danisma\fable-soru-2.md

Önceki turda verdiğin cevap da şurada, bağlam için okuyabilirsin:
C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\danisma\fable-cevap.md

İstersen ölçüm dosyalarına ve ffmpeg loglarına da bakabilirsin:
- `.calisma/kodek-matris/SONUC.md` — derlenmiş sonuç
- `.calisma/kodek-matris/log_*.txt` — ham ffmpeg/SVT-AV1 logları
- `.calisma/kodek-matris/vmaf_*.txt` — ham VMAF çıktıları

## Nasıl cevap vermeni istiyoruz

- **Türkçe yaz.**
- Emin olmadığın yerde "emin değilim" de. Uydurulmuş bir eşik, eşiksizlikten kötüdür;
  önceki turda eşik vermeyi reddetmen bizim için değerliydi, aynısını yap.
- Bizim yanıldığımızı düşündüğün yeri açıkça söyle. Önceki turda parça-2 hakkında
  söylediğin şey ölçümle doğrulandı ve bizim teşhisimizi düzeltti.
- Sayı uydurma. Bir sayıya ihtiyacın varsa hangi ölçümün onu vereceğini söyle.
- Cevabını **tek bir dosyaya yaz**, şuraya:
  `C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\danisma\fable-cevap-2.md`
  Ayrıca cevabın tamamını rapor olarak da döndür.

## Sınırlar

- **Hiçbir kodlama, ölçüm veya ffmpeg koşumu yapma.** Şu anda makinede başka
  ölçümler koşuyor; ek bir ffmpeg süreci onların sayılarını bozar.
- Proje kodunu değiştirme, git'e dokunma.
- Yalnız `.calisma/danisma/fable-cevap-2.md` dosyasına yaz.

## Donen

Cevap yazıldı: `C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\danisma\fable-cevap-2.md`. Hiçbir şey koşturulmadı, koda ve git'e dokunulmadı. Cevabın tamamı:

---

# Cevap 2: Ölçüm Sonuçları Üzerine

Baktığım dosyalar: `SONUC.md`, `log_p3_av1_p6.txt`, `log_rc_p6_vbsiz.txt` (yarım, hâlâ koşuyor), `log_p2_av1_p6_C.txt`, `log_p3_av1_p4.txt`, tüm `vmaf_*.txt`, `kos-*.sh`, x265/x264 pass-1 istatistik dosyalarının başlıkları, `ComplexityProfile.cs`, `CompressionStrategy.cs`. Hiçbir şey koşturmadım.

## Önce yanıldığınızı düşündüğüm iki yer

**"Oran denetimi çöküyor" teşhisi yarı doğru.** parça-3 @483k'da SVT-AV1 oran denetimi hedefi ıskalamıyor; kolu sonuna kadar çevirmiş (`q=63`, izin verilen üst sınır) ve o kolun ucunda dosya hâlâ 915k. Bu bir denetim hatası değil, **kuantizasyon tavanına dayanmış bir kodlayıcı**. Aşımın kaynağı, kolun üstünde denetimin ulaşamadığı bir şeyin bit harcaması — soru 1.

**Sapmanın "preset'e bağlı" olduğu çıkarımı verinin taşımadığı bir çıkarım.** preset 4/2 band içinde kaldığı satır 4811k; preset 6'nın aştığı satır 483k. İki değişken birlikte değişmiş. `kos-rc.sh`'nin `p4_vb` kolu bunu ayıracak. Beklentim, preset 4'ün de aşacağı ama daha az — mekanizma preset'ten bağımsız.

## Bulgu 1 — SVT-AV1 tavanı

### 1. `enable-variance-boost` bu aşımı üretiyor olabilir mi?

Evet, en olası suçlu bu; mekanizması loglarda görünüyor.

Variance boost, kare için seçilmiş q'ya **süper-blok düzeyinde eksi delta-q** uygular: düşük varyanslı bloklara daha düşük qindex verir. Oran denetimi yalnız kare q'sunu yönetir ve 63'te durur; boost o 63'ün **altına** iner. Denetimin görebildiği kol tavandayken gerçek kuantizasyon tavanda değil. HDR PQ bunu büyütür: PQ eğrisi görüntünün çoğunu düşük kod değerlerine sıkıştırır, "düşük varyanslı blok" oranı yüksektir.

Kanıt, iki logun aynı zaman noktasından (ikinci geçiş, ~31. saniye):

| kol | q | anlık bitrate |
|---|---|---|
| vb açık (`log_p3_av1_p6.txt`, kare 1876) | 63 | 872 kbit/s |
| vb kapalı (`log_rc_p6_vbsiz.txt`, kare 1906, **yarım koşum**) | 63 | 396 kbit/s |

Aynı q, 2,2 kat fark. Kesin sayı `log_rc_p6_vbsiz` bitince gelsin.

Konfig satırı: `AQ mode / Variance Boost strength : 2 / 2` — iki yerel q ayarlayıcı üst üste. Mainline belge variance boost'u CRF için tanımlar; VBR'de desteklenip desteklenmediğinden emin değilim, 4.2.0'ın `Parameters.md`'sine bakın.

Kayda geçsin: vb kapalı koşum da q=63'e çakılı ve hedefin **altında** (396k). Oran denetimi bu bölgede sağlıksız — tavanda oturup hedefi ıskalıyor. Nedenini logdan söyleyemem.

### 2. `q=63`'e dayanmak ne demek, nasıl ayırt edilir?

`q=63` + hedefin üstü = **bu parametre kümesi bu içeriği bu bitrate'e sıkıştıramaz**. "Küme" vurgusu önemli: vb kapatılınca küme değişir, tavan yer değiştirir; preset de kümenin parçası.

Ayırt etme testi tek ve ucuz: **aynı parametrelerle tek geçiş CRF 63** kodlayın. Çıkan bitrate o kümenin **taban bitrate'i**; hiçbir oran denetimi altına inemez.
- Taban > hedef → denetim suçsuz, küme yetersiz. Çare vb kapatmak, preset düşürmek, küçültmek, kare hızı.
- Taban < hedef ama VBR aşıyor → denetim hatası; `maxrate`/`bufsize`, CBR kipine bakılır.

Bu sayı ürüne girer: `CodecModel.FloorBppf` sabit yerine ölçülmüş taban olur.

### 3. Hedef-boyut ürününde SVT-AV1 VBR güvenli mi?

Tek başına değil. İki sapma da yapısal:
- **Aşım** (parça-3 @483k): tavan sorunu; sarmal çözmez, ikinci tur da 63'e çakılır. Kodlamadan önce bilmek gerekir.
- **Eksik kalma** (parça-2 @4837k, −%23,8): logda q=3–7, kuantizasyon **tabanı**. Kalite doymuş (96,2), SVT daha fazla harcamıyor. x265 aynı yerde I-karelerine QP 17'yle 98 Mbit/s basıp hedefi "tutturuyor" — bit çöpe. Hedef tavansa SVT doğru; kotaysa x265. Ben "tavan" derdim.

Risk asimetrik: **kolay içerikte altında kalır, zararsız; zor içerikte tavana çarpar, ölümcül.** Tasarım:
1. Sondaya CRF 63 tabanı ekle.
2. Hedef bpp < taban ise VBR'ye girme; küçült / kare hızı / preset, yeniden hesapla.
3. Taban geçiliyorsa iki geçiş VBR + teslim baytını doğrula; %X üstündeyse hedefi oranla ölçekleyip **bir** kez daha kodla. X'i verinizden alın.
4. `maxrate`/`bufsize` kısıtlı VBR'yi kos-rc'ye kol olarak ekleyin; sürümünüzdeki davranıştan emin değilim.

"Üstünlük riski karşılıyor mu": **tavan sorunu çözülmeden veri yok.** 37,03 915k'da, 17,20 483k'da; kıyas değil. Preset 6'nın 4811k'da x265 slow'un 2 puan altında olduğunu da unutmayın; AV1'i varsayılan yapmazdım.

## Bulgu 2 — küçültme kararı

### 4. Eşik hangi büyüklüğün fonksiyonu?

**Elimizdeki bit / içeriğin istediği bit.** bpp'yi karmaşıklıkla birleştirmek değil, karmaşıklığa **bölmek**. Paydayı zaten ölçüyorsunuz: `ComplexityProfile.ReferenceBppf`. Sinyal `hedef_bppf / ReferenceBppf`; merdiven bu oranı yatay eksen yaparsa kolay ve zor parça aynı eksende buluşur. `MotionExponent` bu karara girmez; kare hızı terimi.

Uyarı: 652x366 ölçek 0,34, sonda 0,5'te. `ScaleFactor` 0,5'in altını `LowScaleDamping = 0.3` ile **ekstrapole ediyor**. parça-2'yi 652x366'ya götüren karar ölçülmemiş bölgede verilmiş.

### 5. Karmaşıklığı ucuza ölçmek

Zaten ölçüyorsunuz: sonda kodlaması. SI/TI (`siti`) daha ucuz ama kodek bit talebiyle zayıf ilişkili. İkinci kaynak bedava: **iki geçişin birinci geçişi zaten karmaşıklık ölçümü.** `p3_x265_B_x265pass` kare başına bit/tür/QP taşıyor; sabit QP'de kare başına bit doğrudan karmaşıklık. Ürün iki geçiş kullanıyorsa küçültme kararı birinci geçişten sonra **o veriyle** yeniden verilebilir, sıfır ek maliyet. `MotionMeasured` için: ölçüm ucuzsa her zaman ölçün, bayrağın yanlış olacağı yol kalmasın.

### 6. "Ne kadar" ayrı soru mu?

Aynı ölçümden çıkar: başlık-başına **dışbükey zarf**. Bitrate düştükçe zarf 1080 → 720 → 540 → 360 basamaklarını sırayla geçer; 1080'den 366'ya atlamak ara basamakları kaybetmek. `ScaleStep = 0.02` sürekli ölçeği standart basamaklara (1280x720, 960x540, 640x360) kısmayı düşünün; 652x366 mod-8 bile değil. VMAF'ın yukarı ölçekleme süzgeci (bicubic/lanczos) kayda geçsin.

## Bulgu 3 — kodek sıralaması

### 7. 0,29 puan berabere mi?

Berabere; gerekçe "algı eşiğinin çok altında". JND sayısı uydurmuyorum, 0,29'un altında olduğundan eminim.

Yanlış öncül: **aynı kodlamayı tekrarlamak sıfır varyans verir.** x265 sabit `frame-threads=4 numa-pools=16` ile bit-eşdeğer; SVT-AV1 de iş parçacığından bağımsız aynı bitstream verir (büyük ölçüde eminim; iki koşum özeti bir dakika). Gürültü **içerik örneklemesi**: per-frame VMAF serisini 10 sn'lik altı dilime bölüp `VMAF(AV1p4) − VMAF(x265)` yayılımına bakın; yeni kodlama gerekmez.

Hız: 132/182 sn bu makinede (16 çekirdek, SVT LoP 5, x265 4 kare iş parçacığı). SVT çekirdekle daha iyi ölçeklenir; 4 çekirdekte oran daralır. Hedef makine sınıfında ölçün. Ve: **ürünün seçtiği preset 6, x265 slow'un 2 puan altında.**

## Bulgu 4 — HDR

### 8. HDR PQ'da VMAF-NEG

**Aynı uzayda göreli sıralama için kullanılabilir; mutlak sayı ve SDR eşikleri için kullanılamaz.** "Kullanılabilir"in ne kadar tutacağından emin değilim. Ölçüsü: SDR kliplerde aynı kodek ikilisini sıralayın; HDR'yle uyuşuyorsa güvenin. Ucuz sağlama: referans ve adayı **aynı** ton eşlemesiyle SDR'a indirip VMAF koşun. Netflix'in HDR-VMAF çalışması var; libvmaf'ta açık model olup olmadığından emin değilim.

### 9. HDR için ayrı metrik?

XPSNR'ı tutun; HDR kalibrasyonundan emin değilim, VMAF'la uyuşmadığı satırı işaretleyin. **CAMBI** (libvmaf, `--feature cambi`) düşünün: HDR düşük bitrate'te baskın kusur bantlaşma, VMAF ona kör. HDR-VDP'ye girmeyin.

### 10. SDR/HDR eşikleri farklı mı?

Farklı bekleyin: sinyal farklı (10-bit + PQ), metrik farklı sapıyor, seçenek kümesi farklı (HDR'de x264 masada yok). Yön aynı, kesişme bpp'si farklı; ölçmeden ortak eşik kullanmayın.

## Merdiven

### 11. Eksik ve israf

- **Sabit bitrate ekseni israf**: 4,8 Mbit kolay içerikte doyar, 0,4 Mbit hâlâ 94,7. Bitrate'i klip başına **normalize** seçin: `ReferenceBppf` × {1/32…1/2}.
- **x264 tam merdivende israf**: klip başına tek nokta.
- **HandBrake referansı** merdivende gereksiz.
- **Küçültme kolu ara basamaksız**: {720, 540, 360} × alt 3 bitrate × kazanan kodek = 9.
- **Teslim baytı**: her noktayı teslim edilen bitrate'e göre çizin.
- **SDR klip önce.**

Bütçe: klip başına 11 + 9 = 20 kodlama + 20 VMAF.

### 12. Bitrate mi CRF mi?

**CRF ile kurun, teslim baytına göre eğri çizin; kıyas BD-rate ile.** Eş-boyut bozulmaz, eğriden okunur. Ama ürün VBR: iki eğri gerekir — CRF (potansiyel) ve VBR (teslim). x265'te çakışır; SVT'deki boşluk "VBR yerine CRF araması mı" sorusunun cevabı. Sıra: `kos-rc.sh` bitsin → CRF merdiveni → VBR eğrisi yalnız AV1'de, tavanın üstünde.

## Sormadınız ama gördüm

- `ab-duzenegi.md:873` parça-3 Auto'yu **882x496, 45,97** gösteriyor; soru **652x366, 22,29** diyor. Hangisi geçerli, kayda geçsin.
- parça-3 @483k x264 harm 6,65 / min 0,00; x265 17,20 / min 1,98. 17 ile 22 farkı anlamsız, ikisi de izlenmez. Ürün "küçült" değil "bu hedef izlenebilir sonuç vermez" demeli.
- SVT birinci geçişi tam preset'te (19 sn / 35 sn); maliyet hesabına girsin.
