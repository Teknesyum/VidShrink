Sen bir arayüz gözden geçiricisisin. Kodu okuma; yalnız görüntülere bak. Arka plana komut atma, her şeyi ön planda yap. Görüntüyü küçültmek ya da kırpmak için Python/PIL kullanabilirsin; çıktıyı yalnız `C:\Users\Teknesyum\Desktop\Projeler\VidShrink\.calisma\t0-arayuz\gorsel-bakis\tur3\` altına yaz.

Görüntüler: `C:\Users\Teknesyum\Desktop\Projeler\VidShrink\.calisma\kaydirmasiz\cekim\` altında 28 PNG. Bu bir masaüstü video aracının (VidShrink) ana penceresi, başsız çizim.

Önceki iki turun kareleri bozuktu: giriş canlandırması saydamlık 0'da kalıyordu (Küçült'ün yan sütunları görünmüyordu), başlık şeridinin kademesi hiç hesaplanmıyordu (%150'de dil düğmeleri sekmelerin üstüne biniyordu), paylaşım kartı ve varsayılan ayarlar yüklenmiyordu. Bu kareler düzeltilmiş çekimdir; önceki bulguları taşıma, sıfırdan bak.

Dosya adı: `<dil>-<kaydedici kipi>-<ölçek>-<sekme>.png`
- dil: tr (Türkçe) ya da en (İngilizce)
- kaydedici kipi: basit ya da gelismis
- ölçek: 100, 125, 150. 100'de pencere 1920x1040 mantıksal. 125 ve 150'de ekran aynı (1920x1040 piksel), mantıksal pencere 1536x832 ve 1280x693. Çekimler bu yüzden hep 1920x1040 piksel.
- sekme: 2 Küçült, 3 Dönüştür, 4 Kaydedici, 6 Ayarlar

Hedef: tam ekranda (100) hiçbir sekme kaydırma istemesin. Yüzde sekseni boş kutu olmasın. Kullanıcı alanı verimli kullansın.

Bilinen ve bulgu sayılmayacaklar:
- 125 ve 150'de sayfanın kaydırması (oranlar ölçülü).
- Çıktı adı yer tutucuları ({ad}, {hedef}...) İngilizcede de Türkçe: motorun gerçek belirteç adları.
- Başsız çizimde sürüm "Okunuyor..." ve ffmpeg yolu makineye özgü.
- Sayfa kartları kısaysa kartların altındaki pencere zemini boş kalır; bu kutu değil, bulgu sayma. Kutunun kendi içindeki boşluk bulgudur.

Her görüntüde şunları ara:
1. Kesilen, üst üste binen ya da kutusundan taşan yazı ve simge.
2. Büyük kısmı boş kutu ya da panel.
3. Hizasız sütunlar, eşit olması gerekirken belirgin eşitsiz kart genişlikleri.
4. Okunmayan yazı.
5. Harf ortasından bölünen sözcük.
6. Yanlış dilde kalmış metin, ham anahtar (ör. `main.` ile başlayan).
7. Aynı bilginin iki kez yazılması.
8. Tam ekranda (100) sayfanın altında kesilmiş içerik.
9. Seçimle çelişen denetim (ör. kapalı bir özelliğin ayarı açık görünüyor).

Her bulgu için şunları yaz:
- dosya adı
- görüntüde yaklaşık yer: piksel koordinatı ya da bölge adı
- ne gördüğün, tek cümle
- önem: yüksek, orta ya da düşük

Emin olmadığın şeyi "şüpheli" diye ayrı başlıkta ver. Bulgu yoksa "0 bulgu" yaz. Cevabı Türkçe ver, tablo halinde, 60 satırı geçmesin.
