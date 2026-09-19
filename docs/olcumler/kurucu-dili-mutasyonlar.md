# Kurucu Dili Ölçüsünün Mutasyonları

`KurucuDiliTests` (5 ölçü) kurucunun tek konuşan yüzeyini — `Core/Setup/SetupText.cs`'in
gömülü tr+en tablosunu — pimliyor. Ölçünün gerçekten davranış gördüğünü sınamak için her
yüzey bilerek kırıldı ve kaç ölçünün kırmızıya döndüğü sayıldı.

Koşum: `dotnet test tests/VidShrink.Tests -c Release --filter KurucuDiliTests`

| Kesim | Ne bozuldu | Kırmızı |
| --- | --- | --- |
| Taban | — | 0 / 5 |
| M1 | Bir anahtarın İngilizce çevirisi boşaltıldı | 1 |
| M2 | İngilizce metinden `{0}` düşürüldü | 1 |
| M3 | Türkçe metinden `HTTP {0}` düşürüldü | 2 |
| M4 | Kullanılmayan anahtar eklendi | **0 → kör nokta → düzeltildi → 1** |
| M5 | `SetupRunner`'a Türkçe cümle geri kondu | 1 |

## M4 kör noktası

İlk turda M4 sıfır kırmızı verdi. Sebep ölçünün kendisiydi: `KullanilmayanAnahtarYok`
kaynak taramasına `SetupText.cs`'i de katıyordu, dolayısıyla anahtarın **kendi bildirimi**
kullanım sayılıyordu. Tarama `SetupText.cs`'i dışarıda bırakacak şekilde düzeltildi
(`KurucuDiliTests.cs`, `Where(path => Path.GetFileName(path) != "SetupText.cs")`) ve aynı
mutasyon 1 kırmızı verdi. Yukarıdaki tablonun M4 satırı düzeltme sonrası koşumdur.

## Ham çıktı

```
TABAN
Başarılı!  - Başarısız:     0, Başarılı:     5, Atlanan:     0, Toplam:     5

M1 en cevirisi bosaltildi
Başarısız! - Başarısız:     1, Başarılı:     4, Atlanan:     0, Toplam:     5

M2 ingilizcede {0} dusuruldu
Başarısız! - Başarısız:     1, Başarılı:     4, Atlanan:     0, Toplam:     5

M3 turkcede HTTP kodu dusuruldu
Başarısız! - Başarısız:     2, Başarılı:     3, Atlanan:     0, Toplam:     5

M4 kullanilmayan anahtar eklendi (kör nokta düzeltilmeden önce)
Başarılı!  - Başarısız:     0, Başarılı:     5, Atlanan:     0, Toplam:     5

M4 kullanilmayan anahtar eklendi (düzeltmeden sonra, 2026-09-19)
Başarısız! - Başarısız:     1, Başarılı:     4, Atlanan:     0, Toplam:     5

M5 SetupRunner'a turkce cumle geri kondu
Başarısız! - Başarısız:     1, Başarılı:     4, Atlanan:     0, Toplam:     5
```
