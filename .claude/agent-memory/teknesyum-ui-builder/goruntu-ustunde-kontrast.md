---
name: goruntu-ustunde-kontrast
description: VidShrink oynatıcısında görüntünün üstünde duran metnin kontrastı beyaz kareye göre hesaplanır; neon renk tek perdede AA'yı geçemez
metadata:
  type: project
---

Karşılaştırma panelinde rozet, şerit ve çip **video karesinin üstünde** duruyor. Kontrastı
zeminden değil perdeden alıyorlar: `PlaybackScrim` = `AppBgColor` %80 örtücülük.

En kötü durum **beyaz kare**dir; hesabı ona göre yap. Ölçülen değerler:

- Beyaz mono metin (`TextBody`) tek perdede **11,88:1**
- `NeonBlue` dolgu tek perdede **8,64:1**
- `NeonPink` metin tek perdede **3,49:1** — 14 px kalın metin için AA eşiği 4,5:1, **kalıyor**

Çözüm: neon renkli metnin çipine `NeonPinkFill` gibi saydam dolgu değil **ikinci bir perde**
ver. Perde üstüne perde binince etkin örtücülük %96'ya çıkıyor ve aynı ölçüm **5,87:1**
oluyor (siyah karede 6,25:1).

**How to apply:** Bu panele görüntü üstünde duran her yeni metin için zemini `PlaybackScrim`
yap ve beyaz kareye göre oranı hesaplayıp sözleşmeye yaz. Beyaz metin güvenli, neon metin
değil. İlgili: [[vidshrink-owns-listesi-daraltiyor]].
