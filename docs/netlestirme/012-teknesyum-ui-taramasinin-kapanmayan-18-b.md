# Netleştirme: teknesyum-ui taramasinin kapanmayan 18 bulgusu icin ne yapalim: kurallari eklent

- tarih: 2026-09-12
- girdi: 012-teknesyum-ui-taramasinin-kapanmayan-18-b-girdi.md
- maliyet: 54735 tokens, 19 s

## Dönen

Soruda belirsiz kalan yerler ve her biri için tek satırlık netleştirme sorusu:

1. Karar kapsamı — Hüküm yalnız bu 18 bulgu için mi verilecek, yoksa aynı kusuru taşıyan
   her kural için genel bir politika mı isteniyor?

2. Yolların birleşebilirliği — Üç yol (kuralı yamala / muafiyet ekle / gürültü kabul et)
   birbirini dışlıyor mu, yoksa bulgu ailesi başına ayrı yol seçilebilir mi?

3. Muafiyetin biçimi — Muafiyet yolu `.claude/teknesyum-ui.json` içinde dosya/kural listesi
   mi olacak, kaynak içine satır işareti (`<!-- ui-ignore -->`) mi, ikisi birden mi?

4. `background-gradient` ölçütü — Kural on bir *durak* mı istiyor, on bir *farklı renk* mi;
   aynı renkli durakların sayılmaması istenen davranış mı?

5. Kabul edilebilir bakiye — Tarama sayacının sıfıra inmesi şart mı, yoksa gerekçesi
   yazılmış bir bakiye (ör. 1 bulgu) kabul mü?
