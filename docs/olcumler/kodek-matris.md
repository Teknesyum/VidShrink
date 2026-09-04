# Kodek matrisi — ham sonuç

Kaynak: `.calisma/kaynak/parca-2-yalniz-video.mkv`, `parca-3-yalniz-video.mkv`
Her ikisi de **1920x1080, 60 fps, yuv420p10le, smpte2084 (HDR PQ)**.
Metrik: VMAF-NEG harmonik ortalama (`harm`), `docs/olcumler/ab-duzenegi.md` ile aynı sütun.
Tüm kodlamalar sıralı koşuldu; hiçbir ikisi aynı anda çalışmadı.

## A — parça-3 (zor içerik), yüksek hedef 4811k

HandBrake referansı: 36.282.675 bayt, harm 74,96. Band ±%2.

| kol | harm | bayt | band | saniye |
|---|---|---|---|---|
| AV1 preset 2 | **77,64** | 36.220.894 | −%0,17 ✓ | 574 |
| x265 slow | 75,66 | 35.966.305 | −%0,87 ✓ | 182 |
| AV1 preset 4 | 75,37 | 36.219.039 | −%0,18 ✓ | 132 |
| HandBrake | 74,96 | 36.282.675 | — | — |
| AV1 preset 6 | 73,71 | 36.277.838 | −%0,01 ✓ | 54 |
| x264 slow | 65,58 | 36.453.076 | +%0,47 ✓ | 28 |

Mevcut Auto bu satırda x264 seçiyor ve 66,11 alıyor.

## B — parça-2 (kolay içerik), düşük hedef 484k, 1080p (küçültme yok)

HandBrake referansı: 3.735.428 bayt, harm 93,73. Band 3.660.719–3.810.136.

