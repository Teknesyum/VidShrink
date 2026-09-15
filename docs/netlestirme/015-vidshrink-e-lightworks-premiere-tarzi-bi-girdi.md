[[netlestirme:015]]

# Netleştirme: VidShrink'e Lightworks/Premiere tarzi bir video duzenleme sekmesi ekliyoruz: kes

İşe başlamadan önce soruyu keskinleştir. Görüş verme, plan yazma, kod yazma.
Yalnız şunu döndür: soruda belirsiz kalan yerler, her biri için tek satırlık bir netleştirme sorusu, en fazla beş. Belirsizlik yoksa "net" yaz.

## Soru

VidShrink'e Lightworks/Premiere tarzi bir video duzenleme sekmesi ekliyoruz: kesme, birlestirme, parca kaldirma, parca basina hiz degisimi ve gerekirse -100 hizda geri oynatma. Ayrica oynatici, kucultme, kaydedici ve duzenleyici sekmeleri ayni videoya odakli olacak. Bugunku yapida bu isi en verimli hangi mimariyle kurariz, hangi basamak once gelmeli ve nerede durmaliyiz?

## Elde olan olgular

# Olgular — VidShrink'e video düzenleme sekmesi

Hepsi bu turda toplandı; ölçümler bu makinede alındı, kaynaklar üç raporda.

## Kullanıcının cümlesi (birebir)

"50+repo tara lightworks premiere pro vb tarzında bir video edit panelimizin olmasını
istiyorum adam recorderle kayıt yaptı videosunu istediği kısmını kesip birleştirip istediği
kısmın hızını değiştirebilmeli istediği kısmı kaldırabilmeli gerekirse -100 hızda
oynatabilmeli vb vb ayrı bir sekmede olacak / oynatıcı shrink editor vb hepsi aynı videoya
odaklanmış olacak recorder yapıldıysa odak record edilen videoda"

## Uygulamanın bugünkü durumu (kaynak okundu)

- .NET 8 + Avalonia 12.1.2. Yedi sekme: Oynatıcı, Küçültme, Dönüştürme, Hakkında,
  Kaydedici, Gelişmiş (gizli), Ayarlar. `MainWindow.axaml:65-1273`.
- `MainWindow.axaml.cs` 4601 satır, `MainWindow.axaml` 1349 satır. Sekmeler tek pencerede.
- **Ortak "geçerli video" nesnesi yok.** Küçültme sekmesi `_sourceName` + `_info` tutuyor
  (`MainWindow.axaml.cs:2718`), oynatıcı kendi yolunu tutuyor, kaydedici kendi sonucunu.
  Sekmeler arası aktarım elle: `OpenInShrinkAsync(path)` (`:2760`) ve
  `OpenInPlayerAsync(path)` (`:2767`) — ikisi de sekmeyi değiştirip dosyayı yeniden yüklüyor.
- Oynatma motoru libmpv: `src/VidShrink.Player/MpvEngine.cs`, `vo=libmpv` + yazılım render,
  BGRA kare kopyası. `IPlaybackEngine.OpenAsync(string path)` **tek dosya** alıyor.
  `MpvEngine.cs:227` `loadfile` çağırıyor, `SetProperty(string,string)` (`:211`) var.
- Çıkış ffmpeg.exe'ye **ayrı süreç** çağrısı (`src/VidShrink.Ffmpeg`); argümanları
  `src/VidShrink.Core` üretiyor.
- Açılış hızı ayrı bir iş: çift tıktan ilk kareye bugün sıcak 1857,8 ms; bunu düşürmek için
  `docs/plan.md`'de hipersürüş tasarısı bekliyor (henüz başlanmadı).
- Depo AGPL-3.0. Tek test projesi; kapı 2404 test, 35 dk.

## Ölçülmüş olgular (bu makinede)

| Ölçü | Sonuç |
| --- | --- |
| Kayıpsız kesme (`-c copy`), 104→126 sn | 65 ms ama **+4,030 sn kesim hatası** |
| Smart cut (kenar GOP yeniden kodla + kalanı kopyala + concat) | 1111 ms, **+0,052 sn hata** |
| Tam yeniden kodlama | 2550 ms |
| `reverse` filtresi belleği, 1080p30 | **~99 MB/sn** (40 sn = 3967 MB) |
| Parçalı ters çevirme tepe belleği | 1325 MB |
| `-100x` geri: `skip_frame nokey` | 389 ms |
| `-100x` geri: `select mod 100` | 2260 ms |
| Küçük resim şeridi, 5 dk 1080p, `-skip_frame nokey` | **369 ms**, 36 kare |
| Aynı şerit, `fps=1/10` | 2122 ms |
| Dalga biçimi, saniyede 200 nokta, 5 dk | 309 ms, 600 KB |

