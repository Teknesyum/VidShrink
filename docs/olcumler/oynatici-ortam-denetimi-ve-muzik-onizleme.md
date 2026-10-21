# Oynatıcı Ortam Denetimi Ve Müzik Önizlemesi

20 Ekim 2026 · later 99 ve 103 · libmpv v0.41.0-1023-g69e63f425 (`tools/libmpv/libmpv-2.dll`) · Windows 11 22631

Sonda: `.calisma/` altında net8.0-windows10.0.19041 konsol programı, `GlobalSystemMediaTransportControlsSessionManager` ile
oturumları okur. Hiçbir ölçümde ses çalınmadı (`ao=null`).

## Windows Ortam Denetimi (SMTC)

| Durum | Sonuç |
|---|---|
| `media-controls=yes`, başlıklı klip, oynuyor | oturum açılıyor; başlık dosyanın `title`'ı, sanatçı boş, durum Playing |
| aynı, `media-controls=no` | oturum yok |
| `media-controls` motor açıldıktan sonra yazılırsa | oturum açılmıyor; seçenek yalnız kurulumda okunuyor |
| `TryTogglePlayPauseAsync` | `pause=yes`, durum Paused; ikinci çağrı `pause=no` |
| Oynat/duraklat düğmesi | açık (`IsPlayPauseToggleEnabled=True`) |
| Sonraki/önceki düğmesi | kapalı (`IsNextEnabled=False`); kuyruk mpv'ye verilmiyor |

Kilitlenme: `media-controls=yes` ile motor kurulup **hemen** kapatılınca `mpv_terminate_destroy` dönmüyor
(yığın: `MpvEngine.Release`). Kurma ile kapatma arasındaki beklemeye göre, süreç başına 20 aç-kapa, ikişer süreç:

| Bekleme | Sonuç |
|---|---|
| 0 ms | 2/2 süreç ilk aç-kapada kilitlendi |
| 5, 20, 50, 100 ms | 40/40 aç-kapa bitti (her biri) |
| `media-controls=no`, 0 ms | 40/40 bitti |

Düzeltme: `MpvEngine.Dispose` seçenek açıkken kurulumdan sonra en az `MediaControlsSettle` (100 ms) bekler. Düzeltmeden
sonra aynı sonda 0 ms beklemeyle 120/120 aç-kapa bitirdi. Test: `OynaticiOrtamDenetimiTests.AcikMotorHemenKapatilincaKilitlenmez`.

## Sonraki ve Önceki Düğmesi (21 Ekim 2026)

mpv 0.41.0-1023 `smtc.cpp`: sonraki düğmesi bölüm kaldıysa `add chapter 1`, yoksa `playlist-next` çalıştırır; önceki
`add chapter -1` ya da `playlist-prev`. Düğme tuş olayına dönmüyor.

- Basamak 1 düştü: `keybind NEXT "script-message vidshrink-next"` rc 0 döndü, sonda `TrySkipNextAsync` çağırdı,
  `MPV_EVENT_CLIENT_MESSAGE` gelmedi; `playlist-pos` kendiliğinden 0'dan 1'e geçti.
- Basamak 2 işledi: `SetNeighbors` listeyi `önceki, çalan, sonraki` kurar (`loadfile append` + `playlist-move`),
  `keep-open=always` mpv'nin kendi ilerlemesini keser. `playlist-pos` değişimi `_neighborHome` CAS'ıyla tek
  `NeighborRequested` atar, arayüz kuyruğu `StepFile` ile ilerletir. Ölçüm: sonraki → `[true]`, önceki → `[false]`.
- Düğmeler mpv'de `playlist-count > 1` iken ikisi birden açık: tek komşuda eksik yöne basış hiçbir şey yapmaz
  (istek yok, konum 0). Tek dosyada ikisi kapalı.
- Sanatçı: mpv yalnız müzik türünde (`metadata/by-key/Artist`) yazıyor. Etiketli `.m4a`'da sondanın okuduğu sanatçı
  etiketle aynı; videoda alan SDK'sız yazılamıyor.

## Kapanışta Kalan Oturum

Kapatılan motorun oturumu `Playing` durumunda kalıyordu; o oturuma oynat/duraklat basmak süreci düşürdü (mpv'nin
düğme işleyicisi serbest bırakılmış tutamağı yakalıyor). Düzeltme: `Dispose` önce `stop` gönderir, `idle-active`'i
en çok 100 ms bekler, sonra 100 ms daha bekler.

| Durum | Sonuç |
| --- | --- |
| Ek bekleme yok | 10 koşumun 4'ünde oturum kaldı |
| Ek 100 ms | 20 koşumdan 19'u temiz (koşum başına 3 kapanış) |
| Ek 200 ms, boşta bekleme 1 sn | 20'de 18 temiz; iyileşme yok, geri alındı |
| Mutasyon: `CloseMediaSession` çağrısı silindi | kalan oturum 1, 2, 3 (birikiyor) |

Kalan oturum bir kez kalınca süreç sonuna kadar duruyor: mpv'nin pencere iş parçacığı küçük resim üretirken
(`GetFileFromPathAsync(..).get()`) bloke kalıyor, boşta güncellemesi ve kapanış iletisi işlenmiyor. Test
(`KapatilanMotorSmtcOturumuBirakmaz`) bu yüzden birikmeyi ölçer: kapanıştan sonra en çok bir oturum.
## Müzik İzinin Önizlemede Çalınması

Denenen yol 1, `lavfi-complex` + `audio-files` + `edl://`:
`[vid1]null[vo];[aid2]volume=-12dB[m];[aid1][m]amix=inputs=2:duration=first:normalize=0[ao]`.
Mekanik olarak çalışıyor (süre 5 sn, 3 iz, `audio-params` 48000 mono floatp, arama sonrası oynuyor), ama müziğin
zamanı EDL zamanına bağlı, çizelge zamanına değil:

- hızı değişen klipte müzik de hızlanıyor;
- müziğin başlangıç anı `adelay` ile verilirse arama gecikmeyi sıfırlıyor;
- ters ya da geri oynatmada müzik kesik kesik çalıyor;
- `aid` seçimiyle (klibin ses izi seçimi) çatışıyor.

Seçilen yol 2: ayrı, yalnız sesli bir mpv motoru (`MusicPreview`) çizelge konumuna kilitlenir. Hız oynatma hızına
eşitlenir, kayma 0,25 sn'yi geçince yeniden aranır, geri/ters oynatmada, müziğin dışında ve sonda susar. Düzey
etiketli `af` halkasıyla (`volume=-12.0dB`) verilir; ses düzeyi ve sessiz önizleme motorundan aynalanır.

Canlı ölçüm (`DuzenleyiciMuzikOnizlemeTests.CanliMotorAoNullIleMuzikSesiniDuzeyleVeKonumlaVerir`, 440 Hz sinüs,
-12 dB, `ao=null`): `audio-params/samplerate` 48000, `af` `volume=-12.0dB` içeriyor, tepe düzey -26,5 ile -33,5 dB
arasında (sinüs kendisi yaklaşık -18 dBFS), çizelgeye kayma 0,35 sn'nin altında, oynatma durunca motor duraklıyor.

Önizlemede yok: kısma (müziğin konuşma altında alçalması) yalnız teslimde.
