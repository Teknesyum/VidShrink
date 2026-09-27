# turetilemeyen-olcu

`src/VidShrink.App/Themes/Theme.axaml`'da belirtece bağlanmamış ham ölçüleri (`TrackingLg` ile `LinkGitHub` arası ve
yüzey saydamlıkları) listeler, her birine gerekçe yazar. Çıktı `docs/ui-denetim/2026-09-27-uc2/turetilemeyen.txt`
(sekmeli: ad, değer, yer, durum; CRLF). Konsola satır sayısını ve gerekçe dağılımını basar.

- Koşum: `node tools/turetilemeyen-olcu/turet.mjs`.
- Gerekçeler betikteki `hesap`, `ozel` ve `tur` tablolarındadır. Eşleşmeyen satır varsayılan "standartta belirteç yok"
  gerekçesini alır; yeni bir ham ölçü sessizce oraya düşer, çıktının farkı gözle okunur.
- Özel raftaki düzen değişirse (uç 3'te köşe 3 → 4) önce tablodaki sayılar güncellenir, sonra koşulur.
