# Doğrulama Turu — Ortak Kurallar

Bir araştırma kolu eksik tablosu yazdı. O tablo bir iddiadır. Senin işin her satırı ölçmek ve
kalan satırları iş paketlerine toplamak; yeni araştırma yapmak değil.

Önce `C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\pazar\ORTAK.md` dosyasını oku:
sütun kuralları, öncelik ölçütü, önceki kararlar ve yasaklar orada. Yasaklar sana da geçerli:
kod yazma, derleme, indirme, program çalıştırma yok; depoya yazma yok; yalnız araştırma
klasörüne yaz. Alt ajan açma; işi kendi araçlarınla yap.

## Her Satır İçin

1. **Bizde gerçekten yok mu?** Kodda en az üç adla ara: Türkçe ad, İngilizce ad, ffmpeg ya da mpv
   seçenek adı, arayüz metni (`src/VidShrink.App/Locales/`). Aramaları tek tek değil toplu yap
   (bir mesajda birkaç Grep). Bir şey bulduysan satırı aç ve oku: özellik çağrılıyor mu,
   arayüzde görünüyor mu, yoksa yalnız bir sabit ya da ölü kod mu?
   - Tam olarak varsa: satırı tablodan çıkar, **Çıkarılanlar**'a yaz.
   - Bir kısmı varsa: Bizde Durum'u `yarım: <ne var> (dosya:satır)` yap.
   - Yoksa: dokunma.
2. **Önceki karar.** ORTAK.md'deki karar listesine denk geliyorsa Karar `kapalı: <K no>`,
   Öncelik `3`.
3. **Defter.** `.claude/acik.md` içinde anahtar sözcükle ara; varsa Bizde Durum'un sonuna
   `defterde` ekle.
4. **Öncelik 1 satırlar.** Kaynak sütunundaki numaraları notlar dosyasında bul, URL'lerden en az
   birini WebFetch ile aç ve rakibin özelliği gerçekten sunduğunu gör. İkinci bağımsız kaynak
   yoksa bir tane bul. İki kaynakta göremediysen önceliği `2` yap, Düzeltmeler'e yaz.
5. **Öncelik ölçütü.** ORTAK.md'deki ölçüte uymuyorsa düzelt (tek rakipte olan `1` olamaz).
6. **Biçim.** Dokuz hücre; hücre içinde `|` yok; İş `küçük`, `orta` ya da `büyük` ile başlar;
   Karar `gerekmez`, `sahip: …` ya da `kapalı: …` ile başlar; Öncelik tek rakam.

Satır numaraları (No) korunur. Çıkarılan No yeniden kullanılmaz. Yeni satır ekleme. Koda
bakarken tabloda olmayan açık bir eksik görürsen **Ek Notlar**'a tek satır yaz.

Kararsız kaldığın satırı çıkarma; olduğu gibi bırak ve **Doğrulanamayanlar**'a yaz.

## Kardeş Tablo

Görev metni bir kardeş dosya adı verdiyse onu da oku. Aynı eksik iki tabloda da varsa satır silme;
**Yinelenenler**'e `OYA-07 = OYB-12` biçiminde yaz.

## Paket Önerisi

Kalan satırları iş paketlerine topla. Paket, ikinci aşamada tek bir işçinin tek seferde
teslim edebileceği birimdir: aynı dosyalara dokunan, birlikte denenebilen satırlar.

- Önceliği `1` ya da `2` olan ve Karar'ı `kapalı` olmayan her satır tam bir pakete girer.
- Önceliği `3` olan satır, bir paketin yanında neredeyse bedavaya geliyorsa o pakete eklenir;
  gelmiyorsa **Pakete Girmeyenler**'e yazılır.
- `kapalı` satır hiçbir pakete girmez.
- Bir paket `gerekmez` ve `sahip` satırlarını karıştırmaz: karar bekleyen iş, beklemeyen işi
  durdurmasın.
- `büyük` paketi bölebiliyorsan böl. Sekme başına dört ile on iki paket beklenir.
- Paket sırası senin önerdiğin yapım sırasıdır: önce küçük ve yüksek öncelikli olan.

Biçim sabit (betik okuyacak):

```
### <ÖNEK>-P1 — Paket Adı
- Satırlar: OYA-03, OYA-07
- İş: küçük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Playback/, src/VidShrink.Player/MpvEngine.cs
- Plan: İki ile dört cümle. Ne yapılır, hangi yoldan (ffmpeg süzgeci, mpv seçeneği, yeni arayüz parçası), neyle doğrulanır.
```

`İş` paketin toplamıdır: `küçük`, `orta` ya da `büyük`. `Karar` ya `gerekmez` ya da
`sahip: <sahibin vereceği kararın tek cümlesi>`.

## Çıktı

Aynı klasöre `<ad>-dogrulanmis.md`. Sırayla:

1. Tablo: aynı dokuz sütun, aynı sıra. Satırlar `| <ÖNEK>-` ile başlar.
2. `## Düzeltmeler` — tablo: `No`, `Ne Değişti`, `Kanıt`. Bu bölümde satırlar `| <ÖNEK>-` ile
   başlamasın: No'yu `` `OYA-07` `` gibi ters tırnak içine al.
3. `## Çıkarılanlar` — tablo: `No`, `Eksik`, `Neden`, `Kanıt (dosya:satır)`. No yine ters tırnakta.
   Kanıt satırını gerçekten açıp oku; tahmin yazma.
4. `## Yinelenenler`
5. `## Doğrulanamayanlar`
6. `## Ek Notlar`
7. `## Sekme Özeti` — beş ile sekiz satır: bu alanda pazara göre neredeyiz, en ağır üç eksik,
   en ucuz üç kazanç.
8. `## Paket Önerisi` — yukarıdaki biçimde paketler.
9. `## Pakete Girmeyenler` — No listesi ve tek cümle neden.

Dil sade Türkçe. Başlıklar Title Case. Sahibin bilmeyebileceği terime ilk geçtiği yerde tek
cümle açıklama.

## Saat

Görev metnindeki saatte dosya bitmiş olmalı. Önce tabloyu olduğu gibi yeni dosyaya kopyala,
sonra satır satır düzelt; yarıda kesilirsen elde dosya kalsın. Saate `date +%H:%M` ile bak.
Süre daralırsa sıra şu: önce öncelik 1 satırlar, sonra öncelik 2, sonra paketler, en son
öncelik 3 satırlar.

## Son Mesajın

Sekiz satırı geçmesin: kaç satır geldi, kaç kaldı, kaç çıkarıldı, kaç satırın önceliği değişti,
kaç öncelik 1 satırı kaynakta doğrulandı, kaç paket, dosyanın yolu. Sayıları tablodan say.
