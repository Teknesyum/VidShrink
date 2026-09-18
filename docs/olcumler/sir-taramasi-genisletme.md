# Sır Taramasının Genişletilmesi (18 Eylül 2026)

`DepodaGomuluAnahtarYok` iki kör noktayla başladı: kapsam `src/**/*.cs` ile sınırlıydı ve
desen değerin tırnak içinde olmasını şart koşuyordu. İkisi de kapandıktan sonra geriye
üçüncü bir körlük kalmıştı: desen **tek anahtar adına** (`Api-Key`/`apiKey`) bağlıydı.
Belirteç, parola ve `Authorization: Bearer` satırı depoya tarama hiç bakmadan girebilirdi.

## Ne değişti

1. **Kapsam artık git'in izlediği dosyalar.** Sızıntı ancak commit'lenmişse sızıntıdır.
   Klasör gezen sürüm `.claude/oturumlar/` altındaki gitignore'lu oturum günlüklerini de
   tarayıp 13 yabancı `toolu_…` satırını suçlu bildiriyordu. `git ls-files` çıktısı
   boş dönerse ölçü "temiz" demeden `izlenen.Count > 500` kapısında düşer.

2. **Üç biçim tanınıyor.** (a) JWT — `eyJ` ile başlayan üç noktalı yapı, tetik sözcük
   gerektirmez çünkü biçimin kendisi kimliktir; (b) `Api-Key`, `apiKey`, `api_key`,
   `secret`, `token`, `bearer`, `password`, `parola` geçen bir satırda en az 24 karakterlik
   hem harf hem rakam taşıyan dizi; (c) alan adları rakamsız olduğu için elenir.

## Tarama bunu bulunca ne oldu

Genişletilmiş sürüm ilk koşumunda iki gerçek bulgu verdi:

- `docs/arastirma/opensubtitles-istemci-kutuphaneleri.md:64` — subliminal kasetinin
  `Authorization: Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9....` satırı. Yükü elenmiş
  olsa da aynı kasetin `Api-Key` satırı gerçekti; maskelendi.
- `tests/VidShrink.Tests/AltyaziOturumTests.cs:416` — kasıtlı sahte pim belirteci.
  Pozitif kontrollerdeki gelenekle aynı biçimde kaynakta iki parçaya bölündü.

## Mutasyon

İzlenen bir belgeye iki sahte sır eklendi (`git`e girecek bir dosya), tam
`dotnet build VidShrink.sln -c Release -warnaserror -m:2` ile derlendi, sonra koşuldu:

```
Collection: ["docs\\arastirma\\opensubtitles-istemci-kutuphanele"···, "docs\\arastirma\\opensubtitles-istemci-kutuphanele"···]
Başarısız! - Başarısız:     1, Başarılı:    64, Atlanan:     0, Toplam:    65
```

Kanıt dosyası suçluları adıyla yazdı:

```
docs\arastirma\opensubtitles-istemci-kutuphaneleri.md: Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.QWERTY
docs\arastirma\opensubtitles-istemci-kutuphaneleri.md: password: Hy7kL9mQ2wRt5vXz8bNc4dFg
pozitif kontrol: True
tel kaydi: True
ayar alani: False
```

Mutasyon `git checkout` ile değil, önceden alınan kaynak kopyasından geri yazıldı; geri
yazma sonrası:

```
Başarılı!  - Başarısız:     0, Başarılı:    65, Atlanan:     0, Toplam:    65
```

`AltyaziIndirmeTests` + `AltyaziOturumTests` birlikte 91/91 yeşil.

## Açık kalan

Tarama satır bazlı: çok satıra bölünmüş bir sır (kaynakta `+` ile birleştirilen iki parça)
yakalanmaz — pozitif kontrollerin kendisi tam da bu boşluğu kullanıyor. Kapatmak için
dosyayı satır satır değil tek dizge olarak taramak gerekir, o zaman da kontroller
suçlu sayılır; ayrım için ayrı bir işaret gerekir.
