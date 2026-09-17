# HandBrake Dalga 2 — Üçüncü Danışma Sorusu

Tarih: 17 Eylül 2026. Önceki yanıtlar: `.calisma/danisma/hb2-yanit.md` (Soru 3 doygunluk kuralı), `hb2b-yanit.md`.

## Olgu 1 — Taban tespiti düzeltildi, amaçlanan hücre düzen kolu olmadan geçmiyor

Koşum 35170987175 (commit 8c481e45): `karanlik` 100 kbit e0 (`--no-resolution-drop --no-fps-drop`) 98k/84k/74k →
0,137/0,134/0,134 MB, çıktı yok. Kök neden: `AtEncoderFloor(previous, sample)` yalnız bir önceki örneğe bakıyordu,
`Correct` adımları ~%14, %20 eşiğine komşu çiftte hiç varılmıyor. Düzeltme (d3330294):
`Saturation.FloorReference(earlier, later) => earlier.FirstOrDefault(s => AtEncoderFloor(s, later))`, ardışık 2-pass
koşusu (araya başka kip girerse sıfırlanır) üstünde. Birim testi + mutasyon kontrolü (yalnız son örnek → test kırıldı).
Ayrıca `--no-resolution-drop` kolunda `LayoutStepMinHeight` null ve `-an` → `StepLayoutDown` null döner; hb.ps1'e
`e0-duzen` kolu eklendi (`--force-codec libsvtav1 --no-fps-drop`).

## Olgu 2 — Koşum 35173328586 (commit d3330294 + origin/main birleşmesi, c2435429 değil)

karanlik (hedef MB 100→0,117 / 300→0,352 / 1200→1,406):
- e0 100: çıktı yok (beklenen: düzen kapalı, ses yok). e1 100 ve 300: çıktı yok.
- **e0-duzen 100: 1. denemede bantta, 1036x442, 0,118 MB, CAMBI 7,47.** Plan kendisi küçük düzen seçti; taban adımı
  hiç tetiklenmedi (ilk deneme bantta).
- e0-duzen 300: 1 deneme, 1842x784, bantta (negatif: 300'de tek deneme). e0 300: 1 deneme 1920x818.
- 1200 üç kol 1 deneme bantta.

rampa (titreşimli yapay rampa, SVT oran denetimi kararsız):
- e0 1200 (düzen kapalı): 1174k→1,931 tavan üstü | 855k→1,478 tavan üstü | 814k→1,059 "under band accepted" (%75).
- **e0-duzen 1200: 1174k→1,931 tavan üstü | 855k→2,005 tavan üstü → "encoder floor, the layout steps down" (1558x876)
  | 855k→0,116 "the encoder did not answer the bitrate" | 1002k→0,116 "saturated, under band delivered" (%8,3).**
- e0 ve e0-duzen 100: aynı (0,028, doygun teslim). 300: aynı 3 deneme bantta.

Yani varsayılan ürün yolunda (çözünürlük düşürme açık) yapay rampada taban kuralı yanlış pozitif verdi: istek %27
düştü, bayt %3,8 **arttı** (1,931 → 2,005), `bytes held ≥ %95` koşulu sağlandı. Düzen inince kodlayıcı çöktü (0,116)
ve ürün, düzen inmeseydi alacağı %75 yerine %8,3 teslim etti.

## Sorular

1. **Taban kuralına "bayt artmadı" koşulu** (`later.ActualMb <= earlier.ActualMb` ya da küçük bir tolerans) girsin mi?
   Gerekçem: gerçek tabanda istek düşerken bayt sabit ya da yavaş iner; %27 kesintide artış oran denetimi kararsızlığı.
   Ölçülmüş tüm gerçek taban örnekleri: 0,137→0,134→0,134; 0,392→0,392→0,391; 0,396→0,391→0,391 (hepsi azalmayan değil,
   artmayan). Tolerans gerekir mi (VT deterministik değil, ama VT'de 2-pass yok)?
2. **Düzen adımından sonra çöküş** (ölü verim <0,5) olursa ne yapılmalı: adım öncesi düzene dönüp aynı isteği mi denemeli,
   yoksa mevcut ölü verim kuralı (bölme/ikiye katlama → Saturated) yeterli mi? Kural 1 girerse bu durum rampada hiç
   oluşmaz; ayrı bir koruma istenir mi, yoksa ölçülmemiş durum için kod eklemek mi yanlış?
3. Kabul ölçütü (CI): önceki ölçüt "bantlasma karanlik 100 e0 → ≤0,117 MB, ≤4 deneme, iz 'taban → düzen'". e0 (düzen
   kapalı) kolu yapısal olarak geçemez; e0-duzen planla geçti ama taban adımını çalıştırmadı. Taban → düzen yolunu gerçekten
   koşan bir CI hücresi gerekir mi (ör. `--no-resolution-drop` yok ama plan 1920x818'de başlasın diye daha yüksek hedef?),
   yoksa birim testi + TasmaKarariTests (gerçek ffmpeg, 4 sn kaynak, iz dalı "encoder floor, the layout steps down")
   yeterli mi? Açık 5'i hangi cümleyle kapatırsın/açık bırakırsın?

Kısa yanıt: her soruya hüküm + CI ölçütü (sayı ile). Kod yazma, ölçüm koşma.
