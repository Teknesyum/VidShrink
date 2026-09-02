# Ayırıcı ölçüsü duvar saatine bağlıydı — kaynak, düzeltme, kanıt

T127. `SplitDragTests.Kare_arasindaki_bos_turda_ayirici_cizim_acmaz` CI'da üçüncü kez
düştü (T111, T116, T123 dallarında). Her seferinde "zamana bağlı, ilgisiz" yazılıp
geçildi. Bu belge kaynağı saptıyor ve kaldırıyor.

Ağaç: `T127-ayirici-kararsizligi`, taban `origin/main` `fcf377f`.

## 1. Kusur üretildi (K1)

Yerel makine hızlı; ölçü olduğu gibi koşturulunca hiç düşmüyor (5/5 yeşil). Düşüren
koşul yapay olarak kuruldu: yüzeyin iç damgası ile ölçünün kendi saatinin başlangıcı
arasına bir duraklama konuldu — yüklü bir koşucunun tam o noktada takılması.

Geçici ölçü `Thread.Sleep(60)` ile:

    Başarısız VidShrink.Tests.SplitDragTests.GECICI_K1_kosucu_duraklarsa_bos_tur_olcusu_duser
      Assert.Equal() Failure: Values differ
      Expected: 0
      Actual:   1

Bu, T116'nın CI'da kaydettiği düşüşün **birebir aynısı**: "beklenen 0, gelen 1"
(`.claude/relay/contracts/done/T116.md:275`, koşum `d435ff9`).

İki duraklama değeriyle koşulunca 40 ms → 1 çizim, 60 ms → 2 çizim çıktı; yani
duraklamanın büyüklüğü çizim sayısını belirliyor. Windows'un uyku çözünürlüğü
(15,6 ms) `Sleep(40)`'ı 50 ms'nin üstüne taşıdığı için 40 de düşüyor.

**Sayı modellendi ve model doğrulandı.** Gerçek boşluk ölçüye içeriden ölçtürüldü ve
beklenen çizim sayısı şu formülle önden hesaplandı:

    boşluk < 50 ms                          -> 0 çizim
    aksi hâlde floor((boşluk + 50 - 100) / (1000/60)) + 1 çizim

Üç duraklama değerinde (25, 40, 60 ms) ve iki ayrı koşumda model gözlenen çizim
sayısını **birebir** verdi (3/3 yeşil, iki kez). Yani düşüş rastgele değil; tek bir
büyüklüğün — boşluğun — fonksiyonu.

## 2. Gerçek kaynak (K2)

**T0'ın hipotezi kısmen doğru, mekanizması değil.** Hipotez "iki ölçü aynı davranışın
iki tarafını ölçüyor, hangisinin kazandığını geçen süre belirliyor; yavaş koşucuda
üstteki ölçü haksız" diyordu. Duvar saatinin kök olduğu doğru. Ama iki ölçü
birbiriyle yarışmıyor ve 50 ms'lik pencere kendi başına haksız değil — çünkü
karşısındaki bütçe 100 ms.

Boşta çizim açma kararını veren tek yer `ComparisonSurface.cs:217-219`. Bu bölümdeki
bütün satır numaraları **düzeltmeden önceki** duruma, `fcf377f` commit'ine aittir;
düzeltme satırları kaydırdı.

    if (_splitMoved &&
        Stopwatch.GetElapsedTime(_lastPresentTicks) >= Stalled &&
        Stopwatch.GetElapsedTime(_lastSplitPaintTicks) >= SplitPaintInterval)

`Stalled` 100 ms (`:31`), `SplitPaintInterval` 1/60 s (`:28`). Damgaları yazan üç yer:
`:51` (alan başlatıcısı), `:234` (kare sunulunca), `:244` (`Repaint` içinde). Beşinin
de kaynağı `Stopwatch` — üretim sınıfının içinde, ölçünün göremediği ve kuramadığı bir
saat.

Ölçü ise **kendi** `Stopwatch`'unu açıyor ve iki saatin adım adım beraber yürüdüğünü
varsayıyor. Düşüş için gereken tek şey, yüzeyin `_lastPresentTicks` damgasıyla ölçünün
saat başlangıcı arasına **≥50 ms** girmesi. O aralıkta yalnız `Repaint()` →
`InvalidateVisual()` çağrısı var; yüklü koşucuda bir GC duraklaması ya da iş parçacığı
kesintisi oraya rahatça sığıyor. Sığdığı anda ölçünün 50 ms'lik penceresi yüzeyin
100 ms'lik bütçesinin **dışına** taşıyor ve çizim açılıyor.

Yani kusur "yavaş koşucu" değil, **iki bağımsız saatin karşılaştırılması**.

## 3. Değişiklik (K3)

Zaman ölçünün denetimine alındı. Üç ucuz çözümün hiçbiri yapılmadı: iddia gevşetilmedi
(tersine sıkıldı, aşağıda), pencere kısaltılmadı, `Skip` konmadı.