| kol | harm | bayt | band | saniye |
|---|---|---|---|---|
| AV1 preset 6 | **94,68** | 3.580.091 | −%4,16 (altında) | 30 |
| x265 slow | 93,89 | 3.788.239 | +%1,41 ✓ | 87 |
| HandBrake | 93,73 | 3.735.428 | — | — |
| x264 slow | 93,38 | 3.963.874 | +%6,11 (üstünde) | 20 |
| **mevcut Auto (1650x928'e küçültülmüş)** | **82,25** | — | +%1,89 | — |

AV1 bandın **altında** kalarak kazanıyor — az bitle. Kıyas lehine değil aleyhine kusurlu.
x264 bandın **üstünde** kalarak kaybediyor — fazla bitle. O da kendi lehine kusurlu.

## C — parça-3 (zor içerik), düşük hedef 483k, 1080p (küçültme yok)

HandBrake 1080p @483k: 3.680.998 bayt, harm 13,72 (`ab-duzenegi.md:328`).
Güncel tablodaki HandBrake satırı: harm 17,88 (`ab-duzenegi.md:944`).

| kol | harm | bayt | bitrate teslim | saniye |
|---|---|---|---|---|
| AV1 preset 6 | 37,03 | 6.907.758 | 914.682 bps — **hedefin %89 üstü, geçersiz** | 54 |
| x265 slow | 17,20 | 3.651.642 | 483.527 bps ✓ | ~180 |
| x264 slow | (koşuyor) | 3.744.057 | — | 28 |
| **mevcut Auto (882x496'ya küçültülmüş, AV1)** | **22,29** | — | — | — |

## D — oran denetimi (rate control) sapması

| dosya | istenen | teslim | sapma |
|---|---|---|---|
| p3_av1_p6_483 | 483k | 914.682 bps | **+%89,4** |
| p2_av1_p6_4837 | 4837k | 3.685.727 bps | **−%23,8** |
| p3_av1_p4_4811 | 4811k | — | band içi ✓ |
| p3_av1_p2_4811 | 4811k | — | band içi ✓ |
| p3_x265_483 | 483k | 483.527 bps | +%0,1 ✓ |

`log_p3_av1_p6.txt` kanıtı: `BRC mode / target bitrate (kbps): VBR / 483` (hedef doğru
alınmış), `Force the look_ahead_distance to be 42` (iki geçiş aktif), ve koşum boyunca
**`q=63.0`** — SVT-AV1'in kuantizasyon tavanı. Kodek kalite kolunu sonuna kadar kısmış
ve hâlâ hedefe inememiş.

Kullandığımız parametre: `keyint=600:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2`.
`variance-boost` düz alanlara fazladan bit verir; oran denetimiyle çatışıyor olabilir.
Sınama betiği: `kos-rc.sh` (preset 6 variance-boost'suz, preset 4 iki yönlü).

## E — GERİ ÇEKİLDİ

Bu bölüm "aynı oran, zıt doğru cevap" başlığıyla küçültmenin zor içerikte kazandırdığını
iddia ediyordu. **İddia düştü**, iki ayrı kusurdan:

1. parça-3 satırı iki değişkeni birden oynatıyordu (1080p x265 ile küçültülmüş AV1'i
   kıyaslıyordu). Kodek sabitlenince küçültme kaybediyor — bölüm K.
2. parça-2 satırındaki yerleşim 652x366 değil 1650x928 (`parca-2-auto.md`).

Yerine geçen bulgu bölüm K'de: tek değişken çözünürlük tutulduğunda küçültme her iki
içerik türünde de kaybettiriyor.

Kapsam kusuru notu duruyor: altı kaynağın altısı da HDR PQ, **SDR ölçümü yok.**

## F — oran denetimi sınaması: suçlu variance-boost

parça-3, hedef 483k, 1080p, tek değişken `enable-variance-boost=1:variance-boost-strength=2`.

| koşum | teslim bps | 483k'ya göre | saniye | bayt |
|---|---|---|---|---|
| preset 6 + VB | 914.682 | **+%89,4** | 54 | 6.907.758 |
| preset 4 + VB | 839.368 | **+%73,8** | 99 | 6.338.983 |
| preset 6, VB'siz | 421.051 | −%12,8 | 50 | 3.179.817 |
| preset 4, VB'siz | 393.113 | −%18,6 | 94 | 2.968.823 |

Preset değişken değil. Variance-boost açıkken iki preset de aşıyor, kapalıyken ikisi de
güvenli yönde (hedefin altında) kalıyor.

Daha önce "sapma preset'e bağlı, kaçıran yalnız preset 6" demiştim; bu ölçüm onu çürüttü.
Preset 4 ve 2'nin hedefi tutturduğunu gördüğüm yer yüksek bitrate'ti (4811k) — orada
variance-boost'un istediği fazladan bit hedefin içine sığıyor.

### Üretim kodundaki karşılığı

`src/VidShrink.Core/FfmpegArguments.cs:532-533` — libsvtav1 için koşulsuz eklenir,
oran denetimi kipine bakmaz. `tests/VidShrink.Tests/FfmpegArgumentsTests.cs:135` ve
`:463` bu dizgeyi pimliyor.

`FfmpegArguments.cs:354-355` — `SupportsRateLimits` libsvtav1'i **muaf tutuyor**, yani
AV1 kodlarken `-maxrate`/`-bufsize` hiç verilmiyor. Aşımı frenleyecek bir tavan yok.
Muafiyetin gerekçesi `docs/olcumler/bppf-tabani.md:244`: "libsvtav1 VBR bunları kabul
etmiyor". O ölçüm eski bir SVT-AV1 sürümüyle yapıldı; bizde v4.2.0 var.

### Kusur neden bugüne kadar görünmedi

T125'in Auto sütunu altı satırda da bandın içindeydi. Çünkü o koşumlar **küçültülmüş yerleşimlere (parça-2 1650x928, parça-3 882x496)
küçültüyordu**; piksel azalınca encoder hedefe rahat iniyor. Aşım yalnız encoder aç
kaldığında — 1080p'de agresif sıkıştırmada — çıkıyor.

Yani kusur uykuda ve **"1080p'de kal + AV1 kullan" değişikliği onu uyandırıyor.**

### SVT-AV1 oran denetimi kolları (negatif kontrollü sınama)

| anahtar | sonuç |
|---|---|
| `mbr=1000` | **RED**: `Max Bitrate only supported with CRF mode` |
| `maxsection-pct=200` | hatasız |
| `minsection-pct=50` | hatasız |
| `gecersizanahtar=1` (negatif kontrol) | **hatasız** |

Uydurma anahtar da hatasız geçtiği için "hatasız" = "destekleniyor" denemez; SVT-AV1
tanımadığı anahtarı sessizce yutuyor. Kesin olan tek şey `mbr`'nin yalnız CRF kipinde
çalıştığı — çünkü o gerçek hata verdi.

## G — düzeltilmiş kolların kalitesi (parça-3 @483k, 1080p, AV1)

| kol | harm | teslim bps | bayt |
|---|---|---|---|
| preset 4 + VB | 37,57 | 839.368 | 6.338.983 |
| preset 6 + VB | 37,03 | 914.682 | 6.907.758 |
| preset 4, VB'siz | 25,46 | 393.113 | 2.968.823 |
| preset 6, VB'siz | 24,71 | 421.051 | 3.179.817 |

Variance-boost +12,11 puan getiriyor ama iki katı bitle. Bit başına verimli mi,
bu veriyle söylenemez — VB'siz kolun ~839k'daki karşılığı ölçülmedi.

Hedef-boyut ürünü açısından soru zaten farklı: VB hedefi tanımadığı için o kalite
teslim edilebilir değil.

## H — Fable'ın düzelttiği iki nokta

**1. "VB'siz kolda oran denetimi sağlıklı" okuması yanlıştı.** VB'siz kol da `q=63`'e
çakılı ve hedefin altında kalıyor (393k–421k, hedef 483k). Tavan iki yönde de var;
VB'yi kaldırmak aşımı çözüyor ama denetimi sağlıklı yapmıyor.

Fable'ın mekanizma açıklaması: variance boost, kare q'sunun **altına** süper-blok
düzeyinde eksi delta-q uyguluyor. Oran denetimi yalnız kare q'sunu yönetir ve 63'te
durur; boost o 63'ün altına iner. Denetimin gördüğü kol tavandayken gerçek kuantizasyon
tavanda değil. HDR PQ büyütür: PQ eğrisi görüntünün çoğunu düşük kod değerlerine
sıkıştırdığı için "düşük varyanslı blok" oranı yüksektir.

Aynı zaman noktasından (ikinci geçiş, ~31. sn) iki log:

| kol | q | anlık bitrate |
|---|---|---|
| VB açık (`log_p3_av1_p6.txt`, kare 1876) | 63 | 872 kbit/s |
| VB kapalı (`log_rc_p6_vbsiz.txt`, kare 1906) | 63 | 396 kbit/s |

**2. parça-3'ün güncel Auto yerleşimi 652x366 değil, 882x496.**
Kaynak: `tools/VidShrink.Ab/veri/t125/parca-3-auto.md` künyesi —
`vidshrink (882x496@60, libsvtav1/2pass, 515k, ...)`, `-vf scale=882:496:flags=lanczos`,
`-preset 6`, istenen 489k, teslim 3.687 MB. 652x366 eski koşumdan (`381e8ab`, harm 9,35).

Üretim 489k isteyip ~488 kbps teslim etmiş — variance-boost'a rağmen hedefi tutturmuş,
çünkü küçültülmüş. Bölüm F'deki "kusur uykuda, 1080p'ye geçince uyanır" okuması
bununla doğrulanıyor.

(Fable bu satırı parça-1 tablosuyla karıştırıp 882x496/45,97 diye okumuştu; 45,97
parça-1'in Auto satırı, `ab-duzenegi.md:873`. Uyarı yine de doğru yere bastı.)

## I — sıradaki ölçümler

**CRF 63 taban** (`kos-taban.sh`): tek geçiş CRF 63, parametre kümesinin taban
bitrate'i. Hiçbir oran denetimi bunun altına inemez. Taban > hedef ise denetim suçsuz,
küme yetersiz. Üç kol: preset 6 VB'siz, preset 6 VB'li, preset 4 VB'siz.

**Denetimli küçültme** (`kos-kucultme.sh`): tek değişken çözünürlük. AV1 preset 4,
VB'siz, aynı hedef. parça-3: 882x496 / 1280x720. parça-2: 882x496 / 1280x720 / 1080p.

## J — CRF 63 taban sonucu (parça-3, 1080p, hedef 483k)

| küme | CRF63 bayt | CRF63 bitrate | VBR@483k teslimi |
|---|---|---|---|
| preset 6, VB'siz | 3.077.395 | 407.489 | 421.051 |
| preset 6, VB'li | 7.746.135 | **1.025.695** | 914.682 |
| preset 4, VB'siz | 3.288.052 | 435.383 | 393.113 |

**"CRF 63 mutlak tabandır" varsayımı bu veriyle düştü.** preset 4 VB'siz kolda VBR
teslimi (393k) kendi CRF63 çıktısının (435k) *altında*. Mutlak taban olsaydı bu
imkânsızdı. CRF 63 ile VBR'ın q=63 tavanı aynı iç qindex'e oturmuyor; CRF kolu zamansal
katmanlara q ölçekleme uyguluyor ve bazı kareleri 63'ün altında kodluyor.

Doğru okunuşu: CRF63 **azami-q çıktısıdır**, mutlak alt sınır değil. Kümeler arası
kıyas için hâlâ geçerli (aynı yöntem), tek bir kümenin ulaşabileceği en düşük bitrate
için değil.

**VB teşhisi buna rağmen ayakta.** Aynı preset, aynı yöntem, tek fark VB:

- VB'siz azami-q çıktısı 407k
- VB'li azami-q çıktısı 1.026k → **2,52 kat**

VB'li kol VBR'da 915k teslim etti; kendi azami-q çıktısının %89'u. Yani denetim
gerçekten dibe vurmuş durumda, elinde kol kalmamış. VB, kümenin bit talebini 2,5 katına
çıkarıyor ve 483k hedefi bu kümenin erişemeyeceği bir yere düşüyor.

## K — denetimli küçültme (tek değişken: çözünürlük)

Tüm kollar AV1 preset 4, `keyint=600:scd=1:tune=0` (VB yok), 2 geçiş, aynı hedef.

**parça-3 (zor içerik), hedef 483k**

| kol | çözünürlük | teslim bps | bayt | harm |
|---|---|---|---|---|
| p3_882 | 882x496 | 483.785 | 3.653.588 | 26,01 |
| p3_1280 | 1280x720 | 483.852 | 3.654.094 | **27,63** |

Boyut paritesi kusursuz (%0,01 fark). 1280x720 **+1,62** kazanıyor.

**parça-2 (kolay içerik), hedef 484k**

| kol | çözünürlük | teslim bps | bayt | harm |
|---|---|---|---|---|
| p2_882 | 882x496 | 333.430 | 2.517.404 | 60,13 |
| p2_1280 | 1280x720 | 431.158 | 3.255.248 | 74,54 |
| p2_1080 | 1920x1080 | 465.502 | 3.514.541 | **95,15** |

Burada parite yok ve **kırılmasının kendisi bulgu**: küçültülen kollar bütçeyi
kullanamıyor. 882x496 hedefin %31 altında, 1280x720 %11 altında, 1080p %4 altında
teslim etti. Küçültmek eksik teslimi büyütüyor.

Yön yine de tartışmasız: 1080p hem **daha çok** bayt harcıyor hem **+35,02** puan
alıyor. %40 bayt farkının 35 puanı kapatabileceği bir okuma yok. 1280x720 ile 1080p
arasında fark %8 bayta karşılık **+20,61** puan.

**Sonuç: her iki içerik türünde de küçültme kaybettiriyor.** Zorda az (+1,62),
kolayda çok (+20,61 / +35,02). Üretimin oran tabanlı küçültme kuralı yanlış yönde.

## L — variance-boost boyut sabitken kalite kaybettiriyor

Üretimin parça-3 Auto çıktısı: 882x496, VB açık, preset 6 → **22,29**
Bu turdaki p3_882: 882x496, VB kapalı, preset 4 → **26,01**

Aynı yerleşim, aynı hedef, teslim edilen bayt ~aynı. Fark **+3,72** ve VB'nin
aleyhine. (İki değişken var — VB ve preset — ama G bölümündeki preset 6/4 kıyası
farkın presetten gelen payını 0,5-0,8 puanla sınırlıyor.)

Bölüm G'de VB +12,11 getiriyor görünüyordu; orada boyut serbestti. Boyut sabitlenince
işaret dönüyor. VB'nin kalitesi hedefi delerek satın alınmış bir kalite.
