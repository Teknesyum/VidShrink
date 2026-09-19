# `ShareDiagnosis.Detail` Düşürüldü: Ölçü ve Mutasyonlar

Sunucunun ham metnini taşıyan `Detail` alanı üretimde hiçbir gösterim yerinde
okunmuyordu. Paylaşım sonucu üç yerde tek satır olarak yazılıyor ve üçü de yalnız
`ShareMessage.Of(result)` çağırıyor — o da `Key` + `Args` okuyor.

Alan düşürüldü. Ham metin, cümlesi kurulamayan iki kolda zaten `Args`'a giriyor:
`share.error.unexpected-detail` (`{3}`) ve `share.error.unexpected-exception` (`{1}`).

Yeni ölçüler `PaylasimHataDiliTests` içinde:
`HamSunucuMetniIcinIkinciDepoYok` (yansımalı yüzey pimi) ve
`CumlesiKurulamayanKolHamMetniArgumanaKoyuyor` (iki olumlu kol + bir olumsuz kontrol).

Koşum: `dotnet test tests/VidShrink.Tests -c Release --filter "PaylasimHataDiliTests|ShareProviderTests"`

| Kesim | Ne bozuldu | Kırmızı |
| --- | --- | --- |
| Taban | — | 0 / 61 |
| M1 | `Detail` konumlu kayıt parametresi olarak geri eklendi | derleme hatası (çağrı yerleri `retryAfter`'ı konumla veriyor) |
| M1b | `Detail` ayrı bir özellik olarak geri eklendi (sessiz geri dönüş) | 1 |
| M2 | `unexpected-detail` argümanından ham metin düşürüldü | 1 |
| M3 | `unexpected-detail` kolu hiç seçilmiyor, hep `unexpected` | 1 |
| M4 | Sınıflandırılan kola (`server-fault`) ham gövde geri kondu | 1 |

M1 mutasyonu derleyicide düşüyor; bu yüzden asıl tehlike M1b'dir — alan sessizce ikinci bir
depo olarak geri gelebilir ve derleme kırılmaz. Pim tam onu görüyor.

## Ham çıktı

```
TABAN
Başarılı!  - Başarısız:     0, Başarılı:    61, Atlanan:     0, Toplam:    61, Süre: 163 ms - VidShrink.Tests.dll (net8.0)

M1 Detail alani geri eklendi

M2 unexpected-detail argumanindan ham metin dusuruldu
Başarısız! - Başarısız:     1, Başarılı:    60, Atlanan:     0, Toplam:    61, Süre: 171 ms - VidShrink.Tests.dll (net8.0)

M1b Detail alani ek ozellik olarak geri eklendi
Başarısız! - Başarısız:     1, Başarılı:    60, Atlanan:     0, Toplam:    61, Süre: 188 ms - VidShrink.Tests.dll (net8.0)

M3 unexpected-detail kolu hic secilmiyor
Başarısız! - Başarısız:     1, Başarılı:    60, Atlanan:     0, Toplam:    61, Süre: 156 ms - VidShrink.Tests.dll (net8.0)

M4 siniflandirilan kola ham govde geri kondu
Başarısız! - Başarısız:     1, Başarılı:    60, Atlanan:     0, Toplam:    61, Süre: 164 ms - VidShrink.Tests.dll (net8.0)
```
