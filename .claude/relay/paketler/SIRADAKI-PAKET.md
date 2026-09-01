# Sıradaki dağıtım — iki iş, sırayla. Aralarında bana dönme.

Kural: birincisini bitirip ittikten sonra **beklemeden** ikincisine geç. Denetimi ben
paralel koşturacağım; sonucu sana ayrıca gelir, beklemek zorunda değilsin.

---

## İş 1 — T87 düzeltme turu 2

Sözleşme: `.claude/relay/contracts/T87.md`, sondaki **"# Düzeltme turu 2"** bölümü.
Önce `git pull`.

Bağımsız denetim **KALDI** verdi. Ölçümlerin kendisi sağlam — tablodaki sayılar gerçek
argümandan doğrulanabiliyor — ama **ölçülen şey sevk edilen şey değil**.

**G1 (KRİTİK)** — HDR + libx265'te psy bayrakları sessizce düşüyor. `FfmpegArguments.cs:180`
`-x265-params psy-rd=2:...` ekliyor, `HdrResolver.cs:48` ikinci bir `-x265-params`
üretiyor ve sonra ekleniyor; ffmpeg'de son yazan kazanıyor. Ölçüldü:

    psy sonra hdr : psy-rd=2.00  (varsayilan — psy yok sayildi)
    hdr sonra psy : psy-rd=0.50  (uygulandi)

Yani sözleşmenin çıkış noktası olan 1080p60 HDR kaynakta libx265 psy/aq-mode hiç
çalışmıyor. Sırayı ters çevirmek çözüm değil — o zaman HDR meta verisi düşer.
İki parametre tek dizgide birleştirilecek.

G2–G6 sözleşmede: totolojik boyut garantisi testi, arayüzün gösterdiği komutun koşan
komut olmaması, kalibrasyonun bayrakları görmemesi, veriden güçlü rapor cümleleri.

Dal: `T87-tepe-tavani-ve-psy`. Bitince it, `main`e **birleştirme**.

---

## İş 2 — T86 (İş 1 biter bitmez başla)

Sözleşme: `.claude/relay/contracts/T86.md`, sondaki **"## Ek madde"** dahil.

Üç ölçü süreçler arası paylaşılan duruma bakıyor ve eşzamanlı koşumda rastgele düşüyor.
Dördüncüsü bugün `main`de CI'da kırmızı verdi:
`UpdaterTests.TheDeletionStepWaitsOutATransientLock` — silme adımının geçici bir kilide
takılıp yeniden denemesini bekliyor, CI'da kilit ilk denemeden önce serbest kaldı.
Ölçü bir davranışı değil bir zamanlamayı sınıyor.

Dal: `T86-olcu-yalitimi`. Bitince it, `main`e **birleştirme**.

---

## İkisi için de geçerli

- Kendi dalında çalış, paylaşılan çalışma ağacına (`Desktop/Projeler/Vidshrink`) yazma.
- Hiçbir assertion gevşetilmez, hiçbir test `Skip`e alınmaz, hiçbir beklenti ölçümün
  kendi çıktısından türetilmez.
- Her düzeltme için mutasyon; sonucu `docs/olcumler/` altına yaz.
- Tam süit bir kez koşulur. Çıktıda kesinti satırı varsa çıkış kodu 0 ve `Başarısız: 0`
  olsa bile koşum yarımdır — raporda toplam test sayısını yaz.
- **İtmeden önce `gh run list --branch <dal>` koş.** Yerel yeşil, CI kırmızı olabiliyor;
  bu projede oldu.
- Ölçmediğin şey için "ölçülmedi" yaz. Kazanç iddiası yazma.
