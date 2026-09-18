# Plan — Yarım Kayıt Metni Kabına Göre Ayrılıyor

Defter satırı: "recorder.output.partial metni durumu soylemiyor". Karar
`docs/netlestirme/019-yarim-kayit-metni.md`: tek metin yerine kaba göre iki metin.

Olgu: öldürülen kayıtta dosya her zaman diskte durur, ama **oynatılabilir olması kaba
bağlı**. Matroska `-flush_packets 1` ile 76 okunur paket bırakıyor; mp4/mov `moov` atomunu
kapanışta yazdığı için 48 baytlık açılmaz dosya kalıyor. Ayrımı kod zaten
`RecorderArguments.SurvivesKill(container)` ile tutuyor.

## Adımlar

1. **Anahtar ikiye ayrılıyor.** `recorder.output.partial` oynatılabilir kolun metnini
   taşır, yeni `recorder.output.partial-broken` oynatılamayan kolu. 42 dil dosyasının
   hepsine.

2. **Seçim `SurvivesKill`'e bağlanıyor.** `RecorderView.ShowResult` yolun kabını
   `RecorderArguments.ContainerOf` ile çözer, `SurvivesKill` doğruysa birinci, değilse
   ikinci anahtarı yazar.

3. **Oynatıcı düğmesi oynatılamayan dosyada pasif.** `BtnToPlayer` yalnız dosya
   açılabilirken etkin; kullanıcı boşuna tıklayıp ikinci bir hata görmesin. Yol ve
   "klasörü aç" her koşulda duruyor, panel dosyayı silmiyor.

4. **Ölçü.** `KaydediciUyariTests`'e iki kol: mkv yarım kayıtta oynatılabilir metni ve
   etkin düğme, mp4 yarım kayıtta oynatılamaz metni ve pasif düğme. Negatif kontrol iki
   metnin gerçekten ayrı olması. `LanguageTests` yeni anahtarı 42 dilde arar.

5. **Belge.** `018`'in yanlış çıkan 3. adımına `019`'a gönderen not düşüldü. `AGENTS.md`
   satırı değişmiyor: uyarı/hata ayrımı aynı, değişen yalnız uyarının metni.

## Kapsam dışı

Yarım mp4'ü onarma. `moov` atomu yazılmamış, ffmpeg onaramaz; 48 baytlık dosya için
"onar" düğmesi vaat olurdu. Kullanıcıya verilen tek yararlı şey önlem: sonraki kayıtta MKV.
