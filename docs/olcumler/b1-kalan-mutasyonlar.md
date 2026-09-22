# B1 Kalanı: Mutasyon Ölçümü

Tarih 2026-09-22, dal `worktree-agent-ac648426fe8f56b4a`. Taban: altı sınıf filtreli koşumda 0/131 kırmızı
(Release, yerel ffmpeg ile altı canlı kol dahil). Her mutasyon tek satırı değiştirip test projesini Release
derledi, yalnız ilgili sınıfı koştu ve dosyayı bayt bayt geri yazdı.

**18 mutasyonun 18'i kırmızı; her madde en az üç kırmızı.**

| Kod | Madde | Sınıf | Mutasyon | Sonuç | Kırılan testler |
|---|---|---|---|---|---|
| M1 | flac | FlacSesTests | bütçe koşulu kaldırıldı | 1/9 | SigmayincaVarsayilanaDusuyor |
| M2 | flac | FlacSesTests | `-sample_fmt s16` yerine `-b:a` | 2/9 | ButceYeterkenFlacKuruluyor, CanliFlacCiktisi |
| M3 | flac | FlacSesTests | tavan 24 bit | 5/9 | ButceYeterkenFlacKuruluyor, TavanOrneklemeVeKanaldan |
| M4 | loudnorm | SesNormallestirmeTests | `volume=` zincirden düştü | 3/11 | CanliSeviye, KapDegisinceSuzgecKorunuyor, ZincirSirasi |
| M5 | loudnorm | SesNormallestirmeTests | süzgeç isteği kopyayı engellemiyor | 2/11 | KapDegisinceSuzgecKorunuyor, SuzgecKopyayiEngelliyor |
| M6 | loudnorm | SesNormallestirmeTests | kopyada not düşmüyor | 1/11 | KopyaZorunlukenNotDusuyor |
| M7 | dış altyazı | DisAltyaziTests | eşleme `0:0`'a kaydı | 5/9 | CanliDisAltyazi, KapKodegiSeciyor |
| M8 | dış altyazı | DisAltyaziTests | MP4'te `copy` | 3/9 | CanliDisAltyazi, KapKodegiSeciyor |
| M9 | dış altyazı | DisAltyaziTests | kesitsiz ek `-i` yazılmıyor | 2/9 | CanliDisAltyazi, GirdiSirasiVeKesit |
| M10 | kapak | KapakResmiTests | her kap kapak taşır | 3/4 | CanliKapak, KapakTasinmayanYerler, KapakVarkenBayraklarIzBasina |
| M11 | kapak | KapakResmiTests | kapak baytı bütçeden düştü | 1/4 | Mp4KapagiTasiyor |
| M12 | kapak | KapakResmiTests | `-c:v:0` yerine `-c:v` | 1/4 | KapakVarkenBayraklarIzBasina |
| M13 | forced | ForcedAltyaziTests | var olan varsayılan korunmuyor | 1/5 | DegismeyenListeler |
| M14 | forced | ForcedAltyaziTests | tercih edilen dil atlandı | 1/5 | SecimSirasi |
| M15 | forced | ForcedAltyaziTests | `DefaultForced` çağrılmıyor | 2/5 | CanliForcedVarsayilan, KararBayragiYaziyor |
| M16 | yakma | AltyaziYakmaTests | yakma zincire eklenmiyor | 2/7 | CanliYakma, YakmaOlceklemedenOnce |
| M17 | yakma | AltyaziYakmaTests | yakılan iz yine eşlenir | 2/7 | CanliYakma, YakilanIzEslenmiyor |
| M18 | yakma | AltyaziYakmaTests | `setpts` işaretleri ters | 1/7 | SuzgecMetni |

## Kapak Sınırı

Kapak yalnız MP4'te taşınıyor. Deneme ffmpeg koşumunda MOV çıktısı `attached_pic` izini sessizce düşürdü;
MKV aynı eşlemeyle kapak yerine tek karelik düz bir video izi yazdı. İkisi de kapak sayılmadığından
`CarriesCover` yalnız MP4'e açık; `KapakTasinmayanYerler` ve canlı kolun MKV tarafı bunu pimliyor.
