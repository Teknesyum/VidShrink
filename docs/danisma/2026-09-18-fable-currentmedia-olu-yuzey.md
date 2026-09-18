# Fable Danışması — `CurrentMedia`'nın Ölü Yüzeyi (18 Eylül 2026)

K19 D0 denetim borcu (a): "`CurrentMedia` yüzeyinin neredeyse tamamı üretimde okunmuyor".
Karar silme ile bağlama arasındaydı; danışma ikisinin arasını ayırdı.

## Gönderilen

> VidShrink deposunda (C:\Users\Administrator\Desktop\Projeler\VidShrink) bir karar veriyorum,
> senden kısa ve net bir hüküm istiyorum. Türkçe yanıtla, kod yazma, dosya değiştirme; yalnız
> oku ve karar ver.
>
> Durum: `src/VidShrink.App/CurrentMedia.cs` sekmelerin ortak odak nesnesi. Ölçtüm, üretimdeki
> okuma sayıları şöyle (obj/ hariç, testler hariç):
>
> - `Changed` olayı: 0 abone
> - `Owner` (enum `MediaFocusOwner`: None/Player/Shrink/Recorder): 0 okuma. Üç yerde yazılıyor
>   (`MainWindow.OdakTakibi.cs:60,86` Focus, `MainWindow.axaml.cs:3058,3145` Publish), hiçbir
>   yerde okunmuyor.
> - `DurationSeconds`, `SourceFps`: 0 okuma. İkisi de `Publish`'te `info.DurationSeconds` /
>   `info.Fps`'ten türüyor, yani `Info`'nun saf izdüşümü.
> - `Path` (public get): dışarıdan 0 okuma, sınıf içinde `Holds` kullanıyor.
> - Gerçekten tüketilenler: `InfoFor` (1), `Publish` (2), `Focus` (2), statik `SamePath` (1).
>
> Tasarım belgesi `docs/plan-duzenleyici.md:167-173` "D0 iki şey taşıyor: ortak CurrentMedia
> nesnesi ve onu **kimin** değiştirebildiğini söyleyen tek bir ayar" diyor, ama o ayarın
> `ChkFollowRecording` olduğunu da aynı paragrafta yazıyor — yani "kim değiştirir" kapısı
> `Owner` alanı değil, o onay kutusu. `Owner` belgede yalnız nesnenin taşıdığı alanlar
> listesinde geçiyor.
>
> Bu oturumda az önce `Focus`'un erken dönüş kolunda `Owner` değişince `Changed` yayılmadığını
> düzelttim ve ölçüyle pimledim (commit 880e2253). Yani şu an olay doğru çalışıyor ama kimse
> dinlemiyor.
>
> Depo kuralları: ölü dosya/ölü kod ağaçta durmaz; her iddia ölçülür; kullanıcı kolaylığı önde.
>
> Sorum tek: `Owner` + `MediaFocusOwner` + `Changed` + `DurationSeconds` + `SourceFps`
> **silinsin mi**, yoksa gerçek bir tüketiciye mi bağlansın (bağlanacaksa hangi somut kullanıcı
> faydası için)? Silme, `Focus`/`Publish` imzalarından `owner` parametresini de düşürür ve dört
> çağrı yerini sadeleştirir.
>
> Kararını tek cümlelik hükümle başlat, sonra en fazla beş satır gerekçe. Gerçekten bir tüketici
> öneriyorsan, o tüketicinin kullanıcıya ne gösterdiğini somut yaz — "ileride lazım olur"
> gerekçesini kabul etmiyorum.

## Gelen

> **Hüküm: Silinsin — `Owner`, `MediaFocusOwner`, `Changed`, `DurationSeconds`, `SourceFps` ve
> `Focus`/`Publish`'teki `owner` parametresi gitsin; tüketici uydurulmasın.**
>
> Gerekçe:
>
> - Belge "kim değiştirir" kapısını `ChkFollowRecording` diye adlandırıyor; `Owner` orada yalnız
>   alan listesinde. Kapı zaten onay kutusu, alan bir gölge.
> - Nesnenin tek gerçek işi `Path` + `Info` + damga üzerinden ffprobe önbelleği (`InfoFor`);
>   `DurationSeconds`/`SourceFps` bu `Info`'nun kopyası, iki gerçek kaynağı bir yerde tutuyor.
> - Aklıma gelen tek somut tüketici "Küçültme sekmesinde 'Bu videoyu oynatıcı seçti' etiketi" —
>   kullanıcı o dosyayı zaten dosya adıyla görüyor, etiket bilgi katmıyor.
> - 880e2253'teki düzeltme sıfır aboneye doğru yayım sağladı; olayı silince o test de nesneyle
>   birlikte `trash/`'e gider, yazık değil.
> - Kalan API: `Holds`, `InfoFor`, `Focus(path)`, `Publish(path, info)`, `SamePath` — belgenin
>   anlattığı şeyin tam kendisi.
>
> Yok

## Uygulanan

Hüküm olduğu gibi uygulandı. Kalan yüzey: `Path`, `Info`, `Holds`, `InfoFor`, `Focus(path)`,
`Publish(path, info)`, `SamePath`. Dört çağrı yeri sadeleşti.

Ölü yüzeyin geri gelmesi derlemeyi kırmadığı için ölçü yansımayla:
`OrtakOdakTests.OluOdakYuzeyiGeriGelmiyor`. `SourceFps`'i geri eklemek 1 kırmızı (ölçüldü).
