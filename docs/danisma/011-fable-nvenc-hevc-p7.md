# Danışma 011: Soru: hevc_nvenc'te preset p4 -> p7 ürüne alınsın mı?

claude-fable-5-1, 3.703 çıktı token, 45 sn. Girdi: [011-fable-nvenc-hevc-p7-girdi.md](011-fable-nvenc-hevc-p7-girdi.md). Cevap olduğu gibi:

---

**Cevap: (b).**

**Neden (a) değil:** hevc-yalnız kapı, aynı 9 hücrenin verisi görüldükten sonra yazılıyor. "8/9, kayıpsız" eşiği sonucu bildikten sonra seçilmiş bir eşiktir; ölçüm hipotezi üretmiş, aynı ölçüm onu doğrulamış sayılıyor. Kapı ilkesinin tam çiğnediği şey bu. Hücre sayısı da az: 9 hücre, 3 kesit — içerik çeşitliliği iki-üç örnekten ibaret.

**Neden (c) değil:** Kapı hevc aleyhine kanıtla düşmedi, tasarımla düştü. İki kodek tek havuza atılmıştı ama uygulanan müdahale aynı değildi: hevc'te p4→p7 üç basamak, av1'de p6→p7 tek basamak. av1'in sıfıra yakın çıkması beklenen bir şeydi ve hevc'in 8/18'lik payını yarıya düşürdü. Etki büyük (+0,07..+0,61), tek hücrede bile kayıp yok, mekanik açıklaması var. Bunu çöpe atmak da veriyi yanlış okumaktır.

**(b) nasıl yapılır ki kaydırma olmasın:**
- Kapıyı **yeni kesitler ölçülmeden önce** yaz ve commit'le; eşiği bu turun sayısından bağımsız koy (ör. ≥7/9 hücrede ≥+0,10, hiçbir hücre < −0,10, HandBrake'e karşı hevc hücrelerinde ≥ tabanın oranı).
- İçerik farklı olsun: bu turun 3 kesitinden hiçbiri girmesin; hareket/gren/düz-yüzey karışımı.
- **Hızı ürün içinde değil, saf kodlayıcıda da kapıya al.** ×1,71..1,79 zaten 2,0 sınırına yakın; ürün içi ×0,95 sayısı 4 çekirdekte FFV1 çözme darboğazına yaslanıyor. Ham H.264/HEVC girdi veya daha çok çekirdekli makinede o darboğaz kalkar ve p7'nin süresi kullanıcıya görünür. Bir hücre darboğazsız girdiyle ölçülmeli.
- Düşerse kapanır, tekrar açılmaz; geçerse ürüne hevc için p7 girer, av1 p6'da kalır.

Kısacası: sonradan daraltılan kapı geçersizdir, ama sonradan **öğrenilen hipotez** yeni bir ön-kayıtlı kapıyla test edilir. Bedeli bir ölçüm turu; kazancı kararın savunulabilir olması.

Yok
