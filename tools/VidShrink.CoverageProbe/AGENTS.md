# VidShrink.CoverageProbe

`docs/olcumler/onizleme-bastan-baslama.md`'nin "gecilmeyen uretim satirlari" izgarasini
ureten duzenek. `kapsam.py` `PanelHost.cs`'in secili bir satirina
`throw new InvalidOperationException("KAPSAM <satir>")` koyar; kol dusmezse o satiri
hicbir kol gecmiyor demektir.

```
python tools/VidShrink.CoverageProbe/kapsam.py --liste
python tools/VidShrink.CoverageProbe/kapsam.py 709
dotnet build -c Release --no-incremental
dotnet test -c Release --no-build --filter "FullyQualifiedName~PlaybackResumeTests"
python tools/VidShrink.CoverageProbe/kapsam.py --geri
```

Yedek dosya tutulmaz; `--geri` `git checkout --` ile geri alir, bu yuzden `PanelHost.cs`
uzerinde kaydedilmemis degisiklikle kosturma. Her sondadan once `--no-incremental`
build sart; `--no-build` ile sonda derlenmez.

Butun satirin taranmasi gerekiyorsa sonda yerine olcum al:
`dotnet test ... --collect:"Code Coverage"` + `dotnet-coverage merge -f xml`.
