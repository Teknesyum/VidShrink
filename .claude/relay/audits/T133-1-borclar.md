# T133 denetim borçları

Denetim kararı **GEÇTİ**, KRİTİK yok. Denetçi: opus, bağımsız, worktree HEAD `68e9d1c`.
Aşağıdaki on bir madde borçtur — mühürü engellemez, kapatılmadı.

Denetçinin doğruladığı ve kusur çıkmayan noktalar mühür kaydında
(`T133-1.used.json`, `verification` alanı).

## Kapatılanlar (T0, mühürle aynı turda)

1. **`docs/olcumler/anahtar-kare-tavani.md:763` — sayım yanlıştı.** "on altı kolun
   **on dördünde** artıyor" diyordu, ardından iki azalan ve bir sabit kolu adıyla
   sayıyordu: 14+2+1=17 > 16. Gerçek sayım 13 artan / 2 azalan / 1 sabit.
   Üretici gövde (`tools/anahtar-kare/rapor-govde.md:343`) ile birlikte düzeltildi —
   `68e9d1c`.

5. **`tools/anahtar-kare/ortak.sh:3` — düzenek worktree'ye çakılıydı.** `ROOT` sabit
   worktree yoluydu; `git worktree remove` sonrası bütün `.sh`'ler ölecekti. Kök artık
   `BASH_SOURCE`'tan türüyor — `04c0de3`.

Ayrıca ham ölçüm verisi (23 KB: ızgara CSV'leri, `harita.json`, hizalama çıktıları)
`.calisma/t133`'ten `tools/anahtar-kare/veri/` altına alındı — `f780474`. `.calisma`
gitignore'da; worktree silinince rapor bir daha bağımsız üretilemezdi.

## Açık kalanlar

2. **`:7` — köken güvencesi fazla iddialı.** "Elle yazılan bölümlerde sayı yoktur; sayı
   geçen her yer üreticiden gelir" yanlış: K5 reçetesinin tamamı (R1 dayanak tablosu,
   ikinci-kodlayıcı tablosu, R2/R3 sayıları), "Sözleşmenin öncülündeki hata" ve Yöntem
   bölümleri elle yazılmış sayı taşıyor. Denetçi hepsini ham veriye karşı doğruladı,
   **sayılar doğru** — yanlış olan güvencenin kendisi.

3. **`:464-465` — "`net p50`'nin mutlak değeri rapor edilmiyor"** derken dört K2 tablosu
   ve sekiz özet cümlesi tam da o mutlak değeri raporluyor. Kastedilen "karara girmiyor";
   yazılan başka şey.

4. **`:534-538` — seçilmiş tablo.** "İşaret ikinci bir kodlayıcıda da aynı" başlığı
   altında mevcut beş satırdan üçü var; atlanan ikisinden biri **tek pozitif satır**
   (`av1_nvenc` `s4-yuksek` **+0,031**). İkisi de raporun başka yerinde (`:624`, `:699`)
   açıkça yazılı ve ikisi de 0,20 eşiğinin çok altında; sonuç değişmiyor.

6. **`tools/anahtar-kare/harita.py:3-7` — sabitler kendi modülünden okunmuyor.**
   ThresholdRule (0.05/0.15/0.08/2.09/40.0/0.90), `SceneDetector.BaseThreshold` 0.012,
   28/28, 5/10/10, 1.0 elle kopyalanmış. Bugün birebir tutuyor (denetçi kontrol etti),
   ama **R1 bu sabitlerin üçünü değiştirecek** ve kopyayı pimleyen hiçbir şey yok.
   Rapor `:105` "üretim koduyla aynı" diyor, "yeniden yazımı" demiyor.

7. **`tools/anahtar-kare/ortak.sh:19-24` — yöntem farkı yarım açıklanmış.** `libx264`
   iki geçiş, `libsvtav1` ve `*_nvenc` tek geçiş. Rapor bunu donanım kolu için yazıyor
   (`:667-669`), `libsvtav1` için yazmıyor; o tablodaki beş hücrenin üçü "eş boyut değil"
   (+%5,679'a kadar) ve sebebi belirtilmemiş.

8. **`tools/anahtar-kare/izgara.sh:19-20` — sessizce yutulan hata kolu.** Hata satırı
   12 kolonluk CSV'ye 6 alan yazıyor, `tablo.oku` `if r.get("mean")` ile onu sessizce
   düşürüyor. Bu koşumda hiçbir hücre kaybolmadı (20/20, 20/20, 20/20, 5/5, sıfır hata
   satırı), ama başarısız bir hücre K1'in "boş hücre yok" şartını gürültüsüzce delerdi.

9. **`:813, :816, :840` — K4'ün satır referansları bayat.** `FfmpegArguments.cs:172-174`
   / `:184-187` / `:179`, dalın tabanı `890af6e`ye göre doğru; bugünkü `origin/main`
   `55f245a`ta aynı metin `:233` / `:244-247` / `:239`da. Denetçi iki ağaçta da okudu,
   **değerler doğru** (+0,179 ve +0,033). K5'te "55f245a'ta okundu" notu var, K4'te yok —
   K4 kilit olduğu için düzeltilemez.

10. **`:159-164` — kaynak tablosunda ölçülmüş zorluk sayısı yok.** 3 Eylül kararı "sınıf
    adının yanında ölçülmüş zorluk sayısını da yaz, ad değil sayı taşısın" diyordu; tablo
    yalnız sınıf adı taşıyor. Ölçüm yapılmış (`tara-hareket.sh`, `:156-157`), sayısı
    rapora girmemiş.

11. **Rol dosyası verilen yolda yok.** `teknesyum-core/0.8.0/agents/auditor.md` mevcut
    değil; o dizinde yalnız `worker.md` var. Denetçi `teknesyum/2.67.0/agents/auditor.md`
    okudu. Ajan hafızasındaki `yapici-rol-dosyasi-builder-md` sınıfının denetçi yüzü.
