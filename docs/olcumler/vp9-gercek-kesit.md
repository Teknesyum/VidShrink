# VP9 Cpu-Used Gerçek Kesitli Tekrar

Durum: **ölçüt yazıldı, ölçüm sürüyor**. Bu bölüm ölçümden önce yazıldı; sonuç aşağıya
ölçütün kelimesiyle eklenecek, ölçüt geriye dönük değiştirilmeyecek.

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

(Ölçüm bitince buraya eklenir.)
