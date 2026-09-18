# Plan — Kullanıcı Ön Ayar Kütüphanesi Arayüze Bağlanıyor

Defter satırı: "Kullanici on ayar kutuphanesi arayuzden cagrilmiyor" (18 Eylül 2026).
Motorda `PresetLibrary.SaveUser` / `LoadUser` / `Import` / `Export` var, arayüzde tek
kullanım gömülü yongaların okunması. Karar: [020-onayar-arayuzu.md](netlestirme/020-onayar-arayuzu.md).

## Kapsam

Bu tur **kaydet / uygula / sil** üçlüsünü çıkarıyor. İçe ve dışa aktarma kapsam dışı;
eksik kalan, kullanıcının ön ayarını başka makineye taşıyamaması — `presets.json` elle
kopyalanır.

## Adımlar

1. **Şerit modeli.** `MainWindow.axaml.cs` içindeki `ChipPlans()` gömülü yongaları
   veriyor; yanına `PresetLibrary.LoadUser()`'dan gelen kullanıcı ön ayarlarını okuyan bir
   kol ekleniyor. Gömülü sekiz yonga, ince dikey ayırıcı, kullanıcı yongaları, en sonda
   "+" — sıra fable'ın kararı.
2. **Kaydetme.** "+" yongası bir flyout açıyor: tek ad kutusu ve Kaydet düğmesi. Boş ad
   kaydetmiyor; aynı ad varsa tek satırlık "üstüne yazılsın mı" sorusu çıkıyor. Kaydedilen
   profil o anki hedef, niyet, kodek, doldurma, kısa kenar, ses ve kabı taşıyor.
3. **Uygulama ve silme.** Kullanıcı yongasına basmak gömülü yonga ile aynı yolu
   (`ApplyChipPlan`'ın ön ayar karşılığı) işletiyor. Yonganın üzerindeki "×" siliyor;
   durum satırında "'Ad' silindi — Geri al" beliriyor, geri alma silinen profili yeniden
   `SaveUser` ediyor.
4. **Biçim.** Kullanıcı yongası kesikli kenarlıkla ayrılıyor; renk uydurulmuyor, ölçü
   `Themes/Theme.axaml` belirtecinden geliyor.
5. **Metinler.** Yeni anahtarlar 42 dile giriyor (ad kutusu, kaydet, üstüne yazma sorusu,
   silindi/geri al, boş ad uyarısı).
6. **Ölçü.** Yeni test sınıfı `OnAyarYongasiTests`: kaydedilen profilin alanları, boş ad,
   aynı ad, uygulama yolu, silme ve geri alma, sıra (gömülü → ayırıcı → kullanıcı → "+").
   Ayar dosyası `VIDSHRINK_SETTINGS_PATH` altında; gerçek `%APPDATA%` yazılmıyor.

## Kapsam dışı

- İçe/dışa aktarma arayüzü ve `SaveFilePickerAsync`.
- Şerit taşma davranışı (kaydırma mı satır kırma mı) — fable "ölçülmeli" dedi, ayrı iş.
- "×" jesti (hover mı sürekli mi) — ölçülmedi, sürekli görünür başlıyor.
