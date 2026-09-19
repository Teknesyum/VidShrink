# Zaman Damgası Hizalaması: Ölçülen Kayma Rapora Bağlandı (19 Eylül 2026)

## Bulgu

`TimestampAlignment` kaynak ve test dosyasının video akışı başlangıçları arasındaki farkı
ölçüyordu; `QualityMeter` her ölçümde dolduruyor, `QualityScore.Alignment` taşıyordu. Üretimde
okuyan tek yüzey yoktu. `OluUyeTests` bunu iki satırda pimliyordu:
`QualityScore.Alignment  yalniz-disarida` ve `TimestampAlignment.Note  yalniz-disarida`.

Sonuç: VMAF kareleri zaman damgasına değil kare indeksine eşlediğinde kullanıcı bunu hiçbir
yerde görmüyordu. 20 ms'lik bir kayma kalite puanını düşürür ve sebebi görünmez kalır.

## Karar

Kayma CLI'ın hem metin hem JSON raporuna girer, **yalnız kayma varken**. Sessiz durumda
satır yok: her ölçümde görünen bir uyarı gürültüdür.

`TimestampAlignment.Note` kaldırıldı. Ffmpeg katmanında gömülü Türkçe cümleydi; yerine
`ShiftMilliseconds` sayısı kondu ve metin iki dilde `result.alignment` anahtarından kuruluyor.
Böylece hem ölü üye kapandı hem katmanda kalan bir sabit Türkçe metin düştü.

## Açılan yüzey

| Yüzey | Yer |
| --- | --- |
| `ShiftMilliseconds` (Note'un yerine) | `src/VidShrink.Ffmpeg/QualityMeter.cs` |
| Metin raporu hizalama satırı | `src/VidShrink.Cli/CliApp.cs` (`ShrinkText`) |
| JSON raporu `result.alignment` nesnesi | `src/VidShrink.Cli/CliApp.cs` (`ShrinkJson`) |
| `result.alignment` kalıbı, tr + en | `src/VidShrink.Cli/Locales/{tr,en}.json` |
| Ölçü | `tests/VidShrink.Tests/HizalamaRaporuTests.cs` (6 kol) |

`ShrinkText` ve `ShrinkJson` `private`'ten `public`'e çıktı; `PlanText`/`PlanJson` zaten
öyleydi, rapor üreticileri artık aynı görünürlükte.

## Ölçüm sırasında çıkan şey

İlk yazımda kapı `vmaf?.Alignment is { Shifted: true } alignment` özellik deseniydi. Ölü üye
tarayıcısı özellik desenini okuma saymıyor ve `TimestampAlignment.Shifted` pimlenmemiş ölü üye
olarak çıktı. Üye gerçekten okunuyordu; pim eklemek yanlış olurdu. Kapı
`is { } alignment && alignment.Shifted` biçimine çevrildi, tarayıcı susturuldu.

**Ders:** ölü üye tarayıcısı beklenmedik bir üye gösterdiğinde önce o üyenin gerçekten
okunmadığını doğrula. Pim, tarayıcının kör noktasını kaydetmek için değil.

## Mutasyon turu

| # | Kesim | Kırmızı |
| --- | --- | --- |
| Taban | — | 0/6 |
| M1 | Metin raporunda hizalama satırı yok | 1/6 |
| M2 | JSON raporunda milisaniye sıfır yazılıyor | 1/6 |
| M3 | Kayma eşiği her zaman doğru | 2/6 |
| M4 | Milisaniye çevrimi yok | 2/6 |
| M5 | tr kalıbında kare yer tutucusu yok | 1/6 |
| Geri | — | 0/6 |

Beş kesimin beşi de kırmızı; taban ve geri kolları 0/6. Sessiz kol (kayma yokken satır
çıkmaması) M3 ile, çevrim M4 ile, iki dilin kalıbı M5 ile pimli.

Sürücü `.calisma/hizalama/mutasyon.py`, ham çıktı `.calisma/hizalama/sonuc.txt`.
