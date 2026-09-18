# 019 — Yarım Kayıt Metni: Yanıt

Danışılan: fable, 18 Eylül 2026. Girdi `019-yarim-kayit-metni-girdi.md`. Yanıt birebir:

---

## Karar: kabına göre iki metin

Tek metin, iki olgunun ortalamasını söyler ("oynatılamayabilir") ve okuyan kişi ne yapacağını bilemez. Arayüz yolu tutuyor, `SurvivesKill(container)` ayrımı zaten veriyor; seçim maliyeti bir `if`. İki metin.

Her iki durum da `RecordResult.Partial` kalır ve `StatusWarning` biçimiyle gösterilir: kullanıcı durdurdu, iş bitti, eksik var. Oynatılamayan dosyada metin sert ("oynatılamaz") ama bu "hata"ya terfi etmez; durumu kap değil sonuç türü belirliyor. Çelişki yok.

## Metinler

Ortak ilk cümle kullanıcının bakış açısından: durdurma isteğini o verdi, ffmpeg'in zaman aşımı onun sorunu değil. "Zorla durduruldu / öldürüldü" yok, "temiz kapanmadı" var. Noktalı virgül yok; her cümle tek başına çevrilebilir.

**Oynatılabilir (`SurvivesKill == true`, bugün Matroska):**

- TR: `Kayıt temiz kapanmadı. Dosya kaydedildi ve oynatılabilir. Son saniyeler eksik olabilir.`
- EN: `Recording did not close cleanly. The file is saved and can be played. The last seconds may be missing.`

**Oynatılamaz (`SurvivesKill == false`, mp4/mov):**

- TR: `Kayıt temiz kapanmadı. Dosya oynatılamaz. Sonraki kayıt için MKV seçin.`
- EN: `Recording did not close cleanly. The file cannot be played. Choose MKV for the next recording.`

"MKV" yerine kap seçicide görünen etiket ne ise o yazılmalı; kullanıcı metindeki adı listede bulmalı.

## Sonraki adım

**Oynatılabilir:** yapılacak bir şey yok, dosya kullanılır. Panelde yol zaten var; oynatıcı sekmesine açan düğme bu durumda etkin kalsın. "Son saniyeler eksik olabilir" beklentiyi ayarlar: kullanıcı sonunu izleyip şaşırmaz. Kesilen Matroska'da süre ve cues yazılmamış olur, bazı oynatıcılar süreyi gösteremez ve sarma kısıtlı olur; bu satıra sığmaz, ama açan düğme çalıştığı sürece sorun değil.

**Oynatılamaz:** kurtarma yok. moov atomu yok, ffmpeg onaramaz; 48 baytlık dosya için "onar" vaadi verilmez. Kullanıcıya verilebilecek tek yararlı şey önlemdir, o da metinde: bir sonrakinde MKV. Oynatıcıya açan düğme bu durumda pasif olsun, kullanıcı boşuna tıklayıp ikinci bir hata görmesin. Dosyayı silmek kullanıcının kararı; panel silmesin, yolu göstermeye devam etsin.

Varsayılan kap zaten Matroska, yani ikinci metin yalnız kabı kendisi değiştiren kullanıcının önüne çıkar. Metnin "MKV seçin" demesi bu yüzden yerinde: kişi o seçimi yapmış olan kişidir.

---

## Not: 018'in 3. adımı düzeltildi

`018-uyari-rengi-27-palette-yok.md`'nin 3. adımı "dosya duruyor, oynatılabilir" diyordu.
Ölçüm bunu yalnız Matroska için doğruluyor; mp4/mov'da öldürülen dosya `moov` atomu
yazılmadan kalıyor (ölçülen örnek 48 bayt, ffprobe "moov atom not found"). O adım bu
belgeyle değiştirildi.
