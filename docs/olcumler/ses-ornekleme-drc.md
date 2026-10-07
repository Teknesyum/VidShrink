# Ses Örnekleme Hızı Ve DRC Ölçeği — 7 Ekim 2026

`--ses-hizi` (HandBrake `--arate`) ve `--ses-drc` (HandBrake `--drc`) için ffmpeg'in neyi kabul
ettiği, neyi sessizce değiştirdiği ölçüldü. ffmpeg 9.0 (gyan full), tek süreç, `-threads 2`,
birkaç saniyelik lavfi kaynağı. Ağdan örnek indirilmedi.

## Kodlayıcı Başına Örnekleme Hızı

Kaynak 48 kHz stereo `sine`, 1 sn; her satır `-c:a <kodlayıcı> -ar <hız>`. HandBrake'in dokuz hızı
denendi: 8, 11,025, 12, 16, 22,05, 24, 32, 44,1, 48 kHz.

| Kodlayıcı | Açılan hızlar | Açılmayan hızlar | Dosyada okunan |
|---|---|---|---|
| `aac` | dokuzunun hepsi | — | istenen hız |
| `ac3`, `eac3` | 32, 44,1, 48 kHz | 8, 11,025, 12, 16, 22,05, 24 kHz | istenen hız |
| `libopus` | 8, 12, 16, 24, 48 kHz | 11,025, 22,05, 32, 44,1 kHz | **her zaman 48000** |
| `flac` | hız listesi bildirmiyor (`ffmpeg -h encoder=flac`) | — | 16 kHz okundu (`FlacSesTests`), ötekiler ölçülmedi |

`ac3` açılmayan hızda `Specified sample rate … is not supported` diyor ve çıkış sıfır değil.
`libopus` 24 kHz'i kabul ediyor ama dosyaya 48 kHz yazıyor: istenen hız teslim edilmiyor.

Karar: `StreamMapping.SampleRatesFor` ac3/eac3 için 32/44,1/48, libopus için yalnız 48 kHz
döndürür. Listede olmayan birleşim kodlamadan önce `error.arate-codec` ile durur (çıkış 64);
ileti kodlayıcıyı, istenen hızı ve yazabildiği hızları söyler. Sessiz düzeltme yok.

## AAC'nin Düşük Hızdaki Bit Hızı Tavanı

Kaynak `anoisesrc` beyaz gürültü, 4 sn; ses baytı `ffprobe packet=size` toplamından. İstenen bit
hızı 128k ve 256k: düşük hızlarda ikisi aynı sonucu veriyor, yani kodlayıcı tavanda.

| Hız | Stereo (kbit/sn) | Mono (kbit/sn) | Koddaki tavan (mono / stereo) |
|---|---|---|---|
| 8 kHz | 41,7 (sabit tohumlu test koşumunda 42,0) | 36,7 | 37 / 42 |
| 11,025 kHz | 57,7 | 51,1 | 52 / 58 |
| 12 kHz | 62,2 | 55,1 | 56 / 63 |
| 16 kHz | 83,2 | 74,2 | 75 / 84 |
| 22,05 kHz | 113,2 | 101,4 | 102 / 114 |
| 24 kHz | 124,2 | 110,1 | 111 / 125 |

32 kHz ve üstü 128k'ya ulaşıyor (256k istekte 32 kHz 162, 44,1 kHz 220, 48 kHz 259 kbit/sn);
orada kelepçe yok. İkiden çok kanal ölçülmedi, orada da kelepçe yok.

Karar: `StreamMapping.AacCeilingK` yukarı yuvarlanmış tavanı verir; aac izi buna kelepçelenir ve
bütçe hesabında videoya kalan pay büyür. Kelepçe plan metninde ayrı bir not satırıyla söylenir.
Canlı test (`CanliAacTavaniOlculenDegerinUstunde`) 8 kHz stereo ölçümünü her koşumda yineler.

## DRC Ölçeği

ffmpeg'de `-drc_scale` yalnız `ac3` ve `eac3` çözücüsünün seçeneği (`ffmpeg -h decoder=ac3`:
0–6, varsayılan 1). `aac` çözücüsünde yok. HandBrake'in aralığı 0–4, VidShrink bunu alır.

