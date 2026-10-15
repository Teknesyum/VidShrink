# VidShrink.Tests

Tek test projesi. Paralel koşum kapalı (`LanguageTests.cs` özniteliği).

**Ne zaman ne koşulur:** yerelde yalnız dokunulan alanın filtresi (`--filter`), her
teslimde. Tam süit yerelde koşulmaz — 35 dakika sürüyor ve CI'da zaten paralel koşuyor;
itmeden sonra `gh run list` yeşili teslimin şartı. `tools/kosum-kapisi` yalnız majör
sürümden (`x.0.0`) önce koşar.

**Derleme CI'nın bayraklarıyla:** teslimden önce `dotnet build VidShrink.sln -c Release
-warnaserror -m:2`. Debug derlemesi yetmez — xUnit analizörleri Debug'ta uyarı, CI'da
hata. Testle yakalanmaz, yalnız aynı bayraklarla derlenerek görülür.

- Zamanlama ölçen testleri yük altında okuma; yerelde filtreli koş, tam süit CI'da.
- Çıktı ve kanıt dosyaları `.calisma/` altına (`GirdiKanit`, `MotorKanit`).
- Sınıf sınıf test kataloğu: `test-katalogu.md` (neyi ölçtüğü, nereye kanıt yazdığı).
