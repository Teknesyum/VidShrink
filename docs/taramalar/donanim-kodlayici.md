# Donanım kodlayıcı sarmalayıcıları — rigaya NVEnc / QSVEnc / VCEEnc

Ortak bulgu: **üçü de hedef bit hızını varsayılan yapmıyor.** NVEnc varsayılanı QVBR, QSVEnc varsayılanı ICQ, VCEEnc varsayılanı CQP — hepsi kalite tabanlı. Bit hızı, üçünde de `--max-bitrate` + `--vbv-bufsize` ile *tavan* olarak veriliyor, sürücü olarak değil. VidShrink'in "VBR hedefi tutturamıyor, 2-3 düzeltme turu" sorunu bu tasarımın tersinden gidiyor.

## Depo

| Depo | Son push | Son sürüm | Yıldız | Açık issue | Lisans |
|---|---|---|---|---|---|
| rigaya/NVEnc | 2026-08-22 | 9.32 (2026-08-15) | 1371 | 9 | MIT (`NVEnc_license.txt`; GitHub API `NOASSERTION` diyor) |
| rigaya/QSVEnc | 2026-08-15 | 8.27 (2026-08-15) | 422 | 12 | MIT (`license.txt`) |
| rigaya/VCEEnc | 2026-08-22 | 9.13 (2026-08-22) | 289 | 10 | MIT (`VCEEnc_license.txt`) |

Üçü de aktif, arşivlenmemiş, aynı yazarın aynı iskeleti. MIT dışında ayrı marka/isim kısıtı bulamadım.

## Ne yapıyor

