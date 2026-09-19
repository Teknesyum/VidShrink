# K8 Borcu 8 — Önizleme Rozetinin Sessiz Kaldığı Hal

19 Eylül 2026. Ölçüm `tests/VidShrink.Tests/OnizlemeRozetiTests.cs`,
mutasyon düzeneği `.calisma/onizleme-rozeti/mutasyon.py`.

## Bulgu

Karşılaştırma panelinin kısa örnek parçası nihai çıktıdan sapınca rozet çıkıyor. Rozet
gövdesi sapmayı gördükten sonra **sayıya** bakıyordu:

```csharp
return clip.Crf is { } crf ? Strings.Get("main.plan.mode.crf-value", crf) : null;
```

Kodlayıcının kalite ölçeği modellenmiyorsa (`PreviewQuality.Desteklenmiyor`) sayı hiç
üretilmiyor. Yani parçanın nihai çıktıdan **en çok** saptığı halde kullanıcı hiçbir uyarı
görmüyordu; sapmanın en görünür hali (`Yaklasik`) uyarılıyor, en ağırı susuyordu.

## Karar

7 Eylül'deki "sayı yoksa rozet de yoktur" kararı `Yaklasik` halini konuşuyordu — o gün masada
olan dert taraf adının tekrarlanması ve ham ondalığın basılmasıydı. Modellenmeyen kodlayıcı
ne tartışıldı ne örneklendi. fable'a danışıldı; kararın kapsamı değil kör noktası olduğu,
lafza uyunca ortaya çıkan sonucun (gürültü değil **sessizlik**) kullanıcının korumak istediği
şeyin tersi olduğu söylendi.

`Desteklenmiyor` haline sayısız tek kelimelik ayrı bir rozet kondu: `main.preview.temsili`,
42 dilde. "Yaklaşık" denmedi — sapmanın türü başka. Sayı varken sayı kazanır, kelime yedek
koldur.

## Mutasyon dökümü

Taban 0/12, geri 0/12. Beş kesim, beşi kırmızı.

| Kesim | Kırmızı |
|---|---|
| M1 sayısız kol düştü (eski hal) | 2 |
| M2 kelime sayıyı eziyor | 1 |
| M3 sapma şartı kalktı | 4 |
| M4 tür parçaya taşınmıyor | 1 |
| M5 modellenmeyen kodek süzgeci kalktı | 2 |

## İlk tur yalan söyledi

İlk koşumda M4 ve M5 sıfır kırmızı verdi. Sebep kod değildi: kesim derlemeyi kırıyordu,
betik `dotnet build`in dönüş kodunu okumuyordu ve `--no-build` **eski ikiliyi** koşturuyordu.
Betik artık derleme kırıkken `DERLEME KIRIK - olcum gecersiz` döndürüyor; M5 de sabit
`if (false)` yerine derlenebilir bir koşula çevrildi. İkinci turda ikisi de kırmızı.

Ölçülen ikinci gerçek: M4 ilk halinde gerçekten ölçüsüzdü — testler `PreviewClip`'i elle
kuruyordu, parçadan panele geçen dikişe hiç dokunmuyordu. Dikiş `SegmentEncoder.Klip` saf
gövdesine alındı ve `TurParcadanPaneleTasiniyor` ile uçtan uca pimlendi.

## Pim değişimi

`PreviewQuality.Desteklenmiyor` pimi düştü — üretimde adıyla okunuyor. `Yaklasik` borçtan
meşruya çevrildi: rozet sayı taşıyan her kolu aynı yazıyor, `Kesin` ile `Yaklasik`'i
ayırmak 7 Eylül kararına aykırı olurdu.
