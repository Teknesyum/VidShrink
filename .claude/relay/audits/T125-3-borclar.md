# T125 denetim borçları

Üç tur döndü. Tur 2 tam denetlendi (GEÇTİ, bir KRİTİK), tur 3 dar kapsamlı
denetlendi (GEÇTİ). Denetçiler bağımsız, opus.

## Tur 2'nin KRİTİK'i — kapatıldı

`docs/olcumler/ab-duzenegi.md:907-910` şunu yazıyordu: "On iki ölçümün on ikisinde
de `SizeEqual` doğru — eş boyut kapısından geçmeyen satır yok."

Yanlıştı. `tools/VidShrink.Ab/AbRunner.cs:118-124` eşitleyiciyi yalnız
`outcomes.Count > 0` iken çağırıyor ve tabanı `outcomes[0].Bytes`ten alıyor; tur 2'nin
`Compatible` koşumları tek yarışmacıyla koşulduğu için eşitleyici hiç çağrılmadı ve her
satırın tabanı kendi baytı oldu. O `SizeEqual=true` kendi kendine kıyastır.

Baytlar HandBrake tabanına karşı yeniden hesaplandığında altı satırın **dördü** ±%2
bandını deliyor (−%5,17 / −%3,19 / −%2,54 / −%2,03). Auto sütunu etkilenmiyor: altı
satırın altısı bandın içinde, en büyük sapma +%1,89 — **manşet ayakta.**

Tur 3'te beyan değiştirildi, tabloya `Compatible Δbayt` ve `Auto Δbayt` sütunları
eklendi, delen dört satır "band delindi" etiketiyle tabloda bırakıldı, "Desen net"
paragrafı bayt payını ayırarak yeniden kuruldu. Δbayt hücrelerinin on sekizi de iki
bağımsız denetimde ham `Bytes`/`BaselineBytes` alanlarından yeniden hesaplandı, on
sekizi de tuttu.

## Kapatılanlar

- **Ölü atıf.** `-%3,69` manşet temizliğinde iki geçişten ikisi birden silinmiş, yerine
  "aşağıya bakın" atfı konmuştu; gösterilen dizi tur 3'ün yoklamasıydı ve o sayı orada
  yoktu. Geri kondu — `205670f`.
- **Künye ifadesi.** "tek bir `vidshrink` ölçümü var" deniyordu; `parca-2-compat` ve
  `parca-3-compat` iki hedef taşıdığı için ikişer ölçüm içeriyor — `d169b28`.
- **Kapıyı susturan backtick'ler.** Dördü kaldırıldı ve sayı gizlenmeden çözüldü: ikisi
  liste maddesine, biri mutasyon tablosuna satır, biri künye listesine. Denetçi eski/yeni
  ondalık kümelerini karşılaştırdı: düşen tek şey `8,5` ve `12,5` yuvarlamaları, tam
  değerleri (`8,54`, `12,53`) tabloda duruyor. Ters yönde de hareket var — mutasyon
  tablosundaki nitel "bayt farkı büyük" ibaresi kalkıp yerine sayısal `+%2,05` sütunu
  geldi.

## Açık kalanlar

1. **Compatible kolu yeniden koşulmadı.** Kusur belgede yazılı ama ölçüm tekrarlanmadı;
   Compatible sütununun puanları HandBrake'e karşı eş boyutta ölçülmüş sayılamaz. Manşet
   Auto sütununa dayandığı için etkilenmiyor. Ayrı iş.

2. **`tools/VidShrink.Ab/AbRunner.cs:118-119` kodda pimlenmedi.** Düzenek tek yarışmacılı
   koşumda hâlâ `SizeEqual=true` ve `fark % = 0,00` üretiyor. T125'in kabul kriterinde yok.

3. **`ab-duzenegi.md:913-915` kapsam gevşekliği.** "Tur 2'nin JSON'larında on beş ölçüm
   var" diyor; dizinde `k6-parca-2-zorunlu1080p.json` ile birlikte 16 ölçüm bulunuyor.
   Parantezdeki "5 satır × 3 sütun" kapsamı tabloya daraltıyor, o kapsamda sayı doğru.

4. **`Competitors.cs:87-88` `T125_YERLESIM_KILIT` ortam değişkeni.** K6 için gerekliydi,
   varsayılanı kapalı (`AllowResolutionDrop = !LayoutLocked`, eski davranış birebir), ama
   sürekli araçta kalıcı ölçüm anahtarı bırakıyor ve `--yardim` çıktısında yok.

5. **Satır sonu karışıklığı.** `ab-kodek-kolu.md` CRLF, `ab-duzenegi.md` LF.

## Denetçinin kendiliğinden yaptığı ek kontrol

Aynı belgede 12 satır yukarıda ikinci bir "on iki satırın on ikisinde de `SizeEqual`
doğru" cümlesi var (`:895-897`). Aynı kusurun ikizi olabilir diye bakıldı:
`sonuc-parca-60-ikiyebolme.json` ve `sonuc-parca.json`'ın 600 MB bölümü iki yarışmacılı
koşumlar, her `vidshrink` satırının `BaselineBytes`i HandBrake baytı. **O cümle sağlam.**
