# K2 — Gerçek Sistem Sesi Yakalama (WASAPI Loopback)

Kaynak: `docs/piyasa/tarama-2026-10-05.md`, K2 satırı. Kapsam yalnız o satır: "Stereo Mix"
olmayan Windows makinesinde de hoparlörden çıkan ses kayda girsin.

## Bugünkü Durum

Kaydedici sesi ffmpeg'in `dshow` girdisinden alıyor. Sistem sesi ancak makinede "Stereo Mix"
ya da sanal kablo gibi bir geri döngü cihazı listeleniyorsa seçilebiliyor. Bu makinede öyle
bir cihaz yok; `docs/olcumler/kaydedici-piksel.md` dshow listesinde yalnız mikrofon gördü.
WASAPI loopback (Windows'un "çıkış cihazında çalanı geri oku" arayüzü) için depoda daha önce
alınmış bir ölçüm yok.

## Seçilen Yol

**Yakalama:** bağımlılıksız `[ComImport]` COM (`IMMDeviceEnumerator`, `IAudioClient`,
`IAudioCaptureClient`). Yeni NuGet paketi **yok**. NAudio (MIT) düşünüldü; `naudio.wasapi`
tek başına 1,6 MB ve kullanacağımız yüzey dört arayüz, o yüzden alınmadı. Uygulama zaten
`DdaOutputs.cs`'de aynı kalıbı kullanıyor.

**Biçim sabit:** 48 kHz, 2 kanal, 16 bit. `AUTOCONVERTPCM` bayrağıyla dönüşümü Windows
yapıyor; böylece ffmpeg argümanı makineden bağımsız: `-f s16le -ar 48000 -ac 2 -i <boru>`.

**Taşıma: adlandırılmış boru, stdin değil.** Kaydedici ffmpeg'i `q` ile nazikçe durdurmak
için stdin'i kullanıyor; PCM oraya yazılamaz. Core argümana sabit bir jeton yazar
(`\\.\pipe\vidshrink-loopback`), süreç başlatan taraf jetonu benzersiz bir boru adıyla
değiştirir, boruyu ffmpeg'den **önce** açar ve bir pompa görevi yazar.

**Zaman çizgisi (saf sınıf, `LoopbackTimeline`):** sistem sessizken loopback hiç veri
vermez. Pompa her turda "duvar saatine göre kaç örnek yazılmış olmalıydı" diye sorar; eksik
sessizlikle doldurulur, fazla (cihaz saati ileri kaçarsa) atılır. Toleranslar: veri gelirken
30 ms, veri yokken 100 ms, ileride 100 ms.

**Başlangıç hizası:** ffmpeg her girdiyi sıfırdan başlatır, ama görüntü ilk karesini boru
bağlanmadan önce yakalar. Kayıt oturumu ffmpeg'in stderr'indeki `Duration: N/A, start: <Unix
saniyesi>` satırını beslemeye verir; besleme o an ile borunun bağlandığı an arasındaki süreyi
(en çok 3 sn) ses izinin başına sessizlik olarak ekler. Damga gelmezse (ddagrab sıfırdan sayar)
ekleme yapılmaz.

**Cihaz değişimi:** yakalama kendi iş parçacığında koşar, saniyede bir varsayılan çıkış
cihazını yoklar. Cihaz değişirse yenisine geçer (`Switched`), hiç cihaz yoksa sessizlik
yazılır (`Unavailable`) ve saniyede bir yeniden denenir. Hiçbir hal kaydı düşürmez; durum
kayıt şeridinin bildirim satırında söylenir.

**Kilitlenme:** pompa her çıkışta boruyu kapatır; iptal `WaitForConnection` ve `Write`'ı
keser. ffmpeg'in stdout/stderr boşaltması bugünkü gibi kalır.

## Dokunulan Yerler

- `src/VidShrink.Core/LoopbackAudio.cs` (yeni): jeton, biçim, cihaz kaydı, `LoopbackTimeline`.
- `src/VidShrink.Core/AudioCaptureArguments.cs`: `CaptureBackend.WasapiLoopback` ve girdi kolu.
- `src/VidShrink.Core/RecorderArguments.cs`: Windows dışında loopback isteği doğrulamada reddedilir.
- `src/VidShrink.Ffmpeg/SystemAudioCapture.cs` (yeni): `ISystemAudioCapture`, durum, kapı.
- `src/VidShrink.Ffmpeg/WasapiLoopbackCapture.cs` (yeni): COM yakalama.
- `src/VidShrink.Ffmpeg/LoopbackFeed.cs` (yeni): boru ve pompa.
- `src/VidShrink.Ffmpeg/CaptureDevices.cs`: Windows listesinin sonuna loopback seçeneği.
- `src/VidShrink.Ffmpeg/RecorderSession.cs`, `ReplayRecorder.cs`, `AudioLevelSource.cs`: boruyu bağlar, kapatır.
- `src/VidShrink.App/Recorder/RecorderView.Ses.cs`, `.Serit.cs`: yerelleştirilmiş etiket, durum satırı.
- `src/VidShrink.App/Locales/*/recorder.json`: üç yeni metin, 42 dil.
- `tests/VidShrink.Tests/SistemSesiTests.cs` (yeni), `BiciminTests.cs` sayım pimi.

## Kararlar

- Yalnız Windows. macOS ve Linux listesine seçenek eklenmez, bugünkü davranış aynen kalır.
- Varsayılan değişmez: seçenek "Sistem sesi" kutusunun **son** öğesi. "Stereo Mix" olan
  makinede sihirbaz yine onu seçer.
- Mikrofonla birlikte seçilince bugünkü `amix` yolu kullanılır; yeni karıştırma kodu yok.
- Ayarda cihazın sabit adı (`WASAPI loopback`) saklanır, ekranda yerelleştirilmiş etiket
  görünür; dil değişince seçim kaybolmaz.

## Ölçü

- `LoopbackTimeline`: sahte saatle sessizlik doldurma, tolerans, ileri kaçma.
- `LoopbackFeed`: sahte kaynakla gerçek ffmpeg'e boru (yalnız Windows); kaynak hiç veri
  vermezken de dosya duvar saati kadar ses taşır. ffmpeg hiç bağlanmazsa kapatma takılmaz.
- Argüman, doğrulama, liste, arayüz etiketi ve kalıcılık.
- Testler gerçek ses cihazına dokunmaz; gerçek WASAPI yolu yalnız elle, en çok 5 saniyelik
  tek koşumla denenir.

## Bilinen Açıklar

- Video girdisinin açılışı ile borunun bağlanışı arasındaki başlangıç kayması ölçüldü ve
  kapatıldı: ffmpeg'in stderr'indeki ilk kare damgası okunur, aradaki süre kadar sessizlik ses
  izinin başına yazılır (`LoopbackAudio.CaptureStart`/`Lead`). Sayılar ve kalan açıklar
  `docs/olcumler/sistem-sesi-loopback.md`'de.
- `AUTOCONVERTPCM` desteklemeyen bir Windows'ta açılış düşer; kaynak "yok" sayılır, kayıt
  sessiz sürer ve durum satırı bunu söyler.
