# Plan: Müzik Çalar ve Çalma Listesi

İstek: "çalma listelerini açabilmemiz lazım vidshrink ile music player olarakda çalışabilmemiz
lazım media playerden neyimiz eksikse tamamla"

Tespit (ajan raporu, kritik iddialar satırında okundu): ses dosyası açılıyor ve çalıyor ama
arayüz video varsayıyor. Çizim döngüsü kare görmeyince süre çubuğu ilerlemiyor
(`PlayerView.axaml.cs:608`), ~2 sn'de takılma uyarısı çıkıyor (`PollStall`, kare sayısına
bakıyor), çizim saati 1 ms'de kalıyor. Çalma listesi ayrıştırıcısı yok; sonraki/önceki yalnız
klasördeki 24 video uzantısını görüyor (`FolderNavigator.cs:15`). Medya tuşları ve etiket
(başlık/sanatçı/kapak) okuması yok.

## Dalgalar

| # | İş | Dosya | Tahmin |
|---|----|-------|--------|
| 1 | **Ses kipi**: motor `HasVideo` (mpv `vid`/`track-list`) bildirir; görüntüsüz dosyada süre çubuğu konum saatinden ilerler, takılma bekçisi kapanır, çizim saati 16 ms'ye iner, ekran görüntüsü/klip/önizleme gizlenir, orta alanda başlık+sanatçı+kapak (mpv `metadata`, `audio-display=embedded-first`) | MpvEngine, IPlaybackEngine, PlayerView (.axaml, .cs, .Tools, .Menu), locale tr/en, test | ~60k token |
| 2 | **Klasörde ses**: sonraki/önceki ve dosya seçici ses uzantılarını da görür; ses dosyası küçültme sekmesine yüklenmez (ffprobe "video yok" hatası gider) | FolderNavigator, ShellIntegration, MainWindow.axaml.cs, MainWindow.OdakTakibi, test | ~20k |
| 3 | **Çalma listesi**: m3u/m3u8/pls mpv'ye doğrudan; wpl/asx/wax/wvx için Core'da küçük XML ayrıştırıcı → girdiler kuyruğa. Açık liste varken sonraki/önceki ve açılır liste listeyi izler | Core/Playlist (yeni), MpvEngine, PlayerView.Playlist, PlayerView.Window, test | ~50k |
| 4 | **Medya tuşları**: Play/Pause, Next, Previous klavye medya tuşları (pencere odaktayken; odak dışı SMTC ayrı iş) | PlayerInputMap, PlayerView, test | ~10k |

Toplam: ~140k token, ~15 dosya, 4 dalga. Her dalga ayrı commit, dokunulan testler yerelde yeşil.

## Kapsam Dışı

Kütüphane (albüm/sanatçı dizini), görselleştirici, CD kopyalama/yazma, odak dışı SMTC
entegrasyonu. WMP'nin bunları vardı; her biri kendi başına büyük iş.

## Karar

Karar (3. dalga): hiçbir liste mpv'ye verilmez; hepsini `Core/PlaylistFile` ayrıştırır, kuyruk
uygulamada (`PlayerView.Kuyruk`). mpv'nin kendi listesi `_path`'i ve ses kartını güncellemiyordu,
yani mpv .wpl'yi okusa bile arayüz ilk girdide kalırdı.
