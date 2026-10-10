# Düzenleyici Kapanışında Kaynak Kilidi

CI koşumu 38027448669 (Windows, kol ana-gadw) `DuzenleyiciKlipOzellikTests.ArayuzPaneliAyariYazarEdlyiYenidenAcmadanTazeler`
testinde düştü: pencere kapandıktan sonra `Directory.Delete(klasor, true)` "kaynak.mp4 is being used by another
process" verdi. Yeniden koşumda geçti.

## Kimin Tuttuğu

Sonda (2026-10-10, bu makine, Release): bir iş parçacığı `kaynak.mp4`'ü paylaşımsız açmayı dener, açamadığı anda
Restart Manager'a (`RmGetList`, dosyayı tutan süreçleri adıyla veren Windows hizmeti) sorar. Görünüm testteki gibi
kurulur: sahte motor (`KlipMotoru`), `EditorView.StripDisabled` açık (küçük resim ve anahtar kare okuması kapalı).

Tutan tek süreç `ffmpeg`: ses dalgası taraması. `EditorView.OpenSourceAsync` → `LoadPeaks` →
`AudioPeaks.LoadAsync` → `AudioPeaks.ReadAsync` içindeki `process.Start()`. `StripDisabled` bu yolu kapatmıyor,
pencere kapanışı da taramayı iptal etmiyordu.

16 baytlık test kaynağında (10 koşum): kilit `OpenSourceAsync` çağrısından 27-30 ms sonra başlıyor, yaklaşık 10 ms
sürüyor. Test gövdesi bu makinede bundan uzun, o yüzden kusur yerelde çoğalmıyor: düzeltmeden önce 30 koşumda 0 düşüş.

Pencereyi uzatmak için 21,5 MB'lık 2 saatlik kaynak (tarama burada ~1,3 sn), pencere açılıştan hemen sonra kapatılır,
klasör kapanışın ardından silinir. Zamanlar ms, koşumun başından:

| Koşum | Pencere Kapandı | Kilit İlk | Kilit Son | Tutan | Silme |
|---|---|---|---|---|---|
| Önce 1 | 1000 | 743 | 2261 | ffmpeg | IOException: The process cannot access the file 'kaynak.mp4' because it is being used by another process. |
| Önce 2 | 990 | 751 | 2108 | ffmpeg | aynı IOException |
| Önce 3 | 953 | 726 | 2028 | ffmpeg | aynı IOException |
| Sonra 1 | 1045 | 775 | 1125 | ffmpeg | silindi |
| Sonra 2 | 1072 | 799 | 1150 | ffmpeg | silindi |
| Sonra 3 | 895 | 784 | 911 | ffmpeg | silindi |

Önce: 3 koşumda 3 düşüş, CI'daki iletinin aynısı; kilit pencere kapandıktan 1,0-1,3 sn sonra da duruyor.
Sonra: kilit kapanışla birlikte kalkıyor. "Kilit Son" her örnekte Restart Manager çağrısının süresini (10-80 ms)
içerir, kapanıştan sonraki 16-80 ms'lik pay ölçümün kendisidir.

## Düzeltme

`EditorView` bağlandığı pencerenin `Closed` olayında dalga ve anahtar kare taramasını iptal eder (ffmpeg öldürülür).
Sekme değişimi iptal etmez, yalnız pencerenin kapanışı. Taramanın görevi `EditorView.PeakLoad` olarak açıldı; testler
`DuzenleyiciKapanis.Kapat` ile pencereyi kapatıp görevin bitmesini bekler, sonra klasörü siler.

Ölçü `DuzenleyiciKapanisTests.PencereKapanincaKaynagiOkuyanTaramalarIptalEdilir`: `Closed` aboneliği kaldırılınca
kırmızı (1/1). Düzeltmeden sonra aynı döngü: 30 koşumda 0 düşüş.

## Aynı Gün ana-b

Koşum 38025889706'nın düşüşü ayrı: `BicimDisiYazimTests.BicimDisiYazimYok`, `src/VidShrink.Core/SubtitleCharset.cs:33`.
Kaynak taraması, zamanlamayla ilgisi yok; bu dalın tabanında yeşil.
