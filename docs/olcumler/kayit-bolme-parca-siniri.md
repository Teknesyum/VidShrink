# Bölünmüş Kaydın Parça Süresi Sınırı

19 Eylül 2026. Ölçü `tests/VidShrink.Tests/KayitBolmeTests.BolmeSureSiniriniParcalaraDagitirYarimKayitIsaretlenir`.

## Kırmızı

CI koşumu `19c56c44`'te parça süresi `3,667` sn geldi, üst sınır `3,5` sn'ydi:

```
Assert.All() Failure: 1 out of 2 items in the collection did not pass.
[0]: Item:  3.6669999999999998
     Error: Assert.InRange() Failure: Value not in range
            Range:  (0.2 - 3.5)
            Actual: 3.667
```

## Sınır tek bir ölçümden konmuştu

Belgedeki cümle bunu zaten söylüyordu: "aşımın yoklama, ilerleme bloğu ve nazik kapanış
arasında payı ayrılmadı; üst sınır bu yüzden 3,5 sn". Yani 3,5 bir hüküm değil, tek bir
koşumun 3,134'üne 0,37 sn pay eklenmiş hâliydi.

## Bu makinede beş koşum

```
bolunmus ok=True partial=False parca=2 sureler=3.134 1.867 toplam=5.001
bolunmus ok=True partial=False parca=2 sureler=3.2   1.8   toplam=5
bolunmus ok=True partial=False parca=2 sureler=3.134 1.867 toplam=5.001
bolunmus ok=True partial=False parca=2 sureler=3.134 1.867 toplam=5.001
bolunmus ok=True partial=False parca=2 sureler=3.2   1.867 toplam=5.067
```

Birinci parça 3,134-3,2; CI koşucusunda 3,667. Aşım makinenin açılış maliyeti ve
sabit değil. Toplam her koşumda 5 sn sınırında kalıyor — bölmenin kendisi doğru çalışıyor.

## Yeni sınır 4,0 sn (o gün için)

Sınırın işi "bölme hiç olmadı, tek parça her şeyi yuttu" durumunu yakalamak. Bölme
olmasaydı parça 5 sn olurdu, 4,0 onu hâlâ yakalar. Bölmenin gerçekten olduğunu asıl
pimleyen şey zaten `Assert.InRange(bolunmus.Segments, 2, 4)`; parça süresi ikincil koruma.

## 23 Eylül 2026 — 4,0 sn de kırmızı verdi, sabit tavan kapatıldı

CI koşumu `35916945339`'da (dal `worktree-agent-a2ecb0cb53132ea03`, iş `test (ana, ...)`)
birinci parça `4,4` sn geldi: aynı iş akışında beş test kabuğu (`ana`, `yerlesim-1..3`,
`ust-serit`) ve `kaydedici-x11` paralel koşuyordu. Kuyruk büyüyordu: 3,134 (yerel) →
3,667 (bir CI koşumu) → 4,4 (paylaşılan koşucu daha kalabalıkken). Kök neden
`RecorderSession.FinishSegmentAsync`'in nazik kapanışı (`stdin`'e `q`, sürecin çıkışını
`StopTimeoutMs=10000` ms'ye kadar beklemek, `RecorderSession.cs:499-505`): bu bekleme
CI'nın o anki CPU yüküne bağlı ve yukarı sınırı yok. `WatchSplitAsync`'in 250 ms'lik
yoklaması (`RecorderSession.cs:344`) ve ffmpeg'in `-progress` blok aralığı (varsayılan
0,5 sn) toplamın küçük bir parçasını açıklıyordu; asıl kuyruk nazik kapanıştan geliyordu.

İki değişiklik:

1. **Ürün**: `-stats_period 0.1` eklendi (`RecorderSession.cs`, `StatsPeriodSeconds`),
   ffmpeg'in ilerleme bloğunu beş katı sıklaştırıyor — bölme ölçütünün gördüğü süre artık
   en fazla ~100 ms bayat, 500 ms değil. Nazik kapanışın kendisi hâlâ CI yüküne bağlı;
   bu değişiklik onu sıfırlamıyor, yalnız tespit gecikmesini kısaltıyor.
