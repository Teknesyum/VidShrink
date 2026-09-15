# Kaydedici Saha Taraması — 15 Eylül 2026

Kapsam: 29 açık kaynak ekran kaydı / yayın / yakalama projesi. Yıldız, dil ve lisans
GitHub API'den (`gh api repos/<ad>`) 15 Eylül 2026'da okundu. Yetenek satırlarının
kaynağı projenin kendi README'si ya da resmî özellik sayfasıdır; bağlantılar §6'da.

**Yöntemin sınırı:** Burada yazan her şey *belgelenmiş* yetenektir, kaynak koddan
doğrulanmış değil. README'si ince olan projelerde (blue-recorder, deepin, StreamFX,
Streamlabs GitHub deposu) yetenek listesi gerçekte olduğundan dar görünür; böyle
yerlerde "belgelenmemiş" dedim, "yok" demedim.

## 1. Projeler

| Proje | Yıldız | Dil | Lisans | Öne çıkan üç yetenek |
|---|---|---|---|---|
| obsproject/obs-studio | 76.246 | C | GPL-2.0 | Sahne/kaynak karışımı, sınırsız sahne + geçiş, neredeyse her eylem için sıcak tuş |
| ShareX/ShareX | 39.582 | C# | GPL-3.0 | Yakalama yöntemi zenginliği (tam ekran, pencere, monitör, bölge, kaydırmalı), ekran kaydı + GIF kaydı, iş akışı sistemi |
| flameshot-org/flameshot | 30.856 | C++ | GPL-3.0 | Yerinde açıklama araçları (ok, kalem, pikselleştir, bulanıklaştır, sayaç), ekrana iğneleme, CLI/DBus |
| NickeManarin/ScreenToGif | 27.651 | C# | MS-PL | Bölge + webcam + çizim tahtası kaydı, dahili kare düzenleyici, gif/apng/video/psd/png çıktısı |
| CapSoftware/Cap | 22.257 | Rust | özel (NOASSERTION) | Instant Mode (kaydederken yükle, durunca bağlantı), Studio Mode (arka plan, zoom, altyazı), kendi deponu kullanma |
| wulkano/Kap | 19.354 | TypeScript | MIT | Menü çubuğu kaydedicisi, eklenti mimarisi, kayıt sırasında duraklatma |
| alyssaxuu/screenity | 18.692 | JS | GPL-3.0 | Ekran üzerine çizim/ok/metin, yumuşak zoom + spot ışığı, imleç ve tıklama vurgusu |
| asciinema/asciinema | 17.803 | Rust | GPL-3.0 | Terminal oturumu kaydı, canlı yayın, oynatmada boşta kalma kırpma |
| MathewSachin/Captura | 10.832 | C# | MIT | Mikrofon + hoparlör sesini karıştırma, tıklama ve tuş vuruşu gösterimi, yapılandırılabilir sıcak tuşlar |
| phw/peek | 10.552 | Vala | GPL-3.0 | Pencerenin kendisi kayıt çerçevesi, GIF için gifski + kalite kaydırıcısı, WebM (proje arşivlendi) |
| screego/server | 10.523 | Go | GPL-3.0 | Çok kullanıcılı ekran paylaşımı, WebRTC, düşük gecikme / yüksek çözünürlük |
| sindresorhus/Gifski | 8.555 | Swift | MIT | Videodan yüksek kaliteli GIF, kalite/boyut/fps/kırpma, paylaşım uzantısı |
| muaz-khan/RecordRTC | 6.918 | JS | MIT | Tarayıcıda ekran/ses/video/canvas kaydı, duraklat-sürdür, süre dilimi geri çağrımı |
| justinfrankel/licecap | 5.595 | C | — | Ekran üstünde yeniden boyutlanan çerçeve, kayıt sırasında çerçeveyi taşıma, duraklatınca metin karesi ekleme |
| streamlabs/desktop | 4.850 | TS | GPL-3.0 | OBS üzerine tema/kaplama mağazası, çoklu platform yayını, tekrarlardan otomatik klip |
| royshil/obs-backgroundremoval | 4.545 | C++ | GPL-3.0 | Yeşil perde olmadan arka plan ayırma, düşük ışık iyileştirme, GPU/CPU çıkarım |
| obsproject/obs-websocket | 4.353 | C++ | GPL-2.0 | OBS'i dışarıdan kumanda (kaydı başlat/durdur, sahne değiştir), telefon/pedal/Stream Deck istemcileri |
| Vhonowslend/StreamFX-Public | 4.182 | C++ | GPL-2.0 | 3B düzen, bulanıklık/gölge/parıltı, kendi shader'ını yazma |
| SeaDve/Kooha | 3.510 | Rust | GPL-3.0 | Monitör ya da bölge seçimi, mikrofon + masaüstü sesi, deneysel donanım hızlandırmalı kodlama |
| MaartenBaert/ssr | 2.892 | C++ | GPL-3.0 | OpenGL/oyun doğrudan yakalama, kayıt sırasında önizleme + canlı istatistik, sıcak tuşla duraklat/sürdür |
| WarmUpTill/SceneSwitcher | 1.561 | C++ | GPL-2.0 | Koşul-eylem makroları (süreç, ses, tarih, medya, imleç), otomatik sahne geçişi |
| vkohaupt/vokoscreenNG | 1.505 | C++ | GPL-2.0 | Büyüteç + Showclick + Halo (tıklama/imleç vurgusu), geri sayım ve zamanlayıcı, çoklu ses kaynağı |
| ammen99/wf-recorder | 1.309 | C++ | MIT | Çıkış (monitör) seçimi, geometri ile bölge, VAAPI donanım kodlaması |
| mhsabbagh/green-recorder | 626 | Python | GPL-3.0 | Wayland + Xorg, mkv/avi/mp4/wmv/gif/nut, tepsi simgesinden durdurma (arşivlendi) |
| xlmnxp/blue-recorder | 594 | Rust | özel (NOASSERTION) | Windows + Linux + FreeBSD, avi/gif/mkv/mp4/nut/webm/wmv, ayarları hatırlama |
| hzbd/kazam | 321 | Python | GPL-3.0 | PulseAudio'nun gördüğü her giriş aygıtından ses, geri sayım, SUPER-CTRL sıcak tuşları |
| exeldro/obs-replay-source | 176 | C | GPL-2.0 | Bellekte son N saniye, sıcak tuşla anlık tekrar, yavaş çekim / ters oynatma |
| linuxdeepin/deepin-screen-recorder | 116 | C++ | GPL-3.0 | Alanı elle ya da pencereyi otomatik seçme, gif/mp4, tepsi simgesinden durdurma |
| Genymobile/scrcpy | 149.687 | C | Apache-2.0 | Cihaz ekranını dosyaya kaydetme, ses aktarımı (Android 11+), sanal ekran |