**NVEnc (NVEncC)** — Modlar `--cqp`, `--cbr`, `--vbr`, `--qvbr <float 0-51, AV1'de 0-63>`. Belge açıkça yazıyor: `--qvbr X` = `--vbr 0 --vbr-quality X` — bit hızı sıfır verilip direksiyon kaliteye bırakılıyor. `--multipass none|2pass-quarter|2pass-full` **yalnız `--vbr`/`--cbr` ile** geçerli; gerekçe belgede: "2-pass bitleri kare içinde daha iyi dağıtır ve hedef bit hızına daha yakın çıkar, özellikle CBR'de". Sınırlar: `--max-bitrate`, `--vbv-bufsize`, `--qp-min`, `--qp-max` (CQP'de yok sayılır), `--qp-init`. Ön bakış `--lookahead 0-32` + `--lookahead-level 0-3`, varsayılanları preset/tune'a bağlı. Preset `-u P1..P7`, **varsayılan P4**; `--tune` varsayılanı `hq`. Sınama: `--check-device` (DeviceId + PCI Bus ID), `--check-hw [id]`, `--check-features [id]`, `--check-preset-params`. Çok GPU'da otomatik seçim üç şarta bakıyor: cihaz istenen kodlamayı destekliyor mu, `--avhw` istendiyse girdinin donanım çözümünü destekliyor mu, interlace istendiyse destekliyor mu.

**QSVEnc** — `--icq 23` (varsayılan), `--la-icq`, `--cqp`, `--cbr`, `--vbr`, `--avbr`, `--la`, `--la-hrd`, `--vcm`, `--qvbr` + `--qvbr-quality 0-51 (23)`. Belgenin kendi mod notu: "CBR/VBR/AVBR temel ve hızlı ama kalitesi düşük; ICQ/LA-ICQ/QVBR/LA daha iyi. Maksimum bit hızı **sadece** VBR ve CBR'de verilebilir." İki ayırt edici bayrak: `--fallback-rc` (platform yeni hız kontrol modunu desteklemiyorsa eskiye düş) ve `--workaround-hevc10bit-enctools` (AlderLake ve öncesi iGPU'da 10-bit HEVC + EncTools bozulması — **varsayılan açık**, uyarı basıp ilgili seçenekleri kapatıyor). AV1'de ön bakış ayrı mod değil: `--la-depth 10-100` + `--extbrc`, `--icq` üstüne bindiriliyor. Preset `-u best|higher|high|balanced|fast|faster|fastest`, **varsayılan balanced**. Sınama: `--check-hw`, `--check-impl`, `--check-features`, `--check-features-html`, `--check-device`.

**VCEEnc (AMF)** — `--cqp` (varsayılan), `--cbr`, `--cbrhq`, `--vbr`, `--vbrhq`, `--qvbr` = `--vbr 0 --qvbr-quality <0-51>`. `--pa` ön analizi **yalnız VBR'de** çalışıyor (`sc`, `ss`, `caq-strength`, `initqpsc`, `fskip-maxqp`), `--pe` ön kodlama destekli hız kontrolü varsayılan kapalı. Preset `-u balanced|fast|slow|slower`. Sınama `--check-hw [id]`, `--check-features [id]`, `--check-clinfo`.

Üçünde de `GPUFeatures/` klasörü var: gerçek donanımdan alınmış `--check-features` çıktıları depoya işlenmiş (NVEnc GTX 660Ti→RTX 5060, QSVEnc Haswell→Battlemage, VCEEnc RX 460→RX 7900 XT). Yetenek matrisi kodda değil veri dosyasında.

## Alınacak fikir

1. **Donanımda hedefi kaliteyle sür, bit hızını tavan yap.** Üç depo da varsayılanı böyle seçmiş. "Hızlı Düşür" turu `-b:v <hedef>` yerine kalite değerini oynatmalı (nvenc `-cq`, qsv `-global_quality`, amf `-qvbr_quality_level` — bu üç ffmpeg adı **doğrulanmadı**, `ffmpeg -h encoder=...` ile teyit edilmeli), `-maxrate`/`-bufsize` yalnız tavan olarak dursun. Düzeltme turu tek eksende ve monoton olur, ikili arama yapılabilir. *Maliyet:* düşük — `EncodePlan`'da kalite ekseni zaten var.
2. **Bit hızı hedefli tur açıkta kalırsa `--multipass 2pass-quarter` karşılığını aç.** Belge hedefi tutturma gerekçesini doğrudan bu bayrağa bağlıyor. ffmpeg karşılığının adı/değerleri **doğrulanmadı**. *Maliyet:* düşük, tek bayrak; bedeli hız.
3. **Yetenek sınaması ayrı adım + bilinen-bozuk tablosu.** `--check-hw` bir kez koşup "bu cihaz bu kodlayıcıyı gerçekten çalıştırıyor mu" cevabı veriyor; üstüne `--fallback-rc` ve `--workaround-hevc10bit-enctools` gibi *isimli, varsayılan açık* kaçış yolları var — bozuk donanım kombinasyonu try/catch değil, tabloya yazılı veri. *Maliyet:* orta — `EncoderCapabilities.ProbeEncoder` var, eksik olan diske yazılan önbellek ve tablo.

## Alınmayacak

- **rigaya ikililerini bağımlılık yapmak.** Üç ayrı çalıştırılabilir, üç ayrı SDK (CUDA, VPL, AMF), satıcı başına farklı bayrak adları. Tek ffmpeg varken kurulum yüzeyi üçe katlanır.
- **`--dynamic-rc`** (kare aralığına göre mod değiştirme) — boyut hedefi için gereksiz karmaşıklık.
- **VCEEnc'in CQP varsayılanı ve `--pa` CAQ ince ayarları** — CQP boyut hedefiyle uyumsuz, `--pa` parametrelerinin ffmpeg `*_amf` karşılığı yok denecek kadar az.
- **QSV'nin eski `--la` / `--la-icq` / `--la-hrd` modları** — belgenin kendisi AV1'de yerine `--la-depth` + `--extbrc` diyor.
- **AviUtl eklentisi, NVOFFRUC / NGX / VPP filtre yığını** — konu dışı.
- Bu bayrak adları ffmpeg bayrak adları **değil**; birebir taşınamaz.

## VidShrink'te nereye dokunur

- `src/VidShrink.Core/FfmpegArguments.cs` — `DefaultPreset` (şu an `av1_nvenc`→`p6`, `*_qsv`→`medium`, `*_amf`→`quality`; NVEnc'in kendi varsayılanı P4) ve `NeedsTwoPasses` (donanımda hep `false` — multipass fikri buraya girer).
- `src/VidShrink.Core/ConversionArguments.cs` — `-cq`/`-crf` seçimi ve `-maxrate`/`-bufsize` tavanının kalite tabanlı moda göre yeniden kurulması.
- `src/VidShrink.Core/PlanCalculator.cs` — düzeltme turunun bit hızı yerine kalite ekseninde ikili arama yapması.
- `src/VidShrink.Ffmpeg/EncoderCapabilities.cs` — `ProbeEncoder` sonucunun kalıcı önbelleği ve bilinen-bozuk donanım tablosu.
- `src/VidShrink.Core/CodecModel.cs` — `UsesCq` ayrımının satıcı başına kalite bayrağı adına genişlemesi.

## Kaynaklar

- `gh api repos/rigaya/{NVEnc,QSVEnc,VCEEnc}` ve `/releases/latest`, 2026-08-22 çekimi.
- `NVEncC_Options.en.md`, `QSVEncC_Options.en.md`, `VCEEncC_Options.en.md` (master, 2026-08-22).
- `NVEnc_license.txt`, `QSVEnc/license.txt`, `VCEEnc_license.txt` — üçü de MIT.
- Depo içi `GPUFeatures/` klasör listeleri.
