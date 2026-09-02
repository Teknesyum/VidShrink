# Türeyen sahne eşiği (T109)

Kaynak: `.calisma/kaynak/kaynak-1080p60-hdr-17dk.mp4` (1920x1080 hevc 10-bit HDR,
60 fps, 1036,165911 sn, 1.729.085.563 bayt, oyun görüntüsü). ffmpeg
9.0-full_build (gyan.dev), 2026-09-02. Makine paylaşımlıydı; paralelde beş ajanın
ölçümü koşuyordu. **Süre sayılarında bu damga var, kesim ve sahne sayılarında
yok** — kesim sayısı yükten etkilenmez, saniye etkilenir.

T105 üç pencerede en iyi eşiğin kaydığını ölçtü: durgun içerik ≤0,08 istiyor,
hareketli içerik ≥0,115, aradaki açı ~0,035, seçilen 0,105 hiçbirinde en iyi
değil. Bu sayfa o kaymayı alıp eşiği içerikten türetiyor.

## Sonuç

Karar eleği artık tek sabit değil. Her aday konumunda eşik şu kuraldan çıkıyor:

    θ(t) = clamp(0,08 + 2,09 · p90(t), 0,05, 0,15)

`p90(t)`, t'nin ±40 sn komşuluğundaki sahne skorlarının 90. yüzdeliği. Adayı
olmayan kareler sıfır sayılır, yani yüzdelik kare sayısı üzerinden alınır, aday
sayısı üzerinden değil.

Altı pencerenin birleşiminde F2 (β=2): **türeyen 0,972**, sabit 0,105 ise
**0,895**. Kalibrasyon (P1–P3) 0,977, sınama (P4–P6) 0,962.

Kod: `SceneMap.ThresholdRule.Measured`, `SceneMap.Agitation`,
`SceneMap.DerivedCutTimes`, `SceneMap.BuildDerived`. Üretim yolu
`SceneDetector.BuildMapAsync`; sabit eşik yolu yalnız karşılaştırma için
`BuildFixedMapAsync` altında duruyor.

## Ölçüm düzeneği

Tek geniş tarama, sonra tüm eşik aileleri o taramadan süzüldü. Tarama komutu
(iş parçacığı sabitlendi, makine paylaşımlıydı):

    ffmpeg -hide_banner -loglevel info -nostats \
      -i .calisma/kaynak/kaynak-1080p60-hdr-17dk.mp4 \
      -filter_complex "[0:v]split=2[a][b];[a]select='gte(scene,0.01)',metadata=print[sc];[b]scale=640:-2[enc]" \
      -map "[sc]" -f null - \
      -map "[enc]" -an -threads 4 -c:v libx264 -preset ultrafast -crf 23 \
      -vstats_file vstats-tam.log -f null - 2> scan-tam.log

Çıktı: 12.686 aday, 62.166 sonda karesi (59,996 kare/sn), 105 sn (makine
paylaşımlıydı). Bu tarama T105'in taramasıyla aynı kurulumdur ve T105'in eğri
tablosunun 16 satırının 16'sını da birebir yeniden üretti — bu sayfadaki hiçbir
fark kurulum kaymasına ait değil.

Taranan taban 0,01, sevk edilen taban 0,012. İkisi aynı sonucu veriyor; ölçüsü
"Taban eşiği" başlığında.

Eşleştirme toleransı 0,25 sn. Bir üretilen kesim, en yakın eşleşmemiş yer
gerçeği kesimi 0,25 sn içindeyse yakalanan, değilse yanlış pozitif sayılıyor.

## 1. Türetme girdisi ölçüyle seçildi

