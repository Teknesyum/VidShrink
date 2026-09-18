# Kanıt Klasörü Kapanışı — Oynatıcı Öbeği

Dal: `t0/kanit-oynatici`. Kural: kanıt dosyasını silen `Kapat` çağrısı **son asertten sonra**
durur; yeşil koşum kendi bıraktığını siler, kırmızı koşum kanıtını korur çünkü düşen asert o
satıra hiç gelmez. Klasör boşalınca o da gider.

`Kapat(params string[] adlar)` sınıf başına kopyalanmadı; her kanıt yardımcısına bir kez
eklendi: `MotorKanit`, `ParcaKanit`, `GorunumKanit`, `SeritKanit`, `GelismisKanit`, `AracKanit`,
`KarsilastirmaKanit`, `GirdiKanit`, `DenetimKanit`, `YolKanit`, `KisayolKanit`. `OynaticiDalga3GirdiTests`'in
kanıt yazıcısı bir yardımcı sınıf değil, sınıfın kendi `Kanit(...)` metodu; `Kapat` oraya, onun yanına kondu.
`YolKanit` ve `KisayolKanit` gövdesinde tek fark var: ad bir klasörse (testin kendi geçici kökü)
ağacıyla birlikte gidiyor — bu iki öbek kanıtını alt klasörle birlikte bırakıyor.

`OynaticiKisayolTests` içindeki mevcut `Kapat()` ortamı kapatıyor (`KisayolOrtam` örnek metodu);
yeni yardımcı `KisayolKanit.Kapat(params string[])`, ayrı tür, çakışma yok.

## Komutlar

```
dotnet build VidShrink.sln -c Release -warnaserror -m:2
dotnet test tests/VidShrink.Tests/VidShrink.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~OynaticiMotorTests|FullyQualifiedName~OynaticiMotorAramaOlculeri|FullyQualifiedName~OynaticiParcaTests|FullyQualifiedName~OynaticiGorunumTests|FullyQualifiedName~OynaticiGelismisTests|FullyQualifiedName~OynaticiAracTests|FullyQualifiedName~OynaticiKarsilastirmaTests|FullyQualifiedName~OynaticiEskiYolTests|FullyQualifiedName~OynaticiGirdiTests.|FullyQualifiedName~KeymapTests|FullyQualifiedName~OynaticiDenetimMotorTests|FullyQualifiedName~OynaticiYolHaritasiTests|FullyQualifiedName~OynaticiKisayolTests|FullyQualifiedName~OynaticiDalga3GirdiTests"
```

Yerelde `VIDSHRINK_LIBMPV=C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\hiper\yeni\tools\libmpv`.

## (a) Yeşil koşum

```
C:\Users\Administrator\Desktop\Projeler\VidShrink\.claude\worktrees\agent-a785fb9951cbbfbeb\tests\VidShrink.Tests\bin\Release\net8.0\VidShrink.Tests.dll (.NETCoreApp,Version=v8.0) için test çalıştırması
VSTest sürümü 17.14.1 (x64)

Test yürütmesi başlatılıyor, lütfen bekleyin...
Toplam 1 test dosyası belirtilen desenle eşleşti.

Başarılı!  - Başarısız:     0, Başarılı:   159, Atlanan:     0, Toplam:   159, Süre: 4 m 49 s - VidShrink.Tests.dll (net8.0)
EXITCODE=0
```

### Koşumdan sonra klasörler

