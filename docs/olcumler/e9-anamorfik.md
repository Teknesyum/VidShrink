# E9 — Anamorfik Kaynak, Piksel En-Boy Oranı (19 Eylül 2026)

## Bulgu

`grep -rn "sample_aspect_ratio\|setsar\|display_aspect" src tests tools` → **0 satır**.
Depo piksel en-boy oranını (PAR) hiçbir yerde okumuyordu; `MediaInfo.Width` ffprobe'un
`width` alanıydı, yani depolanan genişlik. Kare piksel varsayımı DVD dışında doğru,
DVD'de değil.

E4 DVD kolunu açtı. 720x480 NTSC kaynak SAR 8:9 ile 640x480 (4:3), SAR 32:27 ile 853x480
(16:9) gösterilir; ikisi de 720x480 sayılınca ölçek merdiveni yanlış orandan iniyor ve
çıktı yassı kodlanıyordu. HandBrake'in `--non-anamorphic`, `--auto-anamorphic`,
`--itu-par` ve `--keep-display-aspect` bayraklarının dördü de bu tek eksikte birleşiyor
(`docs/handbrake/bayrak-hukumleri-2026-09-19.md`).

## Karar

HandBrake'in `--non-anamorphic` davranışı alındı: yükseklik korunur, genişlik gösterim
genişliğine çevrilir, çıktı **kare pikselli** olur. Anamorfik metadata taşıyan çıktı
üretmiyoruz — küçültme aracının çıktısı paylaşılacak dosya, oynatıcı uyumu kare pikselde
daha yüksek.

## Açılan yüzey

| Yüzey | Yer |
| --- | --- |
| `ParNum` / `ParDen` / `IsAnamorphic` / `DisplayWidth` | `src/VidShrink.Core/MediaInfo.cs` |
| `sample_aspect_ratio` okuması, çeyrek dönüşte ters çevirme | `src/VidShrink.Ffmpeg/FfprobeClient.cs` (`PikselOrani`) |
| Ölçek merdiveni gösterim genişliğinden | `src/VidShrink.Core/PlanCalculator.cs` (`Dimensions`) |
| `setsar=1`, küçültme yolu | `src/VidShrink.Core/VideoFilterChain.cs` (`SquarePixelFilter`) |
| `setsar=1`, dönüşüm yolu | `src/VidShrink.Core/ConversionArguments.cs` |
| Ölçü | `tests/VidShrink.Tests/AnamorfikTests.cs` (6 kol) |

`setsar=1` neden şart: ölçek süzgeci kaynağın SAR metadata'sını olduğu gibi taşır.
Sıfırlanmazsa oynatıcı zaten gösterim genişliğine ölçeklenmiş kareyi ikinci kez esnetir.

## Ölçüm sırasında çıkan şey

ffmpeg 9.0 `-metadata:s:v:0 rotate=90` etiketini artık dosyaya geçirmiyor: üretilen mp4'te
ne `tags.rotate` ne de `side_data_list` çıkıyor. Dönüş kolu ilk turda bu yüzden kırmızıydı.
Görünüm matrisi ikinci bir kopyalama geçişiyle `-display_rotation 90` verilerek yazılıyor;
ffprobe o zaman `side_data_list[].rotation = 90` bildiriyor.

## Mutasyon turu

| # | Kesim | Kırmızı |
| --- | --- | --- |
| Taban | — | 0/6 |
| M1 | PAR hiç okunmuyor | 2/6 |
| M2 | Dönüşte oran ters çevrilmiyor | 1/6 |
| M3 | Gösterim genişliği depolanan genişlik | 2/6 |
| M4 | Merdiven yine depolanan genişlikten iniyor | 1/6 |
| M5 | Küçültme zincirine `setsar` girmiyor | 1/6 |
| M6 | Dönüşüm yolunda `setsar` girmiyor | 1/6 |
| Geri | — | 0/6 |

Altı kesimin altısı da kırmızı: kör nokta yok. İki tur koştu.

**Tur 1 çöpe gitti:** sürücü E8'den kopyalanırken `shutil.copy2` düzeltmesi taşınmamıştı,
geri alınan dosya derlenmiş dll'den eski görünüp MSBuild derlemeyi atlıyordu; `geri` kolu
2/6 kırmızı okununca tablo yazılmadan geçersiz sayıldı. `shutil.copy` + `os.utime` ile
düzeltildi.

**Tur 2'de taban kirli başladı:** `yedekle()` orijinal dosyalara dokunmadığı için tur 1'den
kalan mutasyonlu ikililer taban koşumunda hâlâ diskteydi ve taban 2/6 kırmızı okundu.
Kaynaklar `os.utime` ile tazelenip yeniden derlenince taban **0/6** ölçüldü; sürücüye
`yedekle()` sonrası bir `geri_al()` eklendi. Aradaki M kolları her turda `geri_al()`'dan
sonra koştuğu için bu kusurdan etkilenmedi.

Sürücü `.calisma/e9/mutasyon.py`, ham çıktı `.calisma/e9/sonuc.txt`.
