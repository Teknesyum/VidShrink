---
name: owns-disina-yazmadan-once-sor
description: Kanıt owns dışındaki bir dosyayı gösterdiğinde iş durur ve T0 kapsamı genişletir — kendi başına yazma
metadata:
  type: feedback
---

Sözleşmenin `owns` listesi dışındaki bir dosyayı düzeltmen gerektiği ortaya çıkarsa
dosyaya dokunma: `status: blocked` yap, "T0 kararı gerekli: <dosya>, <neden>" yaz, dur.

**Why:** T0 `owns` listesini sık sık kanıta değil tahmine göre kuruyor (T62, T65, T66'da
üç kez üst üste), yani kusurun gerçek yeri listede olmayabilir. Doğru olan yazıp sonra
haber vermek değil, durup kapsamı T0'a genişlettirmek. T66'da doğru yerde düzeltme
yaptım ama sormadım; sonuç kabul edildi, kural değişmedi.

**How to apply:** Kusuru bulduğun an dosyayı `owns` ile karşılaştır. Liste dışıysa
ölçümü tamamla (kanıt değerli), düzeltmeyi yazma, bloke ol. T0 kapsamı genişletince
devam et.
