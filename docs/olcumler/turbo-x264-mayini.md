# Turbo tavan tablosu x264'ü kapsıyordu (T153)

Durum: K1–K5 kapalı. Dal `T153-turbo-x264-mayini`, `origin/main` (`ee0d8d5`) üzerinden
açıldı, `main`e birleştirilmedi.

`CodecModel.TurboFirstPassCeilings` hem `libx264` hem `libx265` için `veryfast` tavanı
vaat ediyordu. `libx264`'te o tavan kullanılamaz: ikinci geçiş birinci geçişin `weightp`
ayarına uymak zorundadır, `veryfast` `weightp=1` ve `slow` `weightp=2` koşar, x264 ikinci
geçişi hiç açmaz ve çıktı sıfır bayt olur. Üretim T146'dan beri güvenli — `PlanCalculator`
turboyu yalnız `libx265`'te açıyor — ama tablo hâlâ yanlış vaatte bulunuyordu. Bu sözleşme
tabloyu ölçülene hizalar.

## Ölçüm yeniden üretildi

Sözleşmedeki çıktı T146 denetçisinden geliyordu; karar ona dayandığı için aynı ölçüm bu
turda **kendi koşumumla** yeniden üretildi. İki kaynak parçası, her birinden 5 sn kesit,
`scale=1280:720:flags=lanczos,fps=30`, `-b:v 1200k`, iki geçiş, ses yok.
Birinci geçiş `-preset veryfast`, ikinci geçiş `-preset slow` — yani turbo tavanının
ürettiği çift. `ffmpeg version 9.0-full_build-www.gyan.dev`.

Matris iki kez koşturuldu: birinci koşumda çıkış kodu sütunu `tail`in kodunu okuyordu,
ikinci koşum çıkış kodlarını doğrudan `ffmpeg`ten aldı. Aşağıdaki tablo ikinci koşumdur.

```
### parca-1 libx264 : p1exit=0 p2exit=127 boyut=0 bayt
[libx264 @ 0000013c668487c0] different weightp setting than first pass (2 vs 1)
[vost#0:0/libx264 @ 0000013c66848540] [enc:libx264 @ 0000013c667f51c0] Error while opening encoder - maybe incorrect parameters such as bit_rate, rate, width or height.
[out#0/mp4 @ 0000013c6682c700] Nothing was written into output file, because at least one of its streams received no packets.
### parca-1 libx265 : p1exit=0 p2exit=0 boyut=745186 bayt
### parca-2 libx264 : p1exit=0 p2exit=127 boyut=0 bayt
[libx264 @ 000002398ede8340] different weightp setting than first pass (2 vs 1)
[vost#0:0/libx264 @ 000002398ede80c0] [enc:libx264 @ 000002398ed85d00] Error while opening encoder - maybe incorrect parameters such as bit_rate, rate, width or height.
[out#0/mp4 @ 0000023990cb28c0] Nothing was written into output file, because at least one of its streams received no packets.
### parca-2 libx265 : p1exit=0 p2exit=0 boyut=677314 bayt
```

| Parça | Kodek | Geçiş 1 | Geçiş 2 | Çıktı |
|---|---|---|---|---|
| parca-1 | libx264 | exit 0 | **exit 127** | **0 bayt** |
| parca-1 | libx265 | exit 0 | exit 0 | 745 186 bayt |
| parca-2 | libx264 | exit 0 | **exit 127** | **0 bayt** |
| parca-2 | libx265 | exit 0 | exit 0 | 677 314 bayt |

Denetçinin bulgusu doğrulandı: iki parçanın ikisinde de x264 turbosu sıfır bayt üretiyor,
aynı ön ayarlarla x265 turbosu çalışıyor.

## K1 — Mayın önce kırmızıya düştü

Kusur commit'i: `6e1af3f`. O commit yalnız iki şey getirdi — `CodecModel.TurboFirstPassIsSafe`
bugünkü vaadi kodlayan halinde (`=> SupportsTurboFirstPass(codec)`, yani tabloda tavanı
olan her kodek güvenli) ve `tests/VidShrink.Tests/TurboTavanTests.cs`.

Ölçü tabloyu sabitle karşılaştırmıyor; **kararı** pimliyor. Dört kol:

