# VP9 Cpu-Used Ve CRF Ölçümü

Durum: **ölçüldü**. Ölçütler ölçümden önce yazıldı (commit `b5eef71a`); sayılar ve hüküm
"Sonuç" bölümünde, ölçüt metni değiştirilmedi.

## Soru

`libvpx-vp9` küçültme yolunun iki ayarı ölçümsüz duruyor:

1. `-cpu-used`: ürün varsayılanı `4` (`FfmpegArguments.DefaultPreset`), Hızlı modda `5`
   (`PlanCalculator`, satır ~1769). Hangi adım ürün değeri olmalı?
2. CRF: `PlanCalculator` VP9'da CRF'e gitmiyor, iki geçişli VBR'a dönüyor
   (`ReasonCode.Vp9CrfUnmeasuredTwoPass`). Bir CRF ölçeği hedef boya iki geçişten daha iyi
   iner mi?

## Düzenek

- İş akışı: `.github/workflows/vp9-olcumu.yml`, `ubuntu-latest`. `workflow_dispatch` varsayılan dalda\n  olmayan akışı bulmadığı için `olcum-vp9-*` etiketiyle tetiklendi.
- ffmpeg: BtbN `n9.0.2` linux64 GPL, sha256 pimli (`tools/vp9-olcumu/kur.sh`). İlk plan
  depodaki Linux yöntemiydi (`apt-get install ffmpeg`, `ci.yml`/`release.yml`); koşum
  35795234554'te apt ffmpeg'inde `libvmaf` çıkmadı ve iş kırmızı bitti, ölçüm başlamadan
  değiştirildi. 9.0 hattı ürünün Windows ffmpeg'iyle (GyanD 9.0) aynı majör; libvpx derlemesi
  yine farklı olabilir, sayılar bu derleme için geçerlidir.
- Kesitler koşucuda lavfi ile üretilir, internetten örnek inmez. 1920x1080, 24 fps, 10 sn
  (240 kare), `yuv420p`, FFV1 kayıpsız referans. Her iş kesiti kendisi üretir; sha256
  özette yan yana yazılır ve aynı kesitin sha256'sı işler arasında farklıysa özet bunu
  "UYUŞMUYOR" diye işaretler.
  - `gren`: `testsrc2` + zamansal gürültü (`noise=alls=18:allf=t+u`)
  - `gradyan`: `gradients` (yavaş dönen düz gradyan, bant riski)
  - `hareket`: `testsrc2` + `scroll` (her karede büyük global kayma)
- Ürün argümanları: `-c:v libvpx-vp9 -deadline good -cpu-used N -row-mt 1 -pix_fmt yuv420p`,
  WebM, ses yok.
- Kalite: `tools/VidShrink.Bench measure-pair` (VMAF-NEG ortalaması, XPSNR). Süre: kodlamanın
  duvar saati (iki geçişte iki geçişin toplamı), aynı koşucuda sırayla.

## Izgara

- **cpu-used**: 0..8, iki geçişli VBR, sabit iki hedef: **1500k** ve **4000k**. Her
  (kesit, hedef) çifti tek koşucuda, adımlar sırayla koşar.
- **CRF**: 20, 25, 30, 35, 40, 45, 50; `-crf N -b:v 0`, cpu-used 4 (ürün varsayılanı).
- **CRF iniş denemesi**: CRF ızgarasından sonra, her kesit için ölçek **diğer iki kesitin**
  ızgarasından kurulur (CRF'e karşı log bit hızının geometrik ortalaması, doğrusal ara
  değer, tam sayıya yuvarlanır); o CRF ile 1500k ve 4000k hedefleri için gerçekten kodlanır
  ve bit hızı ölçülür. Bu, ürünün göremediği içeriğe ölçeği uygulamanın dürüst hâlidir.

## Karar Ölçütü

### cpu-used

