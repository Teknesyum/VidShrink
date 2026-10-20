# Anahtar Kare Önizlemesi

Later 89 (DZB-25 Klip Ayarlarına Anahtar Kare, DZB-26 Yumuşatma Eğrileri), 2026-10-18, Windows 11 22631,
ffmpeg 9 CLI ve `tools/libmpv/libmpv-2.dll`. Ölçü `tests/VidShrink.Tests/AnahtarKareTests.cs`.

## Soru

Önizleme zinciri bugüne dek sabit sayı taşıyordu. Zamana bağlı ffmpeg ifadesini (içinde `t` geçen
`volume`, `scale`, `crop`) mpv'nin `vf`/`af` lavfi süzgecinde taşıyıp taşımadığı ilk iş ölçüldü.

## Yöntem

- Kaynak: 3 sn, 160x120, `-threads 2` ile üretilmiş klip; sol yarı siyah, sağ yarı beyaz, 440 Hz ton.
- Çizelge 1 sn'de bölündü. İkinci parçaya soldan sağa kayan yarım genişlik kırpma ve
  0 → −40 dB ses karesi (0,5 sn) verildi.
- Önizleme `EdlPreviewDriver` ile gerçek libmpv'de açıldı. Kare motorun kare kopyasından
  (`TryCopyLatest`, orta piksel) okundu, ses motorun tepe ölçerinden (`MeterAudio`, `AudioPeakDb`).
- Teslim tarafı ffmpeg CLI'da `showinfo` ve kare parlaklığıyla ölçüldü.

## Sonuç

- **Görüntü:** libmpv `vf` lavfi grafında `scale=...:eval=frame` ve `t`'li `crop` çalışıyor. `vf`
  özelliğinde graf yazıldığı gibi görülüyor. EDL 0,5 sn'de parlak (> 190), 1,05 sn'de koyu (< 60,
  tuval 160x120), 2,9 sn'de parlak (> 190): pencere çizelge zamanıyla kayıyor.
- **Ses:** `af` tarafında `volume=...:eval=frame` ve `enable` çalışıyor. İlk okumalar −21..−15 dB
  (beklenen −18), kararlı düzlük −57,8..−58,2 dB (beklenen −18 − 40 = −58).
- **Ölçer kayması:** mpv ölçeri oynatma konumundan yaklaşık 0,4 sn önde okuyor. Bu yüzden ses ölçüsü
  zamana değil okuma sırasına bağlandı (ilk üç tepenin en büyüğü ile son beş okumanın ortalaması;
  fark −43..−37 dB aralığında beklenir).
- **ffmpeg CLI:** `scale` içinde `eval=frame` ile `t` çalışıyor; `showinfo` boyutu 160x120 → 208x156
  → 306x230 olarak gösterdi, `crop` çıktısı sabit kaldı.

Hüküm: önizleme zamana bağlı ifadeyi taşıyor; ayrı bir önizleme yolu gerekmedi.

## Zaman Tabanı

- Teslimde `t` klip yerelidir (trim, `setpts=PTS-STARTPTS`, ters çevirme ve hızdan sonra).
- Önizlemede `t` EDL zamanıdır. Parça zamanı ileri klipte `(t-EdlBaşı)/hız`, ters klipte
  `(EdlSonu-t)/hız` olarak yazılır (`ClipFilters.PartTime`).
- Hareketli kırpmada çıktı boyutu ilk karenin dikdörtgenidir; ölçek büyür, kırpma kayar.

## Açık Kalanlar

- 20 Ekim 2026: ters klipte kareler aynalanır (`EditClip.WithMotion`, `KeyframeTrack.Mirrored`), anahtar aynı
  kaynak karesinde kalır; dönüşüm (ölçek, konum, açı) anahtar karesi geldi (`ClipMotion`). İkisi ffmpeg ile
  sentetik klipte ağırlık merkezi ölçülerek doğrulandı (`DonusumKaresiTests`, ±1,5 px).
- Bölmede eğri iki yana kesildiğinde kesim noktasına o anki değerden kare eklenir; kesilen parçanın
  içi doğrusal olmayan eğride yaklaşıktır.
