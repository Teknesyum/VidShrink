Tema: duzenleyici GUI · kaynak: duzenleyici-gui.md

# Açık kaynak düzenleyici arayüzleri — dışa aktarma akışı

Tarama 2026-08-22, sayılar `gh api` ile depodan. `shotcut/shotcut` 404; gerçek depo `mltframework/shotcut`.
Üçünde de **çıktı boyutu tahmini yok** — VidShrink'in ayrıştığı yer burası, ödünç alınacak desen değil.

## Kdenlive — KDE/kdenlive

**Ne yapıyor:** Render penceresi: aramalı hazır ayar ağacı + iş kuyruğu sekmesi.
GPL-3.0 · 5.500 yıldız · GitHub aynası, issue kapalı (0 görünüyor; geliştirme invent.kde.org) · son push 2026-08-22 · GitHub'da yayın yok, son etiket v26.08.0.

**Alınacak fikir:**
- Kalite tek normalize yüzde kaydırıcı: hazır ayar kendi aralığını bildirir, arayüz 0–100'e eşleyip `%quality` yer tutucusunu doldurur (`renderwidget.cpp` 1511+). Kullanıcı CRF sayısı görmez; aynı desen hız kaydırıcısında da var.
- Hata modal değil, satır içi şerit (`error_box` / `error_log` / `infoMessage`). İş koşarken pencere kapanmaz, günlük istenirse açılır.
- Kalan süre metni çoğul-duyarlı (`i18np`) ve yanına anlık hız koyar: "kalan 00:04:12 (kare 812 @ 47 fps)". Tahmin yanlış çıksa bile hız doğrulanabilir kalır.

**Alınmayacak:** `qualityGroup` gibi opsiyonel grup kutusu — kapalıyken hazır ayar değeri, açıkken kullanıcı değeri. İki kaynaklı durum plan özetinde belirsizlik yaratır; VidShrink'te tek gerçek hedef boyuttur.

**Nereye dokunur:** `src/VidShrink.App/MainWindow.xaml.cs` (ilerleme metnine hız), `src/VidShrink.App/LanguageCatalog.cs` (çoğul biçimler), `src/VidShrink.Ffmpeg/EncodeRunner.cs` (ilerleme olayına fps taşı).

## Kaynaklar

- `gh api repos/mltframework/shotcut` + `/releases/latest`; `src/docks/encodedock.{cpp,ui}`, `src/jobs/abstractjob.cpp`
- `gh api repos/KDE/kdenlive` + `/tags`; `src/dialogs/renderwidget.cpp`
- `gh api repos/OpenShot/openshot-qt` + `/releases/latest`; `src/windows/export.py`, `src/presets/`
