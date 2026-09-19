# Taşma Düğmelerinin Ekran Okuyucu Adı — Mutasyon Dökümü

Ölçü: `TasmaKarariTests.TasmaDugmeleriEkranOkuyucuyaAdiylaGorunuyor`.
Filtre: `FullyQualifiedName~TasmaKarariTests|FullyQualifiedName~LanguageTests`.
Taban: 0 kırmızı / 306 yeşil.

| Kesim | Ne bozuldu | Kırmızı |
| --- | --- | --- |
| M1 | Kabul düğmesinin adı canlı metne yazılmıyor | 1 |
| M2 | Kırpma düğmesinin adı canlı metne yazılmıyor | 1 |
| M3 | Gizli kolda ad bayat kalıyor (`else` düşürüldü) | 1 |
| M4 | Kabul düğmesinin XAML adı kaldırıldı | 1 |
| M4b | Kırpma düğmesinin XAML adı kaldırıldı | 1 |
| M5 | Gizli kolun adı canlı metne eşitlendi | 1 |

Altısının altısı kırmızı. M4 ilk turda **hayatta kaldı**: kod her gösterimde adı
üstüne yazdığı için XAML'daki ad hiç okunmuyordu. Panel açılmadan önceki durumu
(pencere yeni, panel katlı) ölçen iki satır eklendi; kesim ondan sonra kırmızı oldu.
