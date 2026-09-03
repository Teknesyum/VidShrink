sozlesme: T141
dal: T141-yedek-yolda-kalite
son-commit: 122803e (isin son kod commit i; devir dosyasi bunun ustune ayri commit olarak eklendi, dal ucu icin git log -1 T141-yedek-yolda-kalite)

# T141 devir — yoklama yedek yola düştüğünde pencerenin kalitesi

Çalışma ağacı devir anında **temiz**; yarım kod, kırmızı test ya da commit
edilmemiş betik yok. `WIP devir` commit'i açılmadı çünkü bırakılacak yarım şey
yoktu. Yedi kriterin yedisi kapandı. Bu dosya, `.calisma/T141/` gitignore'da
olduğu için oradaki bütün ham çıktıyı içine yapıştırır.

Uzun rapor: `docs/olcumler/yedek-yolda-kalite.md` (commit `0ec4633`, `122803e`
ile güncellendi).

## Nerede kaldım

- **K1 — geçti.** İki kolun da koştuğu ölçüldü, ölü kol yok, sözleşme durmadı.
- **K2 — geçti.** Üç kırmızı ayrı commit'te (`1c87aaf`), hiçbiri sabit sayıya bakmıyor.
- **K3 — geçti.** Karar (a): yedek yol da kaliteyi ölçsün. Gerekçe yazıldı.
- **K4 — geçti.** İki kol aynı kararı aldı, karar tablosu raporda.
- **K5 — geçti.** Yedi girdilik eski/yeni tablosu + tüketen yerlerin listesi.
- **K6 — geçti.** Üç mutasyon, çapraz bulaşma yok. Bir kaçak yakalandı ve düzeltildi (`75a658a`).
- **K7 — geçti.** Kol sayımları 35 / 42 / 25, sıfır bulan kol yok. Verify komutu 102 geçti.
  Filtresiz `dotnet test` bu makinede çöküyor ama **`main`de de çöküyor** ve CI'da
  yeşil bitiyor (`33682771946`, `completed success`).

Hiçbir kritere başlanmamış değil. Yarım kalan kriter yok.

## Ölçtüğüm sayılar

Aşağıdakilerin hepsi bu makinede gerçekten koşan komutların ham çıktısı.

### K1 — keşif koşumu

Klipler `lavfi` ile üretildi: `testsrc2=size=320x240:rate=12:duration=8` ve
`testsrc2=size=96x96:rate=12:duration=8`. Ölçer, çağrı sayan bir
`IQualityMeasurement` sahtesi.

```
 kaynak 320x240 sure 8
 HIZLI  half=(160,120) full=24/50151 half=24/18607 quality=var calls=1
 HALFNULL half=null   full=24/50181 half=0/0 quality=NULL calls=0
 YEDEK? half=(161, 121) full=24/50181 half=0/0 quality=NULL calls=0
 YEDEK? half=(-2, -2) full=24/50151 half=24/50143 quality=var calls=1
 YEDEK? half=(1, 1) full=24/50181 half=0/0 quality=NULL calls=0
 ### kucuk kaynak 96x96
 URETIM 96x96: calls=0 kalite kaydi=0 measured=True
 ### normal kaynak 320x240
 URETIM 320x240: calls=2 kalite kaydi=2 measured=True
```

`half=(-2,-2)` satırı dikkat: ffmpeg `scale=-2:-2`yi kabul ettiği için hızlı yol
koştu. Yedek yolu zorlamak için `(161,121)` ya da `(1,1)` kullanılmalı.

### K2 — düzeltme öncesi kırmızı

```
  Başarısız VidShrink.Tests.ComplexityProbeTests.KucukKaynakOlcerVarkenNormalKaynakKadarKaliteUretiyor [1 s]
  Hata İletisi:
   Assert.Equal() Failure: Values differ
Expected: 2
Actual:   0
  Başarısız VidShrink.Tests.ComplexityProbeTests.AyniPencereHizliYoldaOlculuyorYarimYokKolundaOlculmuyor [363 ms]
  Hata İletisi:
   Assert.NotNull() Failure: Value is null
  Başarısız VidShrink.Tests.ComplexityProbeTests.AyniPencereHizliYoldaOlculuyorYedekYoldaOlculmuyor [519 ms]
  Hata İletisi:
   Assert.NotNull() Failure: Value is null

Başarısız! - Başarısız:     3, Başarılı:    32, Atlanan:     0, Toplam:    35, Süre: 35 s
```

