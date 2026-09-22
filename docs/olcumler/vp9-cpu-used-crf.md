# VP9 Cpu-Used Ve CRF Ölçümü

Durum: **ölçütler ölçümden önce yazıldı**. Sayılar ve hüküm aşağıdaki "Sonuç" bölümüne,
ölçüt metni değiştirilmeden eklenir.

## Soru

`libvpx-vp9` küçültme yolunun iki ayarı ölçümsüz duruyor:

1. `-cpu-used`: ürün varsayılanı `4` (`FfmpegArguments.DefaultPreset`), Hızlı modda `5`
   (`PlanCalculator`, satır ~1769). Hangi adım ürün değeri olmalı?
2. CRF: `PlanCalculator` VP9'da CRF'e gitmiyor, iki geçişli VBR'a dönüyor
   (`ReasonCode.Vp9CrfUnmeasuredTwoPass`). Bir CRF ölçeği hedef boya iki geçişten daha iyi
   iner mi?

## Düzenek

- İş akışı: `.github/workflows/vp9-olcumu.yml`, `workflow_dispatch`, `ubuntu-latest`.
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

Ölçüm koşulmadı.
