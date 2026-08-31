# Görev paketi — HDR'yi geri getir, ölçüm düzeneğini tamir et

Sole için yazıldı. Depo dalı: `main` (kendi dalını aç: `sole/hdr-ve-olcum-tamiri`).
`GOREV-handbrake-acigi.md` birleştirildi; raporun `docs/olcumler/handbrake-acigi.md`
`main`de duruyor.

## Önce: raporun hangi yarısı duruyor, hangisi düşüyor

**Duran yarı — teşhis.** Şikâyet tabanını plan ve komut düzeyinde yeniden ürettin ve
dört imzayı da tutturdun: `av1_nvenc`, `882×496`, hable→bt709 tonemap, `-g 120`. HandBrake
tarafının gerçek ayarlarını çıkardın (x265 `psy-rd=2`, `psy-rdoq=1`, AQ mode 2, ~10 sn GOP,
turbo ilk geçiş). `-svtav1-params`'ın çıkış kodu 0 ile hata yuttuğunu `zzznotreal=1` ile
kanıtladın. Turbo ilk geçişi ölçtün: 12,57 sn'ye karşı 15,07 sn, %16,6. Bunların hepsi
kalıcı ve bir sonraki işin temeli.

**Düşen yarı — eş boyut kalite tablosu.** O tablodaki VMAF sayıları sıkıştırma kalitesini
ölçmüyor, bir renk uyuşmazlığını ölçüyor. Kanıt tablonun kendi içinde:

| Hedef | HandBrake bit hızı farkı | HandBrake XPSNR |
|---|---|---:|
| SDR 1/2 | taban | 14,86 |
| SDR 1/6 | ~1/3 | 14,78 |
| SDR 1/20 | ~1/10 | 14,67 |

Bit hızı on kat düşerken XPSNR **0,19 dB** oynuyor. Gerçek bir sıkıştırma kaybı böyle
davranmaz — senin VidShrink sütunun aynı aralıkta 33,13 → 25,83 gidiyor, doğru davranış
bu. Sabit kalan bir hata sıkıştırmadan değil, **her karede aynı olan bir kaymadan** gelir.
Kare 193'ün skorunun tam 0 çıkması da aynı şeyi söylüyor.

Nedeni raporun kendi notunda yazılı: HandBrake çıktısı `bt709` etiketli, SDR kaynağı
etiketsiz, `zscale` "no path between colorspaces" verdi ve düzeltme olarak iki tarafı
aynı `scale=lanczos` ile aynı boyuta getirdin. Bu **geometriyi** eşitledi, **rengi**
eşitlemedi. Etiketsiz kaynak tam aralık, `bt709` etiketli çıktı sınırlı aralık okununca
her piksel kayar; VMAF bunu bozulma sayar.

Aynı kusur HDR satırlarını da geçersiz kılıyor: VidShrink sütunu 13,71 / 13,71 / 13,53 —
yine sabit. Tonemap'in gerçek bir kusur olduğu doğru ve bunu ayrıca kanıtladın, ama
**büyüklüğü** bu düzenekle ölçülemez.

Raporun ilk cümlesi ("SDR'de VidShrink 65,9–69,9 puan daha iyi") bu yüzden geri
çekilmeli. Kullanıcı kendi gözüyle tersini gördü; veri onu desteklemiyor, düzenek
bozuk. Sınırlarını rapor içinde dürüstçe yazmışsın — sorun sınırların yazılmaması değil,
özet cümlenin sınırların dışına çıkması.

## İş 1 — HDR'yi geri getir

Bu ölçüm beklemiyor: kullanıcının kendi dosyasında kanıtlandı. HandBrake PQ/BT.2020
10 bit teslim etti, biz bt709 8 bit teslim ettik.

Kabul kriteri:

1. Varsayılan hızlı yolda HDR kaynak **tonemap edilmiyor**. `HdrResolver.Hdr10Codecs`
   `av1_nvenc`'i ve 10 bit destekleyen öteki donanım kodlayıcılarını tanısın. Hangi
   kodlayıcının gerçekten 10 bit HDR yazabildiğini `EncoderCapabilities` üzerinden
   **ölç**, listeye elle yazma — makinede olmayan kodlayıcı için varsayım üretme.
2. Çıktı gerçekten HDR: `ffprobe` `color_transfer=smpte2084`, `color_primaries=bt2020`,
   `pix_fmt` 10 bit. Statik HDR10 metadata (mastering display, MaxCLL) kaynakta varsa
   çıktıya geçsin; `-x265-params` yolu AV1'de çalışmaz, AV1 tarafında ne işe yaradığını
   ölçüp yaz. Geçmiyorsa **geçmediğini yaz**, geçiyormuş gibi bırakma.
3. Tonemap'e düşmek zorunda kalınan durumda kullanıcı **görüyor**. Bugün planda not var;
   arayüzde göründüğünü bir ölçü tutsun.
