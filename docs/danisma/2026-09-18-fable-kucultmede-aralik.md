# Danışma — Küçültmede Aralık (fable, 18 Eylül 2026)

Soran: T0 alt ajanı, dal `t0/hb-acik-kalan`. Danışılan: fable. HandBrake açığı madde 54.
Soru ve cevap birebir; özet değil.

## Gönderilen soru (birebir)

# Danışma: Küçültmede Aralık (HandBrake Açığı, Madde 54)

Tarih: 2026-09-18. Soran: T0 alt ajanı, dal `t0/hb-acik-kalan`.

## Bağlam

VidShrink hedef boyuta sıkıştıran bir araç (.NET 8 + ffmpeg). "HandBrake her alanda
geçilsin" işinin envanterinde 39 madde var; 8 kapandı, 11 açık, 19 kısmi. Açık maddeler
arasında **madde 54 "küçültmede aralık"** seçildi.

Bugünkü durum, koddan doğrulandı:

- `src/VidShrink.Core/EncodePlan.cs` — Start/End/Trim/Range alanı **hiç yok** (99-130).
- `src/VidShrink.Core/ConversionPlan.cs:17-18` — `TimeSpan? Start`, `TimeSpan? End` var,
  ama bu **dönüştürücü** yolu; hedef boyut hesabı yapmaz.
- `src/VidShrink.Core/ConversionArguments.cs:42` — `if (plan.Start is { } start) a.AddRange(["-ss", ...])`.
- `src/VidShrink.Core/FfmpegArguments.cs:566-575` — `BuildSegment(info, plan, startSeconds,
  durationSeconds, ...)` var ama **yalnız ölçüm parçası** için; `-ss` girdiden önce, ikinci
  geçiş üretmiyor, üretim yolunda çağıran yok.
- CLI'ın `--aralik` bayrağı (`CliRequest.cs:120`) klasör **yoklama periyodu**, kesit değil.

HandBrake `--start-at duration:S --stop-at duration:S` ile kare hassasiyetli kesit
küçültüyor. Bizde kullanıcı videonun bir parçasını hedef boyuta sıkıştıramıyor:
ya tüm videoyu küçültüyor, ya önce dönüştürücüde kesip sonra küçültüyor (iki tam kodlama).

## Kendi ölçümlerimiz (danışmaya giren)

