# K19 D0 — Ortak Odak Nesnesi: Ölçü Ve Mutasyon Dökümü

Sözleşme: `docs/danisma/2026-09-17-fable-kararlar.md` 8. bölüm. Dal `t0/k19-d0-currentmedia`,
taban `f2ef2bfd`. Derleme `dotnet build VidShrink.sln -c Release -warnaserror -m:2`,
koşum `dotnet test ... --no-build --filter FullyQualifiedName~<sınıf>`.

Yoklama ffprobe'a hiç inmez: `MainWindow.Prober` dikişine sayaçlı sahte yoklayıcı takılır
(`OrtakOdakTests.Sayac`, `OrtakOdakTests.GecikmeliSayac`). Ölçülen sayı o sayaçtır.

## Kabul Ölçütü 1 — Oynatıcıdan Küçült'e Geçişte Bir Yoklama

`OrtakOdakTests.OynaticidanKucultmeyeGecisteYoklamaBirKez` çıktısı
(`.calisma/k19-d0/gecis.txt`, birebir):

```
dosya=...\.calisma\k19-d0\gecis.mkv
yoklama=1
kucultme=...\.calisma\k19-d0\gecis.mkv
sure=12,5
fps=30
```

`sure` ve `fps` ortak nesneden okunur (`window.Media.DurationSeconds`, `.SourceFps`), yani
Küçült sekmesi kendi yoklamasını değil odağın çözümlemesini kullanıyor.

## Kabul Ölçütü 2 — Ayar Kapalıyken Değişmez, Açıkken Değişir

`OrtakOdakTests.AyarKucultmeninDosyasiniBelirler` çıktısı (`.calisma/k19-d0/ayar.txt`, birebir):

```
taban=...\.calisma\k19-d0\ayar-onceki.mkv
kapali=...\.calisma\k19-d0\ayar-onceki.mkv yoklama=0
acik=...\.calisma\k19-d0\ayar-kayit.mkv yoklama=1
```

Kapalı kolda Küçült'ün dosyası tabanda kalıyor ve **hiç** yoklama yapılmıyor; açık kolda
kayıt dosyasına geçiyor ve bir yoklama oluyor.

## Koşulan Kollar

| Sınıf | Test | Sonuç |
| --- | --- | --- |
| `OrtakOdakTests` | 6 | 6 geçti |
| `KayitOdakTakibiTests` | 4 | 4 geçti (`VIDSHRINK_LIBMPV` verildiğinde) |
| `AyarKaliciligiTests` | 20 | 20 geçti |
| `BiciminTests` | 19 | 19 geçti |
| `ChipTests` | 14 | 14 geçti |
| `AdvancedPanelTests` | 23 | 23 geçti |
| `CliTests` | 69 | 69 geçti |
| `OluUyeTests` | 15 | 15 geçti |
| `HipersurusTests` | 7 | 7 geçti |
| `KayitTeslimTests` | 10 | 10 geçti |

`KayitOdakTakibiTests` worktree'de libmpv olmadığı için ilk koşumda
`SecenekAcikkenKayitKucultmeyeVeOynaticiyaSekmeDegismedenYuklenir` kolunda kırmızı döndü:
satır 167, `olcu.oynatici` null. Aynı koşum `VIDSHRINK_LIBMPV` verildiğinde 4/4 yeşil; kusur
değil ortam eksiği (`Birleşme worktreesinde libmpv yok`). `olcu.kucultme` her iki koşumda
doğru — Küçült tarafı etkilenmiyor.

## Mutasyon Dökümü

Her mutasyon elle uygulandı, Release derlendi, `OrtakOdakTests` koşuldu, sonra kaynak elle
geri yazıldı (`git checkout` kullanılmadı).

