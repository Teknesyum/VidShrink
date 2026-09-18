# Ortak Kurallar (her ajan)

- Türkçe çalış, Türkçe rapor. Kodda yorum yok. Önce `AGENTS.md` ve `tests/VidShrink.Tests/AGENTS.md` oku ve uy.
- Beş dosya ya da fazlası: önce `docs/plan.md` başına kısa plan bölümü.
- Kendi dalında çalış, dalına it. main'e birleştirme, sürüm, etiket yok.
- Dalın CI'ı yeşil olana dek düzelt. CI'ı sınırlı döngüyle ön planda bekle: `for i in $(seq 1 90); do gh run view <id> --json status -q .status | grep -q completed && break; sleep 60; done`. Sınırsız until yasak. Beklerken turu kapatma.
- Her davranış değişikliğinin testi; her test için bir mutasyonla kırmızıya döndüğünü göster (mutasyonu geri al).
- Arayüz davranışı gerçek girdi yolundan sınanır (ham klavye/fare olayı, piksel/geri okuma); kaynak metin okuyan pin tek başına kanıt değildir.
- Tam süiti yerelde koşma; dokunduğun sınıfların filtresi (`--filter` sınıf adını eşleştirir). Build `-m:2`, build ve test ayrı komut.
- Yerel oynatıcı testleri: `VIDSHRINK_LIBMPV=C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\libmpv\libmpv-2.dll`.
- `%APPDATA%\VidShrink`'e ve kullanıcının masaüstü kurulumuna dokunma; `VIDSHRINK_SETTINGS_PATH` `.calisma` altına. Gerçek HKCU'ya yazma.
- Bu PC'de stres testi, tüm çekirdek döngüsü, ağır kodlama yok; her an tek ffmpeg. Ağır ölçüm CI'da.
- Geçici dosyalar `.calisma/` altına, bitince sil.
- Yeni kullanıcı metni 42 dilde; `BiciminTests` sayım pinlerini ve üstündeki cümleyi birlikte güncelle.
- Commit sonu: `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.
- Rapor: dal, son sha, CI koşum no + sonucu, her kalem için ne yapıldı + test adı + mutasyon sonucu, yapılmayan varsa neden. Kanıtsız "yapıldı" yazma.
