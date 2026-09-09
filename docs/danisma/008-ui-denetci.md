# Danisma 002 — UI Bitis Kapisi Denetcisi

- Koltuk: `.calisma/koltuk-denetci.md` (agency.js show, 178 satir)
- Ajana verilen olgular: `.calisma/ui-olgular.md` (ayni dosya, tasarimciyla ortak)
- Tarih: 2026-09-07
- Karar: **HOLD**

> **Uyari — bu dosya tam metin degildir.** Ajanin dondugu ham metin
> `tasks/<id>.output` altinda tutuluyordu; o dosya bu oturumda uzerine yazildi ve
> geri getirilemedi. Asagisi benim oturum icindeki kaydimdir: bulgular ve sorular
> eksiksiz, cumleler ajanin kendi cumleleri degil. Tirnak icindeki tek cumle
> dogrudan alintidir.

## Karar

**HOLD.** Arayuz bugunku haliyle bitis kapisindan gecmiyor.

En keskin bulgu, dogrudan alinti:

> "Bu arayuz, sikistirmanin ne yapacagini dugmeye basmadan once hicbir yerde
> soylemiyor."

Motor bir plan hesapliyor, panel kareleri karsilastirabiliyor, ayarlar plani
degistiriyor — ucu de var, ucu de birbirine bagli degil.

## Bulgular

1. **Izinler iki bagimsiz onay kutusu olarak gosteriliyor, oysa bunlar bir feda
   sirasi.** Kullanici "cozunurluk mu kare hizi mi once dussun" sorusuna cevap
   veriyor; arayuz bunu iki ayri evet/hayir gibi sunuyor.

2. **Bilgi satirlari once → sonra olmali.** Bugun tek durum gosteriliyor; degisimi
   gosteren sutun yok.

3. **"Amac" panelinin asil kusuru yer kaplamasi degil, etkisinin hic
   gosterilmemesi.** Secim plani degistiriyor (`CompressionStrategy.cs:91`,
   Archive -6.0 / Sharing -3.0 CRF ofseti) ama ekranda bunun izi yok; panel anlam
   yerine bosluk dolduruyor.

4. **Onizleme paneli urunun en ikna edici ani, ama iki sey onu susturuyor:**
   "Yaklasik onizleme" rozeti bir ozur cumlesi gibi duruyor ve zoom kirik oldugu
   icin panel dekorasyona dusuyor.

5. **Alti sekme kodun yerlesimini yansitiyor, kullanicinin isini degil.** En soldaki
   sekmenin varsayilan olmamasi da bunun isareti: sekme modeli ise oturmuyor.

## Tek onerilen degisiklik

Kucult sekmesinin **ilk okumasi plan + onizleme** olsun; ayarlar bunlarin etrafina
dizilsin ve ayar degisince ikisi de canli guncellensin.

## Gecis (PASS) kosullari — yedi madde

1. Dugmeye basmadan once tahmini cikti boyutu ve CRF ekranda goruluyor.
2. Ayar degisince bu iki sayi canli guncelleniyor.
3. Izinler feda sirasi olarak, tek bir siralanabilir kontrol halinde sunuluyor.
4. Bilgi satirlari once → sonra bicimine geciyor.
5. Amac secimi ya etkisini gosteriyor ya kontrol olmaktan cikiyor.
6. Onizleme rozeti ozur degil bilgi: hangi tarafin ne oldugunu soyluyor.
7. Zoom calisiyor; kirik etkilesim urunun en guclu anini bozmuyor.

## Denetcinin kullaniciya sordugu uc soru

1. Onizleme paneli sayfada bugun tam olarak nerede duruyor?
2. Motorun urettigi plan hangi alanlari tasiyor; tahmini sure hesaplaniyor mu?
3. Bos / yukleniyor / hata / iptal durumlari bugun nasil gorunuyor?

## Ortak uyari

Tasarimci ve denetci **bagimsiz olarak** ayni seyi soyledi: yerlesim pimi testleri
kirmizya donecek, yeniden temellendirme isin bir parcasi olarak planlanmali.