```
--- klasor: .calisma/oynatici-motor
oynatici-motor\h264_1080p60.mp4
oynatici-motor\h264_2160p30.mp4
oynatici-motor\hevc_1080p60.mp4
oynatici-motor\kucuk-320x180-25sn.mp4
oynatici-motor\serit-cikti-crf35.mp4
oynatici-motor\serit-kaynak-320x180-30.mp4
--- klasor: .calisma/dalga2
dalga2\gomulu.srt
dalga2\parca-2ses-1altyazi.mkv
--- klasor: .calisma/dalga3
dalga3\gecici
dalga3\bolumler.txt
dalga3\bolumlu-320x180.mp4
dalga3\gecici\ayar-0128ca8e
dalga3\gecici\bilgi-a638685e
dalga3\gecici\birak-129a2773
dalga3\gecici\bolum-037b6f5d
dalga3\gecici\goruntu-94e11765
dalga3\gecici\goruntu-ayar-25617c45
dalga3\gecici\klasor-00084395
dalga3\gecici\klasor-ayar-67887bc1
dalga3\gecici\otomatik-All-ecd816b5
dalga3\gecici\otomatik-Off-c61adbbf
dalga3\gecici\secici-ayar-05eda554
dalga3\gecici\secici-ayar-d3c734fc
dalga3\gecici\secici-hedef-380cefd7
dalga3\gecici\secici-hedef-566b71ed
dalga3\gecici\sira-61b5226d
dalga3\gecici\son-31dfe37e
dalga3\gecici\son-ayar-4bcd3c39
dalga3\gecici\ustte-428eaae0
dalga3\gecici\ayar-0128ca8e\bozuk.json
dalga3\gecici\ayar-0128ca8e\film_01-02-05-500.png
dalga3\gecici\ayar-0128ca8e\player-settings.json
dalga3\gecici\bilgi-a638685e\history.json
dalga3\gecici\bilgi-a638685e\player-recent.json
dalga3\gecici\birak-129a2773\birakilan.mp4
dalga3\gecici\bolum-037b6f5d\history.json
dalga3\gecici\bolum-037b6f5d\player-recent.json
dalga3\gecici\goruntu-94e11765\kucuk-320x180-25sn_00-00-03-166.png
dalga3\gecici\goruntu-94e11765\pencere-boyutu.png
dalga3\gecici\goruntu-ayar-25617c45\history.json
dalga3\gecici\goruntu-ayar-25617c45\player-recent.json
dalga3\gecici\klasor-00084395\klip 1.mp4
dalga3\gecici\klasor-00084395\klip 10.mp4
dalga3\gecici\klasor-00084395\klip 2.mp4
dalga3\gecici\klasor-00084395\notlar.txt
dalga3\gecici\klasor-ayar-67887bc1\history.json
dalga3\gecici\klasor-ayar-67887bc1\player-recent.json
dalga3\gecici\klasor-ayar-67887bc1\player-settings.json
dalga3\gecici\otomatik-All-ecd816b5\ayar
dalga3\gecici\otomatik-All-ecd816b5\a-ilk.mp4
dalga3\gecici\otomatik-All-ecd816b5\b-ikinci.mp4
dalga3\gecici\otomatik-All-ecd816b5\ayar\history.json
dalga3\gecici\otomatik-All-ecd816b5\ayar\player-recent.json
dalga3\gecici\otomatik-Off-c61adbbf\ayar
dalga3\gecici\otomatik-Off-c61adbbf\a-ilk.mp4
dalga3\gecici\otomatik-Off-c61adbbf\b-ikinci.mp4
dalga3\gecici\otomatik-Off-c61adbbf\ayar\history.json
dalga3\gecici\otomatik-Off-c61adbbf\ayar\player-recent.json
dalga3\gecici\secici-ayar-05eda554\history.json
dalga3\gecici\secici-ayar-05eda554\player-recent.json
dalga3\gecici\secici-ayar-05eda554\player-settings.json
dalga3\gecici\secici-ayar-d3c734fc\history.json
dalga3\gecici\secici-ayar-d3c734fc\player-recent.json
dalga3\gecici\secici-hedef-566b71ed\kucuk-320x180-25sn_00-00-01-500.png
dalga3\gecici\sira-61b5226d\a1.mp4
dalga3\gecici\sira-61b5226d\a10.mp4
dalga3\gecici\sira-61b5226d\a2.mp4
dalga3\gecici\sira-61b5226d\b.mkv
dalga3\gecici\sira-61b5226d\belge.txt
dalga3\gecici\sira-61b5226d\C.webm
dalga3\gecici\son-31dfe37e\player-recent.json
dalga3\gecici\son-ayar-4bcd3c39\history.json
dalga3\gecici\son-ayar-4bcd3c39\player-recent.json
dalga3\gecici\ustte-428eaae0\history.json
dalga3\gecici\ustte-428eaae0\player-recent.json
--- klasor: .calisma/dalga7b -> YOK
--- klasor: .calisma/dalga4a
dalga4a\gecici
dalga4a\gecici\kalicilik-3dc613b8
dalga4a\gecici\kalicilik-3dc613b8\player-advanced.json
dalga4a\gecici\kalicilik-3dc613b8\player-history.json
dalga4a\gecici\kalicilik-3dc613b8\player-recent.json
--- klasor: .calisma/dalga4b
dalga4b\gecici
dalga4b\gecici\adres-dba1e050
dalga4b\gecici\ayar-69e18e5d
dalga4b\gecici\klip-1a72af15
dalga4b\gecici\kucukresim-31441680
dalga4b\gecici\kucukresim-esik-933c38f0
dalga4b\gecici\serit-motor-78080aee
dalga4b\gecici\serit-surukleme-e13a25b4
dalga4b\gecici\adres-dba1e050\history.json
dalga4b\gecici\adres-dba1e050\player-tools.json
dalga4b\gecici\ayar-69e18e5d\bozuk.json
dalga4b\gecici\ayar-69e18e5d\player-tools.json
dalga4b\gecici\klip-1a72af15\klip.gif
dalga4b\gecici\klip-1a72af15\klip.mp4
dalga4b\gecici\kucukresim-31441680\history.json
dalga4b\gecici\kucukresim-31441680\player-recent.json
dalga4b\gecici\kucukresim-esik-933c38f0\history.json
dalga4b\gecici\kucukresim-esik-933c38f0\player-recent.json
dalga4b\gecici\serit-motor-78080aee\history.json
dalga4b\gecici\serit-motor-78080aee\player-recent.json
dalga4b\gecici\serit-surukleme-e13a25b4\history.json
dalga4b\gecici\serit-surukleme-e13a25b4\player-recent.json
--- klasor: .calisma/dalga5 -> YOK
--- klasor: .calisma/T176
T176\girdi-20sn.mkv
--- klasor: .calisma/dalga1
dalga1\player-recent.json
--- klasor: .calisma/oynatici-yol-haritasi
oynatici-yol-haritasi\p14
oynatici-yol-haritasi\p14-esik
oynatici-yol-haritasi\p14\player-history.json
oynatici-yol-haritasi\p14-esik\player-history.json
--- klasor: .calisma/oynatici-kisayol
oynatici-kisayol\gecmis
oynatici-kisayol\liste
oynatici-kisayol\tuslar
oynatici-kisayol\kirmizi-sol-mavi-sag-320x180.mp4
oynatici-kisayol\uzun.srt
oynatici-kisayol\gecmis\player-recent.json
oynatici-kisayol\gecmis\player-settings.json
oynatici-kisayol\liste\a-uzun-700sn.mkv
oynatici-kisayol\liste\b-iki-renk.mp4
--- klasor: .calisma/girdi-dalga3 -> YOK
--- klasor: .calisma/girdi -> YOK
```

