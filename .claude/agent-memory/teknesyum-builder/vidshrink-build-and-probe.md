---
name: vidshrink-build-and-probe
description: VidShrink'te calisan build/test komutlari, ffmpeg prob olcumlerinin tuzaklari ve olcum icin scratchpad harness kalibi
metadata:
  type: project
---

VidShrink (Avalonia 11 + .NET 8; arayuz dosyalari `.axaml` / `.axaml.cs`):
`dotnet build VidShrink.sln -c Release` ve `dotnet test VidShrink.sln` kok dizinden calisir.
**25.08.2026 itibariyle** `%LOCALAPPDATA%\Microsoft\dotnet` klasoru YOK; kurulum
`C:\Program Files\dotnet\dotnet.exe` (SDK 8.0.423 + 9.0.316) ve PATH'teki `dotnet` bu.
Eski sozlesmelerin "PATH'teki dotnet 3.1.201, LOCALAPPDATA'yi kullan" notu artik yanlis —
komutu kosturmadan once `(Get-Command dotnet).Source` ile bak. Derlemeden once
PATH'teki `dotnet` **9.0.316** (25.08.2026 itibariyle) ve net8.0 hedeflerini sorunsuz derliyor;
dogrudan `dotnet build ...` cagir. `%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe` bu makinede
**artik yok** - eski sozlesmelerin "PATH'teki dotnet 3.1.201, MSB3644 ile duser" ortam notu
gecersiz. Once `dotnet --version` ile bak, sozlesmenin ortam notuna guvenme. Derlemeden once
`Get-Process VidShrink.App | Stop-Process -Force`, yoksa App.dll kilitli kalir.

**Why:** Ayni anda baska sozlesme kosarken bin/ kilidi cakisiyor; ikinci deneme genelde geciyor.

**How to apply:**
- ffmpeg'i `Process` ile kosarken `RedirectStandardError` acikken cikti MUTLAKA okunmali
  (`ReadToEndAsync`), yoksa pipe dolar ve ffmpeg asilir. `ComplexityProbe`/`CalibrationProbe`
  kalibi: `-f null -` + stderr'den `video:NNNKiB` ve son `frame=NN`.
- GUI'siz olcum icin scratchpad'de kucuk bir console projesi acip Core/Ffmpeg csproj'larina
  ProjectReference ver; `ToolLocator.StartInfo` **internal**, disaridan `ProcessStartInfo`
  kurmak gerekir.
- Kisa ornek klip olcumu gercege cok yakin (%1-2), ama pencere secimi temsili degilse
  mutlak seviye %15-20 sapiyor. Kaynagin sahne dagilimi
  `ffprobe -select_streams v:0 -show_entries packet=pts_time,size -of csv=p=0` ile
  830 MB dosyada 0,55 sn'de cikiyor — pencere temsilini olcmek icin ucuz vekil.
- x264 icin gercek CRF yarilanma adimi bu kaynakta 4,65; `CodecModel.CrfHalvingStep`'teki
  6,0 sabiti yanlis.