scrcpy bir masaüstü kaydedicisi değil, Android aynalama aracıdır; listeye "kayıt ile
kumanda aynı pencerede" arayüz kararı için alındı. flameshot ve Gifski de video
kaydetmez (sırasıyla ekran görüntüsü ve GIF dönüştürme); açıklama araçları ve GIF
kalite denetimi için burada.

## 2. Yetenek matrisi (en güçlü 10 proje)

Sütunlar: OBS = obs-studio, SX = ShareX, S2G = ScreenToGif, Cpt = Captura,
Scr = screenity, CAP = Cap, VKO = vokoscreenNG, KOO = Kooha, SSR = SimpleScreenRecorder,
KAP = Kap. Hücre: **+** belgelenmiş, **−** belgede yok, **k** kısmi / deneysel.

| Yetenek | OBS | SX | S2G | Cpt | Scr | CAP | VKO | KOO | SSR | KAP |
|---|---|---|---|---|---|---|---|---|---|---|
| Bölge seçimi | k | + | + | + | + | + | + | + | + | + |
| Çoklu monitör | + | + | − | + | − | − | + | k | − | − |
| Pencere yakalama | + | + | + | + | + | + | + | k | − | − |
| Oyun yakalama | + | − | − | − | − | − | − | − | + | − |
| Webcam bindirme | + | − | + | + | + | + | + | − | − | − |
| Mikrofon sesi | + | k | − | + | + | + | + | + | + | k |
| Sistem/masaüstü sesi | + | k | − | + | + | + | + | + | + | k |
| Ayrı ses izleri | + | − | − | − | − | − | − | − | − | − |
| Sahne/kaynak karışımı | + | − | − | − | − | − | − | − | − | − |
| Sıcak tuşlar | + | + | k | + | k | k | + | − | + | k |
| Geri sayım / gecikme | − | k | k | − | + | − | + | + | − | − |
| Zamanlayıcı (otomatik dur) | − | − | − | − | + | − | + | − | − | − |
| Geri sarmalı tampon | + | − | − | − | − | − | − | − | − | − |
| Anlık tekrar | + | − | − | − | − | − | − | − | − | − |
| Çizim / açıklama | − | + | + | − | + | k | − | − | − | − |
| İmleç vurgusu | − | − | + | + | + | − | + | k | − | − |
| Tıklama efekti | − | − | − | + | + | − | + | − | − | − |
| Tuş vuruşu gösterimi | − | − | + | + | − | − | − | − | − | − |
| Otomatik duraklatma | − | k | − | − | + | − | − | − | − | − |
| Duraklat / sürdür | + | − | + | k | + | − | − | − | + | + |
| Bölme / kırpma | − | − | + | − | + | + | − | − | − | k |
| GIF çıktısı | − | + | + | + | + | − | + | + | − | k |
| Kalite ön ayarları | + | − | + | − | − | + | − | + | + | − |
| Donanım kodlayıcı seçimi | + | − | − | k | − | k | − | k | k | − |
| Düzenleyici / sonradan işleme | − | + | + | − | + | + | k | − | − | k |
| Uzaktan kumanda / otomasyon | + | + | − | + | − | − | − | − | − | + |

