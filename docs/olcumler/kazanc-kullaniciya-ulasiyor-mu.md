# Bilerek durma kullanıcıya ulaşıyor mu (T100)

T89 ölçülen kaliteyi plana bağladı: plan "burada durmak daha iyi" diyebiliyor.
`EncodeRunner` bunu bant altı kaza sayıp bütçeyi yeniden dolduruyordu. Bu belge,
durmanın gerçekten kazanç olup olmadığını ölçer.

Soru tek: **bütçeyi harcamamak kullanıcı için iyi mi?** Küçük dosya kazanç, ama VMAF
düşer. İkisi yan yana ölçülmeden kısıt savunulamaz.

## Düzenek

- Ölçüm aracı `tools/VidShrink.Bench`,
  `shrink <kaynak> <hedef> --measured-quality --fill filltarget`.
- Kaynaklar `.calisma/kaynak/parca-{1,2,3}.mkv` — üçü de 1080p60 HDR hevc, 60 s.
- Kalite VMAF-NEG (mean / harmonic / p10), yanında XPSNR.
- Üç kol: **önce** (`main` 14db838), **kapı** (durma korunuyor, taban yok),
  **taban** (durma yalnız `FillBand.HardFloorMb` üstünde korunuyor).

## Ölçüm

| kaynak | hedef | kol | deneme | çıkan | mean | harm | p10 | taban |
|---|---|---|---|---|---|---|---|---|
| parca-1 | 40 MB | önce | 3 | 39,04 MB | 82,15 | 79,98 | 72,78 | ok |
| parca-1 | 40 MB | kapı | 1 | 37,39 MB | 82,44 | 80,52 | 76,98 | ok |
| parca-1 | 40 MB | taban | 1 | 37,39 MB | 82,44 | 80,50 | 76,92 | ok |
| parca-2 | 40 MB | önce | 3 | 39,13 MB | 94,72 | 56,42 | 95,14 | ok |
| parca-2 | 40 MB | kapı | 1 | 8,67 MB | 91,28 | 55,16 | 90,75 | **İHLAL** |
| parca-2 | 40 MB | taban | 3 | 39,14 MB | 94,72 | 56,41 | 95,12 | ok |
| parca-3 | 20 MB | önce | 1 | 19,51 MB | 40,51 | 20,10 | 8,51 | ok |
| parca-3 | 20 MB | kapı | 1 | 19,51 MB | 40,53 | 20,05 | 8,45 | ok |

parca-3 denetim satırı: planı 1. denemede bant içinde, değişen dal hiç çalışmıyor.

## Yargı

Kısıt **koşulsuz doğru değil.** İki uç ayrı davranıyor:

- **parca-1, bandın hemen altında durdu** (alt kenar 38,0 MB; çıkan 37,39 MB, hedefin %93,5'i). Durmak üç ölçüde de kazandı: mean +0,29,
  harmonic +0,54, p10 **+4,20** — üstelik 1,64 MB daha küçük dosyayla. Yeniden doldurma
  kaliteyi *düşürüyordu*, çünkü `PlanCalculator.Correct` CRF planını atıp 2-pass VBR'a
  geçiyor; sabit bit hızı, CRF'in kolay sahnelerden kısıp zor sahnelere aktardığı payı
  geri veremiyor. En açık fark p10'da, yani en kötü karelerde.

- **parca-2, bütçenin %78'ini harcamadan durdu.** Durmak mean −3,44, p10 −4,39
  kaybettirdi ve çıkan 8,67 MB, 40 MB hedefin sert tabanının (36,0 MB) çok altında.
  Arayüzdeki "Hedefi doldur" seçeneği kullanıcıya *"bütçenin izin verdiği en iyi
  kaliteyi çıkarır"* diyor; 40 MB isteyip 8,67 MB almak bu sözün karşılığı değil.

Sonuç: **durma yalnız çıkan dosya `FillBand.HardFloorMb`'nin üstündeyken korunur.**
Sınır uydurulmuş bir sayı değil — `FillBand` zaten "bunun altına inme" anlamıyla
taşıyordu, burada ikinci kez kullanıldı. Bandın hemen altındaki kasıtlı durma geçer,
bütçeyi boşa bırakan durma geçmez.

## Süre

**Makine paylaşımlıydı, süre yeniden ölçülmeli.** Ölçüm sırasında aynı makinede başka
ajanların ffmpeg süreçleri koşuyordu (başlangıçta 2, sonra 6). Denetim satırı parca-3
hiç değişmeyen tek denemelik planla 52,5 s → 80,7 s oynadı; bu, süre sayılarının
gürültüsünün ölçmek istediğimiz farktan büyük olduğunu gösteriyor. Kaydedilen ham
değerler (parca-1 215,6 s → 41,3 s, parca-2 150,6 s → 156 s) yön olarak beklenen
işareti taşıyor ama tek başına kanıt sayılmaz.

## Ölçülmeyen

- T89'un tablosu yeniden üretilemedi: o turun `klip` (1080p60 SDR) ve `oyun` (av1)
  kaynakları silinmiş. Bugünkü üç kaynağın hepsi HDR; **SDR ve av1 kolları ölçülmedi.**
- parca-3 `taban` kolunda yeniden ölçülmedi; planı bant içinde bittiği için değişen
  dala hiç girmiyor.
- Kalite ölçüleri tek koşumdur; **tekrar sapması ölçülmedi.**
