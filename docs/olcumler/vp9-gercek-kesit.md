# VP9 Cpu-Used Gerçek Kesitli Tekrar

Durum: **ölçüldü**. Bu bölüm ölçümden önce yazıldı (commit `4ebc725c`); sayılar ve hüküm
"Sonuç" bölümünde, ölçüt metni değiştirilmedi.

## Soru

`docs/olcumler/vp9-cpu-used-crf.md` hükmü (`önerilir: cpu-used 1`) tek bir sentetik hücreye
(gren/4000) dayanıyor; Danışma 013 (`docs/danisma/013-fable-vp9-cpu-used-karari.md`) 1 ile 2
arasını ayırmak için **gerçek kesitli** kısa bir tekrar istedi. Ürün varsayılanının 1'e
inmesi ayrı, ölçülmüş bir kararla zaten main'e girdi (`ff81d654`); bu tur o kararı açmıyor,
yalnız 1 mi 2 mi sorusuna gerçek görüntüyle kanıt topluyor.

## Kaynak Kuralı

İnternetten yeni örnek video inmiyor. `docs/olcumler/vp9-cpu-used-crf.md`'nin sentetik
kesitlerinin (`lavfi`) yerine, `kalite-olcumu.yml` ve `handbrake-kiyas.yml`'nin zaten
kullandığı açık kaynak film **Sintel** (`https://download.blender.org/durian/movies/Sintel.2010.1080p.mkv`,
CC BY 3.0, Blender Foundation) kullanılır. Kesit seçimi de aynı düzenekten:
`tools/kalite-paketi-3/kos.ps1 -Is kesit`, sinyal istatistiğine göre karanlık/parlak/orta/
hareketli dört 10 saniyelik FFV1 kayıpsız kesit çıkarır. Bu ölçüm üçünü kullanır:
**karanlık, orta, hareketli** (gren/gradyan/hareket sentetik üçlüsünün gerçek karşılığı;
parlak dışarıda kalıyor çünkü orta zaten düz/az doku riskini — gradyanın gerçek karşılığını
— taşıyor).

## Düzenek

- İş akışı: `.github/workflows/vp9-gercek-kesit.yml`, `windows-latest`.
  `workflow_dispatch` varsayılan dalda olmayan akışı bulmuyor; önceki ölçümdeki gibi
  `olcum-vp9-gercek-*` etiketiyle tetiklenir.