**Üretim tarafı — `src/VidShrink.App/Playback/ComparisonSurface.cs`:**

Yüzey artık geçen süreyi doğrudan `Stopwatch`tan almıyor; bir zaman kaynağı alıyor.

    private readonly Func<long> _now;

    public ComparisonSurface() : this(Stopwatch.GetTimestamp)
    {
    }

    internal ComparisonSurface(Func<long> now)

    private TimeSpan Since(long ticks) => Stopwatch.GetElapsedTime(ticks, _now());

Beş duvar saati okuması da bu kaynağa bağlandı: alan başlatıcısı (artık kurucuda),
karar satırlarının ikisi, kare sunumu ve `Repaint`. Üretimde varsayılan
`Stopwatch.GetTimestamp` — davranış aynı, tek fark saatin nereden geldiği.

**Ölçü tarafı — `tests/VidShrink.Tests/SplitDragTests.cs`:**

`SahteSaat` elle ilerletiliyor; ölçüde tek bir `Stopwatch.StartNew` ya da `Sleep`
kalmadı. Süre artık turla birlikte, adım adım ilerliyor:

    saat.Advance(TimeSpan.FromMilliseconds(0.2));
    surface.Split = ...;
    Round(surface);

**İddia gevşemedi, sıkıldı.** `Kare_gelmezken_...` ölçüsü eskiden bir bant tutuyordu:

    Assert.True(repaints >= 1, ...);
    var ceiling = (int)Math.Ceiling(seconds * 60) + 2;
    Assert.True(repaints <= ceiling, ...);

Şimdi tek bir tam sayı tutuyor — 250 ms boşta, 0,5 ms adımla, 60 Hz tavanı:

    Assert.Equal(500, moves);
    Assert.Equal(15, repaints);

Ölçülerin süresi 2 s'den **375 ms**'ye düştü; artık gerçek zaman beklenmiyor.

Bir uyarı: `Assert.Equal(495, moves)` ve `Assert.Equal(500, moves)` **davranış
ölçmüyor** — döngü aritmetiğinden çıkan sabitler. İşleri düzeneği çivilemek: adım
büyüklüğü ya da pencere değişirse bu sayı da değişir ve çizim beklentisinin yeniden
hesaplanması gerektiği görülür. Davranışı ölçen iddialar `repaints` üstündekiler.

## 4. İki yönlü mutasyon (K4)

Üçü de üretim kodunda, ölçüye dokunmadan yapıldı.

**(a) Boşta HER turda çizim açsın** — koşul `if (_splitMoved)`e indirildi:

    Başarısız SplitDragTests.Kare_arasindaki_bos_turda_ayirici_cizim_acmaz
      Expected: 0
      Actual:   495
    Başarısız SplitDragTests.Kare_gelmezken_ayirici_cizimi_ekran_araliginda_bir_kez_acar
      Expected: 15
      Actual:   500
    Başarısız! - Başarısız: 2, Başarılı: 3, Toplam: 5

**(b) Boşta HİÇ çizim açmasın** — `Stalled` erişilemez yapıldı (`TimeSpan.FromDays(1)`):

    Başarısız SplitDragTests.Kare_gelmezken_ayirici_cizimi_ekran_araliginda_bir_kez_acar
      Expected: 15
      Actual:   0
    Başarısız! - Başarısız: 1, Başarılı: 4, Toplam: 5

İlk denemede mutasyon (b) `_splitMoved &&` → `false &&` olarak yazılmıştı; derleme
`CS0414` ile durdu (alan atanıyor ama okunmuyor), yani o mutasyon ölçüye hiç
ulaşmadı. Sabitin kendisini bozmak aynı davranışı derlemeyi kırmadan üretiyor.

**(c) Tavan 60 Hz yerine 50 Hz** — yeni iddianın eskisinden **daha sıkı** olduğunun
kanıtı:

    Başarısız SplitDragTests.Kare_gelmezken_ayirici_cizimi_ekran_araliginda_bir_kez_acar
      Expected: 15
      Actual:   13

Eski bant iddiası bu mutasyonu **kaçırırdı**: 13 hem `>= 1` hem `<= 17` idi. Yani
düzeltme ölçüyü kararlı yaparken zayıflatmadı, kuvvetlendirdi.

## 5. Kardeş ölçüler (K5)

`Kare_gelmezken_...` duvar saatine bağlıydı — hem 150 ms'lik ısınma döngüsü, hem
250 ms'lik ölçüm penceresi, hem de tavanı gerçek geçen süreden hesaplıyordu
(`seconds * 60`). Sahte saate alındı; yukarıdaki mutasyon çıktıları onun.

