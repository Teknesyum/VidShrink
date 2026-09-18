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

M1a ilk taramada **sağ kaldı**: sahte yoklayıcı eşzamanlı (`Task.FromResult`) döndüğü için iki
yükleme hiç üstüste binmiyordu, önbellek tek başına yetiyordu. Gerçek açılış sırasında ffprobe
asenkron döner ve iki kol gerçekten binişir; `GecikmeliSayac`'lı
`UstusteBinenIkiYuklemeTekYoklama` eklendikten sonra M1a kırmızı verdi. Beş mutasyonun beşi
kırılıyor.

## Kapatılamayan

- `CurrentMedia.LastPositionSeconds` üretimde `Tabs.SelectionChanged` → `OdakKonumunuAnimsa`
  ile yazılıyor; bu kol libmpv ve gerçek oynatma gerektirdiği için yalnız nesnenin kendi
  sözleşmesi (`OdakKonumuYalnizOdaktakiDosyayaYazilir`) pimli, uçtan uca kol pimsiz.
- Yeni kullanıcı metni yok; `settings-tab.follow-recording.{label,hint}` T7'de 42 katalogda
  yerleşmişti ve gövdesi D0'ın semantiğini zaten söylüyor.
