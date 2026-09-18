# 020 — Kullanıcı Ön Ayar Kütüphanesi Arayüzden Nasıl Açılsın (Girdi)

18 Eylül 2026. Fable'a verilen metnin tamamı aşağıdadır.

---

VidShrink bir video küçültme aracı (.NET 8 + Avalonia, Windows). Motorda çalışan ama
arayüzden erişilemeyen bir yetenek var; nereye ve nasıl bağlanacağına karar vermeni
istiyorum. Kullanıcı kolaylığı önde.

**Motorda hazır olan (`src/VidShrink.Core/PresetLibrary.cs`):**

- `PresetProfile` kaydı: `Id`, `Name`, `Kind` (General / Platform / Device / User),
  `Chip`, `TargetMb`, `SizeCapped`, `Intent`, `Codec`, `Fill`, `MaxShortEdge`,
  `AudioKbps`, `Container`, `Source`.
- `SaveUser(profile)` — kullanıcı ön ayarını `%APPDATA%\VidShrink\presets.json`'a yazar,
  aynı `Id` varsa üstüne yazar, gömülü ön ayarların kimliğini reddeder.
- `LoadUser()` — o dosyadan okur, yoksa boş liste.
- `Import(path)` / `Export(profiles, path)` — dosyadan alma ve dosyaya verme; içe
  aktarılan her ön ayar `Kind = User` olur ve yonga bağı düşer.
- `BuiltIn` — gömülü ön ayarlar; bunların 8'i arayüzde **yonga** olarak görünüyor.

**Arayüzde bugün olan:**

- Küçült sekmesinde bir yonga şeridi var: 8 düğme (Arşiv, 8, 16 WhatsApp, 25, 100, 128,
  180, Yarısı). Yongaya basınca hedef boyut, niyet, kodek ve doldurma siyaseti birlikte
  değişiyor. Şerit, hedef kaydırıcısı ile kalite kaydırıcısı arasında duruyor.
- Ayarlar **ayrı bir diyalog değil, bir sekme** (Genel, Kabuk menüsü, Paylaşım,
  Güncelleme panelleri).
- Taşma menüsü, hamburger ya da sağ tık menüsü arayüzde **hiç yok** — bugüne kadar
  kullanılmamış bir kalıp.
- Dosya seçici `StorageProvider` ile üç yerde kullanılıyor; `SaveFilePickerAsync`
  hiç kullanılmamış.
- Her metin 42 dile çevriliyor; eklenen her anahtar 42 dosyaya girer.

**Sorular:**

1. Kullanıcı ön ayarları nereden açılmalı? Yonga şeridinin sonuna eklenen "+" yongası mı,
   Ayarlar sekmesinde bir panel mi, yoksa başka bir yer mi? Gerekçeni kullanıcının
   izleyeceği yol üzerinden yaz.
2. "Şu anki ayarlarımı ön ayar olarak kaydet" eylemi hangi denetimle sunulmalı ve
   kullanıcıdan ne kadar bilgi istemeli (yalnız ad mı, yoksa hangi alanların
   kaydedileceğini seçtirmeli mi)?
3. Kaydedilmiş bir kullanıcı ön ayarı arayüzde gömülü yongalardan **ayırt edilmeli mi**,
   ediliyorsa neyle (renk uydurmak yasak — ayrım biçim, simge ya da yerleşimle olmalı)?
4. İçe/dışa aktarma bu turda arayüze girmeli mi, yoksa önce kaydet/uygula/sil üçlüsü mü
   çıkmalı? Bu turda girmeyecekse neyin eksik kalacağını bir cümleyle yaz.
5. Silme eyleminde onay sorulmalı mı? Sorulacaksa nasıl — ayrı bir pencere mi, geri alma
   şeridi mi?

Kısa ve kesin yanıt ver; her soruya tek bir karar ve bir gerekçe. Emin olmadığın yerde
"ölçülmeli" de, uydurma.
