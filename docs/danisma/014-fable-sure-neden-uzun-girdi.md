# Danışma 014 girdi: Soru

Ajana giden metin:

---

[[danisma:014]]

# Soru
Kullanici: "ufacik bi kurulum dosyasi degistiriyoruz, ufacik bir readme satiri degistirecez, neden bu kadar uzun suruyor?"
Kucuk bir degisikligin (kurucu dosyasi + README satiri) yayina ~30-45 dk surmesinin gercek nedeni ne ve en etkili kisaltma ne?

# Olgular (olculmus)
- Proje: .NET 8 + Avalonia video araci. Tek test projesi ~4900+ test.
- Push CI (ci.yml): her push'ta 8 paralel parca (windows-latest), her parca ayri ffmpeg+libmpv kurar, tum sinifi derler, bir test dilimini kosar. main CI 36120718858: 13.1 dk duvar saati. En uzun parca ana-kalan ~12 dk.
- ci.yml paths-ignore: .claude/**, docs/**, **/*.md -> sadece README degisirse CI hic kosmaz. Ama kurucu kodu + README ayni commit'te -> tam CI.
- Yayin (release.yml) 0.9.3: 17.6 dk; bunun 12.4 dk'si ayni commit icin testleri IKINCI kez kosmakti. Bugun eklendi (ad70e5d0): minor/yamada ayni commit CI yesilse yayin testleri atlar -> yayin ~3.6 dk.
- Proje kurali: main'e yalniz T0 birlestirir, main CI yesili teslimin sarti, surum yalniz main CI yesilken. Dokunulan alanin testleri yerelde yesil olmadan teslim yok.
- Olcum: kurucu alanina dokunan 18 sinifi (Kurucu*, Installer, Shell*, Updater, OluUye, BelgeBasliklari, ShareFlow, KulturTuzakTeli, MacOs*, DosyaIliski, Kabuk*, OynaticiKurulum) filtreleyen tek parca yerelde 305 test, 3 dk 29 sn (derleme dahil 3 dk 42 sn).
- Plan: ci.yml'e "alan" isi: degisen dosyalarin hepsi kurucu alanindaysa (src/VidShrink.Setup/, src/VidShrink.Core/Setup/, Install-VidShrink.ps1, install-vidshrink.sh, ilgili test dosyalari, Directory.Build.props'ta yalniz Version satiri, .md) matrix tek "kurucu" parcaya iner; aksi halde tam 8 parca. Major surum tam suit. Tahmin: CI ~13 dk -> ~5-6 dk, toplam minor ~10 dk.
- Onceki turda (0.9.3) ek sure kaynaklari: 7z/tar hatasi teshisi, deps release'e zip yukleme, iki README baslik esitligi testi kirildi, yayin testleri tekrar kosti. Ajan tarafinda kurulum paneli ayri isi arka planda suruyor.

# Istenen
1) Kullaniciya 3-4 satirlik durust aciklama: sure nereye gidiyor.
2) Plan dogru mu, risk nedir (kurucu disi kod kirilip gozden kacar mi), daha iyi bir yol var mi (or. main'de tam, dalda alan)?