Kanıt `.txt` dosyalarının tamamı gitti. `dalga7b`, `dalga5` ve `girdi-dalga3` klasörleri tümden
yok oldu. Geri kalan klasörler boşalmadı çünkü kanıt değil, **önbelleğe alınmış üretim** tutuyorlar:

- `.calisma/oynatici-motor` — `MotorKlipleri.Hazirla`'nın önbelleği (`h264_1080p60.mp4`,
  `h264_2160p30.mp4`, `hevc_1080p60.mp4`, `kucuk-320x180-25sn.mp4`) ve `SeritKlip`'in iki klibi.
  Silinseler her koşumda dakikalarca 2160p yeniden kodlama olur; üstelik bu klipleri başka sınıflar da tüketiyor.
- `.calisma/dalga2` — `ParcaKanit.Klip`'in ürettiği `parca-2ses-1altyazi.mkv` + `gomulu.srt`.
- `.calisma/dalga3`, `.calisma/dalga4a`, `.calisma/dalga4b` — `Gecici(...)` test başına geçici kök
  açıyor; bunlar kanıt değil, testin çalışma alanı.
- `.calisma/T176` — `girdi-20sn.mkv` önbellek klibi.
- `.calisma/oynatici-kisayol` — `IkiRenk`, `Uzun`, `Liste` önbellek klipleri.
- `.calisma/dalga1\player-recent.json`, `.calisma/oynatici-yol-haritasi\p14*\player-history.json`,
  `.calisma/oynatici-kisayol\gecmis\player-*.json` — görünümün kapanış sırasında yazdığı ayar/geçmiş
  dosyaları; testin son asertinden sonra doğdukları için `Kapat`'ın kapsamında değiller.

## (b) Kırmızı koşum (kasıtlı mutasyon)

