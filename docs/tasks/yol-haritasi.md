# VidShrink motor yol haritası — Sonnet iş paketleri

Kaynak: `docs/claude-engine-audit-report.md` (Codex denetimi, 18 Ağustos 2026).
Bu dosya o raporu **uygulanabilir paketlere** böler. Paketler numara sırasıyla yapılır;
bir paketin kabul kriteri geçmeden sonrakine geçilmez.

Ortam doğrulaması (bu makinede yapıldı, tekrar araştırma gerekmez):

- ffmpeg 9.0-full (gyan) — `libvmaf` var, `model=version=vmaf_v0.6.1neg` yükleniyor ve skor üretiyor.
- `xpsnr`, `ssim`, `zscale`, `tonemap` filtreleri var.
- Encoder: `libx264`, `libx265`, `libsvtav1`, `libvpx-vp9`, `h264_nvenc`, `hevc_nvenc`, `av1_nvenc`, `*_qsv`.

Genel kurallar:

- Kod yorumu yazma. Renk/ölçü uydurma; arayüz değişikliği varsa `teknesyum-ui` tokenlarını kullan.
- `LanguageCatalog` anahtarları **iki yönde de** benzersiz olmalı; tekrar eden Türkçe değer ters
  sözlüğü çalışma anında patlatır.
- Hedef boyut **sert tavandır**. Hiçbir paket `EncodeRunner.ToleranceOver = 1.0` değerini gevşetemez.
- Her paket sonunda: `dotnet build VidShrink.sln -c Release` (0 uyarı) + `dotnet test VidShrink.sln`.
- Motoru değiştiren her paket, P2 sonrası benchmark sonuçlarını önceki sürümle karşılaştırmak zorundadır.

---

## P0 — Yarım kalanlar ve dayanıklılık

Küçük, riski düşük, önce bu.

**0.1 Plan gerekçesi Türkçeye çevrilmiyor**
`PlanCalculator.cs:128` ve `:179` gerekçeyi İngilizce cümle olarak `plan.Reason` içine yazıyor;
`MainWindow.xaml.cs:251` bunu Türkçe arayüzde olduğu gibi gösteriyor.
Çözüm: Core dilden bağımsız **kod** üretsin, App biçimlendirsin — `CompressionStrategy.AdviceCode`
kalıbının aynısı. `EncodePlan.Reason` (string) AI promptu ve JSON şeması için kalsın, yanına
`ReasonCodes` ve gerekli sayısal alanlar eklensin.
App tarafında `DescribeStrategy()` içindeki `T(tr, en)` kalıbıyla çevrilsin.
Kabul: TR arayüzde hiçbir İngilizce cümle görünmüyor; `Reason` string'i JSON tarafında aynen çalışıyor.

**0.2 Atomic output**
Kodlama ve dönüştürme önce `<hedef>.partial` yazsın, başarıda `File.Move(..., overwrite: true)`.
Uygulama açılışında geçici klasördeki eski `vidshrink_*` pass-log ve `.partial` kalıntılarını temizlesin.
Kabul: Kodlama ortasında process öldürüldüğünde hedef adında dosya oluşmuyor.

**0.3 Disk alanı ön kontrolü**
Kodlama başlamadan hedef sürücüde `hedefMB * 3 + 200 MB` yoksa anlaşılır hata.
Kabul: Test, `DriveInfo` yerine saf hesaplama fonksiyonunu sınar.

**0.4 Encoder capability cache**
`ToolLocator` yanına `EncoderCapabilities`: `ffmpeg -encoders` ve `-filters` çıktısını bir kez
okuyup process ömrü boyunca cache'lesin, ffmpeg sürümünü de tutsun.
`CodecModel` ve `PlanCalculator` NVENC veya `libsvtav1` seçmeden önce **varlığını sorsun**,
yoksa yazılım karşılığına düşsün.
Kabul: `libsvtav1` içermeyen bir ffmpeg'de plan üretimi çökmüyor, x265'e düşüyor ve gerekçesinde söylüyor.

---

## P1 — HDR ve 10-bit doğruluğu

Bugün `FfmpegArguments.cs:63` ve `ConversionArguments.cs:76` koşulsuz `-pix_fmt yuv420p` yazıyor.
HDR kaynak sessizce ve yanlış biçimde SDR'ye düşüyor: renkler soluyor, metadata kayboluyor.
`MediaInfo.IsHdr` ölçülüyor ama planlamada hiç kullanılmıyor.

**1.1 MediaInfo genişlet**
`FfprobeClient` zaten `color_transfer` okuyor ama saklamıyor. Eklenecek alanlar:
`ColorPrimaries`, `ColorTransfer`, `ColorSpace`, `ColorRange`, `BitDepth`,
`MasteringDisplayMetadata`, `ContentLightLevel` (side_data), `IsInterlaced` (field_order).

