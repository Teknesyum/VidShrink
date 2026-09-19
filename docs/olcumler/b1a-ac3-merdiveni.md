# ac3 ve eac3: Hangi Bit Hızı Kabul Ediliyor

19 Eylül 2026. ffmpeg 9.0-full_build (gyan.dev). Kaynak: `anoisesrc` pembe gürültü, 48 kHz,
kanal sayısı `pan` ile kuruluyor. Betik ve çıktılar `.calisma/b1a-ac3/`.

B1a planı (`docs/plan.md`) ses bütçesinden seçilen `audioK`'yı ac3/eac3'e verecek. Kodlayıcının
kabul ettiği küme uydurulmaz; burada ölçüldü. Ölçülen iki şey var: **isteğin reddedilip
reddedilmediği** (çıkış kodu) ve **gerçekten teslim edilen bit hızı** (dosya yazılıp ffprobe'la
okunarak). İkisi ayrı, çünkü ffmpeg bazı istekleri reddetmiyor, sessizce merdivene oturtuyor.

## ac3 — kapalı merdiven, 640k tavan

| istenen | teslim | not |
|---|---|---|
| 24k | 32k | merdivenin altına düşen istek **yukarı** çekiliyor |
| 40k | 40k | |
| 48k | 48k | |
| 56k | 56k | |
| 96k | 96k | |
| 130k | 128k | basamak arasında kalan istek **aşağı** iniyor |
| 200k | 192k | aynı |
| 320k | 320k | |
| 640k | 640k | tavan |
| 700k | 640k | tavan aşılmıyor, reddedilmiyor |
| 768k | 640k | aynı |

Kural tek cümlede: **istekten küçük ya da ona eşit en büyük basamak; hiç yoksa en küçük
basamak; 640k'nın üstü 640k'ya iner.** Hiçbir durumda hata verilmiyor — yani ürün "reddedildi"
diye bir kola güvenemez, kendi merdivenini bilmek zorunda.

Ölçülen basamaklar (hepsi birebir teslim edildi): 32, 40, 48, 56, 64, 80, 96, 112, 128, 160,
192, 224, 256, 320, 384, 448, 512, 576, 640.

## Kanal sayısı bir taban dayatıyor

Merdivenin altı kanal sayısına göre yükseliyor. Bu kol **gerçekten hata veriyor** (kodlayıcı
açılmıyor), sessiz oturtma yok:

| kanal | ac3 tabanı | eac3 tabanı |
|---|---|---|
| 1 | 32k | 32k |
| 2 | 32k | 32k |
| 3 | 32k | 32k |
| 4 | 40k | 48k |
| 5 | 48k | 48k |
| 6 | 48k | 48k |

6 kanalda 24k, 32k ve 40k istekleri ac3'te reddedildi; 48k geçti. Ürünün çıkarması gereken
sonuç: çok kanallı bir izde bütçeden düşen `audioK` bu tabanın altına inerse ac3/eac3 kolu
kurulamaz, aac'ye dönülmeli ve kullanıcıya söylenmeli.

## eac3 — tavan yok, yuvarlama çok ince

| istenen | teslim |
|---|---|
| 130k | 130k |
| 200k | 200k |
| 333k | 332k |
| 640k | 640k |
| 768k | 768k |
| 1024k | 1024k |
| 3024k | 3024k |

eac3 ac3'ün kapalı merdivenini taşımıyor: istek neredeyse birebir teslim ediliyor (333k'da
1 kbit'lik oturma var, o da çerçeve boyutunun tam sayıya inmesinden). Bütçe hesabı açısından
eac3 ac3'ten **daha uysal**; tek sert kısıt kanal tabanı.

## Ürüne giren hüküm

- ac3 için merdiven koda yazılır ve bu belgeden okunur; `audioK` merdivene oturtulur.
- eac3 için merdiven yok, yalnız kanal tabanı kontrol edilir.
- İki kodekte de kanal tabanının altına düşen istek kabul edilmez: kol aac'ye döner ve not
  düşer. Taban uydurulmuş değil, yukarıdaki tabloda ölçülmüştür.
- Tavan yalnız ac3'te var (640k) ve aşıldığında hata değil oturma oluyor; ürün yine de
  isteği kendisi kırpar, çünkü bütçeye giren sayı ile teslim edilen sayı ayrışırsa hedef
  boyut hesabı yanlış olur.