Not: bu kırmızı, `KucukKaynak...` ölçüsünün **eski** (çağrı sayan) halinden
alındı. Ölçü sonra `75a658a` ile kalite kaydı sayacak şekilde güçlendirildi;
`Expected: 2 / Actual: 0` sayıları iki halde de aynı.

### K5 — düzeltme ÖNCESİ (`RunDetailedAsync(info, Quality, true, olcer)`)

```
 | 96x96 (yarim yok kolu) | 96x96 8 sn | kayit=0 | cagri=0 | HasQuality=False |
 | 126x126 (yarim yok kolu, sinir) | 126x126 8 sn | kayit=0 | cagri=0 | HasQuality=False |
 | 128x128 (hizli yol, sinir) | 128x128 8 sn | kayit=2 | cagri=2 | HasQuality=True |
 | 320x240 (hizli yol) | 320x240 8 sn | kayit=2 | cagri=2 | HasQuality=True |
 | 640x360 (hizli yol) | 640x360 8 sn | kayit=2 | cagri=2 | HasQuality=True |
 | 320x240 kisa 2 sn | 320x240 2 sn | kayit=1 | cagri=1 | HasQuality=True |
 | yedek yol (zorlanmis) | tek pencere | kalite=NULL | cagri=0 | full=24 half=0 |
```

### K5 — düzeltme SONRASI

```
 | 96x96 (yarim yok kolu) | 96x96 8 sn | kayit=2 | cagri=2 | HasQuality=True |
 | 126x126 (yarim yok kolu, sinir) | 126x126 8 sn | kayit=2 | cagri=2 | HasQuality=True |
 | 128x128 (hizli yol, sinir) | 128x128 8 sn | kayit=2 | cagri=2 | HasQuality=True |
 | 320x240 (hizli yol) | 320x240 8 sn | kayit=2 | cagri=2 | HasQuality=True |
 | 640x360 (hizli yol) | 640x360 8 sn | kayit=2 | cagri=2 | HasQuality=True |
 | 320x240 kisa 2 sn | 320x240 2 sn | kayit=1 | cagri=1 | HasQuality=True |
 | yedek yol (zorlanmis) | tek pencere | kalite=var | cagri=1 | full=24 half=0 |
```

### K6 — mutasyon ızgarası

```
### M1
AyniPencereHizliYoldaOlculuyorYarimYokKolundaOlculmuyor
KucukKaynakOlcerVarkenNormalKaynakKadarKaliteUretiyor
Başarısız! - Başarısız:     2, Başarılı:    33, Atlanan:     0, Toplam:    35, Süre: 40 s
### M2
AyniPencereHizliYoldaOlculuyorYedekYoldaOlculmuyor
Başarısız! - Başarısız:     1, Başarılı:    34, Atlanan:     0, Toplam:    35, Süre: 35 s
### M3
AyniPencereHizliYoldaOlculuyorYarimYokKolundaOlculmuyor
AyniPencereHizliYoldaOlculuyorYedekYoldaOlculmuyor
KucukKaynakOlcerVarkenNormalKaynakKadarKaliteUretiyor
Başarısız! - Başarısız:     3, Başarılı:    32, Atlanan:     0, Toplam:    35, Süre: 43 s
### mutasyonsuz
Başarılı!  - Başarısız:     0, Başarılı:    35, Atlanan:     0, Toplam:    35, Süre: 44 s
```

Mutasyonların ne yaptığı (ızgarayı yeniden üretmek isteyen için,
`src/VidShrink.Ffmpeg/ComplexityProbe.cs` üzerinde tek tek uygulanır,
her birinden sonra `git checkout -- <dosya>`):

- **M1** — `half is null` kolunda kaliteyi düşür:
  `return new WindowSample(lone.Bytes, lone.Frames, 0, 0, lone.Quality);`
  satırındaki `, lone.Quality` kaldırılır.
- **M2** — yedek yolda kaliteyi düşür:
  `return new WindowSample(full.Bytes, full.Frames, halfBytes, halfFrames, full.Quality);`
  satırındaki `, full.Quality` kaldırılır.
- **M3** — ortak yardımcı ölçeri çağırıp cevabını atsın:
  `return (bytes, frames, await qualityMeasurement.MeasureWindowAsync(path, target, start, WindowSeconds, ct));`
  satırı, önce çağrıyı yapıp sonra `return (bytes, frames, null);` dönecek
  şekilde ikiye ayrılır.