**1.2 Politika**
`PlanOptions.HdrPolicy { Preserve, TonemapToSdr }`, varsayılan `Preserve`.
`EncodePlan.PixelFormat` alanı gelsin; `FfmpegArguments` sabit `yuv420p` yerine bunu yazsın.

- **Preserve**: 10-bit destekleyen encoder zorunlu (`libx265` Main10, `libsvtav1`, `hevc_nvenc` main10).
  `-pix_fmt yuv420p10le`, `-color_primaries` / `-color_trc` / `-colorspace` kaynaktan aktarılır,
  x265 için `hdr10-opt=1` ve varsa `master-display` ile `max-cll` parametreleri.
- **TonemapToSdr**: `zscale=t=linear:npl=100` → `tonemap=hable:desat=0` →
  `zscale=p=bt709:t=bt709:m=bt709:r=limited` → `format=yuv420p` zinciri; çıktıya BT.709 metadata yazılır.
- Kullanıcı H.264 seçtiyse HDR korunamaz → otomatik tone-map ve yeni `AdviceCode.HdrTonemapped`
  ile arayüzde açık uyarı.

**1.3 Arayüz**
Shrink sekmesinde HDR kaynak yüklendiğinde görünen iki seçenek (HDR'yi koru / SDR'ye çevir),
`?` rozetiyle: koruma daha büyük dosya ve dar cihaz uyumluluğu, tone-map WhatsApp ve telefon için güvenli.
HDR olmayan kaynakta seçim gizli kalsın.

**1.4 Regresyon klipleri**
lavfi ile üretilebilen HDR10 (`smpte2084` + bt2020) ve HLG (`arib-std-b67`) örnekleri, 8-bit SDR kontrolü.
Kabul: Üç kaynak için üretilen komut satırı beklenen renk metadata'sını içeriyor (birim testi);
gerçek encode sonrası `ffprobe` çıktısında metadata korunmuş.

---

## P2 — Kalite ölçüm altyapısı

Bundan sonraki hiçbir iyileştirme ölçülmeden kabul edilmez.

**2.1 QualityMeter** (`src/VidShrink.Ffmpeg/QualityMeter.cs`)
Girdi: referans dosya, test dosyası, opsiyonel pencere listesi. Çıktı:
`QualityScore(VmafNegMean, VmafNegHarmonic, VmafNegP10, VmafNegMin, Xpsnr, Ssim)`.
Test akışı `zscale` ile referans çözünürlüğüne çıkarılır, sonra
`libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path=...` ile ölçülür;
kare başına skorlar JSON'dan okunup p10 ve harmonik ortalama hesaplanır.
`xpsnr` ayrı geçişte alınır. `libvmaf` yoksa metrik `null` döner, çağıran taraf çökmez.

**2.2 Benchmark CLI** (`tools/VidShrink.Bench`, net8.0 konsol)
Scratchpad'deki `smoke` harness bunun tohumu.

- `bench corpus init` → `bench/corpus.json` içindeki üretim reçetelerini (lavfi komutları ve
  indirme adresleri) çalıştırıp klipleri `bench/media/` altına koyar.
  Klipler `.gitignore`, manifest git'e girer.
- `bench run --targets 1,3,8,16,25 --engines vidshrink,ab-av1,handbrake --out bench/results/`
  → her klip, hedef ve motor için satır: boyut sapması, VMAF NEG mean/harmonic/p10/min,
  XPSNR, encode süresi, decode uyumluluk testi, plan özeti.
- `bench compare <eski.json> <yeni.json>` → tablo, regresyon kapısı sonucu ve çıkış kodu.
- Rakip kurulu değilse o satır `skipped` işaretlenir, benchmark yine çalışır.

**2.3 Corpus sınıfları** (rapordaki liste): düşük ışık ve grain, anime, ekran kaydı, spor,
konuşan kafa, telefon VFR, 4K HDR10/HLG, interlaced, çok kanallı ses, sessiz, çok kısa ve çok uzun.
Her sınıftan en az bir klip; ilk turda lavfi ile üretilebilenler yeterli.

Kabul: `bench run` mevcut motorla uçtan uca çalışıyor, `bench/results/baseline.json` üretilip
depoya işleniyor. Bu dosya bundan sonraki her paketin karşılaştırma tabanıdır.

---

## P3 — Regresyon kapıları

`bench compare` şu kapıları uygulasın, ihlalde sıfırdan farklı çıkış kodu döndürsün:

