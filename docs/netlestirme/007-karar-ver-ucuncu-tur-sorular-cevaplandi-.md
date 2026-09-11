# Netleştirme: Karar ver (ucuncu tur, sorular cevaplandi): VidShrink oynatici motoru icin on ad

- tarih: 2026-09-11
- girdi: 007-karar-ver-ucuncu-tur-sorular-cevaplandi--girdi.md
- maliyet: 63058 token, 86 sn

## Dönen

Netleştirme turu değil, karar turu: araç çağırmadan tek metinde döndürüyorum (ek maliyet yok).

**net** — kalan tek belirsizlik birim dönüşümü; varsayımım: **1 sözleşme turu ≈ 3 insan iş günü** (yapıcı + denetçi + tam süit). Bütün sayılar tahmin.

## Puan Tablosu

Ölçüt ağırlıkları (toplam 28, azami 140): özellik hazırlığı 8 · Windows/Avalonia 11 entegrasyonu 4 · başsız test 3 · maliyet 4 · AGPL uyumu 2 · macOS paketleme 2 · hız/hwdec 2 · bakım 2 · ikili boyut 1. Puan 1–5.

| # | Aday | Lisans (AGPL) | Boyut | Özellik | Win | Başsız | Maliyet | AGPL | macOS | Hız | Bakım | Boyut | **Toplam** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | libmpv + kendi P/Invoke | GPLv2+ → GPLv3 birleşim (FSF görüşü, kesin değil); LGPL derleme mümkün, hazır yok | ~34 MB | 5 | 4 | 4 (SW, tahmin) | 4 | 3 | 3 | 3 (SW tek iş parçacığı, tahmin) | 5 | 3 | **115** |
| 3 | LibVLCSharp 3.10.1 | LGPL-2.1+, sorunsuz | 128 MB | 4 (A-B, kare geri zayıf) | 2 (airspace; callbacks yolu ölçülmedi) | 2 | 3 | 5 | 4 | 4 | 5 | 1 | **95** |
| 2 | libmpv + HanumanInstitute 0.10.1 | 1 ile aynı + MIT sarmalayıcı | ~34 MB | 5 | 2 (Avalonia 12 hedefli) | 2 (doğrulanmadı) | 3 | 3 | 3 | 3 | 2 (tek geliştirici) | 3 | **91** |
| 5 | Bugünkü boru, genişletme | değişmez | 0 ek | 1 (tavan: 13'ün ~5'i erişilmez) | 5 | 5 | 2 | 5 | 4 | 2 (yeniden başlatma, 105 ms) | 5 | 5 | **88** |
| 4 | FFmpeg.AutoGen süreç içi | MIT + mevcut ffmpeg | ~54 MB | 1 | 5 | 5 | 1 | 5 | 3 | 3 (hwaccel elle) | 4 | 3 | **80** |
| 6 | Flyleaf | LGPL-3 | — | — | yalnız WPF/WinUI | — | — | — | yok | — | — | — | elendi |
| 7 | Media Foundation | MIT (Vortice) | — | — | — | — | — | — | yok | — | — | — | elendi |
| 8 | GStreamer-sharp | LGPL-2.1 | — | — | — | — | — | — | — | — | 2021'den beri bakımsız | — | elendi |
| 9 | Avalonia Accelerate | ticari, AGPL ile gerilimli | — | — | Avalonia 12 | — | — | 1 | — | — | — | — | elendi |
| 10 | SDL + ffmpeg | zlib | — | — | airspace, köprü yok | — | — | — | — | — | — | — | elendi |

**En mantıklı seçim: 1, libmpv + kendi P/Invoke.** Fark 20 puan; ikinci LibVLC yalnız lisans ve bakımda önde, ancak airspace ve 128 MB ile kaybediyor.

## Kendi Oynatıcımızı Yazmanın Maliyeti

| Yol | Standart 30 | Gelişmiş 13 | Toplam | Not |
|---|---|---|---|---|
| (a) Boru genişletme, aday 5 | ~14 tur / ~45 gün | ~8 tur / ~25 gün, **13'ün ~5'i erişilmez** (canlı hız+perde, canlı altyazı/ses parçası değişimi, hassas kare geri adımı, kare hassas A-B, canlı ekolayzer) | ~22 tur / ~70 gün, kapsamın ~%85'i | Ucuz başlar, tavana çarpar; her canlı ayar süreç yeniden başlatma |
| (b) FFmpeg.AutoGen süreç içi, aday 4 | ~24 tur / ~75 gün (demux/decode, A/V saat, ses çıkışı, arama, libass P/Invoke) | ~12 tur / ~35 gün | ~36 tur / ~110 gün (≈5–6 ay) | Kapsamın tamamı erişilir, tamamı elle |
| libmpv (004) | ~8 tur / ~25 gün | ~4 tur / ~12 gün | ~14 tur / ~45 gün, dalga 0–6 dahil | Özelliklerin çoğu property/command |

libmpv, (a)'dan %35 ucuz ve tam kapsam; (b)'nin %40'ı.

## Karar: 004 Korunur

libmpv, kendi P/Invoke, SW render PlayerView'a; ffmpeg Küçült/Dönüştür/karşılaştırma için kalır. Dalga planı yeniden yazılmaz; yalnız 0. dalganın ilk adımına eşik ve dönüş yolu eklenir:

- **0. dalga, adım 1 (ölçüm):** libmpv SW render bu makinede, başsız, 1080p60 ve 4K30 örnek dosyalarla kare hızı. **Eşik: 1080p ≥ 55 kare/s, 4K ≥ 24 kare/s.**
- 1080p tutar, 4K tutmaz → devam; 4K için GPU yolu (OpenGlControlBase + ANGLE) 4. dalgaya "isteğe bağlı" kalem.
- 1080p tutmaz → kütüphane değişmez, **ikili tasarım**: arayüzde OpenGL/ANGLE, başsızda SW. 0. dalgaya +1 tur.
- İkisi de çalışmaz (ANGLE Avalonia 11'de bağlanamazsa) → **LibVLCSharp, `libvlc_video_set_callbacks` bellek kopyası yolu** (aday 3); dalga 0 baştan, diğer dalgalar özellik eşlemesiyle korunur.
- **Lisans notu (avantaj sütunu, engel değil):** GPL hazır ikiliyle başlanır; `-Dgpl=false` LGPL derlemesi 6. dalga sonrası isteğe bağlı kalem, CI'da derleme maliyeti ~1 tur.

Yok
