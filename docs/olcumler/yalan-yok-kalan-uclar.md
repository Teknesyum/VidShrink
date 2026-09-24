# Yalan Yok: Kalan Uçlar

Tarih 2026-09-24, dal `worktree-agent-abf276c657d08065e`. Beş iddia satırında açıldı, ölçüldü, sonra düzeltildi.
Test sınıfı `tests/VidShrink.Tests/YalanYokKalanUclarTests.cs`; kaydedici kolu `KaydediciUyariTests` ve `KaydediciDurustlukTests`.

## Hüküm

| # | İddia | Hüküm | Yer |
|---|---|---|---|
| 1 | 38 dilde tavan üstü metni "asla" diyor | Doğrulandı | 38 × `Locales/<dil>/main.json` `main.run.over-ceiling` |
| 2 | İzleri koru açıkken görüntü altyazı notu "İzleri koru'yu açın" diyor | Doğrulandı | `src/VidShrink.Core/StreamMapping.cs:609` |
| 3 | CLI `.mkv` çıktıda ses satırı yalan | Çürüdü (`.mkv`); aynı yalan `.webm`'de var | `src/VidShrink.Cli/CliRequest.cs:228` |
| 4 | CLI `.webm` + VP9 düşüşü | Doğrulandı | `src/VidShrink.Core/PlanCalculator.cs:540,547`, `src/VidShrink.Cli/CliApp.cs:229,385` |
| 5 | Okunamayan yarım MKV kaydı "MKV seçin" diyor | Doğrulandı | `src/VidShrink.App/Recorder/RecorderView.axaml.cs:220` |

## 1. Tavan Üstü Metni

Motor tavanı aşan sonucu teslim etmiyor ama kullanıcı hedefi yükseltip yeniden deneyebiliyor; "asla verilmez" cümlesi
bu yolu kapalı gösteriyordu. ar, de, en, tr zaten "daha büyük sonuç kabul edilmedi" diyordu; öteki 38 dil "asla" diyordu.
Ölçüm dökümünde bg örneği: "защото файл над целта никога не се връща".

Ek bulgu: lt, nl, sk'de `{0}` ile `{1}` yer değiştirmişti — deneme sayısı MB'ın yanına, hedef deneme sayısının yanına düşüyordu.
Üçü de doğru sıraya alındı.

- `TavanUstuMetniAslaDemez`: her dilin "asla" sözcükleri harf sınırıyla aranır (ja/zh-Hans/th düz içerme). Olumsuz kontrol: eski lt metni ve İngilizce örnek yakalanır.
- `TavanUstuYerTutuculariDogruBirimde`: `{0}` ve `{2}` MB'ın yanında, `{1}` değil. Olumsuz kontrol eski lt metni.

## 2. İzleri Koru Açıkken Görüntü Altyazısı

İzleri koru açık, kap MP4/MOV seçili: PGS düşüyor ve not "İzleri koru'yu açın, MKV'de taşınsın" diyordu — kutu zaten açıktı.
Yeni not `ImageSubtitleDroppedByContainer` (`image-subtitle-dropped-by-container`), ses kolundaki `extra-audio-dropped-by-container`
ile aynı biçimde: "kutu açık ama seçilen kap taşımıyor". 42 `main.json` ve CLI en/tr çevrili.

- `IzleriKoruAcikkenGoruntuAltyaziNotuKabiSoyler`: kutu açık → yeni not; kutu kapalı → eski not (olumsuz kontrol).

## 3-4. CLI Çıktı Uzantısı ve VP9 Düşüşü

Ölçüm matrisi: 4 kaynak ses × 4 uzantı × Otomatik/Vp9 × VP9 var/yok, plan ile üretilen argüman yan yana. İlgili satırlar (düzeltmeden önce):

```
aac/96k   mkv   Auto  vp9=True   planSes=copy     -c:a=copy     kap=Mkv   cikti=x.mkv
aac/96k   webm  Auto  vp9=True   planSes=copy     -c:a=libopus  planVideo=libsvtav1  kap=Mp4  cikti=x.webm
aac/96k   webm  Vp9   vp9=False  planSes=copy     -c:a=libopus  planVideo=libx264    kap=Mp4  cikti=x.webm  notlar=AudioPassthrough,Vp9FellBackToMp4
aac/96k   mkv   Vp9   vp9=False  planSes=copy     -c:a=copy     planVideo=libx264    kap=Mkv  cikti=x.mkv   notlar=AudioPassthrough,Vp9FellBackToMp4
```

- `.mkv`: plan ile argüman her satırda aynı. İddia çürüdü.
- `.webm`: plan MP4 kabıyla kuruluyordu (`PresetLibrary.DeliveredContainer` WebM için null), argüman kurucusu uzantıdan WebM görüyordu. Plan "ses kopyalanır" derken ffmpeg libopus yazıyordu.
- `.webm` + VP9 yok: libx264 `x.webm` içine yazılıyordu, not "MP4'e düştü" diyordu. Dosya ne MP4 ne geçerli WebM.
- `.mkv`/`.mov` + VP9 yok: not yine "MP4'e düştü" diyordu, kap MKV/MOV'du.

