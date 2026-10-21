# Oynatıcı — Linux MPRIS Ve Yeni Pencerede Aç (Later 137)

Sahibin kararı: Linux MPRIS evet, yeni pencere evet, ağdan uzaktan denetim hayır, eklenti hayır.
MPRIS ve yeni pencere bugünkü denetim kanalının (`ControlChannel.cs`, 8 komut) üstüne kurulur.

| Parça | Dosyalar | İş |
| --- | --- | --- |
| Durum | `Core/ControlChannel.cs`, `App/MainWindow.Denetim.cs`, `App/Playback/PlayerView.axaml.cs` | `ControlStatus`'a süre ve başlık (`durationSeconds`, `title`), boşken `null` (`lastRecordingPath` gibi) |
| MPRIS saf | yeni `Core/Mpris.cs` | ad, yol, `PlaybackStatus`, µs çevrimi, `file://` çözümü, MPRIS yöntemi → `ControlCommand`, değişen özellik farkı |
| MPRIS veriyolu | yeni `App/Integration/MprisServer.cs`, `App/App.axaml.cs` | Tmds.DBus.Protocol (Avalonia.FreeDesktop'un zaten getirdiği paket, yeni paket yok) ile `/org/mpris/MediaPlayer2`; komutlar `ControlBridge`'den geçer; Linux'ta açık, `VIDSHRINK_MPRIS=0` kapatır |
| Yeni pencere saf | yeni `Core/NewWindow.cs` | `--yeni-pencere`/`--new-window`, ayar anlık kopyası (`pencereler/<pid>/`), ölü kopyaların süpürülmesi, başlatma argümanları |
| Yeni pencere süreç | `App/Program.cs`, `App/App.axaml.cs`, `App/MainWindow.axaml.cs`, `App/MainWindow.KapanistaGuncelleme.cs` | ikinci süreç tek örnek borusuna, kuyruk defterine, güncellemeye, kayıt defterine dokunmaz; ayarı kendi kopyasına yazar |
| Arayüz | `App/Playback/PlayerView.Menu.cs`, `Locales/*/playback.json` | sağ tıkta "Yeni Pencerede Aç", 42 dil |
| Test | yeni `MprisTests.cs`, `YeniPencereTests.cs`, `DenetimKanaliTests.cs`, `.github/workflows/ci.yml` | saf ölçüler her yerde; canlı D-Bus ve libmpv kanıtı CI'nın Linux işinde (`dbus-run-session`) |
| Belge | `docs/control-channel.md`, `docs/later.md`, `CHANGELOG.md`, `tests/VidShrink.Tests/test-katalogu.md` | 137 kapanır: ağ ve eklenti "karar: hayır" |
