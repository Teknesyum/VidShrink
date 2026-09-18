# 023 — Altyazı Varsayılan Bayrağı ve İz Adı: Danışma Girdisi

Fable'a verilen metin, birebir:

---

VidShrink'in küçültme kolu izleri `StreamMapping` ile eşliyor. HandBrake'in
`--subtitle-default`, `--srt-default` ve `--subname` karşılığı bizde yok; `-disposition`
yalnız ses tarafında yazılıyor (`StreamMapping.cs:85`, tek ses izi varsa
`-disposition:a:0 default`). Kaynağın `IsForced` alanı okunuyor ama hiçbir karara girmiyor.

Bugün ölçtüm (`docs/olcumler/e6-altyazi-bayragi-iz-adi.md`), 2 sn'lik kaynakta ses izinin
başlığı "Ana Ses", gömülü srt'nin başlığı "Zorunlu TR" ve `forced` bayrağı açık:

- MKV çıktı: iki başlık da korunuyor, ses `default=1`, altyazı `default=0`, `forced=1`.
- MP4 çıktı: iki başlık da **düşüyor**, üç izin üçü de `default=1` (mp4 muxer'ının kendi
  varsayılanı, bizim kararımız değil), `forced=1`.

Küçültmede kullanıcıya iz kutusu yok: kap uzantıdan türüyor, ses izi dil yeğlemesiyle
seçiliyor, altyazılar bütçeye sığdığı kadar taşınıyor. Kullanıcı kolaylığı ön planda;
yeni açılır kutu açmak son çare.

Sorular:

1. İz adının MP4'te düşmesi düzeltilmeli mi, yoksa "MP4'te iz adı zaten az oynatıcıda
   görünür" deyip bırakılmalı mı? Düzeltilecekse `-metadata:s:a:0 title=` ile kaynağınki
   aynen mi taşınsın?
2. Altyazının MP4'te istenmeden `default=1` olması düzeltilmeli mi? Doğru kural ne:
   hiçbir altyazı varsayılan olmasın mı, yoksa kaynağın `IsDefault`'u mu izlensin,
   yoksa `forced` olan tek altyazı varsayılan mı olsun?
3. Kullanıcıya bu iki şey için bir yüzey (kutu, bayrak) açılmalı mı, yoksa karar
   tamamen kaynaktan mı türesin?

Kısıt: yeni ölçüm uydurma yok, renk/ölçü uydurma yok. Yanıt Türkçe, kısa.
