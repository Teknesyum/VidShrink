# Geçiş Önizleme Yolu

Later 109 (DZB-17). Düzenleyicide iki klibin bindiği süre arka planda küçük bir ara dosyaya
işlenir (`Core/Editing/TransitionPreview`) ve `edl://` listesine ayrı parça olarak girer
(`EdlPreview`'un `blends` sözlüğü). Dosya hazır olana kadar önizleme eski yoldan oynar, hazır
olunca liste yeniden açılır. Yeni bağımlılık yok, MLT yok.

Ölçüm komutu, ffmpeg 9.0 (gyan.dev), `-threads 2`:

```
dotnet run --project tools/VidShrink.Bench -c Release -- transition-preview .calisma/<dal>/gecis 5
```

## A. 1080p'de Bir Saniyelik Geçişin Hazırlanma Süresi

Kaynak 1920x1080 30 fps, 8 sn `testsrc2` + 440 Hz ses. İki klip 0-4 sn ve 4-8 sn, geçiş 1 sn.
Süre ffmpeg sürecinin tamamı (açılış, iki arama, süzgeç, x264 ultrafast crf 12, pcm ses).
Beşer tekrar, 2026-10-21, aynı makinede iki koşum. İkinci koşumda makinede başka ajanların
derlemeleri de dönüyordu.

| geçiş | 1. koşum ortanca (aralık) | 2. koşum ortanca (aralık) |
|---|---|---|
| Erime (fade) | 228 ms (224-235) | 231 ms (224-234) |
| Silme (wipeleft) | 216 ms (195-233) | 236 ms (223-245) |
| Kaydırma (slideleft) | 304 ms (298-318) | 337 ms (311-357) |
| İris (circleopen) | 493 ms (487-504) | 580 ms (555-606) |
| Erime, hız 2, ters | 316 ms (tek) | 335 ms (tek) |

En yavaş tür yarım saniyenin biraz üstünde. Bu sürede önizleme eski yoldan, geçişsiz kesmeyle
oynar.

## B. `lavfi-complex` İle `edl://` Birlikteliği

İki parçalı EDL (0-2 sn ve 5-7 sn) libmpv'de açıldı, `lavfi-complex=[vid1]hflip[vo]` bir kez
açılıştan önce, bir kez sonra yazıldı, oynatma 3 sn'ye kadar sürdü.

| kol | hata | okunan değer | konum | 2 sn sınırı | çizilen kare |
|---|---|---|---|---|---|
| açıldıktan sonra | yok | `[vid1]hflip[vo]` | 3.00-3.03 sn | geçildi | 91 |
| açılmadan önce | yok | `[vid1]hflip[vo]` | 3.00-3.03 sn | geçildi | 90-91 |

Günlükte `lavfi` ya da `error` satırı yok. İkisi birlikte çalışıyor. Yine de geçiş için
seçilmedi: süzgeç tek akışı görür, bir geçişin iki kaynağını aynı anda vermez.

## C. Önizleme Karesi İle Teslim Karesi

Kaynak 640x360 30 fps, iki klip, 1 sn silme geçişi (teslimde 3.0-4.0 sn, kare 90-119).

Dosya düzeyinde: ara dosyanın 30 karesi teslimin 90-119 kareleriyle `psnr` süzgecinden geçti
(iki girdi de `settb=1/30,setpts=N`, mkv'nin 1 ms zaman tabanı yüzünden).

| kare | ortalama PSNR | en az | en çok |
|---|---|---|---|
| 30 | 47.97 dB | 47.13 dB | 49.13 dB |

Oynatıcı düzeyinde: libmpv'den aynı andaki kare çizim geri çağrısından alındı (`MpvEngine.TryCopyLatest`, kesin arama, duraklatılmış), teslim karesinin
n-1, n, n+1'iyle karşılaştırıldı. "Eski" geçiş dosyası olmayan EDL, "yeni" geçiş dosyalı EDL.

| teslim karesi | yeni n-1/n/n+1 | eski n-1/n/n+1 |
|---|---|---|
| 60 (geçiş dışı) | 15.96 / 18.71 / 28.67 | 15.96 / 18.71 / 28.67 |
| 95 | 14.86 / 18.19 / 28.50 | 12.30 / 12.41 / 12.46 |
| 99 | 14.31 / 17.60 / 28.60 | 12.70 / 12.92 / 13.19 |
| 104 | 14.09 / 16.66 / 28.60 | 11.63 / 11.43 / 11.16 |
| 110 | 14.14 / 17.40 / 28.51 | 11.97 / 12.22 / 12.54 |
| 150 (geçiş dışı) | 14.23 / 17.08 / 28.21 | 14.23 / 17.08 / 28.21 |

Geçiş içindeki karelerde yeni yol, geçiş dışındaki karelerle aynı düzeye çıkıyor (yaklaşık
28.5 dB). Eski yol 11-13 dB'de kalıyor. Geçiş dışındaki 60. ve 150. karede iki yol aynı. Bir
karelik kayma ve 28 dB tavanı her iki yolda da, geçiş dışında da var. Bu yüzden geçişten değil,
mpv'den kare alma yolundan geliyor (kare etiketi ya da renk dönüşümü). Bench'e girmeyen elle bir denemede
teslimin süzgeç zinciri, aynı girdilerle kurulmuş ideal bir `xfade` kaynağıyla 99 dB eşleşti.

## Test

`GecisOnizlemeTests.CanliGecisParcasiTeslimKaresiyleOrtusurVeEdlIleAcilir`: 320x180, 0.5 sn
silme. Ara dosya teslimin 45-59 kareleriyle 45.24 dB. Negatif kontrol olarak iris ile işlenen
dosya aynı karelerde 23.84 dB. Test `>= 40` dB ve "en az 10 dB fark" ister, EDL'nin libmpv'deki
süresini de ölçer.

## Açık Kalanlar

- Birden çok ses izi olan kaynakta EDL'nin ses izi düzeni ölçülmedi.
- Metin katmanının zamanlaması geçiş parçasında ayrıca denenmedi.
- Tuvali olmayan anamorfik kaynakta ara dosya görüntü genişliğine yuvarlanır, teslimle
  karşılaştırılmadı.
- Parça sınırında geometri değişimi bir kare gecikebilir.
- Geçiş parçası boyunca çizelgede oynatma başı iki kat hızla ilerler (2·ov çizelge, ov EDL).
- Ses izi değişince ara dosyalar yeniden işlenmez.
- Windows'ta açık dosya ayrılmada silinemezse uygulama çıkışına ya da sonraki açılışın
  süpürmesine kalır.
- mpv kare almasındaki bir karelik sabit kayma ayrıca çözülmedi.
