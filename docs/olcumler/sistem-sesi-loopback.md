# Sistem Sesi Yakalama (WASAPI Loopback)

Tarih 2026-10-10. Makine: Windows 11 Pro 22631, ffmpeg 9.0. Kalem K2
(`docs/piyasa/tarama-2026-10-05.md`), plan `docs/plan-k2-sistem-sesi.md`.

WASAPI loopback, Windows'un "çıkış cihazında çalan sesi geri oku" arayüzüdür. Uygulama sesi
kendisi yakalar ve ham örnekleri (48 kHz, 2 kanal, 16 bit) adlandırılmış borudan ffmpeg'e verir.

## Gerçek Cihazla Tek Koşum

Kural gereği bir kez, 3 saniye. Küçük bir sonda programı `SystemAudioCapture.Open()` ile
varsayılan çıkış cihazını açıp 10 ms aralıkla okudu; boru ve ffmpeg bu koşumda yoktu. Sonda
programı iş bitince silindi.

```
acilis: 16 ms, kaynak: var, hal: Capturing
sure: 3001 ms, tur: 194, veri gelen tur: 180, bayt: 570116, kare: 142529, beklenen kare: 144048, tepe: 95, son hal: Capturing
```

- Açılış 16 ms sürdü, kaynak `Capturing` halinde kaldı.
- 3001 ms'de 142529 kare geldi; 48 kHz'de beklenen 144048. Fark 1519 kare, yani 32 ms.
  Bu fark açılış gecikmesi mi yoksa cihaz saatinin sapması mı, tek koşumdan ayrılamaz.
- Tepe değeri 95 (32767 üzerinden): o an sistemde neredeyse sessizlik vardı. Duyulur bir sesin
  doğru yakalandığı bu koşumla **gösterilmedi**.

## Sahte Kaynakla Ölçülenler

`tests/VidShrink.Tests/SistemSesiTests.cs`, sahte kaynak (`SahteSes`: sesliyken 500 Hz kare
dalga, sessizken hiç veri vermez). Ham satırlar testin çıktısından aynen.

```
sesli=False: 99868 bayt, 520 ms, sifir olmayan 0
sesli=True: 96384 bayt, 502 ms, sifir olmayan 96384
kaynaksiz: 59852 bayt, 311 ms
damgali: 192000 bayt, 0 ms, sifir olmayan 0
en yuksek seviye: -10.8 dB
```

- Kaynak hiç veri vermezken de boru duvar saati hızında dolar: yarım saniyelik örnek (96000
  bayt) 520 ms'de geldi, hepsi sıfır.
- Kaynak açılamazsa boru yine beslenir (çeyrek saniyelik örnek 311 ms).
- İlk kare damgası bağlantıdan 1 sn önceyi gösterince bir saniyelik sessizlik beklemeden yazılır
  (192000 bayt, 0 ms), kaynak sesli olsa da.
- Gerçek ffmpeg sessiz kaynaktan `-t 1` ile bir saniyelik wav yazar (192000 bayt + başlık).

## Başlangıç Kayması

Uçtan uca kol: kaydedici oturumu, gdigrab 640x480 15 fps, sahte sesli kaynak, 4 sn bekleme,
`q` ile durdurma. "İlk ses" dosyanın ses izinde ilk duyulur örneğin anıdır.

| Ölçü | Hizasız | Hizalı |
| --- | --- | --- |
| Görüntü akışı süresi | 4,466 sn | 4,466 sn |
| Ses akışı süresi | 3,840 sn | 4,096 sn |
| Boru bağlantısı (oturum başından) | +371 ms | +370 ms |
| Dosyada ilk ses | ölçülmedi (bu sütun için ölçü yoktu) | 0,271 sn |

Hizasız sütun iki koşumda aynı çıktı; hizalı sütun da iki koşumda aynı çıktı.

Okuma: ffmpeg iki girdiyi de sıfırdan başlatır. Görüntü ilk karesini oturum başından yaklaşık
+10 ile +80 ms arasında yakalıyor, boru +370 ms'de bağlanıyor. Hiza olmadan bağlantı anındaki
ses dosyanın 0. saniyesine düşer, yani ses görüntünün yaklaşık 0,3 sn **önüne** geçer. Hizayla
ilk ses dosyanın 0,271 saniyesine oturuyor; beklenen yer yaklaşık 0,29–0,36 sn. Kalan fark
(20–90 ms) ilk karenin gerçek anını bu ölçüyle daha ince ayıramadığımız için belirsiz.

## Kapanmayan Açıklar

- **Kuyruk kaybı.** Hizalı koşumda ses akışı görüntüden 0,37 sn kısa bitiyor (4,096 / 4,466).
  `q` gelince ffmpeg okumayı bırakıyor; o an boruda ve ffmpeg'in okuma tamponunda duran ses
  dosyaya girmiyor. Kaydın son yaklaşık üçte bir saniyesi sessiz kalıyor. Neden ayrıştırılmadı,
  düzeltilmedi.
- **Gerçek cihaz → boru → ffmpeg** uçtan uca denenmedi; gerçek cihazın tek koşumu yalnız
  yakalamayı ölçtü.
- **Cihaz değişimi** (kulaklık takıp çıkarma) gerçek donanımda denenmedi; yalnız durum satırı
  sahte halle ölçüldü.
- **Uzun kayıtta sapma** gerçek donanımda ölçülmedi. Saf sınıf, saati %1 hızlı ve %1 yavaş
  cihazda bir dakikalık benzetimde sapmayı 100 ms + 10 ms içinde tutuyor.
- **ddagrab yolu** damga vermez (lavfi girdisi sıfırdan sayar); orada başa sessizlik eklenmez,
  kayma ölçülmedi. Yeniden oynatma tamponu ve seviye çubuğu da damga kullanmaz.
- `AUTOCONVERTPCM` desteklemeyen Windows sürümü denenmedi.
- Mikrofonla birlikte (`amix`) canlı kayıt denenmedi; yalnız argümanın bugünkü yolla aynı
  olduğu ölçüldü.