Notlar: OBS'te "bölge" ayrı bir mod değil, kaynağa uygulanan kırpma/dönüştürmedir —
bu yüzden **k**. Kooha'da çoklu monitör, pencere seçimi ve donanım kodlayıcılar
deneysel bayrakla açılıyor. ShareX'in ses yönetimi README'de değil uygulama içi
kayıt ayarlarında; resmî özellik sayfasında geçmediği için **k**.

## 3. "Herkeste var" — 29 projenin en az 15'inde belgelenen yetenekler

1. **Bölge seçimi.** Tam ekranın yanında kare bir alan seçmek neredeyse evrensel:
   ShareX, ScreenToGif, Captura, Kap, peek, SSR, flameshot, Kooha, vokoscreenNG,
   wf-recorder (`-g <geometry>`), screenity, Cap, LICEcap, deepin, kazam.
2. **Mikrofon ve sistem sesinin ayrı ayrı seçilebilmesi.** OBS, Captura ("Mix Audio
   recorded from Microphone and Speaker Output"), Kooha ("microphone, desktop audio,
   or both at the same time"), vokoscreenNG ("Simultaneous recording from multiple
   audio sources is supported"), SSR, screenity, Cap, kazam, wf-recorder,
   green-recorder, blue-recorder, Kap, RecordRTC, Streamlabs, scrcpy.
3. **Birden fazla kapsayıcı/kodlayıcı seçimi.** ffmpeg/libav'a dayanan her proje
   (OBS, SSR "supports many different codecs and file formats", Captura,
   wf-recorder, green-recorder, blue-recorder, vokoscreenNG, Kooha, deepin,
   ScreenToGif, ShareX, Kap, screenity, RecordRTC, Gifski) en az iki çıktı biçimi
   sunuyor.
4. **GIF çıktısı.** ShareX, ScreenToGif, Captura, peek, Kooha, vokoscreenNG, LICEcap,
   Gifski, screenity, green-recorder, blue-recorder, RecordRTC, deepin, Kap (eklenti).
   14 proje — eşiğin bir altında, ama GIF'in artık varsayılan beklenti olduğunu
   gösteriyor.
5. **Sıcak tuşla başlat/durdur.** OBS ("Set hotkeys for nearly every sort of action…
   starting/stopping streams or recordings"), ShareX, Captura, SSR, LICEcap
   (shift+space), vokoscreenNG ("Global keyboard shortcuts"), kazam
   (SUPER-CTRL-R/W/F/Q), peek, Kap, Gifski, scrcpy, SceneSwitcher, screenity,
   deepin (ESC). ~14 proje.
6. **Kayıt penceresinin kendini gizlemesi ya da küçülmesi.** Cümleyle yazan az, ama
   uygulayan çok: peek'te pencerenin *kendisi* çerçeve, LICEcap'te çerçeve kayıt
   sırasında taşınabiliyor, Kap menü çubuğuna, green-recorder ve deepin tepsiye
   çekiliyor, ScreenToGif küçük bir kaydedici penceresi gösteriyor.

**VidShrink için okuma:** 1, 2, 3 ve 5 yoksa ürün "eksik" sayılır; 4 (GIF) eşiği
geçmese bile beklenti.

## 4. Ayırt edici — az projede olup çok işe yarayanlar

- **Geri sarmalı tampon / anlık tekrar.** Yalnız OBS (Replay Buffer) ve
  obs-replay-source'ta ("Keeps the configured seconds from a source in memory",
  sıcak tuşla geri çağırma, yavaş çekim, ters oynatma, kareyle ilerletme). Kaydı
  başlatmayı unutan kullanıcıyı kurtaran tek özellik.
- **Kaydederken yükle, durunca paylaşılabilir bağlantı.** Cap'in Instant Mode'u:
  "Upload while recording and get a shareable link the moment you stop." Kayıttan
  sonraki bekleme süresini sıfırlıyor.
- **Kayıt içi zoom ve spot ışığı.** screenity: "Zoom in smoothly in your recordings
  to focus on specific areas", "go in spotlight mode". Cap'te Studio Mode'un otomatik
  zoom'u.
- **Tıklama ve imleç vurgusunun ayrı ayrı açılması.** vokoscreenNG'de Showclick +
  Halo + büyüteç; Captura'da "Capture Mouse Clicks or Keystrokes"; screenity'de
  "Highlight your clicks and cursor".
- **Tuş vuruşu gösterimi.** Yalnız Captura ve ScreenToGif. Eğitim videosunda ölçülür
  fayda, maliyeti düşük.
- **Otomatik durdurma alarmı.** screenity: "Set up alarms to automatically stop your
  recording"; LICEcap: "automatically stop after X seconds".
- **Duraklatınca araya başlık/metin karesi ekleme.** LICEcap'e özgü: "Pause and
  restart recording, with optional inserted text messages".
- **Kayıt sırasında canlı önizleme + istatistik.** SSR: "Can show a preview during
  recording, so you don't waste time recording something only to figure out
  afterwards that some setting was wrong." Yanlış ayarla çekilmiş bir saatlik kaydı
  önleyen şey.
- **Otomasyon / koşullu kayıt.** SceneSwitcher makroları (süreç, ses seviyesi, tarih,
  medya, imleç koşulları) ve obs-websocket ile dışarıdan kumanda: "Remote control OBS
  from a phone or tablet on the same local network", "auto-pilot, foot pedal".
- **Yeşil perdesiz arka plan ayırma.** obs-backgroundremoval — webcam bindirmesini
  amatörlükten çıkaran şey; "available for everyone on every system, even if they
  don't own a GPU".
- **Kaydırmalı yakalama ve OCR.** ShareX: "Scrolling capture", "Recognize text (OCR)".
- **Oyun / OpenGL doğrudan yakalama.** OBS Game Capture ("Capture hardware-accelerated
  games with high performance") ve SSR ("records OpenGL applications directly (similar
  to Fraps on Windows)").
- **Boşta kalma süresini kırpma.** asciinema'nın "idle time limiting" fikri — uzun ve
  ölü anları olan ekran kayıtlarına doğrudan uyarlanabilir.
- **Ayrı ses izleri.** OBS: "you can select which audio tracks you'd like to record" —
  mikrofonu ve sistem sesini ayrı izlere yazmak sonradan düzenlemeyi kurtarıyor.
- **Kayıt biçiminin çökmeye dayanıklı seçilmesi.** OBS: "MKV is recommended in case of
  unforeseen issues arising, since MKV will not corrupt the whole file if there is no
  graceful stoppage of recording."

## 5. Arayüz kararları

- **Menü çubuğu / tepsi kaydedicisi.** Kap: "Click the menu bar icon to bring up the
  screen recorder… hit the record button to start recording. Click the menu bar icon
  again to stop the recording", "While recording, Option-click the menu bar icon to
  pause or right-click for more options." green-recorder ve deepin de durdurmayı tepsi
  simgesine bağlıyor.
- **Pencerenin kendisi kayıt çerçevesi.** peek: "you simply place the Peek window over
  the area you want to record and press 'Record'"; "window position itself is used to
  obtain the recording coordinates." LICEcap aynı modeli kullanıyor ve çerçeveyi kayıt
  sırasında taşımaya izin veriyor.
- **Kompakt kaydedici + ayrı düzenleyici.** ScreenToGif'te küçük kaydedici penceresi;
  kayıt bitince kare kare düzenleyici açılıyor.
- **Yüzen araç çubuğu.** screenity: "Set a countdown, hide parts of the UI, or move it
  anywhere" — çubuk kayıt sırasında ekranda kalıyor ama taşınabiliyor/gizlenebiliyor.
- **Sihirbaz akışı + makul varsayılanlar.** SSR ayarları sayfa sayfa soruyor, "Sensible
  default settings: no need to change anything if you don't want to" diyor, kayıt
  ekranında önizleme ve canlı istatistik gösteriyor.
- **Ayarsız tek düğme.** Kooha "simply click the record button without having to
  configure a bunch of settings" diyor; gelişmiş seçenekler (çoklu monitör, pencere,
  donanım kodlayıcı) deneysel bayrağın arkasında.
- **Tek pencerede karışım masası.** OBS: "Modular 'Dock' UI allows you to rearrange the
  layout exactly as you like", Studio Mode ile yayına vermeden önce önizleme, Multiview
  ile 8 sahne.
- **İki kip, iki hedef.** Cap: hız için Instant Mode, cila için Studio Mode — "hızlı" ile
  "iyi" arasındaki gerilimi kullanıcıya açıkça soruyor.
- **CLI'da durdurma.** wf-recorder kaydı `Ctrl+C` ile bitiriyor; asciinema oturum içinde
  tuş bağıyla duraklatıp işaret koyuyor.

## 6. Kaynaklar

- OBS Studio — https://obsproject.com/ · kaynak türleri https://obsproject.com/kb/sources-guide · kayıt çıkışı https://obsproject.com/kb/standard-recording-output-guide · gelişmiş kayıt https://obsproject.com/kb/advanced-recording-settings-guide · depo https://github.com/obsproject/obs-studio
- ShareX — https://getsharex.com/ · https://github.com/ShareX/ShareX
- flameshot — https://github.com/flameshot-org/flameshot
- ScreenToGif — https://github.com/NickeManarin/ScreenToGif
- Cap — https://github.com/CapSoftware/Cap
- Kap — https://github.com/wulkano/Kap
- screenity — https://github.com/alyssaxuu/screenity
- asciinema — https://github.com/asciinema/asciinema
- Captura — https://github.com/MathewSachin/Captura
- peek — https://github.com/phw/peek
- screego — https://github.com/screego/server
- Gifski — https://github.com/sindresorhus/Gifski
- RecordRTC — https://github.com/muaz-khan/RecordRTC
- LICEcap — https://www.cockos.com/licecap/ · https://github.com/justinfrankel/licecap
- Streamlabs Desktop — https://streamlabs.com/desktop · https://github.com/streamlabs/desktop
- obs-backgroundremoval — https://github.com/royshil/obs-backgroundremoval
- obs-websocket — https://github.com/obsproject/obs-websocket
- StreamFX — https://github.com/Vhonowslend/StreamFX-Public
- Kooha — https://github.com/SeaDve/Kooha
- SimpleScreenRecorder — https://www.maartenbaert.be/simplescreenrecorder/ · https://github.com/MaartenBaert/ssr
- Advanced Scene Switcher — https://github.com/WarmUpTill/SceneSwitcher/wiki · https://github.com/WarmUpTill/SceneSwitcher
- vokoscreenNG — https://linuxecke.volkoh.de/vokoscreen/vokoscreen.html · https://github.com/vkohaupt/vokoscreenNG
- wf-recorder — https://github.com/ammen99/wf-recorder
- green-recorder — https://github.com/mhsabbagh/green-recorder
- blue-recorder — https://github.com/xlmnxp/blue-recorder
- kazam — https://github.com/hzbd/kazam
- obs-replay-source — https://github.com/exeldro/obs-replay-source
- deepin-screen-recorder — https://github.com/linuxdeepin/deepin-screen-recorder · https://www.deepin.org/en/dtk/deepin-screen-recorder/
- scrcpy — https://github.com/Genymobile/scrcpy
