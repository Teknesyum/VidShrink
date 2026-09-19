# E3: Çıktı Adının Deseni

Kullanıcı çıktı **klasörünü** seçebiliyordu, **adını** seçemiyordu: ad `{ad}_shrunk` olarak
koda gömülüydü. E3 adı ayara açıyor.

Kural iki ayrı yerde yazılıydı — `ShrinkEngine.UniqueOutputPath` ve
`ShrinkJobWindow.UniqueOutputPath`. İkinci kopya sabit çıktı klasörünü de görmüyordu; yani
sağ tık menüsünden gelen kuyruk işi kullanıcının **iki** ayarını birden yok sayıyordu. Bu,
b7a6e209'da kapatılan "ayar kullanıcıya yalan söylüyor" sınıfının aynısı.

## Yer tutucu kümesi

| Yer tutucu | Değeri | Örnek |
|---|---|---|
| `{ad}` | kaynağın adı, eski `_shrunk` eki düşer | `klip` |
| `{hedef}` | hedef boyut | `25mb`, `7_5mb` |
| `{kalite}` | kodlayıcının kalite kolu | `crf26`, `2500k` |
| `{cozunurluk}` | çıktının yüksekliği | `720p` |
| `{kodek}` | kodlayıcı değil **kodek** | `h264`, `hevc`, `av1`, `vp9` |
| `{tarih}` | bugünün tarihi | `2026-09-19` |

Adlar **dürüst**: `{kalite}` kalite kolunu gösterir, hedef boyutu değil — ikisi ayrı yer
tutucu, çünkü tek ada yığmak adı yalancı yapardı. `libx265` ile `hevc_nvenc` aynı kodeği
üretir, ikisi de `hevc` yazar.

Değeri olmayan yer tutucu boşa düşer ve ardında kalan ayırıcı sadeleşir (`ad__720p` değil
`ad_720p`). Ondalık nokta `_` olur: `7.5mb` uzantıyla karışırdı.

## Değişen yüzey

| Ne | Nerede |
|---|---|
| `AdlandirmaDeseni`, `AdBilgisi` | `src/VidShrink.Core/AdlandirmaDeseni.cs` (yeni) |
| `baseName` parametresi, `KaynakAdi` | `ShrinkEngine.UniqueOutputPath` |
| `OutputNamePattern` | `AppSettings` |
| Ayar satırı + canlı örnek | `MainWindow.axaml` / `.axaml.cs` |
| Kuyruğun kendi kopyası **silindi** | `ShrinkJobWindow.axaml.cs` |
| Yedi `settings-tab.output-name.*` anahtarı | 42 dil |

**Çakışma sayacı desenden sonra işler:** sayı desenin ürettiği adın sonuna eklenir
(`klip_25mb_2.mp4`), desenin içine karışmaz.

**Bozuk desen işi durdurmaz:** ad kurulamadıysa varsayılana dönülür, kodlama iptal edilmez.
Ayar ekranında ise hata anahtarı gösterilir, yani kullanıcı sessizce geri düşürülmez.

**CLI kapsam dışı:** `--cikti` zaten tam yolu veriyor, desen orada bir şey eklemez.

## Mutasyonlar

Filtre: `AdlandirmaDeseniTests|SabitCiktiKlasoruTests`

| # | Kesim | Kırmızı |
|---|---|---|
| taban | — | 0/67 |
| M1 | `Uygula` deseni okumuyor, hep varsayılan | 20 |
| M2 | `Sadelestir` kimliğe çevrildi | 4 |
| M3 | `{kodek}` kodlayıcı adını yazıyor | 9 |
| M4 | `YasakIsaretler` denetimi kaldırıldı | **0 — Windows'ta eşdeğer** |
| M5 | Yer tutucu kümesi açıldı | 2 |
| M6 | Motor verilen adı yok sayıyor | 2 |
| M7 | `KaynakAdi` eski `_shrunk` ekini tekrarlıyor | 2 |
| M8 | Kuyruk deseni okumuyor | 1 |
| M9 | Pencere deseni çıktı yoluna geçirmiyor | 1 |
| M10 | Ondalık nokta dosya adında kalıyor | 1 |

**M4 Windows'ta eşdeğer, Linux'ta değil.** `YasakIsaretler` düşünce hemen ardındaki
`Path.GetInvalidFileNameChars()` denetimi Windows'ta aynı dokuz işareti zaten eliyor. Linux'ta
o liste yalnız `/` ve `\0` içerir; `:` `*` `?` `"` `<` `>` `|` oradan geçer ve ad Windows'a
taşınamaz hale gelir. Denetim tam bunun için duruyor ve aynı pim (`YolIsaretiReddediliyor`)
Linux CI koşumunda kesimi kırmızıya çevirir. Kesimin ilk iki denemesi (`return ad;` ve
`if (false)`) derleme hatası verdi; derlenmeyen kesim geçerli mutasyon değil, ikisi de
derlenen biçimle yeniden koşuldu.

## Ölçünün kolları

`AdlandirmaDeseniTests` (53 kol), `SabitCiktiKlasoruTests` ile birlikte 67:

- Altı yer tutucunun değere dönüşmesi; `{kalite}` CRF yokken bit hızına inmesi.
- Değersiz yer tutucunun boşa düşmesi ve ayırıcıların sadeleşmesi (dört yazım).
- Ondalık hedefin ve CRF'in nokta bırakmaması.
- Yedi kodlayıcı adının üç kodeğe inmesi.
- Üç hata kolu ayrı ayrı: boş/boşa düşen desen, yol işareti (yedi yazım), bozuk yer tutucu
  (beş yazım). Olumsuz kontrol: üç geçerli desen hata vermiyor.
- Bozuk desenin ve `null`'ın varsayılana dönmesi; varsayılanın bugünkü davranışı vermesi.
- Kümenin tam olarak altı ad olması.
- `KaynakAdi`'nın eki tekrarlamaması (dört yazım, `shrunk.mp4` negatif kontrol).
- Motorun verilen adı kullanması, ad verilmezse eski gövdeyi kurması (olumsuz kontrol),
  çakışma sayacının desenden sonra işlemesi.
- Kuyruğun kendi kopyasını taşımadığının ve ana pencerenin deseni yola verdiğinin kaynak
  pimleri.
- Yedi anahtarın 42 dilde bulunması; altı yer tutucu adının hiçbir çeviride bozulmaması —
  bozulsa ipucu kullanıcıya olmayan bir ad öğretirdi.

Çeviriler dört alt ajana dağıtıldı (`.calisma/e3/ceviri/grup1..4.json`), her dosya
birleştirilmeden önce yedi anahtar ve altı belirteç için yerinde denetlendi.

## Yol boyunca çıkan karar

`{kodek}` tek başına desen olarak **geçersiz**: kodek bilgisi olmayan bir kaynakta ad boşa
düşerdi. `Hata` bunu sonda bir kez ölçüyor — desen kuru bir `AdBilgisi` ile uygulanıp sonuç
boş kalıyorsa reddediliyor. İlk ölçüm turunda bu, yedi kolu kırmızı yaptı; kural değil ölçü
düzeltildi (`{kodek}` → `{ad}_{kodek}`).
