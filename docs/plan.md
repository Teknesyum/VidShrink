# Yol haritasi — 0.3.1 arayuz turu

Kaynak: kullanicinin son 30 girdisi (`.calisma/son-girdiler.md`), 7 Eylul 2026 mesaji,
ve iki danisman raporu:
[007 UI tasarimci](danisma/008-ui-tasarimci.md) · [008 UI denetcisi](danisma/009-ui-denetci.md).

Denetcinin karari **HOLD**. Iki danisman bagimsiz olarak ayni seyi soyledi: arayuz
sikistirmanin ne yapacagini dugmeye basmadan once soylemiyor, ve yerlesim pimi
testleri bu isle birlikte yeniden temellendirilmeli.

## Sira ve durum

| # | Is | Sozlesme | Durum |
|---|---|---|---|
| F1 | Yerlesim testi ikili arama (CI 456 sn → ~51 sn) | T183 | kosuyor |
| A1 | Onizleme sesi — `AttachAudioSink` baglanmis degil | T184 | acilacak |
| A2 | Tekerlek zoom %100→200 olu, zoom'da kararma | T184 | acilacak |
| A3 | Rozet: sol ORIJINAL / sag ISLENMIS · CRF x, ustte | T184 | acilacak |
| A4 | Duraklat/devam basa sariyor | T184 | acilacak |
| B | Oynaticinin kendi sekmesi — en solda, video acilabilir, kisayollar | T185 | acilacak |
| D | Ayar arayuzu yeniden tasarimi (danismanlarin asil isi) | T186 | acilacak |
| E | Tasma teklifi — %3, dort secenek, sayfa ici serit | T187 | acilacak |
| C | Varsayilan program onerisi + sag menu kisayolu | T188 | acilacak |
| F2 | Yerlesim pimlerinin yeniden temellendirilmesi | D ile ayni dalda | acilacak |
| F3 | README ekran goruntuleri (T177 sonrasi) | — | acilacak |

## A — Onizleme (T184)

Kullanici bunu uc ayri turda soyledi; en gorunur eksik bu.

**A1 ses.** Kok neden bulundu ve teknik bir engel degil: ses yolu
`VidShrink.Ffmpeg` icinde bastan sona yazilmis — `AudioSink.cs` NAudio ile
48 kHz/16 bit/stereo bir cikis kuruyor, `DecoderPipe.SeekAudio` ffmpeg'den ayri bir
PCM borusu aciyor. Yani kullanicinin tarif ettigi "ses dosyasini eszamanli calsan bile
cozulur" yaklasimi zaten kodlanmis. Eksik olan tek sey: `VidShrink.App` hicbir yerde
`AttachAudioSink` cagirmiyor (`grep` bos donuyor), bu yuzden `_sink` her zaman null ve
`SeekAudio` ilk satirda geri donuyor. Bu bir **baglanti bosluğu**, olcum isi degil.

**A2 zoom.** Tekerlek olayi %100 ile %200 arasinda bir sey yapmiyor; ayrica zoom
girip cikarken kare kararip geliyor. Ikisi ayni sozlesmede.

**A3 rozet.** `playback.approximate-preview` ("Yaklasik onizleme") kalkiyor. Yerine
orta panelin **solunda ustte** `ORIJINAL`, **saginda ustte** `ISLENMIS · CRF <x>`.
Rozetler perde hareket ederken sabit durur, ortulen tarafin etiketi soner.
Denetci bunu bagimsiz olarak dogruladi: bugunku rozet "bir ozur cumlesi".

**A4 basa sarma.** 4 Eylul'den kalma borc: ayar degismeden durdur/baslat yapilinca
onizleme en bastan isleme giriyor.

## B — Oynatici sekmesi (T185)

Bugun **hic yok**. `MainWindow.axaml` icinde bes sekme var
(`main.tab.shrink`, `.convert`, `.settings`, `.about`, `.advanced`) ve
`grep PlayerView` bos donuyor. Yani "en sola tasi" degil, "sifirdan ac".

