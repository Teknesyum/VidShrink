# Duzenek — ne olcer, nasil olcer

Kod yorumu yasak; duzenegin gerekceleri burada durur.

## Kollar

`Kollar` uretimin kodek tercihlerini tasir.

| Kol | Tercih | Cikan kodlayici | Neden var |
|---|---|---|---|
| `maks` | `MaxCompression` | `libsvtav1` | Uretim varsayilani |
| `uyumlu` | `Compatible` | `libx264` | Uyumluluk kolu |
| `yedek` | `MaxCompression`, `libsvtav1` gizli | `libx265` | K4 `libsvtav1`in `zones`u sessizce yok saydigini gosterdi; sikistirma ailesinde zone denenebilen tek kodlayici bu yoldan cikar |

`SvtavYok` sarmalayicisi yalniz `IEncoderAvailability`yi ezer; argüman uretimi
gercek yeteneklerle calisir.

## Referans (hak edilen)

Her sahne ayri ayri, sabit `-crf 26` ile, **planin cozunurlugunde** kodlanir.
Olceklendirme sart: `p1` penceresinde plan `806x454`e dusuyor, referans
`1920x1080` kalirsa hak-edilen ile verilen farkli cozunurlukten okunur.

`verilen` sutunu plan ciktisinin sahne araligina dusen paket bitleridir —
kodlayicinin kendi karari. `harita` sutunu `Scene.Bits` paylaridir.
Uc sutun da paydir; toplam boyut farki karsilastirmayi bozmaz.

## Is parcacigi sabitleme

`IsParcacigiArgs` kodlayici basina dogru bayragi secer: x265 `pools`,
x264 `threads`, SVT-AV1 `lp`. Yanlis bayrak ffmpeg tarafindan **uyariyla**
atilir, hata vermez — pinleme sessizce kaybolur.

## K4: cikis kodu destek demek degil

SVT-AV1 ve x264/x265 parametre ayristiricilari tanimadiklari anahtari uyariyla
gecer, cikis kodu 0 doner. Bu yuzden her hucre **iki farkli degerle** kodlanir
ve cikti baytlari karsilastirilir; ayrica ayni parametreyle iki kosumdan
kodlayici basina bir **tekrar gurultusu** olculur. Destek ancak
`fark > gurultu x 2` ve `fark > cikti/100` iken yazilir.

## Zone carpani

`Butce.ZoneCarpanlari` = `Complexity^gamma`, sure agirlikli ortalamasi 1,0'a
normalize, `[0,25 – 4,0]` kiskacinda. Normalizasyon K6'nin sarti: dagitim
bitleri yeniden bolusturur, toplami degistirmez.

`gamma = 1 - qcomp` (varsayilan `qcomp = 0,60`, yani `gamma = 0,40`).
x264/x265 iki gecis hiz denetimi bitleri karmasikliga `qcomp` ussuyle dagitir;
harita tam oranli dagitim onerir (us 1,0). Zone carpani aradaki `1 - qcomp`
farkini kapatir. **Telafi sabiti degil** — kodlayicinin belgelenmis ussuyle
haritanin onerisi arasindaki fark.

## Bozuk harita

`KesimDusur(n)` her n'inci kesimi atar (eksik kesim). `KesimEkle` her sahneyi
ortasindan ikiye boler (fazla kesim). Ikisinde de sahne ici bit yogunlugu duz
kabul edilir; sahne parcalandiginda bit sureyle bolunur.

## Rapor

`Rapor.cs` sayfayi tumuyle olculen JSON/CSV dosyalarindan uretir. Bu projenin
on sekiz kez tekrarlayan kusuru "tablo dogru, onu ozetleyen cumle yanlis"tir;
sayfadaki her sayi ve her karar sozcugu hesaplanir, elle yazilmaz.

## Kosum

    bash tools/sahne-butcesi/01-olcumu-kos.sh

## Ara ciktilar atlanir

Her asama hedef dosyasi varsa atlar. Yeniden olcmek icin o dosyalari silin.
**Uretim kaynagi degistiginde hepsini silin**: `src/` degisirse plan degisir
ve eski cikti yanlis commit'i olcer. T114'te bir kez oldu — `origin/main`
birlestirilince (T107) ayni pencerenin plan cozunurlugu `806x454`ten
`1458x820` / `1728x972` / `1920x1080`e cikti; birlestirme oncesi olculen
butun K1 sayilari silinip bastan kosuldu.
