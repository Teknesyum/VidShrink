# 021 — `izle` Harf Çarpışması: Fable'ın Yanıtı

Girdi: [021-izle-harf-carpismasi-girdi.md](021-izle-harf-carpismasi-girdi.md). Yanıt birebir:

---

**1. Hangisi: 2 (atla ve söyle).**
- Kısıtın "kullanıcı kolaylığı": 3 tüm koşuyu bir tek çakışma yüzünden durdurur, klasördeki diğer 50 dosya da bekler; 1 doğru ama iki katmanlı iş (bekleyen + işlenmiş kaydı + çıktı adı ayrımı) ve çıktı adını nasıl ayıracağın ikinci bir tasarım sorusu doğurur — Linux'ta harf ayrımı yoksa uzantı öncesi ek gerekir, o ek de başka bir dosyayla çakışabilir.
- 2, kusurun asıl zararını (sonsuz bekleme, hiçbirinin küçülmemesi) kapatır: ilki küçülür, `--bir-kez` çıkar, kullanıcı ne olduğunu ve ne yapacağını görür.
- Çakışmanın sebebi kullanıcının klasörü; kararı ona bırakmak doğru. Aracın "birine ad uydurması" sessiz sürpriz yaratır.
- Deterministik seçim şart: sıralı ad (ordinal) küçük olan işlenir, öbürü atlanır; her taramada aynı dosya kazansın, yoksa tekrar sıfırlanma geri gelir.

**2. Kullanıcı ne görüyor.**
- Anı: çakışma bekleyenler tablosuna alınırken, ilk taramada (kararlılık beklenmeden). Bir kez yazılır; sonraki taramalarda aynı çift için tekrar yazılmaz, aksi halde günlük dolar.
- Satır, `StatusWarning` ağırlığında: `Atlandı: klip.mp4 — Klip.mp4 ile ad çakışıyor (harf farkı). Birini yeniden adlandırın.`
- Çıkış kodu `--bir-kez`te: işlenen dosyalar başarılıysa **4 (kısmi)**, çünkü iş bitti ama eksik — projenin "StatusWarning = iş bitti ama eksik" kuralıyla bire bir. Çıkış özetinde bir satır: `1 dosya atlandı (ad çakışması)`. Sürekli kipte çıkış yok, yalnız günlük satırı.
- Kullanıcı dosyayı yeniden adlandırınca izleyici bunu yeni dosya olarak görür ve olağan akıştan küçültür; ek komut gerekmez.

**3. Windows/macOS.**
- Davranış değişmez, ama kod dallanmasın: kontrol her platformda çalışsın, tetiklenmesi yalnız Linux'ta mümkün olur. Platform bayrağı yerine "aynı taramada iki ayrı dizin girdisi aynı kanonik ada iniyor mu" sorusu sorulursa kod tekdir ve macOS'ta harf duyarlı biçimlendirilmiş APFS bölümünde de (var, seyrek) kendiliğinden doğru çalışır.
- Test Windows'ta gerçek dosyayla kurulamaz; tarama sonucunu sahte girdi listesiyle besleyen birim testi yeter.
