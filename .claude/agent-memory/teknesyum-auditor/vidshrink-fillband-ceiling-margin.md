---
name: vidshrink-fillband-ceiling-margin
description: T3 fill-band alt-taşma düzeltmesi tavana sıfıra yakın payla dayanıyor — T4/T5'te tekrar kontrol edilmeli
metadata:
  type: project
---

T3 (Doluluk bandı / FillBand) turunda `PlanCalculator.Correct(..., fillUnderBand: true)`
(`src/VidShrink.Core/PlanCalculator.cs:249-260`) sonucu bandın altında kaldığında bitrate'i
`bandCenterMb/actual` oranıyla yukarı çekiyor ve `videoBudgetKUp` ile sınırlıyor. Bu üst sınır
`targetMb * ContainerOverhead` (yani hedefin ~%99,5'i) — pratikte payı yok.

Buna karşılık aynı fonksiyonun üst-taşma dalı (`Correct(..., fillUnderBand: false`, satır
262-268) `desiredMb = targetMb * CrfFitMargin (0.94)` kullanıyor, yani hedefin altında %6
güvenlik payı bırakıyor.

**Neden önemli:** İki geçişli VBR'ın gerçek çıktısı istenen ortalama bitrate'i aşabiliyor —
zaten bu yüzden `EncodeRunner`'da `over` kontrolü ve yeniden deneme var. Yukarı yönlü düzeltme
sıfıra yakın payla çalıştığından, alt-taşma tekrarı sonrası gerçek dosya hedefi (`targetMb`,
sert tavan) aşabilir. `MaxAttempts=3` alt-taşma ve üst-taşma denemeleri arasında paylaşıldığı
için, alt-taşmadan harcanan bir deneme üst-taşma düzeltmesine daha az hak bırakıyor;
`attempt >= MaxAttempts` durumunda dosya `over` olsa bile olduğu gibi teslim ediliyor
(`EncodeRunner.cs:53-59`, T3'ten önce de var olan "attempts tükenirse teslim et" kaçış kapısı,
T3 onu değiştirmedi ama tetiklenme ihtimalini artırdı).

**Nasıl uygula:** T4/T5'te `FillBand`/`Correct` alanına dokunan her sözleşmede bu asimetriyi
kontrol et — alt-taşma düzeltmesinin üst sınırı hâlâ `targetMb` (payı yok) mu, yoksa
[[vidshrink-fillband-ceiling-margin]] bulgusuna göre güvenlik payı eklendi mi. T3 denetiminde
gerçek 5 ölçümün hiçbiri alt-taşma tekrarını tetiklemedi (hepsi ilk denemede banda düştü), bu
yüzden risk gözlemlenmedi, sadece koddan çıkarıldı — canlı bir teyit yok.
