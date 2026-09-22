# B1a Ses Kodeği Kopyası: Mutasyon Tablosu

Kusur: `PlanCalculator.WithTarget` seçenek kopyası `AudioCodec` alanını taşımıyordu. Kalite
hedefi yolu (`TargetMbForQuality`) her denemede bu kopyayla planladığı için kullanıcının
AC-3/E-AC-3 seçimi AAC'ye düşüyordu. main 8f09dc60'tan beri kırmızıydı
(`OlcekModuluTests.KopyaHicbirSecenegiDusurmuyor`).

Süzgeç: `DolbySesKoluTests|OlcekModuluTests`, 70 ölçü. Taban 0/70 kırmızı.

| Kesim | Değişiklik | Kırmızı | Düşen ölçüler |
|---|---|---|---|
| M1 | `WithTarget` kopyasından `AudioCodec = options.AudioCodec` çıkar | 3 | `KaliteHedefiYoluSecimiKoruyor` (ac3, eac3), `KopyaHicbirSecenegiDusurmuyor` |
| M2 | `NewPlan` geri kolu `PickAudioCodec()` | 0 | — eşdeğer mutant |
| M3 | Eşleme kolu `PickAudioCodec()` | 6 | `AkisListesizKaynaktaSecimOkunuyor`, `KaliteHedefiYoluSecimiKoruyor`, `PanelSecimiIzinKodegineGeciyor` |

M2 eşdeğer: `StreamMapping.Decide` ses varsa ve bütçe sıfırın üstündeyse akış listesi olmasa
da bir ses izi kuruyor (`inventory` yoksa `info.HasAudio || !inventory` kolu), yani `NewPlan`'ın
`streams.Audio.Count > 0` koşulunun else'i ses varken erişilmiyor. Satır yine de seçimi
okuyacak şekilde düzeltildi; iki kol aynı kaynağı okusun.

Ham çıktı: kesimler yerelde koşuldu, 2026-09-22.
