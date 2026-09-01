# Paket — iki iş, arada bekleme yok

İki iş var. Birincisini bitirip ittikten sonra **beklemeden** ikincisine geç.
T0'a dönüp onay bekleme; iki iş de bitince tek raporla dön.

`main`e **birleştirme**. Her iş kendi dalında kalır ve `origin`e itilir.

**Yeni:** CI artık her dal itilişinde koşuyor ve tam süiti koşum kapısından
geçiriyor. `gh run list --branch <dal>` gerçekten sonuç döndürür; itmeden önce koş.

---

## İş 1 — T86 tur 2 (KRİTİK, şimdi başla — `main` bu yüzden kırmızı)

Sözleşme: `.claude/relay/contracts/T86.md` — sondaki "# Düzeltme turu 2" (H1–H6).
Dal: `t86-olcu-yalitimi` sürdürülür (`git switch t86-olcu-yalitimi`).

**H1 KRİTİK — ve 1 Eylül 15:38'de CI'da yeniden düştü** (koşum 33525962057):

    UpdaterTests.TheDeletionStepWaitsOutATransientLock [FAIL]
    Not found: "yeniden denenecek"
    geçici kilit: çıkış 0, 874 ms
    Failed! - Failed: 1, Passed: 910, Skipped: 72, Total: 983

874 ms — kilit ilk denemeden önce serbest kalmış. Ek madde'nin istediği düzeltme
yapılmadı: `UpdaterTests.cs:1076-1083` bırakma iş parçacığı hâlâ `Thread.Sleep(300)`
ile çalışıyor ve `.basladi` işareti `Remove-InstallRoot` **çağrılmadan önce**
yazılıyor. Kilit ölçünün elinde değil, sabit sayacın elinde. Aradaki üç CI koşumu
yeşil geçti — ölçü kararsız, makinenin o anki yüküne bağlı.

Gereken: kilit, günlükte `yeniden denenecek` görülene kadar (ya da silme denemesinin
gerçekten düştüğünü söyleyen bir işaret yazılana kadar) tutulur. Duruma bakılır,
süreye değil. `rg "Sleep\(3" tests/VidShrink.Tests/UpdaterTests.cs` boş dönmeli.

**H2:** rapordaki altı eşzamanlı koşum satırı `Süre: 669,1 sn` biçiminde; `dotnet test`
bu depoda süreyi hiçbir zaman ondalık saniye yazmıyor (aynı dosyanın 51. satırı
`Süre: 7 m 52 s`). Sayıların yanlış olduğu değil, **doğrulanamaz** olduğu söyleniyor.
Altı koşum yeniden yapılır, satırlar araç çıktısından olduğu gibi yapıştırılır, ham
günlüğün yolu yazılır.

Kalanlar sözleşmede: H3 (`ShellMenuTests` yalıtımı zaten vardı, PID eklemek kozmetik),
H4 (`Global\` kilit + `AbandonedMutexException`), H5 (`TEMP`/`TMP` süreç genelinde),
H6 (koşum kapısının üç kör noktası — `Konak işleminden beklenmedik şekilde çıkış
yapıldı` deseni yok; son-eşleşme semantiği ilk `Başarısız: 5`'i kaçırıyor; CI adımında
kapının çıkış kodu 66 yerine 1 olarak bildiriliyor).

`owns` genişletildi: koşum kapısının dosyaları ve raporun kendisi artık senin.

---

## İş 2 — T87 tur 3 (birinciden hemen sonra)

Sözleşme: `.claude/relay/contracts/T87.md` — sondaki "# Düzeltme turu 3" (I1–I6).
Dal: `T87-tepe-tavani-ve-psy` sürdürülür.

Tur 2'nin asıl maddesi G1 **kapandı**: tek birleşik `-x265-params` dizgesi hem psy/AQ
hem HDR yan verisini taşıyor, denetçi gerçek ffmpeg 9.0 + x265 4.3 ile ayırt edici
değerlerle doğruladı. O tarafa dokunma.

**I1 KRİTİK:** boyut garantisi hâlâ koşan hiçbir ölçüyle bağlı değil.
`FfmpegArgumentsTests.cs:216-222` bayt bayt değişmedi ve `Clamp` çıkışlı bir
fonksiyonun `Clamp` sınırlarına `InRange` sokuyor — tanım gereği yanlışlanamaz.
Rapor boşluğu `HardwareRateControlTests.LiveFast...`e havale ediyor ama o test
`[LiveSourceTheory]`, `VIDSHRINK_LIVE_SOURCE` yoksa **atlanıyor**; raporun "63/63"
dediği koşumda gerçek sayı 63 başarılı / 2 atlanan / 65 toplam ve atlananlar tam da
iddiayı taşıyan iki test.

Gereken: canlı kaynak gerektirmeyen, `Clamp` sınırlarına değil **formülün şekline**
bakan bir ölçü — monotonluk, bilinen iki taban oranı arasındaki fark, diz noktasının
konumu. Mutasyonla kanıtla: `PeakRateFactor` formülünü boz, kırmızıya döndüğünü
göster, geri al.

Kalanlar sözleşmede: I2 (önizleme psy/AQ'yu görmüyor — `PanelHost.cs:224` ve
`SegmentEncoder.cs:200` `availability` geçirmiyor), I3 (yoklama arayüz iş parçacığına
düştü, ~360 ms nvenc planında), I4 (üç rapor cümlesi veriden güçlü), I5 (muhasebe).
I6 borç, bu turda kapanması beklenmiyor.

---

## İki işte de geçerli kurallar

- Kendi worktree'nde çalış. Paylaşılan çalışma ağacına (`Desktop/Projeler/Vidshrink`)
  yazma, orada `dotnet test` koşturma.
- Hiçbir assertion gevşetilmez, hiçbir test `Skip`e alınmaz. **Atlanan test bir
  iddianın dayanağı olamaz.** Hiçbir beklenti `Clamp` sınırından, bir sabitten ya da
  ölçümün kendi çıktısından türetilmez.
- **Sabit karşılaştıran ölçü davranış ölçüsü sayılmaz.** Mutasyon üretim davranışını
  bozmalı, sabiti değil.
- **Araç çıktısı yeniden yazılmaz** — rapora olduğu gibi yapıştırılır ve ham günlüğün
  yolu yazılır.
- Kod yorumu yazma. Mevcut yorumları koru; kod değişirse üstündeki cümleyi ona uydur.
- Ara dosyalar `.calisma/` altına; iş bitince kendi bıraktığını sil.
- Tam süiti kapıdan geçir:
  `powershell -NoProfile -ExecutionPolicy Bypass -File tools/kosum-kapisi/kosum-kapisi.ps1 -MinimumTotal 950 -OutputFile .calisma/tam-suit.txt`
- İtmeden önce `gh run list --branch <dal>` koş. Yerel yeşil CI yeşili değildir.
- Rapordaki her sayı ölçümden gelir. Ölçmediğin şey için "ölçülmedi" yaz.