- ffmpeg: ürünün paketlediği **GyanD `codexffmpeg` 9.0 full build**, sha256 pimli
  (`F42F0C4B04EAE3AC918707FF66E3E0FF0CEE527BFA6D322624D4BC1160D5055E`,
  `tools/kalite-paketi-3/kur.ps1`'deki aynı kurulum). Önceki ölçümün BtbN Linux
  derlemesinden farkı burada kapanıyor; libvpx sürümü artık ürünle aynı paket.
- Ürün argümanları: `-c:v libvpx-vp9 -deadline good -cpu-used N -row-mt 1 -pix_fmt yuv420p`,
  WebM, ses yok (önceki ölçümle aynı).
- Kalite: `tools/VidShrink.Bench measure-pair` (VMAF-NEG ortalaması, XPSNR). Süre: iki
  geçişin duvar saati toplamı, aynı koşucuda sırayla.

## Hedef Bit Hızı Seçimi

Önceki ölçümde sabit hedefler (1500k, 4000k) kesitlerin çoğunda doldurulamadı ("bayt
tutmadı") — tasarım hatasıydı. Bu turda hedef, **her kesit için CRF eğrisinden** seçilir:

1. Her kesitte, ürün varsayılanı `cpu-used 4` ile CRF ızgarası (20, 25, 30, 35, 40, 45, 50;
   `-crf N -b:v 0`, tek geçiş) koşulur ve kbps eğrisi çıkarılır.
2. Kesitin hedefi, o eğrideki **CRF 30** noktasının kbps'i (tam sayıya yuvarlanır). CRF 30,
   ızgaranın ortası; bir kesitin gerçekten üretebileceği bir bit hızı olduğu için iki geçişli
   VBR'ın onu doldurabilmesi beklenir (sabit 1500k/4000k'nın aksine).
3. "Bayt tuttu" şartı önceki ölçümle aynı kalır: bir hücrede cpu 1/2/4 adımlarının kbps'i
   birbirinden **%3'ten fazla** sapıyorsa o hücre hükme girmez.

## Izgara

- **Kesit**: karanlık, orta, hareketli (Sintel).
- **cpu-used**: 1, 2, 4 (Danışma 013'ün istediği üçlü; 27 kodlama yerine 9).
- Her (kesit, cpu-used) hücresi: iki geçişli VBR, kesitin CRF-30 hedefinde.

## Karar Ölçütü

- Her kesitte **taban** = cpu-used 1'in VMAF-NEG ortalaması (bu turda 0 yok; 1, üç adımın en
  yavaşı/en kalitelisi).
- Bir adım bir kesitte **kabul** edilir: VMAF-NEG ≥ taban − 0,3 (önceki ölçümle aynı eşik).
- **Hüküm**: cpu-used 2, **yalnız bayt tutan kesitlerin hepsinde** kabul ediliyorsa
  "**önerilir: cpu-used 2**"; değilse "**önerilmez: 1 kalır**". cpu-used 4 bu hükme girmez,
  yalnız bağlam için ölçülür ve bilgi tablosuna yazılır (varsayılanın 1'e inme kararı zaten
  main'de; bu ölçüm onu açmıyor).
- Özet cümle, tablodaki sayılarla birebir tutmalı; hesap ham JSON'dan (`vp9-gercek-kesit-ham.json`)
  yeniden yapılıp doğrulanacak.

## Sınırlar (ölçümden önce bilinen)

- Üç gerçek kesit de aynı kaynaktan (Sintel, animasyon); farklı bir film/gerçek çekim
  sahne çeşitliliği ölçülmüyor.
- CRF 30 hedefi her üç kesit için de doldurulabilir olmayabilir; doldurmayan kesit varsa
  "bayt tutmadı" diye işaretlenip hükme girmeyecek — bu, sonuç bölümünde açıkça yazılacak.

## Sonuç

Koşum **35827886622** (etiket `olcum-vp9-gercek-1`, commit `c2120523`), Windows koşucu,
ffmpeg `9.0-full_build-www.gyan.dev` (GyanD, ürünle aynı derleme), 4 çekirdek. Üç kesitin
sha256'sı `crf` ve `cpu` işleri arasında aynı. Ham veri `vp9-gercek-kesit-ham.json`.

Not: koşum bitince özet betiğinin (`tools/vp9-gercek-kesit/kos.ps1`, `ozet` kolu) yazdığı
"gecersiz" satırı yanlıştı — betik, bu ölçütte olmayan bir ek şart eklemişti ("hüküm yalnız
üç kesitin üçü de bayt tutarsa geçerli"), ölçüt metni ise yalnız *bayt tutan* kesitler
arasında birliği şart kılıyor. Betik düzeltildi (fazladan şart kaldırıldı); aşağıdaki tablo
ve hüküm ham JSON'dan elle yeniden hesaplanıp doğrulandı, koşum tekrar edilmedi (veri
değişmedi, yalnız özet metni hesaplama hatası taşıyordu).

### CRF Eğrisi (cpu-used 4)

kbps, kesit başına, hedef seçimi için:

| kesit | crf 20 | crf 25 | **crf 30** | crf 35 | crf 40 | crf 45 | crf 50 |
|---|---|---|---|---|---|---|---|
| karanlık | 6271 | 4691 | **3323** | 2283 | 1541 | 1040 | 704 |
| orta | 1584 | 1119 | **752** | 486 | 334 | 232 | 171 |
| hareketli | 7289 | 5599 | **4064** | 2811 | 1946 | 1353 | 932 |

### cpu-used 1/2/4, hedef = kesitin CRF 30 kbps'i

VMAF-NEG farkı cpu-used 1'e göre; süre iki geçişin toplamı (sn); bayt yayılımı hücredeki
en büyük / en küçük kbps − 1.

| kesit | hedef kbps | cpu | süre sn | kbps | sapma | VMAF-NEG | fark (cpu 1'e) | kabul |
|---|---|---|---|---|---|---|---|---|
| karanlık | 3323 | 1 | 102,61 | 3324,9 | +0,06% | 98,180 | 0 | evet |
| karanlık | 3323 | 2 | 73,07 | 3325,1 | +0,06% | 97,828 | **−0,352** | **hayır** |
| karanlık | 3323 | 4 | 51,34 | 3396,3 | +2,21% | 96,645 | −1,535 | hayır |
| orta | 752 | 1 | 67,71 | 753,9 | +0,25% | 92,883 | 0 | evet |
| orta | 752 | 2 | 49,17 | 754,2 | +0,30% | 92,421 | −0,462 | hayır |
| orta | 752 | 4 | 37,08 | 844,7 | +12,33% | 92,503 | −0,380 | hayır |
| hareketli | 4064 | 1 | 111,49 | 4065,5 | +0,04% | 98,921 | 0 | evet |
| hareketli | 4064 | 2 | 75,51 | 4063,3 | −0,02% | 98,821 | −0,100 | evet |
| hareketli | 4064 | 4 | 52,34 | 4043,1 | −0,51% | 98,463 | −0,458 | hayır |

- **Bayt tuttu**: karanlık (yayılım %2,15) ve hareketli (yayılım %0,55). **Bayt tutmadı**:
  orta (yayılım %12,04) — cpu-used 4'ün iki geçişi 752 kbps hedefini %12,33 aşıyor; bu
  hücre hükme girmez.
- Bayt tutan iki kesitte cpu-used 2: hareketlide kabul (−0,100 ≥ −0,3), karanlıkta
  **reddedilir** (−0,352 < −0,3). Ölçüt "hepsinde kabul" istiyor; karanlığın reddi tek
  başına yeter.
- **Hüküm (ölçütün kelimesiyle): önerilmez — cpu-used 1 kalır.** cpu-used 4 hükme girmedi
  (yalnız bilgi için ölçüldü); mevcut ürün varsayılanının 1'e inme kararı (`ff81d654`)
  değişmiyor, gerçek kesitli tekrar onu 1'in aleyhine çevirmedi.

### Sınırlar

- Üç kesit de aynı kaynaktan (Sintel, animasyon); gerçek çekim (canlı aksiyon) içerik
  ölçülmedi.
- CRF 30 hedefi "orta" kesitinde iki geçişli VBR ile doldurulamadı (cpu-used 4'te +%12,33);
  bu kesit hükme hiç girmedi, ölçüt tek geçerli/geçersiz ayrımıyla bunu doğru işaretledi
  ama grid'i büyütmek (örn. CRF 25 hedefi) "orta" için de bir hüküm üretebilirdi — bu tur
  bunu denemedi.