Düzeltme:
- `CliRequest.cs:228`: `--cikti` uzantısının kabı plana doğrudan iner.
- `PlanCalculator.cs:540`: WebM teslimi yalnız kodek WebM'e sığıyorsa (`CodecModel.FitsWebM`: vp8/vp9/av1) tutulur, yoksa kap kodekten türer.
- `PlanCalculator.cs:547`: düşüş notu MP4'te `Vp9FellBackToMp4`, başka kapta yeni `Vp9FellBack` ("çıktı WebM değil").
- `CliApp.cs:385` `WebmOrWhatCarries`: `.webm` istenip plan WebM değilse uzantı planın kabına döner; `CliApp.cs:229` bunu stderr'e yazar (`output.not-webm`).

Testler:
- `CiktiUzantisindaSesSatiriGercekKodek` (webm, mkv, mp4): plandaki ses kodeği argümandakiyle aynı.
- `WebmOtomatikKodekteOpusuPlanSoyler`.
- `Vp9DusunceWebmAdliMp4Yazilmaz`: çıktı `.webm` değil, not MP4'ü söyler.
- `Vp9DusunceSecilenKapMp4Denmez` (mkv, mov): not `Vp9FellBack`.
- `WebmUzantisiDegisinceCliSoyler`: `RunAsync` stderr'inde uzantı değişikliği yazılı; VP9'lu koşum yazmaz (olumsuz kontrol).

## 5. Okunamayan Yarım MKV

`YarimAnahtari` yalnız `Playable`'a bakıyordu; ffprobe okuyamayınca mkv kaydına da "Sonraki kayıt için MKV seçin" düşüyordu.
Yeni anahtar `recorder.output.partial-broken-mkv`: aynı metin, MKV cümlesi yok. 42 `recorder.json`. Mp4 kolu öneriyi korur.

- `KaydediciUyariTests.OkunamayanYarimMkvMkvOnermez`: metin yeni anahtar, "MKV" geçmez; eski anahtar hâlâ "MKV" içerir; oynatıcı düğmesi kapalı.
- `KaydediciDurustlukTests.OkunamayanYarimMatroskaOynatilamazDer` yeni anahtarı bekler.
- `YarimMp4OynatilamazDiyor` olumsuz kontrol: mp4'te eski metin.

## Mutasyonlar

Elle uygulandı, koşuldu, bayt bayt geri yazıldı (git checkout yok). Her mutasyon tek satırı değiştirdi (tabloda), koşum yalnız o kolun filtresiyle; döküm aşağıda.

| Mutasyon | Dosya | Sonuç |
|---|---|---|
| M1 lt metni eskiye | `Locales/lt/main.json` | 2/2 kırmızı |
| M2 not koşulsuz `ImageSubtitleDropped` | `StreamMapping.cs:609` | 1/1 kırmızı |
| M3a `--cikti` kabı yine `PresetLibrary`'den | `CliRequest.cs:228` | 2/4 kırmızı (webm kolları; mkv/mp4 yeşil kalması beklenen) |
| M3b `WebmOrWhatCarries` adı korur | `CliApp.cs:387` | 2/2 kırmızı |
| M3c not her kapta `Vp9FellBackToMp4` | `PlanCalculator.cs:547` | 2/2 kırmızı |
| M4 `false when mkv` kolu yok | `RecorderView.axaml.cs:220` | 2/2 kırmızı |

```
M1 lt tavan ustu eski metin: Failed!  - Failed: 2, Passed: 0, Total: 2
M2 goruntu altyazi notu kosulsuz: Failed!  - Failed: 1, Passed: 0, Total: 1
M3a cikti webm plana inmiyor: Failed!  - Failed: 2, Passed: 2, Total: 4
M3b webm adi korunuyor: Failed!  - Failed: 2, Passed: 0, Total: 2
M3c vp9 notu her kapta MP4: Failed!  - Failed: 2, Passed: 0, Total: 2
M4 mkv dali yok: Failed!  - Failed: 2, Passed: 0, Total: 2
```

Mutasyonsuz taban: `BaslikKapsamiTests|YalanYokKalanUclarTests|KaydediciUyariTests|KaydediciDurustlukTests|SoylenenYapilan|MovKabi|Cli` 280/280 yeşil.

## Sayım Pinleri

`BaslikKapsamiTests` iki yeni `main.reason.stream.*` anahtarıyla yeniden temellendi:
- kol toplamı 2639 → 2651, en 244 → 245, tr 90.
- gezilen 47171 → 47300 (43 × 1100; `vp9-fell-back` 12 dilde kola girer).
