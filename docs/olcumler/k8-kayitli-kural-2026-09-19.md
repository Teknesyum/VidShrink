# Kayıtlı Kural Ölçüm Tarafında Kalıyor (19 Eylül 2026)

## Bulgu

`FfmpegArguments.SceneMapRuleOfRecord` üretimde sıfır görünüm, ölçüm tarafında beş (kod
borçları denetimi, madde 12). Pimin kendi gerekçesi "düşürmek ölçüm düzeneğini kırar,
karar ayrı sözleşme" diyordu; karar verildi ve ölçüldü.

Üretimin sahne haritası `ThresholdRule.Measured` ile türetiliyor
(`SceneDetector.cs:131`, `EncodeRunner.cs:578`). Kayıtlı kural aynı altı sayının **altın
kopyası**: `Az_bolme_duzeltmesi_olculdugu_kuralda_kalir` ikisinin kesim listesini
karşılaştırıyor — sabiti sabite değil, iki bölüşü.

## Karar

Borç değil, şartın kendisi. Bir kayıt ancak ölçülenden **bağımsız** durduğu sürece
kayıttır: alan üretime indirilse ya da `ThresholdRule.Measured`'dan okusa, ölçü sabiti
kendisiyle karşılaştırır ve kural kaysa bile hep yeşil kalır.

Pim borçtan meşruya çevrildi, gerekçesi bu bağımsızlığı yazıyor. Davranış değişmedi;
değişen sınıflandırma ve onun altındaki ölçü.

## Mutasyon turu

Süzgeç `FfmpegArguments` + `SceneMap` + `OluUye`.

| # | Kesim | Kırmızı | Kızaran ölçü |
| --- | --- | --- | --- |
| Taban | — | 0/115 | — |
| M1 | Üretimin kuralı kaydı (`Measured` Slope 2.09 → 2.30) | 2/115 | `Az_bolme_duzeltmesi_olculdugu_kuralda_kalir`, `TuretilenEsik_KiskacHerIkiUctaBaglar` |
| M2 | Kayıt `Measured`'dan okuyor, kayma da duruyor | 1/115 | yalnız `TuretilenEsik_KiskacHerIkiUctaBaglar` |
| M3 | Kaydın alanları yer değiştirdi (Offset ↔ Percentile) | 2/115 | `Az_bolme...`, `Kayitli_kuralin_alt_ucu_bolusu_erisilebilir_ucta_degisiyor` |
| Geri | — | 0/115 | — |

## Beklentinin yanlış çıktığı yer

M2'nin **yeşil** geleceğini yazmıştım; 1/115 kırmızı geldi. Sayı değil, **kimin**
kızardığı önemli: M1'de iki ölçü birden yakalıyor, M2'de kayıt pimi susuyor ve geriye
yalnız `SceneMapTests.TuretilenEsik_KiskacHerIkiUctaBaglar` kalıyor — `Measured`'ın
kıskacını bağımsız olarak pimleyen ikinci bir ölçü.

Yani "kayıt düşürülürse kayma görünmez olur" cümlesi yanlıştı. Doğrusu: kayma yine
görünür, ama **bu alanı ayakta tutan pim** kör olur; kayıt tek başına hiçbir şey
korumayan bir kopyaya döner. Sonuç değişmiyor — alan bağımsız kalıyor — gerekçenin
cümlesi düzeltildi.

Sürücü `.calisma/kayitli-kural/mutasyon.py`, ham çıktı `.calisma/kayitli-kural/sonuc.txt`.
