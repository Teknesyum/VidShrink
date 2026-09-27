# Çalışıyor Kanıtı

Soru: değişiklikten sonra uygulama derleniyor, açılıyor ve dokunulan alanın ölçüleri geçiyor mu.

| Ölçü | Sonuç | Kaynak |
|---|---|---|
| `dotnet build VidShrink.sln -c Release -warnaserror -m:2` | 0 uyarı, 0 hata | teslimden önce koşuldu; CI'da aynı bayrak |
| Filtreli test (`-c Release -m:2`): KontrastTests, UretilmisTemaTests, PaletteApplyTests, SettingsTabTests, BrandSpelling, OneriSeridinde, LanguageTests, AcilirGirisTests, AcilirPencerelerde, Balonlarda | 550/550 | `kontrast-sonra.log` |
| Öteki dokunulan test sınıfları: ComparisonPanelTests, NeonYesilTests, SettingsTests, ThemeBackdropTests, YerlesimDenetimiTests, OynaticiAracTests | 800/800 | `dokunulan.log` |
| Pencereler açılıyor: 6 sekme, kuyruk penceresi, 7 kaydedici penceresi, açılır liste | 36 palette kuruldu ve çizildi | `kontrast-sonra.log` (KontrastTests her ekranı kurup çizer) |
| Etiketler ana DLL'de | `labels.en.json`, `labels.tr.json` gömülü kaynak, uydu derlemeye gitmiyor | `VidShrink.App.csproj:47`, `UretilmisTemaTests` |
| `scan.js` | 0 hata | `scan-sonra.txt` |
| `esle.js --denetle` | 0 fark | `esle-sonra.txt` |
| Dal CI'ı | itmeden sonra `gh run list -b worktree-agent-ab2d9de8f21f2713f` | rapordaki Sayılar tablosu |

Kapsam dışı: tam süit yerelde koşulmadı (kural); CI koşar.
