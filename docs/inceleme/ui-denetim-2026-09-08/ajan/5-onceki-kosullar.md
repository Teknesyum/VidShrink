# Ajan 5 — Önceki denetim koşullarının durumu (ham çıktı, 107.910 token, 201 s)

**Belge notu.** 007/008/009 dosyalarının her satırına ` Danisma 00N — …` öneki yapışmış (009:1 `Danisma 002 — UI Bitis Kapisi Denetcisi# Danisma 009`); 009 kendi uyarısıyla tam metin değil. Yol haritası (`docs/tasks/yol-haritasi.md`) motor paketleri; arayüz maddesi yalnız P1 1.3 HDR seçimi.

## 009 — yedi geçiş koşulu

1. **YAPILDI.** Plan paneli düğmeden bağımsız yerleşimde (`MainWindow.axaml:621-632`); `RefreshPlanView` mod satırına `CRF n` (`MainWindow.axaml.cs:2862`), tahmini boyut satırı (`:2870`), ayrıca `TxtEstimateValue` (`axaml:724`, `cs:2907-2910`).
2. **YAPILDI.** `ChkResolution/ChkFps` ve yongalar `OnOptionChanged`'e bağlı (`cs:172-176`) → `ScheduleRecalculate` 160 ms (`cs:420-432`) → `Recalculate` → `RefreshPlanView` (`cs:2645`); değişen sayı `Pulse` alıyor (`cs:2908`).
3. **YAPILMADI.** İzinler hâlâ iki bağımsız `CheckBox` (`MainWindow.axaml:498-499`); sıralanabilir kontrol kanıtı bulunamadı.
4. **YAPILMADI.** Kaynak satırları tek durum (`axaml:262-291`), plan satırları yalnız plan değeri (`cs:2866-2867`); `→` yalnız koşum sonucu metninde (`Locales/tr/main.json:204,208`).
5. **YAPILDI** (kontrol olmaktan çıktı). `CmbIntent` referansı 0; `Intent` yalnız yonga tablosunda (`cs:1260-1267`), `ChipArchive` (`axaml:321`), türetme satırı `RefreshChipDerivation` (`cs:1306-1311`). CRF ofsetinin ekranda açıkça yazıldığı bir yer yok; etkisi plan panelindeki CRF'nin değişmesiyle görünüyor.
6. **YAPILDI.** Sol `ORİJİNAL` / sağ `İŞLENMİŞ` (`Playback/ComparisonPanel.axaml.cs:262-263`, `Locales/tr/playback.json:2-3`); yaklaşık parçada `İŞLENMİŞ · CRF n` (`Playback/PanelHost.cs:203-205`); `approximate-preview` anahtarı kalkmış (grep boş); perde örtünce etiket sönüyor (`ComparisonPanel.axaml.cs:306-314`).
7. **YAPILDI** (kod + birim testi). Tekerlek `ComparisonPanel.axaml.cs:84,386-416` → `ZoomGesture.Wheel:187` tek `Apply` yolu; `ZoomGestureTests.cs:49-62` tavana kadar her çentik ilerliyor. Zoom'da kararma düzeltmesi için ayrı kanıt aramadım.

## plan.md tablo ↔ kod / git

- **B / T185 "acilacak" — aslında bitti.** `git 2661e72 T185 muhurlendi`; `TabPlayer` indeks 0, `SelectedIndex="1"` (`axaml:186-187`). Tablo güncellenmemiş.
- **C / T188 "acilacak" — aslında bitti.** `git 99b6d33 T188 muhurlendi`; C5 öneri şeridi bağlı (`cs:252-255`), C6 `FileAssociationSetup.Ensure` (`App.axaml.cs:78`).
- **D / T186, E / T187:** git'te sözleşme yok; taşma/kesme paneli kodda yok (`overflow|tasma` grep boş). "acilacak" doğru.
- **A3 (mühürlü) KISMEN:** plan "sağda `ISLENMIS · CRF <x>`" diyor; CRF yalnız yaklaşık kesitte ekleniyor (`PanelHost.cs:203-205`), tam çıktıda rozet sadece `İŞLENMİŞ` (`ComparisonPanel.axaml.cs:263`).
- A1 ses: `Playback/PreviewAudio.cs` var. A4 basa sarma: kanıt aramadım.

## 007 iş listesi (10 madde)

1. Amaç kaldır → **YAPILDI** (yukarıda 5).
2. Üç satır → tek satır → **YAPILMADI.** 17 `ComboBox`'ın 9'u hâlâ `Stretch` (`MainWindow.axaml`).
3. ≤3 seçenekli listeler şerit → **KISMEN.** Küçült'te `CmbCodec/CmbFillPolicy/CmbHdrPolicy` yok, `RbHdrPreserve` (`axaml:442`), `RbAdvModeCrf` (`:538`); Dönüştür'de `CmbQualityMode` hâlâ ComboBox (`:854-855`).
4. Bütçe defteri (hedef/tahmin/fark, kontrol altı MB deltası) → **KISMEN.** Tahmin + aralık + "kaynağın %x'i" var (`axaml:717-734`, `cs:2910`); kontrol başına MB deltası kanıtı bulunamadı.
5. Bölümler + katlı başlık özeti → **YAPILDI** (4 bölüm). `SecQuality/SecFrame/SecAudio/SecAdvanced`, `RefreshSectionSummaries` (`cs:1317`).
6-8. Taşma paneli, kesme alt seçimi, sert-tavan koşulu → **YAPILMADI** (T187 açılmadı).
9. Karşılaştırma alanı A/B kare → kanıt aramadım.
10. Açılış sekmesi Küçült pinli → **YAPILDI** (`axaml:186`).

## ui-requirements-history — eskimiş satırlar

- §8 "Amaç ve kodek seçicileri yan yana durabilir" (satır 146) ve §16 "`?` rozeti … Amaç" (satır 288): Amaç kontrolü artık yok, belge güncellenmemiş.
- §8 "`Çözünürlük Düşürülebilir` / `Kare Hızı Düşürülebilir`" (satır 148): kodla uyumlu (`main.json:96-97`), ama 009/3 ile çelişiyor — hangisi geçerli, belge söylemiyor.
- 007 olgusu "23 ComboBox, `CmbIntent` üç seçenekli": bugün 17 ComboBox, `CmbIntent` yok.

## plan.md "C borçları" (arayüzle ilgili olanlar)

- C1 imzalama, C2 `release.yml` süzgeci, C3 gelistirici kipi kapalı makine, C4 Explorer görsel doğrulama: ölçülmedi / açık.
- C5 öneri şeridi ölü kod → **kapandı** (`cs:252-255`). C6 ProgID kaydı ölü → **kapandı** (`App.axaml.cs:78`). plan.md ikisini hâlâ borç yazıyor.

## plan.md "Raftakiler"

- Test paralelliği (B seçeneği) — aynı madde iki kez yazılmış; dokuz ölçülmemiş `Call from invalid thread` hatası önünde, 0.3.0 sonrasına ertelendi.
- Max sıkıştırma modu açılışı — T193 açıldı (`git 1fd9a3a`), arayüz işi değil.
- F2 pim yeniden temellendirme (D ile aynı dalda) ve F3 README ekran görüntüleri — açık.