Her mutasyondan sonra `dotnet build -c Release --no-incremental` ile TAM derlendi;
`--no-build` hiç kullanılmadı.

### K7 — kol sayımları

`dotnet test -c Release --list-tests` çıktısı `^    VidShrink\.Tests\.` deseniyle
sayıldı:

```
ComplexityProbeTests: 35
ComplexityScanTests: 42
PlanCalculatorProbeTests: 25
```

Verify komutunun tamamı:

```
dotnet test -c Release --filter "ComplexityProbeTests|ComplexityScanTests|PlanCalculatorProbeTests"
Başarılı!  - Başarısız: 0, Başarılı: 102, Atlanan: 0, Toplam: 102, Süre: 1 m 24 s
```

### K7 — filtresiz koşumun çöküşü (üç deneme)

```
--- 1. deneme ---
Etkin test çalıştırması iptal edildi. Nedeni: Test ana işlemi kilitlendi
Başarılı! - Başarısız: 0, Başarılı: 81, Atlanan: 0, Toplam: 81, Süre: 59 s
Test Çalıştırması Durduruldu.

--- 2. deneme (verbosity=normal, son satırlar) ---
  Başarılı VidShrink.Tests.SensitivityAbTests.TargetsMustActuallyDiffer [< 1 ms]
Etkin test çalıştırması iptal edildi. Nedeni: Test ana işlemi kilitlendi
Test Çalıştırması Durduruldu.
Toplam test sayısı: Bilinmiyor
     Geçti: 36
 Toplam süre: 12,5622 Saniye

--- 3. deneme (--blame) ---
Başarılı! - Başarısız: 0, Başarılı: 132, Atlanan: 5, Toplam: 137, Süre: 25 s
Test Çalıştırması Durduruldu.
Kilitlenme oluştuğunda çalışan test:
VidShrink.Tests.PanelHostTests.Devir_sinirindaki_bosluk_olculur
```

`PanelHostTests` tek başına koşturulduğunda: `Başarılı! 0 / 13 / 13`.

**Çöküşün bu dala ait olmadığı ölçüldü.** `origin/main` (`1a60a09`) ayrı bir
worktree'ye çıkarıldı, `--no-incremental` ile derlendi, aynı filtresiz koşum
yapıldı; aynı çöküş orada da oldu, bu kez
`QualityTargetTests.SearchLandsWithinTheMeasuredTolerance` üzerinde. O worktree
`git worktree remove --force` ile silindi, geride bir şey bırakmadı.

### CI

```
33682771946  completed  success   T141 K5-K7 ...  26m32s
33681585349  completed  cancelled T141 K3-K4 ...  12m40s
```

`cancelled` olan koşum bir başarısızlık değil; yeni push onu geçtiği için iptal
edildi. Son commit'in koşumu `success`.

## Ölçtüklerim ile varsaydıklarım

**Gerçek koşum var (yukarıdaki ham çıktı):**

- `half is null` kolunun üretim girdisiyle koştuğu — 96x96 klip üretim
  girişinden geçip `Measured=True` döndü ve sıfır kalite kaydı üretti.
- Kolun sınırı — 126x126 bu kola düşüyor, 128x128 hızlı yola giriyor.
  **Türetilmedi, iki klip gerçekten koşturuldu.**
- Yedek yolun zorlanabildiği ve zorlandığında kalitenin kaybolduğu.
- K5'in bütün eski/yeni sayıları.
- Mutasyon ızgarasının her hücresi.
- Kol sayımları ve verify koşumu.
- Çöküşün `main`de de olduğu.

**Tahmin, koşum yok — devralan bunları ölçüm sanmasın:**

- "127 pikselin altındaki her kenar bu kola düşüyor" cümlesinin **tam eşiği**
  `EvenDown(Math.Round(boyut * 0.5)) >= 64` ifadesinden kalemle türetildi.
  Ölçülen tek şey 126 ve 128 sınırı; 127'nin hangi tarafa düştüğü koşturulmadı.
- Yedek yolun üretimde **ne sıklıkta** koştuğu. Beş `null` dönme şartı kodu
  okuyarak sayıldı; hiçbirinin gerçek hayatta ne kadar sık olduğu ölçülmedi.
  "Üretim girdisiyle sıfır, ortam bozulduğunda her pencerede" cümlesi bu
  okumadan çıkıyor, alan verisinden değil.
