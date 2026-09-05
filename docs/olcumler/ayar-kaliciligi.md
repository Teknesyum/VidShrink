# T173 Ayarlar Sekmesi ve Kalicilik Olcumu

Kaynak: src/VidShrink.App/MainWindow.axaml, MainWindow.axaml.cs, AppSettings.cs.
Testler: tests/VidShrink.Tests/AyarKaliciligiTests.cs.

## K1 Denetim / anahtar / hatirlama tablosu

Kaynaktan sayim komutu:

grep -noE x:Name=(Cmb|Chk|Chip|Sldr|Slider|Txt) MainWindow.axaml
grep -n Watch( MainWindow.axaml.cs

40 adet kullanicinin degistirebildigi denetim bulundu (yonga ve gezinme dugmeleri haric,
onlar asagida ayrica listeli). Her satir kaynaktaki Watch(...) kaydiyla dogrulandi.

| Denetim | settings.json anahtari | Hatirlaniyor mu |
|---|---|---|
| SliderTarget / TxtTarget | targetMb | evet |
| SliderQualityTarget / TxtQualityTarget | qualityTarget | evet |
| CmbIntent | intent | evet |
| CmbCodec | codec | evet |
| ChkResolution | mayLowerResolution | evet |
| ChkFps | mayLowerFps | evet |
| ChkFastGpu | fastGpu | evet |
| CmbFillPolicy | fillPolicy | evet |
| CmbHdrPolicy | hdrPolicy | evet |
| CmbAdvMode | advMode | evet, T173 yeni |
| CmbAdvCrf | advCrf | evet, T173 yeni |
| CmbAdvPreset | advPreset | evet, T173 yeni |
| CmbAdvAudioKbps | advAudioKbps | evet, T173 yeni |
| CmbAdvAudioChannels | advAudioChannels | evet, T173 yeni |
| CmbAdvMinResolution | advMinResolution | evet, T173 yeni |
| CmbAdvMinFps | advMinFps | evet, T173 yeni |
| CmbAdvEncoderPath | advEncoderPath | evet, T173 yeni |
| CmbAdvCodecLock | advCodecLock | evet, T173 yeni |
| SliderQuality / TxtQuality | qualityValue | evet |
| CmbQualityMode | qualityMode | evet |
| CmbConvertCodec | convertCodec | evet |
| CmbContainer | container | evet |
| CmbResolution | resolution | evet |
| CmbConvertFps | convertFps | evet |
| CmbConvertAudio | convertAudio | evet |
| TxtCustomResolution | customResolution | evet |
| TxtCustomFps | customFps | evet |
| TxtAudioBitrate | audioBitrate | evet |
| TxtTrimStart | trimStart | evet |
| TxtTrimEnd | trimEnd | evet |
| ChkAutoUpdate | autoUpdate | evet |
| TxtDefaultTargetMb | targetMb, TxtTarget ile ayni anahtar | evet |
| CmbOutputFolderMode | outputFolderMode | evet, T173 yeni |
| TxtOutputFolder | outputFolder | evet, T173 yeni |
| ChkAdvancedDefaultOpen | advancedDefaultOpen | evet, T173 yeni |
| CmbFfmpegPathMode | ffmpegPathMode | evet, T173 yeni |
| TxtFfmpegPath | ffmpegPath | evet, T173 yeni |
| CmbShareTarget | shareTarget | evet |
| CmbShareRetention | shareRetention | evet |
| Dil anahtari, LangSwitch / SettingsLangSwitch | language | evet |
40/40 hatirlaniyor. Yonga dugmeleri (Chip8, ChipWhatsApp, Chip25, Chip100, Chip128,
Chip180, ChipHalf) ayri anahtar tasimiyor; tiklaninca TxtTarget/targetMb degerini
yazar, kalicilik o anahtar uzerinden zaten saglaniyor. BtnBrowseOutputFolder,
BtnBrowseFfmpegPath, BtnBrowseEmpty birer eylem dugmesi (dosya/klasor secici acar),
kendi basina bir tercih tasimiyor. PlanSplitter (plan paneli yuksekligi) ayri bir
duzen dosyasina yaziliyor (SaveSplitterSettings), settings.json disinda, bu
sozlesmenin kapsami disi, T163 alani.

## K2 Dokuz gelismis secim kapat/ac dogrulamasi

AyarKaliciligiTests.DokuzGelismisSecimGeriYuklenir (Theory, 9 vaka) her AppSettings
alanini ayri ayri kaydedip yeniden yukluyor:

dotnet test --filter AyarKaliciligiTests-veya-SettingsTests-veya-LanguageTests
Basarili - Basarisiz 0, Basarili 104, Atlanan 0, Toplam 104, Sure 4 s

SifirlaDugmesiDokuzGelismisKutuyuDaVarsayilanaDondurur gercek MainWindow uzerinde
RestoreAppSettingsForTest sonra ConfirmResetSettingsForTest sonra CaptureAppSettingsForTest
zincirini kosturuyor; sifirlama sonrasi dokuzu da 0a, Otomatik, donduruyor. Ayni
kosumda gecti, yukaridaki 104/104 icinde.

Gercek pencere Avalonia headless ortaminda acilamiyor, SettingsTabTests dosyasinin
kendi aciklamasi da bunu belirtiyor, o yuzden K2nin kapat/ac dogrulamasi ekran
goruntusu yerine AppHost.Run artı gercek AppSettings.Save/Load dosya round-tripi ile
yapildi; daha guclu kanit: gercek JSON dosyasi diskte yaziliyor ve okunuyor.
## K3 Ayarlar sekmesi

Eklenen denetimler ve anahtarlari: dil, mevcut, sekmeye tasindi, TxtDefaultTargetMb
varsayilan hedef boyut, CmbOutputFolderMode artı TxtOutputFolder artı
BtnBrowseOutputFolder, cikti klasoru kaynagin yani veya sabit, ChkAdvancedDefaultOpen
gelismis bolum varsayilan acik, CmbFfmpegPathMode artı TxtFfmpegPath artı
BtnBrowseFfmpegPath artı TxtFfmpegPathError, ffmpeg yolu otomatik veya elle,
gecerlilik denetimi ValidateFfmpegPath ile.

Her biri icin tek satirlik ipucu, settings-tab hint anahtarlari, eklendi; en/main.json
ve tr/main.json dosyalarinda 18 er settings-tab anahtari var, esit sayim, iki dil de
kapsiyor. Renk ve olcu yalniz Theme.axaml belirtecleri, Label, Hint, CheckStyle,
GhostButton; yeni renk/olcu uydurulmadi.

CmbOutputFolderMode ve CmbFfmpegPathMode once satirici ComboBoxItem ile eklenmisti; bu,
sekmede statik liste yasaklayan mevcut SettingsTabTests TheTargetListIsNotWrittenIntoTheMarkup
testini kirdi, bkz K7 altinda D1 notu. Duzeltme: CmbShareTarget in kullandigi kod arkasi
ItemsSource deseni izlendi; yeni RefreshOutputAndFfmpegChoiceLists metodu constructorda
ve OnLanguageChanged icinde cagriliyor. Ekran goruntusu alinamadi, headless test
ortami gercek pencere acmiyor; kanit test gecisi ve kaynak satirlariyla saglandi.

## K4 Yonga sirasi

MainWindow.axaml daki yonga sirasi: Chip8 8, ChipWhatsApp 16, Chip25 25,
Chip100 100, Chip128 128, Chip180 180, artan. x:Name degerleri degismedi.

Olcu: AyarKaliciligiTests.YongaTagDegerleriArtanSiradadir, Tag degerlerini regex ile
okuyup OrderBy ile karsilastiriyor. Raw cikti K6 altinda mutasyon b ile birlikte.
## K5 Yorum

T173 kapsamindaki iki dosyada, MainWindow.axaml ve MainWindow.axaml.cs, diffin eklenen
satirlarinda yorum sayisi sifir. Komut: git diff main icin bu iki dosyada artı satirlar
icinde iki egik cizgi veya XML yorum isareti aranir, sonuc sifir.

Kontrat, T163 gorevinin biraktigi iki XML yorumunu, CRF kilit notu ve dokuz gelismis
kalem listesi, isaret ediyordu; bu ikisi zaten daha once, T173 ten once, farkli
metinle yeniden yazilmisti. Ayni yerde, MainWindow.axaml satir 285-289, hala duran, tek
basina T163 e ait, baska goreve karismamis olu yorum, T163/K4 gelismis ayarlarin
katlama kolu, bu turda silindi.

MainWindow.axaml yorum sayisi 9 dan 8 e dustu, silinen T163/K4 tek blok.

Kalan 8 yorum baska sozlesmelere ait, T43, T46, T54, T61, T74, ikon path notu, panel
girinti notu, ve karisik coklu gorev icerik tasidiklarindan bu sozlesmenin kapsami
disinda birakildi; surgical olmayan silme, ilgisiz belge kaybina yol acar.
## K6 Mutasyon

Her mutasyondan once dotnet build -c Release --no-incremental calistirildi.

Mutasyon a: CaptureAppSettings icinde AdvCrf atamasi boxes birinci SelectedIndex yerine
sabit 0 yapildi. Kirilan olcu: SifirlaDugmesiDokuzGelismisKutuyuDaVarsayilanaDondurur,
beklenen sifirdan farkli AdvCrf degeri gelmedi, test FAIL.

Mutasyon b: MainWindow.axaml de Chip8 ve ChipWhatsApp in Tag degerleri yer degistirdi,
8 ve 16 ters. Kirilan olcu: YongaTagDegerleriArtanSiradadir, artan sira bozuldu, FAIL.

Mutasyon c: BuildLanguageSwitch icindeki iki panelli dizi tek panele indirildi,
SettingsLangSwitch dizi disinda kaldi. Kirilan olcu: AyarlarSekmesiDilDenetimiKalicidir,
kaynakta iki panelli dizi metni artik bulunamadi, FAIL.

Uc mutasyon da revert edildi, son dogrulama: dotnet test filtre AyarKaliciligiTests
Release yapilandirmasinda calisti, sonuc Basarisiz 0, Basarili 14, Toplam 14.
## K7 Kol sayisi

Birinci filtre, AyarKaliciligiTests veya SettingsTests veya LanguageTests, list-tests
ile 104 test buldu. Ikinci filtre, ChipTests veya SettingsTabTests veya ThemeTokenTests
veya WindowLayoutTests, list-tests ile 74 test buldu.

Iki kol da sifir degil. Ikinci kolun eski hali, ArayuzTests veya ThemeTests, sifir
buluyordu; T0 tarafindan bu turda duzeltildi, D1. Her iki filtre calistirilinca ayni
sayida test, 104 ve 74, yesil geldi; filtre isim eslesme sayisi ile gercek kosum
sayisi ayni.

## D4 Borc OutputFolder ve FfmpegPath boru hattina baglanmadi

VidShrink.Ffmpeg ve VidShrink.Core kaynak dosyalarinda OutputFolder ve FfmpegPath
adlarina yapilan arama sonucu bos; hic kullanim yok.

OutputFolder ve FfmpegPath bugun yalniz arayuz artı AppSettings kaliciligi
seviyesinde. Gercek sikistirma ve donusturme cagrilari hala varsayilan davranisi
kullaniyor: cikti dosyayi kaynagin yanina yaziyor, ffmpeg i sistem PATH inden veya
mevcut sabit yoldan buluyor. Bu bilincli bir sinir; VidShrink.Ffmpeg bu sozlesmenin
owns listesinde degil ve kontrat acikca cozum cekirdek veya motor degisikligi
gerektiriyorsa yazma, bildir diyor. Kullanicinin sectigi cikti klasoru veya elle
ffmpeg yolu, bir sonraki sikistirma isleminde henuz fiilen kullanilmiyor; yalniz
hatirlaniyor. Bu boru hattina baglama, ayri bir sozlesme, VidShrink.Ffmpeg owns,
gerektirir.
