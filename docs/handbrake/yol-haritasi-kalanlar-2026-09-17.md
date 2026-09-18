# Yol Haritası Kalanları (17 Eylül 2026)

Denetlenen ağaç: `.calisma/kurucu-kilit`, HEAD **0bc86188** (eski denetim 5be9290e'yi okumuştu; aradan 146 commit girdi).

Salt okuma yapıldı: uygulama açılmadı, test koşulmadı. "Test" sütunundaki adlar kaynaktan okundu, yeşil oldukları bu belgede doğrulanmadı.

Test türü kısaltmaları:
- **(d)** Davranış ölçüyor.
- **(m)** Kaynak metin ya da sabit okuyor.
- **(p)** Piksel ya da gerçek pencere okuyor.

## Yeni Sayım

| | Yapıldı | Kısmen | Yok | Kararla kapandı | Toplam |
|---|---|---|---|---|---|
| Eski denetim | 67 | 30 | 10 | 2 (sapmış) | 109 |
| Yeniden bakılan 42 madde | 23 | 15 | 3 | 1 | 42 |
| **Bugün** | **90** | **15** | **3** | **1** | **109** |

- Sapmış sayılan iki madde yeniden sınıflandı: Döndür "kısmen" oldu, K11 "yapıldı".
- 67 yapıldı maddeden kanıtı zayıf olan 10'u aşağıda ayrıca işaretli. Sayımdan düşülmediler.

---

## 1. Oynatıcı (9 Madde)

| Madde | Eski | Bugün | Kanıt | Eksik olan |
|---|---|---|---|---|
| Döndür (tuş) | Sapmış | **Kısmen** | Dönme artık vf ile karede: `src/VidShrink.Player/MpvEngine.cs:429-440` (`@vsrotate`, `transpose`), 0d983b71. Ham klavye: `OynaticiOdakYoluTests.AnaPencereOdakYolundanTuslarMotoraUlasir` (d). | Tuş hâlâ Ctrl+Shift+S (`Playback/Keymap.cs:141`), kullanıcının bulamadığı tuş aynı. Ham klavye testi `motor.Rotation`'a bakıyor, pikseli okumuyor. Kullanıcı kurulumunda doğrulanmadı. |
| P29 Döndürme gerçekten döner | Kısmen | **Yapıldı** | `OynaticiKisayolTests.KisayolMotoraUlasirVeGeriOkunur("CtrlShiftS")` (p): 0/90/180/270'te kare boyutu ve kırmızı/mavi kenar. `OynaticiGorunumTests.DondurmeAynalamaVeOranMotorOzelligineYazilirVeGeriOkunur` (d): gerçek mpv karesi 180x320. | Tuş `RaiseEvent` ile veriliyor, ham girdiyle değil. |
| P2 Pencere sürüklemede ortaya mıknatıs | Kısmen | **Kısmen** | `Playback/PlayerView.Fare.cs:172-187` `CenterSnap`; `OynaticiYolHaritasiTests.P2PencereEkranOrtasinaYaklasincaYapisir` (d). | Mıknatıs sürükleme sırasında değil, `BeginMoveDrag` döndükten sonra, bırakınca bir kez uygulanıyor (`PlayerView.Fare.cs:168-169`). Test sürükleme yolundan geçmiyor. |
| P3 Sağ tık menüsünde tüm ayarlar | Kısmen | **Kısmen** | Alt menü: dil, tema, ekran görüntüsü klasörü, Gelişmiş, Tüm ayarlar (`MainWindow.axaml.cs:923-929`, `Playback/PlayerView.axaml.cs:430-449`). `P3SagTikAyarlarAltMenusuUygulamaAyarlariniTasir` (d). | "Tüm ayarlar" hâlâ sekmeye götürüyor. Ayarlar sekmesinin geri kalanı menüde yok. |
| P12 −10/+10 yazılı | Kısmen | **Yapıldı** | `Playback/PlayerView.Serit.cs:258-259`; `P12OnSaniyeDugmeleriAtladigiSayiyiYaziyor` (d): yazı görünür, atlanan süre yazıdaki sayıya eşit. | — |
| P14 Alt bar üst barla aynı kural | Kısmen | **Kısmen** | Gecikme ve duraklatma kuralı ortak (`MainWindow.axaml.cs:644,652`); `P14UstBarAltBarlaAyniKurallaGizlenir` (d, elle saat, 9 adım). | Açılma eşiği farklı: üst bar `TitleBar.Height` pikseli (`MainWindow.axaml.cs:656`), alt bar 0,25 oranı (`Themes/Playback.axaml:122`). Test eşiği ölçmüyor. |
| P19 Duraklatma simgesi 0,5 sn | Kısmen | **Yapıldı** | Kalış 500 ms, giriş/çıkış 160 ms (`Themes/Theme.axaml:252`, `Playback/PlayerView.axaml.cs:528-559`). `P19DuraklatmaSimgesiYarimSaniyedeGirerVeCikar` (d): gerçek süre 470-590 ms, ara saydamlık, ortalı. | — |
| P26 Fareye yakın yerden yayılarak açılma | Yok | **Yapıldı** | `Playback/PlayerView.Serit.cs:191-234`; `P26SeritFareyeYakinKisimdanYayilarakAcilir` (d, maske değerleri). | Piksel okunmuyor. Son beş commit P26 zamanlamasını düzeltiyor (4bf2885a, 82e8011a, 48a41982), test oynak geçmişli. |
| P28 GOM paritesi | Kısmen | **Kısmen** | Yandaki altyazılar kendiliğinden yükleniyor: `P28YanindakiAltyazilarKendiligindenYuklenir` (d, sahte ve gerçek libmpv). Gelişmiş panel: `OynaticiGelismisTests.GoruntuAyariKareyiDegistirirAltyaziAyariDegistirmez` (p). | Altyazı **indirme** yok. `src` ve `tests` altında opensubtitles / subdl / indir araması boş. |

## 2. Kaydedici (14 Madde)

**Platform notu:** genel kısayol, tıklama/tuş kancası, pencere listesi, çerçeve, büyüteç ve webcam yalnız Windows'ta çalışıyor.
- `RecorderView.Kisayol.cs:14-17`, `RecorderView.Girdi.cs:15`, `RecorderWindows.cs:26`, `RecorderInput.cs:226`.
- Diğer sistemlerde bu özellikler sessizce hiçbir şey yapmıyor.

Yollar `src/VidShrink.App/Recorder/` altında.

| Madde | Eski | Bugün | Kanıt | Eksik olan |
|---|---|---|---|---|
| R1 Fareyle bölge çizme | Yok | **Yapıldı** | `RecorderRegionPicker.axaml.cs`, `RegionDraw.cs`, `RecorderView.Secici.cs:14-23`. Testler: `KaydediciSeciciTests.FareyleCizilenBolgeAyaraVeArgumanaGecer`, `OranKilidiSurukleyiOranaUydururVeEkrandaTutar`, `CizimPenceresiMasaustunuKaplarVeEscVazgecer` (d). | — |
| R2 Pencere yakalama | Kısmen | **Kısmen** | Windows seçici `RecorderWindows.cs:31` (EnumWindows); `KaydediciSeciciTests.PencereSeciciBasligiIstegeYazar`, `PencereListesiGorunmeyeniKendiniVeBosuEler` (d). | macOS'ta motor tek pencereyi reddediyor (`src/VidShrink.Core/RecorderArguments.cs:849`). Linux motoru `WindowId` alıyor ama arayüzde seçici yok. |
| R3 Tam ekranda üstte kayıt çerçevesi | Yok | **Yapıldı** (Windows) | `RecorderView.Cerceve.cs:18-25` üç hedefte bölge buluyor. `KaydediciCerceveTests.CerceveUcHedefteDeBolgeBulur` (d), `TamEkranCercevesiKaydaGirmez` (p). | `KaydediciArayuzTests.CerceveYalnizBolgeKaydindaVeGizlenmemisseIstenir` adı eskide kalmış. |
| R4 Tepside geri bildirim | Yok | **Yapıldı** | `RecorderTray.cs`, `RecorderView.Tepsi.cs:39-40`. `KaydediciArayuzTests.TepsiDurumuOturumdanOkunur`, `TepsiIpucundaAnlikBoyutVeSureVar` (d, sahte tepsi). | — |
| R5 Genel kısayol | Kısmen | **Yapıldı** (Windows) | `RecorderHotkeys.cs:34-38,95` (RegisterHotKey F7-F11), F10 iptal `RecorderView.Kisayol.cs:54`. `KaydediciArayuzTests.GenelKisayolBasilincaEylemCalisir`, `GenelKisayolCakismasiKullaniciyaSoylenir` (d, sahte kayıtçı). | Gerçek RegisterHotKey çağrısını ölçen test yok. |
| R6 Tıklama gösterme ve ses | Yok | **Yapıldı** (Windows) | `RecorderInput.cs`, `RecorderInputOverlay.cs:47`. `KaydediciGirdiTests.GercekHalkaVeTusYazisiYerindeAcilipKendiligindenKapanir` (p), `TiklamaSesiGecerliDalgaDosyasi`. Ölçüm: `docs/olcumler/kaydedici-piksel.md`. | Ses için yalnız `PlaySoundW` dönüşü ölçülmüş. |
| R7 Tuş gösterimi | Yok | **Yapıldı** (Windows) | `RecorderInputOverlay.cs:66` `RecorderKeyCaption`. `KaydediciGirdiTests.TusYazisiDegistiricilerleBicimlenir` + gerçek pencere testi (yukarıda). | — |
| R8 Program en iyi ayarı seçer | Kısmen | **Yapıldı** | `RecorderView.axaml.cs:72`, `RecorderView.Otomatik.cs:180-182`. `KaydediciAyarTests.AcilistaOtomatikKipBirKezOlcerElleVeBassizdaOlcmez` (d). | — |
| R9 İsteğe bağlı hedef süre/MB | Kısmen | **Yapıldı** | `KaydediciHedefTests.TekHedefKutusuKendiSinirinaGecer`, `BoyutSiniriDolunacaKayitKendiBiter`. Kendiliğinden biten kayıt Stopped oluyor: `src/VidShrink.Ffmpeg/RecorderSession.cs:514-525`, `KayitMotoruTests.SureSiniriDoluncaOturumKendiBiterVeElleDurdurmaBitisSayilmaz` (d, gerçek ffmpeg). | — |
| R10 Klasörü aç + Paylaş vurgulu | Kısmen | **Kısmen** | Bitince klasör: `RecorderView.Serit.cs:245`; `KaydediciHedefTests.KayitBitinceKlasorYalnizKutuAcikkenAcilir` (d). | Kutu varsayılan olarak kapalı (`RecorderSettings.cs:235`). "Klasörü göster" ve "Paylaş" ikisi de PrimaryButton (`RecorderView.axaml:187-188, 219-220`), vurgu birbirinden ayrışmıyor. |
| R11 Mini arayüzden ayar | Kısmen | **Yapıldı** | `RecorderMini.axaml:58-83` altı ayar, `RecorderView.Mini.cs:157`. `KaydediciGirdiTests.MiniAyarlarBuyukPencereyeVeDosyayaGecer`, `MiniAyarKayitSurerkenKancayiYenilerImlecKilitli` (d). | Kodek, hedef ve bölge mini pencerede yok. Plan bunları bilerek dışarıda bırakıyor. |
| R12 Bandicam/OBS düzeyi | Kısmen | **Kısmen** | Var olanlar ve testleri:<br>- geri sayım: `KaydediciArayuzTests.GeriSayimSeritteSayilirVeSonundaBaslatir`<br>- büyüteç: `KaydediciKameraTests.GercekBuyutecPenceresiImleciIzlerVeEkraniKopyalar`<br>- webcam: `GercekKameraKaydinKosesineBinerKarsiKoseBosKalir`<br>- GIF: `KayitFfmpegKoluTests.GifKabiUzantisiniVerirVeMatroskayaYakalar`<br>- tampon: `KaydediciTamponTests.CanliTamponSonSaniyeleriKaydederEskiParcalarSarilir`<br>- canlı önizleme: `KaydediciOnizlemeTests.CanliKayitOnizlemeKaresiniYazarOnizlemesizYazmaz`<br>- boşluk kırpma: `BoslukKirpmaTests`<br>- arka plan: `KaydediciArkaPlanTests.AnahtarlananKameraKaresiPikseldeArkaPlaniBirakir` (p)<br>- bölme: `KayitBolmeTests` | - Arka plan ayırma modelsiz: yalnız `backgroundkey`/`chromakey` (fable kararı, `docs/plan.md` Paket 2b md. 3).<br>- Kayıt sürerken yükleme yok (fable kararı, md. 6).<br>- macOS/Linux'ta Windows'a özgü özellikler çalışmıyor (bkz. R2 ve platform notu).<br>- `docs/plan-kaydedici-dalgalari.md:117-119` durum sütunu bayat: sıcak tuş, GIF ve gizlenme hâlâ "yok" yazıyor. |
| R13 50+ repo araştırması ve plan | Kısmen | **Yapıldı** | 69 proje (`docs/plan-kaydedici-dalgalari.md:13`, `docs/arastirma/kaydedici-*.md`). Planın 21 kaleminin 19'u tam, 2'si (C7, C8) kararla daraltılmış. | A dalgası testleri (`MiniKipTests`) çoğunlukla (m). |
| R16 Kaydedici ayarları çalışsın | Kısmen | **Yapıldı** | 20 alanın 20'si arayüze bağlı: ScreenIndex `RecorderView.Hedef.cs:177,289` (`CmbScreen`), diğer 19 alan `RecorderView.Gelismis.cs:35-46`. Testler: `KaydediciAyarTests.HerAyarBaslatmadanDosyayaVeSonrakiKaydinArgumaninaGecer`, `HerAyarinEtkisiCiktidaFfprobeIleGorunur` (d), `KaydediciAyarGidisDonusTests.HerAyarDiskeGidipAyniDonerDegisir`. | — |

## 3. Küçült ve Ayarlar (8 Madde)

| Madde | Eski | Bugün | Kanıt | Eksik olan |
|---|---|---|---|---|
| S9 Sıfırla + metinler Locales'ten | Kısmen | **Kısmen** | Sıfırlama tamam: 5491bdc5. `VeriSifirlamaTests.SifirlamaHerVeriDosyasiniSilerBaskasinaDokunmaz`, `AyarlardakiOnayDugmesiOynaticiVeKaydediciVerisiniDeSiler` (d). | Sabit metinler duruyor: `MainWindow.axaml:286` `Text="MB"`, `:369` `Text="/100"`, `:867` `Content="CRF"`, `MainWindow.axaml.cs:3416` `$"CRF {plan.Crf}"`. |
| S10 Sabit çözünürlük, WhatsApp işareti | Kısmen | **Yapıldı** | 0387dfa3. `KucultSabitSecimTests.DinamikKutusuKalkincaSabitBoyPlanaGider`, `WhatsAppUyumuKodegiH264eKilitler`, `KayitliSecimGeriYuklenincePlanVeSatirlarUyar` (d). | — |
| S12 Opus ses | Yok | **Kararla kapandı** | `docs/plan.md` İş 14 md. 6: MP4'te WhatsApp/iOS uyumu bozulduğu için uygulanmıyor. Soru `.calisma/kucult-kabuk/soru-opus.md`'de. "İzleri koru" (MKV) yolunda Opus var: `src/VidShrink.Core/StreamMapping.cs:238`. | Kullanıcı onayı belgede görünmüyor; karar fable/T0'da. |
| S13 %3 taşmada seçenekler | Kısmen | **Yapıldı** | 34b440ea. `TasmaKarariTests.KesimYalnizYuzdeUcIcindekiTasmadaOnerilir`, `SonDenemedeDeSorulurVeBuyukSonucKabulEdilirseTeslimEdilir`, `TasmaSorusundaKesmeSecilinceSonucHedefeIner`, `KesilenDosyaGercektenHedefinAltindaKalir` (d, gerçek dosya). | — |
| S14 2 seçenekli combobox yerine düğme | Kısmen | **Kısmen** | f2e75ab1. `AyarRadyoSeridiTests.GenelAyarlardaIkiSecenekliAcilirListeKalmaz`, `RadyoSecimiSeciciyiAcarKaydedilirVeGeriYuklenir` (d). | Paylaşım panelindeki `CmbShareTarget` (`MainWindow.axaml:1278`) iki hedefle (storage.to, uguu.se) hâlâ açılır liste. Test yalnız GeneralSettingsPanel'e bakıyor, bunu yakalamıyor. |
| S16 Rozet yalnız "CRF n", etiketler üstte | Kısmen | **Yapıldı** | `Playback/PanelHost.cs:210` `$"CRF {crf}"`. `ComparisonPanelTests.Taraf_etiketleri_panonun_ustunde_solda_ve_sagda_durur` (d), `PanelHostTests.Rozet_yaklasik_alanindan_surulur` (d, rozet tam "CRF n"). | — |
| S19 Küçült'te Paylaş | Kısmen | **Yapıldı** | ShrinkJobWindow'da Paylaş var (60bcf5d4). `KucultPaylasTests.KucultIsPenceresindePaylasDugmesiIsDurumunuGercektenTakipEdiyor`, `AnaPencereninPaylasDugmesiIsBittiktenSonraGercektenGorunurVeTiklanabilir` (d). | Tasarım (c): ana sekmede düğme iş bitmeden görünmüyor (`KucultPaylasTests.cs:174`, `MainWindow.axaml.cs:1938-1941`). Paylaşım sistem paylaşımı değil, web yüklemesi. Kullanıcının "neden yok" sorusuna bu tasarım cevabı ona söylenmedi. |
| S20 Kesik "Hedefi Do", sekme-panel boşluğu | Kısmen | **Kısmen** | "Hedefi doldur" artık radyo (`MainWindow.axaml:407`). `WindowLayoutTests.NoTurkishBoxLabelIsClipped` yalnız ComboBox/Button ölçüyor; `TheSectionInsetStaysOnTheSpacingScale` (m). | Ekranda doğrulama (başsız ölçüm, TR ve EN) gerekiyor. |

## 4. Kabuk, Güncelleme, Açılış (8 Madde)

| Madde | Eski | Bugün | Kanıt | Eksik olan |
|---|---|---|---|---|
| K4 Eski ayar simgesi, Teknesyum `<>` | Kısmen | **Kısmen** | Dişli geri geldi: `Themes/Icons.axaml:13`, `KabukAciklariTests.AyarlarSimgesiDisliCarktir`. | Dişli yeni çizim, 06f2112b/bf3b2156'daki eski çizimin aynısı değil. Teknesyum düğmesi hâlâ `IconCode` (atom, `MainWindow.axaml:1393`); `<>` hâli bf3b2156'da kaldırılmış, geri dönmemiş. |
| K10 İndirmede panel kapanmasın, iptal | Kısmen | **Yapıldı** | `MainWindow.Guncelleme.cs:46-71,84-89,110-113` (`CancellationTokenSource`, `CancelUpdateDownload`). `KabukAciklariTests.IndirmeSurerkenGuncellemePaneliKapanmaz` (d). | 4 MiB/s sınırı ve dolum patlaması ölçülmedi (`.calisma/eksikler/rapor.md` 2-3). |
| K11 Güncelleme varsayılanı düğme | Sapmış | **Yapıldı** | `src/VidShrink.Core/UpdateCheck.cs:416` (`AutoUpdate` başlangıç değeri yok, yani false). `UpdaterTests.AutoUpdateIsOffUntilTheUserTurnsItOn`, `AnExistingUsersSavedChoiceSurvivesTheNewDefault` (d). | Otomatik ayar açıkken rozet gizleniyor (`MainWindow.axaml.cs:2155-2158`); tasarım gereği, ama elle açan kullanıcı akışı görmüyor. |
| K14 "VidShrink açılıyor" paneli çıkmasın | Kısmen | **Kısmen** | Çift tık başlatıcıyı atlıyor (98b77bea). Panel 400 ms eşikli (`src/VidShrink.Launcher/Program.cs:101-105`). | Eşik olağan açılışta da kurulu ("bekleyen dosyaların taşınması"). Bakım 400 ms'yi aşarsa ya da elle güncellemede panel hâlâ çıkıyor. Test kaynak metne bakıyor: `SplashTests.ThresholdIsFourHundredMillisecondsAndGuardsEveryDraw` (m). Kullanıcı üç kez "olmasın" dedi. |
| K15 Hipersürüş ~100 ms | Kısmen | **Kısmen** | `docs/olcumler/hipersurus-h.md:140`: dosyayla açılışta ilk kare 730 → **595 ms** (10/10, composite R2R). Tahmini tavan ≈200 ms (`:190`, AOT + yazılım kare yolu). | Hedefin 6 katı. B2/B3/B4 ve AOT açık. Kullanıcının makinesinde gerçek kurulumla ölçüm yok. |
| K19 Editör sekmesi | Kısmen | **Yok** | `src` altında Editor/Düzenleyici sınıfı ya da sekmesi yok. Yalnız `docs/plan-duzenleyici.md` var. | Eski denetim "kısmen" demişti; kod yok, doğrusu "yok". |
| K20 Üst bar ve panel "pp" standardına | Yok | **Yok** | Depoda iz yok. Standart depo dışında (`~/.claude` pp rafı). | Doğrulanamaz. Raf içeriği gerekiyor. |
| K21 Hakkında'da tüm platformlar | Yok | **Yapıldı** | `MainWindow.axaml.cs:177` `RefreshPlatforms`. `KabukAciklariTests.HakkindaYayinlananHerPlatformuYazarVeBuKurulumuIsaretler` (d). | — |

## 5. Yol Haritası Açıkları (3 Madde)

| Madde | Eski | Bugün | Kanıt | Eksik olan |
|---|---|---|---|---|
| WhatsApp'a özel azami kalite | Kısmen | **Yapıldı** (ölçümle kapandı) | `docs/YOL-HARITASI.md:10` `[x]`. `docs/olcumler/whatsapp-karanlik.md`: `aq-mode=3` karanlık PSNR'ı −0,01 ile +0,07 dB oynattı, kod değişmedi. | Ayrı kip yok, hüküm "kazanç yok". Üründe eklenen tek şey H.264'e kilitleyen WhatsApp kutusu. Bu satır "kısmen" de okunabilir. |
| Simgeler Fluent dolgu diline | Yok | **Yok** | `docs/YOL-HARITASI.md:12` `[ ]`, "karar kullanıcının". | Kullanıcı kararı. |
| HandBrake algıda da geçilsin | Kısmen | **Kısmen** | Karanlık kaynakta Otomatik artık libx265 (b9dc1ec4). `docs/olcumler/karanlik-x265.md:78-85`:<br>- 600 kbit: ürün VMAF-NEG 77,93 / HB 76,83; XPSNR 35,83 / 35,43; CAMBI 6,74 / 6,52<br>- 2000 kbit: VMAF-NEG 95,68 / 95,18; XPSNR 39,67 / 39,48; CAMBI 6,61 / 6,56 | - CAMBI'de HB hâlâ biraz önde.<br>- Toplam süre HB'nin 1,7-1,9 katı (`:89`).<br>- 600 kbit'te ürün 1574x670'e iniyor, HB 1920x818'de kalıyor.<br>- `docs/YOL-HARITASI.md:13-19` maddesi eski sayılarla açık duruyor.<br>- SVT yazılım yolunda hiçbir kol CAMBI kapısını (7,5) geçmedi, değerler ≈9,0-9,4 (`handbrake-kiyas-hb2b.md:51-58`).<br>- NVENC hevc HB açığı ortalama −0,22 VMAF-NEG; gop10 ve tam kare kapıları kaldı (`nvenc-gop10.md`, `nvenc-tamkare.md`). |

---

## 6. Yapıldı Sayılan 67'den Kanıtı Zayıf 10 Madde

| Madde | Neden zayıf |
|---|---|
| P1 Sol tık oynat/duraklat | Sabit 500 ms çift tık beklemesi (`Playback/PlayerInputMap.cs:270`), sistemin `GetDoubleClickTime` değeri okunmuyor. `OynaticiGirdiTests.TekTikDuraklatirCiftTikTamEkranaGecerVeIkisiCakismaz` gerçek zamanlayıcıyı değil, elle verilen zamanı sınıyor. |
| P6 Çubuğa tıklayınca hızlı atlama | Yalnız atlama hedefi ölçülüyor (`OynaticiGorunumTests.cs:666-674`). "Hızlı" iddiasının ölçümü yok. |
| P17 Ses 5, hız 0,05 adımı | Klavye hız adımı 0,05 değil 0,1 (`Playback/Keymap.cs:53`). Testler 1,1/0,9 bekliyor (`OynaticiKisayolTests.cs:420-421`). Çubuk adımını ölçen test yok. |
| P22 Ses/hız çubukları yeniden tasarım | Bu madde adına test ya da ölçüm yok. Kanıt yalnız stil dosyası (`Themes/Playback.axaml:424-495`). |
| P20 Oynatıcıda pencere anahattı yok | `P20OynaticiSekmesindePencereAnahattiYok` pikseli değil `BorderThickness` değerini okuyor (m'ye yakın). |
| Karıştır / tekrar kısayolu | Hâlâ Ctrl+Alt+Shift+F / B (`Playback/Keymap.cs:149-150`). Eski denetimdeki "pratikte basılmıyor" notu duruyor. |
| R14 Kayıttan sonra Küçült'e gönder | Yol haritası "birinci düğme" diyor. `BtnToShrink` GhostButton, birinciler `BtnReveal` ve `BtnRecShare` (`RecorderView.axaml:187-196,219`). Test `KayitTeslimTests.SonucPanelindeDortKapiVar` (m). |
| R15 Kayıttan sonra Paylaş | Web yüklemesi (`RecorderView.Paylas.cs:14-25`), sistem paylaşımı değil. `KayitTeslimTests.KayitPaylasimiAyniKatmandanGeciyor` (m). |
| S21 İki video senkron | Oynarken kayma düzeltmesi hâlâ yok (alt ajan yeniden baktı). |
| K12 Sağ tık "VidShrink ile Aç" dili | Menü metni hâlâ yalnız TR/EN (`src/VidShrink.Core/Setup/ShellRegistration.cs:39-50`), öbür 41 dil yok. |

---

## 7. Kalan İşler (Sıralı)

"Görünür" sütunu, işin kullanıcının gördüğü davranışı değiştirip değiştirmediğini söyler.

| # | Kalem | Dokunacağı dosyalar | Görünür | Grup |
|---|---|---|---|---|
| 1 | Döndürme: bulunabilir tuş (ipucu/menüde tuş adı, gerekiyorsa GOM'daki karşılığı), ham klavyeyle piksel testi | `Playback/Keymap.cs`, `Playback/PlayerView.axaml.cs` (menü ipucu), `tests/.../OynaticiOdakYoluTests.cs` | Evet | A |
| 2 | "VidShrink açılıyor" paneli: olağan açılışta eşiği kaldır, bakımı panelsiz arka plana al | `src/VidShrink.Launcher/Program.cs:101-112`, `Launcher/Splash.cs`, `tests/.../HipersurusTests.cs` | Evet | D |
| 3 | P14 açılma eşiğini iki barda aynı ölçüye bağla | `MainWindow.axaml.cs:656`, `Themes/Playback.axaml:122`, `OynaticiYolHaritasiTests.cs` | Evet | B |
| 4 | P3 menüde ayarlar: "Tüm ayarlar" sekmeye gitmesin, kalan ayarlar alt menüde | `Playback/PlayerView.axaml.cs:430-449`, `MainWindow.axaml.cs:923-929` | Evet | B (3'ten sonra) |
| 5 | S9 sabit metinler ("MB", "/100", "CRF") Locales'e | `MainWindow.axaml:286,369,867`, `MainWindow.axaml.cs:3416`, `Locales/*/main.json`, `BiciminTests` pinleri | Evet (dile göre) | B (4'ten sonra) |
| 6 | K4 Teknesyum `<>` simgesi | `Themes/Icons.axaml:33`, `MainWindow.axaml:1393` | Evet | B (5'ten sonra) |
| 7 | P2 mıknatıs sürükleme sırasında (BeginMoveDrag yerine kendi taşıma döngüsü) | `Playback/PlayerView.Fare.cs:156-187` | Evet | A (1'den bağımsız dosya, paralel olabilir) |
| 8 | P17 hız adımı 0,05, P1 çift tık süresi sistemden | `Playback/Keymap.cs:53`, `Playback/PlayerInputMap.cs:270`, `OynaticiKisayolTests.cs` | Evet | A (1'den sonra, Keymap ortak) |
| 9 | R10 vurgu ayrımı ve R14 "Küçült'e gönder" birinci düğme | `Recorder/RecorderView.axaml:187-220`, `KayitTeslimTests.cs` | Evet | C |
| 10 | R2 macOS/Linux pencere kaydı ve seçici | `src/VidShrink.Core/RecorderArguments.cs:849`, `Recorder/RecorderWindows.cs` | Evet (mac/Linux) | C (9'dan bağımsız) |
| 11 | Kaydedici plan belgesinin durum sütunu | `docs/plan-kaydedici-dalgalari.md:112-119` | Hayır | C |
| 12 | S20 kesik metin ve boşluğun başsız ölçümü (TR ve EN) | `tests/.../BiciminTests.cs` ya da yeni ölçü testi, gerekiyorsa `Themes/Theme.axaml` | Hayır (düzeltme çıkarsa evet) | E |
| 13 | S14 paylaşım hedefi açılır listesini radyo şeridine çevir, testi tüm Ayarlar sekmesine genişlet | `MainWindow.axaml:1278`, `MainWindow.axaml.cs` (paylaşım paneli), `AyarRadyoSeridiTests.cs` | Evet | B (6'dan sonra) |
| 14 | HandBrake: CAMBI farkı, karanlıkta toplam süre, donanım yolu kıyası; YOL-HARITASI sayılarını güncelle | `src/VidShrink.Core` kodlayıcı argümanları, `tools/kalite-paketi-3/hb.ps1`, `docs/YOL-HARITASI.md:13-19` | Kısmen (kalite/süre) | F (CI'da ölçülür) |
| 15 | K15 hipersürüş B2/B3/B4 + AOT (595 → ~200 ms tavan) | `src/VidShrink.Player/*`, `Playback/PlayerView*`, `VidShrink.App.csproj` | Evet (hız) | G |
| 16 | P28 altyazı indirme | Yeni `Playback/` altyazı sağlayıcı, `Playback/PlayerView.Tracks.cs`, `Locales` | Evet | Karar: servis seçimi, API anahtarı |
| 17 | R12 modelli arka plan ayırma, kayıt sürerken yükleme | `Recorder/RecorderView.Kamera.cs`, `Core/Share` | Evet | Karar: fable "hayır" dedi, yeniden açmak kullanıcının |
| 18 | K19 Editör sekmesi | Yeni modül, `docs/plan-duzenleyici.md` | Evet | Karar + L boy |
| 19 | K20 pp standardı, Fluent simgeler, S12 Opus onayı | pp rafı (depo dışı), `Themes/Icons.axaml` | Evet | Karar |

**Paralel gruplar.** A, B, C, D, E, F ve G birbirinin dosyasına dokunmuyor ve aynı anda koşabilir. Grup içindeki sıra:
- **A:** 1 → 8 (Keymap ortak). 7 ayrı dosyada, paralel koşabilir.
- **B:** 3 → 4 → 5 → 6 → 13. Hepsi `MainWindow.axaml(.cs)` üstünde.
- **E:** yalnız 12.
- **C:** 9, 10 ve 11 ayrı dosyalarda.

Kalan kalem sayısı: kısmen 15 + yok 3 = **18 madde**. Bunlar ve zayıf kanıtlardan doğan işler 19 satıra toplandı; 5 satır kullanıcı kararı bekliyor.
