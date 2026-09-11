[[netlestirme:003]]

# Netleştirme: Rakip oynatıcıların (GOM öncelikli) özellikleri VidShrink oynatıcısına nasıl sın

İşe başlamadan önce soruyu keskinleştir. Görüş verme, plan yazma, kod yazma.
Yalnız şunu döndür: soruda belirsiz kalan yerler, her biri için tek satırlık bir netleştirme sorusu, en fazla beş. Belirsizlik yoksa "net" yaz.

## Soru

Rakip oynatıcıların (GOM öncelikli) özellikleri VidShrink oynatıcısına nasıl sınıflandırılıp hangi sırayla uygulanmalı? (1) Her özelliği Standart (işe yarayan, ana arayüzde) / Gelişmiş (ayrı Gelişmiş başlığı altında) / Alınmayacak olarak sınıflandır, gerekçesi tek cümle. (2) Mevcut mimari sınırlar (sabit -re, her aramada yeni ffmpeg süreci, kullanılmayan PlaybackClock, video/ses ayrı süreç, üç ayrı oynatıcı yolu) göz önünde: önce hangi mimari temel atılmalı (ortak çekirdek, saat, hız) ve özellikler hangi dalgalara bölünmeli? Her dalga için kapsam, önkoşul, kabul ölçütü öner. (3) Gelişmiş başlığı arayüzde nerede durmalı (sağ tık alt menüsü, ayrı panel, ayarlar sekmesi)?

## Elde olan olgular

# Olgular: oynatıcı özellik fark analizi

Kullanıcının cümleleri (aynen):
- "gom ve benzeri playerleri incele tüm özelliklerini istiyorum playerimde önce kapsamlı analiz ve uygulama fabla a danış bana sorma"
- "özellikleri nasıl sınıflandıracağımızı fable karar versin işe yarayan standartlar bi alanda gelişmişleri gelişmiş başlığı altında ayrı"

Proje kısıtları: .NET 8 + Avalonia + ffmpeg (ayrı süreç, rawvideo borusu). Renk yalnız palet, ölçü yalnız Theme.axaml belirteçleri. Arayüz 43 dil. Her iş sözleşme/dalga olarak ilerliyor, dotnet test tam yeşil olmadan teslim yok.

---

# Masaüstü Video Oynatıcı Özellik Envanteri

Kapsam: GOM Player (öncelikli), PotPlayer, VLC, MPC-HC/MPC-BE, mpv, KMPlayer.
Kaynaklar resmi sayfalar, kısayol wiki'leri ve kılavuzlardan derlendi; doğrulanamayan
noktalar ayrıca işaretli. Tarih: 2026-09-11.

## 1. Oynatma denetimi

| Özellik | GOM | PotPlayer | VLC | MPC-HC/BE | mpv | KMPlayer | GOM varsayılan kısayol | Not |
|---|---|---|---|---|---|---|---|---|
| Hız değiştirme | var | var | var | var | var | var | `C` hızlan, `X` yavaşlat, `Z` normal (`Ctrl+Shift+F/B/N` alternatif) | Hepsinde ince adımlı hız çarpanı var |
| Kare kare ileri/geri | var | var | var (yalnız ileri, `E`) | var | var | doğrulanamadı | `F` / `Ctrl+>` ileri, `Ctrl+<` geri | VLC'de resmi olarak sadece ileri kare adımı var |
| A-B tekrar (bölüm döngüsü) | var | var (`[`/`]`) | var (resmi değil; Windows'ta topluluk çözümü, Mac'te `Shift+L`) | doğrulanamadı | var (script/komutla) | doğrulanamadı | `[` başlangıç, `]` bitiş, `/` iptal, `Ctrl+Alt+Shift+A` döngü ayarı | VLC'de A-B döngü arayüzde resmi buton yok |
| Atlama adımları (5/10/30sn vb.) | var | var | var (10sn / Shift+ok 3sn) | var (orta/keyframe/özel adım) | var | var (5sn, Ctrl+ 30sn, Alt+ 1dk, Ctrl+Alt+ 10dk) | Sol/Sağ ok: 10sn geri/ileri (değiştirilebilir) | KMPlayer'ın katmanlı adım sistemi öne çıkıyor |
| Sürdürme / kaldığı yerden devam | var | var | var | var | var (resume playback özelliği var) | var | Preferences > Kaldığı yerden devam ayarı | Hepsinde ayarlanabilir açma/kapama var |

## 2. Arama / zaman çizelgesi

| Özellik | GOM | PotPlayer | VLC | MPC-HC/BE | mpv | KMPlayer | Not |
|---|---|---|---|---|---|---|---|
| Önizleme küçük resmi (seek bar thumbnail) | doğrulanamadı | var | doğrulanamadı (resmi thumbnail yok, eklenti ile) | var (MPC-BE: "Use the preview in the search" ayarı, YouTube tarzı) | var (thumbnail script eklentisiyle, çekirdekte yok) | doğrulanamadı | MPC-BE'de yerleşik, mpv'de topluluk betiğiyle |
| Bölüm işaretleri (chapter) | var (DVD menüsü/chapter geçişi) | var | var | var (chapter marker desteği) | var | doğrulanamadı | GOM'da Page Up/Down: DVD Önceki/Sonraki Bölüm |
| Yer imleri / bookmark | var (`N` ekle, `B` seç, Shift+PgUp/Dn gezin) | var (`P` ekle, `H` tümünü göster) | doğrulanamadı (yerleşik bookmark yok, playlist ile) | doğrulanamadı | doğrulanamadı | var (`P` ile bookmark, Alt+PgUp/Dn ile git) | GOM ve PotPlayer'da net bookmark sistemi var |

## 3. Görüntü

