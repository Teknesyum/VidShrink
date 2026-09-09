# Kırk İki Dil — Denetim

Kaynak dil `en`: 488 anahtar, dört dosya (main 391, performance 31, playback 28, settings 38).
Her dil klasörü aynı dört dosyayı, aynı anahtarlarla ve aynı sırada taşır.

Denetimi üreten düzenek: `tools/dil-denetim.py`. Sütunlar sırasıyla anahtar sayısı,
İngilizcede olup dilde olmayan, dilde olup İngilizcede olmayan, yer tutucu kümesi
uymayan anahtar sayısı, İngilizcesiyle birebir aynı kalan değer sayısı.

`ceviri-yok` sütunundaki tek haneli sayılar marka/teknik terimlerdir (`Plan`, `H.264
(compatible)`, `Codecs` gibi) — o dilde de aynı yazıldığı için bilinçli bırakıldı.

## Koşum

```
ar         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
bg         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
bn         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
cs         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
da         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 1
de         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 1
el         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
es         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 1
et         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
fa         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
fi         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
fr         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 1
he         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
hi         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
hr         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
hu         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 1
id         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 1
it         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
ja         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
ko         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
lt         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
lv         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
ms         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
nb         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
nl         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 2
pl         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
pt         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
ro         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
ru         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
sk         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
sl         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
sr         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
sv         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 2
sw         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
ta         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
th         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
tr         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
uk         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
ur         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
vi         TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
zh-Hans    TAMAM  anahtar 488/488  eksik 0  fazla 0  yertutucu 0  ceviri-yok 0
```

## Testler

`dotnet test --filter "FullyQualifiedName~LocalizationTests|FullyQualifiedName~LanguageTests"`
→ **Başarılı: 127, Başarısız: 0.**

Tam koşum (`dotnet test`, 20 dk 22 sn) → **Başarılı: 1935, Atlanan: 18, Başarısız: 1.**
Tek kırmızı `OynaticiBoruTests_DecoderPipe.Oldurulemeyen_surec_icin_KillTree_basarisiz_bildirir`;
bu dil işinden önce de kırmızıydı, `Process.HasExited` bu makinede "Erişim engellendi" atıyor.

## Maliyet

Dil başına ortalama **~93 bin belirteç** (dört dosyayı okuyan ve yazan tek ajan).
Kırk dil için toplam ölçülen: **~3,7 milyon belirteç**, sekizerli beş dalga.

Dördüncü dalga oturum kotasına çarptı: sekiz ajan da 429 aldı, `bg`/`el`/`hu` yine de
tamamlanmıştı, yarım kalan `ro` silinip yeniden çevrildi.

## Sağdan sola

`Strings.RightToLeftLanguages` = `ar fa he ur`. Pencerenin `FlowDirection`'ı bu listeden
kararlaştırılıyor: `MainWindow` kurulumda ve her dil değişiminde, `ShrinkJobWindow`
açılışta.

```
dotnet test --filter "FullyQualifiedName~LocalizationTests|FullyQualifiedName~LanguageTests"
Başarılı!  - Başarısız: 0, Başarılı: 138, Atlanan: 0, Toplam: 138, Süre: 20 s
```
