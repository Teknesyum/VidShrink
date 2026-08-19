---
name: vidshrink-build-and-probe
description: VidShrink'te calisan build/test komutlari, ffmpeg prob olcumlerinin tuzaklari ve olcum icin scratchpad harness kalibi
metadata:
  type: project
---

VidShrink (WPF + .NET 8): `dotnet build VidShrink.sln -c Release` ve `dotnet test VidShrink.sln`
kok dizinden calisir. Derlemeden once `Get-Process VidShrink.App | Stop-Process -Force`,
yoksa App.dll kilitli kalir.

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
