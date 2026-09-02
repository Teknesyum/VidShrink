# Örnekleme — sabit pencere yerine içeriğe bağlı seçim (T103)

Ölçülen commit: `ef9e905` (dal `T103-icerik-ornekleme`, `main` `b9a6544` üzerinden).
ffmpeg 9.0 (gyan.dev), Windows 11, 2026-09-02. **Makine paylaşımlıydı** — aynı anda
yedi ajanın ölçümü koşuyordu. Bu damga yalnız **süre** sayıları için geçerlidir;
sapma sayıları yükten bağımsızdır, aynı kodlamaların yeniden kullanılan
çıktılarından hesaplanır.

## Ne ölçüldü, neye karşı

Bir yoklamanın işi, dosyanın tamamını kodlamadan `bppf`'ini (kare başına piksel
başına bit) kestirmektir. İki ayrı hata var ve bunlar karıştırılmamalı:

- **Alan kayması**: 2 saniyelik bir pencerenin kodlaması, tüm dosyanın kodlamasına
  göre yapısal olarak pahalıdır — pencerenin kendi IDR'si vardır, lookahead ve
  mbtree geçmişi yoktur. Bu kayma zaten `WindowDomainFactor` ile modelleniyor ve
  bu sözleşmenin konusu değil.
- **Örnekleme hatası**: aynı alan içinde, *hangi* pencerelerin seçildiğinden gelen
  hata. T103'ün konusu budur.

İkisini ayırmak için referans, dosyanın tamamının kodlaması değil, **2 sn'lik
pencerelerin yoğun sayımıdır**: her tam saniyeden başlayan 2 sn'lik pencerenin
ayrı ayrı kodlanması ve ortalaması. Böylece aday planların sapması yalnız yerleşim
farkını gösterir.

Ölçüm izolasyonu: kodlamalar `-f null -` ile yapıldı, boyut `vstats` `f_size`
toplamından geldi; kapsayıcı ek yükü ölçünün dışında.

### Korpus

`.calisma/kaynak/kaynak-1080p60-hdr-17dk-yalniz-video.mkv` (1920x1080 hevc 10-bit
HDR, 60 fps, 1036,166 sn, oyun görüntüsü) tonemap'lenerek 960x540@30 SDR'ye
indirildi, CRF 14 veryfast ile kesildi. Düzenek: `tools/ornekleme/korpus-kur.sh`,
`korpus-kur-2.sh`.

| klip | süre | ne olduğu |
| --- | --- | --- |
| `k1-inisli` | 240 sn | yoğun başlayıp sakinleşen |
| `k2-duz` | 240 sn | tekdüze |
| `k3-tepeli` | 240 sn | ortada tepe |
| `k4-bas` | 240 sn | yük başta |
| `k5-kisa` | 60 sn | kısa dosya |
| `k6-uzun` | 480 sn | uzun dosya |
| `s1-cuval` | 240 sn | %75 kolay + %25 zor, tek geçişte birleştirilmiş |
| `s2-donusumlu` | 240 sn | 20 sn'lik kolay/zor bloklar dönüşümlü |

Korpusun bir sınırı var ve rapora giriyor: bu klipler CRF 14 ile yeniden
kodlanmış 540p kaynaklardır. **`ScanPoints` yanlılığı bu kliplerde güven
bandının dışına düşüyor** (k1 0,185; k5 0,4197 — band 0,5-2,0), oysa gerçek
1080p60 kaynaklarda içeride kalıyor. Yanlılık yolu hakkındaki yargı (K6) bu
yüzden korpustan değil, gerçek kaynaklardan ölçüldü.

## Adaylar

Her aday, ffprobe'un paket boyutlarından çıkarılan **saniye başına kaynak bit
profili** üzerine kurulu. Bu profil çözme gerektirmez; maliyeti aşağıda ayrıca
ölçüldü.

- **bugün** — sabit pencere: `usable*(i+0,5)/n`, `n = süre < 12 ? 2 : 3`.
- **profil-*** — saniyeleri kaynak bitine göre sıralayıp eşit sayılı tabakalara
  böler, her tabakadan bir saniye seçer, tabaka büyüklüğüyle ağırlıklandırır.
  Seçim ölçütü üç varyantta denendi: tabaka **ortalaması**, tabaka **medyanı**,
  ve seçilen saniyeden başlayan **2 sn'lik pencerenin** ortalaması.
- **pencere-*** — tabakalama saniye üzerinde değil, doğrudan **2 sn'lik pencere**
  üzerinde. Sıralanan büyüklük, hedeflenen büyüklük ve ağırlık aynı şeydir.
- **sahne-*** — `SceneMap`'in kesimlerinden sahneler kurar, sahneleri bit oranına
  göre sıralar, süre ağırlıklı tabakalara böler, her tabakanın en uzun sahnesinin
  ortasından örnekler.
- **-oran** eki: her pencerenin `bppf`'i `tabaka ortalaması / pencere bitleri`
  ile çarpılır (tabaka içi oran düzeltmesi).
- **-genel** eki: klasik oran tahmincisi — `ortalama(bppf/pencere biti) × dosya
  ortalaması`.
- **eşaralık** — tabakalama yok, pencereler süreye eşit aralıklı.

Değerlendirme `tools/ornekleme/cozumle.py` ile yapıldı: kodlamalar diske
önbelleklendiği için her aday, yeniden kodlama olmadan aynı kodlamalar üzerinden
puanlanıyor. Bu yüzden aday karşılaştırması **yükten bağımsız ve tekrarlanabilir**;
aynı girdiyle aynı sayıyı verir.

## Seçilen kural

Aday seçimi **en düşük hücreye** göre yapılmadı. Pencere sayısı koşumda içeriğe
göre belirlendiği için bir ailenin tek bir N'de iyi olması yetmez; **her N'de**
kabul edilebilir olması gerekir. Bu yüzden ölçüt, ailenin N'ler arasındaki **en
kötü klip sapmasıdır**.

Kazanan: **pencere tabanlı tabakalama, oran düzeltmesiz**. Kural üç parçadan
oluşuyor ve üçü de ölçümden geliyor:

1. **Neye bakılır** — 2 sn'lik pencerenin kaynak bitleri. Tabakalama, hedefleme
   ve ağırlık aynı büyüklük üzerinde; saniye üzerinden tabakalayıp pencere
   kodlayan varyantlar geçiş saniyelerinde sapıyor.
2. **Kaç pencere** — `N = clamp(2 + 3·cv, 2, 8)`, `cv` = saniye başına kaynak bit
   profilinin değişim katsayısı. Eğim 3, ölçülen "%5 sapma için gereken en küçük
   N" tablosuna uyan en küçük tam sayı katsayıdır (aşağıda).
3. **Üst sınır** — 8. Ölçülen N eğrisinde en kötü sapmanın en aza indiği nokta;
   ötesinde iyileşme yok. Alt sınır 2. Ayrıca dosyanın kendisi sınırlıyor:
   pencereler örtüşmediği için en çok `floor(süre / 2)` pencere olabilir.

Oran düzeltmesi (`-oran`) saniye tabanlı tabakalamada işe yarıyor, pencere
tabanlısında **zarar veriyor** — tabaka ile pencere aynı büyüklük olduğunda
düzeltme fazlalık, kendi varyansını ekliyor. Klasik oran tahmincisi (`-genel`)
elendi: kaynak biti ile 2 sn'lik yeniden kodlamanın `bppf`'i orantılı değil,
sapma %200'ün üzerine çıkıyor.
