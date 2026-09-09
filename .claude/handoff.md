# Devir notu — 9 Eylul 2026

Bir onceki devir notundaki dort adimin **ilk ikisi bitti ve muhurlendi**. `main` ilk kez
tumuyle yesil. Kalan is: kareler, README, 0.3.1.

## Durum

`main` = `aa03efc`. Acik sozlesme **yok** (`.claude/relay/contracts/` altinda yalniz
`done/`). Calisan ajan yok. Worktree birikintisi yok — `VidShrink-T192` ve
`VidShrink-T193` kaldirildi.

Tam suit, T192 ve T193 birlestikten sonra, `VidShrink-T0main` agacinda:

```
Basarisiz: 0, Basarili: 1937, Atlanan: 24, Toplam: 1961, Sure: 21 dk 47 sn
```

## Bugun kapananlar

**T189 — ekran goruntusu duzenegi.** Dort tur. 14/14 kare belirlenimli. Son fark kaynagi
`Pulse`'un 160 ms'lik opaklik zamanlayicisiydi; duzeltme yalnizca duzenek tarafinda
(`src/VidShrink.App/` diff = 0 satir), bagimsiz piksel olcumuyle dogrulandi.

**T192 — ad ve birim yazimi.** Bes tur. Kok sebep `LanguageCatalog.cs`'in `Sentence()`
govdesi: ad sozlugu yalnizca satir basi sozcugune uygulaniyordu, `FFmpeg` cumle
ortasinda `ffmpeg`e dusuyordu. 950 anahtarin 163'unun ciktisi degisti, 124'u kol
degistirdi. Uretim degisikligi pimlendi (`LanguageTests.cs:1110`; mutasyon en 14 + tr 25
= 39 sozcuk kirmizi).

**T193 — max sikistirma modu. Acilmiyor, ve bu artik olculmus bir hukum.** Uc anahtar:
`qcomp` 4312 bayt (gurultu esigi 66614), `zones` 7660 bayt, `qp-scale-compress-strength`
p10 **+0,471** (esik +0,50) ve en kotu sahne **-0,248** (esik +1,00, isaret ters). Son
anahtar gercekten calisiyor — VMAF gurultu tabani 0,061 olculdu, kazanc gurultu degil —
ama esige yetmiyor. Denetci "olcumu genisletmek acar mi" sorusuna sayiyla hayir dedi.

**Yan is:** `KillTree` olcusu main'de kirmiziydi (T192/T193 disi). `712c196` ile kapatildi:
PID 4 uzerinde `HasExited` yoklamasi `Win32Exception`a karsi korundu. **Bedeli:** olcu
artik yalnizca yukseltilmis oturumda gercekten kosuyor.

## Devam dendiginde kosulacak uc adim

1. **Kareleri yenile.** `dotnet run --project tools/VidShrink.Shot`. T192'nin K5'inde
   "T0'a birakildi" diye isaretli; T189 ve T192 birlestigi icin artik onu bekleyen yok.
   Cikan kareler `docs/gorseller/` altina.

2. **T190 README** — EN + TR, mermaid semalari, yeni kareler. Bittiginde fable gorusu al
   (`advice.js`), donen metni `docs/danisma/` altina tam metin yaz.

3. **0.3.1'i kes.** Girecekler: T176 (oynatici girdisi), T185 (oynatici sekmesi en sola),
   T188 (kabuk entegrasyonu), T191 (kabuk klasoru), T189, T192, T193, `712c196`.
   `v0.3.0` etiketi `3a6351a7`'de; bunlarin hicbiri yayindaki ikilide yok.

## Kapanmayan borc — surum notunda soylenmeli

Kabuk paketini isletim sistemine tanitan tek yer `Install-VidShrink.ps1`. Uygulamada ya
da baslaticida `Add-AppxPackage -Register` cagiran kod yok. **Asil kilit imzalama** —
T188 uretimde `0x800B0100` olctu. 0.3.1 bunu kapatmiyor.

## Muhurlerde duran acik borclar

**T192:** `:169`'un gosterdigi `dokum-fark.txt` 163 kayit tasiyor ama cumle 123 diyor;
`:5` ham ciktilarin yerini eksik veriyor; `:26`/`:300` `BiciminTests.TumCiktiDokulur`
yaziyor, metot `BaslikKapsamiTests` sinifinda. Ayrica `ShrinkJobWindow.axaml.cs:274`
hala `InvariantCulture` ile kullaniciya gorunen metin uretiyor (ayri is).

**T193:** gurultu tabani n=3 ile olculdu ve bu kisit belgede yazili degil (daha genis
tabanla ~0,15 cikardi, hukum donmezdi); uclunun 1. kosumu taze degil, K5'ten devralinmis
dosya; `.gitattributes` sozlesmenin `owns` listesi disindaydi, T0 riski olcup kabul etti.

**T189:** alti muhurlu borc — `oynatici-en` yuk altinda 1/14, ucuncu `Pulse` cagri yeri
(`MainWindow.axaml.cs:413-418`) kapsamsiz, `Program.cs` BOM tasiyor, kosuma bagli "54",
capraz makine tekrarlanabilirligi, `.sln`.

## Raftakiler

T186 (ayar arayuzu yeniden tasarimi), T187 (tasma teklifi, %3, dort secenek).
T172'nin `QualityFloorTargetMb` (`PlanCalculator.cs:868`) hala kodek kor.
T171'in acik borclari 3-6.

## Bu oturumun ogrettigi iki sey

**Deponun imza kusuru dorduncu kez cikti** ve dorduncusunu T0 uretti: T192 tur 5
sozlesmesine yazdigim 16/108/68 bolmesi hem olculemezdi hem aritmetigi yanlisti. Yapici
reddetti, denetci reddi dogruladi. Sayiyi sozlesmeye yazmadan once bolmenin **sayilabilir**
oldugunu dogrula.

**Worktree'yi denetci icindeyken kaldirdim.** Denetcinin tam suit kosumu bozuldu (toplam
test 1925'ten 1867'ye dustu, 58 test yok oldu). Denetci bunu kendi yakalayip sayilarini
gecersiz ilan etti. `git worktree remove` yalnizca o agactaki butun ajanlar bittikten
sonra.

## Git disindaki dosyalar

`.calisma/` birikintisi (134 MB, cogu `t57`) `D:\!Tmp\Projeler\VidShrink-calisma`
altina tasindi. Rapora giren her sayi zaten depoda: `docs/olcumler/T192-ham/` (17 dosya)
ve `docs/olcumler/T193-ham/` (JSON'lar + 24 stderr dokumu).