- `X264_icin_tavan_vaat_ediliyor_ama_o_tavan_guvenli_degil` — tablonun x264 için gerçekten
  bir tavan vaat ettiğini `FirstPassPreset("libx264","slow",turbo:true) != "slow"` ile
  ölçer, sonra o tavanın güvenli **olmadığını** ister.
- `Vaat_edilen_tavan_kumesi_guvenli_kumeden_genis_ve_fark_libx264` — vaat/güvenli/güvensiz
  kümelerini tablodan üretir, farkı isimle listeler.
- `Guvensiz_tavan_hicbir_hiz_kipinde_uretim_yolunda_acilmiyor` — güvensiz kümeyi önce
  `Assert.NotEmpty` ile pimler (boş küme sessizce geçen ölü koldur), sonra her hız kipi ×
  her kodek tercihi için `PlanCalculator.Build` koşup `TurboFirstPass` bayrağını okur.
- `Uretim_yolunun_actigi_turbo_kumesi_guvenli_kumeyle_ayni` — 13 bilinen kodeğin her birini
  tek başına kurulu gösteren sahte yoklamayla × 4 kodek tercihi ile üretim yolunu sürer,
  ulaşılan her kodek için `plan.TurboFirstPass` ile `CodecModel.TurboFirstPassIsSafe`
  karşılaştırılır. İki taraftan biri kayarsa ölçü ölür.

Ham kırmızı (`dotnet test -c Release --filter "TurboTavanTests"`, `6e1af3f`):

```
  Başarısız VidShrink.Tests.TurboTavanTests.Guvensiz_tavan_hicbir_hiz_kipinde_uretim_yolunda_acilmiyor [2 ms]
  Hata İletisi:
   Assert.NotEmpty() Failure: Collection was empty
  Standart Çıkış İletileri:
 guvensiz kume: <bos>

  Başarısız VidShrink.Tests.TurboTavanTests.Vaat_edilen_tavan_kumesi_guvenli_kumeden_genis_ve_fark_libx264 [8 ms]
  Hata İletisi:
   Assert.Equal() Failure: Collections differ
           ↓ (pos 0)
Expected: ["libx265"]
Actual:   ["libx264", "libx265"]
           ↑ (pos 0)
  Standart Çıkış İletileri:
 libx264    tavan=veryfast  guvenli=True
 libx265    tavan=veryfast  guvenli=True
 vaat=libx264,libx265 guvenli=libx264,libx265 guvensiz=

  Başarısız VidShrink.Tests.TurboTavanTests.X264_icin_tavan_vaat_ediliyor_ama_o_tavan_guvenli_degil [1 ms]
  Hata İletisi:
   Assert.False() Failure
Expected: False
Actual:   True
  Standart Çıkış İletileri:
 libx264 son=slow turbo ilk=veryfast tavan=veryfast guvenli=True

  Başarısız VidShrink.Tests.TurboTavanTests.Uretim_yolunun_actigi_turbo_kumesi_guvenli_kumeyle_ayni [31 ms]
  Hata İletisi:
   Assert.Equal() Failure: Collections differ
           ↓ (pos 0)
Expected: ["acik"]
Actual:   ["kapali"]
           ↑ (pos 0)
  Standart Çıkış İletileri:
 av1_amf      hizli=kapali       kalite=<ulasilmadi> guvenli=False
 av1_nvenc    hizli=kapali       kalite=<ulasilmadi> guvenli=False
 av1_qsv      hizli=kapali       kalite=<ulasilmadi> guvenli=False
 h264_nvenc   hizli=kapali       kalite=kapali       guvenli=False
 hevc_amf     hizli=kapali       kalite=<ulasilmadi> guvenli=False
 hevc_nvenc   hizli=kapali       kalite=<ulasilmadi> guvenli=False
 hevc_qsv     hizli=kapali       kalite=<ulasilmadi> guvenli=False
 libsvtav1    hizli=<ulasilmadi> kalite=kapali       guvenli=False
 libx264      hizli=kapali       kalite=kapali       guvenli=True
 libx265      hizli=acik         kalite=kapali       guvenli=True

Başarısız! - Başarısız:     4, Başarılı:     0, Atlanan:     0, Toplam:     4, Süre: 42 ms
```

Son tablodaki `libx264  hizli=kapali  guvenli=True` satırı mayının kendisidir: model
"güvenli" diyor, üretim yolu açmıyor.

## K2 — Seçenek (2): tabloda kalıyor, güvensiz işaretleniyor

