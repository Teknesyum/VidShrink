# 019 — Yarım Kayıt Metni: Girdi

Danışma tarihi 18 Eylül 2026. Danışılan: fable. Aşağıdaki metin ajana **birebir** verildi.

---

VidShrink, ekran kaydı da yapan bir video aracı. Kayıt bitince sonuç panelinde dosyanın
yolu ve bir durum satırı görünüyor. Üç durum var: tamamlandı, **yarım kaldı**, başarısız.

"Yarım kaldı" (`RecordResult.Partial`) şu demek: durdurma isteği geldi, ffmpeg nazikçe
kapanmadı ve zaman aşımından sonra **öldürüldü**.

Bugünkü metin (tr): "Kayıt yarıda kesildi ve dosya kapanmadı; oynatılamayabilir."

Önceki danışmanın 3. adımı bu metni "Kayıt yarım kaldı — dosya duruyor, oynatılabilir"
yapmamı söylemişti. Ölçüm bunu **yanlışlıyor**, ama yarısı doğru. Ölçülen olgular:

- Öldürülen kaydın dosyası her koşulda diskte durur, `OutputPath` doludur.
- Kap **Matroska** ise ve `-flush_packets 1` verilmişse dosya oynatılabilir: 640x480
  gdigrab kaydı 7 sn sonra öldürüldüğünde 80 KiB ve 76 okunur paket. Bayrak olmadan
  aynı dosya **0 bayt** kalıyordu. Bayrak bugün Matroska'ya her zaman veriliyor.
- Kap **mp4/mov** ise dosya oynatılamaz: muxer `moov` atomunu kapanışta yazıyor,
  ölçülen örnekte dosya 48 bayt kaldı ve ffprobe "moov atom not found" ile 1 döndü.
- Kod bu ayrımı zaten bir yordamda tutuyor: `RecorderArguments.SurvivesKill(container)`,
  bugün yalnız Matroska için doğru. Varsayılan kap Matroska.
- Arayüz dosyanın yolunu elinde tutuyor, yani hangi kapta olduğunu bilebiliyor.

Sorum: kullanıcıya ne yazmalı?

1. Tek metin mi kalsın (her iki kabı da kapsayan, doğru ama belirsiz), yoksa kabına göre
   **iki ayrı metin** mi olsun (`SurvivesKill` ile seçilen)?
2. Seçtiğin biçim için Türkçe ve İngilizce metinleri yaz. Metin 42 dile çevrilecek, yani
   deyimden uzak ve kısa olsun.
3. Kullanıcı kolaylığı ön planda: bu satırı okuyan kişi **ne yapacağını** bilmeli.
   Oynatılabilir dosyada bir sonraki adım ne, oynatılamayanda ne?

Kısıt: uyarı ile hata arasındaki ayrım renkle değil biçimle taşınıyor (üçgen-ünlem simgesi
+ SemiBold). Metin bu ayrımı tek başına taşımak zorunda değil ama onunla çelişmemeli.
