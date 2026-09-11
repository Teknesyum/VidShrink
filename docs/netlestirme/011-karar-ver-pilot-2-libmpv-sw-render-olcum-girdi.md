[[netlestirme:011]]

# Netleştirme: Karar ver: pilot 2 libmpv SW render olcumu sonrasi motor libmpv mi kalsin LibVLC

İşe başlamadan önce soruyu keskinleştir. Görüş verme, plan yazma, kod yazma.
Yalnız şunu döndür: soruda belirsiz kalan yerler, her biri için tek satırlık bir netleştirme sorusu, en fazla beş. Belirsizlik yoksa "net" yaz.

## Soru

Karar ver: pilot 2 libmpv SW render olcumu sonrasi motor libmpv mi kalsin LibVLC mi, ek olcum gerekir mi

## Elde olan olgular

# Fable 011 — olgular (pilot 2: libmpv SW render ölçümü)

Bu tur karar turu; "net" döndürme, soru döndürme. Karar ver.

## Kullanıcının cümlesi (aynen)
"izin verildi önce avalonia son sürüme güncelleme tamamlansın sonra v1 versiyon için gereksinimler önerdiğin şekilde yapılsın bu aşamada 12 geçmemizin senin önerilerinde bi değişiklik yapıp yapmadığını kısaca değerlendir gerekirse fable a sor fable ne derse onu uygula v1 e koşalım"
Önceki turdan: "en hızlı yöntemi istiyorum ancak tüm özellikleri tüm platformlarda istiyorum burda aşırı greddy olduğumuzu bilsin fable"

## Önceki kararlar
- 007: libmpv + kendi P/Invoke 115/140 (boyut girdisi "~34 MB"), LibVLCSharp 95 → T167 ölçümüyle ~109, HanumanInstitute 91.
- Plan eşiği (0. dalga adım 1): SW render 1080p ≥55, 4K ≥24 kare/sn; "libmpv arama medyanı ≤60 ms ve kurulum deltası <106 MB tutmazsa LibVLC geri çağrı yolu".
- 010: v1 kesiti win-x64 tam + osx-arm64 Standart 30; osx-arm64 bundle'a libmpv 1 turda gömülemezse tek motor kuralıyla her platformda LibVLC.
- Mimari: SW render BGRA → bugünkü WriteableBitmap yolu (Avalonia sürümünden bağımsız).

## Avalonia 12 (pilot 1) — tamamlanmak üzere
- 12.1.2: 8 derleme hatası / 4 dosya; başsız kurulum UseHarfBuzz; 35 test kırmızısı kök nedenden düzeltildi (OverlayLayer ebeveyni 0x0, Loaded gösterilmemiş pencerede ateşleniyor → Opened, IsEffectivelyVisible pencereyi katıyor, yedek font önbelleği kültüre bağlı, odak etkin görünürlük istiyor). Beklenti gevşetilmedi. 4 RID publish çıktı, Windows'ta açıldı, `--kucult 100` 152,4 MB → 103,3 MB. CI son koşum sürüyor.
- 12'nin motor önerisine etkisi: yok. HanumanInstitute.LibMpv.Avalonia 0.10.1 Avalonia ≥12.0.3 ister ama yalnız net10.0 hedefli (biz net8.0). LibVLCSharp.Avalonia 3.10.1 hâlâ Avalonia 11.3.13 hedefli. Bizim yol ikisini de kullanmıyor.

## Pilot 2 ölçümü (bu makine: Ryzen 7 9700X 8C/16T, RTX 5070 Ti, Win11)
libmpv: shinchiro 2026-09-03, mpv v0.41.0-1023, `vo=libmpv`, `MPV_RENDER_API_TYPE_SW` BGRA, kaynak boyutunda tampon. Test dosyaları ffmpeg testsrc2, 35 sn, GOP 2 sn. 3 tekrar; **2. ve 3. tekrarda makinede başka projelerin ~20 süreci çalışıyordu (sistem %96-100 meşgul); 1. tekrar temiz (%4-16).**

