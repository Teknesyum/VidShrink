# T102 arşivi — damga (T111)

Bu dizindeki on bir `*.json.gz`, T102'nin (`docs/olcumler/auto-mod.md`) her VMAF
sayısını üreten kare kare çıktıdır. **Silinmedi ve silinmemeli:** T102'nin
tablolarının tek dayanağı bu; kodlama çıktıları (`.calisma/`) gitmiş durumda.

**Bu arşivin tamamı kilitsiz ölçerle üretildi.** Ölçüm sırasında `libvmaf`
kareleri zaman damgasıyla eşliyordu; kare kilidi (`settb=AVTB,setpts=N`) T110'da
geldi. T111 aynı on bir koşumu yeniden üretip hem kilitsiz hem kilitli ölçtü;
aşağıdaki mekanizma ölçülen mekanizmadır.

## Kaymayı üreten şey

Grafik kayması = videonun kap içi `start_time`'ı **eksi** kaptaki en erken
akışın `start_time`'ı. Kaynak `parca-2.mkv` 0,020000 s taşıyor. Framesync tek bir
dosyanın kaymasına değil, **iki kaymanın farkına** bakıyor.

Kaymanın tam bir kare olduğu ölçüldü, çıkarılmadı: kilit takılıyken referans
bilerek `setpts=N+1` ile bir kare kaydırıldığında `auto` koşumu kilitsiz ölçümün
**dört sayısını da birebir** veriyor (94,448 / 94,525 / 56,308 / 26 kare).
Ters yön (`N−1`) benzer hasar veriyor ama aynı sayıları vermiyor.

## Koşum başına — hangi dosyada kayma var, ne kadar

| dosya | kap içi kayma | kaynağa göre fark | kilitsiz `<1` kare | kilit ne değiştiriyor |
|---|---|---|---|---|
| `auto` | 0,016667 s | −0,003333 s = −0,200 kare | 26 | ort +1,198, harm +39,334, min 0,000 → 92,376 |
| `auto-olceksiz` | 0,016667 s | −0,200 kare | 26 | ort +1,198, harm +39,334 |
| `e1-preset4` | 0,016667 s | −0,200 kare | 26 | ort +1,189, harm +39,295 |
| `e2-gop300` | 0,016667 s | −0,200 kare | 26 | ort +1,209, harm +39,458 |
| `e3-olcek810` | 0,016667 s | −0,200 kare | 26 | ort +1,153, harm +35,745 |
| `uzman-biz3` | 0,016667 s | −0,200 kare | 26 | ort +1,222, harm +39,609 |
| `y1-g300-izgara` | 0,016667 s | −0,200 kare | 26 | ort +1,209, harm +39,454 |
| `y2-g300-hizali` | 0,016667 s | −0,200 kare | 26 | ort +1,201, harm +38,385, **p10 +2,343** |
| `y3-hizali-boyutesit` | 0,016667 s | −0,200 kare | 26 | ort +1,205, harm +38,394, **p10 +2,448** |
| `uzman-hb` (x265) | 0,020000 s | **0,000000 s = 0 kare** | 0 | ort +0,010, min 74,692 → 94,211 |
| `uzman-hb2` (x265) | 0,020000 s | **0,000000 s = 0 kare** | 0 | ort +0,010, min 74,673 → 94,156 |

**İki HandBrake koşumu kaymasız değil** — kaynağın kaymasının aynısını taşıyor,
farkı sıfır. Temiz olan mutlak damgaları değil, farkları. Onlarda da kusur var
ama yalnız kuyrukta: kaynağın kendi damga gürültüsündeki 180 karede.

## Bu tablodaki "kilit ne değiştiriyor" sütunu nereden geliyor

T111'in **yeniden ürettiği** dosyalardan, arşivin kendisinden değil — arşiv
kilitsiz, kilitli karşılığı yok. Yeniden üretimin payı ayrıca ölçüldü ve on bir
koşumun hepsinde ortalamada ≤ 0,013: arşiv ile T111'in kilitsiz ölçümü birbirini
tutuyor, o yüzden kilitli sütun arşive de okunabilir.

Yeniden üretim `3688336` tabanında yapıldı (T110'un mührü). Kodlama komutları
`tools/auto-mod-olcumu/t111-uret.sh`, kayma ölçümü `t111-kayma.sh`, damga
dökümü `t111-damga.sh`, VMAF `t111-olc.sh`, kaydırma kimliği `t111-kaydir.sh`.

## Ölçülmedi

- Kare kare damga farkı yalnız dört koşumda alındı (`auto`, `uzman-biz3`,
  `uzman-hb`, `uzman-hb2`). Kalan yedisi için kanıt yalnız kap ofseti.
- `y2`/`y3`'ün p10'unun neden diğerlerinin yedi katı oynadığı.
- Arşivin kendisi kilitli ölçülemez (kodlama çıktıları yok); kilitli sayılar
  T111'in yeniden ürettiği dosyalardan.
