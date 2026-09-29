Hiçbir bulgu çıkaramadım, çünkü 28 çekimin hepsi boş.

Her dosya 1920x1040 RGBA ve her pikseli `(0,0,0,0)`, yani tamamen saydam. PIL `getextrema()` dört kanalda da `(0,0)` döndürdü; ekranda bembeyaz görünüyorlar. Yani başsız çizim hiçbir şey çizmemiş, çekim düzeneği boş kare kaydetmiş.

| Dosya | Yer | Gördüğüm | Önem |
|---|---|---|---|
| 28 dosyanın tamamı (`tr/en`, `basit/gelismis`, `100/125/150`, `2/3/4/6`) | Tüm kare | Bütün pikseller saydam, sekme, yazı ya da kutu yok | yüksek (çekim hatası) |

**Arayüz bulgusu:** 0. Bu, arayüzün temiz olduğu anlamına gelmiyor; bakılacak görüntü yoktu.

**Şüpheli:** Başsız çizimde kare, pencere düzenlenip görüntülenmeden önce alınmış olabilir. Arka plan saydam kalıp içerik hiç çizilmemiş de olabilir. Kod okumadığım için hangisi olduğunu söyleyemem.

Görüntüler yeniden çekildikten sonra gözden geçirmeyi tekrar yapabilirim. Yeni çekimden önce en az bir PNG'nin alfa kanalının sıfır olmadığına bakmak yeterli.

Dosyalar: `C:\Users\Teknesyum\Desktop\Projeler\VidShrink\.calisma\kaydirmasiz\cekim\*.png`