Azami SW render (untimed, kare/sn; temiz 1. tekrar / 3 tekrar medyanı):
| dosya | hwdec=no | hwdec=auto-copy (d3d11va-copy) | eşik |
|---|---|---|---|
| H.264 1080p60 | 1547 / 1046 | 1569 / 983 | ≥55 GEÇTİ |
| H.264 2160p30 | 397 / 269 | 430 / 303 | ≥24 GEÇTİ |
| HEVC 1080p60 | 1007 / 1007 | 1477 / 1402 | ≥55 GEÇTİ |
| HEVC 2160p30 | 279 / 185 | 215 / 215 | ≥24 GEÇTİ |

Arama (`seek <t> absolute+exact`, komuttan hedef karenin render'ına, 20 konum; temiz 1. tekrar medyanı / 60 aramanın medyanı / p90):
| dosya | hwdec=no | auto-copy | eşik ≤60 |
|---|---|---|---|
| H.264 1080p60 | 36,1 / 38,0 / 63,5 | 43,2 / 54,4 / 116,6 | GEÇTİ |
| H.264 2160p30 | 77,7 / 107,8 / 201,7 | 100,1 / 102,8 / 210,8 | KALDI |
| HEVC 1080p60 | 61,7 / 70,0 / 153,9 | 57,5 / 84,7 / 164,6 | sınırda/KALDI |
| HEVC 2160p30 | 172,0 / 174,4 / 280,7 | 194,5 / 193,0 / 330,6 | KALDI |
LibVLC karşılaştırması (T167): 38,9-57,2 ms medyan, farklı dosya (`parca-1.mkv`, gerçek çekim), farklı yöntem (kare parmak izi). Aynı dosyada LibVLC ölçülmedi.

Timed oynatma: 1080p60 ve 2160p30'da 15 sn pencerede 0 kare düşme, 60,0 / 30,0 kare/sn. İşlemci (tüm çekirdeklerin %'si): 1080p H.264 3,8 (SW) / 2,8 (auto-copy); 4K HEVC 8,4 / 4,8.
Başlangıç (mpv_create → ilk kare): 250-470 ms (1080p), 310-550 ms (4K). DLL yükleme 6-16 ms.
Bellek (çalışma kümesi tepe): 1080p 142-219 MB; 4K SW 555-706 MB, 4K auto-copy 310-415 MB.
mpv uyarı/hata 0, render hatası 0.

## Kurulum boyutu
- libmpv-2.dll (tek dosya, win-x64): açılmış **120.342.528 B**; zip (Optimal) 48.090.253 B; indirilen 7z 31.363.218 B. 007'deki "~34 MB" girdisi indirilen arşivin boyutuydu.
- LibVLC (T167 ölçümü, açılmış): paket varsayılanı 3 mimari +293.052.266 B (ölçüldü); yalnız win-x64 +106.005.111 B (klasör boyutundan türetildi, **doğrulanmadı**). VideoLAN.LibVLC.Windows NuGet önbellekte yok, bu tur doğrulanamadı.
- Uygulamanın bugünkü win-x64 publish'i: 214.540.808 B / 398 dosya (ffmpeg dahil). Kucult/Donustur için ffmpeg pakette kalıyor; libmpv kendi ffmpeg'ini DLL içinde taşıyor.
- Daha küçük libmpv derlemesi (özellik kırpılmış) denenmedi.

## Açık sorular
- Motor: libmpv kalsın mı, LibVLC'ye mi dönülsün? Plan eşiğinin boyut kolu (<106 MB) libmpv için tutmuyor (120 MB), arama kolu 1080p H.264'te tutuyor, 4K'da tutmuyor.
- Ek ölçüm gerekiyorsa hangisi, kaç turda: (a) LibVLC'yi aynı dört dosyada aynı yöntemle ölçmek, (b) temiz makinede tekrar, (c) kırpılmış libmpv derlemesi. Bir ek ölçüm ~1 tur.
- 4K arama >60 ms v1 için kabul edilebilir mi (GOM paritesi hedefi)?