Yaygın bilginin yanlış çıktığı yerler (ölçüldü): `atempo` sınırı [0,5–2,0] değil
**[0,5–100,0]**; `-ss` girdiden önce + `-to` girdiden sonra göreli çalışıyor (30 sn istenen
kesit 130 sn çıktı, güvenli biçim `-t`); `concat` demuxer uyuşmayan çözünürlüğü **sessizce**
geçip yanlış kap etiketi yazdı, `concat` filtresi aynı girdide hata verdi.

## Önizleme yolu (mpv belgesinden, koşturulmadı)

- **`edl://` protokolü** birden çok dosyadan kesitleri **tek sanal zaman çizelgesi** olarak
  açıyor: "EDL files basically concatenate ranges of video/audio from multiple source files
  into a single continuous virtual file"; playlist'ten farkı "appears as a virtual timeline
  (like a single file)". `demux/demux_edl.c` her satırı `timeline_part`
  (`start`,`end`,`source_start`,`source`) yapıyor — kesme noktasında `loadfile` yok.
  `loadfile "edl://..."` geçerli; VidShrink'te ek altyapı gerekmiyor.
- **EDL hız ve ters uygulayamaz.** `timeline_part` yapısında parça başına hız alanı yok.
  Hızlı parçanın önizlemesi ancak çizelge konumu izlenip `speed` özelliği elle sürülerek,
  ters parçanınki ancak o kesit önceden `reverse` ile üretilip EDL'e geçici dosya olarak
  konarak yapılabilir.
- **Gerçek zamanlı geri oynatma kapalı.** `--speed` aralığı 0,01–100, negatif almıyor. Tek
  yol `--play-direction=backward`; mpv'nin kendi belgesi "extremely fragile", "not exactly a
  1st class feature", donanım kod çözmeyle "will probably exhaust all your GPU memory".
- `--lavfi-complex` tek `[vo]`/`[ao]` ile sınırlı; karşılaştırma paneline uygun, çizelge
  önizlemesine değil.
- EDL biçimi "not frozen yet and may change any time"; Windows yolları ve Türkçe karakterler
  için `%<bayt>%<değer>` kaçışı zorunlu.

## Sahadan desenler (30 proje tarandı, kaynaklar raporda)

- Kesme listesi modeli evrensel: MLT `playlist_entry_s`, libopenshot `std::list<Clip*>`,
  auto-editor `Clip{src,start,dur,offset}`, Cinelerra `Edit::get_source()`.
- Zaman: Sprocket (MIT, .NET 10 + Avalonia 12, 7 yıldız, alpha) 240.000 tick/sn `long`.
- Undo: yığın kullananlar (Shotcut `QUndoStack`, Pitivi, Flowblade `MAX_UNDOS=35`, Kdenlive
  lambda çiftleri) anlık görüntü kullananlardan kalabalık.
- Ters oynatma: Olive'de klipte ayrı `speed` + `reverse` bayrağı, negatif çarpan yok;
  OpenShot'ta azalan `Keyframe time` eğrisi; ikisi de graf/zaman-eşleme katmanında.
- Smart cut'ı gerçekten yapan iki proje: LosslessCut (üç aşama: kenarı yeniden kodla,
  gövdeyi kopyala, concat demuxer) ve auto-editor (GOP tarayıp paket seviyesinde mux).
- MLT/GES/libopenshot'un .NET bağlaması **yok** (nuget `totalHits: 0`).
- Lisans: AGPL-3.0 olduğumuz için GPL-3/LGPL sorun değil; asıl tuzak **GPL-2.0-only** —
  Avidemux ve LosslessCut'ın satırları alınamaz, yalnız fikri okunur.

## Kısıtlar

- Renk yalnız palet dosyasından, ölçü yalnız tema belirteçlerinden; yeni sayı uydurulmaz.
- Arayüz 42 dilde; her yeni metin 42 dile girer.
- `main`e yalnız T0 birleştirir; beş dosyadan büyük iş önce `docs/plan.md`.
- İkinci bir yerli bağımlılık (MLT/GES) libmpv'nin sha256'lı indirme düzeneğini ikiye katlar.