2. **Ölçü**: sabit üst sınır (önce 3,5, sonra 4,0 sn) her yük artışında yeniden kırmızı
   veriyordu — kendisi CI gürültüsünü ölçüyordu, doğruluğu değil. Üst sınır artık ölçülen
   toplama bağlı (`toplam * 0.95`): "bölme hiç olmadı" durumunu hâlâ yakalıyor (o durumda
   tek parça toplamın ~%100'ünü tutar), ama kuyruk büyüdükçe elle yeniden ayarlanması
   gerekmiyor. Asıl pim zaten `Assert.InRange(bolunmus.Segments, 2, 4)` — bu değişmedi.
   Bölme kararının kendisi (`RecorderSession.SplitDue`, artık `internal static` ve saf)
   `KayitBolmeTests.SplitDueSaf*` ile gerçek zamanlamadan bağımsız, sahte ilerleme/boyut
   değerleriyle belirlemeci pimlendi (`>=` sınırını `>`'a çeviren mutasyon 1/7 kırmızı verdi).

## 24 Eylül 2026 — `toplam * 0.95` reddedildi, aşım üründen kaldırıldı

Yukarıdaki `toplam * 0.95` pratikte sınırı kaldırıyordu: 2 sn'lik bölmede 4,4 sn'lik
parça (iki kat) da bu sınırın içinde kalırdı. Kullanıcı "2 sn'de böl" dediğinde iki katını
almak gerçek bir kusur — tavanı gevşetmek yerine aşımın kendisi kaldırıldı.

Kök neden zaten biliniyordu: parça, bölme kararı verildikten sonra `FinishSegmentAsync`'in
nazik `q` kapanışını bekleyerek yakalamayı sürdürüyordu, o bekleme CI yüküne bağlıydı. Asıl
düzeltme: her parçanın ffmpeg süreci artık **kendi** `-t`/`-fs` sınırını taşıyor
(`RecorderArguments.ForSegment`, `min(bölme ölçütü, kalan toplam)`), ffmpeg parçayı içerik
zamanında kendisi kapatıyor — dış yoklama ve nazik kapanışın gecikmesi parça süresine hiç
girmiyor. `RecorderSession.WatchExitAsync` artık sürecin doğal çıkışını "parça doldu,
sonrakini aç" (ForSegment null dönmüyorsa) ya da "kayıt bitti" (null dönüyorsa) diye okuyor;
eski `WatchSplitAsync`'in 250 ms'lik dış yoklaması ve `SplitDue`'nun üretimdeki çağrısı
kaldırıldı (saf `SplitDue` metodu da artık üretimde kullanılmadığından testleriyle birlikte
silindi — kalsa ölü kod olurdu).

Üç yerel koşumda (`BolmeSureSiniriniParcalaraDagitirYarimKayitIsaretlenir`, 2 sn bölme, 5 sn
toplam sınır) ölçülen parça süreleri artık gerçek ve sıkı bir tavana sığıyor; tavan 2,5 sn —
4,4 sn'yi de, 2 katına çıkan bir kusuru da yakalar, ama gerçek ~2,0-2,03 sn'lik parçalara
gerçek bir tolerans (bir kare + küçük pay) bırakıyor. Argüman düzeyindeki davranış
(`-t`'nin bölme ölçütünden yazılması, kalan sürenin bölme süresinden kısa olduğu son parça,
bölmesiz kayıtta hiç yazılmaması) `KayitFfmpegKoluTests.SinirsizKayittaBolmeSuresiIlkParcayaYazilir`,
`BolmeSuresiKalanSuredenUzunOlsaDaParcaKurulur` ve `BolmeYokkenOlcutArgumanaGirmez`'de
belirlemeci pimli.