| Özellik | GOM | PotPlayer | VLC | MPC-HC/BE | mpv | KMPlayer | GOM kısayol | Not |
|---|---|---|---|---|---|---|---|---|
| En-boy oranı değiştirme | var | var (sayı tuşları) | var (`A` döngü) | var | var | doğrulanamadı | `Ctrl+F5`–`F11` | |
| Kırpma | var | doğrulanamadı | var (menüden) | var | var (`--vf=crop` / panscan) | doğrulanamadı | Numpad pan&scan tuşları | |
| Yakınlaştırma / kaydırma | var | var | var (`Z` döngü) | var | var | doğrulanamadı | Numpad +/− | |
| Döndürme / aynalama | var | var (`Ctrl+Z` yatay, `Ctrl+V` dikey) | doğrulanamadı (temel döndürme filtresiyle var) | var | var | var ("mirror mode") | `Shift+Ctrl+S` döndür; `Ctrl+Y/V/H/J` çeşitli çevirmeler | |
| Parlaklık/kontrast/doygunluk/renk tonu | var | var | var (Effects menüsü) | var | var | doğrulanamadı | `R/E/Y/T/I/U/P/O` ayrı ayar tuşları, `Q` sıfırla | GOM'da her parametre için ayrı tuş çifti |
| Keskinlik / deinterlace | var (filtre menüsü) | var | var (`D` toggle, Mac `Shift+D` mod döngüsü) | var | var (deinterlace filtre döngüsü) | doğrulanamadı | Shift+F: gelişmiş filtre ayarları | |
| Tam ekran | var | var | var | var | var | var | `Enter` / `Alt+Enter` | |
| Her zaman üstte | var | doğrulanamadı | doğrulanamadı | var (`Ctrl+A`) | doğrulanamadı (ontop özelliği config ile var) | doğrulanamadı | `Ctrl+A`, `Ctrl+T` | |
| Mini / kompakt mod | var (min. boyut) | var (mini mod) | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı | `'` (apostrof) minimum boyut, `Shift+X` küçült+duraklat | |

## 4. Ses

| Özellik | GOM | PotPlayer | VLC | MPC-HC/BE | mpv | KMPlayer | GOM kısayol | Not |
|---|---|---|---|---|---|---|---|---|
| %100 üstü ses güçlendirme | var (normalize + master volume) | var | var (Effects > Amplify, ~%200'e kadar) | var | var (`--volume-max`) | var (400%'e kadar boost bildiriliyor) | `Ctrl+Alt+↓/↑` normalize ses ayarı | KMPlayer'da en yüksek üst sınır bildirildi (doğrulanamadı: tam yüzde kaynağa göre değişiyor) |
| Ses parçası seçimi | var | var (`A`) | var (`B` döngü) | var | var | var | `A` | |
| Ses gecikmesi | var | var | var | var | var | var | Ses tercihleri menüsünden | |
| Ekolayzer | var (`L` preset seç, `Shift+E` aç/kapa) | var (`F7`) | var (grafik ekolayzer) | var | var (harici filtre/script ile) | var | `L`, `Shift+E` | |
| Normalleştirme | var (`Shift+N`) | var | var | doğrulanamadı | var (`--audio-normalize-downmix` vb.) | doğrulanamadı | `Shift+N` | |
| Sessiz (mute) | var | var | var (`M`) | var (`Ctrl+M`) | var | var (`M`) | `M` | |

## 5. Altyazı

| Özellik | GOM | PotPlayer | VLC | MPC-HC/BE | mpv | KMPlayer | GOM kısayol | Not |
|---|---|---|---|---|---|---|---|---|
| Dış dosya (srt/ass/vtt) | var | var | var | var | var | var (SRT/ASS/SSA/SUB/IDX vb.) | `F3` aç, `Ctrl+L` benzeri (MPC) | |
| Gömülü parça seçimi | var (`S`) | var | var (`V`/Mac `S`) | var | var | var | `S` | |
| Gecikme ayarı | var (`>`/`<`/`/`, `Alt+S` senk. kaydet) | var | var (`G`/`H`) | var (`F1` geri, `F2` ileri) | var | var (otomatik senkron + manuel) | `>` `<` `/` | |
| Boyut / renk / konum | var (Alt+PgUp/Dn boyut, Alt+ok konum, Alt+B kalınlık) | var | var (sınırlı) | var | var (stil override) | var | Alt+Home varsayılan konum | |
| Kodlama seçimi | var (Alt+E) | var | var | var | var | var | | |
| Otomatik bulma / indirme | var (Easy Browser ile) | var | doğrulanamadı (yerleşik otomatik indirme yok) | var (`D` ile indirme, MPC-HC eklenti) | doğrulanamadı (çekirdekte yok, script ile) | var (online veritabanından otomatik indirme) | Alt+I Easy Browser | |

## 6. Çalma listesi

| Özellik | GOM | PotPlayer | VLC | MPC-HC/BE | mpv | KMPlayer | GOM kısayol | Not |
|---|---|---|---|---|---|---|---|---|
| Klasördeki sonraki dosyayı otomatik oynatma | var | var | var | var | var | var | Page Up/Down dosya gezme | |
| Sürükle-bırak | var | var | var | var | var | var | — | Hepsinde standart Windows davranışı |
| Karıştır / tekrar | var (`Ctrl+Alt+Shift+F` karıştır, `+B` tekrar) | var | var | var | var | var | `Ctrl+Alt+Shift+F/B` | |

## 7. Ekran görüntüsü ve klip/GIF dışa aktarma