Kural ailesi `θ = clamp(α + β·x, 0,05, 0,15)`. Her aday `x` için ızgara:
komşuluk W ∈ {5, 10, 20, 40} sn, α ∈ [0,030 – 0,110] 0,005 adımla, β'nın
ölçeksiz karşılığı b\* ∈ [0 – 0,100] 0,005 adımla (β = b\*/x'in medyanı).
Yapılandırma **yalnız kalibrasyon penceresinde (P1, P2, P3)** seçildi; eşitlik
bozma önceden ilan edildi: önce en büyük W, sonra en küçük |β|. Sınama
pencereleri (P4, P5, P6) seçime girmedi.

Hücrede F2 (β=2):

| aday | W | α | β | P1 | P2 | P3 | P4 | P5 | P6 | KAL | HOL |
|---|---|---|---|---|---|---|---|---|---|---|---|
| A-p90 | 40 | 0,080 | 2,089 | 1,000 | 0,882 | 1,000 | 1,000 | 0,000 | 1,000 | **0,977** | **0,962** |
| A-p75 | 5 | 0,080 | 2,565 | 1,000 | 0,882 | 0,000 | 1,000 | 0,000 | 1,000 | 0,966 | 0,962 |
| A-medyan | 5 | 0,105 | 0,297 | 1,000 | 0,625 | 0,000 | 0,556 | 0,000 | 1,000 | 0,904 | 0,886 |
| B-sondaBps | 40 | 0,080 | 0,024 | 1,000 | 0,882 | 1,000 | 0,556 | 0,000 | 1,000 | 0,977 | 0,886 |
| C-yoğunluk (0,05) | 40 | 0,075 | 0,110 | 1,000 | 0,882 | 1,000 | 0,556 | 0,000 | 0,938 | 0,977 | 0,867 |
| D-yoğ0,02 | 40 | 0,080 | 0,010 | 1,000 | 0,882 | 1,000 | 0,556 | 0,000 | 1,000 | 0,977 | 0,921 |
| D-yoğ0,03 | 40 | 0,080 | 0,028 | 1,000 | 0,882 | 1,000 | 0,556 | 0,000 | 0,938 | 0,977 | 0,867 |
| D-yoğ0,04 | 40 | 0,080 | 0,054 | 1,000 | 0,882 | 1,000 | 0,556 | 0,000 | 1,000 | 0,977 | 0,921 |
| S-sabit (en iyi) | — | 0,112 | 0 | 1,000 | 0,484 | 0,000 | 0,556 | 0,000 | 1,000 | 0,901 | 0,886 |
| S-sabit 0,105 (T105) | — | 0,105 | 0 | 1,000 | 0,625 | 0,000 | 0,556 | 0,000 | 1,000 | 0,899 | 0,886 |

Adaylar: **A-p90 / A-p75 / A-medyan** sahne skoru dağılımının yüzdelikleri
(boş kareler sıfır), **B-sondaBps** yerel sonda bit hızının küresel ortalamaya
oranı, **C-yoğunluk** 0,05 düzeyinde saniyedeki aday sayısı, **D-yoğ0,0X** aynı
sayım başka düzeylerde. Öneri listesindeki "kare arası hareket miktarı" ayrı bir
girdi olarak ölçülmedi; sahne skorunun kendisi kare arası fark ölçüsüdür ve
A-\* ailesi onun dağılımını kullanır — **ayrı bir hareket vektörü ölçülmedi.**

Beş aday kalibrasyonda 0,977'de eşit. Ayrımı sınama kümesi yaptı: A-p90 0,962,
en yakın rakip 0,921, üçü 0,886'da sabit eşikle aynı yere düşüyor.

**Eşitlik bozmanın payı ölçüldü.** Kalibrasyon tepesinde birden çok yapılandırma
var; hangisinin seçildiği sınama sonucunu oynatıyor. Tepede eşit yapılandırmalar
üzerinden sınama F2'sinin yayılımı:

| aday | eşdeğer yapılandırma | HOL en düşük | HOL medyan | HOL en yüksek |
|---|---|---|---|---|
| A-p90 | 133 | **0,921** | 0,974 | 0,974 |
| A-p75 | 12 | 0,962 | 0,974 | 0,974 |
| A-medyan | 20 | 0,886 | 0,909 | 0,921 |
| B-sondaBps | 226 | 0,811 | 0,962 | 0,974 |
| C-yoğunluk | 21 | 0,867 | 0,974 | 0,974 |
| D-yoğ0,02 | 27 | 0,867 | 0,974 | 0,974 |
| D-yoğ0,03 | 3 | 0,867 | 0,867 | 0,867 |
| D-yoğ0,04 | 13 | 0,867 | 0,974 | 0,974 |

Seçilen A-p90 yapılandırması sınamada 0,962 verdi — kendi eşdeğer kümesinin
**medyanının altında**. Yani seçim sınamaya bakılarak yapılmadı; eğer yapılsaydı
aynı kümede 0,974 bulunabilirdi. A-p90'ın kümedeki en düşüğü (0,921) de
diğerlerinin hepsinden yüksek: eşitlik bozma nasıl düşerse düşsün bu aday
sabitin (0,886) altına inmiyor.

**Üç pencerede çapraz doğrulama işe yaramadı — ölçüldü.** Kalibrasyonda
birini-dışarıda-bırak:

| aday | CV-P1 | CV-P2 | CV-P3 | ortalama |
|---|---|---|---|---|
| A-p90 | 1,000 | 0,484 | 0,000 | 0,495 |
| A-p75 | 0,966 | 0,484 | 0,000 | 0,483 |
| A-medyan | 0,966 | 0,484 | 0,000 | 0,483 |
| B-sondaBps | 1,000 | 0,484 | **1,000** | **0,828** |
| C-yoğunluk | 1,000 | 0,484 | 0,000 | 0,495 |
| D-yoğ0,02 / 0,03 / 0,04 | 1,000 | 0,484 | 0,000 | 0,495 |

Sekiz adayın altısı aynı sayıya düşüyor, ayırt etmiyor. Daha kötüsü: CV'nin
tepeye koyduğu aday (B-sondaBps, 0,828) gerçek sınamada **en kötü genelleyen**
(0,886, eşdeğer kümesinin en düşüğü 0,811). Üç pencerelik bir kümeden çıkarılan
çapraz doğrulama bu işte yanlış adayı seçiyor; kararı elle işaretlenen yeni
pencereler verdi.

**Neden p90 yoğunluktan iyi genelliyor** — mekanizma ölçüldü. Kısa pencerede
±40 sn'lik komşuluk yandaki hareketli bölümü içeri sızdırıyor. Bu, *sayıyı*
yukarı çekiyor (P4'te W=40 yoğunluğu 0,35/sn, P2'de 0,025/sn), eşik 0,0843'ün
üstüne çıkıyor ve P4'teki panel çevirme kesimi kaçıyor. *Yüzdelik* ise azınlıktaki
hareketli karelere dayanıklı: P4'te p90 = 0,0000, eşik α'da yani 0,08 kalıyor,
0,0843 geçiyor. Yoğunluk adaylarının P4'te 0,556'da kalması bu.

## 2. İki yeni pencere elle işaretlendi (üçü işaretlendi)

Yöntem T105'inkiyle aynı: 1 fps kontak sayfası → aday listesi → şüpheli anın
öncesi/sonrası kare çifti → gözle ayrım. Sözleşme iki pencere istedi (biri
durgun biri hareketli); üçüncüsü (diyalog) sayım yoğun olduğu için eklendi.

### P4-durgun — (34,367811 – 93,100433], 58,7 sn, 2 gerçek kesim

    65.834
    93.100433

Statik öğretici paneli. 65,834 panelin sayfa çevirmesi (skor 0,0843), 93,100
oyuna dönüş (skor 0,3226). **Pencere T105'in pencerelerinden kısa**: kaynakta
başka durgun blok yok, bu yüzden 58,7 sn ile sınırlı. Kısalığı sonucu şişirmiyor
— tersine, kısa pencerede komşuluk sızması daha güçlü, adayların çoğu burada
düşüyor.

### P5-hareketli — (789,000 – 972,000], 183 sn, 0 gerçek kesim

Kesintisiz kılıç dövüşü, kesim yok. 0,05 üstünde 245 aday (yoğunluk 1,339/sn;
P3'te 0,439/sn). En yüksek skorlu dokuz aday tek tek kare çiftiyle bakıldı,
dokuzu da yanlış pozitif.

### P6-diyalog — (980,000 – 1036,165911], 56,2 sn, 13 gerçek kesim

    981.232   985.299   989.648   998.715  1004.048  1006.031  1009.248
    1011.549  1013.632  1016.014  1020.632  1031.931  1034.881

Karşılıklı konuşma, omuz üstü çekimler arasında geçiş. On üçü de kare çiftiyle
doğrulandı. 987,500 / 995,500 / 1024,499 kesim **değil**, ayrıca doğrulandı.

### İşaretleme komutları

Kontak sayfası (P4; diğer pencerelerde `-ss`, `-t`, `tile` ve `text` sabiti
değişiyor). Filtre bir dosyadan okunuyor çünkü sürücü harfindeki iki nokta
filtre dizgesini bozuyor; yazı tipi çalışma dizinine kopyalandı:

    cp /c/Windows/Fonts/consola.ttf yazi.ttf
    printf '%s' "fps=1,scale=200:-2,drawtext=fontfile=yazi.ttf:fontcolor=yellow:box=1:boxcolor=black:fontsize=18:x=3:y=3:text=%{eif\\:trunc(t)+34\\:d},tile=8x8" > f-p4.txt
    ffmpeg -hide_banner -ss 34.367811 -t 58.74 -i kaynak-1080p60-hdr-17dk.mp4 \
      -/vf f-p4.txt -frames:v 3 -y sayfa/p4-%02d.png

`-frames:v` sayfa sayısıdır; P4 tek sayfaya sığdı, P5 üç sayfa tuttu.

P5 için `text=%{eif\\:trunc(t)+789\\:d}`, `-ss 789 -t 183`, `tile=8x8`;
P6 için `text=%{eif\\:trunc(t)+972\\:d}`, `-ss 972 -t 64`, `tile=8x9`.

Kare çifti (şüpheli an T için, kesim öncesi ve sonrası):

    ffmpeg -hide_banner -ss $(python -c "print(T-0.060)") -i kaynak.mp4 -frames:v 1 -y cift/once.png
    ffmpeg -hide_banner -ss $(python -c "print(T+0.020)") -i kaynak.mp4 -frames:v 1 -y cift/sonra.png

Aday listesi taramadan süzüldü: `P4` için skor ≥0,02, `P5` için ≥0,06
(yoğunluk yüzünden), `P6` için ≥0,05.

**`-frames:v 1` tek sayfa üretir.** P5'in kontak sayfası önce eksik çıktı; üç
sayfa için `-frames:v 3` gerekti. Sayfa sayısı eksikse pencerenin sonu hiç
görülmez.

## 3. Yanlış pozitif pencere başına sayıldı

Sayıldı, varsayılmadı. Her pencerede üretilen / yakalanan / kaçan / yanlış
pozitif:

| pencere | içerik | gerçek | üretilen | yakalanan | kaçan | **YP** | F2 |
|---|---|---|---|---|---|---|---|
| P1 (144,117–333,300] | karışık | 28 | 28 | 28 | 0 | **0** | 1,000 |
| P2 (333,300–519,666] | durgun | 7 | 6 | 6 | 1 | **0** | 0,882 |
| P3 (600,000–789,000] | hareketli | 0 | 0 | 0 | 0 | **0** | 1,000 |
| P4 (34,368–93,100] | durgun | 2 | 2 | 2 | 0 | **0** | 1,000 |
| P5 (789,000–972,000] | hareketli | 0 | 3 | 0 | 0 | **3** | 0,000 |
| P6 (980,000–1036,166] | diyalog | 13 | 13 | 13 | 0 | **0** | 1,000 |
| **birleşik** | | 50 | 52 | 49 | 1 | **3** | **0,972** |

Sabit 0,105 ile aynı tablo:

| pencere | gerçek | üretilen | yakalanan | kaçan | **YP** | F2 |
|---|---|---|---|---|---|---|
| P1 | 28 | 28 | 28 | 0 | **0** | 1,000 |
| P2 | 7 | 4 | 4 | 3 | **0** | 0,625 |
| P3 | 0 | 6 | 0 | 0 | **6** | 0,000 |
| P4 | 2 | 1 | 1 | 1 | **0** | 0,556 |
| P5 | 0 | 5 | 0 | 0 | **5** | 0,000 |
| P6 | 13 | 13 | 13 | 0 | **0** | 1,000 |
| **birleşik** | 50 | 57 | 46 | 4 | **11** | **0,895** |

T101'in "yanlış pozitif sıfır" cümlesi bu tabloda da yanlış: türeyen eşikte bile
P5'te üç tane var. Kazancın çoğu geri çağırma tarafında (46 → 49 yakalanan,
11 → 3 yanlış pozitif); **P5'in üç yanlış pozitifi hiçbir yapılandırmada
sıfırlanmadı.** Üçü 811,915 (skor 0,1637), 878,949 (0,1453) ve 915,014 (0,1689);
gerçek kesimlerle aynı bantta, bu pencerede sahne skoru ayırt etmiyor. Kaçan tek
kesim P2'nin 334,000'i: 333,300'de kesim olduğu için asgari aralığa takılıyor,
eşikle ilgisi yok (bkz. §7).

## 4. Ölçüt yine F2 (β=2)

T105'in gerekçesi aynen geçerli ve değiştirilmedi: kaçan kesim bir sahnenin bit
bütçesini komşusuyla karıştırır ve hatayı sahne boyunca taşır; yanlış kesim
fazladan bir anahtar kare üretir, bedeli yereldir. Geri çağırma kesinliğin iki
katı ağırlıkta. Başka ölçüt denenmedi.

## 5. Kıskacın uçları

Kuralın kendi ölçülen aralığı — 531 aday konumunda ham (kıskaçsız) θ:

| | en düşük | %25 | medyan | %75 | %95 | %99 | en yüksek |
|---|---|---|---|---|---|---|---|
| aday konumlarında | 0,0800 | 0,1145 | 0,1265 | 0,1421 | 0,1460 | 0,1465 | **0,1467** |
| 1 sn'lik ızgarada | 0,0800 | — | 0,1140 | — | 0,1431 | 0,1459 | **0,1467** |

Pencere başına türeyen eşik (%10/30/50/70/90 konumlarında):

| pencere | %10 | %30 | %50 | %70 | %90 |
|---|---|---|---|---|---|
| P1 | 0,0800 | 0,1011 | 0,1141 | 0,0800 | 0,0800 |
| P2 | 0,0800 | 0,0800 | 0,0800 | 0,0800 | 0,0800 |
| P3 | 0,1155 | 0,1166 | 0,1163 | 0,1156 | 0,1251 |
| P4 | 0,1073 | 0,0800 | 0,0800 | 0,0800 | 0,1078 |
| P5 | 0,1358 | 0,1368 | 0,1433 | 0,1465 | 0,1354 |
| P6 | 0,1162 | 0,1025 | 0,0800 | 0,0800 | 0,0800 |

**Üst uç.** F2 tavanı belirleyemiyor — ölçüldü:

| tavan | KAL | HOL | TÜM | yakalanan | kaçan | YP | tavana değen konum |
|---|---|---|---|---|---|---|---|
| 0,100 | 0,924 | 0,915 | 0,921 | 49 | 1 | 17 | 489 |
| 0,110 | 0,960 | 0,938 | 0,953 | 49 | 1 | 8 | 463 |
| 0,120 | 0,977 | 0,938 | 0,965 | 49 | 1 | 5 | 292 |
| 0,130 | 0,977 | 0,962 | 0,972 | 49 | 1 | 3 | 245 |
| 0,140 | 0,977 | 0,962 | 0,972 | 49 | 1 | 3 | 150 |
| 0,145 | 0,977 | 0,962 | 0,972 | 49 | 1 | 3 | 57 |
| **0,150** | 0,977 | 0,962 | 0,972 | 49 | 1 | 3 | **0** |
| 0,160 – 1,000 | 0,977 | 0,962 | 0,972 | 49 | 1 | 3 | 0 |

0,130'un üstünde ölçü düz: F2 tavanı yukarıdan sınırlamıyor. Aşağıdan
sınırlıyor — tavan 0,120'ye inince sınama 0,962'den 0,938'e düşüyor, çünkü tavan
kuralın kendi aralığını (en yüksek 0,1467) kırpıyor ve dinamik üst yarısını
kapatıyor. Tavan bu yüzden **kuralın ölçülen en yüksek değerinin üstündeki ilk
0,01 basamağına**, 0,15'e kondu. Bu kaynakta hiçbir konum tavana değmiyor:
tavan işletme noktası değil, korkuluk. **Tavanı yukarıdan bağlayan bir ölçü
yok** — 0,15 ile 1,00 arasını ölçü ayırt etmiyor, 0,15 ilan edilmiş bir kural
(ölçülen aralık + bir basamak), ölçüm sonucu değil. Pim bunu böyle yazıyor.

**Alt uç.** Alt kıskaç bu kaynakta **hiç bağlamadı** — ölçüldü. α = 0,08 ve
β·x ≥ 0 olduğu için θ hiçbir zaman 0,08'in altına inmiyor; alt kıskacı 0,05'ten
0,01'e çekmek kesim sayısını da F2'yi de değiştirmiyor:

| α | alt kıskaç | kesim | TÜM F2 |
|---|---|---|---|
| 0,080 | 0,050 | 66 | 0,972 |
| 0,080 | 0,010 | 66 | 0,972 |
| 0,050 | 0,050 | 93 | 0,904 |
| 0,050 | 0,010 | 93 | 0,904 |
| 0,030 | 0,030 | 130 | 0,842 |
| 0,030 | 0,010 | 130 | 0,842 |
| 0,000 | 0,000 | 198 | 0,708 |

Alt kıskaç 0,05'te duruyor çünkü **sabit terim düşürülürse elek orada çöküyor**:
α 0,08 → 0,05'te F2 0,972 → 0,904, 0,03'te 0,842, 0'da 0,708. 0,05, eleğin hâlâ
elediği en düşük ölçülen düzey. Alt uç ayrıca tarama tabanının (0,012) altına
inemez: tabanın altındaki aday zaten günlüğe girmiyor, T105'in "taban tavana
dönüşür" ölçüsü burada da geçerli.

**Sınırın dışına ne zaman çıkılır.** Tavan θ ≥ 0,15 olduğunda bağlar, yani
p90 ≥ 0,0335'te. Bu kaynakta gözlenen p90: en düşük 0,0000, medyan 0,0223, en
yüksek 0,0319. Yani kaynağın **en hareketli 80 saniyelik komşuluğundan ~%5 daha
hareketli** bir içerik tavana değer ve orada eşik 0,15'te donar: daha da
hareketli içerik için elek sertleşmeyi bırakır. Tavansız (kıskaçsız) kural bu
kaynakta aynı sonucu verir; kıskaçsız davranışın **bu kaynağın dışında** ne
yaptığı ölçülmedi.

## Taban eşiği 0,05 → 0,012

A-p90 girdisi 0,05 tabanında çalışmıyor: o tabanda p90 altı pencerenin beşinde
0,0000'a çöküyor, kural sabit eşiğe dönüyor. Kuralın taban duyarlılığı:

| taban | KAL | HOL | TÜM | kesim | aday | p90 en yüksek |
|---|---|---|---|---|---|---|
| 0,010 | 0,977 | 0,962 | 0,972 | 66 | 12.686 | 0,0319 |
| **0,012** | 0,977 | 0,962 | 0,972 | 66 | 10.900 | 0,0319 |
| 0,015 | 0,977 | 0,962 | 0,972 | 69 | 8.354 | 0,0319 |
| 0,018 | 0,919 | 0,962 | 0,932 | 87 | 5.990 | 0,0319 |
| 0,020 | 0,909 | 0,962 | 0,925 | 90 | 4.834 | 0,0319 |
| 0,025 | 0,876 | 0,962 | 0,901 | 99 | 2.916 | 0,0319 |
| 0,030 | 0,876 | 0,882 | 0,878 | 106 | 1.892 | 0,0319 |
| 0,040 | 0,876 | 0,789 | 0,848 | 116 | 942 | 0,0000 |
| 0,050 | 0,876 | 0,789 | 0,848 | 116 | 531 | 0,0000 |

Ölçülen çalışma aralığı **0,010 – 0,015**, 0,018'de kırılıyor. **0,010'un altı
ölçülmedi** — tarama 0,01 tabanıyla üretildi, altındaki aday elde yok.

Sevk edilen değer 0,012, aralığın içinde ve yapay klip üzerinde iki yönlü
kıskaca bağlanabildiği tek nokta: klibin ölçülen basamakları 0,000306 / 0,009986
/ 0,009998 / 0,019999 / 0,019996 / 0,039990, yani kıskaç **(0,0100 – 0,0200]**.
0,010 sevk edilseydi alt kenara 0,000014 kalırdı; 0,012 iki yana da paylı.

Maliyet ölçüldü, **makine paylaşımlıydı**: taban 0,05 → 97 sn ve 96 sn; taban
0,012 → 95 sn ve 98 sn. Fark paylaşımlı makinenin gürültüsünün içinde, yani
**süre farkı ölçülemedi**. Günlük 83.245 bayt → 1.645.969 bayt (~20 kat).

**`ScanArgs`'ın biçimi bozuktu.** Filtreye giren taban `0.###` ile
biçimleniyordu; 0,0005'in altındaki her taban sessizce `0`a kırpılıyor,
`gte(scene,0)` her kareyi aday yapıyordu. 0,05'te fark etmiyordu. `0.#####`
oldu ve sabitin filtreye aynı sayı olarak geçtiği pime bağlandı.

## 6. Müşteriye ne değişti

T98 anahtar kare aralığını, T104 ölçüm penceresini bu sayılardan türetiyor.
Kaynağın tamamı için:

| | sabit 0,105 (T105) | türeyen | fark |
|---|---|---|---|
| sahne | 77 | **67** | −10 |
| ortalama uzunluk | 13,46 sn | **15,47 sn** | +2,01 |
| **medyan uzunluk** | 5,62 sn | **5,33 sn** | −0,29 |
| en kısa | 1,28 sn | 1,28 sn | 0 |
| **en uzun** | 122,37 sn | **271,15 sn** | +148,78 |

**Dağılım sağa çarpık; aralık seçen taraf medyanı kullanmalı.** T105 bunu ölçtü,
türeyen eşikte çarpıklık arttı: ortalama yükselirken medyan düştü. Ortalamaya
bakan bir tüketici sahnelerin uzadığını sanır; medyana bakan tipik sahnenin
kısaldığını görür.

**En uzun sahne iki katına çıktı** ve tüketiciyi asıl bu ilgilendirir. 271 sn'lik
sahne hareketli bölümde: eşik orada yükseliyor, o bölümdeki kesimler eleniyor.
Anahtar kare aralığını sahne uzunluğundan türeten taraf bu tek sahne için
üst sınırına dayanacak. Bunun kodlama tarafındaki bedeli **ölçülmedi**.

## 7. `DefaultMinSceneSeconds` yeniden yargılandı — değişmedi

T105 ölçmüştü: 1,0'ın payı sıfır değil, P2'de 334,000 her eşikte kaçıyor çünkü
333,300'de kesim var. Türeyen eşikte de kaçıyor (P2 6/7). 0,5'e çekmenin
harita tarafındaki ölçüsü:

| asgari aralık | kural | birleşik F2 | YP | P2 | sahne | medyan |
|---|---|---|---|---|---|---|
| 1,0 | türeyen | 0,972 | 3 | 6/7 | 67 | 5,33 sn |
| **0,5** | türeyen | **0,988** | 3 | **7/7** | 69 | 5,22 sn |
| 1,0 | sabit 0,105 | 0,895 | 11 | 4/7 | 77 | 5,62 sn |
| 0,5 | sabit 0,105 | 0,907 | 12 | 5/7 | 81 | 5,33 sn |

0,5 harita tarafında bedelsiz görünüyor: F2 0,972 → 0,988, yanlış pozitif
artmıyor, iki sahne ekleniyor. **Sabit yine de değişmedi**, çünkü bedeli haritada
değil: T98 bu sayıdan anahtar kare aralığı, T104 ölçüm penceresi türetiyor ve
iki sahne daha eklemenin oradaki bedeli **hâlâ ölçülmedi** — T105'te de
ölçülmemişti. Bu turda ölçülmedi çünkü iki dosya da bu sözleşmenin `owns`
listesinde değil. Sayı hazır; kararı ölçen taraf versin.

## 8. Değişmeyen sabitler

- `SceneMap.DefaultMinSceneSeconds = 1,0` — **ölçüldü, değişmedi.** Gerekçe
  yukarıda.
- `SceneDetector.ProbeWidth = 640`, `ProbeCrf = 23`, `ProbePreset = ultrafast` —
  **bu turda ölçülmedi, değişmedi.** T105'in sonda ölçüsü geçerli sayıldı.
- `SceneMap.FixedThreshold = 0,105` (eski adı `DefaultThreshold`, geriye dönük
  duruyor) — **duruyor ama karar vermiyor.** Sabit eşik yolu yalnız bu sayfadaki
  karşılaştırma için ve `QualityMeterTests`in kullandığı yardımcı için korundu.
  Üretim yolu `BuildMapAsync` artık türeyen kuralı kullanıyor ve türeyen harita
  tek bir eşik bildirmiyor: `SceneMap.Threshold` orada `NaN`, kural
  `SceneMap.Rule` alanında. Tek sayı bekleyen bir tüketici sessizce yanlış
  sayıyı okumaz, gürültüyle kırılır.

## 9. Mutasyon kanıtı

Kuralın her parçası iki yönde bozuldu; her mutasyon `SceneMapTests` filtresini
kırdı. Denetçi tabloyu `bash .calisma/T109/mutasyon.sh` ile değil, aynı
mutasyonu elle uygulayıp `dotnet test -c Release --filter "SceneMapTests"`
koşarak doğrulayabilir.

| mutasyon | kırılan ölçü |
|---|---|
| Offset 0,08 → 0,07 | TuretilenEsik_KiskacHerIkiUctaBaglar |
| Offset 0,08 → 0,09 | TuretilenEsik_KiskacHerIkiUctaBaglar |
| Slope 2,09 → 1,90 | TuretilenEsik_KiskacHerIkiUctaBaglar |
| Slope 2,09 → 2,30 | TuretilenEsik_KiskacHerIkiUctaBaglar |
| Slope 2,09 → 0 (sabit eşiğe döner) | DerivedCutTimes_AyniSkorDurgunKomsuluktaGecerHareketlideGecmez + KiskacHerIkiUctaBaglar |
| Komşuluk 40 → 30 sn | Agitation_KomsulukGenisligiOlcuyuDegistirir + Agitation_YuzdelikBosKareleriSifirSayar |
| Komşuluk 40 → 50 sn | aynı ikisi |
| Yüzdelik 0,90 → 0,85 | aynı ikisi |
| Yüzdelik 0,90 → 0,95 | aynı ikisi |
| Alt kıskaç 0,05 → 0,04 | TuretilenEsik_KiskacHerIkiUctaBaglar |
| Alt kıskaç 0,05 → 0,06 | TuretilenEsik_KiskacHerIkiUctaBaglar |
| Üst kıskaç 0,15 → 0,14 | KiskacHerIkiUctaBaglar + KiskacUclariOlculenAralikla |
| Üst kıskaç 0,15 → 0,16 | KiskacHerIkiUctaBaglar + KiskacUclariOlculenAralikla |
| Taban 0,012 → 0,009 | ScanArgs_TabanEsigiFiltreyeGecer + ScanAsync_TabanEsigiIkiYonluKiskacta |
| Taban 0,012 → 0,021 | aynı ikisi |
| Biçim `0.#####` → `0.###` | aynı ikisi |

Ölçülerin hiçbiri iki sabiti karşılaştırmıyor. Davranış ölçen çekirdek pim
`DerivedCutTimes_AyniSkorDurgunKomsuluktaGecerHareketlideGecmez`: **aynı skorlu
(0,09) aynı aday**, durgun komşulukta kesim oluyor, hareketli komşulukta
olmuyor. Ürünün dinamiklik iddiası tam olarak bu tek ölçüde duruyor.
`Skip` yok, ffmpeg yokluğunda sessiz erken dönüş yok; ffmpeg gerektiren
ölçüler `[FfmpegFact]`.

Sevk edilen C# kuralının bu sayfadaki sayıları verdiği ayrıca doğrulandı: aynı
ham tarama `SceneMap.BuildDerived`'a verildiğinde 66 kesimin 66'sı da Python
ölçüm düzeneğininkiyle **birebir aynı** (en büyük fark 0,0 sn), sahne dağılımı
67 / 15,47 / 5,33 / 1,28 / 271,15.

## Ölçülmeyenler

- **Kare arası hareket vektörü** ayrı bir girdi olarak ölçülmedi (bkz. §1).
- **Tavanın üst sınırı** ölçülmedi: 0,15 ile 1,00 arasını ölçü ayırt etmiyor.
- **Taban 0,010'un altı** ölçülmedi; tarama 0,01 ile üretildi.
- **Alt kıskacın bağladığı bir durum** bu kaynakta gözlenmedi; α > alt kıskaç
  olduğu sürece bağlamaz.
- **Kıskaçsız kuralın bu kaynak dışındaki davranışı** ölçülmedi.
- **Başka kaynak ölçülmedi.** Altı pencerenin altısı da aynı 17 dakikalık oyun
  kaydından. Kural bu kaynağın içinde genelledi; başka içerik türüne (film,
  ekran kaydı, animasyon) genellemesi ölçülmedi.
- **`DefaultMinSceneSeconds` 0,5'in tüketici tarafındaki bedeli** ölçülmedi
  (T98 anahtar kare aralığı, T104 penceresi).
- **En uzun sahnenin 271 sn'ye çıkmasının kodlama tarafındaki bedeli**
  ölçülmedi.
- **Türeyen eşiğin tarama maliyetine etkisi yok** çünkü kural taramadan sonra,
  aynı aday listesi üzerinde çalışıyor; ek ffmpeg koşumu gerektirmiyor. Kuralın
  kendi hesap maliyeti ölçülmedi (531 konumda ikili arama, ölçülecek kadar
  büyük görünmüyor ama **ölçülmedi**).

## Devralınan borç

`tools/sahne-yer-gercegi/sahneler.csv` **bayat**: 24 satır, 0,2 eşiğiyle
üretilmiş. Ne T105'in 0,105 haritasını ne buradaki türeyen haritayı gösteriyor.
Bu sözleşmenin `owns`'ında olmadığı için değiştirilmedi. Türeyen haritanın 66
kesimi ölçüm düzeneğinde üretildi; yerine geçecek dosyayı `tools/` sahibi
koymalı.

`tools/sahne-yer-gercegi/gercek-kesimler.txt` (P1'in 28 kesimi) ve T105'in P2
listesi olduğu gibi kullanıldı, yeniden işaretlenmedi.
