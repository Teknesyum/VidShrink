# Yalan Yok, CLI Ve Çalıştırıcı: Mutasyon Dökümü

Kaynak: `.calisma/yalan-yok/envanter.md` maddeleri 4, 11, 12, 23, 24, 26, 31, 35, 38, 50, 51.
Her mutasyon kaynağa elle (dize değiştirme) uygulandı, derlendi, filtre koşuldu, dize geri yazıldı
(`git checkout` kullanılmadı). Taban: `YalanYokCliTests` 34/34 yeşil.

| Madde | Mutasyon | Kırmızı dönen |
|---|---|---|
| 50 | `--aci`/`--angle` kolu geri eklendi | `AciBayragiKaldirildi` |
| 11 | uzantı denetimi `false &&` ile kapatıldı | `BilinmeyenCiktiUzantisiReddediliyor` (3 kol) |
| 12 | en yardımına `_shrunk.mp4` geri yazıldı | `YardimCiktiUzantisininSabitOlmadiginiSoyluyor` |
| 51 | izle ayrımı `false &&` ile kapatıldı | `IzleTekDosyaSecenegineGecersizDiyor` (6 kol) |
| 23/24 | kodlayıcı yedek notu süzgeci boşaltıldı | `PlanKodlayiciYedeginiInsanDiliyleYaziyor`, `KucultMetniGercekKodlayiciyiVeYedekNedeniniYaziyor` |
| 23 | "Kodlayıcı:" satırı istenen kodeği yazıyor | ilk turda yeşil kaldı (testte iki kodek aynıydı); test düzeltildi, `KucultMetniGercekKodlayiciyiVeYedekNedeniniYaziyor` |
| 4 | kopyalama notu hep `null` | `KopyalamaKaynaginKabindaKaldiginiSoyluyor` |
| 26 | CLI `progress.encode` "in this attempt"siz | `KalanSuresiDenemeyeAitOldugunuSoyluyor` |
| 26 | de `main.output.remaining` "Verbleibend" | `KalanSuresiDenemeyeAitOldugunuSoyluyor` |
| 26 | tr `main.stage.attempt` "Tur" | `AsamaMetniDenemeyiSoyluyor` |
| 26 | ar etiketi "المتبقي من هذه المحاولة" | `CiktiOlgulariAyniSatirdaAyniYukseklikte(ar, dar)` (ızgara tek sütuna iniyor) |
| 31 | CLI metni teslim denemesini yazmıyor | `KucultMetniTeslimEdilenDenemeyiAyriYaziyor` |
| 31 | en küçük aşımın `DeliveredAttempt`'i silindi | yeşil kaldı: `CeilingGuardTests` senaryosunda en küçük sonuç son deneme; bu kol kırmızıya çevrilemedi |
| 31 | bant altı yedeğin `DeliveredAttempt`'i silindi | `KosucuTeslimEdilenDenemeninNumarasiniTasiyor(0.06, 8, medium)` |
| 31 | reddedilen bütçe doldurmada teslim numarası silindi | `KosucuTeslimEdilenDenemeninNumarasiniTasiyor(0.2, 800, ultrafast)` |
| 35 | en `main.run.over-ceiling`'e "never" geri yazıldı | `HedefUstuAslaVerilmezDenmiyor` |
| 38 | başarıda boş durum | `BaglantiKopyalanincaBasariSoyleniyor` |
| 38 | hatada önek düşüyor | `PanoYazamazsaHataSoyleniyor`, `PanoYoksaSessizKalinmiyor` |

Koşucu senaryoları (320x240 testsrc, 4 sn, x264 `-threads 1`) yerel keşifle bulundu. Hedef 0,06 MB
ve 8k medium: 1 ve 2. deneme bant altı, 3. deneme aşıyor, 2. deneme teslim ediliyor (3 deneme).
Hedef 0,2 MB ve 800k ultrafast: 1. deneme bantta, doldurma küçük çıkıyor, 1. deneme teslim ediliyor (2 deneme).

Madde 26, Türkçe etiket: dar pencerede çıktı ızgarası iki sütunu ancak "Kalan" ile tutuyor; "Bu denemede
kalan", "Denemede kalan", "Deneme kalanı", "Kalan (deneme)", "Denemede" beşi de ızgarayı tek sütuna indirdi
(`CiktiOlgulariAyniSatirdaAyniYukseklikte(tr, dar)` kırmızı). Ölçü değişmez kuralıyla tr "Kalan" kaldı; denemeyi
hemen üstteki aşama hücresi söylüyor ("Geçiş 1/2 (Deneme 2)"). en ve de dar pencerede tabanda da tek sütun.