| Koşum | Çıkış | stderr |
|---|---|---|
| AC-3 kaynak, `-drc_scale:1 2 -i …` (belirteç ses akışı) | 0 | uyarı yok |
| AC-3 kaynak, belirteç video akışına (`:0`) | 0 | `has not been used for any stream` |
| AAC kaynak, belirteçsiz `-drc_scale 2` | 0 | aynı uyarı |
| `-an` ya da `-c copy` ile | 0 | uyarı yok |
| uydurma `-drc_uydurma:1 2` | sıfır değil | `Unrecognized option` |

Karar: seçenek yalnız yeniden kodlanan AC-3/E-AC-3 izinin akış belirteciyle, `-i`'den önce
yazılır. Kopyalanabilecek Dolby izi bu istekle yeniden kodlanır (çözücü çalışmadan ölçek
uygulanamaz). Kaynak Dolby değilse hiçbir şey yazılmaz, iz kopyalanabiliyorsa kopyalanır ve plan
metni "uygulanmadı" der.

**Duyulur etki ölçülmedi.** lavfi'den ffmpeg'in kendi kodlayıcısıyla üretilen AC-3 ve E-AC-3
kaynakta ölçek 0, 1, 2 ve 4 için çözülen PCM'in md5'i aynı: ffmpeg'in kodlayıcısı akışa DRC verisi
yazmıyor, o yüzden ölçeğin değiştireceği bir şey yok. Gerçek DRC verisi taşıyan (stüdyo çıkışı)
bir AC-3 dosyası elde yok ve örnek indirmek kural dışı. Ölçülen yalnız kabuldür: seçenek doğru
akışa bağlanıyor ve ffmpeg uyarısız alıyor.

Bir fark: seçenek verilmezse ffmpeg'in varsayılanı (1) sürer; HandBrake'in varsayılanı 0'dır.
VidShrink seçeneksiz koşumda hiçbir şey yazmaz, yani eski davranış değişmedi.

## İkisinin Kopyayla İlişkisi

| İstek | Kaynak iz | Sonuç |
|---|---|---|
| `--ses-hizi 44.1` | 48 kHz aac | yeniden kodlanır, `-ar 44100` |
| `--ses-hizi 48` | 48 kHz aac | kopyalanır; tam kopya yolu da açık |
| `--ses-drc 2` | ac3 | yeniden kodlanır, `-drc_scale:<akış> 2` |
| `--ses-drc 2` | aac | kopyalanır, not: uygulanmadı |
| ikisinden biri | ses yok | not: sese uygulanamadı |

İkisi de `izle` komutunda öteki tek dosya seçenekleri gibi reddedilir.

## Mutasyonlar

`SesOrneklemeDrcTests` (56 kol) üzerinde, her biri elle geri alındı.

| Mutasyon | Kırmızı |
|---|---|
| `-ar` yazılmıyor | 3 |
| kopya kapısından `ReencodesFor` çıkarıldı | 8 |
| `-drc_scale` akış belirteçsiz | 6 |
| DRC ölçeği her kodekteki ize yazılıyor | 2 |
| aac kelepçesi yok (`Min` → `Max`) | 2 |
| `RejectedSampleRate` hiçbir izi döndürmüyor | 4 |
| libopus tablosuna 8/12/16/24 kHz eklendi | 2 |
| FLAC tavanı kaynak hızından | 1 |
| `loudnorm` sonu kaynak hızına | 1 |
| `InputArguments` komuta eklenmiyor | 2 |
| tam kopya kapısı isteği okumuyor | 1 |
| `WithTarget` hızı taşımıyor | 1 |
| DRC üst sınırı yok | 2 |
| hız listesi denetimi yok | 3 |
| `izle` `--ses-drc` kabul ediyor | 2 |
| "Dolby değil" notu yalnız sessiz çıktıda | 1 |
| kelepçe notu yazılmıyor | 1 |

On yedi mutasyonun on yedisi kırmızı. İki koşumda (FLAC tavanı, `WithTarget`) canlı aac tavan
testi de kırmızı döndü; bu mutasyonla ilgisizdi: ölçülen 42,02 kbit/sn, tavan 42 ve eşik tam
tavandı. Gürültü tohumu sabitlendi, bant tavanın %80–105'ine alındı; o sayımlar tabloda yok.
