# 024 — Küçültmede İnce Ayar Yüzeyi (E1/E5): Fable'ın Yanıtı

Girdi: [024-ince-ayar-yuzeyi-girdi.md](024-ince-ayar-yuzeyi-girdi.md). Yanıt birebir:

---

**1. Yalnız `-tune` panele girsin; `-level:v` ve serbest dize kapalı kalsın.** `-tune` kalite algısına doğrudan etki eden, üç kodlayıcıda da (x264/x265 `film|animation|grain`, SVT-AV1 `0..3`) sınırlı ve sayılabilir bir küme; mevcut ön ayar açılırının yanına aynı biçimde otururken yeni bir kutu türü açmıyor ve `LockedTune` olarak plan seçeneklerine aynı yoldan pimlenir. `-level:v` ise bir kalite kolu değil, uyumluluk kısıtı: hedef cihaz sorulmadan anlamı yok ve yanlış seçilince ffmpeg planı sessizce başarısız kılar, kullanıcı kolaylığı kısıtıyla çelişir. Serbest kodlayıcı dizesi de "kullanıcı kolaylığı ön planda, testle pimlenmeli" ilkesinin tam tersi — sonsuz uzayı mutasyonla pimleyemezsin, `-crf` ile kilitlerin çakışmasını da denetleyemezsin; LLM süzgecinde kalması yeterli.

**2. İkisi de kullanıcıya açılmasın; `-ar` hedef bit hızından türesin, `-q:a` hiç dokunulmasın.** Hedef boyuta sıkıştıran bir araçta ses kbit'i zaten kullanıcının kararı; düşük kbit'te (≤48) 48 kHz'i 24-32'ye indirmek deterministik bir kural ve tek bir tabloyla testlenir, kullanıcıya sormaya değmez. `-q:a` ise değişken bit hızı demek — hedef boyut hesabını öngörülemez kılar ve `LockedAudioKbps` ile aynı anda tutarsızdır; projenin özüne aykırı bir kol, açılmamalı.

**3. Asimetri kapatılsın, ama ayna değil, mevcut CLI dilinde: yalnız `--crf` ve `--on-ayar` eklensin.** Bu ikisi arayüzde en sık dokunulan kilitler ve CLI'ın betiklenebilirlik gerekçesini taşıyanlar; plan seçenekleri zaten `LockedCrf`/`LockedPreset` tuttuğu için tek bağlantı noktası eklenir, testi arayüzle ortak koşar. Kodlayıcı yolu, ses kanalı, tune gibi kolları CLI'a taşımak dar bayrak setini şişirir; `--kodek` zaten var, ötesi taleple gelsin. CLI'nın "daha dar" kalması doğru bir tercih ama arayüzün ana kolu CLI'da tamamen yoksa bu darlık değil eksiktir.
