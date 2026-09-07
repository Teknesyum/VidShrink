 Danisma 002 — UI Bitis Kapisi Denetcisi# Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi- Koltuk: `.calisma/koltuk-denetci.md` (agency.js show, 178 satir)
 Danisma 002 — UI Bitis Kapisi Denetcisi- Ajana verilen olgular: `.calisma/ui-olgular.md` (ayni dosya, tasarimciyla ortak)
 Danisma 002 — UI Bitis Kapisi Denetcisi- Tarih: 2026-09-07
 Danisma 002 — UI Bitis Kapisi Denetcisi- Karar: **HOLD**
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi> **Uyari — bu dosya tam metin degildir.** Ajanin dondugu ham metin
 Danisma 002 — UI Bitis Kapisi Denetcisi> `tasks/<id>.output` altinda tutuluyordu; o dosya bu oturumda uzerine yazildi ve
 Danisma 002 — UI Bitis Kapisi Denetcisi> geri getirilemedi. Asagisi benim oturum icindeki kaydimdir: bulgular ve sorular
 Danisma 002 — UI Bitis Kapisi Denetcisi> eksiksiz, cumleler ajanin kendi cumleleri degil. Tirnak icindeki tek cumle
 Danisma 002 — UI Bitis Kapisi Denetcisi> dogrudan alintidir.
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi## Karar
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi**HOLD.** Arayuz bugunku haliyle bitis kapisindan gecmiyor.
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi DenetcisiEn keskin bulgu, dogrudan alinti:
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi> "Bu arayuz, sikistirmanin ne yapacagini dugmeye basmadan once hicbir yerde
 Danisma 002 — UI Bitis Kapisi Denetcisi> soylemiyor."
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi DenetcisiMotor bir plan hesapliyor, panel kareleri karsilastirabiliyor, ayarlar plani
 Danisma 002 — UI Bitis Kapisi Denetcisidegistiriyor — ucu de var, ucu de birbirine bagli degil.
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi## Bulgular
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi1. **Izinler iki bagimsiz onay kutusu olarak gosteriliyor, oysa bunlar bir feda
 Danisma 002 — UI Bitis Kapisi Denetcisi   sirasi.** Kullanici "cozunurluk mu kare hizi mi once dussun" sorusuna cevap
 Danisma 002 — UI Bitis Kapisi Denetcisi   veriyor; arayuz bunu iki ayri evet/hayir gibi sunuyor.
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi2. **Bilgi satirlari once → sonra olmali.** Bugun tek durum gosteriliyor; degisimi
 Danisma 002 — UI Bitis Kapisi Denetcisi   gosteren sutun yok.
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi3. **"Amac" panelinin asil kusuru yer kaplamasi degil, etkisinin hic
 Danisma 002 — UI Bitis Kapisi Denetcisi   gosterilmemesi.** Secim plani degistiriyor (`CompressionStrategy.cs:91`,
 Danisma 002 — UI Bitis Kapisi Denetcisi   Archive -6.0 / Sharing -3.0 CRF ofseti) ama ekranda bunun izi yok; panel anlam
 Danisma 002 — UI Bitis Kapisi Denetcisi   yerine bosluk dolduruyor.
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi4. **Onizleme paneli urunun en ikna edici ani, ama iki sey onu susturuyor:**
 Danisma 002 — UI Bitis Kapisi Denetcisi   "Yaklasik onizleme" rozeti bir ozur cumlesi gibi duruyor ve zoom kirik oldugu
 Danisma 002 — UI Bitis Kapisi Denetcisi   icin panel dekorasyona dusuyor.
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi5. **Alti sekme kodun yerlesimini yansitiyor, kullanicinin isini degil.** En soldaki
 Danisma 002 — UI Bitis Kapisi Denetcisi   sekmenin varsayilan olmamasi da bunun isareti: sekme modeli ise oturmuyor.
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi## Tek onerilen degisiklik
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi DenetcisiKucult sekmesinin **ilk okumasi plan + onizleme** olsun; ayarlar bunlarin etrafina
 Danisma 002 — UI Bitis Kapisi Denetcisidizilsin ve ayar degisince ikisi de canli guncellensin.
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi## Gecis (PASS) kosullari — yedi madde
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi1. Dugmeye basmadan once tahmini cikti boyutu ve CRF ekranda goruluyor.
 Danisma 002 — UI Bitis Kapisi Denetcisi2. Ayar degisince bu iki sayi canli guncelleniyor.
 Danisma 002 — UI Bitis Kapisi Denetcisi3. Izinler feda sirasi olarak, tek bir siralanabilir kontrol halinde sunuluyor.
 Danisma 002 — UI Bitis Kapisi Denetcisi4. Bilgi satirlari once → sonra bicimine geciyor.
 Danisma 002 — UI Bitis Kapisi Denetcisi5. Amac secimi ya etkisini gosteriyor ya kontrol olmaktan cikiyor.
 Danisma 002 — UI Bitis Kapisi Denetcisi6. Onizleme rozeti ozur degil bilgi: hangi tarafin ne oldugunu soyluyor.
 Danisma 002 — UI Bitis Kapisi Denetcisi7. Zoom calisiyor; kirik etkilesim urunun en guclu anini bozmuyor.
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi## Denetcinin kullaniciya sordugu uc soru
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi1. Onizleme paneli sayfada bugun tam olarak nerede duruyor?
 Danisma 002 — UI Bitis Kapisi Denetcisi2. Motorun urettigi plan hangi alanlari tasiyor; tahmini sure hesaplaniyor mu?
 Danisma 002 — UI Bitis Kapisi Denetcisi3. Bos / yukleniyor / hata / iptal durumlari bugun nasil gorunuyor?
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi Denetcisi## Ortak uyari
 Danisma 002 — UI Bitis Kapisi Denetcisi
 Danisma 002 — UI Bitis Kapisi DenetcisiTasarimci ve denetci **bagimsiz olarak** ayni seyi soyledi: yerlesim pimi testleri
 Danisma 002 — UI Bitis Kapisi Denetcisikirmizya donecek, yeniden temellendirme isin bir parcasi olarak planlanmali.
