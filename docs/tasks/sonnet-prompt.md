# Sonnet'e verilecek prompt

Aşağıdaki bloğu olduğu gibi kopyala, VidShrink klasöründe açılmış Sonnet oturumuna yapıştır.

---

VidShrink motorunu bir sonraki seviyeye taşıyacaksın. Bağımsız bir denetim raporu hazırlandı ve
uygulanabilir paketlere bölündü.

Önce şu üç dosyayı oku, başka bir şeye başlama:

1. `docs/tasks/yol-haritasi.md` — asıl iş listen, paketler ve kabul kriterleri burada
2. `docs/claude-engine-audit-report.md` — bulguların gerekçesi ve referanslar
3. `docs/implementation-report.md` — motorun bugünkü hâli neden böyle

Sonra sırasıyla **P0** ve **P1** paketlerini uygula. P2 ve sonrasına bu oturumda girme;
P0 ile P1 bitince dur ve rapor ver.

Motorun bugünkü mimarisi:

- `src/VidShrink.Core` — `ComplexityProfile` (ölçülen bit maliyeti ve detay düşüş üssü),
  `CompressionStrategy` (rejim ve tavsiye kodları), `PlanCalculator` (bütçe, düzen araması,
  şeffaflık tavanı), `FfmpegArguments`, `ConversionArguments`
- `src/VidShrink.Ffmpeg` — `FfprobeClient`, `ComplexityProbe` (iki çözünürlüklü prob),
  `EncodeRunner` (iki geçiş, ilerleme, iptal, tekrar deneme)
- `src/VidShrink.App` — WPF arayüz, `LanguageCatalog` ile TR/EN
- `tests/VidShrink.Tests` — 11 xUnit testi

Uyacağın kurallar:

- Kod yorumu yazma.
- Hedef boyut sert tavandır. `EncodeRunner.ToleranceOver = 1.0` değerini gevşetme.
- `LanguageCatalog` anahtarları iki yönde de benzersiz olmalı; tekrar eden Türkçe değer
  ters sözlüğü çalışma anında patlatır.
- Arayüze dokunuyorsan `teknesyum-ui` standardını kullan, renk veya ölçü uydurma.
  Her yeni teknik kontrolün `?` rozeti olsun ve iki dilde açıklasın.
- Mevcut davranışı bozma: ffmpeg argüman sırası, process-tree iptali, kısmi çıktı temizliği,
  stream-copy doğrulaması, CRF boyut dürüstlüğü.
- Her paketten sonra çalıştır: `dotnet build VidShrink.sln -c Release` (0 uyarı) ve
  `dotnet test VidShrink.sln`. Derleme öncesi çalışan uygulamayı kapat:
  `Get-Process VidShrink.App -EA SilentlyContinue | Stop-Process -Force`
- Her yeni davranış için test yaz. Test yazılamayacak bir şey varsa gerekçesini yaz.
- `docs/implementation-report.md` dosyasına ne yaptığını ekle; arayüz değiştiysen
  `docs/ui-requirements-history.md` dosyasına da.
- İş bitince `main` dalına commit ve push at. Commit mesajı ve README İngilizce,
  proje içi dokümanlar Türkçe.

Ortam bilgisi, tekrar araştırmana gerek yok: ffmpeg 9.0-full kurulu; `libvmaf` çalışıyor ve
`vmaf_v0.6.1neg` modeli yükleniyor; `xpsnr`, `zscale`, `tonemap` filtreleri var;
`libx264`, `libx265`, `libsvtav1`, `libvpx-vp9`, NVENC ve QSV encoderları mevcut.
Test klipleri lavfi ile üretilebilir, depoya klip ekleme.

Sonuç raporunda şunlar olsun: hangi paket bitti, hangi dosyalar değişti, test sayısı ve sonucu,
HDR koruma ile tone-map yollarının gerçek bir dosyada doğrulanmış `ffprobe` çıktısı, commit kimliği.

---

## Sonraki oturumlar

P0 ve P1 bittikten sonra aynı prompt, ilgili paket numaraları değiştirilerek tekrar kullanılır.
P2 (ölçüm altyapısı) tek başına bir oturumdur ve bittiğinde `bench/results/baseline.json`
depoya girmiş olmalıdır; P4 ve sonrası o dosya olmadan başlatılmaz.
