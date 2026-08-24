---
name: vidshrink-build-and-probe
description: VidShrink'te calisan build/test komutlari, ffmpeg prob olcumlerinin tuzaklari ve olcum icin scratchpad harness kalibi
metadata:
  type: project
---

VidShrink (Avalonia 11 + .NET 8; arayuz dosyalari `.axaml` / `.axaml.cs`):
`dotnet build VidShrink.sln -c Release` ve `dotnet test VidShrink.sln` kok dizinden calisir.
PATH'teki `dotnet` 3.1.201 ve MSB3644 ile duser; `$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"`
kurup `& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"` cagir. Derlemeden once
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

