# Renk Eşleme

20 Ekim 2026 · dal `worktree-agent-later-88-89` · later 88 (DZB-38)

## Yöntem

- Klip `fps=8/süre,scale=160:-2,[renk zinciri],format=gbrp,showinfo` ile sekiz karede yoklanır
  (`ClipMatch.ProbeArguments`, ffmpeg `-threads 2`). showinfo düzlemleri G, B, R sırasıyla verir.
- Kareler kanal kanal birleşir: ortalama karelerin ortalaması, varyans ort(sd² + m²) − m².
- Kanal başına kazanç a = σref / σhedef (0,25 ile 4 arasında kırpılır), kayma b = (μref − a·μhedef) / 255.
- Doğrusal eşleme `colorlevels` ile yazılır ve klibin renk zincirinin başına konur; taşan uç
  giriş aralığı daraltılarak kırpılır (`ClipMatch.Levels`).
- Referans kendi renk zinciriyle, hedef ham yoklanır; eşleme hedefin öteki renk ayarlarının önünde durur.

## Ölçüm

Test: `RenkEslemeTests.EslemeIkiKlibinOrtalamaVeSapmaFarkiniKucultur`. Kaynaklar `testsrc2` 160x120,
10 fps, 2 sn, yuv444p kayıpsız.

- Referans: `eq=contrast=0.6:brightness=0.08,colorbalance=rm=0.25:bm=-0.2`
- Hedef: `eq=contrast=1.3:brightness=-0.1,colorbalance=bm=0.3:gm=0.1`

| | Ortalama R | Ortalama G | Ortalama B | Sapma R | Sapma G | Sapma B |
|---|---|---|---|---|---|---|
| Referans | 135,13 | 133,63 | 134,88 | 101,65 | 91,36 | 109,76 |
| Hedef | 93,50 | 93,13 | 101,25 | 103,07 | 108,61 | 103,15 |
| Eşlenmiş hedef | 131,63 | 132,50 | 131,50 | 96,94 | 90,32 | 105,80 |

- En büyük ortalama farkı: 41,63 → 3,50 (11,9 kat küçüldü).
- En büyük sapma farkı: 17,25 → 4,71 (3,7 kat küçüldü).
- Üretilen süzgeç: `colorlevels=rimin=0:rimax=0.8433:romin=0.1683:romax=1:gimin=0:gimax=0.9311:gomin=0.2168:gomax=1:bimin=0:bimax=0.8398:bomin=0.1064:bomax=1`

Testin eşiği: ortalama farkı dörtte birin, sapma farkı üçte birin altına inmeli. Sapma farkı tam
kapanmaz, çünkü kazanç birden büyük olunca uçlar 0 ve 255'te kırpılır.

## Mutasyon

`ClipMatch.Match` içinde kazanç 1'e sabitlendi: sapma farkı 17,25 → 9,09 kaldı, ölçüm testi ve
`EslemeHedefinIstatistiginiReferansaTasir` kırmızı oldu. Elle geri yazıldı.

## Açık Kalanlar

- Eşleme kanal başına doğrusal; ton eğrisi (histogram) eşlemesi yok.
- Kırpılan uçlar yüzünden çok parlak ya da çok koyu referansta sapma tam tutmaz.
