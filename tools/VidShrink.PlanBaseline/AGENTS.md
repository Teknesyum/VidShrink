# VidShrink.PlanBaseline

`ManualOverrideTests.K1_VarsayilanT165OncesiMotorlaBirebirAyni` kolunun bekledigi taban
izgarasini uretir. Tek isi var: **hicbir sey sabitlenmemisken** motorun ne urettigini,
iki farkli commit'te ayni komutla yazdirmak.

`PlanOptions`ta yalniz `TargetMb` ve `Codec = Auto` kuruluyor; hicbir `Locked*` alani
kullanilmiyor. Bu yuzden tool T165 oncesi agaclarda da derlenir — tabani orada da
kosabilirsin.

## Kosum

```
dotnet run -c Release --project tools/VidShrink.PlanBaseline
```

Taban commit'inde (`9b092e9`) kosmak icin:

```
git worktree add .calisma/taban 9b092e9
cp -r tools/VidShrink.PlanBaseline .calisma/taban/tools/
dotnet run -c Release --project .calisma/taban/tools/VidShrink.PlanBaseline
git worktree remove .calisma/taban --force
```

Iki ciktiyi `diff` ile karsilastir. Fark cikiyorsa T165 varsayilan davranisi degistirmis
demektir ve K1 kirilmistir.

## Girdi

Bes bilesim `Program.Grid`de, kaynagin butun alanlari `Program.Source`ta ve
`ManualOverrideTests.Info()` ile birebir ayni: `FileSizeBytes = 500 MB`,
`TotalBitrateBps = 35_000_000`, `VideoCodec = "h264"`, `AudioCodec = "aac"`,
`AudioBitrateBps = 128_000`, `AudioChannels = 2`. Kodlayici yoklamasi
`AllWorking` — alti kodlayicinin altisi da calisir durumda.

`VidShrink.sln`e eklenmedi (`VidShrink.PresentBench` de disarida); `dotnet run --project`
ile kosuluyor.
