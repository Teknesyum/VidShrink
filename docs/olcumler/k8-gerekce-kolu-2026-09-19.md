# Hızlı Mod Gerekçesinin Joker Kolu Kapandı (19 Eylül 2026)

## Bulgu

`HardwareVerdictReason.BitrateFloorTooHigh` hiçbir kolda adıyla ayrılmıyordu (kod
borçları denetimi, madde 14). Kullanıcıya çıkan cümleyi kuran `switch`
(`MainWindow.axaml.cs`) üç kapalı dalın ikisini adıyla yazıp üçüncüsünü `_` jokerine
bırakıyordu.

Bugün doğru çalışıyordu — çünkü kalan üç üye yukarıda erkenden dönüyor. Ama sebep
kümesine yeni bir üye girse, kullanıcı ona da "kodlayıcı ancak şu bit hızını takip
ediyor" cümlesini okuyacaktı. Sessiz, yanlış ve ölçüsüz.

## Karar

Kol adıyla yazıldı, `_` kolu `null` döndürüyor ve ölçüm cümlesi yoksa satır hiç
kurulmuyor. Tanınmayan sebep artık başka bir sebebin cümlesini ödünç almıyor.

`ArchitectureOutcome.Assumed` pimi de aynı maddedeydi; gerekçesi zaten "iki üyeli türün
olumsuz kolu, `Read` sorulunca `else`'i çalışıyor" diyordu. Borçtan meşruya çevrildi —
davranış değişmedi, sınıflandırma düzeldi.

## Ölçüm sırasında çıkan şey

İlk turda **M3 yeşildi**: taban cümlesi yerine yavaş yoklama cümlesi verilse hiçbir test
görmüyordu. Var olan kollar yalnız "satır boş değil, `nvenc` geçiyor, `•` ile başlıyor"
diyordu; hangi sayının hangi cümlede olduğu pimli değildi.

`EachClosedReasonCarriesItsOwnNumbers` eklendi: taban cümlesi iki bit hızını taşıyor ve
bütçe sayısını **taşımıyor**, yavaş yoklama cümlesi süreyi ve bütçeyi taşıyor. Kör nokta
kapandıktan sonra M3 kırmızıya döndü.

## Mutasyon turu

Süzgeç `HardwareVerdict` + `OluUye`, 54 kol (2'si atlanan).

| # | Kesim | Kırmızı |
| --- | --- | --- |
| Taban | — | 0/54 |
| M1 | Taban kolu yine joker | 2/54 |
| M2 | Boş ölçüm yine cümle kuruyor | 1/54 |
| M3 | Taban cümlesi yavaş yoklamanınkini veriyor | 1/54 |
| Geri | — | 0/54 |

Üç kesimin üçü kırmızı; taban ve geri 0/54.

Sürücü `.calisma/gerekce/mutasyon.py`, ham çıktı `.calisma/gerekce/sonuc.txt`.