- Her (kesit, hedef) hücresinde **taban** = cpu-used 0'ın VMAF-NEG ortalaması.
- Bir adım bir hücrede **kabul** edilir: VMAF-NEG ≥ taban − 0,3.
- **Ürün değeri** = altı hücrenin **hepsinde** kabul edilen adımlar arasında süresi en kısa
  olan (eşitlikte büyük adım). Hızlı mod değeri de aynı tabloda raporlanır; Hızlı için ayrı
  ölçüt yok, yalnız 5'in kalite kaybı sayıyla yazılır.
- "Aynı bayt" şartı: bir hücrede adımların bit hızı birbirinden %3'ten fazla sapıyorsa o
  hücrenin VMAF-NEG karşılaştırması "bayt tutmadı" diye işaretlenir ve hükme girmez.
- Ürün değeri mevcut `4`'ten farklıysa hüküm "**önerilir**: cpu-used X", aynıysa
  "**önerilmez**: 4 kalır".

### CRF

- İki geçişin iniş hatası: cpu-used 4 hücrelerinde |gerçek bit hızı − hedef| / hedef.
- CRF'in iniş hatası: iniş denemesinde |gerçek bit hızı − hedef| / hedef.
- **CRF açılır** ancak altı hücrenin **en kötüsünde** CRF'in hatası iki geçişin en kötü
  hatasından **küçükse**. Ölçek iki geçişten daha az sapmazsa **CRF açılmaz**; hüküm
  "önerilmez", `Vp9CrfUnmeasuredTwoPass` gerekçesi "ölçüldü, iki geçiş daha isabetli" diye
  güncellenmeye aday olur (ürün kodu bu işte değişmez).
- Bilgi için: CRF 20..50 eğrisi (kbps, MB, VMAF-NEG) kesit başına tabloya yazılır.

## Sonuç

Koşum **35795367223** (etiket `olcum-vp9-2`, commit `b805b062`), ffmpeg
`n9.0.2-3-ga5923073bf`, 4 çekirdek. Üç kesitin sha256'sı dokuz işte aynı. Ham veri
`vp9-cpu-used-crf-ham.json`; tabloların hepsi oradaki `satirlar`dan.

Not: ham JSON'daki `urunCpuUsed: 0` ve özetteki "ürün değeri: 0" satırı, bayt kuralını
uygulamadan hesaplandı (betik hatası, sonra düzeltildi: `tools/vp9-olcumu/kos.ps1`). Aşağıdaki
hüküm ölçütü kelimesiyle uyguluyor.

### cpu-used

VMAF-NEG farkı cpu-used 0'a göre; süre iki geçişin toplamı (sn); bayt yayılımı hücredeki
en büyük / en küçük kbps − 1.

| cpu | gren 1500 | gren 4000 | gradyan 1500 | gradyan 4000 | hareket 1500 | hareket 4000 | toplam sn |
|---|---|---|---|---|---|---|---|
| 0 | 0 / 223 | 0 / 354 | 0 / 134 | 0 / 86 | 0 / 112 | 0 / 174 | 1083 |
| 1 | −0,56 / 111 | **+0,01 / 225** | −0,18 / 69 | −0,23 / 56 | −0,11 / 57 | 0 / 89 | 607 |
| 2 | −1,19 / 79 | −0,43 / 129 | −0,29 / 49 | −0,33 / 45 | −0,30 / 42 | −0,01 / 58 | 402 |
| 3 | −3,23 / 59 | −1,86 / 70 | −0,48 / 26 | −0,11 / 21 | −1,00 / 37 | −0,03 / 49 | 262 |
| 4 | −3,17 / 49 | −1,78 / 57 | −0,59 / 23 | −0,22 / 16 | −1,50 / 26 | −0,03 / 33 | 204 |
| 5 | −4,30 / 50 | −2,31 / 68 | −1,08 / 25 | −0,58 / 23 | −2,81 / 28 | −0,21 / 43 | 238 |
| 6 | −3,96 / 52 | −2,41 / 69 | −1,10 / 24 | −0,60 / 22 | −2,68 / 29 | −0,28 / 42 | 239 |
| 7 | −3,86 / 51 | −2,35 / 69 | −1,08 / 24 | −0,57 / 23 | −2,90 / 29 | −0,17 / 42 | 237 |
| 8 | −4,47 / 51 | −2,59 / 67 | −1,02 / 24 | −0,57 / 23 | −3,09 / 28 | −0,23 / 42 | 236 |
| bayt yayılımı | %7,2 | **%2,6** | %16,5 | %59,8 | %11,5 | %4,3 | |

