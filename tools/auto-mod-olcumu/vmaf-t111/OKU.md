# T111 arşivi — kare kilitli ölçümler

`docs/olcumler/auto-mod.md`'nin **T111 bölümündeki** her sayı buradan yeniden
üretilebilir. Eski arşiv `../vmaf/` altında duruyor; silinmedi, damgalandı
(`../vmaf/OKU.md`).

**Taban commit `3688336`.** Kodlamalar o ağaçtaki motorla üretildi; `main` o
tabandan beri `FfmpegArguments.cs`'i değiştirdi (`8ea80c4`, T98'in dinamik
anahtar kare aralığı). Bu ölçümler o değişiklikten **önceki** motoru ölçüyor.

## Dosya adlandırması

| son ek | ne | filtre zinciri |
|---|---|---|
| `-kilitsiz` | T110 öncesi ölçer — damga eşlemesi | `[0:v]<ölçek>[t];[1:v][r]` |
| `-kilitli` | T110 sonrası ölçer — kare indeksi eşlemesi | her iki zincire `settb=AVTB,setpts=N` |

Her koşum iki kez ölçüldü, **aynı dosya üzerinde**. İki sayının farkı kilidin
payıdır; yeniden kodlamanın payı ayrıca sınırlandı (T102 arşivi ↔ T111 kilitsiz,
on bir koşumda ≤ 0,013 ortalama).

## Özel dosyalar

- `kaydir-auto-ref-artiBir` — kilit takılı, **referans bilerek** `setpts=N+1` ile
  bir kare kaydırıldı. Kilitsiz ölçümün dört sayısını da birebir yeniden üretiyor
  (94,448 / 94,525 / 56,308 / 26 kare `<1`). Kaymanın **tam bir kare** olduğu
  buradan çıkarım değil, kimlik.
- `kaydir-auto-ref-eksiBir` — ters yön (`setpts=N-1`). Benzer hasar, **farklı**
  sayılar (94,452 / 94,524 / 56,529). Kimlik tek yönlü; tesadüf değil.
- `ses-*` — kaymanın kap içi ses akışından geldiğini gösteren dört ölçüm.

`settb`'siz koşum (`setpts=N` tek başına) arşive girmedi: mkv 1/1000 ile mp4
1/15360 zaman tabanı farkı yüzünden 3624 yerine 7012 kare eşliyor, yani ölçüm
değil hata kaydı.

## Ölçülmedi

- Kodlama süreleri. Makine paylaşımlıydı; hiçbir süre sayısı üretilmedi.
- `<1` kümesinin nerede olduğu dışında kare kare damga dökümü — yalnız dört
  koşumda ölçüldü (`auto`, `uzman-biz3`, `uzman-hb`, `uzman-hb2`).
- T98'in dinamik anahtar kare aralığının bu sayılara etkisi.
