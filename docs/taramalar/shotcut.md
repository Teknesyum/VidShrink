Tema: duzenleyici GUI · kaynak: duzenleyici-gui.md

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

## Kaynaklar

- `gh api repos/mltframework/shotcut` + `/releases/latest`; `src/docks/encodedock.{cpp,ui}`, `src/jobs/abstractjob.cpp`
- `gh api repos/KDE/kdenlive` + `/tags`; `src/dialogs/renderwidget.cpp`
- `gh api repos/OpenShot/openshot-qt` + `/releases/latest`; `src/windows/export.py`, `src/presets/`
