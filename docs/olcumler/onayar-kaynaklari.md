# Ön Ayar Kaynakları

Dal `t0/hb-a2-onayar`, 17 Eylül 2026. Veri `src/VidShrink.Core/Presets/platformlar.json`
(gömülü kaynak), okuyan `PresetLibrary`. Yonga şeridi (`MainWindow.ChipPlans`) de aynı tablodan
okur; ikinci tablo yok.

Durum sütunu: **resmi** = değer aşağıdaki resmi yardım sayfasından okundu (kontrol tarihi
17 Eylül 2026). **kodda** = resmi kaynak bulunamadı ya da okunamadı; değer bu işten önce
koddaki değerdir, değiştirilmedi. `OnAyarKutuphanesiTests` bu tablonun her satırını JSON ile
karşılaştırır.

Birim: VidShrink'in MB'ı yongalardaki gibi MiB'dir. "2 GB" gibi GB değerleri 1000 MB sayıldı.

| id | değer | durum | kaynak | not |
|---|---|---|---|---|
| archive | hedef yok | kodda | — | boyut tavanı yok, kalite tavanı |
| size-8 | 8 MB | kodda | — | genel yonga; ipucu Discord'u anmıyor (ücretsiz sınır 20 MB, bkz. discord-free) |
| whatsapp-chat | 16 MB | kodda | https://faq.whatsapp.com/164676891531296/ | SSS sayfası betikle çiziliyor, metni WebFetch ve curl ile okunamadı; 16 MB doğrulanmadı |
| size-25 | 25 MB | kodda | — | genel yonga |
| size-100 | 100 MB | kodda | — | genel yonga |
| share-uguu | 128 MB | kodda | — | `paylasim-hedefleri.json` uguu.se `maxBytes` 134217728 = 128 MiB (ölçülmüş tavan) |
| whatsapp-web | 180 MB | kodda | — | ipucuna göre kullanıcı bildirimi, WhatsApp yayımlamıyor |
| half | kaynağın yarısı | kodda | — | kaynaktan türetilir |
| discord-free | 20 MB | resmi | https://support.discord.com/hc/en-us/articles/25444343291031-File-Attachments-FAQ | "As of August 2026, the free upload limit is 20MB (up from 10MB)"; iş tanımındaki 10 MB eski değer |
| discord-nitro-basic | 50 MB | resmi | https://support.discord.com/hc/en-us/articles/25444343291031-File-Attachments-FAQ | "Nitro Basic offers 50MB"; sayfa 403 veriyor, metin Zendesk API'sinden okundu |
| discord-nitro | 500 MB | resmi | https://support.discord.com/hc/en-us/articles/25444343291031-File-Attachments-FAQ | "Nitro offers up to 500MB" |
| telegram | 2000 MB | resmi | https://telegram.org/faq | "files (doc, zip, mp3, etc.) of up to 2 GB each"; Premium 4 GB alınmadı |
| email-gmail | 25 MB | resmi | https://support.google.com/mail/answer/6584 | "For personal Gmail accounts, the limit is 25 MB." |
| email-outlook | 20 MB | resmi | https://support.microsoft.com/en-us/office/reduce-attachment-size-to-send-large-files-with-outlook-8c698842-b462-4a4c-8d53-5c5dd04f77ef | "For internet email accounts such as Outlook.com or Gmail, the email size limit is 20 megabytes (MB)." (Outlook masaüstü) |
| device-chromecast-gen1-2 | 720 kısa kenar | resmi | https://developers.google.com/cast/docs/media | "H.264 High Profile up to level 4.1 (720p/60fps or 1080p/30fps)"; kare hızı tavanı motora taşınmadı, 720p60 kolu seçildi |
| device-chromecast-gen3 | 1080 kısa kenar | resmi | https://developers.google.com/cast/docs/media | "H.264 High Profile up to level 4.2 (1080p/60fps)" |
| device-nest-hub | 720 kısa kenar | resmi | https://developers.google.com/cast/docs/media | "H.264 High Profile up to level 4.1 (720p/60fps)" |
| device-apple-tv-hd | 1080 kısa kenar | resmi | https://support.apple.com/en-us/111928 | "H.264 video up to 1080p, 60 fps, High or Main Profile level 4.2 or lower" |

## Bilinen Boşluklar

