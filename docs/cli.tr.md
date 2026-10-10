# VidShrink Komut Satırı

[README](../README.tr.md) içindeki *Komut satırı* bölümünün uzun hâli.
[English](cli.md)

Aynı paket, pencerenin yanında başsız bir komut satırı aracı da taşıyor: Windows ve
Linux'ta `vidshrink`, macOS'ta `vidshrink-cli`. Pencerenin çağırdığı karar motorunu
çağırıyor, yani aynı girdi aynı ffmpeg argümanlarını veriyor; bir test ikisini
karşılaştırıyor.

```bash
vidshrink kucult clip.mp4 --hedef 25MB              # boyuta küçült
vidshrink kucult clip.mp4 --kalite 80 --kodek av1   # kalite puanına küçült
vidshrink plan clip.mp4 --hedef 8MB --json          # yalnız plan ve argümanlar, kodlama yok
```

## Anahtarlar, İngilizce Takma Adlar Ve Çıkış Kodları

Anahtarlar: `--kodek auto|h264|hevc|av1`, `--cikti <yol>`, `--json`, `--olcumsuz` (yoklama
kodlamalarını atlar), `--vmaf` (ffmpeg'de libvmaf varsa sonucu ölçer), `--hizli`. İlerleme
stderr'e, boyut, süre, deneme sayısı ve VMAF stdout'a gidiyor. Yardım metni sistem dilini
izliyor, Türkçe ya da İngilizce. `--dil en` (`--lang en`) tek koşumluk olarak bunu eziyor;
bilinen kodlar `en` ve `tr`, tanınmayan kod sessizce İngilizce'ye düşmek yerine kullanım hatası
veriyor.

`--crf N` ve `--on-ayar AD`, pencerede Gelişmiş panelinin kilitlediğini kilitliyor: kalite
değeri (0-63) ve kodlayıcı ön ayarı. Ön ayar adı planın seçtiği kodeğe ait olmalı —
x264/x265 için `slow`, NVENC için `p5`, SVT-AV1 için `8` — ait olmayan ad hata vermiyor,
plan gerekçesine bir satır düşülerek düşüyor.

`--modul N` (`--modulus N`) ölçeklenen kenarları 2, 4, 8 ya da 16'nın katına aşağı
yuvarlıyor. Varsayılan 2, çünkü kodlayıcı tek sayılı kenar kabul etmiyor; büyük çarpan eski
donanım kodlayıcılarının istediği şey ve kenardan biraz daha kırpıyor. Başka bir sayı
kullanım hatası.

Anamorfik kaynak — depolanan pikselleri kare olmayan bir DVD — ölçeklenmeden önce
düzleştiriliyor: yükseklik korunuyor, genişlik karenin ekranda göründüğü genişliğe
çevriliyor ve çıktı kare pikselli oluyor. Kare pikselli kaynakta hiçbir şey değişmiyor.

`--kes <baslangic>-<bitis>` kodlamadan önce kesiyor, böylece hedef boyut elde kalan parçaya
harcanıyor: `--kes 10-40`, `--kes 0:10-0:40`, `--kes 1:02:03-1:02:04`, sona kadar `--kes 90-`.

Uçlardan biri saat yerine kare numarası olabilir: `--kes 300f-900f` 300. kareden 900. kareye,
çevirim kaynağın kare hızıyla. `--bolum 2` ve `--bolum 2-4` aynı pencereyi kaynağın bölüm
işaretlerinden kurar; iki seçenek birlikte verilemez.

`--yak N` (`--burn N`) kaynağın N. altyazısını görüntüye yakıyor. Metin altyazıyı libass
çiziyor; resim altyazı (PGS, VOBSUB, DVB) karenin üstüne bindiriliyor, kare genişliğine
ölçekli ve alta hizalı. MP4 resim altyazı taşıyamıyor; `--yak` verilmezse düşüyor ve plan
bunu seçeneğin adını vererek söylüyor. Arayüz aynısını "Görüntüye yak" listesinden yapıyor;
liste metin ve resim altyazıyı birlikte sunuyor.

`--altyazi-tara` (`--subtitle-scan`) o izi sizin yerinize seçiyor: filmin yalnız yabancı dilde
konuşulan yerlerini çeviren altyazıyı. Zorunlu (forced) bayraklı iz varsa o; yoksa altyazı
paketleri sayılıyor ve aynı dildeki en dolu izin en çok %10'u kadar paket taşıyan tek iz
yakılıyor. Sonuç açık değilse hiçbir şey yakılmıyor ve nedeni bir satırla söyleniyor. `--yak`
ile birlikte verilemiyor. Uygulamada "Görüntüye yak" listesinin yanındaki "Yabancı dil
altyazısını bul" düğmesi aynı işi yapıyor ve bulduğu izi listede seçiyor.

Kapak resmi MP4 ve MKV çıktıda korunuyor (MKV'de png ve jpeg, ek olarak). MOV taşıyamıyor;
plan bunu söylüyor.

Aşağıdaki uzun anahtarların her birinin bir de İngilizce takma adı var; iki yazım aynı
anahtar, betik hangisini isterse onu kullanabiliyor. Tek istisna `--crf` ve `--json` ve `--vmaf`:
bunların tek yazımı var.

| Türkçe | İngilizce |
|---|---|
| `--yardim` | `--help` |
| `--surum` | `--version` |
| `--hedef` | `--target` |
| `--kalite` | `--quality` |
| `--kodek` | `--codec` |
| `--on-ayar` | `--preset` |
| `--modul` | `--modulus` |
| `--bolum` | `--chapters` |
| `--cikti` | `--output` |
| `--kes` | `--cut` |
| `--aralik` | `--interval` |
| `--bir-kez` | `--once` |
| `--gunluk` | `--log` |
| `--tarama` | `--scan` |
| `--baslik` | `--title` |
| `--ana-icerik` | `--main-feature` |
| `--asgari-sure` | `--min-duration` |
| `--azami-sure` | `--max-duration` |
| `--suzgec` | `--filters` |
| `--kirp` | `--crop` |
| `--kirpma-kipi` | `--crop-mode` |
| `--profil` | `--profile` |
| `--profil-dosyasi` | `--preset-file` |
| `--ses-kodek` | `--audio-codec` |
| `--ses-normal` | `--loudnorm` |
| `--ses-kazanc` | `--gain` |
| `--altyazi` | `--subtitle` |
| `--yan-altyazi` | `--sidecar-subtitles` |
| `--yak` | `--burn` |
| `--yak-srt` | `--srt-burn` |
| `--yak-ass` | `--ssa-burn` |
| `--meta-yok` | `--no-metadata` |
| `--altyazi-dil` | `--subtitle-lang` |
| `--ilk-altyazi` | `--first-subtitle` |
| `--altyazi-tara` | `--subtitle-scan` |
| `--sabit-kare` | `--cfr` |
| `--tavan-kare` | `--pfr` |
| `--kare-hizi` | `--fps` |
| `--ses-hizi` | `--arate` |
| `--ses-drc` | `--drc` |
| `--profiller` | `--presets` |
| `--olcumsuz` | `--no-measure` |
| `--hizli` | `--fast` |
| `--dil` | `--lang` |

Çıkış kodları: bantta `0`, bandın altında `2` (kalite doyduğu için daha küçük dosya
saklandı), boy tavanı aşıldığında `3` (en küçük sonuç yine yazılıyor; JSON `output` ve
`overTarget: true` taşıyor), hatada `1`, yanlış kullanımda `64`, iptalde `130`.

## İzlenen Klasör

```bash
vidshrink izle ~/Gelen --cikti ~/Giden --hedef 25MB             # Ctrl+C'ye kadar koşar
vidshrink izle ~/Gelen --cikti ~/Giden --hedef 25MB --bir-kez   # klasörü boşaltır, çıkar
```

`izle`, klasöre düşen her videoyu küçültüyor. `--cikti` çıktı klasörüdür ve zorunludur;
izlenen klasörün kendisi olamaz.

`--aralik <saniye>` tarama aralığını belirliyor (varsayılan 2), `--bir-kez` beklenecek bir
şey kalmayınca çıkıyor, `kucult`'un öbür anahtarları her dosyaya uygulanıyor. `--json` ile
stdout NDJSON oluyor: dosya başına tek satır JSON nesnesi.

Bir dosya, boyu ve değişiklik saati üst üste iki tarama aralığı boyunca aynı kaldığında ve
onu tutan bir yazıcı olmadığında alınıyor — üçüncü tarama alıyor. Kodlanırken kaynak
değişirse çıktı siliniyor ve dosya durulunca yeniden ele alınıyor.

İlerleme, izlenen klasörün içindeki `.vidshrink-izle.json` dosyasında ada ve boya göre
tutuluyor; adı ya da boyu değişen dosya yeni sayılıyor. O klasör salt okunursa durum çıktı
klasörüne `.vidshrink-izle-<ozet>.json` olarak, o da tutmazsa ayar klasörüne
`izle-<ozet>.json` olarak yazılıyor.

`<ozet>`, izlenen klasörün yolunun SHA-256'sının ilk 16 onaltılık karakteri, küçük harfle.
Yol özete aşağıdaki iki kıyasla aynı kurala göre giriyor: koşan sistem harfi yok sayıyorsa
(Windows ve macOS) önce büyük harfe çevriliyor, Linux'ta olduğu gibi alınıyor. Yani `/gelen`
ile `/Gelen` Windows ve macOS'ta tek bir durum dosyasını paylaşıyor, Linux'ta iki ayrı dosya
alıyor; aynı klasör her koşumda aynı adı veriyor.

Başarısız olan dosya, bir sonraki açılışta bir kez yeniden denenir. Her şeyi yeniden
işlemek için durum dosyasını silin.

Koşan sistemin kuralına uyan tam iki kıyas var: izlenen klasörün çıktı klasörüyle
kıyası, ve bir adayın bu koşumun yazdığı çıktı adlarıyla kıyası. Bu ikisi Linux'ta
`Ordinal`, Windows ile macOS'ta `OrdinalIgnoreCase`.

Kuralın macOS yarısı varsayılan APFS bölümünü varsayıyor; o bölüm harf duyarsız ama harf
koruyordur. APFS harf DUYARLI da biçimlendirilebilir ve böyle bir bölümde — ya da harf
duyarlı bir dış bölümde — bu varsayım tutmuyor.

Dosya adının geri kalan her kullanımı **her platformda, Linux dahil** harfi yok sayıyor:
bekleyen, yeniden denenecek ve atlanan tabloları, tarama sırası ve işlenenlerin kaydı. Yani
`Klip.mp4` ile `klip.mp4` aynı izlenen klasörde, dosya sistemi ikisini iki ayrı dosya
olarak tutsa bile çakışıyor.

Çakışma taramanın içinde çözülüyor. Çakışan adlardan sıralı (ordinal) küçük olan kazanıyor
ve olağan akışta küçülüyor; öbürleri atlanıyor ve her biri için bir uyarı satırı yazılıyor:
`Atlandı: klip.mp4 — Klip.mp4 ile ad çakışıyor (harf farkı). Birini yeniden adlandırın.`
Satır dosya başına bir kez yazılıyor, sonraki taramalarda tekrarlanmıyor. Kazanan her
taramada aynı, yani koşum belirlenimli. Atlanan dosyayı yeniden adlandırınca izleyici onu
yeni dosya olarak görüp küçültüyor.

Çıkış kodları: bittiğinde `0`, `--bir-kez` bitip en az bir dosya başarısız olduğunda ya da
ad çakışması yüzünden atlandığında `4`, hatada `1`, yanlış kullanımda `64`, Ctrl+C ile
durdurulduğunda `130`. `--bir-kez` sonunda kaç dosyanın atlandığı bir özet satırında yazıyor.
