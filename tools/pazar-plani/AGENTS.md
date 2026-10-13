# pazar-plani

Pazar araştırmasının tablolarını sayar; `docs/plan-pazar-eksikleri.md` ile `docs/later.md`
dosyalarını üretir. Python 3, bağımlılık yok. Plandaki her sayı buradan çıkar.

    python tools/pazar-plani/topla.py kur docs/piyasa/2026-10-13 . .calisma/pazar-plani
    python tools/pazar-plani/topla.py girdi docs/piyasa/2026-10-13 .calisma/pazar-plani
    python tools/pazar-plani/topla.py kanit docs/piyasa/2026-10-13 .

- `kur` planı ve rafı yeniden yazar; sayım tablosunu, uyarıları ve `TARAMA` satırını basar.
  `TARAMA 0` değilse commit etme: `DepodaGomuluAnahtarYok` ölçüsü aynı satırda kırılır.
- `girdi` sentez kolunun okuduğu özeti üretir. `kanit` her "bizde zaten var" satırının
  gösterdiği kod satırını basar; satır çıkarılmadan önce gözle okunur.
- Girdi klasöründe kol başına üç dosya: `<ad>.md` notlar, `<ad>-eksikler.md` ham tablo,
  `<ad>-dogrulanmis.md` paketler; yanında `sentez.md`, `kisa-cevap.md`, `t0-kod-okumasi.md`, `ek-kanit.md`, `karar-sorulari.md`.
- Dalga yargı değil kuraldır: paketin kararı, işi ve en yüksek önceliğinden hesaplanır.
- Raf başlığı `## N. Ad`. İşe başlanan paketin başlığı `docs/later.md`'den elle silinir; `kur`
  yeniden koşulursa geri gelir, bu yüzden raf kullanılmaya başladıktan sonra koşma.
- `yazisma.py` oturum kaydından ajan görev metinlerini ve raporlarını döker; yalnız bu makinede koşar.
- Yeni tarama turu yeni tarihli klasör ister: `TARIH`, `SURUM`, `SOZ` sabitleri betiğin içinde.