Mutasyon: `OynaticiDenetimTests.cs`, `KeymapTests.KisayolTablosundaCakismaYokVeHerKomutErisilebilir`
son asert `Assert.Empty(izsiz);` → `Assert.NotEmpty(izsiz);`. Koşumdan sonra geri alındı.

```
C:\Users\Administrator\Desktop\Projeler\VidShrink\.claude\worktrees\agent-a785fb9951cbbfbeb\tests\VidShrink.Tests\bin\Release\net8.0\VidShrink.Tests.dll (.NETCoreApp,Version=v8.0) için test çalıştırması
VSTest sürümü 17.14.1 (x64)

Test yürütmesi başlatılıyor, lütfen bekleyin...
Toplam 1 test dosyası belirtilen desenle eşleşti.
  Başarısız VidShrink.Tests.OynaticiKisayolTests.KisayolMotoraUlasirVeGeriOkunur(ad: "CtrlShiftS") [5 s]
  Hata İletisi:
   [CtrlShiftS]
  once (320, 180, mavi, mavi, kirmizi, mavi) Rotation 0
  tus Control, Shift+S '' -> iz rotate -> 90
  Rotation 90 vf '@vsrotate:lavfi=graph=%15%transpose=clock' dwidth 180x320 kare (320, 180, kirmizi, mavi, r0b0, r0b0)
  tus Control, Shift+S '' -> iz rotate -> 180
  Rotation 180 vf '@vsrotate:lavfi=graph=%11%hflip,vflip' dwidth 320x180 kare (180, 320, r0b0, r0b0, mavi, kirmizi)
  tus Control, Shift+S '' -> iz rotate -> 270
  Rotation 270 vf '@vsrotate:lavfi=graph=%16%transpose=cclock' dwidth 180x320 kare (320, 180, mavi, kirmizi, r0b0, r0b0)
  tus Control, Shift+S '' -> iz rotate -> 0
  Rotation 0 vf '' dwidth 320x180 kare (320, 180, mavi, mavi, kirmizi, mavi)
  KALDI 90 derecede kare (320, 180, kirmizi, mavi, r0b0, r0b0), Rotation 90

  Yığın İzleme:
     at VidShrink.Tests.OynaticiKisayolTests.KisayolMotoraUlasirVeGeriOkunur(String ad) in C:\Users\Administrator\Desktop\Projeler\VidShrink\.claude\worktrees\agent-a785fb9951cbbfbeb\tests\VidShrink.Tests\OynaticiKisayolTests.cs:line 270
   at InvokeStub_OynaticiKisayolTests.KisayolMotoraUlasirVeGeriOkunur(Object, Span`1)
   at System.Reflection.MethodBaseInvoker.InvokeWithOneArg(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
[xUnit.net 00:02:06.65]     VidShrink.Tests.OynaticiKisayolTests.KisayolMotoraUlasirVeGeriOkunur(ad: "CtrlShiftS") [FAIL]
[xUnit.net 00:03:14.81]     VidShrink.Tests.KeymapTests.KisayolTablosundaCakismaYokVeHerKomutErisilebilir [FAIL]
  Başarısız VidShrink.Tests.KeymapTests.KisayolTablosundaCakismaYokVeHerKomutErisilebilir [5 ms]
  Hata İletisi:
   Assert.NotEmpty() Failure: Collection was empty
  Yığın İzleme:
     at VidShrink.Tests.KeymapTests.KisayolTablosundaCakismaYokVeHerKomutErisilebilir() in C:\Users\Administrator\Desktop\Projeler\VidShrink\.claude\worktrees\agent-a785fb9951cbbfbeb\tests\VidShrink.Tests\OynaticiDenetimTests.cs:line 341
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)

