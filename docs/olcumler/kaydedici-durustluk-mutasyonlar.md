# Kaydedici Dürüstlük Mutasyonları

Kaynak: `.calisma/yalan-yok/envanter.md` madde 13, 14, 44, 45, 46, 47. Ölçü `tests/VidShrink.Tests/KaydediciDurustlukTests.cs`.
Her mutasyon elle uygulandı (git checkout değil), Release `-warnaserror` derlendi, `KaydediciDurustlukTests|KaydediciUyariTests` (24 test) koşuldu, kaynak geri yazıldı.

| Kod | Dosya | Mutasyon | Kırmızı |
|---|---|---|---|
| M44a | RecorderView.Serit.cs | `kalan.Count == 0` → `>= 0` | SilinemeyenKayitYoluylaSoylenir |
| M44b | RecorderView.Serit.cs | kalan dosya listeye eklenmiyor | SilinemeyenKayitYoluylaSoylenir |
| M45 | RecorderView.axaml.cs | başarısız başlık `done` | BasarisizKayitBasligiBasarisizDer |
| M46a | RecorderSession.cs | taşıma hatasında `(outputPath, null)` | TasinamayanParcaGercekYoluVeSebebiDondurur, BolumlerdenBiriTasinamazsaListeGercekYoluTasir |
| M46b | RecorderView.axaml.cs | `not-moved` kolu kapalı | TasimaHatasiUyariSatirindaSoylenir |
| M47a | RecorderView.axaml.cs | yoklanamayan → `partial` | YoklanamayanYarimKayitOynatilabilirDemez |
| M47b | RecorderSession.cs | `HasPacketAsync` hep `true` | CopDosyaPaketVermez |
| M13 | RecorderSession.cs | `MissingGif` yazılmıyor | CevrilemeyenGifMkvyiKorurVeSoyler |
| M14 | RecorderView.Tampon.cs | GIF'te de `replay.saved` | GifSeciliykenTamponMkvOldugunuSoyler |

Taban: 24/24 yeşil. Dokuz mutasyonun dokuzu kırmızı.