Karar: **seçenek 2**. `libx264` satırı tabloda kalır, ölçülmüş tavanını (`veryfast`)
taşımaya devam eder, ama `Safe: false` alır. `TurboFirstPassIsSafe` bu alanı okur.

Gerekçe — üç seçenek de sahiplik sınırıyla sınandı:

- **(1) x264'ü tablodan çıkar.** `owns` dışına yazmadan yapılamıyor.
  `tests/VidShrink.Tests/TurboFirstPassTests.cs` bu sözleşmenin değil ve kümeyi üç ayrı
  yerde `{libx264, libx265}` olarak pimliyor: `Turbo_kumesi_tam_olarak_x264_ve_x265`
  (`:62`), `Kume_disindaki_her_kodek_turbo_tanimiyor` (`:71`, `kumede = kodek is "libx264"
  or "libx265"`), `Bilinen_kodekler_dort_yazilim_dokuz_donanim` (`:190`,
  `Assert.Equal(2, bilinen.Count(CodecModel.SupportsTurboFirstPass))`). Satırı çıkarmak bu
  üç ölçüyü kırar ve onları düzeltmek sahibi olmadığım dosyaya yazmak demektir.
- **(3) `weightp`i iki geçişe de eşitle ve x264'ü aç.** `ffmpeg` argüman üretimi
  `CodecModel.cs` **dışında**: `-preset` ve yanındaki her şey
  `src/VidShrink.Core/FfmpegArguments.cs` `Build`/`FirstPassPreset` içinde üretiliyor
  (`FfmpegArguments.cs:362` ve `:400`), o dosya bu sözleşmenin değil. Sözleşmenin K2
  maddesi bu durumda "yapma, bildir" diyor. **Bildiriliyor:** x264 turbosunu açmak
  `FfmpegArguments`ın her iki geçişe aynı `weightp` yazmasını gerektirir; ayrı bir
  sözleşme konusudur. Denetçinin kontrol kolu bunun çalıştığını gösteriyor (klip 5:
  2270→1832 ms, VMAF 68,623→67,407) — o sayılar bu turda **yeniden üretilmedi**, çünkü
  karar onlara dayanmıyor.
- **(2) Tabloda bırak, güvensiz işaretle.** `owns` içinde kalıyor, tablo ölçülene uyuyor,
  yanlış vaat kalkıyor. Seçilen bu.

Değişiklik `src/VidShrink.Core/CodecModel.cs` (`f6f4256`): sözlük değeri `string` yerine
`TurboFirstPassEntry(string Ceiling, bool Safe)`; `SupportsTurboFirstPass` ve
`TurboFirstPassCeiling` davranışı aynı kalıyor, yeni `TurboFirstPassIsSafe` alanı okuyor.
Docstring ölçümü taşıyor ve iki metodun ne söylediğini ayırıyor: biri tavanın **varlığını**,
öbürü tavanın **kullanılabilirliğini**.

(3) seçilmediği için giriş × süre × boyut × VMAF tablosu yok; yukarıdaki iki parçalı
ölçüm yalnız mayının varlığını doğrular. Toplam ağır koşum: `ffmpeg` matrisi **2 kez**
(4 iki-geçişli kodlama × 2 = 16 ffmpeg süreci), her kesit 5 sn.

## K3 — T146'nın daraltması kırılmadı

`PlanCalculator.cs` diff'te **yok**. Dal tabanı `ee0d8d5`.

```
$ git diff --stat ee0d8d5..HEAD
 src/VidShrink.Core/CodecModel.cs         |  45 +++++++-
 tests/VidShrink.Tests/TurboTavanTests.cs | 171 +++++++++++++++++++++++++++++++
 2 files changed, 211 insertions(+), 5 deletions(-)

$ git diff --name-only ee0d8d5..HEAD | grep -E "PlanCalculator|EncodePlan|FfmpegArguments|VidShrink.Ffmpeg" | wc -l
0
```

(Bu dosya eklendiğinde diff'e üçüncü satır olarak `docs/olcumler/turbo-x264-mayini.md`
girer; kod tarafı değişmez.)

`PlanCalculator.cs`'teki `TurboFirstPassIsSafe(codec) => codec.Equals("libx265", ...)`
kolu aynen duruyor ve x264 üretimde açılmıyor — yukarıdaki kırmızı tablonun
`libx264 hizli=kapali` satırı bunu ölçüyor. Komşu süitler de yeşil:
`dotnet test -c Release --filter "TurboFirstPassTests|UretimYoluTests|PlanCalculator"`
→ `Başarısız: 0, Başarılı: 150, Toplam: 150`.