Başarısız! - Başarısız:     2, Başarılı:   157, Atlanan:     0, Toplam:   159, Süre: 3 m 56 s - VidShrink.Tests.dll (net8.0)
EXITCODE=1
```

### Koşumdan sonra klasörler

```
--- klasor: .calisma/dalga1
dalga1\keymap-cakisma.txt
dalga1\player-recent.json
--- klasor: .calisma/oynatici-kisayol
oynatici-kisayol\gecmis
oynatici-kisayol\liste
oynatici-kisayol\tuslar
oynatici-kisayol\kirmizi-sol-mavi-sag-320x180.mp4
oynatici-kisayol\uzun.srt
oynatici-kisayol\gecmis\CtrlShiftS.json
oynatici-kisayol\gecmis\player-recent.json
oynatici-kisayol\gecmis\player-settings.json
oynatici-kisayol\liste\a-uzun-700sn.mkv
oynatici-kisayol\liste\b-iki-renk.mp4
oynatici-kisayol\tuslar\CtrlShiftS.txt
```

Mutasyonlu testin kanıtı `dalga1\keymap-cakisma.txt` yerinde; aynı klasördeki diğer on iki testin
kanıtı silinmiş. Yani klasör, yalnız kırmızı testin bıraktığıyla ayakta.

Bu koşumda ikinci bir kırmızı daha çıktı: `OynaticiKisayolTests.KisayolMotoraUlasirVeGeriOkunur(ad: "CtrlShiftS")`
— döndürme karesinin pikselini okuyan, zamanlamaya bağlı **kararsız** bir ölçü. Yeşil koşumda geçti,
mutasyon geri alındıktan sonra tek başına koşturulunca yine geçti (69/69). Mutasyondan bağımsız;
kanıtı (`oynatici-kisayol\tuslar\CtrlShiftS.txt`) kural gereği yerinde kaldı — kuralın ikinci,
istenmeden gelen kanıtı.

```
dotnet test ... --filter "FullyQualifiedName~OynaticiKisayolTests"
Başarılı!  - Başarısız:     0, Başarılı:    69, Atlanan:     0, Toplam:    69, Süre: 48 s - VidShrink.Tests.dll (net8.0)
```

## Yeniden Ölçüm — İki Kolun Dökümü Aynı Genişlikte (18 Eylül 2026)

Yukarıdaki kırmızı koşumun klasör dökümü budanmıştı: yeşilde 13 başlık, kırmızıda 2. Plan
"ikisinin ham çıktısı birebir" dediği için iki kol da aynı listeyle, aynı betikle
(`tools/kanit-dokumu.sh`, 13 klasör, `find -maxdepth 2`) yeniden alındı.

Her kolun öncesinde uçucu klasörler silindi (`dalga1`, `dalga3`, `dalga4a`, `dalga4b`,
`oynatici-yol-haritasi`, `oynatici-kisayol/gecmis`); pahalı önbellek klipleri (2160p kodlama)
duruyor. İlk deneme bunu atladığı için iki koşumun artığı üst üste bindi — `dalga3` 181 satır
çıktı, belgedeki yeşilde 68'di. Ölçü o haliyle kıyas taşımaz, atıldı.

Mutasyon değişmedi: `OynaticiDenetimTests.cs:328`, `Assert.Empty(izsiz);` → `Assert.NotEmpty(izsiz);`.
Koşumdan sonra **elle** geri yazıldı (`git checkout` yok, `git diff` boş).

Motor yerelde `VIDSHRINK_LIBMPV=C:\Users\Administrator\Desktop\Projeler\VidShrink\tools\libmpv\libmpv-2.dll`
ile verildi; belgenin başındaki `.calisma\hiper\yeni\...` yolu bu makinede yok.

### Sonuçlar

```
kirmizi: Başarısız! - Başarısız: 1, Başarılı: 158, Atlanan: 0, Toplam: 159, Süre: 3 m 38 s
         [FAIL] VidShrink.Tests.KeymapTests.KisayolTablosundaCakismaYokVeHerKomutErisilebilir
         Assert.NotEmpty() Failure: Collection was empty
         EXITCODE=1

yesil:   Başarısız! - Başarısız: 1, Başarılı: 158, Atlanan: 0, Toplam: 159, Süre: 3 m 38 s
         [FAIL] VidShrink.Tests.OynaticiMotorTestsGirdi.OnHizliTikteAramalarYuzElliMilisaniyeSinirindaKalir
         150 ms sinirini asan arama: 204.8
         EXITCODE=1
