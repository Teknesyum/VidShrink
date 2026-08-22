# Açık kaynak düzenleyici arayüzleri — dışa aktarma akışı

Tarama 2026-08-22, sayılar `gh api` ile depodan. `shotcut/shotcut` 404; gerçek depo `mltframework/shotcut`.
Üçünde de **çıktı boyutu tahmini yok** — VidShrink'in ayrıştığı yer burası, ödünç alınacak desen değil.

## Shotcut — mltframework/shotcut

**Ne yapıyor:** MLT düzenleyici; dışa aktarma tek dock, solda hazır ayar ağacı, sağda katlanmış sekmeli panel.
GPL-3.0 · 14.963 yıldız · 57 açık issue · son push 2026-08-21 · son sürüm v26.8.1 (2026-08-01).

**Alınacak fikir:**
- Varsayılan görünüm yalnız hazır ayar listesi + "Export File"; codec/bitrate/GOP `advancedButton` arkasında. Yardım metni "varsayılanlar çoğu kullanıcı için uygun H.264/AAC MP4 üretir", gelişmiş modda "geçersiz kombinasyon üretmeni engellemez" der — sorumluluk devrini açıkça söylüyor.
- Hazır ayar veri dosyası: görünen ad (`meta.preset.name`), açıklama balonu (`meta.preset.note`), çıktı uzantısı, gizli bayrağı. Kategori klasör yolundan türer, ağaç Custom / Stock diye ikiye ayrılır.
- Kalan süre tek satır: geçen süre / tamamlanan yüzde × kalan yüzde (`AbstractJob::estimateRemaining`). Kare değil yüzde üzerinden.

**Alınmayacak:** Dört sekmeli gelişmiş panel ve serbest metin `advancedTextEdit`. VidShrink tek pencere, tek hedef; bu yüzey doğrulanamaz kombinasyon üretir.

**Nereye dokunur:** `src/VidShrink.App/MainWindow.xaml` (hedef panelini daralt, gelişmiş alanları katla), `src/VidShrink.Core/EncodePlan.cs` (hazır ayarı veri olarak taşı).

## Kdenlive — KDE/kdenlive

**Ne yapıyor:** Render penceresi: aramalı hazır ayar ağacı + iş kuyruğu sekmesi.
GPL-3.0 · 5.500 yıldız · GitHub aynası, issue kapalı (0 görünüyor; geliştirme invent.kde.org) · son push 2026-08-22 · GitHub'da yayın yok, son etiket v26.08.0.

**Alınacak fikir:**
- Kalite tek normalize yüzde kaydırıcı: hazır ayar kendi aralığını bildirir, arayüz 0–100'e eşleyip `%quality` yer tutucusunu doldurur (`renderwidget.cpp` 1511+). Kullanıcı CRF sayısı görmez; aynı desen hız kaydırıcısında da var.
- Hata modal değil, satır içi şerit (`error_box` / `error_log` / `infoMessage`). İş koşarken pencere kapanmaz, günlük istenirse açılır.
- Kalan süre metni çoğul-duyarlı (`i18np`) ve yanına anlık hız koyar: "kalan 00:04:12 (kare 812 @ 47 fps)". Tahmin yanlış çıksa bile hız doğrulanabilir kalır.

**Alınmayacak:** `qualityGroup` gibi opsiyonel grup kutusu — kapalıyken hazır ayar değeri, açıkken kullanıcı değeri. İki kaynaklı durum plan özetinde belirsizlik yaratır; VidShrink'te tek gerçek hedef boyuttur.

**Nereye dokunur:** `src/VidShrink.App/MainWindow.xaml.cs` (ilerleme metnine hız), `src/VidShrink.App/LanguageCatalog.cs` (çoğul biçimler), `src/VidShrink.Ffmpeg/EncodeRunner.cs` (ilerleme olayına fps taşı).

## OpenShot — OpenShot/openshot-qt

**Ne yapıyor:** Python/Qt düzenleyici, dışa aktarma penceresi Simple / Advanced iki sekme.
GitHub lisans alanı `NOASSERTION` (depoda `COPYING`, kaynak başlıkları GPL) · 6.187 yıldız · 406 açık issue · son push 2026-08-17 · son sürüm v3.5.1 (2026-04-08).

**Alınacak fikir:**
- Basit sekmede dört kademeli daraltma: Proje Türü → Hedef → Profil → Kalite (Low/Med/High). Kullanıcı codec değil varış yeri seçer.
- `src/presets/` altında ~70 XML: `youtube_shorts`, `instagram_reels`, `tiktok`, `dvd_pal`. Adlar platform, codec değil — VidShrink'e doğrudan uygulanabilir adlandırma.
- ffmpeg hatası ham metin değil eşlenmiş mesaj olarak dönüyor (InvalidCodec, InvalidSampleRate, ErrorEncodingVideo → anlaşılır cümle).

**Alınmayacak:** O eşleme exception metninde alt dizi arıyor (`export.py` 1326+, kaynakta duran "TODO: daha iyi bir yol bul" notu); yerelleştirilmiş ya da sürümü değişen metinle kırılır — hata sınıfı kendi katmanımızdan üretilmeli. `titlestring` çalışma anında kurduğu biçim dizesini çeviriye veriyor; çıkarıcı göremez, "Remaining"/"Elapsed" çevrilmeden kalır. Derlenmiş `.qm` dosyalarının depoya girmesi de örnek alınmamalı.

**Nereye dokunur:** `src/VidShrink.Core/PlanCalculator.cs` (hedef adları platform odaklı), `src/VidShrink.Ffmpeg/EncodeRunner.cs` (hata sınıflandırması), `src/VidShrink.App/LanguageCatalog.cs` (biçim dizeleri sabit, çalışma anında birleştirilmiş değil).

## Kaynaklar

- `gh api repos/mltframework/shotcut` + `/releases/latest`; `src/docks/encodedock.{cpp,ui}`, `src/jobs/abstractjob.cpp`
- `gh api repos/KDE/kdenlive` + `/tags`; `src/dialogs/renderwidget.cpp`
- `gh api repos/OpenShot/openshot-qt` + `/releases/latest`; `src/windows/export.py`, `src/presets/`
