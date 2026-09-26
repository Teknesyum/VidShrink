# Kapanışta Güncelleme

Otomatik güncelleme Windows'ta varsayılan açık. İndirme açılıştan sonra, kurulum uygulamanın
gerçek çıkışında: yerinde takas, yeni sürüm açılmaz. Takas düşerse başlatıcı
`--install-on-exit <pid>` ile arkada kalır, uygulamanın çıkmasını bekleyip kurar, açmaz.

## Kapanışa Eklenen Süre

Ölçü `BaslaticiPanelsizTests.KapanistaTakasGercekDosyaSayisindaOlculur`. En kötü kol: uygulamanın
Release çıktısındaki her dosya (2026-09-26, `src/VidShrink.App/bin/Release/net8.0`, 424 dosya)
değişmiş sayılır. Süre `YerindeGuncelleme.Uygula`'nın tamamı: kapı, güncelleme kilidi, koşan süreç
yoklaması, 424 yeniden adlandırma, sürüm işareti. Dosyalar iki bayt; yeniden adlandırma boyuttan bağımsız.

| n | ortanca | aralık |
|---|---|---|
| 10 | 315 ms | 249-391 ms |

Test `< 3000 ms` pimler. Aynı gün 375 dosyalık ilk koşum: ortanca 239 ms (220-316).

## Açılışa Eklenen Süre

Sıfır olarak tasarlandı ve testle pimli, EkranSaati A/B'si alınmadı:

- `KapanistaGuncellemeTests.SessizIndirmeAcilisBitmedenBaslamaz` — indirme `Program.AcilisBitti`
  gelmeden çağrılmaz (bekleme kaldırılınca kırmızı).
- `KapanistaGuncellemeTests.AcilisYoluIndirmeyiBeklemeyeBirakir` — `OnWindowLoaded` indirmeyi
  beklemez (`_ = OtomatikGuncellemeyiBaslatAsync()`).
- `BaslaticiPanelsizTests.ArkaPlanBaslaticisiOtomatikAcikkenIndirmez` — başlatıcı artık uygulamayı
  doğururken arka planda indirme turu açmaz; açılıştaki eşzamanlı indirme kalktı.
