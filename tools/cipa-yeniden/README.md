# cipa-yeniden — T116 olcum duzenegi

T110 kare kilidini urun olcerine (`src/VidShrink.Ffmpeg/QualityMeter.cs`) koydu.
Bu klasor, kilit oncesi konmus cipalarin ne kadar yanlis oldugunu ve kilidin
plani degistirip degistirmedigini olcen duzenegi tasir.

## Program

`CipaYeniden <kaynak> <hedefMb[,hedefMb...]>` — kaynagi `ComplexityProbe` ile
yoklar (`measureQuality: true`, olcer `QualityMeasurement.Instance`), pencere
basina VMAF-NEG cipalarini **tam duyarlikla** basar, `WithProbeQuality` ile
toplanan `QualityAnchor`i ve her hedef icin `PlanCalculator.BuildDetailed`
sonucunu JSON olarak yazar.

`OlcerKilidi` alani `MeasureFilterGraph.Build("null","null","libvmaf")` ciktisidir:
calisan ikilinin kilitli mi kilitsiz mi oldugunu ciktinin kendisi soyler.

Bench'in `--measured-quality` cikti satiri cipalari iki basamaga yuvarlar; T110
`QualityAnchor.VmafNeg` satirini o yuvarlanmis sayilardan **turetmisti**. Bu
program alani dogrudan okur.

## Kilitli / kilitsiz ikili uretimi

    dotnet publish -c Release tools/cipa-yeniden -o .calisma/T116/cipa-kilitli
    # QualityMeter.cs:86-87'de ",{FrameLock}" iki zincirden de cikarilir
    dotnet publish -c Release tools/cipa-yeniden -o .calisma/T116/cipa-kilitsiz
    git checkout -- src/VidShrink.Ffmpeg/QualityMeter.cs

Ayni islem `tools/VidShrink.Bench` icin de yapilir; bench tarafinda kilit
`tools/VidShrink.Bench/Program.cs`'in kendi `MeasureFilterGraph`indedir ve
kilitsiz hali `[0:v]scale=...lanczos[t];[1:v]null[r];` olur.

**Kaynak agaca kilitsiz hali commit edilmez.** Iki ikili de `.calisma/` altinda
kalir; agac her zaman kilitlidir.

## duzenek/

`.calisma/` gitignore'lu oldugu icin rapora giren sayilari ureten betikler
burada durur. Hepsi `.calisma/T116/` icinden calisacak sekilde yazilmistir.

| betik | ne uretir |
| --- | --- |
| `ab-kos.sh` | K2'nin A/B izgarasi: 4 yapilandirma x 2 kol (eski/yeni) x 2 olcer (kilitli/kilitsiz) = 16 `bench shrink` kosumu |
| `s92-kur.sh` / `s92-olc.sh` | §9.2 izgarasi: ref1/ref2 4 sn kesit + x265 crf32 test, kare basina VMAF-NEG gunlugu, kilitli/kilitsiz kare karsilastirmasi |
| `pin-kur.sh` / `pin-olc.sh` | §9.4 duyarli duzenegi: testsrc2 + `-itsoffset` kaymalari, ayrica bilerek 1 ve 2 kare kaydirilmis referanslar |
| `log-yolu-denetimi.sh` + `esc.py` | `QualityMeter.EscapeFilterPath`in urettigi mutlak Windows `log_path` biciminin ffmpeg filtresinde ayrisip ayrismadigi |