1. **`docs/olcumler/sahne-butcesi.md:755-757** — "Referans sahneleri `-ss` ile kesiliyor.
   Kesim noktasi kare sinirina yuvarlanabilir; hata butun sahnelerde ayni yonde ve paylar
   normalize edildigi icin kucuk, ama sifir degil."

2. **`docs/olcumler/sahne-haritasi.md:525`** — "`-ss` girdiden önce (hızlı arama), kare
   çiftinin arası 80 ms — 60 fps'de" (hızlı aramanın kullanıldığı düzenek).

3. **`src/VidShrink.Core/FfmpegArguments.cs:562-564` docstring (ölçülmüş gerekçe)** —
   "`-ss` girdiden **once** gelir: sonra gelirse ffmpeg dosyayi bastan cozer ve 2 sn'lik
   bir parca saniyeler surer."

4. **`docs/olcumler/anahtar-kare-tavani.md`** — üretimin `-g` erişim alanı `[5,0 ; 10,0]`
   saniye; kısa uç (5 sn) sekiz kaynak-nokta hücresinin yedisinde 10 sn'ye göre kaybediyor,
   tek pozitif hücre `+0,109` p10 ile eşiğin (0,20) altında. **Yani üretim çıktısında
   anahtar kare aralığı 5-10 saniye**; kaynakta ise anahtar kare aralığı bilinmiyor.

5. **`docs/olcumler/siyah-kenar.md:105-111`** — depoda "ölçüm parçalarının sessizce farklı
   çıktığı" görülmüş; o yüzden A/B kolları ayrı `-ss` ile kesilmiyor, tek dosya okunuyor.

6. **`docs/olcumler/handbrake-kiyas-cli.md:39`** — ürün CLI'ının **toplam** süre oranı
   HandBrake'e karşı 1,64-3,73 (B5 kapısı 8/8 kaldı); fark ürünün ölçümlü deneme
   turlarından (1-4 deneme) geliyor. Yani aralık kodlamasında her deneme turu kesiti
   yeniden okuyacak — arama maliyeti deneme sayısıyla çarpılıyor.

7. `src/VidShrink.Core/OvershootTrim.cs` — hedefi aşan koşumda **çıktıyı kısaltarak**
   bütçeye sığdıran ayrı bir mekanizma var (`TrimPlan(Side, StartSeconds, EndSeconds,
   DurationSeconds, KeptBytes)`); kullanıcının istediği aralıkla çakışma riski var.

## Sorular

**S1 — `-ss` nereye?** Kullanıcının istediği kesit için `-ss` girdiden önce mi (hızlı,
anahtar kareye yuvarlar), sonra mı (kare hassasiyetli, dosyayı baştan çözer), yoksa
melez mi (`-ss` girdiden önce hedefin biraz gerisine, sonra girdiden sonra kalan farkı)?
Ölçüm 6'ya göre arama maliyeti deneme sayısıyla çarpılıyor; ölçüm 1'e göre yuvarlama
hatası sıfır değil. HandBrake kare hassasiyetli. Hangisi, hangi eşikle?

**S2 — bütçe hangi süreden?** Hedef MB kesitin çıktısına mı uygulanır (bence evet)?
O zaman bit hızı hesabında hangi süre kullanılır: kesit süresi mi, kaynak süresi mi?
Plan hesabında kaynak boyutundan türeyen sezgiler (kaynak MB, bit/piksel/kare tabanı)
var; kesitte kaynak MB'yi süre oranıyla ölçekleyelim mi, yoksa gerçek kesit baytını
mı ölçelim (ek bir `ffprobe`/okuma maliyeti)?

**S3 — `-t` mi `-to` mu?** `-ss` girdiden önceyken `-to` girdi zamanına göre mi çıktı
zamanına göre mi çalışır; hangisi kararlı?

**S4 — bölüm/meta veri.** `StreamMapping.cs:85` `-map_chapters 0` ve `-map_metadata 0`
yazıyor. Kesitte bölüm işaretleri kaynağın zaman eksenine ait; kayıyor. Kesit varken
bölümleri düşürelim mi, kaydıralım mı, olduğu gibi bırakalım mı?

**S5 — OvershootTrim çakışması.** Kullanıcı 10-40 sn aralığını istedi, çıktı hedefi
aştı; `OvershootTrim` çıktıyı daha da kısaltabilir. Kullanıcının istediği aralık
**dokunulmaz** mı olmalı (o zaman aşma başka yolla kapanır), yoksa kısaltma serbest mi?

**S6 — testte pimlenecek kapı.** Mutasyona dayanıklı test için hangi sayıyı pimleyelim:
üretilen argüman dizisinde `-ss`/`-t`'nin **girdiye göre konumu** ve değeri mi, çıktı
süresinin sapma toleransı mı, bit hızının kesit süresinden türediğini gösteren bir
eşitlik mi? Bellekte "sabit karşılaştıran test davranış ölçmez" tuzağı var: `Assert.Equal`
ile iki sabiti karşılaştıran test mutasyonu yakalamıyor. Hangi eşitlik mutasyonu kırar?

Kısa ve hükümlü cevap ver; her soruya tek karar + tek cümle gerekçe. Emin olmadığın yerde
"ölçülmeli" de ve hangi ölçümü söyle.

## Gelen cevap (birebir)

# Danışma Cevabı: Küçültmede Aralık (Madde 54)

## S1 — `-ss` nereye?

**Karar: Melez.** `-ss` girdiden önce, istenen başlangıcın en fazla üretim `-g` tavanı kadar (10 sn, ölçüm 4) gerisine yuvarlanmış bir noktaya; kalan fark girdiden sonra ikinci bir `-ss` ile alınır.

Gerekçe: Ölçüm 3 ve 6 girdi-sonrası tek `-ss`'i deneme sayısıyla çarpılan bir maliyet yapıyor, ölçüm 1 ise girdi-öncesi tek `-ss`'in kare hatasını sıfırlamıyor; melez en fazla 10 sn'lik çözme bedeliyle HandBrake'in kare hassasiyetini verir. Geri gitme payı ölçülmeli: kaynakta gerçek anahtar kare aralığı bilinmiyor (ölçüm 4), `ffprobe -skip_frame nokey` ile üç tipik kaynakta en büyük I-kare aralığı sayılıp pay o değerin üstüne oturtulur.

## S2 — bütçe hangi süreden?

**Karar: Hedef MB kesit çıktısına uygulanır; bit hızı kesit süresinden hesaplanır; kaynak MB sezgileri süre oranıyla ölçeklenir, gerçek kesit baytı okunmaz.**

Gerekçe: Ek `ffprobe` paket sayımı (ölçüm 6) her deneme turuna değil bir kez düşse de, kesit baytı bit/piksel tabanını yalnız değişken bit hızlı kaynakta anlamlı değiştirir ve o sapmayı zaten ölçümlü deneme turu kapatır; süre oranı sıfır maliyetli, hata deneme döngüsünün kapsamında.

## S3 — `-t` mi `-to` mu?

**Karar: `-t` (süre), `-to` değil.**

Gerekçe: `-ss` girdiden önceyken ffmpeg zaman damgalarını sıfırlar ve `-to`'nun girdi mi çıktı eksenine mi bağlandığı sürüme göre değişmiştir; `-t = End − Start` tek yorumludur ve melezde ikinci `-ss` girdiden sonra olsa da çıktı süresi olarak kararlı kalır.

## S4 — bölüm/meta veri

**Karar: Kesit varken `-map_chapters -1`; `-map_metadata 0` kalır.**

Gerekçe: Bölümleri kaydırmak `ffmetadata` yazıp yeniden okumak demek, kaynağın kesit dışındaki işaretlerini de budamak gerekir; HandBrake de aralık kesince bölüm listesini yeniden üretir, biz üretmeyeceksek yanlış zamanlı işaret bırakmaktansa düşürmek doğru olan.

## S5 — OvershootTrim çakışması

**Karar: Kullanıcının aralığı dokunulmaz; aralık planında `OvershootTrim` devre dışı, aşma bir sonraki deneme turunun bit hızını düşürerek kapanır.**

Gerekçe: `OvershootTrim` "hiç ayar verilmemiş tüm video" için sessiz bir son çare; kullanıcı zaten 10-40 dediğinde saniyeleri kırpmak onun kararının üstüne yazmaktır ve ölçüm 5'teki "sessizce farklı parça" hatasının ürün yüzüne çıkmasıdır.

## S6 — testte pimlenecek kapı

**Karar: Üç ayrı test, üçü de sabit değil ilişki pimler:**

1. Argüman dizisinde ilk `-ss` indeksi `<` `-i` indeksi `<` ikinci `-ss` indeksi, ve `ilk_ss + ikinci_ss == istenen_Start` (girdi olarak rastgele Start; toplam eşitliği yuvarlama mutasyonunu, sıra eşitsizliği konum mutasyonunu kırar).
2. `-t` değeri `== End − Start` (End ve Start ayrı ayrı değişen iki örnekle; `-to`'ya dönen mutasyon ikinci örnekte kırılır).
3. Aynı hedef MB, aynı kaynak, iki farklı kesit süresi (T ve 2T) için `plan(T).Bitrate ≈ 2 × plan(2T).Bitrate` (oransal eşitlik; kaynak süresini kullanan mutasyon oranı 1'e çeker ve kırılır).

Gerekçe: Her üçü de iki bağımsız girdi arasında bir **ilişki** kurar; sabit-sabit karşılaştırma yerine girdiyi değiştirince çıktının nasıl değişmesi gerektiğini söyler, dolayısıyla "sabit karşılaştıran test davranış ölçmez" tuzağına düşmez. Çıktı süresi toleransı pimlenmez: ffmpeg'e bağlı, CI'da ffmpeg olmayabilir (bellek: `--no-build eski ikiliyi koşturuyor`).

---

**Ölçülmeli işaretleri:** S1'deki geri gitme payı (kaynak I-kare aralığı, üç kaynak); S2'de süre oranı sezgisinin VBR kaynakta kaç deneme turu fazladan yediği (Bench ile 1 sabit / 1 VBR kaynak, aralıklı vs tam).
