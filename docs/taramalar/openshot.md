Tema: duzenleyici GUI · kaynak: duzenleyici-gui.md

# Açık kaynak düzenleyici arayüzleri — dışa aktarma akışı

Tarama 2026-08-22, sayılar `gh api` ile depodan. `shotcut/shotcut` 404; gerçek depo `mltframework/shotcut`.
Üçünde de **çıktı boyutu tahmini yok** — VidShrink'in ayrıştığı yer burası, ödünç alınacak desen değil.

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
