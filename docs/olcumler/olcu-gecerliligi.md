# Ölçü geçerliliği — VMAF sıfırlarının sebebi ve kirlenen geçmiş sayılar

T106. Kaynak veri: T102'nin ham çıktısı
`.claude/worktrees/agent-a3f969792f618f57f/.calisma/t102/` (yeniden kodlama yapılmadı,
mevcut sekiz VMAF JSON'u ve sekiz çıktı dosyası üzerinden ölçüldü).

Sonuç tek cümlede: **görüntüde bir kusur yok, ölçüde var.** Ölçüm boru hattımız
kareleri zaman damgasına göre eşliyordu ve bizim çıktımızın zaman damgası kaynağınkinden
3,3 ms erken başladığı için her kare **komşu referans karesiyle** karşılaştırılıyordu.

---

## Sözleşmenin öncülü düzeltildi

Sözleşme "beş SVT-AV1 koşumunun birebir aynı **26 karesinde** VMAF-NEG **0**" diyor.
Ham JSON'dan sayıldığında bu iki ayrı eşiğin karışmış hâli:

| koşum | kodek | vmaf **== 0** | vmaf **< 1,0** | min |
|---|---|---|---|---|
| auto | libsvtav1 | 25 | 26 | 0,000 |
| auto-ölçeksiz | libsvtav1 | 25 | 26 | 0,000 |
| e1-preset4 | libsvtav1 | 25 | 26 | 0,000 |
| e2-gop300 | libsvtav1 | 25 | 26 | 0,000 |
| e3-ölçek810 | libsvtav1 | 25 | 26 | 0,000 |
| uzman-biz3 | libsvtav1 | **24** | 26 | 0,000 |
| uzman-hb | x265 (HandBrake) | 0 | 0 | 74,701 |
| uzman-hb2 | x265 (HandBrake) | 0 | 0 | 74,667 |

26 sayısı `<1,0` eşiğinden geliyor; T102'nin `tablolar.py:24` satırı
`sum(1 for x in s if x < 1.0)` sayıp sütuna "sıfır puanlı kare" adını vermiş.
Tam sıfır sayısı dörtte 25, `uzman-biz3`'te 24 — orada 3389. kare 0,132882.

**"Birebir aynı" iddiası `<1,0` eşiğinde doğru, `==0` eşiğinde değil.** Ölçüldü:
**dokuz** AV1 koşumunun `<1,0` kümesi birbirinin aynısı, 26/26 örtüşüyor, simetrik
farkları boş. İki HandBrake koşumunda küme boş. Tam sıfır sayısı ise koşumdan
koşuma **24-25** arasında oynuyor — kümenin sabit, sıfır sayısının değişken
olması `max(x,1)` kelepçesinin ikisini ayırt etmediğini gösteriyor.

Ortak küme, tam tanımıyla: **{1699} ∪ {3385…3408} ∪ {3410}** — yani
`{1699} ∪ ({3385…3410} \ {3409})`, toplam **26 kare**.
Sınır kareleri: 3384 = 2,15 ve 3409 = 12,38 kümenin **dışında**;
3406 = 0,945848 < 1,0 olduğu için kümenin **içinde**. Blok bu yüzden
"kesintisiz" değil: 3409 aralığın ortasında bir delik açıyor.

Bu ayrım pratikte önemsiz, çünkü bench'in harmonik formülü
`scores.Count / scores.Sum(x => 1.0 / Math.Max(x, 1.0))` — **`max(x,1)` kelepçesi
0 ile 0,13'ü aynı kefeye koyuyor.** Kelepçe için 24 ile 26 arasında fark yok;
belirleyici olan `<1,0` kümesinin büyüklüğü.

---

## K1 — 26 karenin ortak nedeni: **bulundu**

**Bu kareler, klipteki tek "görüntünün gerçekten değiştiği" karelerdir.**

Kaynak bir oyun menüsü kaydı: 3624 karenin neredeyse tamamı durgun envanter/karakter
ekranı. Görüntünün kare kareye değiştiği yalnız iki yer var:

- **1699** — 1699/1700 arasında sahne kesimi (`motion_sad` 1700'de 0,004 → **29,49**).
- **3384-3410** — menü geçiş animasyonu; `motion2` 0,01'den **4-8**'e çıkıyor,
  3411'de yeni sahne kesimi (`motion_sad` **29,40**).

Ölçü kareleri bir kaydırarak eşliyordu. Durgun ekranda `kare[n]` ile `kare[n-1]`
neredeyse aynı olduğu için kaydırma görünmüyor (PSNR 50 dB'de kalıyor, VMAF 95'te).
Görüntünün değiştiği bu iki yerde ise iki **farklı** resim karşılaştırılıyor ve skor
çöküyor. Kümenin altı koşumda birebir aynı olması bu yüzden tesadüf değil: küme
kodlayıcının değil **kaynağın** özelliği — hangi karelerde hareket olduğu.

### Elenen hipotezler

| hipotez | nasıl elendi |
|---|---|
| Gerçek kalite çöküşü | 3392. karede AV1 çıktısı referansın Laplace enerjisinin **%98,8**'ini koruyor (temiz karelerdekiyle aynı) ve indeks kilitli PSNR **48,10 dB**. VMAF yine de 0. |
| Siyah/düz kare, sıfır varyans | Kareler çıkarılıp bakıldı: metin ve ince çizgi dolu menü ekranları. `ref_std` 7000-8000 (10 bit), düz değil. |
| Sahne kesimi karesinin kendisi bozuk | Kesim karelerinin **kendisi** temiz; bozuk görünen, kesimin yanlış tarafıyla eşlenen kare. |
| Ölçekleme adımı (`scale=1920:1080:lanczos`) | T102 ölçeksiz kontrol koşumu yaptı, sonuç birebir aynı çıktı (`auto-olceksiz.json`, md5 farklı ama istatistikler özdeş). Doğru eleme — ama sebep "bulunamadı" diye bırakılmış. |
| Kodlayıcı ayarı (preset, `-g`, çözünürlük) | Beş farklı ayar, aynı 26 kare. Ayar hiçbir şey değiştirmiyor. |
| AV1'e özgü bir libvmaf arızası | **Yanlış.** Kusur kodlayıcıdan bağımsız — aşağıya bakınız. |
| Kare sayısı uyuşmazlığı | Üç dosyada da tam **3624** kare. |
| Çözülmüş kare sırasında kayma | `test[n]` ile `ref[n-1]`, `ref[n]`, `ref[n+1]` tek tek karşılaştırıldı: **her karede `d=0` kazanıyor.** Çözülen görüntüde kayma yok; kayma eşlemede. |

---

## K2 — Arızanın yeri: **(b), boru hattımız.** (a) ve (c) elendi

### (c) SVT-AV1 çıktısı gerçekten bozuk — **elendi**

Aynı kare çiftleri indekse kilitlenerek yeniden ölçüldü:

| ölçü | eşleme zaman damgasına göre | eşleme indekse kilitli |
|---|---|---|
| PSNR-Y, tüm dosya, en düşük kare | **17,21 dB** (kare 1699) | **44,74 dB** (kare 2028) |
| PSNR-Y kare 3392 | 24,06 dB | 48,10 dB |
| PSNR-Y kare 3410 | 17,29 dB | 45,97 dB |

Dosyanın hiçbir yerinde 44,7 dB'nin altına inen kare yok. Çıktı sağlam.

### (a) libvmaf'in kendisi AV1'de kareleri yanlış hizalıyor — **elendi**

Kusur libvmaf'te olsaydı `psnr` filtresinde görünmezdi. Görünüyor: aynı dosya çifti,
aynı ffmpeg, sadece `psnr` — **VMAF'in çöktüğü 26 karenin hepsinde** PSNR de
çöküyor. Ortak olan şey libvmaf değil, ikisinin de kullandığı **framesync**.

Sınırını yazmak gerekiyor: iki küme **birebir aynı değil**. Damga eşli PSNR'de
`psnr_y < 40 dB` olan **93 kare** var; VMAF'in 26 karesi bunların **öz alt kümesi**
(67 kare fazladan). Eleme yine de geçerli — elemeyi taşıyan şey "kümeler eşit"
değil, "VMAF'in suçladığı her kareyi libvmaf'siz bir ölçü de suçluyor". Kalan 67
kare de aynı kaymanın eseri; PSNR eşiğinin 40 dB'de olması onları görünür,
VMAF'in doygunluğu ise görünmez kılıyor.

### (b) Boru hattımız kareleri kaydırıyor — **kaldı, sebebi bulundu**

Kaynağın video akışı **0,020000 s**'de başlıyor (Matroska, 1/1000 zaman tabanı,
milisaniyeye yuvarlanmış ve titreşimli damgalar: 0,016/0,017/0,018 s aralıkları).
Bizim çıktımız **0,016667 s**'de başlıyor — kaynağın başlangıç kaymasını atıp tam
1/60'a oturuyor. Fark her karede **−3,3 ms**.

ffmpeg framesync kareleri damgaya göre eşler ve "damgası küçük-eşit olan **son**
referans karesini" seçer. Bizim kare hep erken geldiği için seçilen kare komşusu
oluyor. Kayma 3,3 ms; kare süresi 16,67 ms — yani **yarım kareden küçük bir kayma
tam bir kare kaydırma üretiyor.**

Filtre grafiği `Program.cs`'de `setpts` içermiyordu:

```
[0:v]scale=w=1920:h=1080:flags=lanczos[t];[t][1:v]libvmaf=...
```

**HandBrake neden kurtuluyor:** çıktısını kaynağın **0,020000** başlangıcıyla
yazıyor, kayması ±1 ms içinde kalıyor. Tek istisnası kanıtın kendisi: 2721. karede
HandBrake'in damgası **−1 ms**'e düşüyor ve tüm dosyada tek başına düşen PSNR karesi
(**34,5 dB**, diğer her karede ≥ 47,95 dB) **tam da 2721**. Yani 1 ms'lik negatif
kayma bile tam kare kaydırıyor.

### Kusur AV1'e özgü değil — bu, sözleşmenin öncülünün ikinci düzeltmesi

T102'deki altı çıktının hepsi AV1 olduğu için kusur AV1'e atfedilmiş. Aynı kaynak
üç kodlayıcıya kodlandı ve `start_time` okundu:

| kodek | kap | start_time |
|---|---|---|
| kaynak | mkv | **0,020000** |
| libsvtav1 | mp4 | 0,016016 |
| libx264 | mp4 | 0,016016 |
| libx265 | mp4 | 0,016016 |
| libx265 | mkv | 0,017000 |
| HandBrake x265 | mp4 | **0,020000** |

**Kaynağın başlangıç kaymasını kodlayıcı değil, bizim ffmpeg çağrımız düşürüyor.**
Kusur `libsvtav1`, `libx265`, `libx264` ve donanım yollarının hepsini eşit vuruyor.
AV1 sadece T102'de ölçülen taraf olduğu için suçlu göründü.

**Şart:** kaynağın video akışının `start_time`'ı sıfırdan farklı olmalı. `start_time=0`
olan kaynakta kusur ortaya çıkmaz. Kirlenmiş olabilecek bir tabloyu ayıklamanın en ucuz
yolu:
`ffprobe -v error -select_streams v:0 -show_entries stream=start_time -of csv=p=0 <kaynak>`

### Düzeltme

`Program.cs`'de `MeasureFilterGraph.Build` her iki girdiyi de kare indeksine kilitliyor:

```
[0:v]scale=w=..:h=..:flags=lanczos,settb=AVTB,setpts=N[t];[1:v]settb=AVTB,setpts=N[r];[t][r]<ölçü>
```

`settb=AVTB,setpts=N` kare hızından bağımsız çalışır (`setpts=N/FRAME_RATE/TB`
değişken kare hızlı kaynakta bozulur). Bu grafik hem `libvmaf` hem `xpsnr` için
kullanılıyor; ikisi de aynı framesync'ten geçtiği için **XPSNR sayıları da aynı
kusurdan etkilenmişti.**

### Düzeltmeden önce ve sonra, aynı dosyalar

| koşum | min | `<1,0` kare | ortalama | harmonik | p10 |
|---|---|---|---|---|---|
| AV1 auto — eski (damga eşli) | 0,000 | 26 | 94,462 | **56,313** | 94,534 |
| AV1 auto — yeni (indeks kilitli) | **92,391** | **0** | 95,659 | **95,655** | 94,904 |
| x265 HandBrake — eski | 74,701 | 0 | 95,763 | 95,759 | 95,396 |
| x265 HandBrake — yeni | 76,219 | 0 | 95,799 | 95,793 | 95,473 |

**Harmonik ortalamada AV1 ile x265 arası fark: 39,446 → 0,138.**
p10 farkı 0,862 → 0,569. HandBrake tarafı da kusurdan bir miktar etkilenmiş
(min 74,70 → 76,22); yani düzeltme her iki tarafı da yukarı çekiyor, ama
AV1 tarafını kıyaslanamayacak kadar fazla.

Yol haritasının "AV1'i x265'e karşı 39,4 puan geride gösteriyor" tespiti doğruydu;
sebebi yanlıştı. **Ölçülen gerçek fark 0,14 puan — yani bu kaynakta iki kodlayıcı
kalitede başa baş.**

---

## K3 — Harmonik ortalama kalıyor, girdisi ve raporlanışı değişti

**Karar: harmonik ortalama yanlış istatistik değil. Yanlış olan girdiydi ve
kelepçenin sessizliğiydi.**

Gerekçe: harmonik ortalama VMAF için Netflix'in kendi önerdiği havuzlama; kötü
kareleri ortalamadan ağır cezalandırması **istenen** davranış — kullanıcı en kötü
sahneyi izler, ortalamayı izlemez. Onu bu ölçümde işe yaramaz yapan şey formül değil,
ona verilen bozuk diziydi. Girdi düzelince harmonik (95,655) ortalamaya (95,659)
0,004 puan yaklaşıyor; yani sağlıklı dizide zaten sakin davranıyor.

Kalacağı girdi: **kare eşlemesi indekse kilitlenmiş** bir libvmaf koşumunun skorları.

`max(x, 1.0)` kelepçesi **duruyor** — sıfıra bölmeyi engelliyor ve onsuz tek bir 0
sonucu tanımsız yapıyor. Ama kelepçe artık **sessiz değil**:

- `VmafPool.FloorClampedFrames` — tabana kelepçelenen kare sayısı,
- `VmafPool.Min` — **kelepçelenmemiş** gerçek en düşük skor,
- `VmafPool.Suspect` — kelepçelenen kare varsa doğru.

Harmonik ortalamanın basıldığı üç yer, kelepçeyi **iki ayrı biçimde** ama üçü de
görünür şekilde yazıyor:

- `shrink` özeti (`Program.cs:788`) ve `compare` çıktısı (`:917`) serbest metin
  damgası basıyor: `(SUPHELI: n kare 1 altında, tabana kelepcelendi)`.
- `peak-curve` tablosu (`:484-527`) markdown olduğu için serbest metin
  basamıyor; onun yerine **başlıklı iki sütun** ekliyor: `min` ve `kelepce`.

Bu ayrım kasıtlı: tabloya serbest metin damgası basmak sütun hizasını bozar.
Doğrulaması iddiaya değil çıktıya soruldu — `peak-curve` gerçek koşumda
başlık, ayıraç, veri ve hata satırlarının **dördü de 12 hücre** üretiyor. Kelepçenin
0 ile 0,13'ü ayrı tutmadığı `VmafPoolingTests` içinde ölçüye bağlandı:
`SifirIleTabanAltiKucukDeger_HarmonikOrtalamada_AyniKefeyeKonur`.

Kelepçenin bu ölçümdeki ağırlığı: 26 kelepçelenmiş kare, 3624 karelik dizide
ters toplamın **%40,6**'sını tek başına oluşturuyor (26,0 / 64,07).

---

## K4 — Kirlenen geçmiş sayılar

**Sayılar düzeltilmedi, işaretlendi.** Aşağıdaki dosyalar başka sözleşmelerin
`owns`'unda; dağıtım T0'a ait.

Damga: **ölçü kusuru, yeniden ölçülmeli.**

Her satır için geçerli test: ölçümün kaynağının `start_time`'ı sıfırdan farklı mı,
ve karşılaştırılan iki taraftan biri bizim boru hattımızdan mı çıktı. Kaynak
`start_time=0` ise satır temizdir.

### Yüksek öncelik — bizim çıktımız, harmonik/p10/min raporluyor

`docs/olcumler/kazanc-kullaniciya-ulasiyor-mu.md`
- `:26` `parca-2 | önce | mean 94,72 | harm 56,42 | p10 95,14` — **T102'nin 56,313'üyle
  aynı imza, aynı kaynak (`parca-2`)**; harmonik ortalamanın 38 puan altında.
- `:27` `parca-2 | kapı | 91,28 | 55,16 | 90,75`
- `:28` `parca-2 | taban | 94,72 | 56,41 | 95,12`
- `:23-25` parca-1 satırları, `:29-31` parca-3 satırları (`:30` harmonik **20,10**,
  p10 8,51 — aynı imza, daha ağır)
- `:41` "harmonic +0,52, p10 +4,14" ve `:46` "mean −3,44, p10 −4,39" — bu farklar
  kirlenmiş sütunlardan kuruluyor.

`docs/olcumler/handbrake-acigi.md` — VidShrink tarafı `av1_nvenc`, karşısı HandBrake.
Kusur **yalnız bizim satırlarımızı** vurduğu için buradaki açık olduğundan büyük görünüyor.
- `:36` metrik tanımı (harmonik + parantezde p10)
- `:40`, `:42`, `:44`, `:46`, `:48`, `:50` — `av1_nvenc` satırları
- `:41`, `:43`, `:45`, `:47`, `:49`, `:51` — HandBrake karşılıkları (karşılaştırma
  ortağı; bunlar daha temiz, ama fark cümlesi ikisinden çıktığı için o da şüpheli)
- `:87-91` ablasyon tablosu; `:88` "**−50,26 VMAF**" iddiası
- `:149-153` harmonik/p10/XPSNR tablosu
- `:155` "harmonik VMAF 10,07 ve p10 11,87 düşürdü … yazılım yolu harmonik +8,65,
  p10 +13,76"
- `:119`, `:121`, `:123` HDR karşılaştırması ("harmonikte 10,18, p10'da 14,60")

`docs/olcumler/tepe-tavani-ve-psy.md` — `av1_nvenc` p5
- `:26` `Kapalı | mean 75,197 | harmonik 63,440 | p10 36,772`
- `:27` `Açık | 75,292 | 63,597 | 36,910`
- `:28`, `:30` fark satırı ve düzyazısı
  (harmonik mean'in 12, p10'un 38 puan altında — imza)

`docs/gpu-kodlama-bulgusu.md`
- `:19-21` `libx265 slow 66,44` ↔ `av1_nvenc p7 65,62` ↔ `hevc_nvenc 62,01`
- `:24-26` "0,8 VMAF geride … x265 slow ile başa baş"
- `:120` ve `docs/cpu-algoritma-checkup.md:37`, `:120` bu sayıyı devralıyor

`docs/olcumler/ornekte-vmaf-maliyeti.md` — kodlayıcı adlandırılmamış, doğrulanmalı
- `:70-73` dört satır mean/harmonik/p10
- `:77` "harmonic +0,025, p10 +0,441 … harmonic +0,121, p10 +0,370"

`docs/olcumler/algi-olcusu.md` — `libx264`
- `:169-173` `harmonik 86,3932 / 12,5446`, `p10 88,2500 / 5,7337`,
  `min 0,7867 / 0,0000`, `XPSNR 40,3535 / 21,3199`
- `:97-99` ham/kelepçeli harmonik tablosu
- `:131-137` — handbrake-acigi'nin 10,18 / 14,60 farklarının "etkilenmedi" denetimi.
  **Bu denetim yalnız kelepçeyi kapsıyor, framesync kusurunu kapsamıyor** —
  yani "etkilenmedi" sonucu bu kusur için geçerli değil, yeniden yapılmalı.

### İkinci öncelik — aynı boru hattı, bizim çıktımız, AV1 değil

`docs/motor-dogrulama-raporu.md` (`libx264/2pass`)
- `:341-402` 60 satırlık kalite tablosu (harmonik + p10 + XPSNR)
- `:126-130` 1 MB vakası VMAF farkları (`:129` k4 **−20,77**)
- `:199-205` `--fill qualityceiling` tablosu
- `:34`, `:105` kapı cümleleri

`docs/olcumler/olculen-kaliteyle-plan.md` (`hevc_nvenc` + yazılım)
- `:38-45`, `:50-53`, `:61`, `:182-197`, `:212-215`, `:222-224`, `:236`

`docs/macos-guncelleme.md`
- `:281` `VMAF-NEG harm=94.29 p10=93.55, XPSNR=45.45`

### T102'nin kendi raporu — sebep cümlesi yanlış, sayılar kirli

`docs/olcumler/auto-mod.md` (T102 dalında; `main`'e geldiğinde damgalanmalı)
- `:38-41`, `:48-54`, `:56-61`, `:66-67`, `:76` — 26 kare / 39 puan anlatısı
- `:69-70` ve `:435-439` — **"ölçekleme adımı sebep değil"** doğru bir eleme ama
  sebep bulunamadı diye bırakılmış; sebep burada yazılı
- `:197-201`, `:208` — `harmonik 56,313 / 56,472 / 95,727`, "HandBrake − auto:
  harmonik **+39,414**" → **gerçek fark 0,138**
- `:214-215` "harmonik sütunu okunmamalı" gerekçesi — artık okunabilir, girdi düzeldi
- `:231-233`, `:239`, `:275-277`, `:299` — Δp10 ablasyon sayıları
- `:420-439` kusur 4 metni

`.claude/relay/YOL-HARITASI-2.md`
- `:113-115` T102 satırları
- `:218-220` "AV1'i x265'e karşı **39,4 puan** geride gösteriyor" → **0,14 puan**
- `:222-228` kodlayıcı seçim kuralının bu sayıya dayandığı uyarısı — **kural artık
  düzeltilmiş ölçüyle yeniden kurulabilir**

### Ölçüm düzeneğinin kendisi — sayı değil, sayım kusuru

`tools/auto-mod-olcumu/tablolar.py`
- `:24` `sifir = sum(1 for x in s if x < 1.0)` — sütun başlığı "sıfır puanlı kare",
  saydığı şey "1'in altındaki kare". Sözleşmedeki **26** buradan geliyor.
  Gerçek tam sıfır sayısı koşuma göre **24-25**. Bu satırdan çıkan her tablo
  ikinci bir damga istiyor: **ölçü kusuru değil, sayım/adlandırma kusuru.**
  İkisi bağımsız — framesync düzeltilince sütun sıfırlanacak ama başlığı hâlâ
  yanlış şeyi sayıyor olacak.

### Temiz görünenler

`docs/olcumler/` altında VMAF sayısı bulunmayanlar: `T30-panel-olcumleri.md`,
`T32-anahtar-kare-olcumleri.md`, `T33-oynatma-olcumleri.md`, `T37-sunum-olcumleri.md`,
`ceviri-olcusu-mutasyonu.md`, `sahne-haritasi.md`, `suit-esszamanli-kosum.md`,
`surecler-arasi-olcu-yalitimi.md`, `t27-ipucu-satir-genislikleri.md`,
`t27-ipucu-satir-genislikleri-once.md`, `t84-tur2-mutasyon.md`.

---

## K5 — Bench artık auto modu ölçüyor

`Program.cs:663` `PlanOptions.Codec`'i hiç kurmuyordu; `PlanOptions` varsayılanı
`CodecPreference.Compatible`, uygulamanın varsayılanı ise `Auto`. Bench ürünün
sevk edilen modunu hiç ölçmemişti.

`--codec-preference auto|compatible|maxcompression|fast` eklendi, **varsayılan `Auto`** —
yani bench artık uygulamanın kendi varsayılanıyla ölçüyor.

Bu ayrım **ölçüye bağlandı**, koşum kaydına değil:
`KodekTercihi_AutoIleCompatible_AgresifHedefte_FarkliKodlayiciSecer` ve
`KodekTercihi_PlanaGercektenGecer_VarsayilaninaDusmez` — ikincisi bütün
`CodecPreference` değerlerini plana sokup en az ikisinin farklı kodlayıcı
seçtiğini doğruluyor, yani tercihin plana **geçtiğini** tutuyor.

Koşumun kendisi `.calisma/` altında ve git'e gitmiyor; tekrar üretmek için
kaynağa ihtiyaç duymayan hâli:

```
ffmpeg -y -f lavfi -i "testsrc2=size=1920x1080:rate=60:duration=12"        -c:v libx264 -pix_fmt yuv420p kaynak.mp4
dotnet run --project tools/VidShrink.Bench -c Release --        shrink kaynak.mp4 1.5 --plan-only --codec-preference compatible
dotnet run --project tools/VidShrink.Bench -c Release --        shrink kaynak.mp4 1.5 --plan-only --codec-preference auto
```

Ölçülen koşum (`.calisma/t106/k5/kaynak.mkv`, 12 sn, hedef 1,5 MB, `--plan-only`):

| `--codec-preference` | seçilen kodek |
|---|---|
| `compatible` (bench'in eski sabit davranışı) | `libx264` |
| `auto` (uygulamanın varsayılanı) | `libsvtav1` |

Aynı kaynak, aynı hedef, **farklı kodlayıcı.** Bench bugüne kadar bu rejimde
kullanıcının hiç görmediği bir kodlayıcıyı ölçüyordu.

Uçtan uca gerçek koşum (`shrink … 1.5 --codec-preference auto --no-calibrate`):

```
kaynak | fill=FillTarget | codec-tercihi=Auto | prob 5,4s | kalibre=False | olculen kalite=yok
1,5 MB -> 1,44 MB (95,8%), bant=ic tasma=yok taban=ok, 1920x1080@60, libsvtav1/2pass, 873k,
deneme=1, kalibre=hayir, plan=0,3s, sure=10,9s,
VMAF-NEG mean=94.34 harm=94.34 p10=94.11 min=94.02, XPSNR=45.43 (y=43.84 u=47.31 v=49.91)
```

Başlık satırı artık `codec-tercihi`yi yazıyor (koşumun hangi rejimi ölçtüğü rapordan
okunabiliyor), sonuç satırı `min=`i yazıyor. Taban altı kare olmadığı için `SUPHELI`
uyarısı basılmadı; `harm` ile `mean` 0,00 farkla örtüşüyor — düzeltilmiş boru hattının
sağlıklı dizideki beklenen davranışı. Süre 10,9 s: **makine paylaşımlıydı**,
karşılaştırma için kullanılmamalı.

---

## Kare kilidinin gerekçesi — T110 bunu referans alacak

Bu bölüm ölçüm aracının dışında da geçerli. `src/VidShrink.Ffmpeg/QualityMeter.cs:278`
üretim yolunda **aynı açık duruyor**: kare kilidi yok. `--measured-quality` ve
kalibrasyon çıpaları o yoldan geçiyor. **Bu sözleşme oraya dokunmadı** (sahibi
T110); aşağısı o iş için gerekçedir.

Neden `settb=AVTB,setpts=N`, neden başka bir şey değil:

- **Neden bir şey gerekiyor.** ffmpeg'in iki girdili filtreleri (`psnr`, `libvmaf`,
  `xpsnr`) kareleri **zaman damgasıyla** eşler: "damgası test karesinden küçük-eşit
  olan **en son** referans karesi". Yarım kareden küçük, tek yönlü bir sapma bile
  tam bir kare kaydırma üretir. Sapma sabit olduğu için de kaydırma **her karede**
  tekrarlanır.
- **Neden `setpts=N`.** Kareleri damgaya değil **sıraya** göre eşler. Kaynak ile
  çıktının kare sayısı aynı olduğu sürece — ki ölçüm bunu zaten varsayıyor —
  n'inci kare n'inci kareyle karşılaşır. Kare hızından bağımsızdır;
  `setpts=N/FRAME_RATE/TB` bilinen ve sabit bir kare hızı ister, `setpts=N` istemez.
- **Neden `settb=AVTB` önce.** `N` sayacını sabit ve ince bir zaman tabanına yazar;
  girdinin kendi tabanı kaba olduğunda (ör. 1/30) kesir tik'ler yuvarlanmaz.
- **Neden ölçeklemeden sonra.** `scale` kendi çıktı damgasını üretir; kilit önce
  gelirse ölçekleyici onu ezer.
- **Sınırı.** Kilit kare **sayıları eşitse** doğrudur. Ölçüm zaten eşit sayı
  varsayıyor; eşit değilse doğru davranış hizalamak değil, **ölçümü reddetmektir**.
  Bu sözleşme o reddi eklemedi — T110 için açık uç.

---

## K6 — Düzeltmeyi tutan ölçüler

`tests/VidShrink.Tests/VmafPoolingTests.cs`, **15 ölçü**. Havuzlama tarafı sabit
dizilerle, kare kilidi tarafı **gerçek ffmpeg koşumuyla** ölçülüyor.

Havuzlama (saf, hızlı):

- taban altı kare yokken harmonik tanıma birebir uyuyor,
- taban altı kareler sayılıyor, `Min` **kelepçelenmeden** raporlanıyor,
- 0 ile 0,132882 harmonikte aynı kefeye konuyor ama `Min` ve ortalama ayırıyor,
- tek bir 0 harmonik ortalamayı, ortalamanın düşüşünün 20 katından fazla düşürüyor,
- taban altı kare sayısı arttıkça harmonik tek yönlü düşüyor (0/1/5/25/26),
- sabit dizide üç istatistik de aynı değeri veriyor,
- boş dizi 0 değil `null` dönüyor, NaN sessizce yutulmuyor.

Kare kilidi (davranışsal — kusurun küçük ölçekli kopyası):

Ölçü, T106'nın kusurunu minyatürde yeniden kuruyor. Tek bir kaynak üretiliyor
(`testsrc2`, 160x120, 30 fps, 60 kare, kayıpsız ffv1) ve **kendisiyle**
karşılaştırılıyor; referans girdiye `-itsoffset 0.004` veriliyor. 4 ms, 33 ms'lik
karenin altında — tam olarak sahadaki 3,3 ms'lik kayma gibi.

- kilitliyken 60 karenin hepsi tam eşleşiyor (`psnr_y = inf`),
- kilit olmadan aynı çift çöküyor (`psnr_y` en düşük **23,53 dB**),
- iki akış birbirine göre bir kare kaydırılınca skor dizisi bozuluyor.

**Sınırı yazmak gerekiyor:** bu üç ölçü ffmpeg istiyor, bu yüzden `[FfmpegFact]`
ile işaretli — ffmpeg'i olmayan koşucuda **atlanıyor, düşmüyor.** CI koşucusunda
ffmpeg yok (aynı süitte 83 test bu sebeple atlanıyor; yerelde 17). Yani kare
kilidini tutan pim **yerelde ve ffmpeg'i olan koşucuda** koruyor, CI'da
korumuyor. Havuzlama ölçüleri saf, her yerde koşuyor. Kilidi CI'da da tutmak
istenirse yapılacak iş ffmpeg'i koşucuya kurmaktır; bu sözleşme onu yapmadı.

Mutasyon denemesi — her satır kaç ölçü düşürüyor. **Her tur `--no-incremental`
ile yeniden derlendi;** artımlı derleme bir turda bayat ikili koşturup yanlış
sonuç verdi:

| mutasyon | düşen ölçü |
|---|---|
| `Math.Max(raw, HarmonicFloor)` → `raw` (kelepçe kalkar) | 2 |
| `Min` kelepçelenmiş değerden hesaplanır | 2 |
| taban altı sayacı yalnız `raw == 0.0` sayar | 2 |
| **`FrameLock` → `settb=AVTB` (kare kilidi kalkar)** | **2** |
| `FrameLock` → `settb=AVTB,setpts=N+1` | 0 — **eşdeğer mutasyon** |
| `FrameLock` → `settb=AVTB,setpts=PTS-STARTPTS` | 0 — zayıf ama geçerli almaşık |

Son iki satır ölçünün açığı değil, mutasyonun kendi özelliği; ikisi de yazılmadan
bırakılmıyor:

- **`setpts=N+1` eşdeğer.** Sabit iki dalda birden kullanılıyor, yani her iki akışı
  **aynı miktarda** kaydırıyor. Framesync eşleşmesi değişmiyor; yalnız çıktının
  mutlak damgaları bir tik kayıyor. Hiçbir davranışsal ölçü bunu yakalayamaz,
  çünkü ortada davranış farkı yok. Yakalanması gereken şey **göreli** kayma, ve o
  ayrı bir ölçüyle pimlenmiş durumda (üstteki üçüncü madde).
- **`PTS-STARTPTS` hayatta çünkü bu kusuru gerçekten düzeltiyor.** İki akışı da
  başlangıcına göre sıfırlıyor, sabit ofseti siliyor. `setpts=N`'i tercih etme
  sebebi: damga *titreşimliyse* (kare başına değişen sapma) `PTS-STARTPTS`
  yetmez, indeks kilidi yeter. Bu üstünlüğü **ölçemedim** — jitter'lı bir damga
  dizisini bu ffmpeg sürümünde diske yazdıramadım (mkv/nut/avi üçü de CFR'ye
  normalize etti), o yüzden almaşığın zayıflığı burada **gerekçe, kanıt değil**.

---|---|
| `Math.Max(raw, HarmonicFloor)` → `raw` (kelepçe kalkar) | 2 |
| `Min` kelepçelenmiş değerden hesaplanır | 2 |
| taban altı sayacı yalnız `raw == 0.0` sayar | 2 |
| `FrameLock` → `"null"` (kare kilidi kalkar) | 2 |

---

## Ölçülemeyen / bilerek bırakılan

- **Yalnız tek kaynakta ölçüldü** (`parca-2.mkv`, oyun menüsü, 1080p60, HDR10,
  `start_time=0,020`). Kusurun büyüklüğü kaynağa bağlı: `start_time=0` olan kaynakta
  hiç görünmez, hareketi az olan kaynakta az görünür. Yukarıdaki geçmiş sayı listesi
  bu yüzden "kirlenmiş" değil **"yeniden ölçülmeli"** diye damgalandı.
- **Geçmiş tablolar yeniden ölçülmedi** — sözleşme dışı, dosyalar başka
  sözleşmelerin `owns`'unda.
- **`start_time`'ı neden düşürdüğümüz kovalanmadı.** Kusur ölçüm tarafında
  kapatıldı (eşleme artık damgaya bakmıyor). Ama teslim edilen dosyanın kaynaktan
  3,3 ms kaymış olması **ürün tarafında ayrı bir soru**: ses/görüntü eşzamanı
  bundan etkileniyor mu? Ölçülmedi, ayrı sözleşme ister.
- **Yerel tam süit bir ölçüde kırmızı verdi:**
  `PerformanceCheckTests.OlcumYukAltindaYalnizAgirlasiyor` (1100 geçti, 1 kaldı).
  Ölçü boş makinede ffmpeg hızını ölçüp `ProcessorCount-1` iş parçacığıyla yük
  bindirdikten sonrakiyle kıyaslıyor; altı ajan koşarken "boş" okuma boş değil.
  İki bağımsız kanıtla eleme: tek başına koşturulunca **geçti**, ve aynı commit'te
  **CI yeşil** (`33575828972`). Bu sözleşmenin dokunduğu hiçbir dosya
  (`tools/VidShrink.Bench`) bu ölçünün yolunda değil.
- **Süre sayısı verilmedi.** Makine altı ajanla paylaşımlıydı; kodlama koşumlarında
  iş parçacığı sabitlendi (`-threads 4`, `pools=4`, `lp=4`) ama süre yine de
  güvenilmez. Bu belgedeki **kalite ve boyut** sayıları paylaşımdan etkilenmez.
