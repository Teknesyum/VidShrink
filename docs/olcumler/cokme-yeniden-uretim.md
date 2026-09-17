# Paket 3 — Çökme Yeniden Üretimi (Kalem 23, Düzenek)

Durum: **çökme üretilemedi; eşzamanlılık kararsız test üretti.** `suit-esszamanli-kosum.md`'deki açık borç
(yerel makinede filtresiz süitin çökmesi) CI koşucusunda aynı süiti aynı anda 1, 2 ve 3 kez koşturarak arandı.

## Düzenek

- İş akışı `.github/workflows/cokme-yeniden-uretim.yml`, koşum **35109530525** (etiket `olcum-cokme-1`),
  matris `esszamanli: [1, 2, 3]`, her biri 2 tur. Betik `tools/cokme-yeniden-uretim/kos.ps1`.
- Her süreç: `dotnet test tests/VidShrink.Tests -c Release --no-build --blame-crash --blame-crash-dump-type mini
  --blame-hang --blame-hang-timeout 20m`, kendi sonuç dizini ve kendi `VIDSHRINK_SETTINGS_PATH`'i ile.
- Bellek: 15 sn'de bir ayrılmış bellek (committed) ve tavan (commit limit). Tepe değer turdaki tüm süreçler için ortak.
- Negatif kontrol: `esszamanli=1` (CI'nın bugünkü tek süreç koşumu).
- Yeniden koşmak: `git tag olcum-cokme-<n>; git push origin olcum-cokme-<n>`.

## Tablo

| Eşzamanlı | Tur | Süreç | Çıkış | Geçen | Kalan | Süre (dk) | Host çöktü | Asılma | Döküm | Tepe ayrılan GB | Tavan GB | Kalan testler |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 1 | 1 | 0 | 2427 | 0 | 26,93 | hayır | hayır | 0 | 8,07 | 18,87 | |
| 1 | 2 | 1 | 0 | 2427 | 0 | 24,48 | hayır | hayır | 0 | 9,10 | 18,87 | |
| 2 | 1 | 1 | 1 | 2426 | 1 | 29,51 | hayır | hayır | 0 | 15,09 | 18,87 | PlanCalculatorProbeTests.TheWarmedStartupStillLeavesTheHdrProbeOnTheCaller |
| 2 | 1 | 2 | 0 | 2427 | 0 | 32,24 | hayır | hayır | 0 | 15,09 | 18,87 | |
| 2 | 2 | 1 | 0 | 2427 | 0 | 30,80 | hayır | hayır | 0 | 13,16 | 21,01 | |
| 2 | 2 | 2 | 1 | 2426 | 1 | 26,70 | hayır | hayır | 0 | 13,16 | 21,01 | PerformanceCheckTests.DonanimYoluKapatilincaKararDegisiyor |
| 3 | 1 | 1 | 1 | 2424 | 3 | 31,08 | hayır | hayır | 0 | 15,13 | 18,87 | ShrinkRequestTests+QueueTests.K5_five_files_are_all_processed_exactly_once_no_loss; OynaticiAracTests.SeritDugmeleriMotoraUlasirVeMotordanGeriOkunur; OynaticiGercekGirdiTests.SeritDugmeleri_HamFareIle_MotoraUlasiyor |
| 3 | 1 | 2 | 0 | 2427 | 0 | 35,58 | hayır | hayır | 0 | 15,13 | 18,87 | |
| 3 | 1 | 3 | 0 | 2427 | 0 | 32,55 | hayır | hayır | 0 | 15,13 | 18,87 | |
| 3 | 2 | 1 | 1 | 2426 | 1 | 32,47 | hayır | hayır | 0 | 17,01 | 20,68 | OynaticiGercekGirdiTests.SeritDugmeleri_HamFareIle_MotoraUlasiyor |
| 3 | 2 | 2 | 1 | 2426 | 1 | 31,72 | hayır | hayır | 0 | 17,01 | 20,68 | OynaticiParcaTests.AltyaziDosyasiBirakilincaYuklenirVideoBirakmaOynaticidaKalir |
| 3 | 2 | 3 | 1 | 2425 | 2 | 31,80 | hayır | hayır | 0 | 17,01 | 20,68 | PerformanceCheckTests.OlcumYukAltindaYalnizAgirlasiyor; KayitMotoruTests.DuraklatilanKayitIkiParcayiBirlestirir |

Toplam test 2459, atlanan 32 (her süreçte aynı). Süreler süitin kendi `Total time` satırından.

## Sonuç

- Çökme üretilemedi: 12 süreçte host çökmesi 0, asılma 0, döküm 0.
- Negatif kontrol (`esszamanli=1`) iki turda da 2427 geçti, 0 kaldı; tepe ayrılan bellek 8,07 ve 9,10 GB.
- `esszamanli=2`: 4 süreçten 2'si birer testle kaldı (toplam 2 kalan); tepe ayrılan 15,09 ve 13,16 GB.
- `esszamanli=3`: 6 süreçten 4'ü kaldı (toplam 7 kalan); tepe ayrılan 15,13 ve 17,01 GB, ikinci turda tavanın
  (20,68 GB) %82'si.
- 9 kalışın (8 ayrı test) hepsi zamanlama, kuyruk ya da gerçek girdi/oynatıcı/kayıt testi (`DonanimYoluKapatilincaKararDegisiyor` bütçe aşımı:
  `BudgetExhausted`, `WallMs = 30072`, `BudgetMs = 30000`); yalnız biri
  (`SeritDugmeleri_HamFareIle_MotoraUlasiyor`) iki ayrı turda kaldı, geri kalan yedisi birer kez.

Karar: kod değişmedi. Eşzamanlı süit CI'da çökmüyor ama kararsız test üretiyor; yerel çökme için bu düzenek
yeterli değil. **Ölçülmedi:** yerel makinedeki çökmenin kendisi (kural gereği yerelde tam süit koşulmadı),
4 ve üstü eşzamanlılık, bellek tavanı dolduğunda davranış.

Açık kusur: ilk koşumda özet ayrıştırıcısı `Total:` arıyordu, `dotnet test` çıktısı `Total tests:` yazıyor;
`ozet.json`'daki `SonucSatiri`/`Toplam` boş geldi. Tablodaki sayılar `cikti.txt`'lerden okundu; ayrıştırıcı
aynı commit'te düzeltildi.
