# Görev paketi — T83'ü ana dala birleştir

Bu paket Sole'e verilmek üzere yazıldı. **T84 bittikten ve commit'lendikten sonra** verilir.

## Durum

İki iş aynı dosyalara paralel yazdı ve ayrıştı.

- **`main`** — üstünde senin T84 işin var: ayarların diske yazılması, sıfırlama düğmesi,
  sürüm metni, bilgi kutusu genişliği, `?` rozeti. Ayrıca anka arka planı (`Theme.axaml`,
  `Playback.axaml`). Bunların hepsi **eski** `MainWindow` üzerine yazıldı; o sürümde
  metinler hâlâ İngilizce gömülü ve çeviri görsel ağacı gezerek yapılıyor.
- **`worktree-agent-a5f76c5597f48fd0f`** (commit `b976332`) — T83. `MainWindow.axaml`,
  `MainWindow.axaml.cs` ve `Performance/` içindeki **369 metni** anahtara taşıdı,
  `LanguageCatalog`'un sözlüklerini sildi (433 → 265 satır), `Localization/Text.cs` ile
  `{loc:Text}` işaretleme uzantısını ekledi, `WindowLayoutTests`i Türkçe pencereye
  yeniden temellendirdi.

`git merge` denendi, `MainWindow` dosyalarında çakışıyor.

## Amaç

İki tarafın da kazanımı korunarak tek ağaç. Kural: **metin tarafında T83, davranış
tarafında T84 kazanır.**

## Ne yapılacak

1. `git merge worktree-agent-a5f76c5597f48fd0f` ve çakışmaları çöz.
2. `MainWindow.axaml` / `MainWindow.axaml.cs`: T83'ün anahtar tabanlı hâli **taban**.
   Senin T84'te eklediğin davranışı (ayar okuma/yazma, sıfırlama düğmesi ve onayı, sürüm
   dizgisinin `+` işaretinden kesilmesi, bilgi kutusu genişliği, rozet hizası) o tabanın
   üstüne yeniden uygula. Eklediğin **her yeni metin anahtara** taşınır; düğme ve onay
   metinleri `Locales/{en,tr}/settings.json` içine girer.
3. `Themes/Theme.axaml`: anka bloğu ve `InfoBadge*` belirteçleri ikisi de kalır.
   T83, `AiHintText` adlı `x:String`in artık okunmadığını bildirdi — sil.
4. `Playback/`: T82 mühürlü, onun sürümü kazanır. T83 derleme kırılmasın diye
   `LanguageCatalog.Playback(english, turkish)` adında dar bir köprü bırakmış ve
   `PanelHost.cs` içinde 3, `ComparisonPanel.axaml.cs` içinde 1 çağrıyı ona bağlamış.
   **Köprüyü kaldır**, o dört çağrıyı doğrudan `Strings.Get` ile
   `Locales/{en,tr}/playback.json` anahtarlarına bağla; metinler orada hazır duruyor.
5. `LanguageCatalog.cs`: T83'ün hâli kazanır. `[assembly: InternalsVisibleTo("VidShrink.Tests")]`
   satırı bu dosyada duruyor ve kalkarsa `LocalizationTests` derlenmez.

## Kabul kriteri

1. `dotnet test -c Release` tamamı yeşil — `PerformanceCheckTests` dahil, `--no-build` yok.
2. T84'ün sekiz kabul kriteri hâlâ geçiyor (sözleşme: `.claude/relay/contracts/T84.md`).
3. T83'ün sekiz kabul kriteri hâlâ geçiyor (sözleşme: `.claude/relay/contracts/T83.md`).
   Özellikle: gömülü metin kalmadığını sınayan iki ölçü, Türkçenin İngilizceden farklı
   olduğunu sınayan ölçü, dilin `Locales`e klasör kopyalayarak eklenebildiğini sınayan ölçü.
4. `LanguageCatalog.Playback` köprüsü kaynakta yok.
5. Yeni renk ya da ölçü uydurulmadı; `Theme.axaml` belirteçlerinden çıkıldı.

## Çıktı

Çakışan dosyaların listesi ve her birinde hangi tarafı taban aldığın; tam süit sayıları;
kaldırılan köprünün yerine bağlanan dört çağrının yeri.

## Notlar

- Yorum yazma; mevcut yorumları koru.
- T83, `Strings.Use` süreç geneli olduğu için testlerin paralel koşumunu kapatmış
  (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`, `LanguageTests.cs`).
  Süit 3 dk → 11 dk çıktı. Bu turda düzeltme, sadece hâlâ orada olduğunu doğrula.