## K4 — Mutasyon

Düzeltme geri alındı: tablodaki tek hücre `["libx264"] = new("veryfast", Safe: false)` →
`Safe: true`. `dotnet build -c Release --no-incremental` sonrası:

| Kol | Eski (`Safe: false`) | Yeni (mutasyon, `Safe: true`) |
|---|---|---|
| `TurboTavanTests` | 4 geçti | **4 kaldı** |
| `FfmpegArgumentsTests` | 66 geçti | 66 geçti |

Mutasyonun ham çıktısı:

```
[xUnit.net 00:00:08.27]     VidShrink.Tests.TurboTavanTests.Guvensiz_tavan_hicbir_hiz_kipinde_uretim_yolunda_acilmiyor [FAIL]
[xUnit.net 00:00:08.27]     VidShrink.Tests.TurboTavanTests.Vaat_edilen_tavan_kumesi_guvenli_kumeden_genis_ve_fark_libx264 [FAIL]
[xUnit.net 00:00:08.27]     VidShrink.Tests.TurboTavanTests.X264_icin_tavan_vaat_ediliyor_ama_o_tavan_guvenli_degil [FAIL]
[xUnit.net 00:00:08.29]     VidShrink.Tests.TurboTavanTests.Uretim_yolunun_actigi_turbo_kumesi_guvenli_kumeyle_ayni [FAIL]
Başarısız! - Başarısız:     4, Başarılı:    66, Atlanan:     0, Toplam:    70, Süre: 3 s - VidShrink.Tests.dll (net8.0)
```

Geri alındıktan ve `--no-incremental` yeniden derlemeden sonra:

```
Başarılı!  - Başarısız:     0, Başarılı:    70, Atlanan:     0, Toplam:    70, Süre: 3 s - VidShrink.Tests.dll (net8.0)
```

## K5 — Her verify kolu gerçekten test buluyor

`dotnet test -c Release --list-tests --filter "<kol>"`, kol başına sayım:

| Kol | Test sayısı |
|---|---|
| `TurboTavanTests` | 4 |
| `FfmpegArgumentsTests` | 66 |
| ikisi birlikte (verify satırı) | 70 |

`TurboTavanTests` kolunun listelediği dört ad:

```
    VidShrink.Tests.TurboTavanTests.X264_icin_tavan_vaat_ediliyor_ama_o_tavan_guvenli_degil
    VidShrink.Tests.TurboTavanTests.Vaat_edilen_tavan_kumesi_guvenli_kumeden_genis_ve_fark_libx264
    VidShrink.Tests.TurboTavanTests.Guvensiz_tavan_hicbir_hiz_kipinde_uretim_yolunda_acilmiyor
    VidShrink.Tests.TurboTavanTests.Uretim_yolunun_actigi_turbo_kumesi_guvenli_kumeyle_ayni
```

`FfmpegArgumentsTests` kolunun 66 adının hepsi tek sınıftan; alt-dize eşleşmesi başka
sınıf çekmiyor:

```
$ sed 's/^ *//' list-FfmpegArgumentsTests.txt | awk -F'.' '{print $1"."$2"."$3}' | sort | uniq -c
     66 VidShrink.Tests.FfmpegArgumentsTests
```

Sıfır bulan kol yok. 4 + 66 = 70, birleşik koşumun bulduğu sayıyla aynı.

## Kalan borç

- `PlanCalculator`ın `private static bool TurboFirstPassIsSafe(string codec)` metodu artık
  `CodecModel.TurboFirstPassIsSafe` ile aynı şeyi söyleyen ikinci bir kaynak. K3
  `PlanCalculator.cs`e dokunmayı yasakladığı için birleştirilmedi; iki taraf ayrışırsa
  `Uretim_yolunun_actigi_turbo_kumesi_guvenli_kumeyle_ayni` ölçüsü kırılır. Birleştirme
  `PlanCalculator.cs`in sahibi olan bir sözleşmeye kalıyor.
- x264 turbosunu gerçekten açmak (seçenek 3) `FfmpegArguments.cs`in her iki geçişe aynı
  `weightp` yazmasını gerektiriyor; ayrı sözleşme.
