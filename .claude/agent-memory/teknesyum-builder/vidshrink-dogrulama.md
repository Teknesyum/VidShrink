---
name: vidshrink-dogrulama
description: VidShrink'te canli olcum nasil tekrarlanabilir yapilir, bench araci neyi atliyor, iki gecisli libx264 isabet hatasi
metadata:
  type: project
---

Canli olcumu scratchpad programiyla yapma — `tests/VidShrink.Tests` icine ortam
degiskeniyle acilan bir `[Theory]` koy (`VIDSHRINK_LIVE_SOURCE` yoksa sessizce
`return`). Boyle bir test depoda kalir, normal `dotnet test` etkilenmez ve denetci
komutu birebir tekrarlayabilir.

**Why:** T3 round 1'de olcum scratchpad'te yapildi, denetim tekrarlanamadigi icin
bulgu acti. `tools/VidShrink.Bench` `EncodeRunner.RunAsync`'i `fillPolicy` vermeden
cagiriyor, yani FillTarget yolunu hic test etmiyor.

**How to apply:** Zincir `FfprobeClient.ProbeAsync` → `ComplexityProbe.RunAsync` →
`BuildDetailed` (taslak) → `CalibrationProbe.RunAsync` → `BuildDetailed` →
`EncodeRunner.RunAsync(..., FillPolicy.FillTarget, profile)`. Izi
`ITestOutputHelper` ile bas, `-l "console;verbosity=detailed"` ile gor.

Olculen: iki gecisli libx264 istenen boyutun ~%98,15'ini teslim ediyor (52,6 sn
1080p48 kaynak, 177 MB talep → 173,9 MB). Yani %3'ten dar bir boyut bandini tek
denemede tutturmak encoder'in kendi isabetiyle mumkun degil — assert yazarken
tavan ve sert taban iddia et, bant uyelugini logla.

Ilgili: [[vidshrink-build-and-probe]]

## Gecikme olcumunde alet secimi

Iki sayiyi (oncesi/sonrasi) karsilastiracaksan **ayni testte, ayni kosumda ve ayni
yoklama araligiyla** olc. Ayri testlerden alinan iki sayi bu projede karsilastirilamaz.

Yoklama araligi sonucu belirliyor: `await Task.Delay(1)` Windows'ta ~15 ms uyuyor ve
olcuyu kendi granulasyonuna yuvarliyor; `await Task.Yield()` dongusu ise bir cekirdegi
doldurup **olculen ffmpeg surecini ac birakiyor** ve sayiyi buyutuyor (ayni devir olcumu
78 ms yerine 102 ms verdi). Ikisi de bozuyor, farkli yonlerde. Birini sec, iki tarafta da
onu kullan ve raporda hangisini kullandigini yaz.

Ayrica: tam `dotnet test` paralel kosuyor, gecikme sayilari yuk altinda basiliyor.
Yalniz o testi kosturmakla tam takim icinde kosturmak farkli sayi veriyor (126 vs 110).
Ikisini de yaz, hangisinin rapora girdigini soyle.
