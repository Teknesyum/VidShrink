# A3 İzle — İki Denetim Turunun Bulguları

Dal `t0/hb-a3-izle`. Denetim bağımsız ajan tarafından yapıldı, düzeltmeler aynı dalda.

## Birinci tur (KALDI)

- **KRİTİK.** `Cli/Locales/en.json` ve `tr.json` yardım metninde izle kullanım satırı, izle
  paragrafı ve `--aralik` / `--bir-kez` satırları altı kez tekrarlıydı. Sebep, mutasyon
  koşumları sırasında düzenleme betiğinin aynı metne tekrar tekrar uygulanması.
- **ORTA 1.** Ctrl+C'nin 130 döndürdüğünü ölçen test yoktu; `Cancelled => InBand` mutasyonu yaşıyordu.
- **ORTA 2.** `--bir-kez` dosya hatasıyla bitince 0 dönüyordu; belirgin kod gerekiyordu.
- **ORTA 3.** Salt okunur izlenen klasörde durum dosyası yazılamıyor, izleme düşüyordu.
- **ORTA 4.** Kararlılık kuralı tek yoklamaya bakıyordu; Unix'te kilit yoklaması işe yaramıyor.
  Kodlama sırasında büyüyen kaynak işlendi sayılıyordu.
- **ORTA 5.** `README.md` izle komutunu "planned, not shipped" diye anıyordu.
- **DÜŞÜK.** Geçici hatanın yeniden denenmemesi, `_shrunk` ekine göre kör eleme, Linux'ta
  harf duyarsız klasör kıyası, `--cikti` ve `--json` yardım metinleri, `Flush(true)`.

## İkinci tur (GEÇTİ, kapatılan kalemler)

- **ORTA 1.** `ExitCodes.WatchFailures` sabitini 7 yapan mutasyon yaşıyordu: yardım ve README
  "4" derken testler sabitin kendisiyle kıyaslıyordu. Çıkış kodu artık çıplak sayıyla pimli.
- **ORTA 2.** `PathComparison`'ın macOS kolu hiçbir koşucuda ölçülmüyordu (CI test işi
  windows-latest, ubuntu işi filtreli). `ComparisonFor(bool windows, bool mac)` dikişi ve
  `[Theory]` ile üç bileşim her koşucuda pimlendi.
- **DÜŞÜK.** Durum dosyasının atomik yer değiştirmesi (geçici dosya geride kalmıyor) ölçülüyor;
  README'nin "iki ardışık tarama" cümlesi kodun üçüncü yoklamada alması gerçeğine göre düzeltildi.
- **Borç.** `Flush(true)` gözlemlenebilir değil (güç kesintisi taklit edilemiyor), testsiz duruyor.