- Hedef boyut aşımı: **0 vaka**, istisnasız.
- Hedef sapma p95: en fazla %1.
- Yarım veya başarısız çıktı: **0**.
- Decode uyumluluğu: %100.
- VMAF NEG harmonik ortalaması: baseline'dan 0,5 puandan fazla düşmesin.
- En kötü %10 sahne (p10): baseline'dan 1,0 puandan fazla düşmesin.

Kabul: Kapılar bilerek bozulmuş bir planla denenip kırmızı verdiği gösterilir.

---

## P4 — Algısal düzen araması

Asıl fark burada. Bugün `PlanCalculator.SearchLayout` çözünürlük ve FPS adaylarını **analitik ceza
fonksiyonuyla** puanlıyor. Bit bütçesi zaten sabit; asıl soru "bu bütçede hangi düzen gerçekten daha
iyi görünüyor" ve buna tahmin ederek değil ölçerek cevap verilebilir.

1. `SceneSampler` — sahne değişimlerini (`select` filtresi, scene eşiği 0,3) tarayıp 5–12 temsili
   pencere seçer. `ComplexityProbe`'un sabit üç pencerelik `Windows()` mantığının yerini alır.
2. `LayoutTournament` — analitik aramanın ürettiği ilk 3–4 adayı (farklı ölçek, FPS, codec)
   aynı bit bütçesinde pencerelerde kodlar, `QualityMeter` ile karşılaştırır, kazananı seçer.
3. Skor kapısı ortalama değil **p10 ve harmonik ortalama** olsun; en kötü sahne tabanı korunur.
4. CRF modunda interpolasyonlu ikili arama: iki örnek encode'dan bitrate/CRF eğrisi çıkarılır,
   tahmin oradan düzeltilir.
5. Zaman bütçesi: toplam kodlama süresinin %20'sini aşmasın, aşacaksa aday sayısı kısılır.
   Arayüzde Hassas mod anahtarı (varsayılan açık, kapatılabilir); kapalıyken bugünkü analitik yol
   çalışır ve hızlı kalır.

Kabul: Baseline'a karşı benchmark sonucunda aynı boyutta VMAF NEG harmonik ortalaması yükselmiş;
encode süresi artışı raporlanmış ve %25'in altında.

---

## P5 — Codec turnuvası ve rate-control soyutlaması

**5.1 IEncoderStrategy** — her encoder için ayrı sınıf: capability probe, kalite aralığı ve yönü,
CRF/CQ/VBR/2-pass argüman üretimi, preset maliyet modeli, 8/10-bit ve HDR desteği,
pass-log davranışı, boyut tahmini ve düzeltme politikası.
Uygulamalar: `X264Strategy`, `X265Strategy`, `SvtAv1Strategy`, `Vp9Strategy`, `NvencStrategy`, `QsvStrategy`.
`FfmpegArguments` tek reçete yazmayı bırakır, stratejiye sorar.

**5.2 Auto codec seçimi** P4 turnuvasının içine girer: uyumluluk hedefi (WhatsApp, telefon, arşiv)
bir kısıt olarak modellenir, kalan adaylar ölçülen kaliteye göre yarışır.
Kabul: WhatsApp niyetinde asla H.264 dışına çıkılmıyor; arşiv niyetinde AV1 kazanabiliyor ve
kazandığında gerekçesini söylüyor.

---

## P6 — İçerik uzmanlığı ve ses motoru

- Grain, animasyon, ekran içeriği ve karanlık sahne skorları örnek pencerelerden çıkarılır.
- SVT-AV1 film-grain sentezi adayları (0/4/8/12) ve x264/x265 `tune` adayları **yalnızca**
  P2 ölçümünde kazandırıyorsa uygulanır.
- Kaynak normalizasyonu: interlace tespiti ve `bwdif`, VFR zaman tabanı, SAR/DAR,
  crop ve letterbox tespiti, renk aralığı, rotation metadata doğrulaması, çoklu ses ve altyazı politikası.
- Ses motoru: LUFS ve true-peak analizi, konuşma-müzik ayrımı, AAC/Opus seçimi, 5.1 downmix,
  sessizlik oranına göre bütçe.

---

## P7 — Piyasa iddiası

Rakip motorların (ab-av1, HandBrake varsayılanları, doğrudan ffmpeg reçeteleri) aynı makine ve
aynı encoder sürümünde otomatik benchmarkı; sonuçların CSV/JSON ve grafik olarak yayımlanması;
kör insan A/B testi; kapıların CI'da korunması.

README'deki "en iyi" iddiası **yalnızca** bu paket bittikten sonra geri konur.
Bugünkü README zaten iddiayı erteleyecek biçimde düzeltilmiş durumda, o hâli bozma.
