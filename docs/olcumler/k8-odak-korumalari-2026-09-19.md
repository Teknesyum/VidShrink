# K8 Borcu 19 — Ortak Odağın Ölçü Zarfı

19 Eylül 2026. Ölçüler `tests/VidShrink.Tests/KayitOdakTakibiTests.cs`,
mutasyon düzeneği `.calisma/borc19/mutasyon.py`, ham çıktı `.calisma/borc19/sonuc.txt`.

## Bulgu

Borç "iki dosyanın ölçü zarfı en dar" diyordu ve **şüpheli** işaretliydi: incelik mi
yokluk mu ölçülmemişti. Ölçülünce ikisi ayrıldı.

`CurrentMedia` dardı ama boş değil — `OrtakOdakTests` sekiz ölçüyle damgayı, tazeliği,
yeniden yoklamayı, odak düşmesini ve ölü yüzeyi pimliyor. Zarfı dar gösteren şey dosya
sayısıydı, kapsam değil.

`MainWindow.OdakTakibi.cs` tarafında ise iki **koruma** ölçüsüzdü: takip sırasında
oynatıcının kendi olayının geri dönmesini engelleyen `_following`, ve oynatıcı açılışı
patladığında takibi düşürmeyen yutma kolu. İkisi de kullanıcıya çıkan bir davranış —
biri aynı dosyanın küçültme sekmesine iki kez yüklenmesi, öbürü kaydın teslim edilmemesi.

## Eklenen ölçüler

`TakipSirasindaOynaticidanGelenOlayIkinciKezYuklemiyor`: sahte oynatıcı açıcısı olayı
geri sürüyor, takip sırasında yükleyici bir kez koşuyor. Olumsuz kontrol takibin
bitişinde: aynı olay bu kez yüklüyor, yani koruma kalıcı değil.

`OynaticiAcilisiPatlasaDaTakipDusmuyor`: açıcı istisna atıyor, iş başarıyla bitiyor,
küçültme sekmesi yüklenmiş kalıyor ve sonraki olay yine işliyor.

## Mutasyon dökümü

Taban 0/14, geri 0/14. Üç kesim, üçü kırmızı.

| Kesim | Kırmızı |
|---|---|
| M1 geri dönüş koruması kalktı | 1 |
| M2 oynatıcı istisnası yutulmuyor | 1 |
| M3 koruma kalıcı kalıyor | 2 |

## İlk tur ölçüyü reddetti

M1 ve M3 ilk yazımlarında derlemeyi kırdı: koruma kaldırılınca `_following` okunmayan
alana düşüyor, `_following = _following` da kendine atama. Depoda uyarı hatadır, bu yüzden
düzenek `DERLEME KIRIK - olcum gecersiz` dedi — yanlış yeşil değil, okumayı reddetti.

Kesimler derlenebilir hale getirildi (`_following && path.Length < 0` ve `_ = _following;`)
ve ikinci turda üçü de kırmızı. Bu, 19 Eylül'de önizleme rozeti turunda düzeneğe eklenen
derleme kapısının ikinci kez işe yaramasıdır.
