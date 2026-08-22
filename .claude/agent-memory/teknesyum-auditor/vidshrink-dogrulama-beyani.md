---
name: vidshrink-dogrulama-beyani
description: VidShrink relay turlarinda build/test/canli-ekran dogrulamasi hep builder beyani olarak geliyor; denetciye komut ciktisi verilmiyor
metadata:
  type: project
---

VidShrink sözleşmelerinde `## Doğrulama` maddeleri (`dotnet build` 0 uyarı,
`dotnet test` N/N, canlı ekran) denetime **komut çıktısı olarak gelmiyor**; yalnızca
builder'ın sözleşmeye ve `LOG.md`'ye yazdığı cümle olarak geliyor. T6, T7, T8'de aynı.

**Why:** Denetçinin çalıştırma aracı yok; T0 çıktıyı denetim isteğine eklemiyor.
Beyanı kanıt sayarsan denetim kendi kendini onaylayan bir halkaya dönüşür.

**How to apply:** Build/test satırlarını her zaman `? kanıtsız` yaz, turu bu yüzden
kaldı verme — bunlar Doğrulama maddesi, Kabul kriteri değil. Kabul kriterlerini kod
okuyarak kapat. Derlemenin gerçekten olduğunu kanıtlaman gerekirse WPF projesinde
`src/*/obj/{Debug,Release}/net8.0-windows/*.g.cs` içindeki üretilmiş `x:Name` alanlarına
bak — bu, XAML'in derlendiğinin somut izidir (uyarı sayısını kanıtlamaz).
İlgili: [[vidshrink-owns-siniri-kopyalama]]
