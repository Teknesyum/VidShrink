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