| Özellik | GOM | PotPlayer | VLC | MPC-HC/BE | mpv | KMPlayer | GOM kısayol | Not |
|---|---|---|---|---|---|---|---|---|
| Ekran görüntüsü (kaydet/panoya kopyala) | var | var | var (`Shift+S`) | var | var (yerleşik screenshot komutu) | doğrulanamadı | `Ctrl+G` gelişmiş yakalama, `Ctrl+C` panoya, `Ctrl+E` dosyaya, `Ctrl+Q` galeri | |
| Video kayıt (segment kaydı) | doğrulanamadı | var | var (`Shift+R`) | doğrulanamadı | doğrulanamadı (çekirdekte yok) | doğrulanamadı | | |
| GIF dışa aktarma | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı | var (yalnız topluluk betiği: "giffer", "video_cutter", çekirdekte yok) | doğrulanamadı | | mpv dışında hiçbirinde resmi GIF dışa aktarma doğrulanamadı |

## 8. Bilgi paneli

| Özellik | GOM | PotPlayer | VLC | MPC-HC/BE | mpv | KMPlayer | GOM kısayol | Not |
|---|---|---|---|---|---|---|---|---|
| Codec / bitrate / çözünürlük / fps gösterimi | var (`Ctrl+F1`) | var | var (Tools > Codec Information, `Ctrl+J` benzeri) | var | var (OSD ile) | var | `Ctrl+F1` dosya bilgisi | |

## 9. Fare hareketleri