```

Yeşil kolun düşeni mutasyondan bağımsız, yüklü makinede kayan bir zamanlama ölçüsü
(`[HedefMakineFact]`, CI'da atlanıyor, yerelde sessiz makine istiyor). Tek başına tekrarlandı:

```
--filter "FullyQualifiedName~OnHizliTikteAramalarYuzElliMilisaniyeSinirindaKalir"
Başarılı!  - Başarısız: 0, Başarılı: 1, Atlanan: 0, Toplam: 1, Süre: 240 ms
```

Aynı makinede arka arkaya üç uzun koşum döndüğü için bu ölçünün yerelde kayması beklenir;
kapanış kuralı hakkında bir şey söylemez.

### İki dökümün farkı

Dökümler satır satır karşılaştırıldı; `gecici\<ad>-<rastgele>` köklerinin adları her koşumda
değiştiği için onlar elendi. Geriye kalan **tek** fark:

```
$ diff yesil-dokum.txt kirmizi-dokum.txt | grep "^[<>]" | grep -v "gecici"
> dalga1\keymap-cakisma.txt
```

Kuralın söylediği tam olarak bu: kırmızı kolda yalnız düşen testin kanıtı ayakta kalıyor,
aynı klasördeki diğer on iki testin kanıtı siliniyor, geri kalan 12 klasör iki kolda birebir aynı.

### Kırmızı kolun dökümü (13 klasör, tamamı)

```
--- klasor: .calisma/oynatici-motor
oynatici-motor\h264_1080p60.mp4
oynatici-motor\h264_2160p30.mp4
oynatici-motor\hevc_1080p60.mp4
oynatici-motor\konum-kurulu
oynatici-motor\konum-kurulu\app
oynatici-motor\konum-kurulu\tools
oynatici-motor\konum-mac
oynatici-motor\konum-mac\app
oynatici-motor\konum-ortam
oynatici-motor\konum-ortam\app
oynatici-motor\konum-ortam\ozel
oynatici-motor\konum-ortam\tools
oynatici-motor\konum-yayin
oynatici-motor\konum-yayin\app
oynatici-motor\konum-yayin\tools
oynatici-motor\kucuk-320x180-25sn.mp4
oynatici-motor\serit-cikti-crf35.mp4
oynatici-motor\serit-kaynak-320x180-30.mp4
--- klasor: .calisma/dalga2
dalga2\gomulu.srt
dalga2\parca-2ses-1altyazi.mkv
--- klasor: .calisma/dalga3
dalga3\bolumler.txt
dalga3\bolumlu-320x180.mp4
dalga3\gecici
dalga3\gecici\ayar-8f8fa36e
dalga3\gecici\bilgi-82b668bb
dalga3\gecici\birak-0f70351a
dalga3\gecici\bolum-d3514a64
dalga3\gecici\goruntu-57e8205b
dalga3\gecici\goruntu-ayar-e4314242
dalga3\gecici\klasor-827d8c16
dalga3\gecici\klasor-ayar-26255267
dalga3\gecici\otomatik-All-54dd8083
dalga3\gecici\otomatik-Off-e284f31c
dalga3\gecici\secici-ayar-378aff89
dalga3\gecici\secici-ayar-c4aef90f
dalga3\gecici\secici-hedef-0ddcab57
dalga3\gecici\secici-hedef-f52968d6
dalga3\gecici\sira-b8ba77f8
dalga3\gecici\son-525128df
dalga3\gecici\son-ayar-4396e255
dalga3\gecici\ustte-70661357
--- klasor: .calisma/dalga7b -> YOK
--- klasor: .calisma/dalga4a
dalga4a\gecici
dalga4a\gecici\kalicilik-d0e80cd6
--- klasor: .calisma/dalga4b
dalga4b\gecici
dalga4b\gecici\adres-1d1af62b
dalga4b\gecici\ayar-67c879f3
dalga4b\gecici\klip-813d4501
dalga4b\gecici\kucukresim-a38d65f1
dalga4b\gecici\kucukresim-esik-75e279bb
dalga4b\gecici\serit-motor-e507104e
dalga4b\gecici\serit-surukleme-7ae8e1b3
--- klasor: .calisma/dalga5 -> YOK
--- klasor: .calisma/T176
T176\girdi-20sn.mkv
T176\k1-sekme.txt
--- klasor: .calisma/dalga1
dalga1\keymap-cakisma.txt
dalga1\player-recent.json
--- klasor: .calisma/oynatici-yol-haritasi
oynatici-yol-haritasi\p14
oynatici-yol-haritasi\p14-esik
oynatici-yol-haritasi\p14-esik\player-history.json
oynatici-yol-haritasi\p14\player-history.json
--- klasor: .calisma/oynatici-kisayol
oynatici-kisayol\gecmis
oynatici-kisayol\gecmis\player-recent.json
oynatici-kisayol\gecmis\player-settings.json
oynatici-kisayol\kirmizi-sol-mavi-sag-320x180.mp4
oynatici-kisayol\liste
oynatici-kisayol\liste\a-uzun-700sn.mkv
oynatici-kisayol\liste\b-iki-renk.mp4
oynatici-kisayol\tuslar
oynatici-kisayol\uzun.srt
--- klasor: .calisma/girdi-dalga3 -> YOK
--- klasor: .calisma/girdi -> YOK
```

### Yeşil kolun dökümü (13 klasör, tamamı)

```
--- klasor: .calisma/oynatici-motor
oynatici-motor\h264_1080p60.mp4
oynatici-motor\h264_2160p30.mp4
oynatici-motor\hevc_1080p60.mp4
oynatici-motor\konum-kurulu
oynatici-motor\konum-kurulu\app
oynatici-motor\konum-kurulu\tools
oynatici-motor\konum-mac
oynatici-motor\konum-mac\app
oynatici-motor\konum-ortam
oynatici-motor\konum-ortam\app
oynatici-motor\konum-ortam\ozel
oynatici-motor\konum-ortam\tools
oynatici-motor\konum-yayin
oynatici-motor\konum-yayin\app
oynatici-motor\konum-yayin\tools
oynatici-motor\kucuk-320x180-25sn.mp4
oynatici-motor\serit-cikti-crf35.mp4
oynatici-motor\serit-kaynak-320x180-30.mp4
--- klasor: .calisma/dalga2
dalga2\gomulu.srt
dalga2\parca-2ses-1altyazi.mkv
--- klasor: .calisma/dalga3
dalga3\bolumler.txt
dalga3\bolumlu-320x180.mp4
dalga3\gecici
dalga3\gecici\ayar-fd4d4508
dalga3\gecici\bilgi-3c8c94dc
dalga3\gecici\birak-00a32573
dalga3\gecici\bolum-250cb7b6
dalga3\gecici\goruntu-ae3c1b49
dalga3\gecici\goruntu-ayar-e12a2f07
dalga3\gecici\klasor-ayar-f7f026a9
dalga3\gecici\klasor-c9b93dcb
dalga3\gecici\otomatik-All-14fd1278
dalga3\gecici\otomatik-Off-a347180d
dalga3\gecici\secici-ayar-3458440d
dalga3\gecici\secici-ayar-e51fe0e8
dalga3\gecici\secici-hedef-2abad47f
dalga3\gecici\secici-hedef-caa8dc38
dalga3\gecici\sira-ea5405ca
dalga3\gecici\son-9843d07d
dalga3\gecici\son-ayar-2952030f
dalga3\gecici\ustte-b0e10541
--- klasor: .calisma/dalga7b -> YOK
--- klasor: .calisma/dalga4a
dalga4a\gecici
dalga4a\gecici\kalicilik-e367e018
--- klasor: .calisma/dalga4b
dalga4b\gecici
dalga4b\gecici\adres-217ef9b5
dalga4b\gecici\ayar-5bd3f644
dalga4b\gecici\klip-1e2fc91c
dalga4b\gecici\kucukresim-39579a93
dalga4b\gecici\kucukresim-esik-8e51d6e9
dalga4b\gecici\serit-motor-3026207e
dalga4b\gecici\serit-surukleme-8bd1a561
--- klasor: .calisma/dalga5 -> YOK
--- klasor: .calisma/T176
T176\girdi-20sn.mkv
T176\k1-sekme.txt
--- klasor: .calisma/dalga1
dalga1\player-recent.json
--- klasor: .calisma/oynatici-yol-haritasi
oynatici-yol-haritasi\p14
oynatici-yol-haritasi\p14-esik
oynatici-yol-haritasi\p14-esik\player-history.json
oynatici-yol-haritasi\p14\player-history.json
--- klasor: .calisma/oynatici-kisayol
oynatici-kisayol\gecmis
oynatici-kisayol\gecmis\player-recent.json
oynatici-kisayol\gecmis\player-settings.json
oynatici-kisayol\kirmizi-sol-mavi-sag-320x180.mp4
oynatici-kisayol\liste
oynatici-kisayol\liste\a-uzun-700sn.mkv
oynatici-kisayol\liste\b-iki-renk.mp4
oynatici-kisayol\tuslar
oynatici-kisayol\uzun.srt
--- klasor: .calisma/girdi-dalga3 -> YOK
--- klasor: .calisma/girdi -> YOK
```
