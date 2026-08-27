---
name: vidshrink-kontrast-tavani
description: Koyu zemine renk eklemek kontrastı düşürür; önce zemini karart, sonra katmana yer aç
metadata:
  type: feedback
---

VidShrink'in zemini neredeyse siyah (kontrast ~19.6-20). "Kontrast bugünkünün
altına düşmeyecek" denen her işte, zemine alfa ile eklenen her katman oranı
aşağı çeker; sadece renk seçerek bu kural sağlanamaz.

**Why:** T55'te arka plana kırmızı ve anka kuşu silüeti eklendi. Var olan
duraklar üstüne %5 kırmızı bindirmek 19.66'yı 19.20'ye düşürüyordu. Çözüm
durakları önce daha koyuya (luminans olarak) almak, açılan payı silüete
vermekti: zemin 19.64 → 20.30 çıktı, silüet üstü 19.71'de kaldı.

**How to apply:** Katman opaklığını gözle seçme; tavanı hesapla. Silüet
opaklığı 0.06'da 19.71, 0.08'de 19.44 (taban 19.64'ün altı). Yani opaklığı
artırmak isteyen önce zemin duraklarını karartmalı. Ölçüm
`tests/VidShrink.Tests/ThemeBackdropTests.cs` içinde, eski duraklar taban
olarak sabit yazılı.
