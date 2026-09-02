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
(`.claude/relay/contracts/done/T116.md:274-275`, koşum `d435ff9`).

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

Boşta çizim açma kararını veren tek yer `ComparisonSurface.cs:217-220`:

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
