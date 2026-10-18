# Oynatıcı HDR Ton Eşleme

Tarih: 2026-10-18. Makine: Windows 11, RTX 5070 Ti, pinli libmpv (`tools\libmpv\libmpv-2.dll`). Başlık: `docs/later.md` 96.

## Soru

HDR video (HDR10/PQ, HLG) SDR ekranda soluk ya da patlak görünüyor mu, ve mpv'nin kendi GPU ton
eşlemesi oynatıcının yazılım çizim yolunda (`vo=libmpv`, BGRA) çalıştırılabilir mi?

## Düzenek

- Klipler yerelde ffmpeg ile üretildi (`-threads 2`, indirme yok): aynı kolaj SDR (BT.709) olarak, ve
  doğrusallaştırılıp BT.2020 PQ'ya 100 nit ve 1000 nit tepe ile, bir de HLG olarak kodlandı
  (libx265 ultrafast, `pools=2:frame-threads=1`). 320x180; hız için 1080p ve 2160p PQ.
- Ölçer `VidShrink.Player`'a başvuran küçük bir konsol programıydı; kareyi `TryCopyLatest` ile
  okuyup t=1,0 sn'de ortalama luma, patlak piksel oranı (luma ≥ 250) ve SDR karesine ortalama
  mutlak fark yazdı. Süre ölçümleri aynı programın `Stopwatch` okumaları.

## Bugünkü Yol

`vo=libmpv` yazılım çizimi; renk dönüşümünü zimg yapıyor (`sws-allow-zimg=yes`) ve PQ/HLG'yi ton
eşlemeden BT.1886'ya kırpıyor. `tone-mapping`, `target-trc`, `target-prim`,
`target-colorspace-hint` vo_gpu seçenekleri; bu yolda kareye dokunmuyor, baytlar birebir aynı çıktı.

| Klip | Ortalama luma | Patlak % | SDR'ye fark |
|---|---|---|---|
| SDR referans | 122,9 | — | — |
| PQ 100 nit | 131,2 | 0 | 16,4 |
| PQ 1000 nit | 157,6 | **6,5** | 34,0 |
| HLG | 130,6 | 0 | 14,9 |

## Denenen Yollar

| Yol | PQ 100 | PQ 1000 (patlak %) | HLG | Not |
|---|---|---|---|---|
| `tone-mapping`/`target-*` özellikleri | aynı | aynı | aynı | etkisiz |
| `vf format=convert=yes` | — | patlak 4,9 | — | ton eşlemiyor |
| `vf lavfi=[zscale,tonemap=hable]` | 81,6 | 133,9 (0) | 81,0 | işlemci halkası, karar gereği dışarıda |
| `vf lavfi=[libplacebo]` | 102,5 | 172,0 (0) | 103,9 | ilk kare +450–1290 ms, arama 550–2371 ms |
| **`vf gpu`** (seçilen) | **87,9** | **87,4 (0)** | **101,8** | mpv'nin GPU ton eşleyicisi, Vulkan |

libplacebo halkasında bir karede zaman damgası kayması görüldü (içerik 0,96 sn, damga 1,0 sn).

## Seçilen Yol

mpv'nin `gpu` video süzgeci ekran dışı bir Vulkan bağlamında mpv'nin kendi ton eşleyicisini
koşturuyor ve çıktıyı yazılım çizim yoluna geri veriyor. Hedef `target-prim=bt.709`,
`target-trc=bt.1886`. Halka `@vstone` etiketiyle yalnız `video-params/gamma` `pq` ya da `hlg`
iken takılıyor; SDR'de takılmıyor, anahtar kapalıyken kaldırılıyor ve kare bugünkü yolla birebir
aynı dönüyor.

| Ölçü | Bugün / kapalı | `vf gpu` açık |
|---|---|---|
| İlk kare (320x180) | 209–332 ms | 579–882 ms |
| Arama | 12–31 ms | 29–330 ms |
| 2 sn oynatmada düşen kare, 1080p | 0–1 | 0 |
| 2 sn oynatmada düşen kare, 2160p | 0–5 | 0 |
| Oynatma süresi farkı | — | +0,5–0,65 sn (GPU bağlamının açılışı) |

`target-peak=100` verilince ortalamalar PQ 117,5 / PQ 1000 116,7 / HLG 136,5 oluyor, SDR'ye
daha yakın; varsayılan mpv'nin 203 nitlik referans beyazı olarak bırakıldı.

## Düşüş

Süzgeç çalışırken düşerse mpv "Disabling filter ... because it has failed" yazıp bugünkü yoldan
oynatmaya devam ediyor (bozuk libplacebo ile ölçüldü). Süzgeç takılırken reddedilirse motor
`ToneRefused` tutuyor ve oynatma yine bugünkü yoldan sürüyor. Vulkan'sız makine yerelde
canlandırılamadı: yönetici sürecinde `VK_LOADER_DRIVERS_SELECT`, `VK_ICD_FILENAMES`,
`VK_DRIVER_FILES` yok sayıldı.

18 Ekim 2026: Vulkan aygıtı olmayan CI koşucusunda `vf pre @vstone:gpu` süreci `AccessViolationException` ile düşürdü (`mpv_command` ← `ApplyToneMapping` ← olay döngüsü). Süzgeç artık `VulkanProbe` bir aygıt bulursa takılıyor (`vkCreateInstance` + `vkEnumeratePhysicalDevices`, süreç başına bir kez); yoksa `ToneRefused` ve bugünkü yol. Test: `VulkanAygitiYokkenHalkaTakilmazOynatmaSurer`.

## Açık Kalanlar

- Ekran HDR'ye geçiş (`target-colorspace-hint`) yazılım çiziminde uygulanamıyor: Avalonia'ya giden
  kare 8 bit BGRA. HDR ekranda da ton eşlenmiş SDR gösteriliyor.
- `vf gpu` mpv'de deneysel; ilk kareye ve aramaya gecikme ekliyor.
- Parlaklık hedefi (`target-peak`) sahibin kararı.

Test: `tests/VidShrink.Tests/OynaticiHdrTonEslemeTests.cs`.
