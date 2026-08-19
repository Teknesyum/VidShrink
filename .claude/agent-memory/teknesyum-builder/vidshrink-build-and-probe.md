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
