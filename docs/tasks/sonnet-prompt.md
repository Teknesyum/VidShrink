# Sonnet'e verilecek prompt

Aşağıdaki bloğu olduğu gibi kopyala, VidShrink klasöründe açılmış Sonnet oturumuna yapıştır.

---

VidShrink motorunu bir sonraki seviyeye taşıyacaksın. Bağımsız bir denetim raporu hazırlandı ve
uygulanabilir paketlere bölündü.

Önce şu üç dosyayı oku, başka bir şeye başlama:

1. `docs/tasks/yol-haritasi.md` — asıl iş listen, paketler ve kabul kriterleri burada
2. `docs/claude-engine-audit-report.md` — bulguların gerekçesi ve referanslar
3. `docs/implementation-report.md` — motorun bugünkü hâli neden böyle

Sonra **P2** paketini (kalite ölçüm altyapısı) uygula. P3 ve sonrasına bu oturumda girme;
P2 bitince dur ve rapor ver.

P0 ve P1 tamamlandı (commit `fe95925`): gerekçe kodları, atomic çıktı, disk kontrolü,
encoder capability cache, HDR/10-bit politikası. Bunları yeniden yapma.

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
`bench run` çıktısının bir özeti ve `bench/results/baseline.json` yolu, commit kimliği.

---

## Sonraki oturumlar

Aynı prompt, paket numarası değiştirilerek tekrar kullanılır.
P2 bittiğinde `bench/results/baseline.json` depoya girmiş olmalıdır;
P4 ve sonrası o dosya olmadan başlatılmaz.

## P0/P1 denetiminde çıkan ders

Atomic çıktı ilk hâlinde `<hedef>.partial` adını kullanıyordu. ffmpeg konteyner biçimini
dosya uzantısından seçtiği için bu **her kodlamayı** kırıyordu, ama 30 testin hiçbiri
başarılı bir encode'u uçtan uca çalıştırmadığı için yeşil görünüyordu.
Bundan sonra: bir davranış değiştiğinde, o davranışın **mutlu yolunu** gerçek ffmpeg ile
sınayan en az bir test yaz. Yalnızca hata yolunu test etmek yeterli değil.
