# Danışma 010: Soru: Ön ayar profilindeki kap (Container) alanı uygulansın mı, nasıl?

claude-fable-5-1, 2.315 çıktı token, 34 sn. Girdi: [010-fable-onayar-kap-girdi.md](010-fable-onayar-kap-girdi.md). Cevap olduğu gibi:

---

**Öneri: B.** Mp4/Mov/Mkv uygulanır, WebM Mp4'e düşer ve bunu içe aktarma notu söyler.

**Gerekçe**

Alan bugün bir vaat: HandBrake'ten `av_mkv` profili getiren kullanıcı profil kartında "Mkv" görüyor, çıktı Mp4 çıkıyor. Bu "vaat edilen = yapılan" ilkesini zaten ihlal ediyor; A seçeneği ihlali kaldırır ama kullanıcının elinden gerçek bir tercihi de alır. Kap seçimi kullanıcı kolaylığı açısından ucuz ve anlaşılır bir tercihtir — dosya uzantısından ibaret görünür, sonucu anında doğrulanabilir.

B'nin maliyeti düşük: kodlama aşaması kabı zaten çıktı uzantısından okuyor (`ContainerOf`), MOV ailesi MP4 gibi davranıyor. Yani yeni kodlama yolu yok; yalnız iki yerde varsayılan uzantı profilden gelir (CLI `--cikti` yoksa, pencerede küçültme çıktısı). `--cikti` verilmişse kullanıcının uzantısı kazanır — açık istek, profilin sessiz tercihini ezer.

Mkv'nin "izleri koru"yu açmaması doğru: kap ile iz politikası ayrı kararlar; profil kabı söylüyor, izleri söylemiyor. Bağlanırsa kullanıcı istemediği bir davranış değişikliği alır ve neden olduğunu göremez.

C reddi: WebM için vp9 merdivende yok, AV1+opus kolu yeni kodlama yolu ve ölçüm ister. Talep ölçülmüş değil (gömülü 42 profilin hiçbiri WebM taşımıyor, yalnız HandBrake içe aktarımında çıkabilir). Bu iş kap alanını uygulama kararına asılmaz; ayrı sözleşme olur, WebM içe aktarımı görülürse açılır.

**Dürüstlük koşulu (B'nin parçası):** WebM profili içe aktarılınca not açıkça "kap WebM desteklenmiyor, Mp4 kullanılacak" demeli ve profil kartında Mp4 görünmeli; alan sessizce düşürülmez.

Doğrulama: `src/` altında `\.Container\b` taraması `PresetProfile.Container` okuyan tek satır göstermedi; girdideki "hiçbir yolda okunmuyor" olgusu tutuyor.

Yok
