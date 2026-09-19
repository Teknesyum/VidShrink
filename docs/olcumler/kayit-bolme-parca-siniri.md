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

## Yeni sınır 4,0 sn

Sınırın işi "bölme hiç olmadı, tek parça her şeyi yuttu" durumunu yakalamak. Bölme
olmasaydı parça 5 sn olurdu, 4,0 onu hâlâ yakalar. Bölmenin gerçekten olduğunu asıl
pimleyen şey zaten `Assert.InRange(bolunmus.Segments, 2, 4)`; parça süresi ikincil koruma.