| # | Mutasyon | Dosya | Sonuç |
| --- | --- | --- | --- |
| M1a | Uçuştaki yoklamanın paylaşımı silindi | `MainWindow.OdakTakibi.cs` | 1 kırmızı — `UstusteBinenIkiYuklemeTekYoklama` |
| M1b | Önbellek kısayolu (`_media.InfoFor`) silindi | `MainWindow.OdakTakibi.cs` | 2 kırmızı |
| M2 | `CurrentMedia.Fresh` her zaman `true` | `CurrentMedia.cs` | 1 kırmızı — `DosyaDiskteDegisirseYenidenYoklanir` |
| M3 | `FollowRecordingAsync`'teki ayar kapısı silindi | `MainWindow.OdakTakibi.cs` | 1 kırmızı — `AyarKucultmeninDosyasiniBelirler` |
| M4 | `CurrentMedia.Remember`'daki yol kapısı silindi | `CurrentMedia.cs` | 1 kırmızı — `OdakKonumuYalnizOdaktakiDosyayaYazilir` |
| M5 | `Publish` süre/fps taşımıyor (`= 0`) | `CurrentMedia.cs` | 1 kırmızı — 12,5 → 0 |
| M6 | Küçült kendi yoklamasına döndü (`Prober(path, …)`) | `MainWindow.OdakTakibi.cs` | 3 kırmızı |
| M7 | Doğru sayı, yanlış yol (`Prober(path.ToUpperInvariant(), …)`) | `MainWindow.OdakTakibi.cs` | 4 kırmızı, sayı değişmeden |

M5-M7 bağımsız denetçinin mutasyonları (18 Eylül 2026). M7 ikinci commit'in eklediği
`Yollar` piminin ölü olmadığını gösteriyor: sayaç aynı kalıyor, yalnız yol büyük harfe
dönüyor ve ölçü bunu görüyor:

```
  Başarısız OrtakOdakTests.OynaticidanKucultmeyeGecisteYoklamaBirKez
   Assert.Equal() Failure: Collections differ
   Expected: ["C:\\Users\\Administrator\\Desktop\\Projeler\\VidSh"···]
   Actual:   ["C:\\USERS\\ADMINISTRATOR\\DESKTOP\\PROJELER\\VIDSH"···]
Başarısız! - Başarısız: 4, Başarılı: 2, Toplam: 6
```

M5, `sure`/`fps`'in sabit karşılaştırması değil üretim yolundan geçen değer olduğunu pimler.

M1a ilk taramada **sağ kaldı**: sahte yoklayıcı eşzamanlı (`Task.FromResult`) döndüğü için iki
yükleme hiç üstüste binmiyordu, önbellek tek başına yetiyordu. Gerçek açılış sırasında ffprobe
asenkron döner ve iki kol gerçekten binişir; `GecikmeliSayac`'lı
`UstusteBinenIkiYuklemeTekYoklama` eklendikten sonra M1a kırmızı verdi. Beş mutasyonun beşi
kırılıyor.

## Kanıt Klasörünün Kapanışı

Dalın tabanı `f2ef2bfd` kuralı koymuştu: yeşil koşum kendi bıraktığını siler, kırmızı koşum
kanıtını korur. D0 ilk teslimde bu kuralı uygulamamıştı; `Kapat` yardımcısı altı testin de
son asertinden **sonra** çağrılıyor, düşen asert oraya hiç varmıyor. Klasör boşalınca o da
siliniyor. İki yönde ölçüldü.

Yeşil:

```
Başarılı!  - Başarısız:     0, Başarılı:     6, Atlanan:     0, Toplam:     6, Süre: 2 s
--- klasor:
yok
```

Kırmızı (`Assert.Equal(30, olcu.Fps)` → `31`):

```
Başarısız! - Başarısız:     1, Başarılı:     5, Atlanan:     0, Toplam:     6
--- klasor:
gecis.mkv
gecis.txt
```

Yalnız düşen testin dosyaları kalıyor; diğer beş test kendi bıraktığını yine siliyor.
Geri yazmada `Copy-Item`'ın LastWriteTime'ı koruduğu tuzağa düşüldü: kaynak `30` okurken
ölçü hâlâ `Expected: 31` diyordu, çünkü MSBuild dll'i taze sayıp derlemeyi atlıyordu.
`(Get-Item <yol>).LastWriteTime = Get-Date` sonrası derleme gerçekleşti ve 6/6 yeşil döndü.

## Kapatılamayan

- `CurrentMedia.LastPositionSeconds` üretimde `Tabs.SelectionChanged` → `OdakKonumunuAnimsa`
  ile yazılıyor; bu kol libmpv ve gerçek oynatma gerektirdiği için yalnız nesnenin kendi
  sözleşmesi (`OdakKonumuYalnizOdaktakiDosyayaYazilir`) pimli, uçtan uca kol pimsiz.
- Yeni kullanıcı metni yok; `settings-tab.follow-recording.{label,hint}` T7'de 42 katalogda
  yerleşmişti ve gövdesi D0'ın semantiğini zaten söylüyor.