Kullanicinin sabitledigi davranis: acilista **Kucult** secili gelir; ama mp4 dosyasi
"birlikte ac" ile acildiginda **Oynatici** secili gelir. Kisayollarin hepsi calisir.

Tasarimcinin uyarisi: sekme seridinde en solda durup varsayilan olmayan bir sekme
celiskili okunuyor. Cozum sekme sirasini degistirmek degil, Oynatici'yi bir ayirici ve
ikonla **mod** gibi okutmak (tasarimcinin A plani). Kullanici "en solda olsun" dedigi
icin B plani (serit disi ayri mod dugmesi) uygulanmiyor.

## D — Ayar arayuzu (T186)

Tasarimcinin dort bolgesi: A baglam (birakma alani + tek satira saran bilgi seridi,
bugunku dokuz satir yerine), B **Hedef** (birincil: sayi kutusu + Doldurma modu ona
yapisik bir operator + cip seridi), C **Kalite ve uyumluluk** (kodek, HDR yalniz HDR
kaynakta, izinler, GPU), D **Gelismis** kapali.

Bilesen kurali: adlandirilmis durum secimi → **segment kontrolu** (Kodek 3, HDR 2,
Doldurma 2); gercek ac/kapa → **anahtar** (Izinler, GPU). Iki ve uc secenek icin ayni
bilesen. Etiket ustte, tam genislikte segment seridi altta — cunku Turkce etiketler
sariyor. En fazla uc segment.

**Amac kontrol olmaktan cikiyor**, cip seridinin basinda `( Arsiv ) ( Paylasim )`
hazir ayarina donuyor. Motorda karsiligi duruyor ve olculebilir:
`CompressionStrategy.cs:91` Arsiv icin -6.0, Paylasim icin -3.0 CRF ofseti suruyor.
Yani secim gercekten plani degistiriyor; bugun eksik olan bu etkinin ekranda hic
gorunmemesi.

Denetcinin tek onerisi ayni yeri gosteriyor: Kucult'un ilk okumasi **plan + onizleme**
olsun, ayarlar etrafina dizilsin, ayar degisince ikisi de canli guncellensin.

Izinler iki bagimsiz onay kutusu degil, bir **feda sirasi** olarak sunulur.
Bilgi satirlari **once → sonra** bicimine gecer.

## E — Tasma teklifi (T187)

Esik **%3**. Asla sessiz yapilmaz. Dort secenek: tekrar render, iptal, buyuk
surumu kabul, ve sondan / bastan / her ikisinden ~%3 kesintiyi kabul.

Tasarimcinin bicimi: kalici pencere degil, sonuc kartinin icinde **sayfa ici serit**.
Bir birincil oneri + iki guvenli secenek + kapali bir yikici grup. Ayirt edici bes
katman: konum, belirtec, somut sonuc metni ("4:12 → 4:05, sondan ~7 sn"), klavye
varsayilani **asla** yikici secenek olmaz, ve yerinde iki adimli onay. Kesim once
zaman cizgisinde onizlenir.

## C — Kabuk entegrasyonu (T188)

Ikisi de bugun yok denecek durumda: dosya iliskilendirme kodu hic yok; sag menu icin
`VidShrink.ShellExtension` C++ seyrek paketi var ama calismiyor.

## F2 — Pim yeniden temellendirmesi

Iki danisman da bagimsiz uyardi: D isi yerlesim pimi testlerini kirmizya dondurur.
Bu bir surpriz degil, isin parcasi. Pimler D ile **ayni dalda** yeniden temellendirilir;
ayri sozlesmeye birakilirsa main iki kosum kirmizi kalir — bu depoda daha once oldu.

## Raftakiler

- **Max sikistirma modu** — 6 Eylul 2026'da kullanici erteledi ve hatirlatilmasini
  istedi. Bu tur bittiginde gundeme gelir.
- **Test paralelligi (B secenegi)** — kullanici 0.3.0 sonrasina erteledi. Onunde
  dokuz olculmemis yerel `Call from invalid thread` hatasi duruyor.
