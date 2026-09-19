# Kaynağın Duraklaması Şeride İniyor (19 Eylül 2026)

## Bulgu

`ComparisonSourceState.Duraklatildi` üretiliyor, hiçbir kol onu ayırmıyordu (kod borçları
denetimi, madde 15). Pim "ayırmanın gerekip gerekmediği ölçülmedi" diyordu; ölçüldü ve
**gerekiyordu.**

Karşılaştırma kaynağı kendi kararıyla duraklayabiliyor: ilk kare oynatma isteği gelmeden
üretilirse `EngineComparisonFrameSource` durumu `Duraklatildi` yapıyor
(`EngineComparisonFrameSource.cs:323`), ayrıca dışarıdan `Pause()` de aynı yere düşüyor.
Panel ise şerit düğmesini akış kurulurken `true` yapıp bırakıyordu
(`PanelHost.cs:430`) ve durumu yalnız `Kullanilamiyor` ile `Durdu` için okuyordu.

Sonuç: görüntü duruyor, düğme "oynuyor" diyor. Kullanıcıya yalan.

## Karar

`PanelHost.Report` artık oynuyor/duraklatıldı durumunu şeride yazıyor ve sesi aynı kolda
durduruyor ya da başlatıyor. Değer değişmediyse hiçbir şey yazılmıyor.

Geri besleme yok: şerit düğmesi olayını yalnız kullanıcı bastığında yayıyor
(`ControlStrip.TogglePlay`), alandan yazmak olay doğurmuyor.

`Aciliyor` bilerek dışarıda: açılış sırasında şerit kullanıcının bıraktığı yerde kalır.

## Mutasyon turu

Süzgeç `PanelHost` + `OluUye`, 31 kol.

| # | Kesim | Kırmızı |
| --- | --- | --- |
| Taban | — | 0/31 |
| M1 | Durum kolu hiç koşmuyor | 2/31 |
| M2 | Duraklatılmış "oynuyor" sayılıyor | 1/31 |
| M3 | Açılırken de şerit yazılıyor | 1/31 |
| Geri | — | 0/31 |

Üç kesimin üçü kırmızı; taban ve geri 0/31. M1'in iki kırmızısının ikincisi ölü üye
ölçüsü: kol kalkınca `Duraklatildi` yeniden sıfır tüketicili kümeye düşüyor, yani pimin
kalkması da ölçülü.

Sürücü `.calisma/duraklama/mutasyon.py`, ham çıktı `.calisma/duraklama/sonuc.txt`.