- **Bayt tuttu** yalnız gren/4000 hücresinde. Öbür beşi "bayt tutmadı": gradyan her iki
  hedefin altında kalıyor (cpu 0-2 4000k'da −%38..−41), hareket 1500k'da cpu 7-8 −%6..−10
  iniyor. Bu beş hücre ölçüte göre hükme girmez.
- Gren/4000'de kabul edilen adımlar (taban 92,509 − 0,3 = 92,209 ve üstü): cpu 0 (92,509,
  354 sn) ve cpu 1 (92,516, 225 sn). En hızlısı **cpu-used 1**.
- **Hüküm (ölçütün kelimesiyle): önerilir — cpu-used 1.** Hüküm tek bir sentetik hücreye
  dayanıyor; bu hücrede cpu 1, ürünün 4'ünden 3,9 kat yavaş (225 / 57 sn) ve 1,78 VMAF-NEG
  iyi. Ürün kodu bu işte değişmedi.
- Hızlı mod (`5`): 4'e göre altı hücrenin altısında hem daha düşük VMAF-NEG (−0,18..−1,31)
  hem toplamda daha uzun süre (238 / 204 sn) verdi. Bu ölçümde 5, 4'ten hızlı değil.

### CRF

CRF eğrisi (cpu-used 4, `-b:v 0`), kbps:

| crf | gren | gradyan | hareket |
|---|---|---|---|
| 20 | 98646 | 120,4 | 11955 |
| 25 | 69609 | 92,0 | 10275 |
| 30 | 38801 | 64,6 | 8430 |
| 35 | 12562 | 51,2 | 6423 |
| 40 | 4490 | 42,0 | 4520 |
| 45 | 2688 | 35,2 | 2812 |
| 50 | 1311 | 32,0 | 1668 |

Aynı CRF'te kesitler arası bit hızı 800 kata kadar açılıyor (CRF 20: 98646 / 120 kbps).

İniş denemesi (ölçek diğer iki kesitten) ve iki geçişin (cpu-used 4) iniş hatası:

| kesit | hedef | CRF | CRF kbps | CRF hata | iki geçiş kbps | iki geçiş hata |
|---|---|---|---|---|---|---|
| gren | 1500 | 20 (ölçek dışı) | 98646 | +%6476 | 1400 | −%6,7 |
| gren | 4000 | 20 (ölçek dışı) | 98646 | +%2366 | 3868 | −%3,3 |
| gradyan | 1500 | 50 | 32 | −%97,9 | 1257 | −%16,2 |
| gradyan | 4000 | 41 | 40 | −%99,0 | 3756 | −%6,1 |
| hareket | 1500 | 30 | 8430 | +%462 | 1489 | −%0,7 |
| hareket | 4000 | 20 (ölçek dışı) | 11955 | +%199 | 3956 | −%1,1 |

En kötü hata: CRF %6476, iki geçiş %16,2. CRF ölçeği iki geçişten daha az sapmıyor.
**Hüküm (ölçütün kelimesiyle): CRF açılmaz — önerilmez.** `Vp9CrfUnmeasuredTwoPass`
gerekçesi "ölçüldü, iki geçiş daha isabetli" diye güncellenmeye aday.

### Sınırlar

- Kesitler sentetik; gradyan içeriği 4000k'yı dolduramayacak kadar kolay, bu yüzden cpu-used
  hükmü altı hücreden yalnız birine dayanıyor. Gerçek kesitli bir tekrar hükmü sağlamlaştırır.
- libvpx sürümü BtbN derlemesinin; ürünün GyanD 9.0 derlemesindeki libvpx ile aynı olduğu
  ölçülmedi.
