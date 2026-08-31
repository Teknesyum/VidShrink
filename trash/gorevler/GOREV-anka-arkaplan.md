# Görev paketi — VidShrink çalışma alanı arka planı

Bu paket Sole'e verilmek üzere yazıldı. Yürütücü model: **Fable**.

## Durum

VidShrink'in çalışma alanının arkasında vektörle çizilmiş bir anka duruyor. Üç tur
çalışıldı: dört düz levhadan 54 yola, katmanlı tüylere, dalgalı tüy kenarına, eşit
olmayan saçaklara, düzensiz kor parçacıklarına ve radyal ışımaya çıkıldı. Ölçüler
tutuyor, kullanıcı hâlâ beğenmiyor: figür "çizilmiş" duruyor, gövde tek parça bir
gradyan kütlesi gibi okuyor.

Vektörle fotoğraf dokusuna çıkılmıyor. Bu paket onu zorlamıyor; **ortamı değiştirmeyi**
istiyor.

## İstenen

Uygulamanın arkasında duran, panellerin ardından hafifçe görünen bir arka plan.
Anka kalabilir, kalmayabilir — karar senin. Ölçü şu: kullanıcı ona baktığında
"çizilmiş bir kuş" değil, **bir ortam** görsün.

## Kısıtlar

Bunlar tartışmaya kapalı; deponun lisansı ve arayüz disiplini bunları gerektiriyor.

1. **Dışarıdan görüntü gelmez.** Depo AGPL-3.0-or-later. Kaynağı ve lisansı belli
   olmayan hiçbir fotoğraf, render ya da model çıktısı içeri girmez. Üretilen ne ise
   depoya girecek biçimiyle üretilmeli.
2. **Avalonia XAML.** Çizim `src/VidShrink.App/Themes/Theme.axaml` içinde
   `DrawingGroup` olarak yaşıyor. Çıktı ya aynı yerde XAML olacak, ya da nasıl
   yükleneceği açıkça anlatılmış bir varlık olacak.
3. **Palet büyümez.** `Ember` ailesi dokuz belirteç. Yeni ton eklenmez; ihtiyaç
   duyulan her şey mevcut belirteçlerin opaklık ve rampa varyasyonlarından kurulur.
4. **Okunabilirlik.** En kötü hâlde gövde metni kontrastı 4,5:1 üstünde kalmalı.
   Bugünkü sayılar: panelsiz 8,37:1, panelin ardında 17,7:1.
5. **Tuval** 1600×1000, `viewBox` ile ölçekleniyor.
6. **Panellerin ardında kalır.** Arka plan dikkat çekmez; paneller kaldırıldığında
   görünür, panellerin üstüne çıkmaz.

## Teslim

1. Ne yaptığın ve neden o yolu seçtiğin — iki paragrafı geçmesin.
2. `Theme.axaml`'e girecek blok.
3. `.calisma/anka-onizleme.svg` ile aynı işi gören bir önizleme: XAML'deki yollar ve
   gradyan duraklarıyla birebir, tarayıcıda açılabilir. Sönüm durakları SVG'de
   `stop-color="#000000" stop-opacity="0"` yazılır — XAML `Transparent` saydam siyaha
   interpole ediyor, SVG'de `stop-opacity="0"` tek başına saydam **kırmızıya** interpole
   eder ve önizleme uygulamadakinden parlak çıkar.
4. Ölçülen iki kontrast sayısı.
