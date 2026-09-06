# Astra incelemesinin değerlendirmesi

Kaynak: `docs/astra.md` (6 Eylül 2026, `631a27d5`, statik kaynak incelemesi).
Değerlendiren: T0. Yöntem: her iddia için kaynağa bakıldı; özet değil, satır numarası.

## Doğrulanan bulgular

### 1. Kalibrasyon geçerliliği — **doğru, ciddi**

`ComplexityProfile.cs:14`:

```csharp
public bool Matches(string codec, double scale, double fps)
    => string.Equals(Codec, codec, StringComparison.OrdinalIgnoreCase)
       && Math.Abs(Scale - scale) <= ScaleTolerance
       && Math.Abs(Fps - fps) <= FpsTolerance;
```

`Width` ve `Height` kayıtta var (`:9-10`), karşılaştırmada yok. `Preset` de yok.
Hemen altındaki `EncodeSpeed.Matches` (`:30`) **preset ve geometriyi karşılaştırıyor** —
yani doğru kalıp aynı dosyada, on beş satır aşağıda duruyor.

Sonuç: preset `veryfast`→`slow` değişince kalibrasyon geçerli sayılıyor ve ölçüm
tekrarlanmıyor. Bu bir tahmin hatası değil, **geçersiz ölçümün geçerli sayılması**.

### 2. Pass-through teslimi tavanı denetlemiyor — **doğru**

`EncodeRunner.cs:240` `PassThrough`: `File.Copy` → `FileInfo.Length` → `new EncodeResult(true, ...)`.
Hedef boyut hiç okunmuyor, geçici dosya + doğrulama koruması yok — normal yolda o koruma var.
Kaynak zaten hedefin altındaysa zararsız; kararı veren tarafta bir hata olursa
sert tavan sessizce delinir ve `true` döner.

### 3. ffprobe süreç ömrü — **doğru, düşük öncelik**

`FfprobeClient.cs:25-26` stdout'u sonra stderr'i sırayla okuyor; `ct.Register` ile
süreç ağacı öldürme yok, kendi zaman aşımı yok. `FfmpegRunner.cs:107` aynı işi
`using (ct.Register(() => TryKill(process)))` ile yapıyor. Canlı bir kilitlenme
üretilmedi; fark kodda görünür.

### 4-6. Kalite eğrisi, rejim eşiği, sahne örneklemesi

Bunlar kusur değil **tasarım eleştirisi** — Astra da öyle sunuyor. Doğru tarafı:
`WithProbeQuality` her kalite değerini aynı `ProbeBppf` ile eşleştirdiği için eğim
öncülde kalıyor. Ama düzeltmesi ölçüm gerektirir, kod okumakla karar verilmez.

## Astra'nın gördüğü iki kırmızı

İkisi de **ürün kusuru değil ölçüm kusuru** — bu depoda üçüncü kez aynı şekil.

`QualityHintTests.cs:47` aramayı `ChipWhatsApp`'tan başlatıyor; `MainWindow.axaml`'de
`Chip8` **330. satırda**, `ChipWhatsApp` **337. satırda**. Yani mevcut bir düğme
inceleme aralığının dışında kalıyor. Astra'nın teşhisi doğru.

`TipOverflowTests.NoTipLineOverflowsTheBalloonByASingleWord` — bazı tr/en ipucu
satırları taşma denetimini geçmiyor. T177 ipucu satırlarını zaten siliyor; bu
kırmızının T177 sonrası durumu yeniden ölçülmeli, `631a27d5` anlık görüntüsünden
rapor edilmemeli.

**Açık borç:** T177 sekizinci yongayı (`ChipArchive`) ekliyor ama `QualityHintTests.cs`
o sözleşmenin `owns` kümesinde değil ve test yedi yongayı sabit sayıyor
(`QualityHintTests.cs:56,63`). Yapıcı dokunamaz, birleşmede kırmızı verir.

## Fable danışması

Yapılmadı. Gerekçe: birinci, ikinci ve üçüncü bulgu **kaynağa bakılarak kesinleşti** —
ikinci bir modelin görüşü doğruluklarına bir şey eklemez. Dördüncü ve altıncı bulgu
ise görüş değil **ölçüm** ister; danışma yerine deney doğru araçtır.

## Sıra önerisi

Astra'nın önerdiği sırayı bir yerde değiştiriyorum: **pass-through denetimi kalibrasyon
anahtarından önce.** Sebep — biri yanlış tahmin üretir, öteki kullanıcıya sessizce
sözünü tutmamış dosya teslim eder.

1. `PassThrough` teslim denetimi (küçük, kapalı, testi kolay).
2. `CalibrationSignature` anahtarının preset + geometri ile kurulması.
3. `QualityHintTests` çapası (T177 ile birlikte).
4. `FfprobeClient` ortak süreç katmanına taşınması.
5. Ölçüm gerektirenler (rejim, örnekleme, kalite eğrisi) — 0.3.2 ve sonrası, her biri
   **ayrı** deneyde.

Astra'nın kendi doğrulama sınırı aynen geçerli: motor davranışı ölçülmedi, Bench
koşulmadı, hiçbir performans veya kalite kazancı iddia edilmiyor.