`Akan_karede_ayirici_fazladan_cizim_actirmaz` **temiz** — ve bu artık bakışla değil
sayıyla kanıtlanıyor. Süreye bakan tek dal boş tur dalının içinde; ölçü her turda kare
beslediği için o dala hiç girilmiyor. Ölçü bunu iddia ediyor:

    Assert.Equal(0, repaints.Item2);   // surface.IdleRounds

`IdleRounds` sıfırsa saat okuyan satır hiç koşmamıştır. Kalan iki ölçü
(`Ayirici_yazilan_konuma_gider_ve_uclarda_kirpilir`,
`Yarim_pikselden_kucuk_kayma_birikir_klavye_adimi_gecer`) yalnız `Split` yazıcısını
sınıyor; o yazıcıda saat okuması yok (`Math.Clamp` ve `Bounds.Width` üstünden yarım
piksel eşiği).

## 6. CI (K6)

Yerel yeşille teslim edilmedi; dalın CI koşumu görülerek yazıldı.

| koşum | commit | sonuç |
|---|---|---|
| `33601659638` | `c3da7a9` (düzeltmenin kendisi) | success |
| `33601866470` | `7a2ca56` (dal başı) | success |

Dal başındaki koşumun tam süiti:

    Passed!  - Failed: 0, Passed: 1129, Skipped: 105, Total: 1234, Duration: 9 m 41 s
      - VidShrink.Tests.dll (net8.0)

Aynı ağaç yerelde de tam süitle koşturuldu (paylaşımlı makine, 22 dk 32 sn):

    Başarılı!  - Başarısız: 0, Başarılı: 1213, Atlanan: 23, Toplam: 1236

İki koşumun toplamları tutmuyor (yerel 1236, CI 1234) ve atlanan sayıları çok farklı
(23 / 105). **Sebebi ölçülmedi.** Ortam geçitleri akla yakın bir açıklama ama
doğrulanmadı; buraya tahmin yazılmıyor. Her iki koşumda da kırmızı yok.

`SplitDragTests` bu koşumda kırmızı değil — üç kez düşen ölçü artık düşmüyor. Tek
yeşil koşum "kararsızlık bitti"nin kanıtı değildir; aşağıda ölçülmeyenler arasında.

## 7. Ölçülmeyenler

- **Kararsızlığın gerçekten bittiği ölçülmedi.** Elde tek bir yeşil CI koşumu var
  (`33601866470`). Ölçü artık duvar saatini hiç okumadığı için mekanizma olarak
  düşemez, ama bu bir çıkarım — tekrarlanan koşumla sınanmadı.
- **Bu ölçünün geçmişte tam olarak kaç kez düştüğü ölçülmedi.** Üç bilinen vaka var
  (T111, T116, T123 — koşum 33597779584), taranmış bir sayı değil. Kapalı dalların CI
  kaydı geriye dönük taranmadı.
- **Yerel makinede ölçünün doğal düşme oranı ölçülmedi.** Düşüş yapay olarak
  üretildi; "bu makinede yüz koşumda kaç kez düşer" sorusu sorulmadı.
- **Düşüşü CI'da hangi olayın ürettiği ölçülmedi.** Boşluğa neyin girdiği (GC
  duraklaması, JIT, iş parçacığı kesintisi) saptanmadı — yalnız ≥50 ms'lik bir
  boşluğun yettiği gösterildi. Düzeltme boşluğun kaynağından bağımsız olduğu için
  bu soru açık bırakıldı.
- **`SplitDragTests` dışındaki ölçüler kısmen ölçüldü.** Tarama yapıldı: test
  kaynağında duvar saati okuması (`Stopwatch`, `Thread.Sleep`, `Task.Delay`,
  `DateTime.Now/UtcNow`) geçen **16** dosya var. Bunlardan **beş iddia** doğrudan
  duvar saati okumasına bakıyor:

      tests/VidShrink.Tests/PerformanceCheckTests.cs:554
      tests/VidShrink.Tests/PerformanceCheckTests.cs:693
      tests/VidShrink.Tests/PerformanceCheckTests.cs:764
      tests/VidShrink.Tests/UpdaterTests.cs:316
      tests/VidShrink.Tests/UpdaterTests.cs:1173

  Bu beşinin gerçekten kararsız olup olmadığı **ölçülmedi** ve düzeltilmedi — ikisi de
  bu sözleşmenin `owns`u dışında. `PerformanceCheckTests.cs:693` en geniş bandı tutuyor
  (`Assert.InRange(saat.ElapsedMilliseconds, 1500, 15_000)`); ayrı bir sözleşmeye
  değer. Kalan 11 dosyadaki duvar saati okumasının iddiaya girip girmediği
  sınıflandırılmadı.
- **Süre ölçülmedi.** Makine paylaşımlı, on beşe yakın ajan koşuyor; bu belgedeki
  375 ms / 2 s karşılaştırması aynı koşulda arka arkaya alındı ama yük denetlenmedi.