| Özellik | GOM | PotPlayer | VLC | MPC-HC/BE | mpv | KMPlayer | Not |
|---|---|---|---|---|---|---|---|
| Teker ile ses/arama | var (varsayılan ses, `Ctrl+`teker sistem karıştırıcı, `Ctrl+Shift+`teker master ses) | var | var (teker=ses) | var | var (yapılandırılabilir) | var | GOM'da tekerlek davranışı modifiye tuşlarla katmanlı |
| Çift tık tam ekran | doğrulanamadı (GOM'da varsayılan davranış net doğrulanamadı) | var | var | var | var | var | |
| Sağ tık menüsü | var | var | var | var | var | var | Hepsinde bağlam menüsü standart |

## 10. Klavye kısayolları

Bkz. Bölüm 12 (GOM tam liste). Diğer oynatıcılarda tam liste sayıları:
PotPlayer ~257 kısayol (varsayılan+özelleştirilebilir), VLC 200+ yapılandırılabilir tuş,
MPC-HC/BE ~29-50 arası belgelenen temel kısayol (tam liste dahili Options>Keys ekranında),
KMPlayer ~25 belgelenen temel kısayol (F2 > Keys/Remote control menüsünden tam liste),
mpv'de varsayılan kısayollar ikili içine gömülü, kaynak `etc/input.conf` dosyasında listeli.

## 11. Dosya ilişkilendirme, son açılanlar, akış/URL açma

| Özellik | GOM | PotPlayer | VLC | MPC-HC/BE | mpv | KMPlayer | Not |
|---|---|---|---|---|---|---|---|
| Dosya ilişkilendirme (varsayılan oynatıcı yapma) | var (kurulumda seçenek) | var | var | var | var (Windows'ta manuel ilişkilendirme) | var | |
| Son açılanlar listesi | var | var | var | var | var | var | |
| Akış / URL açma | var (`Ctrl+U`) | var | var (`Ctrl+N` ağ akışı) | var | var (komut satırından URL) | doğrulanamadı | `Ctrl+U` |

---

## 12. GOM Player — Tam Varsayılan Klavye Kısayol Listesi

Kaynak: GOM Lab resmi SSS sayfası (gomlab.com/en/faq-info?id=51).

### Fonksiyon tuşları
- F1: GOM Player Hakkında penceresini göster
- F2: Dosya aç
- F3: Altyazı dosyası aç
- F4: Videoyu durdur ve kapat
- F5: Tercihler penceresini aç
- F6: 360° video oynatma
- F7: Kontrol Paneli penceresini aç/kapat
- F8: Çalma Listesi penceresini aç/kapat
- F12: DVD aç

### Favoriler
- Alt+G: Geçerli dosyayı Favorilere ekle
- Alt+D: Geçerli klasörü Favorilere ekle
- Alt+F: Favoriler Yöneticisini aç

### Pencere boyutu/konumu
- ' (apostrof): Minimum boyut
- Shift+X: Küçült ve duraklat
- 1–9, 0: Çeşitli yakınlaştırma seviyeleri ve tam ekran modları
- Alt+1–7: Alternatif yakınlaştırma kısayolları
- Del: Video girişine sığdır
- Alt+Enter, Enter: Tam Ekran (En-boy oranını koru)
- Ctrl+Enter: Tam Ekran (Gerilmiş)
- Ctrl+Alt+Enter: Tam Ekran (Gerilmiş - Oranı koru)
- Esc: Tam ekranı kapat / küçült
- Numpad 1–9: Pan & scan denetimleri
- Numpad +/−: Oynatma alanı boyutunu ayarla
- Ctrl+Alt+Numpad: Pencere konumlandırma

### Oynatma denetimi
- Ctrl+I: Easy Browser'ı aç
- Ctrl+O: Dosya aç
- Ctrl+U: URL aç
- Ctrl+D: Klasör aç
- Boşluk, Ctrl+P: Oynat/Duraklat
- Ctrl+Boşluk: Durdur
- Ok tuşları (modifiyeli): İleri/geri sarma seçenekleri
- F, Ctrl+>: Bir kare ileri
- Ctrl+<: Önceki kare
- Page Up/Down: Dosyalar arasında gezin
- Backspace: Başa dön
- G: Atlanacak konumu belirt
- B: Yer imi seç
- N: Yer imi ekle
- Shift+Page Up/Down: Yer imleri arasında gezin

### Oynatma hızı
- Z, Ctrl+Shift+N: Varsayılan hız
- X, Ctrl+Shift+B: Hızı düşür
- C, Ctrl+Shift+F, Ctrl+Shift+G: Hızı artır

### Çalma listesi
- Ctrl+Alt+Shift+F: Karıştırmayı aç/kapat
- Ctrl+Alt+Shift+B: Tekrarı aç/kapat
- Ctrl+F: Listede bul

### En-boy oranı
- Ctrl+F5–F11: Çeşitli en-boy oranı seçenekleri

### Görüntü rengi ve efekt denetimi
- R, E, Y, T, I, U, P, O: Parlaklık, kontrast, doygunluk, ton ayarları
- W: Kontrast ve parlaklığı artır
- Q: Görüntü renk ayarlarını sıfırla
- V: Video seç
- Shift+Ctrl+S: Döndür
- Ctrl+Y, Ctrl+V, Ctrl+H, Ctrl+J: Çevirme seçenekleri
- Ctrl+M, Ctrl+N, Ctrl+L: Efekt aç/kapa

### Altyazı
- Alt+O, Alt+H, Alt+E: Altyazı dosyası ve görüntüleme denetimleri
- Alt+L: Dili değiştir
- Alt+S: Geçerli senkronu kaydet
- Alt+I: Altyazıları Easy Browser ile aç
- Alt+Ok tuşları, Alt+Numpad: Altyazı konumlandırma
- Alt+Home: Varsayılan altyazı konumu
- Alt+Page Up/Down: Altyazı boyutlandırma
- Alt+B: Altyazı kalınlığını değiştir
- >, <, /: Altyazı senkron ayarları
- Home, End: Altyazılar arasında gezin
- S: Altyazı dilini seç
- Ctrl+Alt+Shift+S: Altyazı dilini göster/gizle

### Ses denetimi
- Ok tuşları, Fare tekerleği: Ses seviyesi denetimleri
- Ctrl+Ok/Tekerlek: Sistem karıştırıcı ses seviyesi
- Ctrl+Shift+Ok/Tekerlek: Ana ses seviyesi
- M: Sessiz
- A: Ses akışı seç
- L: EQ ön ayarı seç
- Shift+G: Gelişmiş Ses Yakalamayı aç
- Shift+E, Shift+N: Ekolayzer ve normalleştirici aç/kapa
- Ctrl+Alt+↓/↑: Normalleştirilmiş sesi ayarla
- Shift+P: Ses Tercihlerini aç
- Shift+R, Shift+S: Reverb efektini ve 3D ses efektini aç/kapa

### AB Tekrar / Atlama
- [, ]: Başlangıç işareti (A) ve bitiş işareti (B) belirle
- Shift+[/]: İşaretleri temizle
- /: AB Tekrarı iptal et
- ': AB Atlama ayarları
- Ctrl+Alt+Shift+A: AB Döngü belirle

### Ekran görüntüsü
- Ctrl+G: Gelişmiş Ekran Yakalama
- Ctrl+C: Panoya kopyala
- Ctrl+E: Dosya olarak kaydet
- Ctrl+Q: Anlık görüntü/galeri oluştur
- Ctrl+7–0: Masaüstü arka planı seçenekleri

### DVD denetimi
- D: DVD menüsü
- Page Up/Down: Önceki Bölüm / Sonraki Bölüm
- Ctrl+Backspace: DVD Kök Menüsüne git
- Enter: DVD Enter tuşu

### Diğer kısayollar
- Ctrl+X: Oynatma bitince çık
- Ctrl+Z: Film bitince bilgisayarı kapat
- Ctrl+F1: Geçerli dosya bilgisini göster
- Ctrl+A, Ctrl+T: Her zaman üstte aç/kapa
- Alt+X, Alt+Q, Alt+F4: GOM Player'dan çık
- Alt+T: TTS aç
- Shift+F: Gelişmiş filtre ayarları
- Shift+Delete: Oynatılan dosyayı çöp kutusuna taşı
- Tab, Ctrl+Shift+Tab: Oynatma bilgisi göster

---

## 13. Kaynak URL'leri

- https://www.gomlab.com/en/faq-info?id=51&page=1&product=GOMPLAYER (GOM Player kısayolları, resmi SSS)
- https://www.gomlab.com/en/faq-info?id=80&page=15 (GOM Player Plus kısayolları, resmi SSS)
- https://www.gomlab.com/en/guide/gomplayerplus/2024?pageNum=control (GOM Player+ resmi kullanıcı kılavuzu)
- https://tutorialtactic.com/keyboard-shortcuts/gom-player-shortcuts/ (GOM Player 148 kısayol derlemesi)
- https://hotkeysworld.com/gom-player-for-windows (GOM Player kısayol listesi)
- https://defkey.com/potplayer-shortcuts (PotPlayer kısayolları)
- https://tutorialtactic.com/blog/potplayer-shortcuts/ (PotPlayer 100+ kısayol derlemesi)
- https://hotkeysworld.com/potplayer-for-windows (PotPlayer kısayol listesi)
- https://www.vlchelp.com/vlc-media-player-shortcuts/ (VLC kısayolları, kategori bazlı)
- https://www.ditig.com/vlc-cheat-sheet (VLC kısayol özet sayfası)
- https://www.makeuseof.com/vlc-media-player-windows-shortcuts/ (VLC Windows kısayolları)
- https://defkey.com/mpc-hc-shortcuts (MPC-HC kısayolları)
- https://aboutdevice.com/mpc-be-keyboard-shortcut-or-hotkeys/ (MPC-BE kısayolları)
- https://github.com/gehrleib/MPC-BE-with-madVR (MPC-BE + madVR entegrasyonu)
- https://www.slant.co/versus/23474/23476/~mpc-be_vs_gom-player (MPC-BE vs GOM karşılaştırması)
- https://mpv.io/manual/master/ (mpv resmi kılavuz, klavye denetimi)
- https://github.com/mpv-player/mpv/blob/master/etc/input.conf (mpv varsayılan kısayol tanımları, kaynak dosya)
- https://github.com/stax76/awesome-mpv (mpv topluluk betikleri: giffer, video_cutter, thumbnail script)
- https://defkey.com/mpv-media-player-shortcuts (mpv kısayol özeti)
- https://defkey.com/kmplayer-shortcuts (KMPlayer kısayolları)
- https://www.addictivetips.com/windows-tips/20-features-of-kmplayer-that-you-probably-dont-know-about/ (KMPlayer az bilinen özellikler)
- https://km-player.com/features.html (KMPlayer resmi özellik sayfası)
- https://shortcutworld.com/KMPlayer/win/KMPlayer_Shortcuts (KMPlayer 25 kısayol listesi)

---

# VidShrink Oynatıcı — Mevcut Envanter

Salt okuma amaçlı mimari/özellik envanteri. Kod yazılmadı, değiştirilmedi.

## 1. Mimari

Kod tabanında birbirinden bağımsız **üç "oynatıcı"** var:

**(a) Tek dosya sekmesi — `PlayerView`**
- `PlayerView.axaml.cs` (461 satır) tek başına oynatıcıyı yönetir: `ZoomGesture _zoom`, `FullscreenSwitch _fullscreen`, `StallWatch _stall`, `SeekCoalescer _seek`, `DecoderPipe? _pipe`, `DecoderPipe.ContinuousPlayback? _play`, `AudioSink? _audio` alanları.
- `StartPlayback`: `_pipe.StartContinuousPlayback(atSeconds)` çağırır, 16ms'lik `DispatcherTimer` (`_render`) her tikte `RenderLatest()` çağırır (`PlayerView.axaml.cs:` — RenderLatest `play.TryCopyLatest(ref _shown, DrawBgra)` ile en son kareyi çeker).
- `OpenAsync`: `DecoderPipe` açar, `pipe.HasAudio` ise `AudioSink` bağlar, 500ms'lik watchdog `DispatcherTimer` (`PollStall`) kurar.
- Girdi haritası **App katmanında** `PlayerInputMap.cs` (281 satır) içinde: `Wheel`/`Press`/`Key` statik fonksiyonları donanım olayını `PlayerCommand`'a çevirir; `PlayerView` bunu yorumlar.
- `SeekCoalescer` (`PlayerInputMap.cs:114-206`) — sadece bu sekmede kullanılır; hızlı ardışık `Nudge` çağrılarını tek pump-loop ile biriktirir (`GoTo`→`_pending=true`→`PumpAsync` tek seferde en son hedefe gider).
- Alt katman: `DecoderPipe` (`src/VidShrink.Ffmpeg/Playback/DecoderPipe.cs`, 653 satır) — `StartContinuousPlayback` ffmpeg'i `-re -fps_mode passthrough` ile rawvideo BGRA çıktısı için başlatır; `ContinuousPlayback` iç sınıfı çift tamponlu (`_buffer`/`_front`, `Pump()` içinde kilit altında takas). `SeekAsync`: 128MB tavanlı keyframe-indeksli LRU önbellek → önbellek isabetinde anında, `MaxForwardCatchupFrames=3` içindeyse "yakalama", değilse `RestartVideo` (yeni ffmpeg süreci, `-ss X -skip_frame nokey`). Ses için **ayrı bir ffmpeg süreci** (`SeekAudio`, PCM s16le çıktısı) — video ve ses iki bağımsız boru.
- Ses çıkışı: `AudioSink.cs` (101 satır) — NAudio `WaveOutEvent`+`BufferedWaveProvider`, 48kHz/16bit/stereo, `DesiredLatency=80ms`.
- **`PlaybackClock.cs`** (82 satır, `src/VidShrink.Ffmpeg/Playback/`) — hız-farkında soyut saat sınıfı (`Start/Pause/Resume/Seek/Rate/PositionSeconds`) var ama **kod tabanında hiçbir yerde örneklenmiyor** (grep ile doğrulandı — kendi dosyası dışında referans yok). Yani A/V senkronu için "resmi" bir saat mekanizması yok; gerçek senkron, video ve ses borularının aynı zaman damgasına ayrı ayrı `-re` ile seek edilmesine dayanıyor, aralarında uzlaştırıcı bir kod bulunamadı.

**(b) Karşılaştırma paneli — `ComparisonPanel` + `PanelHost` + `PipeComparisonFrameSource`**
- `MainWindow.axaml.cs:142`: `_preview = new PanelHost(Preview, () => new PipeComparisonFrameSource());` — panel tek bir hstack ffmpeg sürecinden beslenir.
- `PipeComparisonFrameSource.cs` (492 satır): tek ffmpeg süreci → `FrameRing`(4) + `FramePool`; arka plan `Pump()` iş parçacığı; `Play/Pause` ffmpeg sürecini değil **okuyucu iş parçacığını** `ManualResetEventSlim` ile durdurur; `SeekAsync` sürecin **tamamını yeniden başlatır** (DecoderPipe'daki önbellek/yakalama yok).
- `ComparisonGraph.cs` (170 satır) hstack filtre grafiğini kurar: `[0:v]fps=N,scale[l];[1:v]fps=N,scale[r];[l][r]hstack[v]`.
- `ComparisonSurface.cs` (514 satır): tek `WriteableBitmap`'i **iki farklı kırpma dikdörtgeniyle iki kez çiziyor** (split-view airspace sorununu böyle çözüyor); `TopLevel.RequestAnimationFrame` ile sürülüyor (DispatcherTimer değil).
- `PanelHost.cs` (1055 satır) — panel ile kaynak arasında yapıştırıcı. "Handover" deseni: bir segment bitmeden ~160ms önce (`HandoverLeadSeconds=0.16`) bir sonraki segmentin ffmpeg borusu önceden açılır (`BeginHandover/OpenStandbyAsync`), `SwapAt(duration,fps)=duration-1.5/fps` anında `_source` atomik olarak değiştirilir (`SwapToStandby`) — ffmpeg'in ~105ms süreç-başlatma gecikmesini gizlemek için.
- `Seek`: segment-önizleme modunda yeni bir kodlama planlar (`ScheduleClip`, 400ms debounce — `SegmentEncoder`), tam-çıktı modunda doğrudan `source.SeekAsync` (yine süreç yeniden başlatma).
- `ClipSignature` = ffmpeg argüman listesinin kendisi (manuel alan karşılaştırması değil) — "bir şey değişti mi" testinde kullanılıyor.

**(c) Önizleme sesi — `PreviewAudio.cs`** (146 satır): hstack borusu sadece video taşıdığı için, karşılaştırma paneline ses beslemek üzere **üçüncü, bağımsız** bir `DecoderPipe`+`AudioSink` çifti açılır (`AttachAsync`, aynı yol zaten bağlıysa yeniden açmaz).

**Kablolama (`MainWindow.axaml` / `.axaml.cs`):**
- `MainWindow.axaml:192`: `<playback:PlayerView x:Name="Player"/>`
- `MainWindow.axaml:613`: `<playback:ComparisonPanel Grid.Row="0" x:Name="Preview" .../>`
- `MainWindow.axaml.cs:2531`: `internal PlayerView PlayerTab => Player;`
- `MainWindow.axaml.cs:2536,2558`: açılışta `PlayerView.Echo(...)` iz kayıtları.
- Kabuk entegrasyonu: `VidShrink.Core.ShellIntegration.ResolveStartupPath(argv)` komut satırı dosya yolunu çözer, `MainWindow(path)` kurucusu `LoadStartupFileAsync` ile oynatıcı sekmesine geçip dosyayı açar (`tests/VidShrink.Tests/OynaticiGirdiTests.cs:288-320`, `KabukYolununActigiSekmeOynaticidir`).

Diğer ilgili dosyalar: `ZoomGesture.cs` (302 satır, saf C#, Avalonia bağımsız) — `T` (0..1) parametresiyle hem zoom hem panel "gölgeleme" (Band/Mid/Full `ShelterStage`) aynı histerezis makinesinden sürülüyor (`FullAt=1.00`, `FullDropAt=0.92`). `HoverZone.cs` (250 satır) — kontrol çubuğunun otomatik gösterme/gizlemesi, `IHoverClock` test edilebilir saat soyutlaması, Windows `SPI_GETCLIENTAREAANIMATION` P/Invoke ile "azaltılmış hareket" tespiti. `ControlStrip.axaml.cs` (406 satır) — zaman çizelgesi sürükleme, `SetEncodeProgress` ile final-kodlama ilerleme imleci.

## 2. Mevcut özellik listesi

| Özellik | Durum | Kanıt |
|---|---|---|
| Oynat/Duraklat | **VAR** | `PlayerInputMap.cs:83` (sağ tık→TogglePlay), `:90` (Space→TogglePlay); `ControlStrip.axaml.cs` PlayPauseRequested |
| Arama çubuğu / scrub | **VAR** | `ControlStrip.axaml.cs` `OnTimelinePressed/Moved/Released`; `ControlStrip.axaml:` Timeline Grid |
| Tekerlek ile arama | **VAR** | `PlayerInputMap.cs:73-79` `Wheel()` — adım 1/10/60/300 sn (Ctrl/Shift kombinasyonu) |
| Zoom/Pan (tekerlek+Alt, sürükle) | **VAR** | `ZoomGesture.cs` tam sınıf; `PlayerInputMap.cs:75-76` Alt+tekerlek→Zoom |
| Tam ekran | **VAR** | `FullscreenSwitch` (`PlayerInputMap.cs:216-242`), orta tık→ToggleFullscreen (`:84`) |
| Klavye kısayolları | **KISMİ** | Sadece 3 tuş tanınıyor: Space/Menu/Escape (`PlayerInputMap.cs:24-30`, `PlayerView.axaml.cs` `OnKey`); ok tuşuyla arama/ses YOK (ControlStrip'in Timeline'ında Left/Right/PageUp/PageDown var ama sadece karşılaştırma panelinde, odak Timeline'dayken) |
| Sağ tık bağlam menüsü | **VAR** | `PlayerInputMap.MenuRows` (3 satır: playpause/fullscreen/reset), `PlayerView.BuildMenu()`, test: `OynaticiGirdiTestsMenuSatirlari` |
| Ses seviyesi (volume) | **YOK** | `AudioSink.cs` içinde seviye kontrolü yok; Playback klasöründe "Volume" grep boş |
| Sessize alma (mute) | **YOK** | grep boş |
| Oynatma hızı değişimi | **YOK** | `DecoderPipe` sadece `-re` (gerçek zamanlı) ile çalışır, hız parametresi yok; `PlaybackClock.Rate` tanımlı ama sınıf hiç kullanılmıyor |
| Kare kare ilerleme | **YOK** | Playback klasöründe frame-step komutu yok |
| Altyazı | **YOK** | grep boş, ffmpeg argümanlarında `-vf subtitles` vb. yok |
| Ses parçası seçimi | **YOK** | `SeekAudio` sabit `-ar 48000 -ac 2`, parça seçimi parametresi yok |
| Klip dışa aktarma (kullanıcı kararıyla dosyaya kaydetme) | **YOK/KISMİ** | `SegmentEncoder.cs` sadece geçici, en fazla 2 adet (`KeepClips=2`) otomatik silinen ÖNİZLEME klipleri üretir (`TempPrefix="vidshrink_preview"`); kullanıcının seçtiği bir "dışa aktar" komutu bulunamadı |
| Bilgi paneli | **KISMİ** | `TxtState` sadece konum/oynatma durumu/zoom% gösterir (`main.player.state` = "position {0} s - {1} - zoom {2}%"); çözünürlük/codec/bit hızı gibi tam bilgi paneli yok |
| Sürükle-bırak | **KISMİ** | `MainWindow.axaml.cs:160-163` (`DragDrop.SetAllowDrop`, `OnDrop`) var ama bu ana pencere/sıkıştırma akışı seviyesinde; `_cts is null` koşuluyla sıkıştırma sürerken engelleniyor; doğrudan oynatıcı sekmesine özel bir sürükle-bırak değil |
| Son açılanlar listesi | **YOK** | grep boş |
| Kaldığı yerden devam (resume position) | **YOK** | grep boş; `SeekCoalescer`/`DecoderPipe` konum durumu kalıcı hale getirilmiyor |
| Takılma tespiti (stall watch) | **VAR** | `StallWatch` (`PlayerInputMap.cs:244-281`), `main.player.stalled` lokalizasyon anahtarı |
| Karşılaştırma/split-view oynatıcı | **VAR** | `ComparisonPanel`+`PanelHost`+`PipeComparisonFrameSource`, hstack filtre grafiği |
| Gapless segment geçişi (handover) | **VAR** | `PanelHost.cs` `BeginHandover/SwapToStandby`, `HandoverLeadSeconds=0.16` |
| Ekran görüntüsü alma | **YOK** | grep boş |

## 3. Mimarinin sınırları (yeni özellik eklerken karşılaşılacak engeller)

- **Hız değişimi (`-re` sabiti):** `DecoderPipe.StartContinuousPlayback` ve `PipeComparisonFrameSource` her ikisi de ffmpeg'i sabit `-re` (gerçek zamanlı, kaynağın kendi hızında) bayrağıyla çağırıyor. Oynatma hızını değiştirmek (0.5x/2x gibi) için mevcut mimaride ffmpeg argümanına parametrik bir `-re` alternatifi (örn. `setpts=N/PTS` filtresi + yeniden hesaplanan `-r`/`-fps_mode`) eklenmesi ve her hız değişiminde **sürecin yeniden başlatılması** gerekir — DecoderPipe'ın önbellek/yakalama mantığı sadece "aynı hızda ileri/geri seek" için tasarlı, hız çarpanını hesaba katmıyor.
- **Altyazı render'ı:** İki boru da çıkışı çıplak `rawvideo BGRA` olarak alıyor (`ComparisonGraph.BuildFilter`, `DecoderPipe`'ın rawvideo çıkışı); ffmpeg tarafında `subtitles=` filtresi eklenebilir ama bu, filtre grafiğini (hstack için zaten karmaşık olan `ComparisonGraph.BuildFilter`) değiştirmek ve her platformda libass bağımlılığının var olduğunu doğrulamak anlamına gelir — şu an hiçbir doğrulama/probe yok (yalnızca `HstackWorks` probe'u var, altyazı için karşılığı yok).
- **Ses parçası seçimi:** `SeekAudio` sabit `-ar 48000 -ac 2` ile PCM akışı üretiyor, ffmpeg'e hangi `-map 0:a:N` akışının seçileceği parametrize edilmemiş; ayrıca video ve ses **iki ayrı süreç** olduğu için akış seçimi her iki tarafta senkronize güncellenmeli (video tarafı zaten akış seçmiyor).
- **ffmpeg süreç-başlatma maliyeti:** Hem `DecoderPipe.RestartVideo` hem `PipeComparisonFrameSource.SeekAsync` seek'te **yeni ffmpeg süreci** başlatıyor; `PanelHost`'un "handover" mekanizması bu maliyeti (~105ms, koddaki yorumdan) sadece segment sonunda önceden bilinen geçişler için gizliyor — rastgele/anlık bir "hız değiştir" veya "altyazı aç/kapa" komutu için böyle bir önceden-açma stratejisi yok, her değişiklik muhtemelen görünür bir donma yaratır.
- **`PlaybackClock` ölü kod:** A/V senkronu için soyut bir saat sınıfı var ama hiç kullanılmıyor; hız veya altyazı zamanlaması gibi yeni bir zaman-bağımlı özellik eklenirken bu sınıfı diriltmek cazip görünebilir ama şu an hiçbir çağıran kod yok — sıfırdan entegre edilmesi gerekir, "kullanılıyor" diye güvenilemez.
- **Üç bağımsız oynatıcı yolu:** Tek dosya (`PlayerView`+`DecoderPipe`), karşılaştırma paneli (`PanelHost`+`PipeComparisonFrameSource`) ve önizleme sesi (`PreviewAudio`+ayrı `DecoderPipe`) birbirinden kopya kodla ayrı akışlar; herhangi bir oynatıcı-geneli özellik (örn. hız, altyazı, ses parçası) üç yerde ayrı ayrı uygulanmak zorunda — ortak bir "oynatıcı çekirdeği" abstraksiyonu yok.

## 4. Test düzeni

- Tek gerçek Avalonia çalışma zamanı: `AppHost.cs` (`tests/VidShrink.Tests/AppHost.cs`) süreç başına bir kez, **ayrı bir iş parçacığında** `AppBuilder` kurar — Windows'ta `UseWin32()` (gerçek pencere), diğer platformlarda `UseHeadless(...)` (`AppHost.Backend`). **Mock/sahte Avalonia yok** — gerçek `PlayerView`, `MainWindow`, `ComparisonPanel` nesneleri oluşturuluyor, olaylar `RaiseEvent` ile gerçek `RoutingStrategies` üzerinden gönderiliyor.
- Girdi simülasyonu gerçek olay nesneleriyle yapılıyor: `OynaticiGirdiTests.cs:44-93` `GirdiSurucu` sınıfı — `PointerWheelEventArgs`/`PointerPressedEventArgs`/`KeyEventArgs` inşa edip `view.RaiseEvent(...)` çağırıyor (mock yok, gerçek Avalonia olay yolu).
- Bazı testler **gerçek ffmpeg** kullanıyor (`[FfmpegAvailableFact]`/`[FfmpegFact]` özel attribute'ları — ffmpeg mevcut değilse testi atlıyor): `GirdiKlipFixture` (`OynaticiGirdiTests.cs:361-412`) 20 saniyelik gerçek bir test klibi üretip `DecoderPipe`'ı gerçek dosyaya karşı açıyor (`OynaticiGirdiTestsGercekBoru.OnHizliTikGercekBoruyaKarsiBirikirVeAramalarSinirdaKalir`, satır 449-492) — burada `SeekCoalescer`, gerçek `DecoderPipe.SeekAsync`'e karşı ölçülüyor, gecikme (`LatenciesMs`) 150ms sınırına karşı doğrulanıyor.
- `PanelHostTests.cs`: gerçek ffmpeg gerektirmeyen testler için **elle yazılmış sahte kaynak** var — `SessizKaynak : IComparisonFrameSource` (satır 55) arayüzü uygulayan minimal bir sınıf; `AppHost.Run(() => new PanelHost(new ComparisonPanel(), () => new SessizKaynak(), encoder))` (satır 81) deseniyle gerçek `ComparisonPanel` + sahte kaynak birleştiriliyor. Gerçek boru gerektiren testler `[FfmpegFact]` ile `PipeComparisonFrameSource` kullanıyor (satır 630).
- Hem başarı hem "kanıt dosyası" deseni tekrarlanıyor: her test `AppHost.Run(() => {...})` içinde çalışır, ölçülen değerler bir `StringBuilder`'a yazılır ve `GirdiKanit.Write("kN-ad.txt", ...)` ile `.calisma/T176/` klasörüne insan-okunabilir kanıt olarak kaydedilir (kural: "rapora giren sayı görülebilir olsun").
- Yeni bir özellik testi eklemek için izlenecek örnek desen: `OynaticiGirdiTests.TekerlekAdimlariHaritadakiDortSayidir` (satır 161-173) gibi **saf mantık testi** (mock gerektirmez, `PlayerInputMap` statik fonksiyonlarını doğrudan çağırır) VEYA `MenuDugmesiBaglamMenusunuAcarVeUcSatirTasir` (satır 256-285) gibi **gerçek `PlayerView` + `AppHost.Run`** deseni (gerçek nesne kur, gerçek olay gönder, `view.Trace` ile iç izi doğrula) — ffmpeg'e ihtiyaç yoksa ikinci desen, gerçek boru davranışı ölçülecekse `[FfmpegFact]` + `GirdiKlipFixture` deseni izlenmeli.
- Test dosyaları (yalnızca canonical `tests/VidShrink.Tests/`, `.calisma/T155-denetim/klon/` ve `.claude/worktrees/T191-audit/` altındaki kopyalar göz ardı edildi): `OynaticiGirdiTests.cs`, `OynaticiBoruTests.cs`, `PanelHostTests.cs`, `ComparisonPanelTests.cs`, `ZoomGestureTests.cs`, `SegmentEncoderTests.cs`, `PreviewSegmentTests.cs`, `AdvancedPanelTests.cs`, `CodecLockTests.cs`, `HardwareRateControlTests.cs`, `PlaybackPanelTests.cs`.
- **"SeekCoalescer" araması:** terim yalnızca `PlayerInputMap.cs` (tanım) ve `OynaticiGirdiTests.cs` (kullanım/test) içinde geçiyor — `PanelHost`/`ComparisonPanel` tarafında karşılığı yok; karşılaştırma paneli kendi debounce mekanizmasını (`SegmentEncoder`'ın 400ms'lik `DebounceMilliseconds`) ayrı olarak uyguluyor. İki oynatıcı arasında paylaşılan tek bir "arama biriktirme" bileşeni yok.

## 5. Renk/ölçü kuralı

`Themes/Theme.axaml` ölçü belirteçlerini tanımlıyor, ayrıca `Themes/Palette/*`'teki `*Color` anahtarlarını sarmalayan fırça (`SolidColorBrush`) kaynaklarını da burada topluyor:

- **Ölçü belirteçleri** (`Theme.axaml`): `SectionMargin` (satır 324, `0,16,0,0`), `SpaceMd`/`SpaceSm` (satır 301-302, `12`/`8`), `PanelPadding`, `RadiusPanel`/`RadiusPanelScalar` — oynatıcı XAML'leri (`PlayerView.axaml`, `ComparisonPanel.axaml`, `ControlStrip.axaml`) bunları `Margin`/`Spacing`/`CornerRadius` için kullanıyor.
- **Renk belirteçleri**: `AppBg` (satır 10, `AppBgColor`'a sarılı), `NeonEmber` (satır 22, `NeonEmberColor`'a sarılı) — ikisi de asıl renk değerini `Themes/Palette/` altındaki palet dosyasından alıyor, `Theme.axaml` sadece fırça sarmalayıcısı.
- **Stil/tipografi belirteçleri**: `H2` (başlık teması), `GhostButton` (menü düğmesi teması), `FontMono` (satır 287, Consolas/Cascadia Mono ailesi), `Panel` (Border teması), `MonoValue` (durum metni teması) — `PlayerView.axaml` bunların hepsini kullanıyor (satır 13,18,19,25,30,37,39).
- Oynatıcı dosyalarında elle yazılmış renk/ölçü sabiti bulunamadı; hepsi `StaticResource` üzerinden geliyor — AGENTS.md'deki "renk yalnız Palette, ölçü yalnız Theme.axaml" kuralına uygun.

---
*Bu rapor salt okuma bulgularına dayanır; hiçbir kaynak dosya değiştirilmedi.*
