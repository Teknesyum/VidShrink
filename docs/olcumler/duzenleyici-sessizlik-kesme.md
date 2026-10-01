# Düzenleyici — Otomatik Sessizlik Ve Siyah Kare Kesme

Tarih: 2026-10-01. Dal: `worktree-agent-a776ee630e59c9b35`. Test sınıfı: `tests/VidShrink.Tests/DuzenleyiciSessizlikTests.cs`.

## Canlı Ölçüm

Kaynak: 6 sn 320x240 lavfi (gri görüntü, 440 Hz ses), 1,5–3 sn ses sıfır, 3–4,5 sn görüntü siyah.
Tarama tek süreç, `-threads 2`, BelowNormal, `silencedetect=noise=-35dB:d=0.1` ve
`blackdetect=d=0.1:pic_th=0.98:pix_th=0.10`. Kesim ayarı: en kısa 0,5 sn, kenar payı 0,1 sn.

| Ölçü | Beklenen | Bulunan | Fark |
| --- | --- | --- | --- |
| Sessiz aralık | 1,500–3,000 | 1,515–3,008 | 0,015 / 0,008 |
| Siyah aralık | 3,000–4,500 | 3,000–4,520 | 0,000 / 0,020 |
| Birleşik kesim (pay sonrası) | 1,600–4,400 | 1,615–4,420 | 0,015 / 0,020 |
| Kesimden sonra çizelge süresi | 3,200 | 3,195 | 0,005 |

Kabul: her sınır ±0,1 sn içinde. İlerleme 1'de bitti ve azalmadı; süreç önceliği BelowNormal okundu.
Yalnız görüntülü kaynakta sessizlik taraması boş döndü (hata değil), iptal süreci öldürüp `OperationCanceledException` verdi.

ffmpeg'in bu kaynakta yazdığı satırlar (ayrıştırıcı testinde pimli):

```
[Parsed_silencedetect_0 @ 00000206645fcec0] silence_start: 1.514667
[Parsed_silencedetect_0 @ 00000206645fcec0] silence_end: 3.008021 | silence_duration: 1.493354
[Parsed_blackdetect_0 @ 00000206646626c0] black_start:3 black_end:4.52 black_duration:1.52
```

## Mutasyonlar

Taban: 10/10 yeşil. Her mutasyon tek satır, derlenip yalnız `DuzenleyiciSessizlikTests` koşuldu, sonra geri konuldu.

| Mutasyon | Kırmızı | Kırmızı testler |
| --- | --- | --- |
| M1 birleştirme kaldırıldı (`span.Start <= prev.End` → `false`) | 2/10 | Plan, Canlı |
| M2 sol kenar payı kaldırıldı | 3/10 | Plan, Arayüz, Canlı |
| M3 öncelik BelowNormal yerine Normal | 1/10 | Canlı |
| M4 tek geri alma adımı yerine adım adım kayıt | 2/10 | Model (bölme/silme), Arayüz |
| M5 eşik değişimi bayat saymıyor (`SameScan`) | 2/10 | Ayar, Arayüz |
| M6 dosya başında da pay bırakılıyor | 1/10 | Plan |

## CI'nın Yakaladıkları

| Koşum | Kırmızı | Düzeltme |
| --- | --- | --- |
| 36790285304 | Yerleşim denetimi: 1024 px'te "Sessizlik" düğme metni araç çubuğundan taştı (ru, ta, bg, nl) | Düğme simgeli oldu (`IconVolumeMute`), metin ipucuna taşındı |
| 36791426549 | Kontrast: 11/36 açık palette onay kutusu işareti 1,14–1,36 | İki kutuya `CheckStyle` teması |

## Bilinen Sınırlar

- Metin katmanları kesimle kaymaz; kesilen aralığın içindeki ya da arkasındaki metin eski zamanında kalır.
- Siyah kare eşiği (`pix_th=0.10`, `pic_th=0.98`) sabit, panelde ayarı yok.
- Sessiz ve siyah aralıklar birleşim olarak kesilir (ikisinden biri yeterli), kesişim kipi yok.
- Tarama kaynak dosyada koşar; aralıklar kaynak saniyesidir, çizelgede artık olmayan kısım kesilmez.
