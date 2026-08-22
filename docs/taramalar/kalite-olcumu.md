# Kalite ölçümü taraması

Ortak bulgu: **üç deponun hiçbiri VMAF skorunu "yeterince iyi" eşiğine çevirmiyor.** Ölçer, raporlar, kararı kullanıcıya bırakır. "6 puan = JND", "hedef 93" gibi rakamlar Netflix blog yazılarından gelir, deponun belgelerinde geçmiyor (`resource/doc/*.md` grep boş) — **doğrulanamadı**. Şeffaflık tavanı VidShrink'in kendi işi.

## Depo

| Depo | Lisans | Son push | Son sürüm | Açık issue | Yıldız |
|---|---|---|---|---|---|
| Netflix/vmaf | BSD+Patent (API `NOASSERTION`, README beyanı) | 2026-08-14 | v3.2.0 · 2026-06-20 | 115 | 5457 |
| slhck/ffmpeg-quality-metrics | MIT | 2026-07-29 | v3.12.1 · 2026-07-29 | 4 | 557 |
| CrypticSignal/video-quality-metrics | MIT | 2026-05-04 | **yok** (`releases/latest` 404) | 0 | 143 |

## Ne yapıyor

**Netflix/vmaf** — libvmaf, model dosyaları, referans CLI. Kanonik ffmpeg komutu `resource/doc/ffmpeg.md`: iki girişi `setpts=PTS-STARTPTS` ile hizala, bozulmuşu `scale=W:H:flags=bicubic` ile referans çözünürlüğüne çıkar, `[distorted][reference]libvmaf=log_fmt=xml:log_path=...:model_path=...:n_threads=4`, çıkış `-f null -`. Sıra **bozulmuş önce, referans sonra**. Windows `model_path` kaçışına ayrı başlık: ters bölü → bölü, iki nokta `\\:` (`D\\:/mypath/vmaf_v0.6.1.json`).

2026-06'da v1 kuşağı (`vmaf_v1.0.16_*`): 1080p/3H, telefon/5H, 4K/1.5H [0,100], 4K/3H [0,110]. v1 **libvmaf > 3.2.0** ve 10-bit ölçüm ister; CAMBI'ye kodlama boyutları `cambi.enc_width/enc_height/enc_bitdepth` ile verilebilir.

**slhck/ffmpeg-quality-metrics** — tek ffmpeg çağrısında PSNR/SSIM/VMAF/VIF/MSAD. Referansı `settb=AVTB,setpts=PTS-STARTPTS` ile hizalar, `split=2` ile ikiye ayırır, bozulmuşu `scale=rw:rh` ile referans boyutuna getirir (`scale2ref` ffmpeg 7.1'de kalktı). libvmaf'a `shortest=1:repeatlast=0` ekler; koddaki gerekçe: framesync varsayılanı kısa akışın son karesini tekrarlar, donmuş kare fazla referans kareleriyle eşleşir, havuzlanmış skor çöker. Hız: `n_subsample`, `--vmaf-threads`, `--threads`, `select='lt(n\,N)'` ile ilk N kare. Ölçümden önce `libvmaf` filtresini sınar, yoksa açık mesaj verir; v1 modeli eski libvmaf'la istenirse ayrı mesaj. Varsayılan model `vmaf_v0.6.1.json`, 11 model paketle gelir.

**CrypticSignal/video-quality-metrics** — aynı videoyu farklı CRF/preset ile kodlayıp VMAF/PSNR/SSIM tablosu ve kare bazlı grafik üretir. Filtre: `model=path=...|enable_transform=true`, `log_fmt=json`, `n_subsample`, `n_threads`, `feature=name=psnr|name=float_ssim`. Asıl fikir **Overview Mode**: her `--interval` saniyede `--clip-length` saniyelik parça alıp birleştirir; README örneğinde 60 sn video 20 sn'ye iner (`--interval 5 --clip-length 2`), kesimler en yakın I-frame'e yuvarlandığı için tam değil. Skorları tek ortalama değil **Min | StdDev | Mean** olarak raporlar.

## Alınacak fikir

1. **`shortest=1:repeatlast=0`** — `QualityMeter` bunu vermiyor. Kırpılmış ya da kare sayısı kayan çıktıda skor sessizce çöker, sahte "kalite düşük" kararı üretir. Maliyet: tek seçenek.
2. **Overview Mode** — uzun videoda ölçümü birkaç kısa dilimde yapmak. `ComplexityProbe` zaten dilim mantığı taşıyor, aynı dilimleri ölçüme vermek doğal. Maliyet: orta.
3. **Model yolu + sürüm sıkılaştırması** — `HasFilter("libvmaf")` var ama model dosyası ayrı bir başarısızlık yüzeyi. Netflix'in Windows kaçış kuralı ve slhck'in "libvmaf sürümü yetersiz" ayrımı, hatayı "ffmpeg failed (1)" olmaktan çıkarır. Maliyet: düşük.

## Alınmayacak

- **VMAF v1 modelleri.** slhck README'ye göre Temmuz 2026'da hiçbir yayınlanmış libvmaf sürümü v1'i desteklemiyor, master build gerekiyor — kurulum yüzeyi kaldırmaz. `vmaf_v0.6.1neg` kalsın.
- **CrypticSignal'ın `|` → `^|` kaçışı.** cmd.exe içindir. VidShrink `Process`'e argüman dizisi geçiyor, araya kabuk girmiyor; kaçış filtreyi bozar.
- **CAMBI/CIEDE/VIF ek özellikleri.** Ölçüm süresine biner, tavan kararına girmez.
- **CrypticSignal'a bağımlılık.** Etiketli sürüm yok, son push 2026-05-04; desen alınır, paket alınmaz.

## VidShrink'te nereye dokunur

- `src/VidShrink.Ffmpeg/QualityMeter.cs` — `shortest/repeatlast`, `n_subsample`/`n_threads`, model yolu kaçışı, hata mesajı. Mevcut zincir `[0:v]zscale=w=..:h=..[t];[t][1:v]<filtre>`: girdi sırası doğru, ama iki tarafta da `setpts=PTS-STARTPTS` yok — PTS'i 0'dan başlamayan kaynakta hizalama riski.
- `src/VidShrink.Ffmpeg/EncoderCapabilities.cs` — libvmaf sürümü ve model dosyası varlığı sınaması.
- `src/VidShrink.Ffmpeg/ComplexityProbe.cs` — Overview Mode dilimlerinin kaynağı.
- `src/VidShrink.Core/PlanCalculator.cs` — şeffaflık tavanı eşiği burada kalır; hiçbir depo bu sayıyı vermiyor.
- `tests/VidShrink.Tests/QualityMeterTests.cs` — kısa/uzun akış eşleşmesi için gerileme testi.

## Kaynaklar

- `gh api repos/{Netflix/vmaf, slhck/ffmpeg-quality-metrics, CrypticSignal/video-quality-metrics}` + `/releases/latest`, 2026-08-22.
- Netflix/vmaf: `README.md`, `resource/doc/ffmpeg.md`, `resource/doc/models_v1.md`, `resource/doc/faq.md`.
- slhck: `README.md`, `src/ffmpeg_quality_metrics/ffmpeg_quality_metrics.py`. CrypticSignal: `README.md`, `libvmaf.py`.
