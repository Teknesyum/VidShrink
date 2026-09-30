# Düzenleyici Klip Özellikleri: Ölçüm Ve Mutasyonlar

Tarih: 2026-10-01. Dal: `worktree-agent-a4f85634f10ca820f`. Sınıf: `tests/VidShrink.Tests/DuzenleyiciKlipOzellikTests.cs`.

## Yerel Koşum

| Filtre | Sonuç |
|---|---|
| `DuzenleyiciKlipOzellikTests` | 10/10 yeşil |
| `Duzenleyici` + `BiciminTests` + `BaslikKapsamiTests` + `LanguageTests` | 607/607 yeşil |

Derleme `dotnet build VidShrink.sln -c Release -warnaserror -m:2`: 0 uyarı, 0 hata.

Canlı kollar 320x240 lavfi, en çok 3 sn, `-threads 2`, tek süreç. Ses farkı `-ss 1` sonrası `mean_volume` ile
okundu: -10 dB ayarında ölçülen fark -10,0 dB (tüm dosyanın `max_volume`'u başlangıç sıçraması yüzünden -11,5 veriyordu).

## Mutasyonlar

Her mutasyon elle uygulandı, test projesi derlendi, yalnız `DuzenleyiciKlipOzellikTests` koşuldu, dosya yedekten geri kondu.

| # | Mutasyon | Kırmızı |
|---|---|---|
| M1 | `EditClip.SplitAt`: baş parçada `FadeOut = 0` kaldırıldı | 1/10 (`BolmeSolmayiKenardaTutarKirpmaVeHizAyariTasir`) |
| M2 | `ClipFilters`: 90° için `transpose=1` yerine `transpose=2` | 3/10 (`DisaAktarimGrafi…`, `ArayuzPaneli…`, `OnizlemeZincirleri…`) |
| M3 | `EditExport.Plan`: ayar varken Tam'a düşme kaldırıldı (`HasEffects`) | 3/10 (`AyarVarsaTamaDuser…` Fast ve Smart, `CanliDisaAktarma…`) |
| M4 | `EditorView.Apply`: kesimler aynıyken de EDL yeniden açılır (`SameCuts` yerine `SequenceEqual`) | 1/10 (`ArayuzPaneliAyariYazarEdlyiYenidenAcmadanTazeler`) |

Dört mutasyonun dördü kırmızı.

## Pin Güncellemeleri

- `BaslikKapsamiTests.KolDegistirenAnahtarlarSayilir`: 2907 → 2917, en 274 → 275, tr 102. On iki yeni anahtardan
  10 kol 5 dilde (en fade-in; fr rotate; it fade-in, fade-out, flip-h, flip-v; pt fade-in, fade-out, panel; sv fade-in).
- `AdVeBirimYazimiCumleOrtasindaDaKorunur`: 53277 → 53793 (43 x 1251), `kayip` 0.
