# Bileşen Sürüm Kütüğü

Kaynak: Teknesyum-UI rafı, `bilesen-surumleri` kitabı. Birden çok Teknesyum programı aynı
paylaşılan bileşeni (libmpv, ffmpeg, eklenti, şablon, ortak DLL) kullandığında her biri kendi
başına indirip üstüne yazmaz; tek bir kütük dosyası tutulur:
`%LOCALAPPDATA%\Teknesyum\bilesenler.md` (macOS/Linux'ta `~/.local/share/teknesyum/`).

## Satır biçimi

`| bileşen | sürüm | sha256 | yol | kuran program | tarih |` — bileşen başına bir satır.

## Açılışta kıyaslama

1. Program kendi istediği sürümü kütükle karşılaştırır.
2. Aynıysa ve sha256 tutuyorsa paylaşılanı kullanır, indirmez.
3. Kütükteki daha yeniyse ve uyumluluğu biliniyorsa kullanır.
4. Çakışma (eski, uyumu bilinmeyen ya da sha tutmayan sürüm) sessizce değiştirilmez;
   kullanıcıya "güncelle / yan yana kur / dokunma" seçenekleriyle sorulur.
5. Yazan program satırı kendi adı ve tarihiyle günceller; yan yana kurulumda ikinci satır
   sürümlü yolla eklenir.

## VidShrink'in durumu

VidShrink bugün `ffmpeg`/`ffprobe`'u (`tools/ffmpeg/`) ve `libmpv`'yi (`tools/libmpv/`,
bkz. kök `AGENTS.md`) kendi başına, kendi sha256 pini ve kendi indirme yoluyla getirir;
başka bir Teknesyum programıyla ortak bir kütük dosyasını henüz okumaz ya da yazmaz. Bu
depoda bugüne kadar VidShrink'ten başka bir Teknesyum masaüstü programı libmpv/ffmpeg
paylaşmadığı için çakışma hiç yaşanmadı ve paylaşılan kütüğe ihtiyaç doğmadı.

Standart uygulanabilir kabul edilir (proje libmpv/ffmpeg kullanıyor), ama kütüğü okuyup
yazan kısım `src/VidShrink.Ffmpeg` ve `src/VidShrink.Player`'ın kurulum/yoklama koduna
dokunan bir değişikliktir — bu iş paketinin belge/yapılandırma kapsamı dışında. İkinci bir
Teknesyum programı libmpv ya da ffmpeg paylaşmaya başladığında bu kütük burada anlatılan
biçimde devreye alınmalı; o zamana kadar bu belge yalnız standardı ve mevcut boşluğu
kaydeder.
