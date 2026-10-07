# Bekleyen Yuvası Yoklama Yarışı: Sahte Uygulama Bekleyeni Kapıdan Çeviriyordu

`main` CI koşumu `37254025970` tek testte kırmızıydı:
`BaslaticiPanelsizTests.ArkaPlanBaslaticisiBeklerkenYukleHizliYoldanAcar`,
`"yuva-dolu"` beklenirken `"yuva-bos"`.

Kusur üründe değil, düzenekte: `tools/VidShrink.SahteUygulama/Program.cs`.

## Kusur

Sahte uygulamanın `Yukle`'si, arka plan başlatıcısının kapıya geldiğini anlamak için
20 ms'de bir bekleyen yuvasını yokluyordu:

```csharp
while (... && !(Tutuluyor(KurulumBekleyeni.Ad(appDirectory)) && File.Exists(muhur) && !Tutuluyor(UpdateStaging.MutexName)))
    Thread.Sleep(20);
```

`Tutuluyor` boş muteksi **alıp bırakıyor** (`WaitOne(0)` → `ReleaseMutex`). Başlatıcı
tarafında `KurulumBekleyeni.Calistir` aynı yuvayı arka planda sıfır bekleme payıyla istiyor
(`Tut(bekleyen, YuvaBeklemesi(elle))`, arka planda 0 ms). Yoklama tam o ana denk gelirse
başlatıcı "yuva dolu, başka bekleyen var" görüp çekiliyor.

Bekleyen çekilince yuva bir daha hiç dolmuyor: döngü 20 sn'lik sınırına kadar dönüyor,
basış anındaki tek yoklama `yuva-bos` yazıyor. CI imzası da bu: dokuz tur ~2,2 sn, onuncu
tur 20 sn.

## Kanıt

Yarış penceresi mikrosaniyelik; yerelde kendiliğinden çıkmıyor. Pencere geçici olarak
genişletildi: `Tutuluyor` içinde `WaitOne` ile `ReleaseMutex` arasına `Thread.Sleep(15)`.

| Düzenek | Sonuç |
|---|---|
| Eski döngü + 15 ms'lik pencere | 2. turda `Expected: "yuva-dolu"`, `Actual: "yuva-bos"`; test 22 sn |
| Yeni döngü + aynı 15 ms'lik pencere | 10/10 tur `hizli`, ortanca 50 ms (49-53); test 21 sn |

İkinci satır aynı zamanda mutasyon: düzeltme geri alınınca kusur aynı iletiyle geri geliyor.
Genişletme ölçümden sonra kaldırıldı.

## Düzeltme

Döngü yuvayı yoklamıyor. Sahne mührü ancak bekleyen yuvayı tutarken yazılıyor, güncelleme
kilidi de indirme bitince bırakılıyor; mühür var ve kilit boşsa bekleyen kapıdadır:

```csharp
while (... && !(File.Exists(muhur) && !Tutuluyor(UpdateStaging.MutexName)))
    Thread.Sleep(20);
```

Basış anındaki tek `Tutuluyor(KurulumBekleyeni.Ad(...))` yoklaması duruyor; o anda bekleyen
yuvayı çoktan almış olduğu için yarışmıyor.

Güncelleme kilidinin yoklanması aynı biçimde alıp bırakıyor ve eski döngüde de vardı.
Mühür yokken hiç koşmuyor (`&&` kısa devresi), yani bekleyenin sıfır paylı indirme kilidi
isteğiyle çakışmıyor; mühürden sonra bekleyen kilidi kapının ardından, süreli istiyor
(`docs/olcumler/bekleme-butceleri.md`). Bu kol ayrıca ölçülmedi.
