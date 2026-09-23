# VP9 CRF Ölçeği

Koşum: `vp9-crf-olcek` / 35833205185 (success, 9 dk), dal `worktree-agent-a090568a33aee2665`, commit `6678ca79`.
Düzenek: `.github/workflows/vp9-crf-olcek.yml`, `tools/vp9-crf-olcek/kos.ps1`. Kaynak Sintel 1080p, kesitler `vp9-gercek-kesit` ile aynı seçim (karanlık/orta/hareketli), her kesitin ilk 4 sn'si.
ffmpeg 9.0 (GyanD), 4 çekirdek. libx264 `-preset slow` (ürünün varsayılanı), libvpx-vp9 `-deadline good -cpu-used 1 -row-mt 1 -crf N -b:v 0`. Ölçü VMAF-NEG ortalaması (`bench measure-pair`). Eşdeğer CRF, VP9 eğrisinde komşu iki nokta arasında doğrusal ara değer.

## Sonuç

- x264 CRF 23'ün VP9 eşdeğeri: karanlık 32.9, orta 30.8, hareketli 34.5; ortalama 32.7 → referans **33**.
- x264 30 → 44.7..47.1 (ort. 46.2); x264 37 → 58.0..59.3 (ort. 58.8); x264 16 → 11.3..18.0 (ort. 15.6).
- x264 45'in kalitesine VP9 63'te bile inilmiyor: VP9 63 = 48.8/74.2/59.3, x264 45 = 28.0/62.8/36.0. Üst sınır kodlayıcının tavanı 63.
- x264 10'un kalitesi orta ve hareketli kesitte VP9 10'un üstünde (< 10); VP9 10 altı ölçülmedi.
- Önerilen aralık: **(10, 63)**, referans **33**. Bu aralık x264 ~16..~40 kalite bandını kapsar; x264 10..45 bandının iki ucu VP9 CRF ölçeğinde bu ızgarayla karşılanmıyor.
- Eşit kalitede VP9/x264 bayt oranı 0.28..0.75 (x264 23'te 0.35..0.69).
## Esit VMAF-NEG Noktasi

| kesit | x264 crf | x264 VMAF-NEG | x264 kbps | VP9 esdeger crf | VP9 kbps (ara) | VP9/x264 bayt |
|---|---|---|---|---|---|---|
| karanlik | 10 | 99.957 | 23864.2 | 15.3 | 8105.5 | 0.34 |
| karanlik | 16 | 99.94 | 10802.1 | 18 | 6957.2 | 0.644 |
| karanlik | 23 | 97.621 | 4238.1 | 32.9 | 2902.9 | 0.685 |
| karanlik | 30 | 84.456 | 1774.3 | 46.8 | 1034.9 | 0.583 |
| karanlik | 37 | 59.11 | 785.4 | 59.3 | 365.6 | 0.465 |
| karanlik | 45 | 28.024 | 351.2 | > 63 |  |  |
| orta | 10 | 98.336 | 20973.6 | < 10 |  |  |
| orta | 16 | 97.043 | 5680.7 | 11.3 | 2751.4 | 0.484 |
| orta | 23 | 94.193 | 1626.9 | 30.8 | 574.9 | 0.353 |
| orta | 30 | 89.144 | 613.9 | 44.7 | 198.2 | 0.323 |
| orta | 37 | 79.852 | 321.5 | 58 | 90.2 | 0.281 |
| orta | 45 | 62.773 | 191.1 | > 63 |  |  |
| hareketli | 10 | 99.91 | 21788.2 | < 10 |  |  |
| hareketli | 16 | 99.719 | 10172.8 | 17.5 | 7608.4 | 0.748 |
| hareketli | 23 | 97.778 | 4155.3 | 34.5 | 2750.2 | 0.662 |
| hareketli | 30 | 90.141 | 1883.8 | 47.1 | 1126.1 | 0.598 |
| hareketli | 37 | 69.186 | 941.6 | 59.2 | 430.4 | 0.457 |
| hareketli | 45 | 35.993 | 477.5 | > 63 |  |  |

## Kesit Ortalamasi

| x264 crf | VP9 esdeger crf (kesitler) | ortalama |
|---|---|---|
| 10 | karanlik 15.3, orta < 10, hareketli < 10 | 15.3 |
| 16 | karanlik 18, orta 11.3, hareketli 17.5 | 15.6 |
| 23 | karanlik 32.9, orta 30.8, hareketli 34.5 | 32.7 |
| 30 | karanlik 46.8, orta 44.7, hareketli 47.1 | 46.2 |
| 37 | karanlik 59.3, orta 58, hareketli 59.2 | 58.8 |
| 45 | karanlik > 63, orta > 63, hareketli > 63 |  |

## Ham Egri

| kesit | kodek | crf | kbps | MB | VMAF-NEG | P10 | sure sn |
|---|---|---|---|---|---|---|---|
| hareketli | libvpx-vp9 | 10 | 12659.8 | 6.037 | 99.875 | 100 | 24.94 |
| hareketli | libvpx-vp9 | 16 | 8200.1 | 3.91 | 99.759 | 99.602 | 19.47 |
| hareketli | libvpx-vp9 | 20 | 6724 | 3.206 | 99.653 | 98.817 | 18.17 |
| hareketli | libvpx-vp9 | 24 | 5635 | 2.687 | 99.496 | 97.964 | 16.51 |
| hareketli | libvpx-vp9 | 28 | 4207.6 | 2.006 | 99.053 | 96.587 | 15.08 |
| hareketli | libvpx-vp9 | 31 | 3460 | 1.65 | 98.592 | 95.102 | 14.32 |
| hareketli | libvpx-vp9 | 34 | 2838 | 1.353 | 97.926 | 93.471 | 14.85 |
| hareketli | libvpx-vp9 | 37 | 2326.4 | 1.109 | 96.99 | 91.359 | 16.43 |
| hareketli | libvpx-vp9 | 40 | 1842.4 | 0.879 | 95.434 | 88.828 | 15.13 |
| hareketli | libvpx-vp9 | 44 | 1409.9 | 0.672 | 93.09 | 85.14 | 14.42 |
| hareketli | libvpx-vp9 | 48 | 1054.3 | 0.503 | 89.276 | 79.955 | 13.83 |
| hareketli | libvpx-vp9 | 52 | 779.6 | 0.372 | 84.134 | 73.361 | 13.23 |
| hareketli | libvpx-vp9 | 56 | 580.4 | 0.277 | 77.607 | 66.113 | 12.61 |
| hareketli | libvpx-vp9 | 63 | 302.6 | 0.144 | 59.268 | 46.978 | 10.81 |
| hareketli | libx264 | 10 | 21788.2 | 10.389 | 99.91 | 100 | 17.33 |
| hareketli | libx264 | 16 | 10172.8 | 4.851 | 99.719 | 99.852 | 10.23 |
| hareketli | libx264 | 23 | 4155.3 | 1.981 | 97.778 | 93.46 | 7.2 |
| hareketli | libx264 | 30 | 1883.8 | 0.898 | 90.141 | 81.104 | 5.74 |
| hareketli | libx264 | 37 | 941.6 | 0.449 | 69.186 | 57.643 | 4.71 |
| hareketli | libx264 | 45 | 477.5 | 0.228 | 35.993 | 19.045 | 3.94 |
| karanlik | libvpx-vp9 | 10 | 11647.2 | 5.554 | 99.964 | 100 | 32.19 |
| karanlik | libvpx-vp9 | 16 | 7696.4 | 3.67 | 99.956 | 100 | 24.47 |
| karanlik | libvpx-vp9 | 20 | 6289 | 2.999 | 99.924 | 100 | 22.21 |
| karanlik | libvpx-vp9 | 24 | 5308.2 | 2.531 | 99.817 | 99.362 | 20.81 |
| karanlik | libvpx-vp9 | 28 | 4000.9 | 1.908 | 99.252 | 97.382 | 20.81 |
| karanlik | libvpx-vp9 | 31 | 3290.8 | 1.569 | 98.371 | 95.84 | 18.47 |
| karanlik | libvpx-vp9 | 34 | 2699.3 | 1.287 | 97.186 | 93.765 | 16.89 |
| karanlik | libvpx-vp9 | 37 | 2164 | 1.032 | 95.274 | 90.835 | 17.69 |
| karanlik | libvpx-vp9 | 40 | 1718.8 | 0.82 | 92.719 | 87.193 | 15.4 |
| karanlik | libvpx-vp9 | 44 | 1269.7 | 0.605 | 88.207 | 81.371 | 14.21 |
| karanlik | libvpx-vp9 | 48 | 944.6 | 0.45 | 82.781 | 74.094 | 13.44 |
| karanlik | libvpx-vp9 | 52 | 690.1 | 0.329 | 76.032 | 66.141 | 12.68 |
| karanlik | libvpx-vp9 | 56 | 506.1 | 0.241 | 68.487 | 57.651 | 12.05 |
| karanlik | libvpx-vp9 | 63 | 255.8 | 0.122 | 48.806 | 34.02 | 10.28 |
| karanlik | libx264 | 10 | 23864.2 | 11.379 | 99.957 | 100 | 15.44 |
| karanlik | libx264 | 16 | 10802.1 | 5.151 | 99.94 | 100 | 10.49 |
| karanlik | libx264 | 23 | 4238.1 | 2.021 | 97.621 | 94.323 | 7.04 |
| karanlik | libx264 | 30 | 1774.3 | 0.846 | 84.456 | 78.234 | 5.4 |
| karanlik | libx264 | 37 | 785.4 | 0.374 | 59.11 | 49.244 | 4.48 |
| karanlik | libx264 | 45 | 351.2 | 0.167 | 28.024 | 19.216 | 3.8 |
| orta | libvpx-vp9 | 10 | 3137.2 | 1.496 | 97.2 | 93.172 | 18.48 |
| orta | libvpx-vp9 | 16 | 1705.8 | 0.813 | 96.471 | 92.219 | 11.81 |
| orta | libvpx-vp9 | 20 | 1293.3 | 0.617 | 95.971 | 91.792 | 10.43 |
| orta | libvpx-vp9 | 24 | 1028.5 | 0.49 | 95.58 | 91.725 | 9.57 |
| orta | libvpx-vp9 | 28 | 717.6 | 0.342 | 94.746 | 91.185 | 8.56 |
| orta | libvpx-vp9 | 31 | 567.1 | 0.27 | 94.159 | 90.618 | 7.98 |
| orta | libvpx-vp9 | 34 | 448.9 | 0.214 | 93.4 | 90.056 | 7.26 |
| orta | libvpx-vp9 | 37 | 348.4 | 0.166 | 92.446 | 88.906 | 8.46 |
| orta | libvpx-vp9 | 40 | 273 | 0.13 | 91.343 | 87.208 | 7.2 |
| orta | libvpx-vp9 | 44 | 207.3 | 0.099 | 89.561 | 84.158 | 5.79 |
| orta | libvpx-vp9 | 48 | 162.6 | 0.078 | 87.293 | 80.536 | 5.49 |
| orta | libvpx-vp9 | 52 | 129.2 | 0.062 | 84.994 | 76.421 | 5.25 |
| orta | libvpx-vp9 | 56 | 103.4 | 0.049 | 82.186 | 71.853 | 4.99 |
| orta | libvpx-vp9 | 63 | 64.8 | 0.031 | 74.177 | 58.146 | 4.66 |
| orta | libx264 | 10 | 20973.6 | 10.001 | 98.336 | 96.407 | 16.61 |
| orta | libx264 | 16 | 5680.7 | 2.709 | 97.043 | 94.32 | 10.78 |
| orta | libx264 | 23 | 1626.9 | 0.776 | 94.193 | 91.353 | 6.38 |
| orta | libx264 | 30 | 613.9 | 0.293 | 89.144 | 83.331 | 4.71 |
| orta | libx264 | 37 | 321.5 | 0.153 | 79.852 | 67.623 | 4.31 |
| orta | libx264 | 45 | 191.1 | 0.091 | 62.773 | 41.036 | 3.87 |

## Kesit Sha256
- hareketli: 500DFDF28B27D0261659624E4630BCBECC66322326B262091D54C344BDDA97D4 (tutuyor)
- karanlik: D2905779F20B2601421CE1453AAA688F955CE0367A32D932A2B8848140516082 (tutuyor)
- orta: 12F15E4A113B54EE5BCF023432ADC79CC6C0310EF66217B916D97D12CAB68763 (tutuyor)
