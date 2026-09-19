# E8 — Süzgeç Yüzeyi (19 Eylül 2026)

## Bulgu

HandBrake bayrak denetimi (`docs/handbrake/bayrak-hukumleri-2026-09-19.md`) üç bağımsız
grupta aynı boşluğu gösterdi: `src/VidShrink.Core/VideoFilterChain.cs` kırpma,
keskinleştirme, gürültü giderme, deblock, deband, döndürme, dolgu ve gri tonlamayı tam
üretiyor, ama `VideoFilterChain.Parse` üretimde hiçbir yerden çağrılmıyordu.

Ölçüm: `grep -rn "VideoFilterChain.Parse" src` → 0 satır; `new PlanOptions` kuran altı
yerin hiçbiri `Filters` vermiyor. Tek canlı süzgeç otomatik çözgüydü
(`EncodeRunner.cs:116`). Yani eksik olan özellik değil, yüzeydi.

## Açılan yüzey

| Yüzey | Yer |
| --- | --- |
| CLI kolu `--suzgec` / `--filters` | `src/VidShrink.Cli/CliRequest.cs` |
| Plan bağlantısı | `CliRequest.ToPlanOptions` |
| Gelişmiş panel satırı | `src/VidShrink.App/MainWindow.axaml`, `MainWindow.axaml.cs` (`SuzgecOku`) |
| Yardım + `error.bad-filter` | `src/VidShrink.Cli/Locales/{en,tr}.json` |
| 42 dilde etiket | `main.advanced.filters.{label,hint,bad}` |
| Ölçü | `tests/VidShrink.Tests/SuzgecYuzeyiTests.cs` (5 kol) |

Ayrıştırma komut satırı okunurken yapılıyor: bozuk dizge koşum başlamadan 64 döndürüyor.
Arayüzde bozuk dizge varsayılana düşüyor ve kutunun yanındaki satır uyarıyor — plan
hesabı sessizce yanlış süzgeçle koşmuyor.

## Pim tazelemeleri

`BiciminTests`: dil başına üç anahtar geldi, gezilen 42828 → 42957 (43 × 999); kol
değiştiren anahtar 1677 → 1682 (es/pt `label`, hu/ro `bad`, ro `label`). `CliTests`:
takma ad sayısı 20 → 21, iki README'ye `--suzgec | --filters` satırı eklendi.

## Mutasyon turu

| # | Kesim | Kırmızı |
| --- | --- | --- |
| Taban | — | 0/5 |
| M1 | CLI süzgeç kolu adı değiştirildi | 4/5 |
| M2 | Bozuk dizge yutuluyor, varsayılana düşüyor | 1/5 |
| M3 | İstek plan seçeneğine geçmiyor | 2/5 |
| M4 | Boş dizge varsayılanı eziyor | 5/5 |
| M5 | Keskinleştirme zincire girmiyor | 1/5 |
| M6 | Gri tonlama zincire girmiyor | 1/5 |

Taban 5/5 yeşil, geri alma sonrası yine 5/5 yeşil. Altı mutasyonun altısı da kırmızı
verdi; sıfır kırmızı kalan kol yok. Sürücü `.calisma/e8/mutasyon.py`, ham çıktı
cp1254 kodlu `.calisma/e8/sonuc.txt`.

M4 beş kolun beşini birden kırmızı yaptı — boş dizge varsayılanı ezerse her koşuma bir
süzgeç takılır; olumsuz kontrolün asıl değeri bu.
