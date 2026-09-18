# Plan — Uyarı Durumu Renkten Değil Biçimden Ayrılır (Defter 73)

## Sorun

`Themes/Controls.axaml` iki durum teması taşıyor: `StatusSuccess` → `NeonSuccess`,
`StatusError` → `NeonPink`. **Uyarı durumu yok.** Somut kurban
`Recorder/RecorderView.axaml` içindeki `TxtWarning`: "kayıt yarım kaldı"
(`recorder.output.partial`) iletisi hata rengiyle yazılıyor. Yarım kayıt uyarıdır —
dosya duruyor, oynatılabiliyor.

27 paletin hiçbirinde uyarı hue'su yok. `NeonEmberColor` adı kehribar çağrıştırıyor
ama gerçek değerleri kırmızı/pembe ailesinde (#FF9D0006 … #FFFF5555), hatadan ayırt
edilmiyor.

## Karar

Danışma `docs/netlestirme/013-uyari-rengi.md`. Seçilen yol **B**: uyarı renkten değil
biçimden ayrılır. 27 palete elle renk seçmek "renk uydurma" yasağını çiğnerdi;
Neon/Synthwave/Cobalt gibi ev yapımı paletlerde yayımlanmış bir sarı-altın rol yok.
Mevcut bir anahtarı (mavi/mor) uyarıya atamak kullanıcıya yanlış anlam öğretirdi.

Ayrım kullanıcı gözüyle: **hata** = kırmızı, simgesiz, kısa. **uyarı** = gövde rengi,
önünde üçgen-ünlem simgesi, `SemiBold`. Kırmızı görünmediği için kullanıcı kayıp
paniğine girmiyor; simge ve ağırlık "oku" diyor. WCAG 1.4.1 de durumun yalnız renkle
verilmemesini istiyor — A yolu seçilseydi bile simge şart olurdu.

## Adımlar

1. `Themes/Icons.axaml` — `IconWarning`, Fluent UI System Icons `Warning` 24 Filled.
   Takımın kaynağı `docs/tasarim/fluent-simge-eslemesi.md` tablosunda; yeni satır oraya.
2. `tests/VidShrink.Tests/IkonImzaTests.cs` — yeni simgenin 256 bitlik imza pimi;
   `ImzalarBirbirindenUzak` en yakın çift uzaklığını yeniden ölçüyor.
3. `Themes/Controls.axaml` — `StatusWarning` ControlTheme: `Foreground` `TextBody`,
   `FontWeight` `SemiBold`, `TextWrapping` `Wrap`.
4. `Recorder/RecorderView.axaml` — `TxtWarning` yatay bir yığına alınır: solda
   `IconWarning`, sağda metin; tema `StatusWarning`. Erişilebilirlik için
   `AutomationProperties.HelpText` uyarı olduğunu söyler.
5. `AGENTS.md` — tek satır: `StatusError` hatada, `StatusWarning` "iş bitti ama eksik"
   durumunda.
6. Yeni bir test: yarım kayıt iletisinin hata fırçasıyla **çizilmediği**, uyarı
   simgesinin görünür olduğu. Negatif kontrol: gerçek hata kolu kırmızı kalıyor.

## Dışarıda Bırakılan

Danışmanın 3. adımı — `recorder.output.partial` metnine "dosya duruyor, oynatılabilir"
eki — 42 dil dosyasına dokunuyor ve renk boşluğundan ayrı bir iş. Deftere ayrı satır
olarak yazılıyor.
