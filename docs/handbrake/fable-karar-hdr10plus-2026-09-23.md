# Fable Kararı — HDR10+ Köprüsü (23 Eylül 2026)

Danışman: fable. Girdi: `docs/olcumler/hdr10plus-tasima.md` (sarmalayıcılardan 0/3, `dhdr10-info` ile 3/3).
Metin kelimesi kelimesine:

Karar: Köprüyü kur, yalnız x265 üzerinden ve HDR10+ kaynakta kodeği x265'e yönlendirerek. SVT-AV1 kolu için köprü yok, orada gerekçe satırı kalır.

1. Tetik: yalnız kaynakta HDR10+ SEI ölçüldüğünde çözme geçişi koşar; HDR10 veya SDR kaynak bu koddan hiç geçmez. Geçiş ilerleme çubuğunda ayrı adım olarak görünür.
2. Kodek yönlendirme: HDR10+ kaynakta HdrResolver x265 seçer; kullanıcı SVT-AV1'i açıkça seçtiyse köprü kapanır ve gerekçe 'HDR10+ SVT-AV1 kolunda taşınmaz' olur.
3. Kapama koşulları: trim veya fps değişimi varken köprü kapalı, gerekçe 'HDR10+ kesitte düştü'. Kare hizalaması ayrı bir turun işi; ölçülmeden açılmaz.
4. İki geçişli kodlama: her iki geçişe aynı dhdr10-info verilir, çıkışta ffprobe ile kare başına HDR10+ sayılır; sayı kaynağa eşit değilse iş uyarıyla (StatusWarning) biter.
5. Doğrulama ölçüsü: sentetik kaynakta JSON alan değerleri kaynak SEI ile bire bir karşılaştırılır (yalnız kare sayısı değil), Windows yolu ':' kaçışıyla test edilir; ölçüm docs/olcumler/hdr10plus-tasima.md'ye eklenir.
