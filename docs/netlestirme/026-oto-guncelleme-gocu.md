# 026 Otomatik Güncelleme Göçü

2026-09-26. Kullanıcı: "otomatik güncellemeyi varsayılan aç". Motivasyon: Serkan eski sürümde kalmıştı.

## Sorun

0.8.5-0.9.5 arası ayar kaydı `autoUpdate: false`'u herkesin dosyasına yazdı. Varsayılanın `false`'u ile
bilerek kapatanın `false`'u ayırt edilemiyor. Göç yoksa yeni varsayılan yalnız yeni kurulumlara ulaşır,
Serkan dahil mevcut kullanıcılara ulaşmaz.

## Danışma (fable, verbatim)

Soru: (A) göç yok; (B) işaret alanıyla tek seferlik göç; (C) göç + ilk açılışta bildirim.

> **Öneri: (B).** Gerekçe: istek "Serkan eski sürümde kalmasın" — göçsüz varsayılan, sorunu yaşayan tam o kitleye ulaşmıyor; (A) isteğin sözünü yerine getirip özünü boşa çıkarır.
>
> (C)'yi eklemem: bilerek kapatanların sayısı bu kitlede küçük, ve zaten kurulum kapanışta, açılışı uzatmadan oluyor; bir bildirim yüzeyi bu kadar az kişi için maliyetli ve her yeni kullanıcıya gürültü. İşaret alanı yeterli, ayardan kapatma yolu açık.
>
> **Kullanıcıya sorulmaya değmez.** Motivasyonu kendisi verdi (mevcut kullanıcı geride kaldı); doğal okuma göçü içerir. Yalnız teslimde bir satır not düş: "0.8.5+ kullanıcılarında da bir kez açılıyor, bilerek kapatan ayardan tekrar kapatır."

## Karar

(B). `UpdateSettings.Save` artık `autoUpdateDefaultOn: true` yazıyor. `Load`, bu işareti taşımayan dosyadaki
`false`'u Windows'ta `true` okuyor. İlk kayıttan sonra işaret dosyada kalıyor; kullanıcının yeniden kapattığı
`false` korunuyor. Pim: `UpdaterTests.OldUnmarkedOffTurnsOnOnceThenAChoiceSticks`, göç kolu kapatılınca kırmızı.
