# Arayüz elden geçirme — 0.3.1

Kaynak: [007-ui-finish-gate-arayuz-elestirisi.md](danisma/007-ui-finish-gate-arayuz-elestirisi.md).
Kullanıcının şikâyeti: ayar yüzeyi seyrek, ikili seçimler açılır listede, amaç paneli kocaman.

Ajanın merkezî tespiti: ekrandaki asıl nesne **bayt bütçesi** (hedef / tahmin / fark) ve
o ekranda hiçbir biçimde yok. Etiket + `?` + tam genişlik liste + statik ipucu kalıbı
23 kez tekrarlanıyor; yüzey herhangi bir kodlayıcı arayüzüne kopyalanabilir durumda.

## Kullanıcının verdiği kararlar (6 Eylül 2026)

- Oynatıcı sekmesi **en sola** alınır, **açılışta seçili sekme Küçült** kalır.
- İstisna: uygulama bir video dosyasıyla açılırsa (dosya ilişkilendirme / "birlikte aç")
  **Oynatıcı sekmesi seçili gelir**.
- Kaydırmasız sığma testi `MinHeight = 720` üzerinde pinlenir.
- `CmbQualityMode` gerçekte Dönüştür sekmesinde ve kolları `CRF` / `Sabit bit hızı` —
  iki kolu da adlandırılmış ikili, yani şerit olur.

## Sözleşmeler

| id | iş | owns |
|---|---|---|
| T177 | Küçült yüzeyi: `CmbIntent` kalkar, üç satır tek satıra iner, ≤3 seçenekli listeler şeride döner, sekme altı bölüme ayrılır, Gelişmiş eş seviyeye çıkar | `MainWindow.axaml` (188-742), `MainWindow.axaml.cs` ilgili bağlantılar, `Locales/**` |
| T178 | Bayt bütçesi defteri: hedef / tahmin / fark satırı, kontrol altı metinler MB deltasına döner, tahmin koşum sonrası gerçekle kapanır | `PlanCalculator`, `EncodePlan`, Küçült üst şeridi |
| T179 | Taşma teklifi: eşik %3, gömülü panel, dört seçenek her biri sayı taşır, kesme alt seçimi satır içi (son/baş/ikisi), yalnız sert tavanlı hedeflerde | Core kesme hesabı, `EncodeRunner`, koşum paneli |
| T180 | Sekme sırası + oynatıcı: Oynatıcı en sola, açılış sekmesi pinli, dosya argümanıyla açılışta Oynatıcı seçili, kısayolların tamamı doğrulanır, karşılaştırma alanı A/B kareye iner | `MainWindow.axaml` sekme gövdesi, `App.axaml.cs` argüman yolu, `PlayerView` |

Sıra: T177 → T178 → T179; T180 bağımsız, paralel koşabilir.

## Doğrulama koşulları (ajanın verdiği, aynen)

- Kaynak okuyan test: seçenek sayısı ≤3 olan `ComboBox` sayısı **0**.
- Kaynak okuyan test: `HorizontalAlignment="Stretch"` taşıyan `ComboBox` sayısı **0**.
- `CmbIntent` referansı **0**; her yonga için yonga→plan eşleme testi.
- Varsayılan ayarlarla `MinHeight = 720`'de `ScrollViewer.Extent ≤ Viewport` — **T180'de**.
  T177'de ulaşılamaz: taşmayı orta sütun tutuyor (`PlanPanelMinHeight` 868 px, görüş alanı
  625 px), onu küçültmek karşılaştırma alanının A/B kareye inmesi demek. T177 yalnız
  ölçülen taşmayı pinler.
- Katlı bölüm başlığı güncel değerleri yazar (ses bit hızını değiştir → başlık metni değişir).
- Taşma panelinin dört seçeneğinin **her biri bir sayı içerir**; "Kes"e tıklamak pencere
  sayısını değiştirmez; kesilecek saniye = süre × taşma oranı birim testi; Arşiv hedefinde
  aynı taşmada panel görünmez.
- Boş açılışta ilk görüntüde bırakma alanı ve hedef boyut var, transport çubuğu yok.

## Kural

Renk ve ölçü uydurulmaz — `Themes/Theme.axaml` belirteçleri dışına çıkılmaz.
`teknesyum-ui` deposu kurulu değil; belirteçte karşılığı olmayan bir ölçü gerekirse
iş durur ve kullanıcıya sorulur.