- (a) seçeneğinin maliyetinin "hızlı yolun zaten ödediği maliyetin aynısı"
  olduğu. Kod okumasından çıkan bir akıl yürütme; VMAF süresi ölçülmedi,
  sahte ölçerle koşuldu.
- Çöküşün sebebi olarak yazılan "yük altında Avalonia arayüz testleri test ana
  işlemini düşürüyor". Çöküşün `main`de de olduğu ölçüldü, **sebebi ölçülmedi.**
  Bu bir hipotez.

## Güvenilmeyecek şeyler

- Sözleşme metnindeki satır numaraları `b88bb66` ağacından; ben `b4161d7`
  üzerinde çalıştım ve `ComplexityProbe.cs`i değiştirdim. Raporda ve devir
  dosyasında geçen `:737`, `:746-748`, `:98`, `:733` gibi numaralar **artık
  kaymış durumda.** Kola ismiyle bak: `SampleWindowAsync` içindeki
  `if (half is null)` dalı ve `split is { } measured` sonrasındaki yedek dal.
- `k1-kesif.txt` içindeki `YEDEK? half=(-2, -2)` satırı yanıltıcı ada sahip:
  o satırda yedek yol **koşmadı**, hızlı yol koştu. Adı keşif sırasında konmuş,
  düzeltilmedi.
- K2 kırmızısı ölçünün eski halinden alındı (yukarıda not düşüldü). Ölçü adı
  aynı, gövdesi değişti.
- Raporda ve bu dosyada `.calisma/T141/...` diye anılan fixture yolları
  gitignore'da; içerikleri buraya yapıştırıldı, dosyalara güvenme.
- `docs/olcumler/yedek-yolda-kalite.md` içinde "K7'nin tamamı yeşil şartı bu
  makinede sağlanamıyor" diyen bölüm duruyor ve doğru; ama altına CI'ın yeşil
  bittiği eklendi. İki cümleyi birlikte oku, ilkini tek başına alıp "K7 kaldı"
  sanma.

## Dokunduğum dosyalar

`git diff --stat b4161d7..122803e`:

```
 .claude/relay/kapsam.json                     |  18 ++
 docs/olcumler/yedek-yolda-kalite.md           | 246 ++++
 src/VidShrink.Ffmpeg/ComplexityProbe.cs       |  46 ++-
 tests/VidShrink.Tests/ComplexityProbeTests.cs | 102 ++-
```

Üçü `owns` içinde. **`owns` dışına çıkan tek şey `.claude/relay/kapsam.json`** —
onu ben yazmadım, rapor dosyasını yazınca kanca kendi defterine bir satır
ekledi. Geri almadım çünkü makinenin kendi indeksi; istenirse çıkarılabilir.

`IQualityMeasurement.cs` ve `QualityMeter.cs`e **dokunulmadı**, imza değişikliği
gerekmedi. `src/VidShrink.Core/ProbeResult.cs`e dokunulmadı — K3'te (b) ve (c)
seçeneklerinin elenme sebebi buydu.

`main` ile hiçbir birleştirme yapılmadı. `.calisma/kaynak/`a dokunulmadı.

## Sıradaki adım

Ben olsam kod yazmazdım; sözleşme kapanmış durumda ve CI son commit üzerinde
yeşil. Yapılacak iş mühür ve temizlik: `T141-yedek-yolda-kalite` dalını T0
`main`e birleştirir, sonra `git worktree remove` ile `.claude/worktrees/T141`
kaldırılır ve `.calisma/T141/` silinir (içindeki her şey bu dosyaya
yapıştırıldı, kaybolacak bir şey yok). Denetçi ham çıktı isterse `.calisma`ya
değil bu dosyaya baksın. Eğer birisi işi ilerletecekse en kıymetli iki borç
sırayla şunlar: (1) `SplitSampleAsync` ölçere kodlama son teslim tarihinin
artığını veriyor, yeni `MeasuredSampleAsync` ise `ct` kullanıyor — iki yol bu
noktada ayrık ve birleştirilmeli; (2) ölçer istisna attığında üç yolda da
sessizce `null` dönülüyor, "ölçemedim" işareti hâlâ yok ve onu kapatmak
`ProbeResult`a alan eklemeyi gerektirdiği için ayrı bir sözleşme ister.
Filtresiz `dotnet test`in bu makinede çökmesi de ayrı bir iş; `main`de de var,
CI'da yok, sebebi ölçülmedi.
