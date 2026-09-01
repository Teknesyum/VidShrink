# T84 tur 2 — mutasyon denetimi

Her düzeltme geri alındı ve ilgili ölçünün kırmızıya döndüğü koşuldu. Kırmızıya
dönmeyen ölçü koruma vermiyor demektir; tur 2'nin açılma nedeni tam buydu.

Koşum: `dotnet test -c Release tests/VidShrink.Tests/VidShrink.Tests.csproj --filter <ad>`
Ağaç: `T84-tur2-olcuyu-gercege-baglama`, worktree `C:\vs-t84`.

| # | Geri alınan düzeltme | Ölçü | Sonuç |
| --- | --- | --- | ---: |
| M1 | `tr/main.json` · `main.convert.crf-label.tip` kısaltılmadan önceki haline döndürüldü | `TipOverflowTests` | Başarısız 1 / 2 |
| M2 | Sekme seçimi `main.tab.advanced` yerine `main.tab.settings` başlığına çevrildi | `EveryTabIsMeasuredAtTheSmallestSize` | Başarısız 2 / 2 |
| M3 | İlk etiketli rozet `InfoButtonBesideLabel` yerine `InfoButton` temasına döndürüldü | `InfoBadgesAlignWithTheirLabelAndTheQuestionMarkFits` | Başarısız 1 / 1 |
| M4 | `RestoreSettings` içindeki `if (settings.FastGpu.HasValue)` kapısı geri kondu | `ResetRestoresEveryControlToItsDefault` | Başarısız 1 / 1 |
| M5 | `TipLineMetrics.Ceiling` elle `746` yazıldı, `TooltipMaxWidth` 780→700 | `TipLineThresholdsComeFromTheThemeAndEveryLineCanWrap` | Başarısız 1 / 1 |
| M6 | `tr/settings.json` içinde `settings.reset-cancel` anahtarı yeniden adlandırıldı | `ResetCopyComesFromBothLocaleFiles` | Başarısız 1 / 1 |

Altı mutasyonun altısı kırmızıya döndü.

## M6'nın kendi bulgusu

İlk denemede M6 **yeşil** kaldı. Ölçü `text.Contains("settings.reset-cancel")` ile
alt dizge arıyordu; mutasyonun ürettiği `settings.reset-cancel-x` o dizgeyi zaten
içeriyor. Ölçü anahtar kümesini `JsonDocument` ile okuyup eşitlik arayacak biçimde
yeniden yazıldı, mutasyon o zaman kırmızıya döndü.

## Ölçülen hizalama sapması

`InfoBadgesAlign…` ilk koşumunda 44 rozetin **30'u** etiketinin dikey ortasından
**4,5 px** aşağıda çıktı. Neden: etiket kendi alt boşluğunu (`LabelMargin` = `0,0,0,8`)
taşıyor ve satırı aşağı doğru büyütüyor, rozet ise satırın dikey ortasına yerleşiyor.

İlk denenen düzeltme — boşluğu etiketten satıra taşımak — hizalamayı düzeltti ama
sayfayı **6 px** kaydırdı (`ThePageScrollsAtMostDownAtTheDesignSize(loaded: True)`):
düğme `TargetMinSize` (24) yer tutuyor, etiket 19 + 8 = 27; boşluk eklenince satır
32'ye çıkıyor.

Yürürlükteki düzeltme yüksekliği değiştirmiyor: etiket yanındaki rozet
`InfoBadgeLabelMargin` (`6,0,0,8`) ve `InfoBadgeSize` (18) taşıyor — 18 + 8 = 26,
satır etiketin 27'sinde kalıyor, rozetin ortası 9,5'e oturuyor. Görünen rozet zaten
18 px'di; kalan 3 px görüntüsüz bir halkaydı. Dokunma hedefi olan denetimler
`TargetMinSize` kullanmayı sürdürüyor.

44 rozetin tamamı 2 px tavanının altında ve soru işareti hiçbirinde kırpılmıyor.

## Kapsam dışı yazımlar

Birinci turun `owns` dışına yazdığı görsel değişiklikler tek tek ele alındı.

**Doğrudan yazılmış opaklık kalmadı.** `Theme.axaml` içindeki sekiz fırça —
`PhoenixGlowInner/Mid/Outer`, `PhoenixEmberSpark`, `EmberAtmosphereOuter/Core/Veil/Coal`
— opaklığını artık belirteçten alıyor. Ekranda tek piksel değişmiyor: aynı sayılar,
adlandırılmış hâlde. `TheThemeCarriesNoInlineOpacityValues` bunu ölçüyor;
`Opacity="0"` dışarıda tutuluyor, o bir ton değil "hiç görünme" demek.

`ThemeTokenTests.TheGlowAndTheEmbersAreOpacityVariantsOfTheSameRamp` özniteliği
doğrudan sayıya çeviriyordu ve bu taşımayla kırıldı; belirteç adını çözecek biçimde
düzeltildi. Ölçünün kendisi gevşetilmedi.

**`PhoenixOpacity` 0.30 → 0.08 duruyor.** Bu bir görsel yoğunluk kararı; ölçüyle
doğrulanacak bir sayı değil ve `teknesyum-ui` kurulu olmadığı için yerine bir değer
uydurulmadı. Kullanıcının kararına bırakıldı.

**`Playback.axaml`** yalnız bir yorum satırında `PhoenixOpacity`'nin yeni değerini
anıyor; işlevsel yazım yok.

## `TooltipMaxWidth` 460 → 780 gerekçesi

Alt sınır ölçüldü: 460'ta (sarma genişliği 426) 190 satırın **135'i** taşıyor,
**25'i** tek kelimeyle taşıyordu — `t27-ipucu-satir-genislikleri-once.md`. 780'de
(746) 204 satırın **40'ı** taşıyor, tek kelimeyle taşıyan **yok**.

Üst sınırı pencerenin tabanı koyuyor: balon `MinWidth` 1040'tan geniş olamaz, yoksa
kullanıcı pencereyi küçülttüğünde balon ekrandan taşar.
`TheTooltipBubbleFitsTheNarrowestWindow` bunu ölçüyor.

## Ölçüm düzeneğinin kendi tuzağı

M6 sonrası tam süit iki kırmızı verdi: `settings.reset-cancel-x` hâlâ ortadaydı.
Kaynak dosya geri alınmıştı, ama `Locales/*.json` derleme çıktısına **tarihe bakarak**
kopyalanıyor; geri alınan dosya eski tarihli olduğu için MSBuild kopyayı tazelemedi ve
test mutasyonlu kopyayı okudu. Mutasyondan sonra kaynak dosyaya `touch` gerekiyor.