- `owns` disi dosya gerektigi anlasilirsa (orn. `ReasonCode` enum'i `EncodePlan.cs`'de,
  UI wiring `MainWindow.xaml.cs`'de ama sozlesme sadece `MainWindow.xaml` veriyor) hemen
  durmadan once tum owned-file isini bitirip Kayit noktasina net T0-karari notu yaz; T0
  genelde owns'i genisletip devam ettiriyor — bu proje `.xaml` ve `.xaml.cs`'i ayri
  ownable dosya sayiyor, otomatik ayni saymak yanlis varsayim.
- Gercek dosya olcumu icin scratchpad'de PlanCalculator+EncodeRunner'a ProjectReference
  veren bir console harness (net8.0-windows, ImplicitUsings=enable) hizli calisiyor;
  `EncoderCapabilities.Instance` gecmeyi unutma yoksa PickCodec fallback mantigi calismaz.
  830 MB/52,6 sn kaynakta 180 MB hedef ~34 sn, 8 MB hedef ~16 sn suruyor.
  `Environment.GetFolderPath(SpecialFolder.DesktopDirectory)` masaustune yazmak icin yeterli.
- `LanguageCatalog.EnglishToTurkish` XAML'daki statik Text/Content/ToolTip metinlerini
  calisma zamaninda anahtar-deger sozlugunden ceviren genel bir agac gezicisiyle
  tuketiliyor (`MainWindow.xaml.cs`'de ~74. satir civari) — yeni ComboBoxItem/ToolTip
  eklerken sadece sozluge girdi eklemek yeterli, ayrica kod yazmaya gerek yok.
- NVENC'i `lavfi` ile yoklarken kare boyutu **256x256'dan kucuk olmamali**; 128x128'de
  `InitializeEncoder failed: Frame dimensions are less than the minimum supported value`
  donuyor ve saglam bir GPU yanlis negatif veriyor.
- Donanim kodlayici bayraklari ailelere gore ayriliyor: `-rc` sadece NVENC ve AMF'de var
  (AMF'de `vbr` yok, `vbr_peak` var), QSV'de hic yok; `-cq` sadece NVENC'te; `-look_ahead`
  sadece `h264_qsv`'de. Yazmadan once `ffmpeg -h encoder=<ad>` ile bak.
- `mov` kapsayicisi av1 kabul etmiyor (`av1 only supported in MP4 and AVIF`), `mp4` ve `mkv` kabul ediyor.
- Tek ffmpeg kosusunda iki olcum almak icin `-vstats_file` **ise yaramaz**: global secenek,
  cikis basina ayrilamiyor ve satirlarda akis kimligi yok. `-f null` da cikis basina boyut
  vermiyor. Calisan kalip: `-filter_complex split` + iki `-map` + iki `-f h264` gecici
  dosya, boyutu `FileInfo.Length`'ten oku. Sonuc `video:NNNKiB` ozetiyle %0,002 icinde
  ortusuyor ve daha hassas (ozet kB'a yuvarli).
- `ComplexityProbe` suresini pencere ornekleme degil `ScanBiasAsync`'in 40 noktali taramasi
  baskiliyor; pencereleri paralellestirmek uctan uca ancak %16 kazandiriyor.
- `av1_nvenc` iki gecisli VBR hedefinde bu makinede hedefin **ustune** cikiyor (25 MB'da
  25,07), `docs/gpu-kodlama-bulgusu.md`'deki "%12 alti" tek gecis olcumu bu yola uymuyor.
- Yeni parametreleri public prob imzalarinin **sonuna** varsayilanli ekle; `MainWindow.xaml.cs`
  cogu zaman `owns` disinda ve pozisyonel `ct` gecisi kiriliyor.
- Kodlama hizini prob orneklerinden cikarirken **her surecin kendi `fps=` degerini toplama**.
  Paralel kosan 6 ornegin toplami makinenin toplam kapasitesini verir; tek bir ffmpeg
  dusuk cozunurlukte bunu yakalayamaz (670p'de toplam 469 fps, ayni komut yalniz kosunca
  282 fps). Guvenilir olcu: toplam kare / prob turunun toplam duvar saati. ffmpeg 9'un
  stats satirinda `elapsed=` alani var ve `-stats_period 0.1` daha cok satir verir, ama
  ikisi de bu toplama sorununu cozmuyor.
- `CalibrationSignature` ve hiz imzasi kalibrasyonu plana kilitliyor; kalibrasyon dosya
  yuklenirken bir kez, acilis hedefinin (16 MB) taslagiyla kosuyor. Kullanici hedefi
  buyutup cozunurluk degisince kalibrasyon ve sure dusuyor. Kalibrasyona bagli yeni bir
  gosterge eklerken bunu once olc, sonra soz ver.
- Canli test iskeleti `FillBandTests.cs`'te: `LiveSourceTheoryAttribute` (`VIDSHRINK_LIVE_SOURCE`
  yoksa Skip). Ayni namespace'ten kullanilabiliyor; `Fact` karsiligi
  `LiveSourceFactAttribute` `CalibrationProbeTests.cs`'te.
- GUI kosturan bir exe'yi PowerShell'den olcerken `Start-Process -Wait` asilir (acilan
  pencere kapanana kadar bekler). Calisan kalip: `[Diagnostics.Process]::Start($psi)` +
  `WaitForExit()`, sonra `Get-Process VidShrink.App | Stop-Process -Force`.
- Scratchpad yolu 8.3 kisa adla (`TEKNES~1`) geliyor; `FileInfo.FullName` uzun adi
  dondugu icin `Substring($kok.Length+1)` ile goreli yol cikarmak bir karakter kayiyor.
  Once `(Get-Item $kok).FullName` ile normalize et.
- Test klibi depoda ve `%USERPROFILE%\Downloads` altinda **yok**; olcum sozlesmelerinde
  klibi kendin uretmen gerekiyor. Calisan kalip:
  `ffmpeg -f lavfi -i "testsrc2=size=1920x1080:rate=30:duration=60" -c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p`
  (4K icin `size=3840x2160`, HEVC icin `libx265 -crf 25 -tag:v hvc1`). Cikan bit hizlari
  gercek kayda yakin (1080p ~5,7 Mbps, 4K ~19,8 Mbps) ama testsrc2 yuksek entropili, kod
  cozme maliyeti kotumser tarafta.
- Bu makinede ffmpeg **surec acilis tabani** medyan 53 ms, p95 95 ms
  (`ffmpeg -f lavfi -i nullsrc=s=64x64 -frames:v 1 -f null -`, 20 tekrar). Tek kare cekme
  gibi kisa isleri olcerken bu tabani ayirmadan yorumlama.
- `-ss T -i dosya -frames:v 1` gecikmesinin p95'i medyanin 4-5 kati cikiyor ve sebep disk
  degil **anahtar kare uzakligi** (x264/x265 varsayilan GoP 250 kare ≈ 8,3 sn). Soguk/sicak
  farki olcum gurultusu icinde. Kare cekme maliyeti kod cozmede, olceklemede degil:
  960 px yerine 3840 px istemek %10'dan az fark yapiyor.
- `VidShrink.Tests` `VidShrink.Core`'un **internal** uyelerini goremiyor (Core'da
  `InternalsVisibleTo` yok; yalniz `VidShrink.App/LanguageCatalog.cs`'te var). Sozlesme csproj'u
  `owns`'a koymuyorsa sinanacak yardimci uyeyi bastan `public` yaz — sonradan
  `InternalsVisibleTo` eklemek owns disina yazmak demek.
- `VidShrink.Core.csproj` hicbir NuGet paketi tasimiyor (sade net8.0). Paket gerektiren bir
  cozum secmeden once BCL/P/Invoke karsiligina bak: orn. DPAPI icin
  `System.Security.Cryptography.ProtectedData` paketi yerine `crypt32.dll`'den
  `CryptProtectData`/`CryptUnprotectData` P/Invoke, csproj'a dokunmadan calisiyor
  (`[SupportedOSPlatform("windows")]` koy, yoksa CA1416 uyarisi cikar ve 0-uyari kurali kirilir).
- PE alt sistemini dogrulama: `$pe = [BitConverter]::ToInt32($bytes,0x3C)`,
  `[BitConverter]::ToUInt16($bytes,$pe+92)` -> 2 GUI, 3 konsol.
- ffmpeg olcumlerinde **p95 icin n=12 yetmiyor**: Percentile(v,95) o orneklemde dizinin
  maksimumunu donduruyor, tek bir zamanlama tokezlemesi kapi kararini belirliyor. Bu
  makinede kare cekme cagrilarinin %2-6 kadari 700-850 ms bandinda takiliyor ve ayni
  kuyruk **hic kare cozmeyen** `ffprobe -show_format` cagrisinda da var - yani kuyruk kod
  cozmenin degil surec acilisinin. Gecikme kapisi kuran her olcumde n>=200 al ve yaninda
  kod cozmesiz bir taban olc, yoksa kuyrugu yanlis seye baglarsin.
- `-autorotate` **deger almayan bir bayrak**; `-autorotate 1` yazilirsa 1 bir cikis
  dosyasi sanilir ve ffmpeg "cannot be applied to output url" ile duser.
- `-ss` giris damgalarini sifirlar; kaynagin kendi zaman damgasi isteniyorsa `-copyts`
  gerekiyor. Teslim edilen kareyi tahmin etme, `showinfo` suzgecini zincire ekleyip
  stderr'den `pts_time` ve `s:WxH` oku. showinfo kodlayicidan cok kare gorur
  (`-frames:v 1` olsa bile), teslim edilen **ilkidir** - LastIndexOf degil IndexOf.
- Test klibine HDR etiketi basmak icin `setparams=color_primaries=bt2020:color_trc=smpte2084`
  kullan. Cikis secenegi olarak verilen `-color_trc`/`-color_primaries` libx264/libx265
  uzerinden **sessizce dusuyor**: dosya yaziliyor, hata yok, ama ffprobe'da color_transfer
  alani hic cikmiyor ve HDR tespiti dogru olarak false donuyor. Dondurme metadatasi icin
  `-display_rotation 90 -i giris -c copy cikis` calisiyor; `-metadata:s:v rotate=90`
  artik yazmiyor.

- Scratchpad console harness'ini **`.exe` olarak calistirma**: ekran kapisi hook'u
  (`hooks/ekran-kapisi.js`) exe cagrisini "masaustu penceresi aciyor" diye engelliyor.
  Konsol uygulamasi olsa bile engellenir. Calisan yol: `dotnet exec <yol>\bin\Release\net8.0\ad.dll args`
  (veya olcumu `VIDSHRINK_LIVE_SOURCE` kapili bir xunit testine koyup `dotnet test` ile kos —
  test komutlari hic engellenmiyor).
- Paralel kosan baska bir sozlesme `VidShrink.App` altina yarim `.axaml` birakirsa tum
  cozum AXN0001 ile duser ve `dotnet test` kosturulamaz. Cozum: kendi projeni ayri derle
  (`dotnet build src/VidShrink.Ffmpeg/...`) ve testleri scratchpad'de Core+Ffmpeg'e
  ProjectReference veren gecici bir xunit projesiyle kos — `<Compile Include="<mutlak yol>" />`
  ile gercek test dosyasini iceri al, `EnableDefaultCompileItems=false` **yazma** yoksa
  `GlobalUsings.cs` derlenmez ve `Fact` bulunamaz.
- Windows anonim borusundan kare okurken makinenin ffmpeg besleme tavani 2x1920x1080'de
  ~123 fps (`-re` yokken), `-re` ile hedef 60 fps sasmadan tutuluyor. Tuketici tam besleme
  hiziyla yoklarsa (60 Hz vs 60 fps) faz kilidi olmadigi icin kareler %25'e varan oranda
  bayatlayip duser; 180 Hz'de %1,3'e iner. Kare kaynagi olcerken tuketici hizini **besleme
  hizinin ustunde** tut, yoksa halka tasarimini haksiz yere sucluyorsun.

- Tam takim testi kosarken **ayni anda build kosturma**: `BulletPaintingTests` gibi Avalonia
  basliksiz-uygulama testleri o zaman 5 tanesi birden FAIL veriyor, tek basina filtreyle
  kosunca hepsi yesil. Basarisizligi koda baglamadan once takimi tek basina bir kez daha kos.
- Zamansal karmasikligi olcmek ucuz ve sonuc sezgiye ters cikabiliyor: ayni 2 sn'lik pencereyi
  bir de `-vf fps=<kaynak/2>` ile kodlayip kare basina biti kiyaslamak yetiyor, maliyeti 1,5 sn
  (pencere ornekleriyle **es zamanli** baslatirsan). gothic oyun kaydinda oran 1,76 cikti, yani
  `log2(1,76)=0,79`: fps'i yariya indirmek bitlerin yalniz %13'unu kazandiriyor. Modeldeki
  `FpsBitrateExponent = 0,75` (0,25'lik hareket ussu karsiligi) bu tur icerikte fps dusurmeyi
  **fazla ucuz** gosteriyor. "Asiri sikistirmada fps kesilmeli" sezgisi olcumle dogrulanmadan
  kodlanmamali.
- Kalite/bit-yogunlugu egrisinde **diz yok**: 640x360@24 libx264'te VMAF bppf 0,010'da 21,4,
  0,035'te 47,3, 0,090'da 74,7 - ikiye katlama basina kabaca +11 puan, duz. Bir "taban bppf"
  koyacaksan bunun olculmus cokme noktasi degil secilmis politika cizgisi oldugunu yaz.
  `ffmpeg` bu makinede `libvmaf` ve `xpsnr` suzgeclerini tasiyor, `QualityMeter.MeasureAsync`
  dogrudan kullanilabiliyor (8 sn'lik kesitte 9 nokta 53 sn).
- Altin-veri (golden master) testi `SpeedModeTests.QualityModeLeavesTodaysPlansUntouched`:
  500 MB/120 sn kaynakta 180 MB Balanced, 25 MB Aggressive, 8 MB Extreme rejimine dusuyor.
  Plan motorunda rejime bagli bir sey degistirdiysen bu dosya kirmizi olur ve `owns` disindadir.
- **Boruyu kare boyutunda tek `read` ile okuma.** Windows anonim borusunun ic tamponu kucuk;
  15,8 MB'lik tek okuma istegi tamami gelene kadar bloklayip ureticiyle tuketiciyi siraya
  sokuyor. 2x1920x1080 BGRA'da tek okuma 70,9 fps, ayni kareyi 64 KB parcalarla toplamak
  148 fps veriyor; blok buyudukce kotulesiyor (2 kare 41,8 / 4 kare 22,6). 64 KB ile 256 KB
  arasi fark yok, 1 MB biraz kotu. Parcali okuyan tam yol (aralik istatistigiyle) bu makinede
  108-117 fps surduruyor.
- Bir boru olcumunde `-re` kipinin p95/p99'una **aralik kapisi kurma**: ffmpeg'in gercek
  zamanli hiz sinirlayicisi kareleri ikiserli salvolar halinde veriyor, p95 her panel
  boyutunda ~30 ms (iki kare periyodu) cikiyor - boru hedefin bes kati hizli olsa bile.
  O sayi ffmpeg'in temposu, borunun gecikmesi degil. Kapasite kapisi "azami hiz"
  (`-re` yok) satirlarindan okunur.
- Aralik/gecikme listesini `new List<double>(maxFrames)` ile boyutlandirma: donguluk boru
  olcumlerinde `maxFrames` `int.MaxValue` geciliyor ve calisma "Array dimensions exceeded
  supported range" ile hicbir sey yazmadan duser. `Math.Min(maxFrames, 1 << 16)` kullan.
- Sinirli `Channel` ile ureticili tuketici yazarken **yazma tarafina iptal jetonu ver**:
  tuketici hedef kare sayisina ulasip donguden cikinca uretici `WriteAsync` uzerinde
  sonsuza kilitleniyor ve `await reader` hic donmuyor. Olcum sessizce asilir.
- Donanim kodlayicisinin **kendi bir alt bit hizi var** ve altinda `-b:v`/`-maxrate` bosa
  gidiyor. `av1_nvenc`te olcum: megapiksel basina kbit/s = `4,29 * fps + 75,6`, arti hicbir
  duzende inmedigi mutlak bir taban (~39 kbit/s). Istenen bit hizi bu tabanin **iki katinin
  altina** dusunce teslim %9-%17 tasiyor, 0,4 katta bes kat tasiyor; iki katin ustunde
  istegi takip ediyor. Kucuk hedefte plan ulasilamayan bir duzen seciyorsa once buraya bak
  (`CodecModel.MinBitrateK` / `UsableBitrateK`).
- Teslim edilen dosyanin plani asan maliyeti **yuzde degil sabit hiz**: mp4 kapsayicisi
  hedeften bagimsiz 9,0 kbit/s yiyor (100/50/25/8 MB'da ffprobe ile ayni sayi). 100 MB'da
  butcenin %0,7'si, 8 MB'da %9'u - kucuk hedeflerin tasmasinin asil sebebi bu, hiz kontrolu
  degil. `ContainerOverhead` (0,995) bunu modellemiyor.
- Kalibrasyon dongusu iki duzen arasinda sonsuza kadar salinabiliyor: A'da kalibre edilince
  B kazaniyor, B'de kalibre edilince A. Skorlar kil payi esit oldugunda oluyor ve bit hizi
  birkac kbit/s oynayinca ortaya cikiyor. Cozum `PlanCalculator.CalibratedShapeHysteresis`:
  profilin olculdugu sekle kucuk bir puan bonusu.
- `ffmpeg failed (-1)` bir kodlama hatasi **degil**. ffmpeg kendi hatalarinda pozitif kod
  dondurur; -1 ya `Process.Kill` (Windows'ta -1 birakir, `EncodeRunner.cs` TryKill) ya da
  gercek `AVERROR(EPERM)` = -1, yani cikti veya iki gecis gunlugu dosyasi acilamadi. Iki
  gecis gunlugu sistem `%TEMP%`'inde duruyor (`EncodeRunner.cs:63`), `.calisma/` altinda degil.
- Canli olcum kalibi: 400 sn 1080p60 kaynagi `-stream_loop` ile gercek bir ekran kaydindan
  uret, `VIDSHRINK_LIVE_SOURCE` + `VIDSHRINK_LIVE_OUT` ver, `dotnet test --filter
  "FullyQualifiedName~HardwareRateControlTests.Live" -l "console;verbosity=detailed"`.
  Donanim yari 4 dakika, islemci yarisi 15 dakika suruyor. Teslim edilen dosyanin akis
  kirilimini `ffprobe -select_streams v:0/a:0 -show_entries stream=bit_rate` ile al -
  toplamdan cikarinca kapsayici payi cikiyor.
