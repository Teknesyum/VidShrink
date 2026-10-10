# K7 Planı: Düzenleyicide Çok Kaynak Ve Birleştirme

## Kapsam

Düzenleyicinin çizelgesine birden çok kaynak dosya art arda eklenir ve tek çıktıda birleştirilir.
Geçiş efekti, çok izli çizelge ve ses karıştırma bu işin dışındadır.

## Model

- `EditClip.Source`: parçanın geldiği kaynağın sırası. İlk kaynak 0, eklenenler 1, 2…
- `EditTimeline`: ek kaynakların süreleri (`ExtraSources`) yalnız büyüyen bir listedir.
  `AddSource(süre)` kaynağı kaydeder ve tamamını çizelgenin sonuna tek parça olarak koyar; parçanın
  eklenmesi tek geri alma adımıdır, kaynak listede kalır.
- Kaynak zamanıyla çalışan işlemler (`ToTimeline`, `RemoveSource`, `SplitSource`, `SourceToTimeline`)
  kaynak sırası alır; varsayılan 0'dır. Kenar kırpma sınırı parçanın kendi kaynağının süresidir ve
  komşu ancak aynı kaynaktansa sınır olur.

## Önizleme

`EdlPreview` yol listesi alır; her EDL satırı parçanın kendi kaynağının yolunu yazar.

## Teslim

- Parçaların hepsi tek kaynaktansa bugünkü tek kaynaklı yol aynen koşar (Hızlı, Akıllı, Tam).
- Birden çok kaynak varsa kip ne seçilirse seçilsin tek adımda yeniden kodlanır: her parça ilk
  kaynağın görünen boyutuna sığdırılır (oran korunur, boşluk doldurulur), onun kare hızına ve
  48 kHz stereo sese getirilir. Sesi olmayan kaynağın parçası sessizlikle doldurulur.
  Plan `MergeForcedFull` taşır, arayüz bunu tek satırla söyler.
- Parçaları ayrı yazan teslimde her parça tek kaynaklıdır; kendi kaynağının bilgisiyle planlanır.

## Proje Dosyası

- Tek kaynaklı proje sürüm 1 olarak, eskisiyle aynı baytlarla yazılır.
- Ek kaynak varsa sürüm 2: `sources` dizisi (yol, boy, zaman, süre) ve parça başına `source`.
- Eski (sürüm 1) dosya aynen açılır. Ek kaynaklardan biri yoksa ya da değişmişse proje bayat sayılır.

## Arayüz

- Araç çubuğunda ve çizelgenin sağ tık menüsünde "Kaynak ekle".
- Sürükle-bırak: çizelge açıksa bırakılan dosyalar sona eklenir; kapalıysa ilki açılır, kalanı eklenir.
- Her kaynağın ses dalgası, anahtar kare çentiği ve küçük resmi ayrı taranır; pencere kapanınca
  hepsinin taraması iptal edilir.

## Bilinen Sınırlar

- Sessizlik ve sahne taraması yalnız ilk kaynağı tarar; ek kaynakların parçalarına dokunmaz.
- Klip ayarlarının (kırpma) önizlemesi ilk kaynağın boyutunu ölçü alır.

## Ölçü

`DuzenleyiciCokKaynakTests`: model ve geri alma, EDL, birleştirme argümanları, sürüm 1 dosyanın
açılması, sürüm 2 gidiş dönüşü, kapanışta bütün taramaların iptali, gerçek ffmpeg ile kısa birleştirme.
