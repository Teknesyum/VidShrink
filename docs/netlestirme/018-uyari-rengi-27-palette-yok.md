# Danışma Yanıtı — Uyarı Rengi 27 Palette Yok (18 Eylül 2026)

Danışılan: fable. Girdi `018-uyari-rengi-27-palette-yok-girdi.md`. Yanıt birebir:

---

**Karar: B.**

Gerekçe: A'nın 27 rengi de gerçekten "yayımlanmış kaynaktan" gelmez — Ayu/Nord/Dracula/Gruvbox/RosePine'ın sarı-altın rolü var ama Neon, Synthwave, Cobalt gibi ev yapımı paletlerde kaynak yok, orada yine renk uydurulur. C ise kullanıcıya yanlış anlam öğretir: mavi/mor "bilgi" okunur, "dikkat" okunmaz; ucuz ama kalıcı bir yanılgı. B tek kural ihlali yapmaz, 2 dosyaya dokunur ve erişilebilirlik gereği (WCAG 1.4.1: durum yalnız renkle verilmez) zaten yapılması gereken şeyi yapar — A seçilse bile simge şart olurdu.

Uyarı hatadan nasıl ayrılır (kullanıcı gözüyle): hata = kırmızı, simgesiz, kısa; uyarı = gövde rengi (`TextBody`), önünde üçgen-ünlem simgesi, `SemiBold`, ve metin sonucu değil **durumu** söyler ("Kayıt yarım kaldı — dosya duruyor, oynatılabilir"). Kırmızı görünmediği için kullanıcı "bir şey kaybettim" paniğine girmez; simge ve ağırlık "oku" der.

Uygulama tarifi:
1. `Themes/Controls.axaml`: `StatusWarning` ControlTheme — `Foreground={DynamicResource TextBody}`, `FontWeight=SemiBold`, sol tarafta `PathIcon`/glif (ölçü `Themes/Theme.axaml` belirtecinden, renk `TextBody`).
2. `Recorder/RecorderView.axaml:180` `TxtWarning` → `StatusWarning`; `AutomationProperties.HelpText`'e "uyarı" yaz ki ekran okuyucu da ayırsın.
3. `recorder.output.partial` metnini "dosya duruyor, oynatılabilir" ekiyle güncelle (tüm diller).
4. `StatusError` hatalarda, `StatusWarning` "iş bitti ama eksik" durumlarında — bu ayrımı `AGENTS.md`'ye tek satır yaz.

---

## Uygulanan

Plan `docs/plan.md`. 3. adım (42 dil dosyasındaki metin eki) renk boşluğundan ayrı bir iş
olarak deftere yazıldı; 1, 2 ve 4. adımlar bu turda uygulanıyor.
