# MKV Ve MOV Çıktıda Kapak Resmi

Tarih 2026-10-07, dal `worktree-agent-a56b0b45fa9b372c7`, ffmpeg 9.0 (gyan.dev full build).
Kaynak klipler 3 sn 320x240 `testsrc` + `sine`, 64x64 tek renk kapak; her koşum `-threads 2`, tek süreç.
Kapak izi burada ffprobe'un `attached_pic=1` gösterdiği izdir.

## Sonuç

- **MKV:** kapak taşınıyor. Koşucu kapağı kaynaktan bayt bayt çıkarıp `-attach` ile ek olarak yazıyor.
- **MOV:** taşınamıyor. Plan `StreamNote.CoverDropped` notunu basıyor (`plan.stream.cover-dropped`,
  arayüzde `main.reason.stream.cover-dropped`).
- **MKV'de bmp kapak:** taşınamıyor, aynı not. png ve mjpeg taşınıyor.
- **MP4:** değişmedi, kapak izi `-map` + `-c copy` + `attached_pic` ile gidiyor.

## Ölçüm: Kapak İzini Eşlemek Yetmiyor

| Koşum | Komutun kapak kısmı | Çıktıda |
|---|---|---|
| MP4 → MKV | `-map 0:2 -c:v:1 copy -disposition:v:1 attached_pic` | ikinci bir video izi, `attached_pic=0` |
| MKV → MKV | aynı | ikinci bir video izi, `attached_pic=0` (etiketler `FILENAME`, `MIMETYPE` olarak kalıyor) |
| MP4 → MOV | aynı | iz yok, çıkış kodu 0, uyarı yok |
| MKV → MP4 | aynı | `attached_pic=1` |
| MP4 → MKV | `-attach kapak.png -metadata:s:t:0 mimetype=image/png -metadata:s:t:0 filename=cover.png` | `attached_pic=1`, iki etiket yerinde |

MKV'de kapak bir video izi değil bir ektir; iz olarak eşlenince oynatıcı onu kapak diye değil ikinci
video diye görür. MOV eşlemeyi sessizce atıyor: hata yok, uyarı yok. Bu yüzden MOV'da not zorunlu.

## Ölçüm: Ek Yolu

| Soru | Ölçülen |
|---|---|
| Çıkarılan kapak kaynakla aynı bayt mı (`-map 0:N -c copy -frames:v 1 -update 1 -f image2`) | MP4'ten png, jpg, bmp ve MKV'den png: dördü de bayt eşit |
| Çıktı MKV'den geri çıkarılan kapak kaynakla aynı bayt mı | eşit |
| jpg ek | `mjpeg`, `attached_pic=1`, `mimetype=image/jpeg` |
| bmp ek | `codec_type=attachment`, `attached_pic=0`: kapak sayılmıyor |
| `mimetype` etiketi yazılmazsa | başlık yazılamıyor ("no mimetype tag"), çıkış 127, çıktı 0 bayt |
| Kaynakta bir font eki kopyalanırken kapak | kapak ikinci ek; etiketi `-metadata:s:t:1` ile doğru yere gidiyor |
| `-filter_complex` + `-map [v]` (resim altyazı yakma) ile birlikte | kapak yerinde, MKV'de de MP4'te de |
| Kesit (`-ss`/`-t`) ile birlikte | kapak yerinde |
| `-map_metadata -1` ile birlikte | kapak ve iki etiketi yerinde |

Üç karar buradan çıktı. Etiket belirteci sabit `0` değil, kopyalanan ek sayısıdır
(`StreamPlan.CoverAttachment`). `mimetype` her zaman yazılır, yazılmazsa dosya hiç oluşmuyor. bmp
ek olarak yazılabiliyor ama kapak olarak okunmuyor, o yüzden taşınmış sayılmaz.

Ek yalnız son geçişte ve çıkarılan dosyanın yolu varken yazılır. Çıkarma düşerse kodlama durmaz;
koşucu `EncodeRunner.CoverLostWarning` uyarısını verir ve çıktı kapaksız kalır.

## Mutasyonlar

`KapakResmiTests` 13 kol. Her mutasyon tek yeri değiştirdi, Release derlendi, yalnız bu sınıf
koşuldu, dosya elle geri yazıldı.

| Kod | Mutasyon | Sonuç |
|---|---|---|
| M1 | `mimetype` etiketi yazılmaz | kırmızı |
| M2 | ek belirteci `0`'a sabit | kırmızı |
| M3 | taşınamayan kapakta not düşmez | kırmızı |
| M4 | ek ilk geçişte de yazılır | kırmızı |
| M5 | kayıp uyarısı susar | kırmızı |
| M6 | kapak baytı bütçeye girmez | kırmızı |
| M7 | bmp de ek sayılır | kırmızı |
| M8 | çıkarma sürecinin çıkış kodu denetlenmez | yeşil (eşdeğer) |
| M9 | platform tesliminde de ek yazılır | kırmızı |

Dokuzun sekizi kırmızı. Kol başına kırılan test sayısı kaydedilmedi. M8 eşdeğer: çıkış kodundan
sonra dosyanın varlığı ve boyutu ayrıca denetleniyor, düşen çıkarma oradan yakalanıyor.

## Ölçülmeyenler

- Gerçek bir film dosyasının kapağı; yalnız 64x64 lavfi resmi denendi.
- Oynatıcıların (VLC, mpv, Kodi) MKV ekini kapak olarak göstermesi; yalnız ffprobe okundu.
- Birden çok kapak taşıyan kaynak; ilk kapak alınıyor.
- gif ve webp kapak; png ve mjpeg dışındaki her kodek not düşürür.
- WebM çıktı: kapak taşınmıyor ve bu tur ona not eklemedi (`StreamMapping.Decide` WebM'i dışarıda tutuyor).
