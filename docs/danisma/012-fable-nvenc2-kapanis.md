# Danışma 012: Soru: NVENC 2 satırı kapansın mı?

claude-fable-5-1, 2.913 çıktı token, 55 sn. Girdi: [012-fable-nvenc2-kapanis-girdi.md](012-fable-nvenc2-kapanis-girdi.md). Cevap olduğu gibi:

---

**Cevap: (a).**

Açık artık ölçümün ayırt edebileceği yerin altında: hevc/av1'de −0,10..−0,22 VMAF-NEG, 9 hücrelik havuzun kendi içi dağılımından küçük ve her ölçer sürümünde sabit ofsetle kayabilen bir sayı; kimse fark etmez, kapı da bunu "geride" diye yazamaz. Dokuz ayrı kol aynı sonucu verdi (bayt tarafsız kollar sıfır, kaliteyi kaldıran tek kol b_ref each'i baytı bozuyor, p7 iki turda kapıyı geçemedi); bu tutarlılık kalan açığın bir bayrak değil x265 psy ayarlaması ile donanım kodlayıcı arasındaki yapısal fark olduğunu söylüyor, bir kol daha dizmek turu satın alır ama bilgi almaz. Kullanıcının "her alanda geçilsin" hedefi bu satırda karşılandı sayılır: h264'te +2,32 açık ara önde, hevc/av1'de ölçüm sınırı içinde eşit — satır "kapandı: h264 önde, hevc/av1 eşit (≤0,22, algılanabilir değil)" diye kapatılır. Tek istisna p10 kolu: hevc'i +0,057'ye çeken o yol bu satırın değil, "10 bit çıkış ürüne girsin mi" diye ayrı bir ürün kararının konusu; istenirse defterde yeni satır olarak açılır, bu satırı bekletmez.

Yok

result: Danışma 012 cevabı (a) — NVENC 2 satırı kapatılsın: hevc/av1 açığı ≤0,22 VMAF-NEG ölçüm sınırı içinde, h264 önde; p10 ayrı defter satırı olur
