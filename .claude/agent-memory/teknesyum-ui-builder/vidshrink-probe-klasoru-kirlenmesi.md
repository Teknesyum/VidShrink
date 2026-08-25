---
name: vidshrink-probe-klasoru-kirlenmesi
description: Scratchpad'teki eski probe klasöründe kalan .axaml dosyaları VidShrink.App tipleriyle çakışır; probe'u boş klasörde kur
metadata:
  type: project
---

Süreç içi ölçüm exe'sini scratchpad'te kurarken klasörün gerçekten boş olduğunu doğrula.

Önceki bir turdan kalan `MainWindow.axaml` (yanındaki .cs silinmiş olsa bile) Avalonia
derleme hedefleri tarafından işlenir ve probe derlemesinde `VidShrink.App.MainWindow`
adında ikinci bir kısmi sınıf üretir. Sonuç yanıltıcıdır: `MainWindow bir Show tanımı
içermiyor`, `VidShrink.App.MainWindow öğesinden Avalonia.Controls.Window öğesine
dönüştürülemiyor`. Avalonia sürümü değil, isim çakışması.

**Why:** T22 turu 2'de üç derleme bu yüzden kaybedildi; hata mesajı sürüm uyuşmazlığı
gibi okunuyor.

**How to apply:** probe klasörünü kurmadan önce `Get-ChildItem $p -File` ile listele,
`.axaml` ve `.axaml.cs` artıklarını sil, `obj`/`bin`'i temizle.

İlgili: [[vidshrink-pencere-ici-olcum]], [[vidshrink-ekran-disi-kosu]]