- WhatsApp sohbet tavanı (16 MB) resmi kaynaktan doğrulanamadı; kodda kaldı.
- `PlanOptions`'ta kare hızı tavanı yok; cihaz profilleri yalnız kısa kenarı ve H.264'ü taşır.

## HandBrake Çevirisi

Test verisi `tests/VidShrink.Tests/Veri/handbrake/sentetik-sosyal-10mb-720p.json`: HandBrake ön ayar JSON
şemasının alan adlarıyla elle yazılmış sentetik dosya ("Sentetik Sosyal 10 MB 720p"). Değerler bizim seçimimiz;
HandBrake kodundan ya da verisinden metin alınmadı.

Taşınan: `PresetName`, `PictureWidth`/`PictureHeight` (kısa kenar), `FileFormat`. Yaklaşık:
`VideoEncoder` (aile → kodek tercihi), `VideoQualityType` (2 = kalite tavanı), `AudioList` (ilk izin
bit hızı), addaki "N MB" → hedef boyut. Düşen: `VideoAvgBitrate`, `VideoQualitySlider`, çok geçiş,
`VideoPreset`, kare hızı (motor karar verir), filtreler ve altyazı (karşılığı yok), klasör alanları
(yapı). Her üst alan için tam bir not üretilir.

## Mutasyonlar

24 mutasyon, her biri tek başına uygulanıp derlendi ve dört sınıfın filtresiyle koşuldu (30 test), sonra geri alındı.
Hepsi kırmızı döndü; 30 testin her biri en az bir mutasyonda kırmızı.

| mutasyon | kırmızıya dönen |
|---|---|
| discord-free 20 → 10 | PlatformTavanlariResmiKaynaktan, CihazProfiliCozunurlukTavaniTasir, BelgeTablosuVeriyleAyni |
| addaki `MB` deseni `GB` | SentetikOnAyarHedefTabanliPlanaCevrilir, TasinanVeDusenAlanlarRaporlanir, CiftAnahtarTekNotVerir |
| şema tavanı 99 | BozukDosyaAnlasilirHataVerir |
| bilinmeyen alan reddi (`Disallow`) | DisaIceAktarmaGidisDonusVeBilinmeyenAlanYokSayilir |
| kullanıcı yolu `%APPDATA%` | KayitVarsayilanAyarKlasorunuIzler |
| ChipPlans'a sabit satır | YongaPlanlariTablodanOkunur + 4 YongaPlanTests |
| targetMb doğrulaması yok | KaydetYukleGidisDonusVeAyniIdDegistirir, BozukDosyaAnlasilirHataVerir |
| klasörler de çevrilir | KlasorCevrilmez, SentetikOnAyar…, TasinanVeDusen… |
| x264 → Auto | SentetikOnAyarHedefTabanliPlanaCevrilir |
| de dilinden bir anahtar | HataVeNotAnahtarlari42Dilde |
| kısa kenar min → max | AddaBoyutYoksaKaliteTavani, SentetikOnAyar… |
| HandBrake dosyası algılanmaz | BozukDosyaAnlasilirHataVerir |
| ayrılmış id reddi yok | YerlesikIdIceAlinamaz |
| çift anahtar atlanmaz | CiftAnahtarTekNotVerir |
| aynı id büyük/küçük harf duyarlı | KaydetYukleGidisDonusVeAyniIdDegistirir |
| kalite kipinde boyut tavanı | AddaBoyutYoksaKaliteTavani |
| FixedResolution taşınmaz | CihazProfiliCozunurlukTavaniTasir, SentetikOnAyar… |
| whatsapp-chat 16 → 17 | YongaDegerleriDegismedi, BelgeTablosuVeriyleAyni, YongaPlanTests |
| telegram `checked` silindi | ResmiKaynakliHerDegerinAdresiVeTarihiVar |
| gömülü kaynak derlemeye girmez | GomuluKaynakDerlemeyeGirer + 21 test |
| bilinmeyen kap Mp4 sayılır | DesteklenmeyenKodlayiciVeKapDuser |
| boş listede hata yok | GecersizGirdiAnlasilirHataVerir |
| JSON'da Chip8 intent SocialMedia → Sharing | YongaPlanlariTablodanOkunur (YongaDegerleriDegismedi yeşil kaldı) |
| sentetik veride AudioBitrate 128 → 160, PictureHeight 720 → 1080 | SentetikOnAyarHedefTabanliPlanaCevrilir, TasinanVeDusenAlanlarRaporlanir |
