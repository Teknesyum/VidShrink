# İleri Renk Ölçümü (Later 88, DZB-36/38)

Tarih: 2026-10-18. Dal: `yan/egriler-renk`. Ölçen test: `DuzenleyiciIleriRenkTests.CanliKaredeOnceSonraOlcusu`
(64x36 lavfi kaynak, `-threads 2`, teslim zinciri `ClipFilters.Color`, kare `rgb24` ortalaması).

## Önce / Sonra

| Kol | Kaynak | Ayar | Önce | Sonra |
|---|---|---|---|---|
| Eğri | gri 0x808080 | orta ton 0,5 → 0,8 | luma 128 | luma 203 |
| Teker | koyu gri 0x606060 | orta ton 240°, miktar 0,6 | R 95, G 95, B 95 (B−R 0) | R 79, G 78, B 109 (B−R 30) |
| Otomatik renk | soluk, yeşile kaymış | `normalize` | ortalama R 105,6 G 144,0 B 80,6; sapma 63,3; aralık 190 | R 121,7 G 133,0 B 114,8; sapma 18,2; aralık 246 |
| Kontrol | aynı kaynak | düz teslim (doygunluk 0,99) | — | sapma 65,4; aralık 176 |

`colorbalance` tam 0,5 griyi (0x808080) oynatmaz; bu yüzden teker kolu 0x606060 kaynakla ölçülür.
libmpv önizlemesi tırnaklı `curves=master='…'` grafını EDL zamanında kabul ediyor (`LibmpvTirnakliEgriGrafiniEdlZamanindaUygular`).

## Renk Eşleme

İki çekimi birbirine eşleyen hazır bir libavfilter süzgeci yok. Bu turda yalnız ölçüldü, arayüze girmedi; açık.

## Mutasyon

Sekiz mutasyonun sekizi kırmızı (`DuzenleyiciIleriRenkTests`, her biri python ile geri alındı):

| # | Mutasyon | Düşen test |
|---|---|---|
| 1 | `pl=1` → `pl=0` | 3 |
| 2 | teslimde eğri/teker parçası eklenmez | 7 |
| 3 | `HasColor`dan `HasGrade` çıkar | 7 |
| 4 | JSON `autoColor` anahtarı okunmaz | 1 |
| 5 | orta ton eğri kutusu gölge değerini gösterir | 1 |
| 6 | teker yeşil kanalı açısı ters | 3 |
| 7 | orta ton açısı sarılmaz | 1 |
| 8 | `normalize` gücü 0 | 4 |
