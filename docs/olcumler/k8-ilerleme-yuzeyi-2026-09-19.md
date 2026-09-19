# Kurulum İlerlemesinin İkinci Yüzeyi Kapandı (19 Eylül 2026)

## Bulgu

`InstallProgress.Bar` ve `.Sentence` yalnız testlerin okuduğu yüzeydi (kod borçları
denetimi, madde 18). Kökü açılış panelinin silinmesi değil, **iki okuma yolu** olmasıydı:

- Çubuk: `Advance()` hesabı yapıp yeni değeri **döndürüyordu**, `Bar` aynı değeri
  alan olarak da tutuyordu. Üretim dönüşü okuyordu, ölçü alanı.
- Cümle: `Step` son cümleyi hem `_sentence`'a hem günlüğe yazıyordu. Üretim günlüğü
  satır satır çiziyor, `Sentence`'ı hiç okumuyordu.

## Karar

Tek okuma yolu bırakıldı.

- `Advance` artık `void`. İlerletmek ile okumak ayrı iş; çubuk her yerde `Bar`'dan
  okunuyor — üretim paneli (`MainWindow.Guncelleme.cs`), ölçüm aracı
  (`tools/VidShrink.Bench/CubukAtagi.cs`) ve testler aynı satırdan.
- `Sentence` silindi. Son cümle `History`'nin son satırı; ikinci kopya yok.

Devinim azaltılmışken panel hâlâ `Percent`'i çiziyor — o kolda kare ilerletilmiyor,
çünkü yumuşatma kapalı.

## Mutasyon turu

Çekirdek yasası (`KurulumIlerlemesiTests`, 14 kol):

| # | Kesim | Kırmızı |
| --- | --- | --- |
| Taban | — | 0/14 |
| M1 | Çubuk tavanı geçebiliyor | 1/14 |
| M2 | En küçük adım yok | 3/14 |
| M3 | Açılış atağı hiç bitmiyor | 1/14 |
| M4 | Yüzde geri gidebiliyor | 1/14 |
| M5 | Bitişte çubuk sıçrıyor | 1/14 |
| Geri | — | 0/14 |

Yeni üretim okuması ayrı ölçüldü (`GoruntuCek` + `BaslaticiPanelsiz`, 30 kol):

| # | Kesim | Kırmızı |
| --- | --- | --- |
| Taban | — | 0/30 |
| M6 | Panel çubuğu hiç ilerlemiyor | 1/30 |
| Geri | — | 0/30 |

Altı kesimin altısı kırmızı; iki taban ve iki geri 0/N.

Sürücü `.calisma/ilerleme/mutasyon.py`, ham çıktı `.calisma/ilerleme/sonuc.txt` ve
`sonuc-m6.txt`.