4. Ölçü mutasyonla sınansın: `av1_nvenc`'i listeden çıkar, ölçünün kırmızıya döndüğünü
   göster, geri al.
5. SDR kaynakta hiçbir şey değişmiyor — bir ölçü bunu tutsun.

## İş 2 — ölçüm düzeneğini tamir et

Kabul kriteri:

1. `QualityMeter`'ın karşılaştırma yolu iki tarafı **aynı ve açıkça belirtilmiş** renk
   uzayına getirsin: etiketsiz kaynağa varsayılan atarken ne varsaydığını söylesin,
   sınırlı/tam aralık farkını çözsün. Bugünkü `scale=lanczos` eşitlemesi yetmiyor.
2. Doğrulama ölçüsü: **aynı dosyayı kendisiyle karşılaştır** — VMAF 100'e, XPSNR
   üst sınıra çıkmalı. Sonra dosyayı yalnız `bt709` etiketiyle yeniden kaplayıp aynı
   karşılaştırmayı tekrarla; skor yine tavanda kalmalı. Bugün kalmıyor, hata tam burada.
3. Bir tarafı tonemap edilmiş karşılaştırma **sayı üretmesin**. VMAF yerine "renk uzayı
   uyuşmuyor, karşılaştırılamaz" dönsün. Tonemap'in maliyetini ölçmek isteyen ayrı bir
   ölçüm kurar; tek sayıya sıkıştırılan karşılaştırma yanıltıyor.
4. Düzenek tamir olunca **eş boyut tablosunu yeniden koştur** ve raporu değiştir. Eski
   tabloyu silme, "geçersiz — düzenek bozuktu" diye işaretleyip yenisini altına koy.

## İş 3 — ablasyonu doğru rejimde tekrarla

Beşli ablasyonu yalnız 1/20 hedefinde koştun. O uçta her şey zaten yıkık: FPS kapatma
tabanla aynı kararı verdi, yazılım −0,17, geniş tepe −0,29 çıktı. Şikâyetin rejimi orası
değil — 17 dakikalık 1080p60 için ~120 MiB, yani yaklaşık 940 kbps.

Kabul kriteri: ablasyon **şikâyetin rejiminde** tekrarlansın. Tepe tavanının orada
açık mı kapalı mı olduğu ayrıca yazılsın — HDR 1/6'da açıldığını (`-b:v 7034k` /
`-maxrate 7737k` = 1,10×) zaten görmüşsün, SDR 1/20'de 1,02× ile fiilen CBR'di. Hangi
oranda hangisinin geçerli olduğu tabloya girsin.

## Kaynak

`VIDSHRINK_LIVE_SOURCE` boştu ve kullanıcının 17 dakikalık dosyası gelmedi; iki kısa
sentetik klip kaldı, ikisi de aynı arşivden. Bu tek başına tablonun genellenememesinin
ikinci nedeni.

Kullanıcının dosyası hâlâ gelmezse **en az bir gerçek çekim** bul: sentetik desen değil,
kamera ya da oyun kaydı, en az 60 saniye, tercihen 1080p60. `trash/` altındaki iki çıktı
**referans değil** — ikisi de sıkıştırılmış.

## Sınırlar

- İş 1 gerçek motor değişikliğidir; İş 2 ve 3 ölçüm tarafıdır. İkisini ayrı commit'lerde tut.
- `dotnet test -c Release` tamamı yeşil. Taban: 958 ölçü, 941 geçiyor, 17 atlanıyor,
  0 başarısız. Atlanan sayısı artmasın.
- Senin olanlar: `src/VidShrink.Core/HdrResolver.cs`, `src/VidShrink.Ffmpeg/QualityMeter.cs`,
  `src/VidShrink.Ffmpeg/EncoderCapabilities.cs`, `tools/VidShrink.Bench/**`,
  `docs/olcumler/handbrake-acigi.md`, kendi eklediğin testler.
- `src/VidShrink.Core/PlanCalculator.cs` ve `CompressionStrategy.cs` **senin değil** —
  ceza sabitlerinin kalibrasyonu ayrı bir pakette, ölçüm düzeltilene kadar başlamıyor.
- Ara dosyalar `.calisma/` altına; iş bitince kendi bıraktığını sil. `.calisma/` şu an boş
  başlıyor, birikinti `trash/calisma/` altına alındı.
- Yorum yazma; mevcut yorumları koru.
- Kendi dalında çalış (`sole/hdr-ve-olcum-tamiri`), bitince **it**. `main`e sen birleştirme.

## Not

Bu turda paketi güncellerken benim bir commit'im (`e76d86a`) senin dalına düştü, çünkü
paylaşılan çalışma ağacı senin dalındaydı. Üçüncü kez oluyor. Dalını açtıktan sonra ağacı
bende bırakma; ben `main`de yazarken sen kendi dalındaysan ikimizin commit'i karışıyor.
